namespace Upsy.Core.Station;

/// <summary>One telemetry snapshot as reported by the station.</summary>
public sealed record Telemetry(
    int BatteryPercent,
    int DcInputW,
    int AcInputW,
    int AcOutputW,
    int DcOutputW,
    bool AcOutputOn,
    bool DcOutputOn,
    double AcInputVolts,
    double DcInputVolts);

public sealed record StationIdentity(string Model, string Serial, string ArmFirmware, string DspFirmware);

public sealed record StationSettings(LedMode Led, bool EcoOn, int EcoShutdownHours, ChargingMode Charging, bool PowerLiftingOn);

public enum LedMode
{
    Unknown,
    Low,
    High,
    Sos,
    Off,
}

public enum ChargingMode
{
    Unknown,
    Standard,
    Silent,
    Turbo,
}
