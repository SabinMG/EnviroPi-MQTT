using EnviroPi.Sensor.Services;
using EnviroPi.Sensor.Workers;
using EnviroPi.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EnviroPi.Tests.Sensor;

public class SensorPublisherWorkerTests
{
    [Fact]
    public async Task Publishes_telemetry_for_this_device()
    {
        var published = new TaskCompletionSource<TelemetryPayload>(TaskCreationOptions.RunContinuationsAsynchronously);
        var publisher = Substitute.For<IMqttPublishService>();
        publisher.PublishAsync(Arg.Any<TelemetryPayload>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                published.TrySetResult(call.Arg<TelemetryPayload>());
                return Task.CompletedTask;
            });

        using var worker = new SensorPublisherWorker(publisher, NullLogger<SensorPublisherWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            var payload = await published.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(Environment.MachineName, payload.DeviceId);
            Assert.InRange(payload.Timestamp, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow);
            // Values are rounded to one decimal place before publishing.
            Assert.Equal(Math.Round(payload.TemperatureCelsius, 1), payload.TemperatureCelsius);
            Assert.Equal(Math.Round(payload.HumidityPercent, 1), payload.HumidityPercent);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Stops_cleanly_when_cancelled()
    {
        var publisher = Substitute.For<IMqttPublishService>();
        using var worker = new SensorPublisherWorker(publisher, NullLogger<SensorPublisherWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(worker.ExecuteTask!.IsCompleted);
    }
}
