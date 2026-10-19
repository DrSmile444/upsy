using Upsy.Core.Link;
using Upsy.Core.Protocol;

namespace Upsy.Core.Station;

/// <summary>Bluetti EB3A: Modbus register reads over service ff00 (write ff02, notify ff01), polled every 2 s.</summary>
public sealed class BluettiDriver(TimeProvider clock) : IStationDriver
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan SettingsInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(4);
    public const int MaxConsecutiveFailures = 3;

    public StationProfile Profile => StationCatalog.Eb3a;

    public async Task RunAsync(IGattChannel channel, Action<StationData> publish, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(publish);
        var failures = 0;
        var nextSettingsRead = DateTimeOffset.MinValue;
        StationIdentity? identity = null;
        StationSettings? settings = null;

        while (!ct.IsCancellationRequested && channel.IsConnected)
        {
            var core = await ReadAsync(channel, Eb3aRegisters.CoreStart, Eb3aRegisters.CoreCount, ct).ConfigureAwait(false);
            var detail = core is null ? null : await ReadAsync(channel, Eb3aRegisters.DetailStart, Eb3aRegisters.DetailCount, ct).ConfigureAwait(false);
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
                var raw = await ReadAsync(channel, Eb3aRegisters.SettingsStart, Eb3aRegisters.SettingsCount, ct).ConfigureAwait(false);
                if (raw is not null)
                {
                    settings = Eb3aRegisters.DecodeSettings(raw);
                }
            }

            publish(new StationData(Eb3aRegisters.DecodeTelemetry(core, detail), identity, settings));
            await Task.Delay(PollInterval, clock, ct).ConfigureAwait(false);
        }
    }

    /// <summary>One request/response; null on timeout or an invalid response, which the poll loop counts as a failure.</summary>
    private async Task<ushort[]?> ReadAsync(IGattChannel channel, ushort start, ushort count, CancellationToken ct)
    {
        // A late answer to an earlier request must not be taken for this one.
        while (channel.Notifications.TryRead(out _))
        {
        }

        if (!await channel.WriteAsync(Modbus.BuildReadRequest(start, count), withResponse: false, ct).ConfigureAwait(false))
        {
            return null;
        }

        using var timeout = new CancellationTokenSource(RequestTimeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        var assembler = new FrameAssembler();
        try
        {
            while (true)
            {
                var chunk = await channel.Notifications.ReadAsync(linked.Token).ConfigureAwait(false);
                if (assembler.Append(chunk) is { } frame)
                {
                    return Modbus.ParseReadResponse(frame, count).Registers;
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (System.Threading.Channels.ChannelClosedException)
        {
            return null;
        }
    }
}
