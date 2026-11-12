using EnviroPi.Display.Models;
using EnviroPi.Display.Services;
using Iot.Device.Button;
using Microsoft.Extensions.Options;

namespace EnviroPi.Display.Workers;

/// <summary>
/// Listens to a push button on a GPIO pin and switches the display page on every press.
/// </summary>
public sealed class ButtonWorker : BackgroundService
{
    private static readonly TimeSpan DebounceTime = TimeSpan.FromMilliseconds(50);

    private readonly int? _pin;
    private readonly IDisplayService _display;
    private readonly ILogger<ButtonWorker> _logger;
    private readonly Func<int, ButtonBase> _createButton;

    public ButtonWorker(IOptions<DisplayOptions> options, IDisplayService display, ILogger<ButtonWorker> logger)
        : this(options.Value.ButtonGpioPin, display, logger,
            pin => new GpioButton(pin, isPullUp: true, hasExternalResistor: false, debounceTime: DebounceTime))
    {
    }

    /// <summary>Creates the worker with a custom button factory (used by tests).</summary>
    internal ButtonWorker(int? pin, IDisplayService display, ILogger<ButtonWorker> logger, Func<int, ButtonBase> createButton)
    {
        _pin = pin;
        _display = display;
        _logger = logger;
        _createButton = createButton;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_pin is not int pin)
        {
            _logger.LogInformation("Page button disabled (Display:ButtonGpioPin is not set).");
            return;
        }

        ButtonBase button;
        try
        {
            button = _createButton(pin);
        }
        catch (Exception ex)
        {
            // No GPIO on a dev machine, or the pin is in use; the display still works without the button.
            _logger.LogWarning("Page button on GPIO{Pin} not available ({Message}).", pin, ex.Message);
            return;
        }

        using (button)
        {
            button.Press += OnPress;
            _logger.LogInformation("Page button ready on GPIO{Pin}.", pin);
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service stopping.
            }
            finally
            {
                button.Press -= OnPress;
            }
        }
    }

    private void OnPress(object? sender, EventArgs e)
    {
        // Runs on the GPIO event thread; an exception here would take down the process.
        try
        {
            _display.ShowNextPage();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to switch display page.");
        }
    }
}
