using Upsy.Core.Alerts;
using Upsy.Core.Link;
using Upsy.Core.Power;
using Upsy.Core.Presentation;
using Upsy.Core.Station;
using static Upsy.Core.Tests.Fixtures;

namespace Upsy.Core.Tests;

public sealed class StatusTextTests
{
    private static readonly StationIdentity Id = new("EB3A", "2240000000001", [new("ARM", "2063.02"), new("DSP", "2052.07")]);
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 14, 32, 0, TimeSpan.Zero);

    private static TimeEstimate Est(int minutes) => new(TimeSpan.FromMinutes(minutes), FromStation: false);

    private static TimeEstimate Station(int minutes) => new(TimeSpan.FromMinutes(minutes), FromStation: true);

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
        var v = StatusText.Build(Connected(T(58, acOut: 128)), Est(62), null, 20);
        Assert.Equal("EB3A · 58% · 128 W out · about 1 h left (estimated)", v.Tooltip);
        Assert.Equal("Estimated at the current 128 W load", v.Detail);
        Assert.Equal("About 1 h left", v.Headline);
        Assert.Equal("EB3A · on battery", v.Source);
    }

    [Fact]
    public void Tooltip_charging()
    {
        var v = StatusText.Build(Connected(T(54, acIn: 390, acOut: 126, volts: 228.4)), null, null, 20);
        Assert.Equal("EB3A · 54% · charging +264 W", v.Tooltip);
        Assert.Equal(Tone.Ok, v.Tone);
        Assert.Equal("Mains 228 V", v.Detail);
    }

    [Fact]
    public void Tooltip_full_on_mains()
    {
        var v = StatusText.Build(Connected(T(100, acIn: 126, acOut: 126, volts: 228)), null, null, 20);
        Assert.Equal("EB3A · 100% · on mains", v.Tooltip);
        Assert.Equal("Full", v.Headline);
    }

    [Fact]
    public void Low_battery_is_caution()
    {
        var v = StatusText.Build(Connected(T(18, acOut: 131)), Est(21), null, 20);
        Assert.Equal(Tone.Caution, v.Tone);
        Assert.Equal("Low · about 20 min left", v.Headline);
    }

    [Theory]
    [InlineData(LinkStatus.BluetoothOff, "upsy · Bluetooth is off", NoticeAction.BluetoothSettings)]
    [InlineData(LinkStatus.Searching, "EB3A · looking for the station", NoticeAction.None)]
    public void No_data_states_name_the_reason(LinkStatus status, string tooltip, NoticeAction action)
    {
        var v = StatusText.Build(new MonitorSnapshot(status, null, Id, null, null, Now, null), null, null, 20);
        Assert.Equal(tooltip, v.Tooltip);
        Assert.Null(v.Percent);
        Assert.Equal(action, v.NoticeAction);
    }

    [Fact]
    public void Paused_offers_resume()
    {
        var v = StatusText.Build(new MonitorSnapshot(LinkStatus.Paused, null, Id, null, null, Now, Now.AddMinutes(30)), null, null, 20);
        Assert.Equal(NoticeAction.Resume, v.NoticeAction);
        Assert.StartsWith("EB3A · paused until", v.Tooltip, StringComparison.Ordinal);
        Assert.Contains("Bluetti phone app", v.NoticeBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Tooltip_never_exceeds_the_limit()
    {
        var longName = StationContext.Eb3a with { Name = new string('X', 200) };
        var v = StatusText.Build(Connected(T(58, acOut: 128)), Est(62), null, 20, longName);
        Assert.True(v.Tooltip.Length <= StatusText.TooltipLimit);
    }

    [Fact]
    public void Alert_texts()
    {
        Assert.Equal(("EB3A: mains power lost", "On battery at 92%, about 1 h 40 min at 128 W (estimated)."),
            StatusText.Alert(AlertKind.MainsLost, T(92, acOut: 128), Est(98), null, "EB3A"));
        Assert.Equal(("EB3A: battery low, 20%", "About 20 min left at 131 W (estimated). Save your work."),
            StatusText.Alert(AlertKind.BatteryLow, T(20, acOut: 131), Est(21), null, "EB3A"));
        Assert.Equal(("DELTA 2: battery critical, 10%", "48 min left at 103 W. Shut down or hibernate now."),
            StatusText.Alert(AlertKind.BatteryCritical, T(10, acOut: 103), Station(48), null, "DELTA 2"));
        Assert.Equal(("DELTA 2: mains power is back", "Charging at 61%, full in 1 h 28 min."),
            StatusText.Alert(AlertKind.MainsRestored, T(61, acIn: 400, acOut: 130, volts: 228), null, Station(88), "DELTA 2"));
        Assert.Equal(("EB3A: mains power is back", "Charging at 61%, full in about 50 min (estimated)."),
            StatusText.Alert(AlertKind.MainsRestored, T(61, acIn: 400, acOut: 130, volts: 228), null, Est(52), "EB3A"));
    }

    [Fact]
    public void Charging_with_a_station_reported_time_to_full()
    {
        // DELTA 2 capture: 80%, 328 W from mains at 233.3 V, 101 W out, 73 min to full.
        var t = Fixtures.T(80, acIn: 328, acOut: 101, volts: 233.3);
        var view = StatusText.Build(Connected(t), null, Station(73), 20);
        Assert.Equal("Charging · +227 W", view.Headline);
        Assert.Equal("Full in 1 h 13 min · mains 233 V", view.Detail);
    }

    private static readonly StationContext Delta2At84 = new("DELTA 2", StationCatalog.Delta2, 84);

    [Theory]
    [InlineData(LinkStatus.SignInNeeded, "Sign-in needed", NoticeAction.SignIn)]
    [InlineData(LinkStatus.KeyRejected, "Account not accepted", NoticeAction.TryAgain)]
    [InlineData(LinkStatus.KeyTableMissing, "EcoFlow not set up", NoticeAction.SetupGuide)]
    public void Battery_level_only_names_the_reason_and_the_fix(LinkStatus status, string headline, NoticeAction action)
    {
        var v = StatusText.Build(new MonitorSnapshot(status, null, null, null, null, null, null), null, null, 20, Delta2At84);
        Assert.True(v.LevelOnly);
        Assert.Equal(84, v.Percent);
        Assert.Equal(headline, v.Headline);
        Assert.Equal(action, v.NoticeAction);
        Assert.Equal("DELTA 2 · 84% · battery level only", v.Tooltip);
        Assert.Null(v.Power);
    }

    [Fact]
    public void A_station_that_advertises_but_refuses_the_link_is_probably_held_by_its_app()
    {
        var busy = new MonitorSnapshot(LinkStatus.Searching, null, null, null, null, null, null, Failures: StatusText.BusyAfterFailures);
        var v = StatusText.Build(busy, null, null, 20, Delta2At84);
        Assert.Equal("In use by another app", v.Headline);
        Assert.Contains("EcoFlow app", v.NoticeBody, StringComparison.Ordinal);

        var early = StatusText.Build(busy with { Failures = 1 }, null, null, 20, Delta2At84);
        Assert.Equal("Looking for the station…", early.Headline);
    }

    [Fact]
    public void Tooltip_with_two_stations_leads_with_the_tray_station()
    {
        var eb3a = StatusText.Build(Connected(T(100, acIn: 46, acOut: 46, volts: 228)), null, null, 20);
        var delta2 = StatusText.Build(Connected(T(82, acOut: 103)) with { Identity = null }, null, null, 20, new("DELTA 2", StationCatalog.Delta2));
        Assert.Equal("EB3A · 100% · on mains\nDELTA 2 · 82% · 103 W out", StatusText.Tooltip(eb3a, [delta2]));
    }

    [Fact]
    public void Other_stations_are_dropped_first_at_the_tooltip_limit()
    {
        var tray = StatusText.Build(Connected(T(58, acOut: 128)), Est(62), null, 20, StationContext.Eb3a with { Name = new string('E', 90) });
        var other = StatusText.Build(Connected(T(82, acOut: 103)), null, null, 20, new("DELTA 2", StationCatalog.Delta2));
        var text = StatusText.Tooltip(tray, [other]);
        Assert.Equal(tray.Tooltip, text);
        Assert.True(text.Length <= StatusText.TooltipLimit);
    }

    [Fact]
    public void First_start_lists_the_supported_stations()
    {
        var v = StatusText.FirstStart();
        Assert.Equal("Looking for a station…", v.Headline);
        Assert.Contains("EcoFlow DELTA 2", v.NoticeBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Eb3a_charging_shows_an_estimated_time_to_full()
    {
        var v = StatusText.Build(Connected(T(54, acIn: 390, acOut: 126, volts: 228.4)), null, Est(37), 20);
        Assert.Equal("Full in about 35 min, estimated · mains 228 V", v.Detail);
    }

    [Fact]
    public void Station_runtime_is_exact_and_says_so()
    {
        var v = StatusText.Build(Connected(T(82, acOut: 103, volts: 0)), Station(431), null, 20, new("DELTA 2", StationCatalog.Delta2));
        Assert.Equal("7 h 11 min left", v.Headline);
        Assert.Equal("At the current 103 W load · from the station", v.Detail);
        Assert.Equal("DELTA 2 · 82% · 103 W out · 7 h 11 min left", v.Tooltip);
    }

    [Theory]
    [InlineData(73, true, "1 h 13 min (station)")]
    [InlineData(45, true, "45 min (station)")]
    [InlineData(60, true, "1 h (station)")]
    [InlineData(37, false, "About 35 min (estimated)")]
    [InlineData(1500, true, "More than 24 h (station)")]
    public void Details_values_name_the_source(int minutes, bool fromStation, string expected) =>
        Assert.Equal(expected, StatusText.DetailsValue(new TimeEstimate(TimeSpan.FromMinutes(minutes), fromStation)));
}
