using EnviroPi.Sensor.Services;
using EnviroPi.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnviroPi.Tests.Sensor;

// SCD4x command codes (Sensirion datasheet).
internal static class Scd4xCommands
{
    public const string StopPeriodic = "3F86";
    public const string StartPeriodic = "21B1";
    public const string DataReady = "E4B8";
    public const string ReadMeasurement = "EC05";

    /// <summary>A fake SCD4x that reports data ready and returns <paramref name="co2Ppm"/>.</summary>
    public static FakeI2cDevice Device(ushort co2Ppm)
    {
        var device = new FakeI2cDevice(address: 0x62);
        device.Responses[DataReady] = FakeI2cDevice.SensirionWords(0x8006);
        // CO2, then raw temperature and humidity words (ignored by EnviroPi).
        device.Responses[ReadMeasurement] = FakeI2cDevice.SensirionWords(co2Ppm, 0x6667, 0x5EB9);
        return device;
    }
}

public class Scd4xCo2SensorTests
{
    [Fact]
    public void Starts_periodic_measurement_after_stopping_any_previous_run()
    {
        var device = Scd4xCommands.Device(co2Ppm: 600);

        using var sensor = new Scd4xCo2Sensor(NullLogger<Scd4xCo2Sensor>.Instance, () => device);

        var commands = device.Writes.Select(Convert.ToHexString).ToList();
        Assert.Contains(Scd4xCommands.StartPeriodic, commands);
        Assert.True(commands.IndexOf(Scd4xCommands.StopPeriodic) < commands.IndexOf(Scd4xCommands.StartPeriodic));
    }

    [Fact]
    public async Task Returns_null_when_no_sensor_is_connected()
    {
        using var sensor = new Scd4xCo2Sensor(NullLogger<Scd4xCo2Sensor>.Instance,
            () => throw new IOException("No device at 0x62"));

        Assert.Null(await sensor.ReadCo2PpmAsync(CancellationToken.None));
    }

    [Fact]
    public void Stops_measuring_when_disposed()
    {
        var device = Scd4xCommands.Device(co2Ppm: 600);
        var sensor = new Scd4xCo2Sensor(NullLogger<Scd4xCo2Sensor>.Instance, () => device);
        device.Writes.Clear();

        sensor.Dispose();

        Assert.Contains(Scd4xCommands.StopPeriodic, device.Writes.Select(Convert.ToHexString));
    }
}

// Reads wait for the sensor's 5 s measurement period, so these live in their own
// class to run in parallel with the other test classes.
public class Scd4xCo2SensorReadTests
{
    [Fact]
    public async Task Reads_co2_in_ppm()
    {
        using var sensor = new Scd4xCo2Sensor(NullLogger<Scd4xCo2Sensor>.Instance, () => Scd4xCommands.Device(co2Ppm: 1234));

        Assert.Equal(1234, await sensor.ReadCo2PpmAsync(CancellationToken.None));
    }
}

public class Scd4xCo2SensorReadFailureTests
{
    [Fact]
    public async Task Returns_null_when_the_bus_fails_during_a_read()
    {
        var device = Scd4xCommands.Device(co2Ppm: 1234);
        using var sensor = new Scd4xCo2Sensor(NullLogger<Scd4xCo2Sensor>.Instance, () => device);
        device.FailWrites = true;

        Assert.Null(await sensor.ReadCo2PpmAsync(CancellationToken.None));
        // Shutting down with a broken bus must not throw either (the driver's Dispose writes a stop command).
    }
}
