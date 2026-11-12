using System.Text;
using EnviroPi.Display.Models;
using EnviroPi.Display.Services;
using EnviroPi.Display.Workers;
using Iot.Device.Button;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EnviroPi.Tests.Display;

public class ButtonWorkerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Each_press_switches_the_display_page()
    {
        var display = Substitute.For<IDisplayService>();
        var button = new TestButton();
        var logger = new ReadyLogger();
        using var worker = new ButtonWorker(17, display, logger, _ => button);

        await worker.StartAsync(CancellationToken.None);
        await logger.Ready.WaitAsync(Timeout);
        button.Click();
        button.Click();
        await worker.StopAsync(CancellationToken.None);

        display.Received(2).ShowNextPage();
    }

    [Fact]
    public async Task Uses_the_configured_gpio_pin()
    {
        int? requestedPin = null;
        var button = new TestButton();
        var logger = new ReadyLogger();
        using var worker = new ButtonWorker(27, Substitute.For<IDisplayService>(), logger,
            pin => { requestedPin = pin; return button; });

        await worker.StartAsync(CancellationToken.None);
        await logger.Ready.WaitAsync(Timeout);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(27, requestedPin);
    }

    [Fact]
    public async Task Releases_the_button_when_stopped()
    {
        var button = new TestButton();
        var logger = new ReadyLogger();
        using var worker = new ButtonWorker(17, Substitute.For<IDisplayService>(), logger, _ => button);

        await worker.StartAsync(CancellationToken.None);
        await logger.Ready.WaitAsync(Timeout);
        await worker.StopAsync(CancellationToken.None).WaitAsync(Timeout);

        Assert.True(button.IsDisposed);
    }

    [Fact]
    public async Task Keeps_the_service_running_when_the_gpio_is_not_available()
    {
        using var worker = new ButtonWorker(17, Substitute.For<IDisplayService>(), NullLogger<ButtonWorker>.Instance,
            _ => throw new PlatformNotSupportedException("No GPIO on this machine"));

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!.WaitAsync(Timeout);

        Assert.True(worker.ExecuteTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Does_not_create_a_button_when_disabled()
    {
        var created = false;
        using var worker = new ButtonWorker(null, Substitute.For<IDisplayService>(), NullLogger<ButtonWorker>.Instance,
            _ => { created = true; return new TestButton(); });

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!.WaitAsync(Timeout);

        Assert.False(created);
    }

    [Fact]
    public async Task A_failing_display_does_not_break_the_button()
    {
        var display = Substitute.For<IDisplayService>();
        display.When(d => d.ShowNextPage()).Throw(new IOException("I2C glitch"));
        var button = new TestButton();
        var logger = new ReadyLogger();
        using var worker = new ButtonWorker(17, display, logger, _ => button);

        await worker.StartAsync(CancellationToken.None);
        await logger.Ready.WaitAsync(Timeout);

        var ex = Record.Exception(button.Click);
        button.Click();
        await worker.StopAsync(CancellationToken.None);

        Assert.Null(ex);
        display.Received(2).ShowNextPage();
    }

    [Theory]
    [InlineData("""{"Display":{"ButtonGpioPin":22}}""", 22)]
    [InlineData("""{"Display":{"ButtonGpioPin":null}}""", null)]
    [InlineData("""{}""", 17)]
    public void Button_pin_is_read_from_appsettings(string json, int? expected)
    {
        var config = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json))).Build();
        var options = new DisplayOptions();
        config.GetSection("Display").Bind(options);

        Assert.Equal(expected, options.ButtonGpioPin);
    }

    /// <summary>A button driven from code instead of a GPIO pin.</summary>
    private sealed class TestButton : ButtonBase
    {
        public bool IsDisposed { get; private set; }

        public void Click()
        {
            HandleButtonPressed();
            HandleButtonReleased();
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>Completes <see cref="Ready"/> once the worker logs that it is listening to the button.</summary>
    private sealed class ReadyLogger : ILogger<ButtonWorker>
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Ready => _ready.Task;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter(state, exception).StartsWith("Page button ready")) _ready.TrySetResult();
        }

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
