using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Upsy.Core.Alerts;
using Upsy.Core.Station;

namespace Upsy.Core.Settings;

/// <summary>Encrypts small secrets for the current Windows user (DPAPI in the app, reversible fakes in tests).</summary>
public interface ISecretProtector
{
    string Protect(string secret);

    /// <summary>Null when the value cannot be decrypted (another user, a damaged file).</summary>
    string? Unprotect(string protectedSecret);
}

/// <summary>settings.json, version 2: the station list, the tray station, the alert threshold and the protected EcoFlow user ID.</summary>
public sealed record UpsySettings(
    IReadOnlyList<StationEntry> Stations,
    string? TrayStationId = null,
    int LowPercent = AlertMonitor.DefaultLowPercent,
    string? ProtectedEcoFlowUserId = null)
{
    public const int CurrentVersion = 2;

    public static UpsySettings Empty { get; } = new([]);

    public int Version { get; init; } = CurrentVersion;

    public string? EcoFlowUserId(ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);
        return ProtectedEcoFlowUserId is { } p ? protector.Unprotect(p) : null;
    }

    public UpsySettings WithEcoFlowUserId(string? userId, ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);
        return this with { ProtectedEcoFlowUserId = userId is null ? null : protector.Protect(userId) };
    }
}

public static class SettingsCodec
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>Reads version 1 or 2. Version 1 (<c>{ "StationAddress": n, "LowPercent": p }</c>) becomes one EB3A entry; <paramref name="migrated"/> tells the caller to back up the old file.</summary>
    public static UpsySettings Load(string? json, out bool migrated)
    {
        migrated = false;
        if (string.IsNullOrWhiteSpace(json))
        {
            return UpsySettings.Empty;
        }

        var node = JsonNode.Parse(json)?.AsObject() ?? throw new JsonException("settings are not a JSON object");
        if (node["Version"]?.GetValue<int>() is >= UpsySettings.CurrentVersion)
        {
            return node.Deserialize<UpsySettings>(Json) ?? UpsySettings.Empty;
        }

        migrated = true;
        var low = node["LowPercent"]?.GetValue<int>() ?? AlertMonitor.DefaultLowPercent;
        if (node["StationAddress"]?.GetValue<ulong>() is not { } address)
        {
            return UpsySettings.Empty with { LowPercent = low };
        }

        var id = address.ToString("X12", CultureInfo.InvariantCulture);
        return new UpsySettings([new StationEntry(id, StationCatalog.Eb3a.Key, address)], id, low);
    }

    public static string Save(UpsySettings settings) => JsonSerializer.Serialize(settings, Json);
}
