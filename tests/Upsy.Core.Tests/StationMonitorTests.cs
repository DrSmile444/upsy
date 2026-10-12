using Microsoft.Extensions.Time.Testing;
using Upsy.Core.Link;
using Upsy.Core.Station;

namespace Upsy.Core.Tests;

public sealed class StationMonitorTests
{
    private readonly FakeTimeProvider _clock = new();
    private readonly FakeTransport _transport = new();

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

    [Fact]
    public async Task Connects_and_publishes_decoded_telemetry_identity_and_settings()
    {
        using var monitor = new StationMonitor(_transport, _clock);
        ulong? learned = null;
        monitor.AddressLearned += (_, a) => learned = a;
        using var cts = new CancellationTokenSource();
        var run = monitor.RunAsync(cts.Token);

        await WaitFor(() => monitor.Current.HasData);
        Assert.Equal(98, monitor.Current.Telemetry!.BatteryPercent);
        Assert.Equal("EB3A", monitor.Current.Identity!.Model);
        Assert.Equal(ChargingMode.Standard, monitor.Current.Settings!.Charging);
        Assert.Equal(FakeTransport.Address, learned);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task Bluetooth_off_is_reported_and_recovers()
    {
        _transport.Radio = RadioState.Off;
        using var monitor = new StationMonitor(_transport, _clock);
        using var cts = new CancellationTokenSource();
        var run = monitor.RunAsync(cts.Token);

        await WaitFor(() => monitor.Current.Status == LinkStatus.BluetoothOff);
        _transport.Radio = RadioState.On;
        _clock.Advance(StationMonitor.RadioRecheck);
        await WaitFor(() => monitor.Current.HasData);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task Pause_releases_the_link_and_resumes_by_itself_after_thirty_minutes()
    {
        using var monitor = new StationMonitor(_transport, _clock);
        using var cts = new CancellationTokenSource();
        var run = monitor.RunAsync(cts.Token);
        await WaitFor(() => monitor.Current.HasData);

        monitor.Pause();
        await WaitFor(() => monitor.Current.Status == LinkStatus.Paused);
        await WaitFor(() => _transport.OpenSessions == 0);
        Assert.Null(monitor.Current.Telemetry);

        _clock.Advance(StationMonitor.PauseDuration);
        await WaitFor(() => monitor.Current.HasData);
        Assert.Equal(2, _transport.Connects);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task Repeated_read_failures_drop_the_session_and_reconnect()
    {
        using var monitor = new StationMonitor(_transport, _clock);
        using var cts = new CancellationTokenSource();
        var run = monitor.RunAsync(cts.Token);
        await WaitFor(() => monitor.Current.HasData);

        _transport.FailReads = true;
        _clock.Advance(StationMonitor.PollInterval);
        await WaitFor(() => monitor.Current.Status == LinkStatus.Connecting);
        _transport.FailReads = false;
        _clock.Advance(StationMonitor.Backoff(1));
        await WaitFor(() => monitor.Current.HasData && _transport.Connects == 2);

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

    private sealed class FakeTransport : IStationTransport
    {
        public const ulong Address = 0xA1B2C3D4E5F6;

        public volatile RadioState Radio = RadioState.On;
        public volatile bool FailReads;
        private int _connects;
        private int _open;

        public int Connects => _connects;

        public int OpenSessions => _open;

        public Task<RadioState> GetRadioStateAsync(CancellationToken ct) => Task.FromResult(Radio);

        public Task<IStationSession?> ConnectAsync(ulong? knownAddress, CancellationToken ct)
        {
            Interlocked.Increment(ref _connects);
            Interlocked.Increment(ref _open);
            return Task.FromResult<IStationSession?>(new Session(this));
        }

        private sealed class Session(FakeTransport owner) : IStationSession
        {
            public ulong Address => FakeTransport.Address;

            public bool IsConnected => true;

            public Task<ushort[]?> ReadAsync(ushort start, ushort count, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                if (owner.FailReads)
                {
                    return Task.FromResult<ushort[]?>(null);
                }

                ushort[] regs = start switch
                {
                    Eb3aRegisters.CoreStart => Fixtures.DumpCore,
                    Eb3aRegisters.DetailStart => Fixtures.Detail(0),
                    _ => Fixtures.DumpSettings,
                };
                return Task.FromResult<ushort[]?>(regs);
            }

            public ValueTask DisposeAsync()
            {
                Interlocked.Decrement(ref owner._open);
                return ValueTask.CompletedTask;
            }
        }
    }
}
