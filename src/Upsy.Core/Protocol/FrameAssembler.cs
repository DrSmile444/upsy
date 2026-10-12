namespace Upsy.Core.Protocol;

/// <summary>Joins BLE notification chunks into whole Modbus frames.</summary>
public sealed class FrameAssembler
{
    private readonly List<byte> _buffer = [];

    public void Reset() => _buffer.Clear();

    /// <summary>Adds a chunk; returns the frame once it is complete, otherwise null.</summary>
    public byte[]? Append(ReadOnlySpan<byte> chunk)
    {
        foreach (var b in chunk)
        {
            _buffer.Add(b);
        }

        var expected = Modbus.ExpectedLength(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_buffer));
        if (expected is null || _buffer.Count < expected.Value)
        {
            return null;
        }

        var frame = _buffer.GetRange(0, expected.Value).ToArray();
        _buffer.Clear();
        return frame;
    }
}
