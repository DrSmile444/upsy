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

/// <summary>A time to empty or to full, and whether the station reported it or upsy estimated it (the wording differs).</summary>
public sealed record TimeEstimate(TimeSpan Value, bool FromStation);

/// <summary>
/// Remaining runtime and time to full for one station. A time the station reports wins; otherwise upsy estimates
/// from the model's capacity and factors and a smoothed power: discharge over about a minute, net charge over a
/// few minutes so a load that cycles (a boiler compressor) does not jump the number.
/// </summary>
public sealed class RuntimeEstimator(TimeProvider clock, StationProfile profile)
{
    public static readonly TimeSpan TimeConstant = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan WarmUp = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan ChargeTimeConstant = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan ChargeWarmUp = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan Cap = TimeSpan.FromHours(24);

    private readonly Smoothed _discharge = new(TimeConstant);
    private readonly Smoothed _charge = new(ChargeTimeConstant);

    public double? AverageDischargeW => _discharge.Value;

    public double? AverageChargeW => _charge.Value;

    public void Reset()
    {
        _discharge.Reset();
        _charge.Reset();
    }

    public void Add(Telemetry t)
    {
        ArgumentNullException.ThrowIfNull(t);
        var now = clock.GetUtcNow();
        _discharge.Add(Math.Max(0, t.AcOutputW + t.DcOutputW - t.DcInputW), now);
        if (PowerReading.From(t).Charging)
        {
            _charge.Add(Math.Max(0, t.AcInputW + t.DcInputW - t.AcOutputW - t.DcOutputW), now);
        }
        else
        {
            // A pause (thermal, full, unplugged) ends the charge average; the warm-up starts again with the next charge.
            _charge.Reset();
        }
    }

    /// <summary>Remaining runtime on battery with a load, or null when it should not be shown.</summary>
    public TimeEstimate? EstimateToEmpty(Telemetry t, PowerReading p)
    {
        ArgumentNullException.ThrowIfNull(t);
        ArgumentNullException.ThrowIfNull(p);
        if (p.OnMains || p.Charging || !p.LoadPresent)
        {
            return null;
        }

        if (t.ToEmpty is { } reported)
        {
            return new(Capped(reported), FromStation: true);
        }

        if (!_discharge.Ready(WarmUp) || _discharge.Value < 1)
        {
            return null;
        }

        var hours = profile.NominalCapacityWh * profile.UsableFactor * t.BatteryPercent / 100.0 / _discharge.Value!.Value;
        return new(Capped(TimeSpan.FromHours(hours)), FromStation: false);
    }

    /// <summary>Time to full while charging, or null when it should not be shown.</summary>
    public TimeEstimate? EstimateToFull(Telemetry t, PowerReading p)
    {
        ArgumentNullException.ThrowIfNull(t);
        ArgumentNullException.ThrowIfNull(p);
        if (!p.Charging)
        {
            return null;
        }

        if (t.ToFull is { } reported)
        {
            return new(Capped(reported), FromStation: true);
        }

        if (!_charge.Ready(ChargeWarmUp) || _charge.Value < 1)
        {
            return null;
        }

        var missingWh = profile.NominalCapacityWh * (100 - t.BatteryPercent) / 100.0;
        var hours = missingWh / (_charge.Value!.Value * profile.ChargeEfficiency);
        return new(Capped(TimeSpan.FromHours(hours)), FromStation: false);
    }

    private static TimeSpan Capped(TimeSpan span) => span > Cap ? Cap : span;

    /// <summary>Exponential moving average over time, with the span it has been fed for.</summary>
    private sealed class Smoothed(TimeSpan timeConstant)
    {
        private DateTimeOffset _first;
        private DateTimeOffset _last;

        public double? Value { get; private set; }

        public void Reset() => Value = null;

        public void Add(double sample, DateTimeOffset now)
        {
            if (Value is null)
            {
                Value = sample;
                _first = now;
            }
            else
            {
                var dt = Math.Max(0, (now - _last).TotalSeconds);
                Value += (1 - Math.Exp(-dt / timeConstant.TotalSeconds)) * (sample - Value.Value);
            }

            _last = now;
        }

        public bool Ready(TimeSpan warmUp) => Value is not null && _last - _first >= warmUp;
    }
}
