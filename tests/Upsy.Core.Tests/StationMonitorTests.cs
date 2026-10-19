using Microsoft.Extensions.Time.Testing;
using Upsy.Core.Link;
using Upsy.Core.Station;

namespace Upsy.Core.Tests;

public sealed class StationMonitorTests
{
    private static readonly StationProfile Pushing = StationCatalog.Delta2 with { Key = "test" };
    private readonly FakeTimeProvider _clock = new();

    private static async Task WaitFor(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > until)
            {
                throw new TimeoutException("condition not reached");
            }

            await Task.Delay(10);
        }
    }

    private static StationData Data(int percent) => new(Fixtures.T(percent), null, null);

    [Fact]
    public async Task Connects_and_publishes_decoded_telemetry_identity_and_settings()
    {
        var transport = new FakeTransport(_ => FakeEb3a.Create());
        using var monitor = new StationMonitor(transport, new BluettiDriver(_clock), _clock, FakeEb3a.Address);
        using var cts = new CancellationTokenSource();
        var run = monitor.RunAsync(cts.Token);

        await WaitFor(() => monitor.Current.HasData && monitor.Current.Settings is not null);
        Assert.Equal(98, monitor.Current.Telemetry!.BatteryPercent);
        Assert.Equal("EB3A", monitor.Current.Identity!.Model);
        Assert.Contains(new SettingReadout("Charging mode", "Standard"), monitor.Current.Settings!.Items);
        Assert.Equal([FakeEb3a.Address], transport.Addresses);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task Bluetooth_off_is_reported_and_recovers()
    {
        var transport = new FakeTransport(_ => FakeEb3a.Create()) { Radio = RadioState.Off };
        using var monitor = new StationMonitor(transport, new BluettiDriver(_clock), _clock, FakeEb3a.Address);
        using var cts = new CancellationTokenSource();
        var run = monitor.RunAsync(cts.Token);

        await WaitFor(() => monitor.Current.Status == LinkStatus.BluetoothOff);
        transport.Radio = RadioState.On;
        _clock.Advance(StationMonitor.RadioRecheck);
        await WaitFor(() => monitor.Current.HasData);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task Pause_releases_the_link_and_resumes_by_itself_after_thirty_minutes()
    {
        var transport = new FakeTransport(_ => FakeEb3a.Create());
        using var monitor = new StationMonitor(transport, new BluettiDriver(_clock), _clock, FakeEb3a.Address);
        using var cts = new CancellationTokenSource();
        var run = monitor.RunAsync(cts.Token);
        await WaitFor(() => monitor.Current.HasData);

        monitor.Pause();
        await WaitFor(() => monitor.Current.Status == LinkStatus.Paused);
        await WaitFor(() => transport.OpenChannels == 0);
        Assert.Null(monitor.Current.Telemetry);

        _clock.Advance(StationMonitor.PauseDuration);
        await WaitFor(() => monitor.Current.HasData);
        Assert.Equal(2, transport.Opens);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task Repeated_read_failures_drop_the_link_and_reconnect()
    {
        var fail = false;
        var transport = new FakeTransport(_ => FakeEb3a.Create(() => Volatile.Read(ref fail)));
        using var monitor = new StationMonitor(transport, new BluettiDriver(_clock), _clock, FakeEb3a.Address);
        using var cts = new CancellationTokenSource();
        var run = monitor.RunAsync(cts.Token);
        await WaitFor(() => monitor.Current.HasData);

        Volatile.Write(ref fail, true);
        _clock.Advance(BluettiDriver.PollInterval);
        await WaitFor(() => monitor.Current.Status == LinkStatus.Connecting);
        Volatile.Write(ref fail, false);
        _clock.Advance(StationMonitor.Backoff(1));
        await WaitFor(() => monitor.Current.HasData && transport.Opens == 2);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task A_silent_link_is_dropped_by_the_watchdog()
    {
        var transport = new FakeTransport(a => new FakeChannel(a));
        var runs = 0;
        var driver = new ScriptedDriver(Pushing, async (_, publish, ct) =>
        {
            Interlocked.Increment(ref runs);
            publish(Data(82));
            await Task.Delay(Timeout.InfiniteTimeSpan, ct); // then nothing more
        });
        using var monitor = new StationMonitor(transport, driver, _clock, 0x01);
        using var cts = new CancellationTokenSource();
        var run = monitor.RunAsync(cts.Token);
        await WaitFor(() => monitor.Current.HasData);

        for (var s = 0; s <= Pushing.NoUpdateTimeout.TotalSeconds + 1 && monitor.Current.HasData; s++)
        {
            _clock.Advance(StationMonitor.WatchdogTick);
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        await WaitFor(() => monitor.Current.Status == LinkStatus.Connecting && transport.OpenChannels == 0);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task Reconnect_waits_for_the_profile_minimum_delay()
    {
        var transport = new FakeTransport(a => new FakeChannel(a));
        var driver = new ScriptedDriver(Pushing, (_, publish, _) =>
        {
            publish(Data(82));
            return Task.CompletedTask; // the link ends right away
        });
        using var monitor = new StationMonitor(transport, driver, _clock, 0x01);
        using var cts = new CancellationTokenSource();
        var run = monitor.RunAsync(cts.Token);
        await WaitFor(() => transport.Opens == 1 && monitor.Current.Status == LinkStatus.Connecting);

        _clock.Advance(Pushing.MinReconnectDelay - TimeSpan.FromSeconds(1));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(1, transport.Opens);
        _clock.Advance(TimeSpan.FromSeconds(1));
        await WaitFor(() => transport.Opens == 2);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task Missing_services_are_retried_until_the_station_answers()
    {
        var answer = false;
        var transport = new FakeTransport(a => Volatile.Read(ref answer) ? new FakeChannel(a) : null);
        var driver = new ScriptedDriver(Pushing, async (_, publish, ct) =>
        {
            publish(Data(82));
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        });
        using var monitor = new StationMonitor(transport, driver, _clock, 0x01);
        using var cts = new CancellationTokenSource();
        var run = monitor.RunAsync(cts.Token);

        await WaitFor(() => monitor.Current.Status == LinkStatus.Searching && monitor.Current.Failures == 1);
        _clock.Advance(StationMonitor.Backoff(1, Pushing.MinReconnectDelay));
        await WaitFor(() => monitor.Current.Failures == 2);
        Volatile.Write(ref answer, true);
        _clock.Advance(StationMonitor.Backoff(2, Pushing.MinReconnectDelay));
        await WaitFor(() => monitor.Current.HasData);
        Assert.Equal(0, monitor.Current.Failures);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task An_auth_problem_waits_for_the_user_and_retries_on_request()
    {
        var signedIn = false;
        var transport = new FakeTransport(a => new FakeChannel(a));
        var driver = new ScriptedDriver(Pushing, async (_, publish, ct) =>
        {
            if (!Volatile.Read(ref signedIn))
            {
                throw new StationAuthException(LinkStatus.SignInNeeded, "no user id");
            }

            publish(Data(82));
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        });
        using var monitor = new StationMonitor(transport, driver, _clock, 0x01);
        using var cts = new CancellationTokenSource();
        var run = monitor.RunAsync(cts.Token);

        await WaitFor(() => monitor.Current.Status == LinkStatus.SignInNeeded);
        Assert.True(monitor.Current.NeedsUser);
        _clock.Advance(StationMonitor.MaxBackoff * 4);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(1, transport.Opens);

        Volatile.Write(ref signedIn, true);
        monitor.Retry();
        await WaitFor(() => monitor.Current.HasData);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task A_new_address_is_used_for_the_next_connect()
    {
        var transport = new FakeTransport(a => a == 0x02 ? new FakeChannel(a) : null);
        var driver = new ScriptedDriver(Pushing, async (_, publish, ct) =>
        {
            publish(Data(82));
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        });
        using var monitor = new StationMonitor(transport, driver, _clock, 0x01);
        using var cts = new CancellationTokenSource();
        var run = monitor.RunAsync(cts.Token);
        await WaitFor(() => monitor.Current.Failures == 1);

        monitor.UpdateAddress(0x02);
        await WaitFor(() => monitor.Current.HasData);
        Assert.Equal(0x02UL, monitor.Address);

        await cts.CancelAsync();
        await run;
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(4, 16)]
    [InlineData(5, 30)]
    [InlineData(9, 30)]
    public void Backoff_doubles_up_to_thirty_seconds(int failures, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), StationMonitor.Backoff(failures));

    [Fact]
    public void Backoff_respects_a_minimum() =>
        Assert.Equal(TimeSpan.FromSeconds(5), StationMonitor.Backoff(1, TimeSpan.FromSeconds(5)));
}
