using System.Device.I2c;
using Iot.Device.Scd4x;

namespace EnviroPi.Sensor.Services.Sensors;

/// <summary>
/// Sensirion SCD40/SCD41 NDIR CO₂ sensor on I2C bus 1, address 0x62.
/// Runs in periodic mode: the sensor produces a new measurement every 5 seconds.
/// </summary>
public sealed class Scd4xCo2Sensor : ICo2Sensor, IDisposable
{
    private readonly ILogger<Scd4xCo2Sensor> _logger;
    private readonly Scd4x? _scd4x;

    public Scd4xCo2Sensor(ILogger<Scd4xCo2Sensor> logger)
        : this(logger, () => I2cDevice.Create(new I2cConnectionSettings(busId: 1, deviceAddress: Scd4x.DefaultI2cAddress)))
    {
    }

    /// <summary>Creates the sensor with a custom I2C device factory (used by tests).</summary>
    internal Scd4xCo2Sensor(ILogger<Scd4xCo2Sensor> logger, Func<I2cDevice> createDevice)
    {
        _logger = logger;
        try
        {
            // The constructor stops any measurement left running by a previous run and
            // re-initialises the sensor, so starting periodic mode afterwards is reliable.
            var scd4x = new Scd4x(createDevice());
            scd4x.StartPeriodicMeasurements();

            _scd4x = scd4x;
            _logger.LogInformation("SCD4x CO2 sensor connected over I2C.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("SCD4x CO2 sensor not available ({Message}). CO2 will not be published.", ex.Message);
        }
    }

    public async Task<double?> ReadCo2PpmAsync(CancellationToken cancellationToken)
    {
        if (_scd4x == null) return null;

        try
        {
            // Waits until the next periodic measurement is ready (at most one 5 s period).
            var (co2, _, _) = await _scd4x.ReadPeriodicMeasurementAsync(cancellationToken);
            if (co2 is null)
            {
                _logger.LogWarning("SCD4x returned an invalid CO2 reading (CRC check failed).");
                return null;
            }
            return Math.Round(co2.Value.PartsPerMillion);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Failed to read SCD4x CO2 sensor: {Message}", ex.Message);
            return null;
        }
    }

    public void Dispose()
    {
        try
        {
            // Scd4x.Dispose sends a stop command, which throws if the bus has failed;
            // that must not crash service shutdown.
            _scd4x?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error while stopping the SCD4x sensor.");
        }
    }
}
