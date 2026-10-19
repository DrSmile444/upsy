// EcoFlow BLE framing. Protocol knowledge from rabits/ha-ef-ble (Apache-2.0), see THIRD-PARTY-NOTICES.md.
using System.Buffers.Binary;

namespace Upsy.Core.Protocol.EcoFlow;

public static class EcoFlowCrc
{
    /// <summary>CRC-8, poly 0x07, init 0, not reflected (packet header).</summary>
    public static byte Crc8(ReadOnlySpan<byte> data)
    {
        byte crc = 0;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (byte)((crc & 0x80) != 0 ? (crc << 1) ^ 0x07 : crc << 1);
            }
        }

        return crc;
    }

    /// <summary>CRC-16/ARC: poly 0x8005 reflected (0xA001), init 0.</summary>
    public static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (ushort)((crc & 1) != 0 ? (crc >> 1) ^ 0xA001 : crc >> 1);
            }
        }

        return crc;
    }
}

/// <summary>
/// Inner packet, protocol version 2:
/// <c>AA 02 len(LE16) crc8 0D seq(4) 00 00 src dst cmdSet cmdId payload crc16(LE)</c>.
/// Stations XOR the payload with <c>seq[0]</c> when that byte is non-zero.
/// </summary>
public sealed record EcoFlowPacket(byte Src, byte Dst, byte CmdSet, byte CmdId, byte[] Payload, uint Seq = 0)
{
    public const byte Prefix = 0xAA;
    public const byte Version = 0x02;
    private const int HeaderLength = 16;

    public byte[] ToBytes()
    {
        var bytes = new byte[HeaderLength + Payload.Length + 2];
        bytes[0] = Prefix;
        bytes[1] = Version;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), (ushort)Payload.Length);
        bytes[4] = EcoFlowCrc.Crc8(bytes.AsSpan(0, 4));
        bytes[5] = 0x0D;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(6), Seq);
        bytes[12] = Src;
        bytes[13] = Dst;
        bytes[14] = CmdSet;
        bytes[15] = CmdId;
        Payload.CopyTo(bytes, HeaderLength);
        var crc = EcoFlowCrc.Crc16(bytes.AsSpan(0, HeaderLength + Payload.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(HeaderLength + Payload.Length), crc);
        return bytes;
    }

    /// <summary>Parses a version 2 packet, checking both CRCs and undoing the payload XOR.</summary>
    public static EcoFlowPacket? TryParse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderLength + 2 || data[0] != Prefix || (data[1] & 0x0F) != Version || EcoFlowCrc.Crc8(data[..4]) != data[4])
        {
            return null;
        }

        var length = BinaryPrimitives.ReadUInt16LittleEndian(data[2..]);
        var end = HeaderLength + length;
        if (end + 2 > data.Length || EcoFlowCrc.Crc16(data[..end]) != BinaryPrimitives.ReadUInt16LittleEndian(data[end..]))
        {
            return null;
        }

        var payload = data[HeaderLength..end].ToArray();
        var xor = data[6];
        if (xor != 0)
        {
            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] ^= xor;
            }
        }

        return new EcoFlowPacket(data[12], data[13], data[14], data[15], payload, BinaryPrimitives.ReadUInt32LittleEndian(data[6..]));
    }
}

/// <summary>Outer BLE frame: <c>5A 5A type&lt;&lt;4 01 len(LE16, includes crc) payload crc16(LE)</c>.</summary>
public static class EcoFlowFrame
{
    public const byte TypeCommand = 0x00;
    public const byte TypeProtocol = 0x01;
    private const int HeaderLength = 6;

    public static byte[] Build(byte frameType, ReadOnlySpan<byte> payload)
    {
        var bytes = new byte[HeaderLength + payload.Length + 2];
        bytes[0] = 0x5A;
        bytes[1] = 0x5A;
        bytes[2] = (byte)(frameType << 4);
        bytes[3] = 0x01;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), (ushort)(payload.Length + 2));
        payload.CopyTo(bytes.AsSpan(HeaderLength));
        var crc = EcoFlowCrc.Crc16(bytes.AsSpan(0, HeaderLength + payload.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(HeaderLength + payload.Length), crc);
        return bytes;
    }

    internal static int HeaderSize => HeaderLength;
}

/// <summary>Joins notifications into outer frames, resynchronising on the 5A 5A prefix after garbage or a bad CRC.</summary>
public sealed class EcoFlowFrameReader
{
    private const int MaxFrame = 10_000;
    private readonly List<byte> _buffer = [];

    public void Append(ReadOnlySpan<byte> chunk)
    {
        foreach (var b in chunk)
        {
            _buffer.Add(b);
        }
    }

    /// <summary>Takes the next complete frame; false when more data is needed.</summary>
    public bool TryTake(out byte frameType, out byte[] payload)
    {
        frameType = 0;
        payload = [];
        while (true)
        {
            var start = IndexOfPrefix(0);
            if (start < 0)
            {
                // Keep a trailing 5A that may start the next frame.
                var keep = _buffer.Count > 0 && _buffer[^1] == 0x5A ? 1 : 0;
                _buffer.RemoveRange(0, _buffer.Count - keep);
                return false;
            }

            _buffer.RemoveRange(0, start);
            if (_buffer.Count < 8)
            {
                return false;
            }

            var length = _buffer[4] | (_buffer[5] << 8);
            if (length < 2 || length > MaxFrame)
            {
                _buffer.RemoveRange(0, 2);
                continue;
            }

            var total = EcoFlowFrame.HeaderSize + length;
            if (_buffer.Count < total)
            {
                return false;
            }

            var frame = _buffer.GetRange(0, total).ToArray();
            var body = frame.AsSpan(0, total - 2);
            if (EcoFlowCrc.Crc16(body) != BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(total - 2)))
            {
                _buffer.RemoveRange(0, 2);
                continue;
            }

            _buffer.RemoveRange(0, total);
            frameType = (byte)(frame[2] >> 4);
            payload = frame[EcoFlowFrame.HeaderSize..(total - 2)];
            return true;
        }
    }

    private int IndexOfPrefix(int from)
    {
        for (var i = from; i + 1 < _buffer.Count; i++)
        {
            if (_buffer[i] == 0x5A && _buffer[i + 1] == 0x5A)
            {
                return i;
            }
        }

        return -1;
    }
}
