using EnviroPi.Sensor.Services;
using EnviroPi.Sensor.Services.Sensors;
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
        var payload = await PublishFirstPayloadAsync(ClimateReturning(null), Co2Returning(null));

        Assert.Equal(Environment.MachineName, payload.DeviceId);
        Assert.InRange(payload.Timestamp, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow);
    }

    [Fact]
    public async Task Publishes_the_climate_reading_rounded_to_one_decimal()
    {
        var payload = await PublishFirstPayloadAsync(ClimateReturning(new ClimateReading(22.447, 41.66)), Co2Returning(null));

        Assert.Equal(22.4, payload.TemperatureCelsius);
        Assert.Equal(41.7, payload.HumidityPercent);
    }

    [Fact]
    public async Task Publishes_default_values_when_there_is_no_climate_sensor()
    {
        var payload = await PublishFirstPayloadAsync(ClimateReturning(null), Co2Returning(812));

        Assert.Equal(20.0, payload.TemperatureCelsius);
        Assert.Equal(50.0, payload.HumidityPercent);
        Assert.Equal(812, payload.Co2Ppm);
    }

    [Fact]
    public async Task Still_publishes_co2_when_the_climate_sensor_throws()
    {
        var climate = Substitute.For<IClimateSensor>();
        climate.ReadAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("BME280 unplugged"));

        var payload = await PublishFirstPayloadAsync(climate, Co2Returning(640));

        Assert.Equal(640, payload.Co2Ppm);
        Assert.Equal(20.0, payload.TemperatureCelsius);
    }

    [Fact]
    public async Task Includes_the_co2_reading()
    {
        var payload = await PublishFirstPayloadAsync(ClimateReturning(null), Co2Returning(812));

        Assert.Equal(812, payload.Co2Ppm);
    }

    [Fact]
    public async Task Publishes_without_co2_when_there_is_no_co2_sensor()
    {
        var payload = await PublishFirstPayloadAsync(ClimateReturning(null), Co2Returning(null));

        Assert.Null(payload.Co2Ppm);
    }

    [Fact]
    public async Task Still_publishes_temperature_and_humidity_when_the_co2_sensor_throws()
    {
        var co2 = Substitute.For<ICo2Sensor>();
        co2.ReadAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("SCD4x unplugged"));

        var payload = await PublishFirstPayloadAsync(ClimateReturning(new ClimateReading(23.1, 38.2)), co2);

        Assert.Null(payload.Co2Ppm);
        Assert.Equal(23.1, payload.TemperatureCelsius);
        Assert.Equal(38.2, payload.HumidityPercent);
    }

    [Fact]
    public async Task Stops_cleanly_when_cancelled()
    {
        var publisher = Substitute.For<IMqttPublishService>();
        using var worker = new SensorPublisherWorker(publisher, ClimateReturning(null), Co2Returning(null),
            NullLogger<SensorPublisherWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(worker.ExecuteTask!.IsCompleted);
    }

    private static IClimateSensor ClimateReturning(ClimateReading? reading)
    {
        var climate = Substitute.For<IClimateSensor>();
        climate.ReadAsync(Arg.Any<CancellationToken>()).Returns(reading);
        return climate;
    }

    private static ICo2Sensor Co2Returning(double? ppm)
    {
        var co2 = Substitute.For<ICo2Sensor>();
        co2.ReadAsync(Arg.Any<CancellationToken>()).Returns(ppm);
        return co2;
    }

    private static async Task<TelemetryPayload> PublishFirstPayloadAsync(IClimateSensor climate, ICo2Sensor co2)
    {
        var published = new TaskCompletionSource<TelemetryPayload>(TaskCreationOptions.RunContinuationsAsynchronously);
        var publisher = Substitute.For<IMqttPublishService>();
        publisher.PublishAsync(Arg.Any<TelemetryPayload>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                published.TrySetResult(call.Arg<TelemetryPayload>());
                return Task.CompletedTask;
            });

        using var worker = new SensorPublisherWorker(publisher, climate, co2, NullLogger<SensorPublisherWorker>.Instance);
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
