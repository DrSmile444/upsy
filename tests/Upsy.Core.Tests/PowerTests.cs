using Microsoft.Extensions.Time.Testing;
using Upsy.Core.Power;
using Upsy.Core.Station;
using static Upsy.Core.Tests.Fixtures;

namespace Upsy.Core.Tests;

public sealed class PowerReadingTests
{
    [Fact]
    public void Unplugged_is_on_battery() => Assert.False(PowerReading.From(T(56, acIn: 0, acOut: 156, volts: 2.6)).OnMains);

    [Fact]
    public void Plugged_in_is_on_mains() => Assert.True(PowerReading.From(T(54, acIn: 390, acOut: 126, volts: 228.4)).OnMains);

    [Fact]
    public void Full_and_idle_on_a_live_socket_is_still_on_mains() =>
        Assert.True(PowerReading.From(T(100, acIn: 0, acOut: 0, volts: 228)).OnMains);

    [Fact]
    public void Charging_while_powering_a_load()
    {
        var p = PowerReading.From(T(54, acIn: 390, acOut: 126, volts: 228.4));
        Assert.True(p.Charging);
        Assert.Equal(264, p.NetBatteryW);
    }

    [Fact]
    public void Full_on_mains_is_not_charging() => Assert.False(PowerReading.From(T(100, acIn: 140, acOut: 126, volts: 228)).Charging);

    [Fact]
    public void Solar_on_battery_counts_as_charging()
    {
        var p = PowerReading.From(T(60, acOut: 30, volts: 2.6, dcIn: 80));
        Assert.False(p.OnMains);
        Assert.True(p.Charging);
        Assert.Equal(50, p.NetBatteryW);
    }

    [Theory]
    [InlineData(130, true)]
    [InlineData(5, false)]
    public void Load_presence(int acOut, bool expected) => Assert.Equal(expected, PowerReading.From(T(60, acOut: acOut)).LoadPresent);
}

public sealed class RuntimeEstimatorTests
{
    private readonly FakeTimeProvider _clock = new();

    [Fact]
    public void No_runtime_before_ten_seconds_of_samples()
    {
        var est = new RuntimeEstimator(_clock, StationCatalog.Eb3a);
        var t = T(56, acOut: 128);
        est.Add(t);
        _clock.Advance(TimeSpan.FromSeconds(4));
        est.Add(t);
        Assert.Null(est.EstimateToEmpty(t, PowerReading.From(t)));
    }

    [Fact]
    public void Fluctuating_load_gives_a_smooth_estimate()
    {
        var est = new RuntimeEstimator(_clock, StationCatalog.Eb3a);
        int[] loads = [156, 135, 129, 149, 120, 119, 126, 138, 126, 136, 118, 120, 144, 128];
        var estimates = new List<double>();
        foreach (var w in loads)
        {
            var t = T(56, acOut: w);
            est.Add(t);
            if (est.EstimateToEmpty(t, PowerReading.From(t)) is { } r)
            {
                estimates.Add(r.Value.TotalMinutes);
            }

            _clock.Advance(TimeSpan.FromSeconds(2));
        }

        Assert.NotEmpty(estimates);
        for (var i = 1; i < estimates.Count; i++)
        {
            Assert.InRange(Math.Abs(estimates[i] - estimates[i - 1]), 0, 3);
        }

        // 268 Wh x 0.85 x 56% / ~140 W is about 55 minutes.
        Assert.InRange(estimates[^1], 50, 62);
    }

    [Fact]
    public void No_runtime_on_mains_or_without_load()
    {
        var est = new RuntimeEstimator(_clock, StationCatalog.Eb3a);
        for (var i = 0; i < 10; i++)
        {
            est.Add(T(56, acOut: 128));
            _clock.Advance(TimeSpan.FromSeconds(2));
        }

        var mains = T(56, acIn: 390, acOut: 128, volts: 228);
        var idle = T(56, acOut: 5);
        Assert.Null(est.EstimateToEmpty(mains, PowerReading.From(mains)));
        Assert.Null(est.EstimateToEmpty(idle, PowerReading.From(idle)));
    }

    [Fact]
    public void Reset_requires_a_new_warm_up()
    {
        var est = new RuntimeEstimator(_clock, StationCatalog.Eb3a);
        var t = T(56, acOut: 128);
        for (var i = 0; i < 10; i++)
        {
            est.Add(t);
            _clock.Advance(TimeSpan.FromSeconds(2));
        }

        Assert.NotNull(est.EstimateToEmpty(t, PowerReading.From(t)));
        est.Reset();
        est.Add(t);
        Assert.Null(est.EstimateToEmpty(t, PowerReading.From(t)));
    }

    [Fact]
    public void Tiny_load_is_capped_at_a_day()
    {
        var est = new RuntimeEstimator(_clock, StationCatalog.Eb3a);
        var t = T(100, acOut: 21);
        for (var i = 0; i < 10; i++)
        {
            est.Add(t);
            _clock.Advance(TimeSpan.FromSeconds(2));
        }

        // 268 x 0.85 / 21 W is about 10.8 h, below the cap; a DC trickle is not.
        Assert.True(est.EstimateToEmpty(t, PowerReading.From(t))!.Value < RuntimeEstimator.Cap);
    }

    [Fact]
    public void Station_reported_runtime_wins_and_reads_like_the_screen()
    {
        // DELTA 2 capture: on battery at 82%, 103 W on AC, station reports 431 min to empty.
        var t = T(82, acOut: 103, volts: 0) with { ToEmpty = TimeSpan.FromMinutes(431) };
        var est = new RuntimeEstimator(_clock, StationCatalog.Delta2);
        var runtime = est.EstimateToEmpty(t, PowerReading.From(t));
        Assert.Equal(new TimeEstimate(TimeSpan.FromMinutes(431), FromStation: true), runtime);
        Assert.Equal("7 h 11 min", Presentation.StatusText.Duration(runtime!));
    }

    [Fact]
    public void Station_reported_runtime_is_not_shown_on_mains()
    {
        var t = T(80, acIn: 328, acOut: 101, volts: 233.3) with { ToEmpty = TimeSpan.FromMinutes(431) };
        Assert.Null(new RuntimeEstimator(_clock, StationCatalog.Delta2).EstimateToEmpty(t, PowerReading.From(t)));
    }

    [Fact]
    public void Capacity_comes_from_the_model()
    {
        var eb3a = new RuntimeEstimator(_clock, StationCatalog.Eb3a);
        var delta2 = new RuntimeEstimator(_clock, StationCatalog.Delta2);
        var t = T(50, acOut: 100);
        for (var i = 0; i <= 12; i++)
        {
            eb3a.Add(t);
            delta2.Add(t);
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        var p = PowerReading.From(t);
        Assert.Equal(1024 / 268.0, delta2.EstimateToEmpty(t, p)!.Value / eb3a.EstimateToEmpty(t, p)!.Value * (0.85 / 1.0), 3);
    }

    [Fact]
    public void Profiles_carry_the_provisional_factors()
    {
        Assert.Equal((0.85, 0.93), (StationCatalog.Eb3a.UsableFactor, StationCatalog.Eb3a.ChargeEfficiency));
        Assert.Equal((1.0, 1.0), (StationCatalog.Delta2.UsableFactor, StationCatalog.Delta2.ChargeEfficiency));
    }

    private void Feed(RuntimeEstimator est, Func<int, Station.Telemetry> sample, int seconds)
    {
        for (var s = 0; s <= seconds; s += 2)
        {
            est.Add(sample(s));
            _clock.Advance(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public void Eb3a_time_to_full_from_capacity_percent_and_net_charge()
    {
        var est = new RuntimeEstimator(_clock, StationCatalog.Eb3a);
        var t = T(54, acIn: 390, acOut: 126, volts: 228);
        Feed(est, _ => t, 62);
        var full = est.EstimateToFull(t, PowerReading.From(t))!;
        Assert.False(full.FromStation);
        // 268 Wh x 46% / (264 W x 0.93) = 0.5021 h
        Assert.Equal(268 * 0.46 / (264 * 0.93), full.Value.TotalHours, 3);
    }

    [Fact]
    public void No_time_to_full_in_the_first_minute()
    {
        var est = new RuntimeEstimator(_clock, StationCatalog.Eb3a);
        var t = T(54, acIn: 390, acOut: 126, volts: 228);
        Feed(est, _ => t, 50);
        Assert.Null(est.EstimateToFull(t, PowerReading.From(t)));
    }

    [Fact]
    public void A_charging_pause_shows_no_time_and_restarts_the_warm_up()
    {
        var est = new RuntimeEstimator(_clock, StationCatalog.Eb3a);
        var charging = T(90, acIn: 390, acOut: 126, volts: 228);
        var paused = T(92, acIn: 48, acOut: 48, volts: 228);
        Feed(est, _ => charging, 70);
        Feed(est, _ => paused, 10);
        Assert.Null(est.EstimateToFull(paused, PowerReading.From(paused)));
        Feed(est, _ => charging, 20);
        Assert.Null(est.EstimateToFull(charging, PowerReading.From(charging)));
    }

    [Fact]
    public void A_cycling_load_moves_the_estimate_less_than_a_direct_recompute()
    {
        var est = new RuntimeEstimator(_clock, StationCatalog.Eb3a);
        // The boiler compressor switches the load between 40 W and 130 W every 3 minutes; input does not follow.
        Station.Telemetry At(int s) => T(50, acIn: 330, acOut: (s / 180) % 2 == 0 ? 40 : 130, volts: 228);
        // Past the start-up transient (10 min), over three full cycles.
        Feed(est, At, 600);
        var estimates = new List<double>();
        var direct = new List<double>();
        for (var s = 602; s < 1800; s += 2)
        {
            var t = At(s);
            est.Add(t);
            if (est.EstimateToFull(t, PowerReading.From(t)) is { } full)
            {
                estimates.Add(full.Value.TotalMinutes);
                direct.Add(268 * 0.5 / ((t.AcInputW - t.AcOutputW) * 0.93) * 60);
            }

            _clock.Advance(TimeSpan.FromSeconds(2));
        }

        static double Spread(List<double> v) => v.Max() - v.Min();
        Assert.True(Spread(estimates) < Spread(direct) / 2, $"smoothed {Spread(estimates):0.0} min vs direct {Spread(direct):0.0} min");
    }

    [Fact]
    public void A_reported_time_to_full_is_the_stations()
    {
        var t = T(80, acIn: 328, acOut: 101, volts: 233.3) with { ToFull = TimeSpan.FromMinutes(73) };
        var full = new RuntimeEstimator(_clock, StationCatalog.Delta2).EstimateToFull(t, PowerReading.From(t));
        Assert.Equal(new TimeEstimate(TimeSpan.FromMinutes(73), FromStation: true), full);
    }
}
