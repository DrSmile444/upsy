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

/// <summary>Everything the UI shows, derived from one monitor snapshot. Copy follows DESIGN.md.</summary>
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
    NoticeAction NoticeAction);

public enum NoticeAction
{
    None,
    BluetoothSettings,
    Resume,
}

public static class StatusText
{
    public const int TooltipLimit = 127;
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

    public static StatusView Build(MonitorSnapshot s, TimeSpan? runtime, int lowPercent)
    {
        ArgumentNullException.ThrowIfNull(s);
        var model = s.Identity?.Model is { Length: > 0 } m ? m : "EB3A";
        if (!s.HasData)
        {
            return NoData(s, model);
        }

        var t = s.Telemetry!;
        var p = PowerReading.From(t);
        var source = p.OnMains ? "on mains" : "on battery";
        string headline;
        string? detail;
        Tone tone;
        string tip;

        if (p.OnMains)
        {
            var volts = string.Create(Inv, $"Mains {t.AcInputVolts:0} V");
            if (p.Charging)
            {
                headline = $"Charging · +{p.NetBatteryW} W";
                detail = volts;
                tone = Tone.Ok;
                tip = $"{model} · {t.BatteryPercent}% · charging +{p.NetBatteryW} W";
            }
            else
            {
                headline = t.BatteryPercent >= 100 ? "Full" : "On mains";
                detail = p.LoadPresent ? $"{volts} · passing {p.OutputW} W to the load" : volts;
                tone = Tone.Neutral;
                tip = $"{model} · {t.BatteryPercent}% · on mains";
            }
        }
        else
        {
            var critical = t.BatteryPercent <= AlertMonitor.CriticalPercent;
            var low = t.BatteryPercent <= lowPercent;
            tone = critical ? Tone.Critical : low ? Tone.Caution : Tone.Neutral;
            var left = runtime is { } r ? $"{Runtime(r)} left" : null;
            if (p.Charging)
            {
                headline = $"Charging · +{p.NetBatteryW} W";
                detail = "From DC input";
                tone = Tone.Ok;
                tip = $"{model} · {t.BatteryPercent}% · charging +{p.NetBatteryW} W";
            }
            else if (critical)
            {
                headline = left is null ? "Critical" : $"Critical · {left}";
                detail = "Shut down or hibernate now.";
                tip = $"{model} · {t.BatteryPercent}% · {p.OutputW} W out" + (left is null ? "" : $" · {left}");
            }
            else if (low)
            {
                headline = left is null ? "Low" : $"Low · {left}";
                detail = $"Save your work. Alert threshold is {lowPercent}%.";
                tip = $"{model} · {t.BatteryPercent}% · {p.OutputW} W out" + (left is null ? "" : $" · {left}");
            }
            else
            {
                headline = left is null ? (p.LoadPresent ? "On battery" : "On battery · no load") : Capitalize(left);
                detail = p.LoadPresent ? $"At the current {p.OutputW} W load" : null;
                tip = $"{model} · {t.BatteryPercent}% · {p.OutputW} W out" + (left is null ? "" : $" · {left}");
            }
        }

        return new StatusView(model, $"{model} · {source}", t.BatteryPercent, headline, detail, tone, Clip(tip), p, null, null, NoticeAction.None);
    }

    public static (string Title, string Body) Alert(AlertKind kind, Telemetry t, TimeSpan? runtime, string model)
    {
        ArgumentNullException.ThrowIfNull(t);
        var p = PowerReading.From(t);
        var left = runtime is { } r ? $"{Capitalize(Runtime(r))} left at {p.OutputW} W." : $"{p.OutputW} W out.";
        return kind switch
        {
            AlertKind.MainsLost => ("Mains power lost", $"{model} is on battery at {t.BatteryPercent}%" + (runtime is { } rr ? $", {Runtime(rr)} at {p.OutputW} W." : ".")),
            AlertKind.MainsRestored => ("Mains power is back", p.Charging ? $"{model} is charging at {t.BatteryPercent}%." : $"{model} is on mains at {t.BatteryPercent}%."),
            AlertKind.BatteryLow => ($"Battery low: {t.BatteryPercent}%", $"{left} Save your work."),
            AlertKind.BatteryCritical => ($"Battery critical: {t.BatteryPercent}%", $"{left} Shut down or hibernate now."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static StatusView NoData(MonitorSnapshot s, string model)
    {
        var seen = s.LastSeen is { } ls ? $"{model} · last seen {ls.ToLocalTime():HH:mm}" : s.Identity is null ? "First start" : model;
        return s.Status switch
        {
            LinkStatus.BluetoothOff => new(model, seen, null, "Bluetooth is off", null, Tone.Neutral, "upsy · Bluetooth is off", null,
                "Turn on Bluetooth to see the station", "upsy reconnects by itself as soon as it is on.", NoticeAction.BluetoothSettings),
            LinkStatus.Paused => new(model, model, null, "Paused", null, Tone.Neutral,
                Clip($"upsy · paused until {s.PausedUntil?.ToLocalTime():HH:mm}"), null,
                "Connection paused", $"The Bluetti phone app can use the station now. No alerts while paused. Resumes by itself at {s.PausedUntil?.ToLocalTime():HH:mm}.", NoticeAction.Resume),
            _ when s.Identity is null && s.LastSeen is null => new(model, "First start", null, "Looking for an EB3A…", null, Tone.Neutral, "upsy · looking for the station", null,
                "Turn on the station near this PC", "No pairing needed. upsy finds it by name and remembers it.", NoticeAction.None),
            _ => new(model, seen, null, s.Status == LinkStatus.Connecting ? "Connecting…" : "Looking for the station…", null, Tone.Neutral,
                "upsy · looking for the station", null,
                "Not in range or busy", "Make sure the EB3A is on and the Bluetti app is closed. Retrying every 30 s.", NoticeAction.None),
        };
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string Clip(string text) => text.Length <= TooltipLimit ? text : text[..(TooltipLimit - 1)] + "…";
}
