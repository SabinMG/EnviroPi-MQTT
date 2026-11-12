using EnviroPi.Shared.Models;

namespace EnviroPi.Display.Services;

/// <summary>
/// Shows telemetry on an output device (OLED, LCD, console, ...).
/// </summary>
public interface IDisplayService
{
    /// <summary>Shows <paramref name="telemetry"/> on the current page.</summary>
    void RenderTelemetry(TelemetryPayload telemetry);

    /// <summary>Switches to the next page (temperature/humidity ↔ CO₂) and redraws immediately.</summary>
    void ShowNextPage();
}
