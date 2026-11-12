using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using EnviroPi.Display.Services;
using EnviroPi.Display.Workers;
using EnviroPi.Sensor.Services;
using EnviroPi.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Server;

namespace EnviroPi.Tests.Integration;

/// <summary>
/// Runs the real publisher and receiver against an in-process MQTT broker.
/// </summary>
public sealed class EndToEndTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private MqttServer _broker = null!;
    private IOptions<MqttOptions> _options = null!;

    public async Task InitializeAsync()
    {
        var port = GetFreePort();
        _broker = new MqttServerFactory().CreateMqttServer(
            new MqttServerOptionsBuilder().WithDefaultEndpoint().WithDefaultEndpointPort(port).Build());
        await _broker.StartAsync();

        _options = Options.Create(new MqttOptions
        {
            Server = "127.0.0.1",
            Port = port,
            ClientIdPrefix = "Test",
            Topic = $"test/{Guid.NewGuid():N}"
        });
    }

    public async Task DisposeAsync()
    {
        await _broker.StopAsync();
        _broker.Dispose();
    }

    [Fact]
    public async Task Telemetry_published_by_the_sensor_is_rendered_by_the_display()
    {
        var display = new RecordingDisplay();
        using var receiver = new MqttReceiverWorker(_options, display, NullLogger<MqttReceiverWorker>.Instance);
        await using var publisher = new MqttPublishService(_options, NullLogger<MqttPublishService>.Instance);

        await receiver.StartAsync(CancellationToken.None);
        try
        {
            var payload = new TelemetryPayload { DeviceId = "pi-test", TemperatureCelsius = 22.4, HumidityPercent = 41.7, Co2Ppm = 1234 };

            // The receiver subscribes asynchronously, so keep publishing until it sees a message.
            var rendered = await PublishUntilRenderedAsync(display, () => publisher.PublishAsync(payload));

            Assert.Equal((22.4, 41.7, (double?)1234), rendered);
        }
        finally
        {
            await receiver.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Display_ignores_malformed_messages_and_keeps_receiving()
    {
        var display = new RecordingDisplay();
        using var receiver = new MqttReceiverWorker(_options, display, NullLogger<MqttReceiverWorker>.Instance);
        await using var publisher = new MqttPublishService(_options, NullLogger<MqttPublishService>.Instance);

        await receiver.StartAsync(CancellationToken.None);
        try
        {
            // A plain string is serialized as a JSON string, which is not a TelemetryPayload object.
            var valid = new TelemetryPayload { TemperatureCelsius = 19.9, HumidityPercent = 60.1 };
            var rendered = await PublishUntilRenderedAsync(display, async () =>
            {
                await publisher.PublishAsync("not telemetry");
                await publisher.PublishAsync(valid);
            });

            Assert.Equal((19.9, 60.1, (double?)null), rendered);
            Assert.All(display.Renders, r => Assert.Equal((19.9, 60.1, (double?)null), r));
        }
        finally
        {
            await receiver.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<(double Temp, double Humidity, double? Co2)> PublishUntilRenderedAsync(RecordingDisplay display, Func<Task> publish)
    {
        using var cts = new CancellationTokenSource(Timeout);
        while (!display.FirstRender.IsCompleted)
        {
            await publish();
            await Task.WhenAny(display.FirstRender, Task.Delay(200, cts.Token));
        }
        return await display.FirstRender;
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class RecordingDisplay : IDisplayService
    {
        private readonly TaskCompletionSource<(double, double, double?)> _first = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentQueue<(double Temp, double Humidity, double? Co2)> Renders { get; } = new();
        public Task<(double Temp, double Humidity, double? Co2)> FirstRender => _first.Task;

        public void RenderTelemetry(TelemetryPayload telemetry)
        {
            var reading = (telemetry.TemperatureCelsius, telemetry.HumidityPercent, telemetry.Co2Ppm);
            Renders.Enqueue(reading);
            _first.TrySetResult(reading);
        }

        public void ShowNextPage()
        {
        }
    }
}
