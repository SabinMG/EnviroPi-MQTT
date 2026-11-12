namespace EnviroPi.Display.Services;

/// <summary>
/// Shows telemetry on an output device (OLED, LCD, console, ...).
/// </summary>
public interface IDisplayService
{
    void RenderTelemetry(double tempCelsius, double humidityPercent);
}
