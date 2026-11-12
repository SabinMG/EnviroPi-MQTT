using EnviroPi.Display.Rendering;
using EnviroPi.Display.Services;
using EnviroPi.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnviroPi.Tests.Display;

public class OledDisplayServiceTests
{
    [Fact]
    public void Degrades_gracefully_when_no_display_hardware_is_present()
    {
        // On a dev machine / CI there is no I2C bus; construction and rendering must not throw.
        using var display = new OledDisplayService(NullLogger<OledDisplayService>.Instance);

        var ex = Record.Exception(() => display.RenderTelemetry(21.5, 48.3));

        Assert.Null(ex);
    }

    [Fact]
    public void Configures_the_panel_for_64_rows()
    {
        var device = new FakeI2cDevice();
        using var display = new OledDisplayService(NullLogger<OledDisplayService>.Instance, () => device);

        // The library's default init targets 128x32; these must be sent afterwards to override it.
        var commands = device.Writes.Where(w => w[0] == 0x00).Select(w => Convert.ToHexString(w[1..])).ToList();
        Assert.True(commands.LastIndexOf("A83F") > commands.LastIndexOf("A81F"), "multiplex ratio 64");
        Assert.True(commands.LastIndexOf("DA12") > commands.LastIndexOf("DA02"), "alternative COM pins");
    }

    [Fact]
    public void Sends_the_full_rendered_frame_to_the_display_over_i2c()
    {
        var device = new FakeI2cDevice();
        using var display = new OledDisplayService(NullLogger<OledDisplayService>.Instance, () => device);
        device.Writes.Clear();   // ignore init and clear-screen traffic

        display.RenderTelemetry(22.4, 41.7);

        // SSD1306 data writes start with control byte 0x40. In horizontal addressing mode each
        // byte is a vertical strip of 8 pixels (LSB on top), pages of 8 rows left to right.
        var data = device.Writes.Where(w => w[0] == 0x40).SelectMany(w => w.Skip(1)).ToArray();
        Assert.Equal(128 * 64 / 8, data.Length);

        using var expected = new MonoBitmap(128, 64);
        TelemetryScreen.Render(expected, 22.4, 41.7);
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 128; x++)
            {
                var lit = (data[(y / 8) * 128 + x] >> (y % 8) & 1) == 1;
                Assert.True(expected.IsOn(x, y) == lit, $"pixel ({x},{y})");
            }
        }
    }

    [Fact]
    public void Keeps_running_when_an_i2c_write_fails()
    {
        var device = new FakeI2cDevice();
        using var display = new OledDisplayService(NullLogger<OledDisplayService>.Instance, () => device);
        device.FailWrites = true;

        var ex = Record.Exception(() => display.RenderTelemetry(22.4, 41.7));

        Assert.Null(ex);
    }
}
