using System.Globalization;
using EnviroPi.Display.Rendering;
using EnviroPi.Tests.TestDoubles;

namespace EnviroPi.Tests.Display;

public class TextRendererTests
{
    [Theory]
    [InlineData("", 1, 0)]
    [InlineData("A", 1, 5)]
    [InlineData("AB", 1, 11)]   // 5 + 1 spacing + 5
    [InlineData("AB", 3, 33)]
    public void Measures_text_width(string text, int scale, int expected)
    {
        Assert.Equal(expected, TextRenderer.MeasureWidth(text, scale));
    }

    [Fact]
    public void Draws_glyph_pixels_at_the_given_position()
    {
        using var frame = new MonoBitmap(20, 10);

        TextRenderer.DrawText(frame, "L", x: 2, y: 1);

        // 'L' = vertical stroke in the first column (rows 0-6) plus a bottom bar on row 6.
        for (var row = 0; row <= 6; row++) Assert.True(frame.IsOn(2, 1 + row), $"stroke row {row}");
        for (var col = 0; col < 5; col++) Assert.True(frame.IsOn(2 + col, 7), $"bar col {col}");
        Assert.False(frame.IsOn(4, 3));
        Assert.Equal(7 + 4, CountLit(frame));
    }

    [Fact]
    public void Scaling_multiplies_each_pixel_into_a_block()
    {
        using var one = new MonoBitmap(40, 30);
        using var three = new MonoBitmap(40, 30);

        TextRenderer.DrawText(one, "7", 0, 0, scale: 1);
        TextRenderer.DrawText(three, "7", 0, 0, scale: 3);

        Assert.Equal(CountLit(one) * 9, CountLit(three));
    }

    [Fact]
    public void Draws_a_degree_sign_even_though_the_font_lacks_one()
    {
        using var frame = new MonoBitmap(10, 10);

        TextRenderer.DrawText(frame, "°", 0, 0);

        Assert.True(CountLit(frame) > 0);
        Assert.False(frame.IsOn(0, 0));   // ring, not a filled block
        Assert.True(frame.IsOn(1, 0));
    }

    [Fact]
    public void Clips_text_that_runs_off_the_image()
    {
        using var frame = new MonoBitmap(8, 8);

        var ex = Record.Exception(() => TextRenderer.DrawText(frame, "WIDE TEXT", x: -3, y: 4, scale: 2));

        Assert.Null(ex);
        Assert.True(CountLit(frame) > 0);
    }

    internal static int CountLit(MonoBitmap frame)
    {
        var count = 0;
        for (var y = 0; y < frame.Height; y++)
            for (var x = 0; x < frame.Width; x++)
                if (frame.IsOn(x, y)) count++;
        return count;
    }
}

public class TelemetryScreenTests
{
    [Theory]
    [InlineData(22.4, "22.4°C")]
    [InlineData(-5, "-5.0°C")]
    [InlineData(19.96, "20.0°C")]
    public void Formats_temperature(double value, string expected)
    {
        Assert.Equal(expected, TelemetryScreen.FormatTemperature(value));
    }

    [Fact]
    public void Formats_with_a_dot_regardless_of_system_culture()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");   // uses ',' as decimal separator
        try
        {
            Assert.Equal("41.7 %", TelemetryScreen.FormatHumidity(41.7));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData(-40.0)]
    [InlineData(-10.5)]
    [InlineData(85.0)]
    public void Temperature_fits_on_screen_across_the_BME280_range(double celsius)
    {
        Assert.InRange(TextRenderer.MeasureWidth(TelemetryScreen.FormatTemperature(celsius), 3), 1, 128);
    }

    [Fact]
    public void Renders_every_section_of_the_screen()
    {
        using var frame = new MonoBitmap(128, 64);

        TelemetryScreen.Render(frame, Readings.Of(22.4, 41.7), DisplayPage.Climate);

        Assert.True(AnyLitInRows(frame, TelemetryScreen.TemperatureLabelY, TelemetryScreen.TemperatureLabelY + 8), "temperature label");
        Assert.True(AnyLitInRows(frame, TelemetryScreen.TemperatureValueY, TelemetryScreen.TemperatureValueY + 24), "temperature value");
        Assert.True(AnyLitInRows(frame, TelemetryScreen.HumidityLabelY, TelemetryScreen.HumidityLabelY + 8), "humidity label");
        Assert.True(AnyLitInRows(frame, TelemetryScreen.HumidityValueY, 64), "humidity value");
    }

    [Fact]
    public void Clears_the_previous_reading()
    {
        using var first = new MonoBitmap(128, 64);
        using var reused = new MonoBitmap(128, 64);

        TelemetryScreen.Render(first, Readings.Of(11.1, 11.1), DisplayPage.Climate);
        TelemetryScreen.Render(reused, Readings.Of(88.8, 88.8), DisplayPage.Climate);
        TelemetryScreen.Render(reused, Readings.Of(11.1, 11.1), DisplayPage.Climate);

        Assert.Equal(first.AsByteSpan().ToArray(), reused.AsByteSpan().ToArray());
    }

    private static bool AnyLitInRows(MonoBitmap frame, int fromY, int toY)
    {
        for (var y = fromY; y < toY; y++)
            for (var x = 0; x < frame.Width; x++)
                if (frame.IsOn(x, y)) return true;
        return false;
    }
}

public class Co2PageTests
{
    [Theory]
    [InlineData(612.0, "612")]
    [InlineData(40000.0, "40000")]
    [InlineData(null, "---")]
    public void Formats_co2(double? ppm, string expected)
    {
        Assert.Equal(expected, TelemetryScreen.FormatCo2(ppm));
    }

    [Theory]
    [InlineData(null, "NO SENSOR")]
    [InlineData(450.0, "GOOD")]
    [InlineData(999.0, "GOOD")]
    [InlineData(1000.0, "VENTILATE")]
    [InlineData(2500.0, "VENTILATE")]
    public void Chooses_a_status_from_the_co2_level(double? ppm, string expected)
    {
        Assert.Equal(expected, TelemetryScreen.Co2Status(ppm));
    }

    [Fact]
    public void Every_line_fits_on_screen_at_the_SCD41_maximum()
    {
        Assert.InRange(TextRenderer.MeasureWidth(TelemetryScreen.FormatCo2(40000), 3), 1, 128);
        Assert.InRange(TextRenderer.MeasureWidth("VENTILATE", 2), 1, 128);
        Assert.InRange(TextRenderer.MeasureWidth("NO SENSOR", 2), 1, 128);
    }

    [Fact]
    public void Shows_a_different_screen_from_the_climate_page()
    {
        using var climate = new MonoBitmap(128, 64);
        using var co2 = new MonoBitmap(128, 64);

        TelemetryScreen.Render(climate, Readings.Of(22.4, 41.7, 612), DisplayPage.Climate);
        TelemetryScreen.Render(co2, Readings.Of(22.4, 41.7, 612), DisplayPage.Co2);

        Assert.NotEqual(climate.AsByteSpan().ToArray(), co2.AsByteSpan().ToArray());
        Assert.True(LitInRows(co2, TelemetryScreen.Co2ValueY, TelemetryScreen.Co2ValueY + 24) > 0, "CO2 value");
    }

    [Fact]
    public void Inverts_the_status_area_when_the_room_needs_airing()
    {
        using var good = new MonoBitmap(128, 64);
        using var stale = new MonoBitmap(128, 64);

        TelemetryScreen.Render(good, Readings.Of(22, 40, 600), DisplayPage.Co2);
        TelemetryScreen.Render(stale, Readings.Of(22, 40, 1450), DisplayPage.Co2);

        // A filled box with dark text lights most of the status rows; plain "GOOD" lights few.
        var statusArea = 128 * (64 - TelemetryScreen.Co2StatusBoxY);
        Assert.True(LitInRows(stale, TelemetryScreen.Co2StatusBoxY, 64) > statusArea / 2);
        Assert.True(LitInRows(good, TelemetryScreen.Co2StatusBoxY, 64) < statusArea / 4);
    }

    [Fact]
    public void Shows_placeholders_when_there_is_no_co2_sensor()
    {
        using var noSensor = new MonoBitmap(128, 64);
        using var expected = new MonoBitmap(128, 64);

        TelemetryScreen.Render(noSensor, Readings.Of(22, 40, co2Ppm: null), DisplayPage.Co2);

        // Same pixels as drawing the placeholder texts directly.
        expected.Clear(System.Drawing.Color.Black);
        TextRenderer.DrawCentered(expected, "CO2 (ppm)", TelemetryScreen.Co2LabelY);
        TextRenderer.DrawCentered(expected, "---", TelemetryScreen.Co2ValueY, scale: 3);
        TextRenderer.DrawCentered(expected, "NO SENSOR", TelemetryScreen.Co2StatusTextY, scale: 2);
        Assert.Equal(expected.AsByteSpan().ToArray(), noSensor.AsByteSpan().ToArray());
    }

    private static int LitInRows(MonoBitmap frame, int fromY, int toY)
    {
        var count = 0;
        for (var y = fromY; y < toY; y++)
            for (var x = 0; x < frame.Width; x++)
                if (frame.IsOn(x, y)) count++;
        return count;
    }
}
