using Upsy.Core.Alerts;
using Upsy.Core.Link;
using Upsy.Core.Presentation;
using Upsy.Core.Station;
using static Upsy.Core.Tests.Fixtures;

namespace Upsy.Core.Tests;

public sealed class StatusTextTests
{
    private static readonly StationIdentity Id = new("EB3A", "2240000000001", "2063.02", "2052.07");
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 14, 32, 0, TimeSpan.Zero);

    private static MonitorSnapshot Connected(Station.Telemetry t) => new(LinkStatus.Connected, t, Id, null, Now, Now, null);

    [Theory]
    [InlineData(62, "about 1 h")]
    [InlineData(21, "about 20 min")]
    [InlineData(2, "about 5 min")]
    [InlineData(98, "about 1 h 40 min")]
    [InlineData(1500, "more than 24 h")]
    public void Runtime_is_rounded(int minutes, string expected) => Assert.Equal(expected, StatusText.Runtime(TimeSpan.FromMinutes(minutes)));

    [Fact]
    public void Tooltip_on_battery()
    {
        var v = StatusText.Build(Connected(T(58, acOut: 128)), TimeSpan.FromMinutes(62), 20);
        Assert.Equal("EB3A · 58% · 128 W out · about 1 h left", v.Tooltip);
        Assert.Equal("About 1 h left", v.Headline);
        Assert.Equal("EB3A · on battery", v.Source);
    }

    [Fact]
    public void Tooltip_charging()
    {
        var v = StatusText.Build(Connected(T(54, acIn: 390, acOut: 126, volts: 228.4)), null, 20);
        Assert.Equal("EB3A · 54% · charging +264 W", v.Tooltip);
        Assert.Equal(Tone.Ok, v.Tone);
        Assert.Equal("Mains 228 V", v.Detail);
    }

    [Fact]
    public void Tooltip_full_on_mains()
    {
        var v = StatusText.Build(Connected(T(100, acIn: 126, acOut: 126, volts: 228)), null, 20);
        Assert.Equal("EB3A · 100% · on mains", v.Tooltip);
        Assert.Equal("Full", v.Headline);
    }

    [Fact]
    public void Low_battery_is_caution()
    {
        var v = StatusText.Build(Connected(T(18, acOut: 131)), TimeSpan.FromMinutes(21), 20);
        Assert.Equal(Tone.Caution, v.Tone);
        Assert.Equal("Low · about 20 min left", v.Headline);
    }

    [Theory]
    [InlineData(LinkStatus.BluetoothOff, "upsy · Bluetooth is off", NoticeAction.BluetoothSettings)]
    [InlineData(LinkStatus.Searching, "upsy · looking for the station", NoticeAction.None)]
    public void No_data_states_name_the_reason(LinkStatus status, string tooltip, NoticeAction action)
    {
        var v = StatusText.Build(new MonitorSnapshot(status, null, Id, null, null, Now, null), null, 20);
        Assert.Equal(tooltip, v.Tooltip);
        Assert.Null(v.Percent);
        Assert.Equal(action, v.NoticeAction);
    }

    [Fact]
    public void Paused_offers_resume()
    {
        var v = StatusText.Build(new MonitorSnapshot(LinkStatus.Paused, null, Id, null, null, Now, Now.AddMinutes(30)), null, 20);
        Assert.Equal(NoticeAction.Resume, v.NoticeAction);
        Assert.StartsWith("upsy · paused until", v.Tooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void Tooltip_never_exceeds_the_limit()
    {
        var longId = Id with { Model = new string('X', 200) };
        var v = StatusText.Build(new MonitorSnapshot(LinkStatus.Connected, T(58, acOut: 128), longId, null, Now, Now, null), TimeSpan.FromMinutes(62), 20);
        Assert.True(v.Tooltip.Length <= StatusText.TooltipLimit);
    }

    [Fact]
    public void Alert_texts()
    {
        Assert.Equal(("Mains power lost", "EB3A is on battery at 92%, about 1 h 40 min at 128 W."),
            StatusText.Alert(AlertKind.MainsLost, T(92, acOut: 128), TimeSpan.FromMinutes(98), "EB3A"));
        Assert.Equal(("Battery low: 20%", "About 20 min left at 131 W. Save your work."),
            StatusText.Alert(AlertKind.BatteryLow, T(20, acOut: 131), TimeSpan.FromMinutes(21), "EB3A"));
        Assert.Equal(("Battery critical: 10%", "About 10 min left at 131 W. Shut down or hibernate now."),
            StatusText.Alert(AlertKind.BatteryCritical, T(10, acOut: 131), TimeSpan.FromMinutes(10), "EB3A"));
        Assert.Equal(("Mains power is back", "EB3A is charging at 61%."),
            StatusText.Alert(AlertKind.MainsRestored, T(61, acIn: 400, acOut: 130, volts: 228), null, "EB3A"));
    }
}
