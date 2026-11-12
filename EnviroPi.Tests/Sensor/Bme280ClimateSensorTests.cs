using EnviroPi.Sensor.Services.Sensors;
using EnviroPi.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnviroPi.Tests.Sensor;

public class Bme280ClimateSensorTests
{
    [Fact]
    public async Task Returns_null_when_the_sensor_is_not_connected()
    {
        using var sensor = new Bme280ClimateSensor(NullLogger<Bme280ClimateSensor>.Instance,
            () => throw new IOException("No device at 0x76"));

        Assert.Null(await sensor.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Returns_null_when_the_bus_fails()
    {
        var device = new FakeI2cDevice(address: 0x76) { FailWrites = true };

        using var sensor = new Bme280ClimateSensor(NullLogger<Bme280ClimateSensor>.Instance, () => device);

        Assert.Null(await sensor.ReadAsync(CancellationToken.None));
    }
}
