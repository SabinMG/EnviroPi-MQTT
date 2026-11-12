using System.Drawing;
using Iot.Device.Graphics;

namespace EnviroPi.Display.Rendering;

/// <summary>
/// Draws text onto a <see cref="BitmapImage"/> using the built-in 5x8 pixel font,
/// scaled up by an integer factor. Pixels outside the image are clipped.
/// </summary>
public static class TextRenderer
{
    private static readonly Font5x8 Font = new();

    // Spacing between characters, in unscaled pixels.
    private const int CharSpacing = 1;

    // Font5x8 only covers ASCII 32..127, so the degree sign is drawn from this glyph.
    private static readonly ushort[] DegreeGlyph =
    [
        0b01100,
        0b10010,
        0b10010,
        0b01100,
        0, 0, 0, 0
    ];

    public static int CharWidth => Font.Width;
    public static int CharHeight => Font.Height;

    /// <summary>Width in pixels of <paramref name="text"/> at <paramref name="scale"/>.</summary>
    public static int MeasureWidth(string text, int scale = 1)
        => text.Length == 0 ? 0 : (text.Length * (Font.Width + CharSpacing) - CharSpacing) * scale;

    /// <summary>Draws <paramref name="text"/> with its top-left corner at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static void DrawText(BitmapImage image, string text, int x, int y, int scale = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(scale, 1);

        foreach (var c in text)
        {
            DrawChar(image, c, x, y, scale);
            x += (Font.Width + CharSpacing) * scale;
        }
    }

    /// <summary>Draws <paramref name="text"/> horizontally centred on the image.</summary>
    public static void DrawCentered(BitmapImage image, string text, int y, int scale = 1)
        => DrawText(image, text, (image.Width - MeasureWidth(text, scale)) / 2, y, scale);

    private static void DrawChar(BitmapImage image, char c, int x, int y, int scale)
    {
        ReadOnlySpan<ushort> rows;
        if (c == '°')
        {
            rows = DegreeGlyph;
        }
        else
        {
            Font.GetCharData(c, out rows);
        }

        for (var row = 0; row < rows.Length; row++)
        {
            for (var col = 0; col < Font.Width; col++)
            {
                // Leftmost column is the highest of the glyph's Width bits.
                if ((rows[row] & (1 << (Font.Width - 1 - col))) != 0)
                {
                    FillBlock(image, x + col * scale, y + row * scale, scale);
                }
            }
        }
    }

    private static void FillBlock(BitmapImage image, int x, int y, int size)
    {
        for (var dy = 0; dy < size; dy++)
        {
            for (var dx = 0; dx < size; dx++)
            {
                int px = x + dx, py = y + dy;
                if (px >= 0 && py >= 0 && px < image.Width && py < image.Height)
                {
                    image.SetPixel(px, py, Color.White);
                }
            }
        }
    }
}
