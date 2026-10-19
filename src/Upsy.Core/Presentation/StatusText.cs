using System.Globalization;
using Upsy.Core.Alerts;
using Upsy.Core.Link;
using Upsy.Core.Power;
using Upsy.Core.Station;

namespace Upsy.Core.Presentation;

public enum Tone
{
    Neutral,
    Ok,
    Caution,
    Critical,
}

public enum NoticeAction
{
    None,
    BluetoothSettings,
    Resume,
    SignIn,
    TryAgain,
    SetupGuide,
}

/// <summary>Which station a view is for: its display name, its model, and the level it advertises (EcoFlow) when known.</summary>
public sealed record StationContext(string Name, StationProfile Profile, int? AdvertisedPercent = null)
{
    public static StationContext Eb3a { get; } = new("EB3A", StationCatalog.Eb3a);
}

/// <summary>Everything the UI shows for one station, derived from one monitor snapshot. Copy follows DESIGN.md.</summary>
/// <param name="LevelOnly">The percentage comes from the advertisement: no power, runtime or alerts.</param>
/// <param name="Brief">One short line for the tooltip when another station leads it ("DELTA 2 · 82% · 103 W out").</param>
public sealed record StatusView(
    string Model,
    string Source,
    int? Percent,
    string Headline,
    string? Detail,
    Tone Tone,
    string Tooltip,
    PowerReading? Power,
    string? NoticeTitle,
    string? NoticeBody,
    NoticeAction NoticeAction,
    string Name = "",
    bool LevelOnly = false,
    string Brief = "");

public static class StatusText
{
    public const int TooltipLimit = 127;

    /// <summary>A station advertising but refusing the link this many times in a row is probably held by its phone app.</summary>
    public const int BusyAfterFailures = 3;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>"about 20 min", "about 1 h", "about 1 h 40 min", "more than 24 h".</summary>
    public static string Runtime(TimeSpan span)
    {
        if (span >= RuntimeEstimator.Cap)
        {
            return "more than 24 h";
        }

        var minutes = span.TotalMinutes;
        var step = minutes < 60 ? 5 : 10;
        var rounded = (int)(Math.Round(minutes / step, MidpointRounding.AwayFromZero) * step);
        rounded = Math.Max(rounded, 5);
        var h = rounded / 60;
        var m = rounded % 60;
        return h == 0 ? $"about {m} min" : m == 0 ? $"about {h} h" : $"about {h} h {m} min";
    }

    public static StatusView Build(MonitorSnapshot s, TimeSpan? runtime, int lowPercent, StationContext? station = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        station ??= StationContext.Eb3a;
        var name = station.Name;
        if (!s.HasData)
        {
            return NoData(s, station);
        }

        var t = s.Telemetry!;
        var p = PowerReading.From(t);
        var source = p.OnMains ? "on mains" : "on battery";
        string headline;
        string? detail;
        Tone tone;
        string tip;
        string brief;

        if (p.OnMains)
        {
            var volts = string.Create(Inv, $"Mains {t.AcInputVolts:0} V");
            if (p.Charging)
            {
                headline = $"Charging · +{p.NetBatteryW} W";
                detail = t.ToFull is { } full ? $"Full in {Runtime(full)} · {Lower(volts)}" : volts;
                tone = Tone.Ok;
                tip = $"{name} · {t.BatteryPercent}% · charging +{p.NetBatteryW} W";
                brief = $"{name} · {t.BatteryPercent}% · charging";
            }
            else
            {
                headline = t.BatteryPercent >= 100 ? "Full" : "On mains";
                detail = p.LoadPresent ? $"{volts} · passing {p.OutputW} W to the load" : volts;
                tone = Tone.Neutral;
                tip = $"{name} · {t.BatteryPercent}% · on mains";
                brief = tip;
            }
        }
        else
        {
            var critical = t.BatteryPercent <= AlertMonitor.CriticalPercent;
            var low = t.BatteryPercent <= lowPercent;
            tone = critical ? Tone.Critical : low ? Tone.Caution : Tone.Neutral;
            var left = runtime is { } r ? $"{Runtime(r)} left" : null;
            brief = $"{name} · {t.BatteryPercent}% · {p.OutputW} W out";
            if (p.Charging)
            {
                headline = $"Charging · +{p.NetBatteryW} W";
                detail = t.ToFull is { } full ? $"Full in {Runtime(full)} · from DC input" : "From DC input";
                tone = Tone.Ok;
                tip = $"{name} · {t.BatteryPercent}% · charging +{p.NetBatteryW} W";
                brief = $"{name} · {t.BatteryPercent}% · charging";
            }
            else if (critical)
            {
                headline = left is null ? "Critical" : $"Critical · {left}";
                detail = "Shut down or hibernate now.";
                tip = brief + (left is null ? "" : $" · {left}");
            }
            else if (low)
            {
                headline = left is null ? "Low" : $"Low · {left}";
                detail = $"Save your work. Alert threshold is {lowPercent}%.";
                tip = brief + (left is null ? "" : $" · {left}");
            }
            else
            {
                headline = left is null ? (p.LoadPresent ? "On battery" : "On battery · no load") : Capitalize(left);
                detail = p.LoadPresent ? $"At the current {p.OutputW} W load" : null;
                tip = brief + (left is null ? "" : $" · {left}");
            }
        }

        return new StatusView(s.Identity?.Model ?? station.Profile.Model, $"{name} · {source}", t.BatteryPercent, headline, detail, tone, Clip(tip), p,
            null, null, NoticeAction.None, name, Brief: Clip(brief));
    }

    /// <summary>The flyout and tooltip before any station is known.</summary>
    public static StatusView FirstStart() => new(
        string.Empty, "First start", null, "Looking for a station…", null, Tone.Neutral, "upsy · looking for a station", null,
        "Turn on the station near this PC",
        "upsy reads the Bluetti EB3A and the EcoFlow DELTA 2. No pairing needed: it adds the first one it finds and remembers it.",
        NoticeAction.None, "upsy", Brief: "upsy · looking for a station");

    /// <summary>The tray station's tooltip, then one short line per other station, dropping those lines first at the limit.</summary>
    public static string Tooltip(StatusView tray, IEnumerable<StatusView> others)
    {
        ArgumentNullException.ThrowIfNull(tray);
        ArgumentNullException.ThrowIfNull(others);
        var text = tray.Tooltip;
        foreach (var other in others)
        {
            var next = $"{text}\n{other.Brief}";
            if (next.Length > TooltipLimit)
            {
                break;
            }

            text = next;
        }

        return text;
    }

    public static (string Title, string Body) Alert(AlertKind kind, Telemetry t, TimeSpan? runtime, string name)
    {
        ArgumentNullException.ThrowIfNull(t);
        var p = PowerReading.From(t);
        var left = runtime is { } r ? $"{Capitalize(Runtime(r))} left at {p.OutputW} W." : $"{p.OutputW} W out.";
        return kind switch
        {
            AlertKind.MainsLost => ($"{name}: mains power lost", $"On battery at {t.BatteryPercent}%" + (runtime is { } rr ? $", {Runtime(rr)} at {p.OutputW} W." : ".")),
            AlertKind.MainsRestored => ($"{name}: mains power is back", p.Charging
                ? $"Charging at {t.BatteryPercent}%" + (t.ToFull is { } full ? $", full in {Runtime(full)}." : ".")
                : $"On mains at {t.BatteryPercent}%."),
            AlertKind.BatteryLow => ($"{name}: battery low, {t.BatteryPercent}%", $"{left} Save your work."),
            AlertKind.BatteryCritical => ($"{name}: battery critical, {t.BatteryPercent}%", $"{left} Shut down or hibernate now."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static StatusView NoData(MonitorSnapshot s, StationContext station)
    {
        var name = station.Name;
        var model = station.Profile.Model;
        var vendor = station.Profile.Vendor;
        var seen = s.LastSeen is { } ls ? $"{name} · last seen {ls.ToLocalTime():HH:mm}" : name;
        var level = station.AdvertisedPercent;
        StatusView Level(string headline, string title, string body, NoticeAction action) => new(
            model, $"{name} · battery level only", level, headline, null, Tone.Neutral,
            Clip(level is null ? $"{name} · {Lower(headline)}" : $"{name} · {level}% · battery level only"), null, title, body, action, name,
            LevelOnly: level is not null, Brief: Clip(level is null ? $"{name} · {Lower(headline)}" : $"{name} · {level}% · level only"));

        return s.Status switch
        {
            LinkStatus.SignInNeeded => Level("Sign-in needed", "Connect your EcoFlow account",
                $"The {model} shares data only with the account it is added to in the EcoFlow app. upsy keeps just the account’s user ID; your password is not stored.",
                NoticeAction.SignIn),
            LinkStatus.KeyRejected => Level("Account not accepted", $"Add the {model} again in the EcoFlow app",
                $"The station remembers a different account. In the EcoFlow app, remove the {model} and add it again with this account, then try again.",
                NoticeAction.TryAgain),
            LinkStatus.KeyTableMissing => Level("EcoFlow not set up", "The EcoFlow key table is missing",
                "upsy needs a one-time setup step for EcoFlow stations. The guide shows the command; other stations are not affected.",
                NoticeAction.SetupGuide),
            LinkStatus.Searching or LinkStatus.Connecting when level is not null && s.Failures >= BusyAfterFailures => Level("In use by another app",
                $"The {vendor} app may be using the station",
                $"The {model} talks to one app at a time. Close the {vendor} app on your phone; upsy reconnects by itself.", NoticeAction.None),
            LinkStatus.BluetoothOff => new(model, seen, null, "Bluetooth is off", null, Tone.Neutral, "upsy · Bluetooth is off", null,
                "Turn on Bluetooth to see the station", "upsy reconnects by itself as soon as it is on.", NoticeAction.BluetoothSettings,
                name, Brief: $"{name} · Bluetooth is off"),
            LinkStatus.Paused => new(model, name, null, "Paused", null, Tone.Neutral,
                Clip($"{name} · paused until {s.PausedUntil?.ToLocalTime():HH:mm}"), null,
                "Connection paused", $"The {vendor} phone app can use the station now. No alerts while paused. Resumes by itself at {s.PausedUntil?.ToLocalTime():HH:mm}.",
                NoticeAction.Resume, name, Brief: $"{name} · paused"),
            _ => new(model, seen, null, s.Status == LinkStatus.Connecting ? "Connecting…" : "Looking for the station…", null, Tone.Neutral,
                $"{name} · looking for the station", null,
                "Not in range or busy", $"Make sure the {model} is on and the {vendor} app is closed. Retrying every 30 s.", NoticeAction.None,
                name, Brief: $"{name} · not connected"),
        };
    }

    private static string Lower(string text) => text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string Clip(string text) => text.Length <= TooltipLimit ? text : text[..(TooltipLimit - 1)] + "…";
}
