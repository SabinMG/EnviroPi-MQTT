using System.Drawing;
using System.Globalization;
using Iot.Device.Graphics;

namespace EnviroPi.Display.Rendering;

/// <summary>
/// Lays out a telemetry reading for a 128x64 monochrome screen:
/// <code>
///   TEMPERATURE        (label, 1x)
///     22.4°C           (value, 3x)
///    HUMIDITY          (label, 1x)
///     41.7 %           (value, 2x)
/// </code>
/// </summary>
public static class TelemetryScreen
{
    public const int TemperatureLabelY = 0;
    public const int TemperatureValueY = 11;
    public const int HumidityLabelY = 39;
    public const int HumidityValueY = 48;

    public static void Render(BitmapImage frame, double temperatureCelsius, double humidityPercent)
    {
        frame.Clear(Color.Black);

        TextRenderer.DrawCentered(frame, "TEMPERATURE", TemperatureLabelY);
        TextRenderer.DrawCentered(frame, FormatTemperature(temperatureCelsius), TemperatureValueY, scale: 3);

        TextRenderer.DrawCentered(frame, "HUMIDITY", HumidityLabelY);
        TextRenderer.DrawCentered(frame, FormatHumidity(humidityPercent), HumidityValueY, scale: 2);
    }

    public static string FormatTemperature(double celsius)
        => celsius.ToString("0.0", CultureInfo.InvariantCulture) + "°C";

    public static string FormatHumidity(double percent)
        => percent.ToString("0.0", CultureInfo.InvariantCulture) + " %";
}
