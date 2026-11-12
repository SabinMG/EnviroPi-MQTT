using System.Drawing;
using Iot.Device.Graphics;

namespace EnviroPi.Display.Rendering;

/// <summary>
/// A minimal in-memory 32bpp image that the SSD13xx driver can draw.
/// Avoids pulling in SkiaSharp (and its native libraries) just to set pixels.
/// </summary>
public sealed class MonoBitmap : BitmapImage
{
    private const int BytesPerPixel = 4;
    private readonly byte[] _buffer;

    public MonoBitmap(int width, int height)
        : base(width, height, width * BytesPerPixel, PixelFormat.Format32bppArgb)
    {
        _buffer = new byte[width * height * BytesPerPixel];
    }

    /// <summary>
    /// Registers <see cref="MonoBitmap"/> as the image factory used by the Iot.Device graphics
    /// drivers. The SSD1306 driver creates back-buffer images internally (e.g. in ClearScreen),
    /// and throws if no factory is registered.
    /// </summary>
    public static void RegisterAsImageFactory() => RegisterImageFactory(new Factory());

    public bool IsOn(int x, int y) => GetPixel(x, y).GetBrightness() > 0.5f;

    public override void SetPixel(int x, int y, Color color)
    {
        var i = Offset(x, y);
        // Little-endian ARGB: B, G, R, A
        _buffer[i] = color.B;
        _buffer[i + 1] = color.G;
        _buffer[i + 2] = color.R;
        _buffer[i + 3] = color.A;
    }

    public override Color GetPixel(int x, int y)
    {
        var i = Offset(x, y);
        return Color.FromArgb(_buffer[i + 3], _buffer[i + 2], _buffer[i + 1], _buffer[i]);
    }

    public override Span<byte> AsByteSpan() => _buffer;

    public override void SaveToStream(Stream stream, ImageFileType format)
        => throw new NotSupportedException("MonoBitmap is an in-memory frame buffer only.");

    public override IGraphics GetDrawingApi()
        => throw new NotSupportedException("Use TextRenderer to draw on a MonoBitmap.");

    protected override void Dispose(bool disposing)
    {
    }

    private int Offset(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Pixel ({x},{y}) is outside {Width}x{Height}.");
        }
        return (y * Width + x) * BytesPerPixel;
    }

    private sealed class Factory : IImageFactory
    {
        public BitmapImage CreateBitmap(int width, int height, PixelFormat pixelFormat)
            => pixelFormat is PixelFormat.Format32bppArgb or PixelFormat.Format32bppXrgb
                ? new MonoBitmap(width, height)
                : throw new NotSupportedException($"MonoBitmap does not support {pixelFormat}.");

        public BitmapImage CreateFromStream(Stream data)
            => throw new NotSupportedException("MonoBitmap cannot load image files.");
    }
}
