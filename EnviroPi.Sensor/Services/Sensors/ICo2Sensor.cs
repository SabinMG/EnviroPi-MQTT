namespace EnviroPi.Sensor.Services.Sensors;

/// <summary>
/// Reads the CO₂ concentration of the surrounding air.
/// </summary>
public interface ICo2Sensor
{
    /// <summary>
    /// Returns the latest CO₂ reading in ppm, or <c>null</c> when no sensor is connected
    /// or the reading failed.
    /// </summary>
    Task<double?> ReadCo2PpmAsync(CancellationToken cancellationToken);
}
