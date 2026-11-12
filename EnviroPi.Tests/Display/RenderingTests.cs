using System.Globalization;
using EnviroPi.Display.Rendering;

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

        TelemetryScreen.Render(frame, 22.4, 41.7);

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

        TelemetryScreen.Render(first, 11.1, 11.1);
        TelemetryScreen.Render(reused, 88.8, 88.8);
        TelemetryScreen.Render(reused, 11.1, 11.1);

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
