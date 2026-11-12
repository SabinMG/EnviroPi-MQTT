using EnviroPi.Display.Rendering;
using EnviroPi.Display.Services;
using EnviroPi.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnviroPi.Tests.Display;

public class OledDisplayServiceTests
{
    [Fact]
    public void Releases_the_i2c_device_when_setup_fails()
    {
        var device = new FakeI2cDevice { FailWrites = true };

        using var display = new OledDisplayService(NullLogger<OledDisplayService>.Instance, () => device);

        Assert.True(device.IsDisposed);
    }

    [Fact]
    public void Releases_the_i2c_device_on_dispose()
    {
        var device = new FakeI2cDevice();
        var display = new OledDisplayService(NullLogger<OledDisplayService>.Instance, () => device);
        Assert.False(device.IsDisposed);

        display.Dispose();

        Assert.True(device.IsDisposed);
    }

    [Fact]
    public void Degrades_gracefully_when_no_display_hardware_is_present()
    {
        // On a dev machine / CI there is no I2C bus; construction and rendering must not throw.
        using var display = new OledDisplayService(NullLogger<OledDisplayService>.Instance);

        var ex = Record.Exception(() => display.RenderTelemetry(Readings.Of(21.5, 48.3)));

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

        display.RenderTelemetry(Readings.Of(22.4, 41.7));

        // SSD1306 data writes start with control byte 0x40. In horizontal addressing mode each
        // byte is a vertical strip of 8 pixels (LSB on top), pages of 8 rows left to right.
        var data = device.Writes.Where(w => w[0] == 0x40).SelectMany(w => w.Skip(1)).ToArray();
        Assert.Equal(128 * 64 / 8, data.Length);

        using var expected = new MonoBitmap(128, 64);
        TelemetryScreen.Render(expected, Readings.Of(22.4, 41.7), DisplayPage.Climate);
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

        var ex = Record.Exception(() => display.RenderTelemetry(Readings.Of(22.4, 41.7)));

        Assert.Null(ex);
    }

    [Fact]
    public void Starts_on_the_climate_page_and_toggles_between_pages()
    {
        using var display = new OledDisplayService(NullLogger<OledDisplayService>.Instance, () => new FakeI2cDevice());

        Assert.Equal(DisplayPage.Climate, display.CurrentPage);
        display.ShowNextPage();
        Assert.Equal(DisplayPage.Co2, display.CurrentPage);
        display.ShowNextPage();
        Assert.Equal(DisplayPage.Climate, display.CurrentPage);
    }

    [Fact]
    public void Switching_page_redraws_the_last_reading_immediately()
    {
        var device = new FakeI2cDevice();
        using var display = new OledDisplayService(NullLogger<OledDisplayService>.Instance, () => device);
        var reading = Readings.Of(22.4, 41.7, co2Ppm: 1450);
        display.RenderTelemetry(reading);
        device.Writes.Clear();

        display.ShowNextPage();

        using var expected = new MonoBitmap(128, 64);
        TelemetryScreen.Render(expected, reading, DisplayPage.Co2);
        Assert.Equal(ToDisplayBytes(expected), SentPixelData(device));
    }

    [Fact]
    public void Shows_a_waiting_screen_until_the_first_reading_arrives()
    {
        var device = new FakeI2cDevice();
        using var display = new OledDisplayService(NullLogger<OledDisplayService>.Instance, () => device);
        device.Writes.Clear();

        display.ShowNextPage();   // no reading yet

        using var expected = new MonoBitmap(128, 64);
        TelemetryScreen.RenderWaiting(expected);
        Assert.Equal(ToDisplayBytes(expected), SentPixelData(device));
    }

    [Fact]
    public void Switching_page_without_display_hardware_does_not_throw()
    {
        using var display = new OledDisplayService(NullLogger<OledDisplayService>.Instance, () => throw new IOException("no bus"));

        var ex = Record.Exception(display.ShowNextPage);

        Assert.Null(ex);
        Assert.Equal(DisplayPage.Co2, display.CurrentPage);
    }

    // SSD1306 data writes start with control byte 0x40.
    private static byte[] SentPixelData(FakeI2cDevice device)
        => device.Writes.Where(w => w[0] == 0x40).SelectMany(w => w.Skip(1)).ToArray();

    // Horizontal addressing: each byte is a vertical strip of 8 pixels (LSB on top), page by page.
    private static byte[] ToDisplayBytes(MonoBitmap frame)
    {
        var bytes = new byte[frame.Width * frame.Height / 8];
        for (var y = 0; y < frame.Height; y++)
            for (var x = 0; x < frame.Width; x++)
                if (frame.IsOn(x, y)) bytes[(y / 8) * frame.Width + x] |= (byte)(1 << (y % 8));
        return bytes;
    }
}
