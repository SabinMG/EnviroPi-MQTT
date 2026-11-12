namespace EnviroPi.Sensor.Services.Sensors;

/// <summary>A temperature and humidity reading.</summary>
public readonly record struct ClimateReading(double TemperatureCelsius, double HumidityPercent);

/// <summary>
/// Reads the temperature and relative humidity of the surrounding air.
/// </summary>
public interface IClimateSensor
{
    /// <summary>
    /// Returns the current reading, or <c>null</c> when no sensor is connected
    /// or the reading failed.
    /// </summary>
    Task<ClimateReading?> ReadAsync(CancellationToken cancellationToken);
}
