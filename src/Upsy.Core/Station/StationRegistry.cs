using System.Globalization;
using System.Text.Json.Serialization;

namespace Upsy.Core.Station;

/// <summary>A station the user keeps in upsy. <see cref="Id"/> is stable for its lifetime (serial when known at add time, else the address).</summary>
public sealed record StationEntry(string Id, string ProfileKey, ulong Address, string? Serial = null)
{
    [JsonIgnore]
    public StationProfile? Profile => StationCatalog.ByKey(ProfileKey);
}

/// <summary>The list of stations, their display names and which one the tray icon shows.</summary>
public sealed class StationRegistry
{
    private readonly List<StationEntry> _stations;
    private string? _trayId;

    public StationRegistry(IEnumerable<StationEntry> stations, string? trayStationId)
    {
        ArgumentNullException.ThrowIfNull(stations);
        _stations = [.. stations.Where(s => s.Profile is not null)];
        _trayId = trayStationId;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<StationEntry> Stations => _stations;

    /// <summary>The station the tray icon shows: the chosen one, else the first; null when the list is empty.</summary>
    public StationEntry? TrayStation => _stations.FirstOrDefault(s => s.Id == _trayId) ?? _stations.FirstOrDefault();

    /// <summary>Stations in flyout order: the tray station first, then the rest in the order they were added.</summary>
    public IEnumerable<StationEntry> Ordered => TrayStation is { } tray ? [tray, .. _stations.Where(s => s != tray)] : [];

    public bool Contains(DiscoveredStation station)
    {
        ArgumentNullException.ThrowIfNull(station);
        return Find(station) is not null;
    }

    /// <summary>The listed station this advertisement belongs to, matched by serial and model, else by address.</summary>
    public StationEntry? Find(DiscoveredStation station)
    {
        ArgumentNullException.ThrowIfNull(station);
        return _stations.FirstOrDefault(s => s.ProfileKey == station.Profile.Key && s.Serial is { } serial && serial == station.Serial)
            ?? _stations.FirstOrDefault(s => s.Address == station.Address);
    }

    public StationEntry Add(DiscoveredStation station)
    {
        ArgumentNullException.ThrowIfNull(station);
        if (Find(station) is { } existing)
        {
            return existing;
        }

        var entry = new StationEntry(station.Serial.Length > 0 ? station.Serial : station.Address.ToString("X12", CultureInfo.InvariantCulture), station.Profile.Key, station.Address, station.Serial);
        _stations.Add(entry);
        Changed?.Invoke(this, EventArgs.Empty);
        return entry;
    }

    public void Forget(string id)
    {
        if (_stations.RemoveAll(s => s.Id == id) > 0)
        {
            if (_trayId == id)
            {
                _trayId = null;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetTrayStation(string id)
    {
        if (_stations.Any(s => s.Id == id) && _trayId != id)
        {
            _trayId = id;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Records what a connection learned: a changed address or the serial of a station migrated without one.</summary>
    public void Update(string id, ulong address, string? serial)
    {
        var i = _stations.FindIndex(s => s.Id == id);
        if (i < 0)
        {
            return;
        }

        var updated = _stations[i] with { Address = address, Serial = serial ?? _stations[i].Serial };
        if (updated != _stations[i])
        {
            _stations[i] = updated;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The model name, with a short serial suffix only when two listed stations share a model ("DELTA 2 ·0140").</summary>
    public string DisplayName(StationEntry station)
    {
        ArgumentNullException.ThrowIfNull(station);
        var model = station.Profile?.Model ?? station.ProfileKey;
        var twins = _stations.Count(s => s.ProfileKey == station.ProfileKey) > 1;
        if (!twins)
        {
            return model;
        }

        var tail = station.Serial is { Length: >= 4 } serial ? serial[^4..] : station.Address.ToString("X4", CultureInfo.InvariantCulture)[^4..];
        return $"{model} ·{tail}";
    }

    public string? TrayStationId => TrayStation?.Id;
}
