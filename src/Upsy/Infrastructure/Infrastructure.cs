using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;
using Upsy.Core.Protocol.EcoFlow;
using Upsy.Core.Settings;

namespace Upsy.Infrastructure;

/// <summary>Small rotating text log in %LOCALAPPDATA%\upsy, for diagnosing hours-long runs.</summary>
public static class Log
{
    private const long MaxBytes = 512 * 1024;
    private static readonly object Gate = new();
    private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "upsy");
    private static readonly string FilePath = Path.Combine(Dir, "upsy.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
                {
                    File.Move(FilePath, FilePath + ".1", overwrite: true);
                }

                File.AppendAllText(FilePath, string.Create(CultureInfo.InvariantCulture, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {level} {message}{Environment.NewLine}"));
            }
            catch (IOException)
            {
                // Logging must never take the app down.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

/// <summary>settings.json in %APPDATA%\upsy (version 2, see Upsy.Core.Settings). A version 1 file is migrated once and kept as settings.v1.json.</summary>
public static class SettingsStore
{
    private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "upsy");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    public static UpsySettings Load()
    {
        try
        {
            var json = File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
            var settings = SettingsCodec.Load(json, out var migrated);
            if (migrated)
            {
                File.Copy(FilePath, Path.Combine(Dir, "settings.v1.json"), overwrite: true);
                Save(settings);
                Log.Info($"settings migrated to version {UpsySettings.CurrentVersion} ({settings.Stations.Count} station(s)); the old file is settings.v1.json");
            }

            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.Warn($"settings unreadable, using defaults: {ex.Message}");
            return UpsySettings.Empty;
        }
    }

    public static void Save(UpsySettings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, SettingsCodec.Save(settings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"settings not saved: {ex.Message}");
        }
    }
}

/// <summary>DPAPI for the current Windows user, with app-specific entropy: another account cannot read the EcoFlow user ID.</summary>
public sealed class DpapiProtector : ISecretProtector
{
    private static readonly byte[] Entropy = "upsy.ecoflow.user-id"u8.ToArray();

    public string Protect(string secret) =>
        Convert.ToBase64String(ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser));

    public string? Unprotect(string protectedSecret)
    {
        try
        {
            return System.Text.Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedSecret), Entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            Log.Warn("the stored EcoFlow user ID cannot be read by this Windows account; sign in again");
            return null;
        }
    }
}

/// <summary>The EcoFlow key table in %LOCALAPPDATA%\upsy (fetched by tools/get-ecoflow-keydata.ps1, never in the repo).</summary>
public static class KeyTableStore
{
    public static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "upsy", "ecoflow-keydata.bin");

    /// <summary>Null when the file is missing or is not the expected table.</summary>
    public static EcoFlowKeyTable? Load()
    {
        try
        {
            return File.Exists(FilePath) ? EcoFlowKeyTable.FromBytes(File.ReadAllBytes(FilePath)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"key table unreadable: {ex.Message}");
            return null;
        }
    }
}

/// <summary>Start with Windows via HKCU\...\Run; the registry value is the source of truth.</summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "upsy";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}

/// <summary>Windows light/dark settings.</summary>
public static class WindowsTheme
{
    private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>Taskbar theme (drives the tray icon colour).</summary>
    public static bool TaskbarIsLight => ReadFlag("SystemUsesLightTheme");

    /// <summary>App theme (drives the flyout).</summary>
    public static bool AppsAreLight => ReadFlag("AppsUseLightTheme");

    private static bool ReadFlag(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(Personalize);
        return key?.GetValue(name) is int v && v != 0;
    }
}
