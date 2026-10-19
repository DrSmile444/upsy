using Upsy.Core.Protocol.EcoFlow;

namespace Upsy.Core.Station;

/// <summary>A supported station seen advertising nearby.</summary>
/// <param name="AdvertisedPercent">Battery level carried in the advertisement (EcoFlow), or null.</param>
public sealed record DiscoveredStation(StationProfile Profile, ulong Address, string Serial, string? Name, int? AdvertisedPercent, short Rssi);

/// <summary>The models upsy has a driver for, and how to recognise them from an advertisement.</summary>
public static class StationCatalog
{
    public const string Eb3aNamePrefix = "EB3A";

    public static readonly StationProfile Eb3a = new(
        Key: "eb3a",
        Vendor: "Bluetti",
        Model: "EB3A",
        NominalCapacityWh: 268,
        Gatt: new(Uuid(0xff00), Write: Uuid(0xff02), Notify: Uuid(0xff01)),
        NoUpdateTimeout: TimeSpan.FromSeconds(20),
        MinReconnectDelay: TimeSpan.Zero,
        // Provisional until calibrated from a logged discharge and charge (tools/energy_calibration.py).
        UsableFactor: 0.85,
        ChargeEfficiency: 0.75);

    public static readonly StationProfile Delta2 = new(
        Key: "delta2",
        Vendor: "EcoFlow",
        Model: "DELTA 2",
        NominalCapacityWh: 1024,
        Gatt: new(Uuid(0x0001), Write: Uuid(0x0002), Notify: Uuid(0x0003)),
        NoUpdateTimeout: TimeSpan.FromSeconds(10),
        // After a link ends the station refuses service discovery for 15-20 s.
        MinReconnectDelay: TimeSpan.FromSeconds(5),
        DcInputLabel: "Solar / car input",
        DcOutputLabel: "USB and 12 V output");

    public static IReadOnlyList<StationProfile> All { get; } = [Eb3a, Delta2];

    public static StationProfile? ByKey(string key) => All.FirstOrDefault(p => p.Key == key);

    /// <summary>Recognises a supported station; null for anything else.</summary>
    public static DiscoveredStation? Recognize(Advertisement ad)
    {
        ArgumentNullException.ThrowIfNull(ad);
        if (ad.Name is { } name && name.StartsWith(Eb3aNamePrefix, StringComparison.Ordinal) && name.Length > Eb3aNamePrefix.Length)
        {
            // EB3A advertises "EB3A<serial>".
            return new DiscoveredStation(Eb3a, ad.Address, name[Eb3aNamePrefix.Length..], name, null, ad.Rssi);
        }

        if (ad.ManufacturerData.TryGetValue(EcoFlowAdvertisement.ManufacturerId, out var data)
            && EcoFlowAdvertisement.TryParse(data) is { } ef
            && Delta2Serials.Any(p => ef.Serial.StartsWith(p, StringComparison.Ordinal)))
        {
            return new DiscoveredStation(Delta2, ad.Address, ef.Serial, ad.Name, ef.BatteryPercent, ad.Rssi);
        }

        return null;
    }

    private static readonly string[] Delta2Serials = ["R331", "R335"];

    private static Guid Uuid(ushort shortId) => new($"0000{shortId:x4}-0000-1000-8000-00805f9b34fb");
}
