using System.Device.I2c;

namespace EnviroPi.Tests.TestDoubles;

/// <summary>
/// In-memory I2C device: records every write and answers reads with canned responses
/// keyed by the last command written. Lets tests run the real Iot.Device drivers.
/// </summary>
internal sealed class FakeI2cDevice(int address = 0x3C) : I2cDevice
{
    private byte[] _lastWrite = [];

    public List<byte[]> Writes { get; } = [];

    /// <summary>Response bytes per command, keyed by the command's hex string (e.g. "EC05").</summary>
    public Dictionary<string, byte[]> Responses { get; } = [];

    /// <summary>When set, every write throws, simulating a disconnected or faulty bus.</summary>
    public bool FailWrites { get; set; }

    public override I2cConnectionSettings ConnectionSettings { get; } = new(1, address);

    public bool IsDisposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (FailWrites) throw new IOException("Simulated I2C failure");
        _lastWrite = buffer.ToArray();
        Writes.Add(_lastWrite);
    }

    public override void WriteByte(byte value) => Write([value]);

    public override byte ReadByte()
    {
        Span<byte> one = stackalloc byte[1];
        Read(one);
        return one[0];
    }

    public override void Read(Span<byte> buffer)
    {
        buffer.Clear();
        if (Responses.TryGetValue(Convert.ToHexString(_lastWrite), out var response))
        {
            response.AsSpan(0, Math.Min(response.Length, buffer.Length)).CopyTo(buffer);
        }
    }

    public override void WriteRead(ReadOnlySpan<byte> writeBuffer, Span<byte> readBuffer)
    {
        Write(writeBuffer);
        Read(readBuffer);
    }

    /// <summary>Encodes 16-bit words the Sensirion way: big-endian, each followed by a CRC-8 (poly 0x31, init 0xFF).</summary>
    public static byte[] SensirionWords(params ushort[] words)
    {
        var bytes = new List<byte>();
        foreach (var word in words)
        {
            byte hi = (byte)(word >> 8), lo = (byte)word;
            bytes.AddRange([hi, lo, SensirionCrc(hi, lo)]);
        }
        return [.. bytes];
    }

    private static byte SensirionCrc(byte hi, byte lo)
    {
        byte crc = 0xFF;
        foreach (var b in new[] { hi, lo })
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x80) != 0 ? (byte)((crc << 1) ^ 0x31) : (byte)(crc << 1);
            }
        }
        return crc;
    }
}
