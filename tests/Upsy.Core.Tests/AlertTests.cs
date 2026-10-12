using Microsoft.Extensions.Time.Testing;
using Upsy.Core.Alerts;
using static Upsy.Core.Tests.Fixtures;

namespace Upsy.Core.Tests;

public sealed class AlertTests
{
    private readonly FakeTimeProvider _clock = new();
    private readonly AlertMonitor _alerts;

    public AlertTests() => _alerts = new AlertMonitor(_clock);

    private IReadOnlyList<AlertKind> Step(int battery, bool mains, int seconds = 2)
    {
        var result = _alerts.Observe(T(battery, acIn: mains ? 390 : 0, acOut: 130, volts: mains ? 228 : 2.6));
        _clock.Advance(TimeSpan.FromSeconds(seconds));
        return result;
    }

    [Fact]
    public void Low_fires_once_when_crossing_the_threshold()
    {
        Assert.Empty(Step(21, mains: false));
        Assert.Equal([AlertKind.BatteryLow], Step(20, mains: false));
        Assert.Empty(Step(19, mains: false));
        Assert.Empty(Step(18, mains: false));
    }

    [Fact]
    public void Low_rearms_after_rising_above_the_threshold()
    {
        Step(21, false);
        Step(20, false);
        Step(21, true);
        Step(21, false);
        Assert.Equal([AlertKind.BatteryLow], Step(20, false));
    }

    [Fact]
    public void Already_low_at_start_fires_once()
    {
        Assert.Equal([AlertKind.BatteryLow], Step(15, mains: false));
        Assert.Empty(Step(15, mains: false));
    }

    [Fact]
    public void Critical_fires_at_ten_percent()
    {
        Step(12, false);
        Assert.Empty(Step(11, false));
        Assert.Equal([AlertKind.BatteryCritical], Step(10, false));
        Assert.Empty(Step(9, false));
    }

    [Fact]
    public void Custom_threshold_is_used()
    {
        _alerts.LowPercent = 30;
        Step(31, false);
        Assert.Equal([AlertKind.BatteryLow], Step(30, false));
    }

    [Fact]
    public void Mains_loss_is_reported_after_five_seconds()
    {
        Step(90, mains: true);
        Assert.Empty(Step(90, mains: false, seconds: 3));
        Assert.Empty(Step(90, mains: false, seconds: 2));
        Assert.Equal([AlertKind.MainsLost], Step(90, mains: false));
        Assert.Empty(Step(90, mains: false));
    }

    [Fact]
    public void Mains_restore_is_reported_after_five_seconds()
    {
        Step(60, mains: false);
        Step(60, mains: true, seconds: 5);
        Assert.Equal([AlertKind.MainsRestored], Step(60, mains: true));
    }

    [Fact]
    public void Brief_glitch_is_ignored()
    {
        Step(90, mains: true);
        Step(90, mains: false, seconds: 3);
        Assert.Empty(Step(90, mains: true));
        Assert.Empty(Step(90, mains: true, seconds: 10));
    }

    [Fact]
    public void State_found_at_connect_is_not_a_transition()
    {
        Assert.Empty(Step(50, mains: false, seconds: 10));
        Assert.Empty(Step(50, mains: false, seconds: 10));
    }

    [Fact]
    public void Reconnect_does_not_report_a_transition()
    {
        Step(80, mains: true);
        _alerts.ObserveNoData();
        Assert.Empty(Step(80, mains: false, seconds: 10));
        Assert.Empty(Step(80, mains: false));
    }

    [Fact]
    public void Low_alert_is_not_repeated_after_a_reconnect()
    {
        Assert.Equal([AlertKind.BatteryLow], Step(18, false));
        _alerts.ObserveNoData();
        Assert.Empty(Step(18, false));
    }
}
