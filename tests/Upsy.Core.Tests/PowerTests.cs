using Microsoft.Extensions.Time.Testing;
using Upsy.Core.Power;
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
        var est = new RuntimeEstimator(_clock);
        var t = T(56, acOut: 128);
        est.Add(t);
        _clock.Advance(TimeSpan.FromSeconds(4));
        est.Add(t);
        Assert.Null(est.Estimate(t, PowerReading.From(t)));
    }

    [Fact]
    public void Fluctuating_load_gives_a_smooth_estimate()
    {
        var est = new RuntimeEstimator(_clock);
        int[] loads = [156, 135, 129, 149, 120, 119, 126, 138, 126, 136, 118, 120, 144, 128];
        var estimates = new List<double>();
        foreach (var w in loads)
        {
            var t = T(56, acOut: w);
            est.Add(t);
            if (est.Estimate(t, PowerReading.From(t)) is { } r)
            {
                estimates.Add(r.TotalMinutes);
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
        var est = new RuntimeEstimator(_clock);
        for (var i = 0; i < 10; i++)
        {
            est.Add(T(56, acOut: 128));
            _clock.Advance(TimeSpan.FromSeconds(2));
        }

        var mains = T(56, acIn: 390, acOut: 128, volts: 228);
        var idle = T(56, acOut: 5);
        Assert.Null(est.Estimate(mains, PowerReading.From(mains)));
        Assert.Null(est.Estimate(idle, PowerReading.From(idle)));
    }

    [Fact]
    public void Reset_requires_a_new_warm_up()
    {
        var est = new RuntimeEstimator(_clock);
        var t = T(56, acOut: 128);
        for (var i = 0; i < 10; i++)
        {
            est.Add(t);
            _clock.Advance(TimeSpan.FromSeconds(2));
        }

        Assert.NotNull(est.Estimate(t, PowerReading.From(t)));
        est.Reset();
        est.Add(t);
        Assert.Null(est.Estimate(t, PowerReading.From(t)));
    }

    [Fact]
    public void Tiny_load_is_capped_at_a_day()
    {
        var est = new RuntimeEstimator(_clock);
        var t = T(100, acOut: 21);
        for (var i = 0; i < 10; i++)
        {
            est.Add(t);
            _clock.Advance(TimeSpan.FromSeconds(2));
        }

        // 268 x 0.85 / 21 W is about 10.8 h, below the cap; a DC trickle is not.
        Assert.True(est.Estimate(t, PowerReading.From(t)) < RuntimeEstimator.Cap);
    }
}
