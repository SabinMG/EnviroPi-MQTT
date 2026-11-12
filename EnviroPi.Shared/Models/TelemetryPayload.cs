namespace EnviroPi.Shared.Models;

public class TelemetryPayload
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double TemperatureCelsius { get; set; }
    public double HumidityPercent { get; set; }

    /// <summary>CO₂ in parts per million, or null when no CO₂ sensor reading is available.</summary>
    public double? Co2Ppm { get; set; }
}
