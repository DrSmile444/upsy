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
}

/// <summary>Bluetooth side of the monitor, implemented with WinRT in the app and faked in tests.</summary>
public interface IStationTransport
{
    Task<RadioState> GetRadioStateAsync(CancellationToken ct);

    /// <summary>Connects to <paramref name="knownAddress"/>, or scans by name when it is null or fails. Null when nothing was found.</summary>
    Task<IStationSession?> ConnectAsync(ulong? knownAddress, CancellationToken ct);
}

public interface IStationSession : IAsyncDisposable
{
    ulong Address { get; }

    bool IsConnected { get; }

    /// <summary>Reads registers; null on timeout or an invalid response.</summary>
    Task<ushort[]?> ReadAsync(ushort start, ushort count, CancellationToken ct);
}

public sealed record MonitorSnapshot(
    LinkStatus Status,
    Telemetry? Telemetry,
    StationIdentity? Identity,
    StationSettings? Settings,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? LastSeen,
    DateTimeOffset? PausedUntil)
{
    public static MonitorSnapshot Initial { get; } = new(LinkStatus.Searching, null, null, null, null, null, null);

    public bool HasData => Status == LinkStatus.Connected && Telemetry is not null;
}

/// <summary>Owns the connection lifecycle: radio check, connect, poll, back off, pause.</summary>
public sealed class StationMonitor(IStationTransport transport, TimeProvider clock, ulong? knownAddress = null) : IDisposable
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan SettingsInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan RadioRecheck = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan PauseDuration = TimeSpan.FromMinutes(30);
    public const int MaxConsecutiveFailures = 3;

    private readonly object _gate = new();
    private CancellationTokenSource _interrupt = new();
    private CancellationTokenSource? _linked;
    private DateTimeOffset? _pausedUntil;
    private ulong? _address = knownAddress;
    private MonitorSnapshot _current = MonitorSnapshot.Initial;

    public event EventHandler<MonitorSnapshot>? Changed;

    public event EventHandler<ulong>? AddressLearned;

    public MonitorSnapshot Current => _current;

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

    /// <summary>Drops the current session and connects again, e.g. after the PC resumes from sleep.</summary>
    public void Reconnect() => Interrupt();

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

                if (await transport.GetRadioStateAsync(wake).ConfigureAwait(false) != RadioState.On)
                {
                    Publish(_current with { Status = LinkStatus.BluetoothOff, PausedUntil = null });
                    await Task.Delay(RadioRecheck, clock, wake).ConfigureAwait(false);
                    continue;
                }

                Publish(_current with { Status = _address is null ? LinkStatus.Searching : LinkStatus.Connecting, PausedUntil = null });
                var session = await transport.ConnectAsync(_address, wake).ConfigureAwait(false);
                if (session is null)
                {
                    failures++;
                    Publish(_current with { Status = LinkStatus.Searching });
                    await Task.Delay(Backoff(failures), clock, wake).ConfigureAwait(false);
                    continue;
                }

                failures = 0;
                if (_address != session.Address)
                {
                    _address = session.Address;
                    AddressLearned?.Invoke(this, session.Address);
                }

                await using (session.ConfigureAwait(false))
                {
                    await PollAsync(session, wake).ConfigureAwait(false);
                }

                Publish(_current with { Status = LinkStatus.Connecting });
                await Task.Delay(Backoff(1), clock, wake).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Interrupted by Pause/Resume/Reconnect: loop again with the new state.
            }
            catch (OperationCanceledException)
            {
                break;
            }
#pragma warning disable CA1031 // The loop must survive any transport failure and retry.
            catch (Exception)
            {
                failures++;
                Publish(_current with { Status = LinkStatus.Searching });
                try
                {
                    await Task.Delay(Backoff(failures), clock, ct).ConfigureAwait(false);
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

    internal static TimeSpan Backoff(int failures)
    {
        var seconds = Math.Min(MaxBackoff.TotalSeconds, 2 * Math.Pow(2, Math.Max(0, failures - 1)));
        return TimeSpan.FromSeconds(seconds);
    }

    private async Task PollAsync(IStationSession session, CancellationToken ct)
    {
        var failures = 0;
        var nextSettingsRead = DateTimeOffset.MinValue;
        StationIdentity? identity = null;
        var settings = _current.Settings;

        while (!ct.IsCancellationRequested && session.IsConnected)
        {
            var core = await session.ReadAsync(Eb3aRegisters.CoreStart, Eb3aRegisters.CoreCount, ct).ConfigureAwait(false);
            var detail = core is null ? null : await session.ReadAsync(Eb3aRegisters.DetailStart, Eb3aRegisters.DetailCount, ct).ConfigureAwait(false);
            if (core is null || detail is null)
            {
                if (++failures >= MaxConsecutiveFailures)
                {
                    return;
                }

                continue;
            }

            failures = 0;
            identity ??= Eb3aRegisters.DecodeIdentity(core);
            var now = clock.GetUtcNow();
            if (now >= nextSettingsRead)
            {
                nextSettingsRead = now + SettingsInterval;
                var raw = await session.ReadAsync(Eb3aRegisters.SettingsStart, Eb3aRegisters.SettingsCount, ct).ConfigureAwait(false);
                if (raw is not null)
                {
                    settings = Eb3aRegisters.DecodeSettings(raw);
                }
            }

            Publish(new MonitorSnapshot(LinkStatus.Connected, Eb3aRegisters.DecodeTelemetry(core, detail), identity, settings, now, now, null));
            await Task.Delay(PollInterval, clock, ct).ConfigureAwait(false);
        }
    }

    private void Publish(MonitorSnapshot snapshot)
    {
        if (snapshot.Status != LinkStatus.Connected)
        {
            snapshot = snapshot with { Telemetry = null, UpdatedAt = null };
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
