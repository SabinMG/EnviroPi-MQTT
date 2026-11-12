using System.Device.I2c;
using Iot.Device.Ssd13xx;

namespace EnviroPi.Display.Services;

public sealed class OledDisplayService : IDisplayService, IDisposable
{
    private readonly ILogger<OledDisplayService> _logger;
    private Ssd1306? _oled;

    public OledDisplayService(ILogger<OledDisplayService> logger)
    {
        _logger = logger;
        try
        {
            // Address 0x3C is standard for 0.96" SSD1306 OLED screens
            var settings = new I2cConnectionSettings(busId: 1, deviceAddress: 0x3C);
            var i2cDevice = I2cDevice.Create(settings);
            _oled = new Ssd1306(i2cDevice, 128, 64);
            _oled.ClearScreen();
            _logger.LogInformation("SSD1306 OLED display connected over I2C.");
        }
        catch (Exception ex)
        {
            // Hardware display not found; degrades gracefully
            _logger.LogWarning("OLED display not available ({Message}). Telemetry will only be logged.", ex.Message);
        }
    }

    public void RenderTelemetry(double tempCelsius, double humidityPercent)
    {
        if (_oled == null) return;

        // Clear and render telemetry values
        _oled.ClearScreen();

        // TODO: Render text (e.g. via Iot.Device.Graphics + SkiaSharp) showing temperature and humidity
    }

    public void Dispose()
    {
        _oled?.Dispose();
    }
}
