using System.Threading.Channels;
using Upsy.Core.Station;

namespace Upsy.Core.Link;

/// <summary>An open GATT link to one station: bytes out through the write characteristic, notifications in.</summary>
public interface IGattChannel : IAsyncDisposable
{
    ulong Address { get; }

    bool IsConnected { get; }

    /// <summary>Notification payloads in arrival order.</summary>
    ChannelReader<byte[]> Notifications { get; }

    Task<bool> WriteAsync(ReadOnlyMemory<byte> data, bool withResponse, CancellationToken ct);
}

/// <summary>Bluetooth side of the monitor, implemented with WinRT in the app and faked in tests.</summary>
public interface IStationTransport
{
    Task<RadioState> GetRadioStateAsync(CancellationToken ct);

    /// <summary>Opens <paramref name="profile"/> on the station at <paramref name="address"/>; null when it does not answer or lacks the service.</summary>
    Task<IGattChannel?> OpenAsync(ulong address, GattProfile profile, CancellationToken ct);
}

/// <summary>The one BLE scanner the app runs; every advertisement it hears is raised once.</summary>
public interface IAdvertisementSource
{
    event EventHandler<Advertisement>? Received;
}

/// <summary>What a driver learned from the station; identity and settings may lag behind telemetry.</summary>
public sealed record StationData(Telemetry Telemetry, StationIdentity? Identity, StationSettings? Settings);

/// <summary>The protocol for one model. It runs on an open channel until the link fails or is cancelled.</summary>
public interface IStationDriver
{
    StationProfile Profile { get; }

    /// <summary>Publishes data as it arrives. Returns when the link stops answering; throws <see cref="StationAuthException"/> when the station cannot be read without user action.</summary>
    Task RunAsync(IGattChannel channel, Action<StationData> publish, CancellationToken ct);
}

/// <summary>The station refused us for a reason that retrying will not fix; the monitor waits for <see cref="StationMonitor.Retry"/>.</summary>
public sealed class StationAuthException : Exception
{
    public StationAuthException()
    {
    }

    public StationAuthException(string message)
        : base(message)
    {
    }

    public StationAuthException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public StationAuthException(LinkStatus status, string message)
        : base(message) => Status = status;

    public LinkStatus Status { get; } = LinkStatus.KeyRejected;
}
