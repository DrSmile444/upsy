using Upsy.Core.Protocol;
using Upsy.Core.Station;

namespace Upsy.Core.Tests;

public sealed class ProtocolTests
{
    [Fact]
    public void Read_request_matches_the_frame_captured_in_the_spike() =>
        Assert.Equal("0103000A002865D6", Convert.ToHexString(Modbus.BuildReadRequest(10, 40)));

    [Fact]
    public void Valid_response_returns_registers()
    {
        var result = Modbus.ParseReadResponse(Fixtures.Response(Fixtures.DumpCore), 40);
        Assert.True(result.IsSuccess);
        Assert.Equal(Fixtures.DumpCore, result.Registers);
    }

    [Fact]
    public void Bad_checksum_is_rejected()
    {
        var frame = Fixtures.Response(Fixtures.DumpCore);
        frame[^1] ^= 0xFF;
        Assert.Equal("bad checksum", Modbus.ParseReadResponse(frame, 40).Error);
    }

    [Fact]
    public void Short_frame_is_rejected() => Assert.False(Modbus.ParseReadResponse([0x01, 0x03, 0x02], 1).IsSuccess);

    [Fact]
    public void Wrong_register_count_is_rejected() =>
        Assert.Equal("wrong length", Modbus.ParseReadResponse(Fixtures.Response([1, 2]), 3).Error);

    [Fact]
    public void Exception_response_is_reported()
    {
        byte[] head = [0x01, 0x83, 0x02];
        var crc = Modbus.Crc16(head);
        byte[] frame = [.. head, (byte)crc, (byte)(crc >> 8)];
        Assert.Equal("station error 2", Modbus.ParseReadResponse(frame, 40).Error);
    }

    [Fact]
    public void Split_notifications_are_joined_into_one_frame()
    {
        var frame = Fixtures.Response(Fixtures.DumpCore);
        var assembler = new FrameAssembler();
        Assert.Null(assembler.Append(frame.AsSpan(0, 2)));
        Assert.Null(assembler.Append(frame.AsSpan(2, 40)));
        var whole = assembler.Append(frame.AsSpan(42));
        Assert.Equal(frame, whole);
    }

    [Fact]
    public void Exception_frame_completes_after_five_bytes()
    {
        var assembler = new FrameAssembler();
        Assert.NotNull(assembler.Append([0x01, 0x83, 0x02, 0xC0, 0xF1]));
    }
}

public sealed class DecoderTests
{
    [Fact]
    public void Identity_from_the_live_dump()
    {
        var id = Eb3aRegisters.DecodeIdentity(Fixtures.DumpCore);
        Assert.Equal("EB3A", id.Model);
        Assert.Equal("2240000000001", id.Serial);
        Assert.Equal("ARM 2063.02 · DSP 2052.07", id.FirmwareText);
    }

    [Fact]
    public void Telemetry_from_the_charging_capture()
    {
        var t = Eb3aRegisters.DecodeTelemetry(Fixtures.Core(battery: 54, acIn: 390, acOut: 126), Fixtures.Detail(228.4));
        Assert.Equal(54, t.BatteryPercent);
        Assert.Equal(390, t.AcInputW);
        Assert.Equal(126, t.AcOutputW);
        Assert.Equal(228.4, t.AcInputVolts, 1);
        Assert.True(t.AcOutputOn);
        Assert.False(t.DcOutputOn);
    }

    [Fact]
    public void Telemetry_from_the_live_dump()
    {
        var t = Eb3aRegisters.DecodeTelemetry(Fixtures.DumpCore, Fixtures.Detail(0));
        Assert.Equal(98, t.BatteryPercent);
        Assert.Equal(149, t.AcOutputW);
    }

    [Fact]
    public void Settings_from_the_live_dump()
    {
        var s = Eb3aRegisters.DecodeSettings(Fixtures.DumpSettings);
        Assert.Equal(
            [
                new SettingReadout("Charging mode", "Standard"),
                new SettingReadout("ECO mode", "On · off after 1 h at ≤10 W"),
                new SettingReadout("Power lifting", "Off"),
                new SettingReadout("Light", "Off"),
            ],
            s.Items);
        Assert.False(s.LightOn);
    }

    [Theory]
    [InlineData(1, "Low", true)]
    [InlineData(2, "High", true)]
    [InlineData(3, "SOS", true)]
    [InlineData(4, "Off", false)]
    [InlineData(9, "Unknown", false)]
    public void Led_modes(int raw, string text, bool on)
    {
        var s = Eb3aRegisters.DecodeSettings(Fixtures.Settings(raw, 0, 1, 0, 0));
        Assert.Equal(text, s.Items.Single(i => i.Label == "Light").Value);
        Assert.Equal(on, s.LightOn);
    }

    [Theory]
    [InlineData(0, "Standard")]
    [InlineData(1, "Silent")]
    [InlineData(2, "Turbo")]
    public void Charging_modes(int raw, string expected) =>
        Assert.Equal(expected, Eb3aRegisters.DecodeSettings(Fixtures.Settings(4, 0, 1, raw, 0)).Items.Single(i => i.Label == "Charging mode").Value);
}
