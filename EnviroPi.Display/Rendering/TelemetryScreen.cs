using System.Drawing;
using System.Globalization;
using EnviroPi.Shared.Models;
using Iot.Device.Graphics;

namespace EnviroPi.Display.Rendering;

/// <summary>
/// Lays out a telemetry reading for a 128x64 monochrome screen. Two pages:
/// <code>
///   Climate                    Co2
///   TEMPERATURE    (1x)        CO2 (ppm)    (1x)
///     22.4°C       (3x)          1234       (3x)
///    HUMIDITY      (1x)       ███████████
///     41.7 %       (2x)       █VENTILATE█   (2x, inverted at ≥ 1000 ppm, else "GOOD")
/// </code>
/// </summary>
public static class TelemetryScreen
{
    public const int TemperatureLabelY = 0;
    public const int TemperatureValueY = 11;
    public const int HumidityLabelY = 39;
    public const int HumidityValueY = 48;

    public const int Co2LabelY = 0;
    public const int Co2ValueY = 11;
    public const int Co2StatusBoxY = 40;
    public const int Co2StatusTextY = 44;

    /// <summary>Above this, indoor air is considered stale and the room should be aired.</summary>
    public const double Co2VentilateThresholdPpm = 1000;

    public static void Render(BitmapImage frame, TelemetryPayload telemetry, DisplayPage page)
    {
        frame.Clear(Color.Black);

        switch (page)
        {
            case DisplayPage.Co2:
                RenderCo2(frame, telemetry.Co2Ppm);
                break;
            default:
                RenderClimate(frame, telemetry.TemperatureCelsius, telemetry.HumidityPercent);
                break;
        }
    }

    /// <summary>Shown before the first reading arrives.</summary>
    public static void RenderWaiting(BitmapImage frame)
    {
        frame.Clear(Color.Black);
        TextRenderer.DrawCentered(frame, "WAITING FOR", 20);
        TextRenderer.DrawCentered(frame, "DATA", 32);
    }

    public static string FormatTemperature(double celsius)
        => celsius.ToString("0.0", CultureInfo.InvariantCulture) + "°C";

    public static string FormatHumidity(double percent)
        => percent.ToString("0.0", CultureInfo.InvariantCulture) + " %";

    public static string FormatCo2(double? ppm)
        => ppm?.ToString("0", CultureInfo.InvariantCulture) ?? "---";

    public static string Co2Status(double? ppm) => ppm switch
    {
        null => "NO SENSOR",
        >= Co2VentilateThresholdPpm => "VENTILATE",
        _ => "GOOD"
    };

    private static void RenderClimate(BitmapImage frame, double temperatureCelsius, double humidityPercent)
    {
        TextRenderer.DrawCentered(frame, "TEMPERATURE", TemperatureLabelY);
        TextRenderer.DrawCentered(frame, FormatTemperature(temperatureCelsius), TemperatureValueY, scale: 3);

        TextRenderer.DrawCentered(frame, "HUMIDITY", HumidityLabelY);
        TextRenderer.DrawCentered(frame, FormatHumidity(humidityPercent), HumidityValueY, scale: 2);
    }

    private static void RenderCo2(BitmapImage frame, double? ppm)
    {
        TextRenderer.DrawCentered(frame, "CO2 (ppm)", Co2LabelY);
        TextRenderer.DrawCentered(frame, FormatCo2(ppm), Co2ValueY, scale: 3);

        var status = Co2Status(ppm);
        if (ppm >= Co2VentilateThresholdPpm)
        {
            // Inverted box so the warning is visible from across the room.
            TextRenderer.FillRect(frame, 0, Co2StatusBoxY, frame.Width, frame.Height - Co2StatusBoxY, Color.White);
            TextRenderer.DrawCentered(frame, status, Co2StatusTextY, scale: 2, color: Color.Black);
        }
        else
        {
            TextRenderer.DrawCentered(frame, status, Co2StatusTextY, scale: 2);
        }
    }
}
