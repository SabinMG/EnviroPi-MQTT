using System.Device.I2c;
using EnviroPi.Display.Rendering;
using EnviroPi.Shared.Models;
using Iot.Device.Ssd13xx;
using Iot.Device.Ssd13xx.Commands;
using Iot.Device.Ssd13xx.Commands.Ssd1306Commands;

namespace EnviroPi.Display.Services;

public sealed class OledDisplayService : IDisplayService, IDisposable
{
    private const int ScreenWidth = 128;
    private const int ScreenHeight = 64;

    private readonly ILogger<OledDisplayService> _logger;
    private readonly Ssd1306? _oled;
    private readonly MonoBitmap _frame = new(ScreenWidth, ScreenHeight);
    private readonly Lock _drawLock = new();
    private TelemetryPayload? _lastTelemetry;
    private DisplayPage _page = DisplayPage.Climate;

    public OledDisplayService(ILogger<OledDisplayService> logger)
        // Address 0x3C is standard for 0.96" SSD1306 OLED screens
        : this(logger, () => I2cDevice.Create(new I2cConnectionSettings(busId: 1, deviceAddress: 0x3C)))
    {
    }

    /// <summary>Creates the service with a custom I2C device factory (used by tests).</summary>
    internal OledDisplayService(ILogger<OledDisplayService> logger, Func<I2cDevice> createDevice)
    {
        _logger = logger;
        MonoBitmap.RegisterAsImageFactory();
        I2cDevice? device = null;
        try
        {
            device = createDevice();
            var oled = new Ssd1306(device, ScreenWidth, ScreenHeight);

            // Iot.Device.Bindings 4.2 always initialises the SSD1306 as a 128x32 panel
            // (multiplex 0x1F, sequential COM pins), which leaves the bottom half of a
            // 128x64 screen blank. Reconfigure it for 64 rows.
            oled.SendCommand(new SetMultiplexRatio(ScreenHeight - 1));
            oled.SendCommand(new SetComPinsHardwareConfiguration(alternativeComPinConfiguration: true));
            TelemetryScreen.RenderWaiting(_frame);
            DrawFrame(oled);

            // Only keep the display once it is fully set up; a half-initialised one stays null.
            _oled = oled;
            _logger.LogInformation("SSD1306 OLED display connected over I2C.");
        }
        catch (Exception ex)
        {
            // Hardware display not found; degrades gracefully. Close the bus handle we opened.
            device?.Dispose();
            _logger.LogWarning("OLED display not available ({Message}). Telemetry will only be logged.", ex.Message);
        }
    }

    public void RenderTelemetry(TelemetryPayload telemetry)
    {
        lock (_drawLock)
        {
            _lastTelemetry = telemetry;
            Redraw();
        }
    }

    public void ShowNextPage()
    {
        lock (_drawLock)
        {
            _page = _page == DisplayPage.Climate ? DisplayPage.Co2 : DisplayPage.Climate;
            _logger.LogInformation("Display page switched to {Page}.", _page);
            // Redraw the last reading now, so the press feels instant instead of waiting for the next message.
            Redraw();
        }
    }

    /// <summary>The page currently shown.</summary>
    public DisplayPage CurrentPage
    {
        get { lock (_drawLock) return _page; }
    }

    // Caller holds _drawLock.
    private void Redraw()
    {
        if (_oled == null) return;

        try
        {
            if (_lastTelemetry == null)
            {
                TelemetryScreen.RenderWaiting(_frame);
            }
            else
            {
                TelemetryScreen.Render(_frame, _lastTelemetry, _page);
            }
            DrawFrame(_oled);
        }
        catch (Exception ex)
        {
            // A loose wire or I2C glitch shouldn't stop the service; the next reading retries.
            _logger.LogWarning(ex, "Failed to draw on the OLED display.");
        }
    }

    /// <summary>
    /// Sends the whole frame buffer. Ssd1306.DrawBitmap only addresses pages 0-3 (32 rows),
    /// so the address window is set to all 8 pages here.
    /// </summary>
    private void DrawFrame(Ssd1306 oled)
    {
        oled.SendCommand(new SetColumnAddress(0, ScreenWidth - 1));
        oled.SendCommand(new SetPageAddress(PageAddress.Page0, PageAddress.Page7));
        oled.SendData(oled.PreRenderBitmap(_frame));
    }

    public void Dispose()
    {
        _oled?.Dispose();
        _frame.Dispose();
    }
}
