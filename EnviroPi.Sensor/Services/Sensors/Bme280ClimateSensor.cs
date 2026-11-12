using System.Device.I2c;
using Iot.Device.Bmxx80;
using Iot.Device.Bmxx80.PowerMode;

namespace EnviroPi.Sensor.Services.Sensors;

/// <summary>
/// Bosch BME280 temperature and humidity sensor on I2C bus 1, address 0x76.
/// Uses forced mode: the sensor takes one measurement per read and sleeps in between.
/// </summary>
public sealed class Bme280ClimateSensor : IClimateSensor, IDisposable
{
    // A forced measurement with the default oversampling finishes well within this time.
    private static readonly TimeSpan MeasurementTime = TimeSpan.FromMilliseconds(100);

    private readonly ILogger<Bme280ClimateSensor> _logger;
    private readonly Bme280? _bme280;

    public Bme280ClimateSensor(ILogger<Bme280ClimateSensor> logger)
        : this(logger, () => I2cDevice.Create(new I2cConnectionSettings(busId: 1, deviceAddress: Bme280.DefaultI2cAddress)))
    {
    }

    /// <summary>Creates the sensor with a custom I2C device factory (used by tests).</summary>
    internal Bme280ClimateSensor(ILogger<Bme280ClimateSensor> logger, Func<I2cDevice> createDevice)
    {
        _logger = logger;
        I2cDevice? device = null;
        try
        {
            device = createDevice();
            _bme280 = new Bme280(device);
            _logger.LogInformation("BME280 sensor connected over I2C.");
        }
        catch (Exception ex)
        {
            // The driver only owns the device once it is constructed; close it ourselves otherwise.
            device?.Dispose();
            _logger.LogWarning("BME280 sensor not available ({Message}).", ex.Message);
        }
    }

    public async Task<ClimateReading?> ReadAsync(CancellationToken cancellationToken)
    {
        if (_bme280 == null) return null;

        try
        {
            _bme280.SetPowerMode(Bmx280PowerMode.Forced);
            await Task.Delay(MeasurementTime, cancellationToken);
            var result = await _bme280.ReadAsync();

            if (result.Temperature is not { } temperature || result.Humidity is not { } humidity)
            {
                _logger.LogWarning("BME280 returned an incomplete reading.");
                return null;
            }
            return new ClimateReading(temperature.DegreesCelsius, humidity.Percent);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Failed to read BME280 sensor: {Message}", ex.Message);
            return null;
        }
    }

    public void Dispose() => _bme280?.Dispose();
}
