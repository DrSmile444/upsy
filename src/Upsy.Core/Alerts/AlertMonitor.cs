using Upsy.Core.Power;
using Upsy.Core.Station;

namespace Upsy.Core.Alerts;

public enum AlertKind
{
    MainsLost,
    MainsRestored,
    BatteryLow,
    BatteryCritical,
}

/// <summary>Turns a stream of snapshots into one-shot alerts (edge detection, debounce, re-arm).</summary>
public sealed class AlertMonitor(TimeProvider clock)
{
    public const int CriticalPercent = 10;
    public const int DefaultLowPercent = 20;
    public static readonly TimeSpan MainsDebounce = TimeSpan.FromSeconds(5);

    private bool? _confirmedOnMains;
    private bool? _candidateOnMains;
    private DateTimeOffset _candidateSince;
    private bool _lowArmed = true;
    private bool _criticalArmed = true;

    public int LowPercent { get; set; } = DefaultLowPercent;

    /// <summary>Call when data is unavailable (disconnected or paused). The next snapshot is treated as a fresh start.</summary>
    public void ObserveNoData()
    {
        _confirmedOnMains = null;
        _candidateOnMains = null;
    }

    public IReadOnlyList<AlertKind> Observe(Telemetry t)
    {
        ArgumentNullException.ThrowIfNull(t);
        var p = PowerReading.From(t);
        var now = clock.GetUtcNow();
        var alerts = new List<AlertKind>();

        if (_confirmedOnMains is null)
        {
            _confirmedOnMains = p.OnMains;
            _candidateOnMains = null;
        }
        else if (p.OnMains != _confirmedOnMains)
        {
            if (_candidateOnMains != p.OnMains)
            {
                _candidateOnMains = p.OnMains;
                _candidateSince = now;
            }
            else if (now - _candidateSince >= MainsDebounce)
            {
                _confirmedOnMains = p.OnMains;
                _candidateOnMains = null;
                alerts.Add(p.OnMains ? AlertKind.MainsRestored : AlertKind.MainsLost);
            }
        }
        else
        {
            _candidateOnMains = null;
        }

        if (p.OnMains || t.BatteryPercent > LowPercent)
        {
            _lowArmed = true;
        }

        if (p.OnMains || t.BatteryPercent > CriticalPercent)
        {
            _criticalArmed = true;
        }

        if (!p.OnMains)
        {
            if (t.BatteryPercent <= CriticalPercent && _criticalArmed)
            {
                _criticalArmed = false;
                _lowArmed = false;
                alerts.Add(AlertKind.BatteryCritical);
            }
            else if (t.BatteryPercent <= LowPercent && _lowArmed)
            {
                _lowArmed = false;
                alerts.Add(AlertKind.BatteryLow);
            }
        }

        return alerts;
    }
}
