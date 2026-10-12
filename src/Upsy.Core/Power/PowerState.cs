using Upsy.Core.Station;

namespace Upsy.Core.Power;

/// <summary>What the station is doing, derived from one snapshot (the station reports none of this itself).</summary>
public sealed record PowerReading(bool OnMains, bool Charging, bool LoadPresent, int InputW, int OutputW, int NetBatteryW)
{
    public const double MainsVoltageThreshold = 100.0;
    public const int ChargingThresholdW = 10;
    public const int LoadThresholdW = 20;

    public static PowerReading From(Telemetry t)
    {
        ArgumentNullException.ThrowIfNull(t);
        var input = t.AcInputW + t.DcInputW;
        var output = t.AcOutputW + t.DcOutputW;
        var net = input - output;
        // Voltage, not power: a full station with no load draws 0 W from a live socket.
        var onMains = t.AcInputVolts > MainsVoltageThreshold;
        return new PowerReading(
            OnMains: onMains,
            Charging: net > ChargingThresholdW && t.BatteryPercent < 100,
            LoadPresent: output > LoadThresholdW,
            InputW: input,
            OutputW: output,
            NetBatteryW: net);
    }
}

/// <summary>Smoothed discharge power and remaining runtime.</summary>
public sealed class RuntimeEstimator(TimeProvider clock)
{
    public const double NominalCapacityWh = 268.0;
    public const double UsableFactor = 0.85;
    public static readonly TimeSpan TimeConstant = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan WarmUp = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan Cap = TimeSpan.FromHours(24);

    private double? _average;
    private DateTimeOffset _first;
    private DateTimeOffset _last;

    public double? AverageDischargeW => _average;

    public void Reset() => _average = null;

    public void Add(Telemetry t)
    {
        ArgumentNullException.ThrowIfNull(t);
        var now = clock.GetUtcNow();
        var discharge = Math.Max(0, t.AcOutputW + t.DcOutputW - t.DcInputW);
        if (_average is null)
        {
            _average = discharge;
            _first = now;
        }
        else
        {
            var dt = Math.Max(0, (now - _last).TotalSeconds);
            var alpha = 1 - Math.Exp(-dt / TimeConstant.TotalSeconds);
            _average += alpha * (discharge - _average.Value);
        }

        _last = now;
    }

    /// <summary>Remaining runtime, or null when it should not be shown. Values above the cap return <see cref="Cap"/>.</summary>
    public TimeSpan? Estimate(Telemetry t, PowerReading p)
    {
        ArgumentNullException.ThrowIfNull(t);
        ArgumentNullException.ThrowIfNull(p);
        if (_average is null || p.OnMains || p.Charging || !p.LoadPresent || _last - _first < WarmUp || _average < 1)
        {
            return null;
        }

        var hours = NominalCapacityWh * UsableFactor * t.BatteryPercent / 100.0 / _average.Value;
        var span = TimeSpan.FromHours(hours);
        return span > Cap ? Cap : span;
    }
}
