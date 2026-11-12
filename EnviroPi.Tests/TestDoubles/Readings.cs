using EnviroPi.Shared.Models;

namespace EnviroPi.Tests.TestDoubles;

internal static class Readings
{
    public static TelemetryPayload Of(double temperatureCelsius, double humidityPercent, double? co2Ppm = null) => new()
    {
        DeviceId = "test-pi",
        TemperatureCelsius = temperatureCelsius,
        HumidityPercent = humidityPercent,
        Co2Ppm = co2Ppm
    };
}
