// DELTA 2 heartbeat layouts. Offsets from rabits/ha-ef-ble (Apache-2.0) model structs, checked against captures of a
// real DELTA 2 (2026-10-19): SOC, AC in/out, mains voltage, USB and 12 V switches, remaining times. See THIRD-PARTY-NOTICES.md.
using System.Buffers.Binary;
using System.Globalization;
using Upsy.Core.Link;
using Upsy.Core.Station;

namespace Upsy.Core.Protocol.EcoFlow;

/// <summary>Folds the DELTA 2's pushed heartbeats (PD, EMS, INV, MPPT) into telemetry, identity and settings.</summary>
public sealed class Delta2Decoder(string serial)
{
    /// <summary>EMS reports this for a remaining time that does not apply.</summary>
    public const uint NoTime = 5939;

    // PD heartbeat (src 0x02, 0x20/0x02)
    private const int PdFirmware = 5;        // 4 bytes, little-endian version
    private const int PdSoc = 14;
    private const int PdRemain = 19;         // int32 minutes, negative while discharging
    private const int PdUsbOn = 24;
    private static readonly int[] PdPortWatts = [25, 26, 27, 28, 29, 30, 34]; // USB-A x2, QC x2, USB-C x2, 12 V
    private const int PdCarOn = 33;
    private const int PdAcInW = 112;
    private const int PdAcOutW = 114;
    private const int PdDcInW = 116;         // XT60 (solar/car)
    private const int PdMinLength = 118;

    // EMS heartbeat (src 0x03, 0x20/0x02)
    private const int EmsMaxChargeSoc = 12;
    private const int EmsToFull = 17;        // uint32 minutes
    private const int EmsToEmpty = 21;       // uint32 minutes
    private const int EmsMinDischargeSoc = 43;
    private const int EmsMinLength = 25;

    // INV heartbeat (src 0x04, x/0x02)
    private const int InvAcInVolts = 23;     // uint32 mV
    private const int InvAcOn = 45;
    private const int InvXBoost = 46;
    private const int InvMinLength = 46;

    // MPPT heartbeat (src 0x05, 0x20/0x02)
    private const int MpptInVolts = 8;       // uint32 mV
    private const int MpptAcChargeW = 73;

    private byte[]? _pd;
    private byte[]? _ems;
    private byte[]? _inv;
    private byte[]? _mppt;

    /// <summary>Takes one packet; returns true when it was a heartbeat this decoder uses.</summary>
    public bool Apply(EcoFlowPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        switch (packet.Src, packet.CmdSet, packet.CmdId)
        {
            case (0x02, 0x20, 0x02) when packet.Payload.Length >= PdMinLength:
                _pd = packet.Payload;
                return true;
            case (0x03, 0x20, 0x02) when packet.Payload.Length >= EmsMinLength:
                _ems = packet.Payload;
                return true;
            case (0x04, _, 0x02) when packet.Payload.Length >= InvMinLength:
                _inv = packet.Payload;
                return true;
            case (0x05, 0x20, 0x02):
                _mppt = packet.Payload;
                return true;
            default:
                return false;
        }
    }

    /// <summary>The current data, once PD, EMS and INV have each arrived; null before that.</summary>
    public StationData? Snapshot()
    {
        if (_pd is not { } pd || _ems is not { } ems || _inv is not { } inv)
        {
            return null;
        }

        var remain = BinaryPrimitives.ReadInt32LittleEndian(pd.AsSpan(PdRemain));
        var dcInW = U16(pd, PdDcInW);
        var telemetry = new Telemetry(
            BatteryPercent: Math.Min((int)pd[PdSoc], 100),
            DcInputW: dcInW,
            AcInputW: U16(pd, PdAcInW),
            AcOutputW: U16(pd, PdAcOutW),
            DcOutputW: PdPortWatts.Sum(i => pd[i]),
            AcOutputOn: inv[InvAcOn] == 1,
            DcOutputOn: pd[PdUsbOn] == 1 || pd[PdCarOn] == 1,
            AcInputVolts: U32(inv, InvAcInVolts) / 1000.0,
            // MPPT reports a few volts of noise with nothing plugged in.
            DcInputVolts: dcInW > 0 && _mppt is { Length: >= MpptInVolts + 4 } m ? U32(m, MpptInVolts) / 1000.0 : 0,
            ToEmpty: remain < 0 ? Minutes(U32(ems, EmsToEmpty)) : null,
            ToFull: remain > 0 ? Minutes(U32(ems, EmsToFull)) : null);

        var fw = pd.AsSpan(PdFirmware, 4);
        var identity = new StationIdentity(
            StationCatalog.Delta2.Model,
            serial,
            [new FirmwareVersion(string.Empty, string.Create(CultureInfo.InvariantCulture, $"{fw[3]}.{fw[2]}.{fw[1]}.{fw[0]}"))]);

        var settings = new List<SettingReadout> { new("Charge limit", Percent(ems[EmsMaxChargeSoc])) };
        if (ems.Length > EmsMinDischargeSoc)
        {
            settings.Add(new("Discharge limit", Percent(ems[EmsMinDischargeSoc])));
        }

        if (inv.Length > InvXBoost)
        {
            settings.Add(new("X-Boost", inv[InvXBoost] == 1 ? "On" : "Off"));
        }

        if (_mppt is { Length: >= MpptAcChargeW + 2 } mppt)
        {
            settings.Add(new("AC charging power", string.Create(CultureInfo.InvariantCulture, $"{U16(mppt, MpptAcChargeW)} W")));
        }

        return new StationData(telemetry, identity, new StationSettings(settings));
    }

    private static TimeSpan? Minutes(uint minutes) => minutes is 0 or NoTime ? null : TimeSpan.FromMinutes(minutes);

    private static string Percent(byte value) => string.Create(CultureInfo.InvariantCulture, $"{value}%");

    private static int U16(byte[] b, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(offset));

    private static uint U32(byte[] b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(offset));
}
