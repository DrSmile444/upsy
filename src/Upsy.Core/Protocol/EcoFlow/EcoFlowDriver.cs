using Upsy.Core.Link;
using Upsy.Core.Station;

namespace Upsy.Core.Protocol.EcoFlow;

/// <summary>EcoFlow DELTA 2: encrypted session, then decode the heartbeats the station pushes (about 7 packets/s).</summary>
/// <param name="userId">The stored EcoFlow account user ID, read at every connect; null when not signed in.</param>
/// <param name="keyTable">The key table, read at every connect; null when missing or invalid.</param>
public sealed class EcoFlowDriver(string serial, Func<string?> userId, Func<EcoFlowKeyTable?> keyTable, TimeProvider clock) : IStationDriver
{
    public StationProfile Profile => StationCatalog.Delta2;

    public async Task RunAsync(IGattChannel channel, Action<StationData> publish, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(publish);
        var table = keyTable() ?? throw new StationAuthException(LinkStatus.KeyTableMissing, "EcoFlow key table missing or invalid");
        var id = userId() ?? throw new StationAuthException(LinkStatus.SignInNeeded, "no EcoFlow user ID");
        var session = await EcoFlowSession.OpenAsync(channel, id, serial, table, clock, ct).ConfigureAwait(false);
        var decoder = new Delta2Decoder(serial);
        while (!ct.IsCancellationRequested)
        {
            var packet = await session.ReadPacketAsync(ct).ConfigureAwait(false);
            // The station repeats every heartbeat about once a second; publish on PD, the one that carries power.
            if (decoder.Apply(packet) && packet.Src == 0x02 && decoder.Snapshot() is { } data)
            {
                publish(data);
            }
        }
    }
}
