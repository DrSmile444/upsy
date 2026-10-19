using Upsy.Core.Station;

namespace Upsy.Core.Link;

public enum RadioState
{
    On,
    Off,
    Unavailable,
}

public enum LinkStatus
{
    BluetoothOff,
    Searching,
    Connecting,
    Connected,
    Paused,

    /// <summary>EcoFlow: no account user ID is stored yet.</summary>
    SignInNeeded,

    /// <summary>EcoFlow: the station answered authentication with the wrong-key code.</summary>
    KeyRejected,

    /// <summary>EcoFlow: the key table file is missing or invalid.</summary>
    KeyTableMissing,
}

public sealed record MonitorSnapshot(
    LinkStatus Status,
    Telemetry? Telemetry,
    StationIdentity? Identity,
    StationSettings? Settings,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? LastSeen,
    DateTimeOffset? PausedUntil,
    int Failures = 0)
{
    public static MonitorSnapshot Initial { get; } = new(LinkStatus.Searching, null, null, null, null, null, null);

    public bool HasData => Status == LinkStatus.Connected && Telemetry is not null;

    /// <summary>The station cannot be read until the user acts (sign in, rebind, install the key table).</summary>
    public bool NeedsUser => Status is LinkStatus.SignInNeeded or LinkStatus.KeyRejected or LinkStatus.KeyTableMissing;
}

/// <summary>
/// Owns one station's connection lifecycle: radio check, open the GATT channel, run the model's driver under a
/// no-update watchdog, back off, pause. The protocol itself lives in the driver.
/// </summary>
public sealed class StationMonitor(IStationTransport transport, IStationDriver driver, TimeProvider clock, ulong address) : IDisposable
{
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan RadioRecheck = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan PauseDuration = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan WatchdogTick = TimeSpan.FromSeconds(1);

    private readonly object _gate = new();
    private CancellationTokenSource _interrupt = new();
    private CancellationTokenSource? _linked;
    private DateTimeOffset? _pausedUntil;
    private LinkStatus? _blockedBy;
    private ulong _address = address;
    private MonitorSnapshot _current = MonitorSnapshot.Initial;

    public event EventHandler<MonitorSnapshot>? Changed;

    public MonitorSnapshot Current => _current;

    public StationProfile Profile => driver.Profile;

    public ulong Address
    {
        get
        {
            lock (_gate)
            {
                return _address;
            }
        }
    }

    /// <summary>Releases the link for <see cref="PauseDuration"/> so another client can connect; resumes by itself.</summary>
    public void Pause()
    {
        lock (_gate)
        {
            _pausedUntil = clock.GetUtcNow() + PauseDuration;
        }

        Interrupt();
    }

    public void Resume()
    {
        lock (_gate)
        {
            _pausedUntil = null;
        }

        Interrupt();
    }

    /// <summary>Drops the current link and connects again, e.g. after the PC resumes from sleep.</summary>
    public void Reconnect() => Interrupt();

    /// <summary>Tries again after the user acted on a <see cref="MonitorSnapshot.NeedsUser"/> status (signed in, rebound the station).</summary>
    public void Retry()
    {
        lock (_gate)
        {
            _blockedBy = null;
        }

        Interrupt();
    }

    /// <summary>The station was seen advertising at a new address; the next connect uses it.</summary>
    public void UpdateAddress(ulong newAddress)
    {
        lock (_gate)
        {
            if (_address == newAddress)
            {
                return;
            }

            _address = newAddress;
        }

        Interrupt();
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var failures = 0;
        while (!ct.IsCancellationRequested)
        {
            var wake = WakeToken(ct);
            try
            {
                var pausedUntil = PausedUntil();
                if (pausedUntil is not null)
                {
                    Publish(_current with { Status = LinkStatus.Paused, PausedUntil = pausedUntil });
                    await Task.Delay(pausedUntil.Value - clock.GetUtcNow(), clock, wake).ConfigureAwait(false);
                    lock (_gate)
                    {
                        _pausedUntil = null;
                    }

                    continue;
                }

                if (Blocked() is { } blocked)
                {
                    Publish(_current with { Status = blocked, PausedUntil = null });
                    await Task.Delay(Timeout.InfiniteTimeSpan, clock, wake).ConfigureAwait(false);
                    continue;
                }

                if (await transport.GetRadioStateAsync(wake).ConfigureAwait(false) != RadioState.On)
                {
                    Publish(_current with { Status = LinkStatus.BluetoothOff, PausedUntil = null });
                    await Task.Delay(RadioRecheck, clock, wake).ConfigureAwait(false);
                    continue;
                }

                Publish(_current with { Status = LinkStatus.Connecting, PausedUntil = null, Failures = failures });
                var channel = await transport.OpenAsync(Address, driver.Profile.Gatt, wake).ConfigureAwait(false);
                if (channel is null)
                {
                    failures++;
                    Publish(_current with { Status = LinkStatus.Searching, Failures = failures });
                    await Task.Delay(Backoff(failures, driver.Profile.MinReconnectDelay), clock, wake).ConfigureAwait(false);
                    continue;
                }

                await using (channel.ConfigureAwait(false))
                {
                    if (await RunDriverAsync(channel, wake).ConfigureAwait(false))
                    {
                        failures = 0;
                    }
                }

                failures++;
                Publish(_current with { Status = LinkStatus.Connecting, Failures = failures });
                await Task.Delay(Backoff(failures, driver.Profile.MinReconnectDelay), clock, wake).ConfigureAwait(false);
            }
            catch (StationAuthException ex) when (!ct.IsCancellationRequested)
            {
                lock (_gate)
                {
                    _blockedBy = ex.Status;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Interrupted by Pause/Resume/Reconnect/Retry: loop again with the new state.
            }
            catch (OperationCanceledException)
            {
                break;
            }
#pragma warning disable CA1031 // The loop must survive any transport or protocol failure and retry.
            catch (Exception)
            {
                failures++;
                Publish(_current with { Status = LinkStatus.Searching, Failures = failures });
                try
                {
                    await Task.Delay(Backoff(failures, driver.Profile.MinReconnectDelay), clock, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
#pragma warning restore CA1031
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _interrupt.Dispose();
            _linked?.Dispose();
        }
    }

    internal static TimeSpan Backoff(int failures, TimeSpan minimum = default)
    {
        var seconds = Math.Min(MaxBackoff.TotalSeconds, 2 * Math.Pow(2, Math.Max(0, failures - 1)));
        var span = TimeSpan.FromSeconds(seconds);
        return span < minimum ? minimum : span;
    }

    /// <summary>Runs the driver until it returns, throws, or stays silent past the profile's timeout. True when it delivered data.</summary>
    private async Task<bool> RunDriverAsync(IGattChannel channel, CancellationToken ct)
    {
        using var run = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var lastTicks = clock.GetUtcNow().UtcTicks;
        var delivered = false;
        var watchdog = WatchAsync();
        try
        {
            await driver.RunAsync(
                channel,
                data =>
                {
                    var now = clock.GetUtcNow();
                    Interlocked.Exchange(ref lastTicks, now.UtcTicks);
                    delivered = true;
                    Publish(new MonitorSnapshot(LinkStatus.Connected, data.Telemetry, data.Identity ?? _current.Identity, data.Settings ?? _current.Settings, now, now, null));
                },
                run.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Watchdog: no update for too long.
        }
        finally
        {
            await run.CancelAsync().ConfigureAwait(false);
            await watchdog.ConfigureAwait(false);
        }

        return delivered;

        async Task WatchAsync()
        {
            try
            {
                while (!run.Token.IsCancellationRequested)
                {
                    await Task.Delay(WatchdogTick, clock, run.Token).ConfigureAwait(false);
                    if (clock.GetUtcNow().UtcTicks - Interlocked.Read(ref lastTicks) > driver.Profile.NoUpdateTimeout.Ticks)
                    {
                        await run.CancelAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private void Publish(MonitorSnapshot snapshot)
    {
        if (snapshot.Status != LinkStatus.Connected)
        {
            snapshot = snapshot with { Telemetry = null, UpdatedAt = null };
        }
        else
        {
            snapshot = snapshot with { Failures = 0 };
        }

        _current = snapshot;
        Changed?.Invoke(this, snapshot);
    }

    private DateTimeOffset? PausedUntil()
    {
        lock (_gate)
        {
            return _pausedUntil;
        }
    }

    private LinkStatus? Blocked()
    {
        lock (_gate)
        {
            return _blockedBy;
        }
    }

    private CancellationToken WakeToken(CancellationToken ct)
    {
        lock (_gate)
        {
            if (_interrupt.IsCancellationRequested)
            {
                _interrupt.Dispose();
                _interrupt = new CancellationTokenSource();
            }

            _linked?.Dispose();
            _linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _interrupt.Token);
            return _linked.Token;
        }
    }

    private void Interrupt()
    {
        lock (_gate)
        {
            _interrupt.Cancel();
        }
    }
}
