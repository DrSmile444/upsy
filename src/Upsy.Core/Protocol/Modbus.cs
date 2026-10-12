namespace Upsy.Core.Protocol;

/// <summary>Modbus RTU helpers for the read-only subset the station needs (function 0x03).</summary>
public static class Modbus
{
    public const byte StationAddress = 0x01;
    public const byte ReadHoldingRegisters = 0x03;
    private const byte ExceptionFlag = 0x80;

    public static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
            }
        }

        return crc;
    }

    /// <summary>Builds "read holding registers" for <paramref name="count"/> registers from <paramref name="start"/>.</summary>
    public static byte[] BuildReadRequest(ushort start, ushort count)
    {
        var frame = new byte[8];
        frame[0] = StationAddress;
        frame[1] = ReadHoldingRegisters;
        frame[2] = (byte)(start >> 8);
        frame[3] = (byte)start;
        frame[4] = (byte)(count >> 8);
        frame[5] = (byte)count;
        var crc = Crc16(frame.AsSpan(0, 6));
        frame[6] = (byte)crc;
        frame[7] = (byte)(crc >> 8);
        return frame;
    }

    /// <summary>Total length of the frame that begins with <paramref name="head"/>, or null while too few bytes are known.</summary>
    public static int? ExpectedLength(ReadOnlySpan<byte> head)
    {
        if (head.Length < 2)
        {
            return null;
        }

        if ((head[1] & ExceptionFlag) != 0)
        {
            return 5; // addr, func|0x80, code, crc(2)
        }

        return head.Length < 3 ? null : 3 + head[2] + 2;
    }

    /// <summary>Validates a complete response and returns its registers.</summary>
    public static ReadResult ParseReadResponse(ReadOnlySpan<byte> frame, ushort expectedCount)
    {
        if (frame.Length < 5)
        {
            return ReadResult.Fail("short frame");
        }

        var crc = Crc16(frame[..^2]);
        if (frame[^2] != (byte)crc || frame[^1] != (byte)(crc >> 8))
        {
            return ReadResult.Fail("bad checksum");
        }

        if (frame[0] != StationAddress)
        {
            return ReadResult.Fail("wrong address");
        }

        if (frame[1] == (ReadHoldingRegisters | ExceptionFlag))
        {
            return ReadResult.Fail($"station error {frame[2]}");
        }

        if (frame[1] != ReadHoldingRegisters)
        {
            return ReadResult.Fail("wrong function");
        }

        var byteCount = frame[2];
        if (byteCount != expectedCount * 2 || frame.Length != 3 + byteCount + 2)
        {
            return ReadResult.Fail("wrong length");
        }

        var registers = new ushort[expectedCount];
        for (var i = 0; i < expectedCount; i++)
        {
            registers[i] = (ushort)((frame[3 + (i * 2)] << 8) | frame[4 + (i * 2)]);
        }

        return ReadResult.Ok(registers);
    }
}

public readonly record struct ReadResult(ushort[]? Registers, string? Error)
{
    public bool IsSuccess => Registers is not null;

    public static ReadResult Ok(ushort[] registers) => new(registers, null);

    public static ReadResult Fail(string error) => new(null, error);
}
