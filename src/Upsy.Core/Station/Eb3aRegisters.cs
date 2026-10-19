using System.Globalization;
using System.Text;

namespace Upsy.Core.Station;

/// <summary>EB3A register map (read-only). Cross-checked with bluetti_mqtt and a live dump.</summary>
public static class Eb3aRegisters
{
    public const ushort CoreStart = 10;
    public const ushort CoreCount = 40;      // 10..49
    public const ushort DetailStart = 70;
    public const ushort DetailCount = 21;    // 70..90
    public const ushort SettingsStart = 3034;
    public const ushort SettingsCount = 33;  // 3034..3066

    private const int DeviceType = 10;       // 6 registers, ASCII
    private const int Serial = 17;           // 4 registers, little-endian words
    private const int ArmVersion = 23;       // 2 registers
    private const int DspVersion = 25;       // 2 registers
    private const int DcInputPower = 36;
    private const int AcInputPower = 37;
    private const int AcOutputPower = 38;
    private const int DcOutputPower = 39;
    private const int BatteryPercent = 43;
    private const int AcOutputOn = 48;
    private const int DcOutputOn = 49;
    private const int AcInputVoltage = 77;   // x0.1 V
    private const int DcInputVoltage = 86;   // x0.01 V
    private const int LedModeRegister = 3034;
    private const int EcoOn = 3063;
    private const int EcoShutdownHours = 3064;
    private const int ChargingModeRegister = 3065;
    private const int PowerLifting = 3066;

    public static Telemetry DecodeTelemetry(IReadOnlyList<ushort> core, IReadOnlyList<ushort> detail)
    {
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(detail);
        ushort C(int reg) => core[reg - CoreStart];
        ushort D(int reg) => detail[reg - DetailStart];
        return new Telemetry(
            BatteryPercent: Math.Min(C(BatteryPercent), (ushort)100),
            DcInputW: C(DcInputPower),
            AcInputW: C(AcInputPower),
            AcOutputW: C(AcOutputPower),
            DcOutputW: C(DcOutputPower),
            AcOutputOn: C(AcOutputOn) != 0,
            DcOutputOn: C(DcOutputOn) != 0,
            AcInputVolts: D(AcInputVoltage) / 10.0,
            DcInputVolts: D(DcInputVoltage) / 100.0);
    }

    public static StationIdentity DecodeIdentity(IReadOnlyList<ushort> core)
    {
        ArgumentNullException.ThrowIfNull(core);
        ushort C(int reg) => core[reg - CoreStart];

        var model = new StringBuilder();
        for (var r = DeviceType; r < DeviceType + 6; r++)
        {
            foreach (var b in new[] { (byte)(C(r) >> 8), (byte)C(r) })
            {
                if (b is >= 0x20 and < 0x7F)
                {
                    model.Append((char)b);
                }
            }
        }

        ulong serial = 0;
        for (var i = 3; i >= 0; i--)
        {
            serial = (serial << 16) | C(Serial + i);
        }

        string Version(int reg) => ((C(reg) + (C(reg + 1) * 65536L)) / 100.0).ToString("0.00", CultureInfo.InvariantCulture);

        return new StationIdentity(
            model.ToString(),
            serial.ToString(CultureInfo.InvariantCulture),
            [new FirmwareVersion("ARM", Version(ArmVersion)), new FirmwareVersion("DSP", Version(DspVersion))]);
    }

    /// <summary>Settings as readouts for the details list, in the order DESIGN.md shows them.</summary>
    public static StationSettings DecodeSettings(IReadOnlyList<ushort> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ushort S(int reg) => settings[reg - SettingsStart];
        var led = S(LedModeRegister) switch { 1 => LedMode.Low, 2 => LedMode.High, 3 => LedMode.Sos, 4 => LedMode.Off, _ => LedMode.Unknown };
        var charging = S(ChargingModeRegister) switch { 0 => ChargingMode.Standard, 1 => ChargingMode.Silent, 2 => ChargingMode.Turbo, _ => ChargingMode.Unknown };
        var ecoOn = S(EcoOn) != 0;
        return new StationSettings(
            [
                new("Charging mode", charging.ToString()),
                new("ECO mode", ecoOn ? string.Create(CultureInfo.InvariantCulture, $"On · off after {S(EcoShutdownHours)} h at ≤10 W") : "Off"),
                new("Power lifting", S(PowerLifting) != 0 ? "On" : "Off"),
                new("Light", led switch { LedMode.Off => "Off", LedMode.Low => "Low", LedMode.High => "High", LedMode.Sos => "SOS", _ => "Unknown" }),
            ],
            LightOn: led is LedMode.Low or LedMode.High or LedMode.Sos);
    }
}
