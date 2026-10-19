namespace Upsy.Core.Station;

/// <summary>One telemetry snapshot as reported by the station, in the same shape for every model.</summary>
/// <param name="ToEmpty">Remaining time to empty as reported by the station itself (DELTA 2); null when it does not report one.</param>
/// <param name="ToFull">Remaining time to full as reported by the station itself (DELTA 2); null when it does not report one.</param>
public sealed record Telemetry(
    int BatteryPercent,
    int DcInputW,
    int AcInputW,
    int AcOutputW,
    int DcOutputW,
    bool AcOutputOn,
    bool DcOutputOn,
    double AcInputVolts,
    double DcInputVolts,
    TimeSpan? ToEmpty = null,
    TimeSpan? ToFull = null);

public sealed record FirmwareVersion(string Label, string Version);

public sealed record StationIdentity(string Model, string Serial, IReadOnlyList<FirmwareVersion> Firmware)
{
    /// <summary>"ARM 2063.02 · DSP 2052.07".</summary>
    public string FirmwareText => string.Join(" · ", Firmware.Select(f => f.Label.Length == 0 ? f.Version : $"{f.Label} {f.Version}"));
}

/// <summary>A read-only setting as the driver formats it for the details list.</summary>
public sealed record SettingReadout(string Label, string Value);

/// <param name="LightOn">Whether the station's light is on, for models whose glyph has one (EB3A).</param>
public sealed record StationSettings(IReadOnlyList<SettingReadout> Items, bool? LightOn = null);

/// <summary>One BLE advertisement, as the scanner saw it.</summary>
public sealed record Advertisement(ulong Address, string? Name, IReadOnlyDictionary<ushort, byte[]> ManufacturerData, short Rssi);

public sealed record GattProfile(Guid Service, Guid Write, Guid Notify);

/// <summary>Everything model-specific that is not protocol: names, capacity, glyph and link timing.</summary>
/// <param name="Key">Stable id stored in settings and used to pick the glyph ("eb3a", "delta2").</param>
/// <param name="NoUpdateTimeout">The link is treated as failing when no update arrives for this long.</param>
/// <param name="MinReconnectDelay">Wait at least this long after a link ends before connecting again.</param>
/// <param name="DcInputLabel">What the model's DC input is called in the details ("Solar / car input").</param>
/// <param name="DcOutputLabel">What the model's DC outputs are called in the details ("USB and 12 V output").</param>
/// <param name="UsableFactor">Share of the nominal capacity that comes out on discharge, for the runtime estimate.</param>
/// <param name="ChargeEfficiency">Share of the measured net input that ends up in the battery, for the time-to-full estimate.</param>
public sealed record StationProfile(
    string Key,
    string Vendor,
    string Model,
    double NominalCapacityWh,
    GattProfile Gatt,
    TimeSpan NoUpdateTimeout,
    TimeSpan MinReconnectDelay,
    string DcInputLabel = "DC input",
    string DcOutputLabel = "DC output",
    double UsableFactor = 1.0,
    double ChargeEfficiency = 1.0)
{
    public string FullName => $"{Vendor} {Model}";
}

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
