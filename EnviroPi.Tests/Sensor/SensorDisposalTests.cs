using EnviroPi.Sensor.Services.Sensors;
using EnviroPi.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnviroPi.Tests.Sensor;

public class SensorDisposalTests
{
    [Fact]
    public void Bme280_releases_the_i2c_device_when_setup_fails()
    {
        // No canned chip-ID response, so the driver rejects the device.
        var device = new FakeI2cDevice(address: 0x76);
        var sensor = new Bme280ClimateSensor(NullLogger<Bme280ClimateSensor>.Instance, () => device);
        Assert.True(device.IsDisposed);
        sensor.Dispose();
    }

    [Fact]
    public void Bme280_releases_the_i2c_device_on_dispose()
    {
        var device = new FakeI2cDevice(address: 0x76);
        device.Responses["D0"] = [0x60]; // BME280 chip ID
        var sensor = new Bme280ClimateSensor(NullLogger<Bme280ClimateSensor>.Instance, () => device);
        Assert.False(device.IsDisposed);

        sensor.Dispose();

        Assert.True(device.IsDisposed);
    }

    [Fact]
    public void Scd4x_releases_the_i2c_device_on_dispose()
    {
        var device = Scd4xCommands.Device(co2Ppm: 600);
        var sensor = new Scd4xCo2Sensor(NullLogger<Scd4xCo2Sensor>.Instance, () => device);
        sensor.Dispose();
        Assert.True(device.IsDisposed);
    }

    [Fact]
    public void Scd4x_releases_the_i2c_device_when_setup_fails()
    {
        var device = new FakeI2cDevice(address: 0x62) { FailWrites = true };
        var sensor = new Scd4xCo2Sensor(NullLogger<Scd4xCo2Sensor>.Instance, () => device);
        Assert.True(device.IsDisposed);
        sensor.Dispose();
    }
}
