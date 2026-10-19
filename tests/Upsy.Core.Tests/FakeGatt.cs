using System.Threading.Channels;
using Upsy.Core.Link;
using Upsy.Core.Station;

namespace Upsy.Core.Tests;

/// <summary>A GATT channel whose station side is scripted by the test: writes go to <see cref="OnWrite"/>, replies go out through <see cref="Notify"/>.</summary>
internal sealed class FakeChannel(ulong address) : IGattChannel
{
    private readonly Channel<byte[]> _notifications = Channel.CreateUnbounded<byte[]>();

    public ulong Address => address;

    public bool IsConnected { get; set; } = true;

    public bool Disposed { get; private set; }

    public List<byte[]> Written { get; } = [];

    public Func<byte[], bool>? OnWrite { get; set; }

    public ChannelReader<byte[]> Notifications => _notifications.Reader;

    public void Notify(byte[] data) => _notifications.Writer.TryWrite(data);

    public Task<bool> WriteAsync(ReadOnlyMemory<byte> data, bool withResponse, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var bytes = data.ToArray();
        lock (Written)
        {
            Written.Add(bytes);
        }

        return Task.FromResult(OnWrite?.Invoke(bytes) ?? true);
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        _notifications.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}

/// <summary>A transport that hands out channels from a factory and counts them.</summary>
internal sealed class FakeTransport(Func<ulong, FakeChannel?> open) : IStationTransport
{
    private int _opens;
    private int _open;

    public volatile RadioState Radio = RadioState.On;

    public int Opens => _opens;

    public int OpenChannels => _open;

    public List<ulong> Addresses { get; } = [];

    public Task<RadioState> GetRadioStateAsync(CancellationToken ct) => Task.FromResult(Radio);

    public Task<IGattChannel?> OpenAsync(ulong address, GattProfile profile, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _opens);
        lock (Addresses)
        {
            Addresses.Add(address);
        }

        var channel = open(address);
        if (channel is null)
        {
            return Task.FromResult<IGattChannel?>(null);
        }

        Interlocked.Increment(ref _open);
        return Task.FromResult<IGattChannel?>(new Counted(channel, this));
    }

    private sealed class Counted(FakeChannel inner, FakeTransport owner) : IGattChannel
    {
        public ulong Address => inner.Address;

        public bool IsConnected => inner.IsConnected;

        public ChannelReader<byte[]> Notifications => inner.Notifications;

        public Task<bool> WriteAsync(ReadOnlyMemory<byte> data, bool withResponse, CancellationToken ct) => inner.WriteAsync(data, withResponse, ct);

        public async ValueTask DisposeAsync()
        {
            Interlocked.Decrement(ref owner._open);
            await inner.DisposeAsync();
        }
    }
}

/// <summary>An EB3A on the other side of a <see cref="FakeChannel"/>: answers Modbus reads from the captured dump, split in two notifications.</summary>
internal static class FakeEb3a
{
    public const ulong Address = 0xA1B2C3D4E5F6;

    public static FakeChannel Create(Func<bool>? fail = null)
    {
        var channel = new FakeChannel(Address);
        channel.OnWrite = request =>
        {
            var start = (ushort)((request[2] << 8) | request[3]);
            ushort[] regs = start switch
            {
                Eb3aRegisters.CoreStart => Fixtures.DumpCore,
                Eb3aRegisters.DetailStart => Fixtures.Detail(0),
                _ => Fixtures.DumpSettings,
            };
            var response = Fixtures.Response(regs);
            if (fail?.Invoke() == true)
            {
                response[^1] ^= 0xFF; // bad CRC: the read fails at once
            }

            channel.Notify(response[..5]);
            channel.Notify(response[5..]);
            return true;
        };
        return channel;
    }
}

/// <summary>A driver whose behaviour the test supplies, for monitor tests that are not about a protocol.</summary>
internal sealed class ScriptedDriver(StationProfile profile, Func<IGattChannel, Action<StationData>, CancellationToken, Task> run) : IStationDriver
{
    public StationProfile Profile => profile;

    public Task RunAsync(IGattChannel channel, Action<StationData> publish, CancellationToken ct) => run(channel, publish, ct);
}
