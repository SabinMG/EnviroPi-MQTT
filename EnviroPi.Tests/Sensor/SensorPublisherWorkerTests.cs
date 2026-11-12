using EnviroPi.Sensor.Services;
using EnviroPi.Sensor.Workers;
using EnviroPi.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EnviroPi.Tests.Sensor;

public class SensorPublisherWorkerTests
{
    [Fact]
    public async Task Publishes_telemetry_for_this_device()
    {
        var payload = await PublishFirstPayloadAsync(Co2Returning(null));

        Assert.Equal(Environment.MachineName, payload.DeviceId);
        Assert.InRange(payload.Timestamp, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow);
        // Values are rounded to one decimal place before publishing.
        Assert.Equal(Math.Round(payload.TemperatureCelsius, 1), payload.TemperatureCelsius);
        Assert.Equal(Math.Round(payload.HumidityPercent, 1), payload.HumidityPercent);
    }

    [Fact]
    public async Task Includes_the_co2_reading()
    {
        var payload = await PublishFirstPayloadAsync(Co2Returning(812));

        Assert.Equal(812, payload.Co2Ppm);
    }

    [Fact]
    public async Task Publishes_without_co2_when_there_is_no_co2_sensor()
    {
        var payload = await PublishFirstPayloadAsync(Co2Returning(null));

        Assert.Null(payload.Co2Ppm);
    }

    [Fact]
    public async Task Still_publishes_temperature_and_humidity_when_the_co2_sensor_throws()
    {
        var co2 = Substitute.For<ICo2Sensor>();
        co2.ReadCo2PpmAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("SCD4x unplugged"));

        var payload = await PublishFirstPayloadAsync(co2);

        Assert.Null(payload.Co2Ppm);
        Assert.Equal(Environment.MachineName, payload.DeviceId);
    }

    [Fact]
    public async Task Stops_cleanly_when_cancelled()
    {
        var publisher = Substitute.For<IMqttPublishService>();
        using var worker = new SensorPublisherWorker(publisher, Co2Returning(null), NullLogger<SensorPublisherWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(worker.ExecuteTask!.IsCompleted);
    }

    private static ICo2Sensor Co2Returning(double? ppm)
    {
        var co2 = Substitute.For<ICo2Sensor>();
        co2.ReadCo2PpmAsync(Arg.Any<CancellationToken>()).Returns(ppm);
        return co2;
    }

    private static async Task<TelemetryPayload> PublishFirstPayloadAsync(ICo2Sensor co2)
    {
        var published = new TaskCompletionSource<TelemetryPayload>(TaskCreationOptions.RunContinuationsAsynchronously);
        var publisher = Substitute.For<IMqttPublishService>();
        publisher.PublishAsync(Arg.Any<TelemetryPayload>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                published.TrySetResult(call.Arg<TelemetryPayload>());
                return Task.CompletedTask;
            });

        using var worker = new SensorPublisherWorker(publisher, co2, NullLogger<SensorPublisherWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            return await published.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }
}
