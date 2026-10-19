using Upsy.Core.Station;

namespace Upsy.Core.Tests;

/// <summary>Register values captured from the owner's EB3A (local/dump1.log, local/watch*.log).</summary>
internal static class Fixtures
{
    // Registers 10..49 from the live dump: model EB3A, serial 2240000000001 (replaced: the real serial is not kept in the repo), battery 98%, AC out 149 W, AC on.
    public static readonly ushort[] DumpCore =
    [
        17730, 13121, 0, 0, 0, 0, 1019, 32769, 35431, 521, 0, 0, 0, 9694, 3, 8599, 3, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 149, 0, 0, 0, 0, 98, 0, 0, 0, 0, 1, 0,
    ];

    // Registers 3034..3066 from the live dump: LED off, ECO on for 1 h, Standard charging, power lifting off.
    public static readonly ushort[] DumpSettings = Settings(led: 4, eco: 1, ecoHours: 1, charging: 0, lifting: 0);

    public static ushort[] Core(int battery, int dcIn = 0, int acIn = 0, int acOut = 0, int dcOut = 0, bool acOn = true, bool dcOn = false)
    {
        var regs = (ushort[])DumpCore.Clone();
        regs[36 - 10] = (ushort)dcIn;
        regs[37 - 10] = (ushort)acIn;
        regs[38 - 10] = (ushort)acOut;
        regs[39 - 10] = (ushort)dcOut;
        regs[43 - 10] = (ushort)battery;
        regs[48 - 10] = (ushort)(acOn ? 1 : 0);
        regs[49 - 10] = (ushort)(dcOn ? 1 : 0);
        return regs;
    }

    public static ushort[] Detail(double acVolts, double dcVolts = 0)
    {
        var regs = new ushort[Eb3aRegisters.DetailCount];
        regs[77 - 70] = (ushort)Math.Round(acVolts * 10);
        regs[86 - 70] = (ushort)Math.Round(dcVolts * 100);
        return regs;
    }

    public static ushort[] Settings(int led, int eco, int ecoHours, int charging, int lifting)
    {
        var regs = new ushort[Eb3aRegisters.SettingsCount];
        regs[3034 - 3034] = (ushort)led;
        regs[3061 - 3034] = 1;
        regs[3063 - 3034] = (ushort)eco;
        regs[3064 - 3034] = (ushort)ecoHours;
        regs[3065 - 3034] = (ushort)charging;
        regs[3066 - 3034] = (ushort)lifting;
        return regs;
    }

    public static Telemetry T(int battery, int acIn = 0, int acOut = 0, double volts = 2.6, int dcIn = 0, int dcOut = 0) =>
        new(battery, dcIn, acIn, acOut, dcOut, AcOutputOn: true, DcOutputOn: dcOut > 0, volts, 0);

    /// <summary>Wraps registers in a valid response frame.</summary>
    public static byte[] Response(ushort[] regs)
    {
        var frame = new List<byte> { 0x01, 0x03, (byte)(regs.Length * 2) };
        foreach (var r in regs)
        {
            frame.Add((byte)(r >> 8));
            frame.Add((byte)r);
        }

        var crc = Protocol.Modbus.Crc16(frame.ToArray());
        frame.Add((byte)crc);
        frame.Add((byte)(crc >> 8));
        return [.. frame];
    }
}
