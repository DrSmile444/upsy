using Upsy.Core.Station;

namespace Upsy.Core.Power;

public enum EnergyDirection
{
    Charge,
    Discharge,
}

/// <summary>One minute of a charge or discharge run, for calibrating the capacity factors (tools/energy_calibration.py).</summary>
/// <param name="AverageInW">Average input (AC + DC) over the minute.</param>
/// <param name="AverageOutW">Average output (AC + DC) over the minute.</param>
/// <param name="EnergyWh">Energy into the battery (charge: input − output) or out of it (discharge: output − DC input) since the run started.</param>
public sealed record EnergySummary(EnergyDirection Direction, int Percent, int AverageInW, int AverageOutW, double EnergyWh, TimeSpan Elapsed)
{
    public int AverageNetW => AverageInW - AverageOutW;
}

/// <summary>
/// Integrates every sample of a charge or a discharge into a running energy total and summarises it once a minute.
/// A run ends after a minute idle or when the direction changes; gaps over 10 s are not integrated.
/// </summary>
public sealed class EnergyLog(TimeProvider clock)
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan MaxGap = TimeSpan.FromSeconds(10);

    private EnergyDirection? _direction;
    private DateTimeOffset _start;
    private DateTimeOffset _last;
    private DateTimeOffset _lastActive;
    private double _energyWh;
    private DateTimeOffset _minuteStart;
    private double _minuteSeconds;
    private double _minuteInWs;
    private double _minuteOutWs;

    /// <summary>Adds one sample; returns a summary when a minute of the current run is complete.</summary>
    public EnergySummary? Add(Telemetry t)
    {
        ArgumentNullException.ThrowIfNull(t);
        var now = clock.GetUtcNow();
        var p = PowerReading.From(t);
        EnergyDirection? direction = p.Charging ? EnergyDirection.Charge
            : !p.OnMains && p.LoadPresent ? EnergyDirection.Discharge
            : null;

        if (direction is null)
        {
            if (_direction is not null && now - _lastActive > Interval)
            {
                _direction = null;
            }

            _last = now;
            return null;
        }

        if (_direction != direction || now - _lastActive > Interval)
        {
            _direction = direction;
            _start = now;
            _energyWh = 0;
            StartMinute(now);
        }
        else
        {
            var dt = (now - _last).TotalSeconds;
            if (dt > 0 && dt <= MaxGap.TotalSeconds)
            {
                var input = t.AcInputW + t.DcInputW;
                var output = t.AcOutputW + t.DcOutputW;
                var power = direction == EnergyDirection.Charge ? Math.Max(0, input - output) : Math.Max(0, output - t.DcInputW);
                _energyWh += power * dt / 3600;
                _minuteSeconds += dt;
                _minuteInWs += input * dt;
                _minuteOutWs += output * dt;
            }
        }

        _last = now;
        _lastActive = now;
        if (now - _minuteStart < Interval || _minuteSeconds <= 0)
        {
            return null;
        }

        var summary = new EnergySummary(
            direction.Value,
            t.BatteryPercent,
            (int)Math.Round(_minuteInWs / _minuteSeconds),
            (int)Math.Round(_minuteOutWs / _minuteSeconds),
            _energyWh,
            now - _start);
        StartMinute(now);
        return summary;
    }

    private void StartMinute(DateTimeOffset now)
    {
        _minuteStart = now;
        _minuteSeconds = 0;
        _minuteInWs = 0;
        _minuteOutWs = 0;
    }
}
