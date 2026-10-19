// EcoFlow BLE session (encryption type 7). Protocol knowledge from rabits/ha-ef-ble (Apache-2.0), see THIRD-PARTY-NOTICES.md.
using Upsy.Core.Link;

namespace Upsy.Core.Protocol.EcoFlow;

/// <summary>
/// One authenticated session on an open channel. <see cref="OpenAsync"/> runs the handshake:
/// public key exchange, session key request, authentication status, authentication with the account token.
/// After that the station pushes its heartbeats; <see cref="ReadPacketAsync"/> returns them one by one.
/// Only these session messages are ever sent: no command that changes the station.
/// </summary>
public sealed class EcoFlowSession
{
    public const byte AppAddress = 0x21;
    public const byte AuthAddress = 0x35;
    public const byte AuthCmdSet = 0x35;
    public const byte AuthStatusCmd = 0x89;
    public const byte AuthCmd = 0x86;
    public const byte AuthOk = 0x00;
    public const byte AuthWrongKey = 0x06;
    public static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

    private readonly IGattChannel _channel;
    private readonly TimeProvider _clock;
    private readonly EcoFlowFrameReader _reader = new();
    private readonly Queue<EcoFlowPacket> _early = new();
    private byte[] _key = [];
    private byte[] _iv = [];

    private EcoFlowSession(IGattChannel channel, TimeProvider clock)
    {
        _channel = channel;
        _clock = clock;
    }

    /// <summary>Runs the handshake. Throws <see cref="StationAuthException"/> for a rejected key and <see cref="TimeoutException"/> when a step gets no answer.</summary>
    public static async Task<EcoFlowSession> OpenAsync(IGattChannel channel, string userId, string serial, EcoFlowKeyTable keyTable, TimeProvider clock, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(channel);
        var session = new EcoFlowSession(channel, clock);
        await session.HandshakeAsync(userId, serial, keyTable, ct).ConfigureAwait(false);
        return session;
    }

    /// <summary>The next data packet from the station; frames that fail to decrypt or parse are skipped.</summary>
    public async Task<EcoFlowPacket> ReadPacketAsync(CancellationToken ct)
    {
        if (_early.TryDequeue(out var early))
        {
            return early;
        }

        while (true)
        {
            var (type, payload) = await ReadFrameAsync(ct).ConfigureAwait(false);
            if (type == EcoFlowFrame.TypeCommand)
            {
                continue;
            }

            if (EcoFlowPacket.TryParse(EcoFlowCrypto.Decrypt(_key, _iv, payload)) is { } packet)
            {
                return packet;
            }
        }
    }

    private async Task HandshakeAsync(string userId, string serial, EcoFlowKeyTable keyTable, CancellationToken ct)
    {
        using var exchange = new EcoFlowKeyExchange();
        await SendAsync(EcoFlowFrame.Build(EcoFlowFrame.TypeCommand, [0x01, 0x00, .. exchange.PublicKey]), ct).ConfigureAwait(false);
        var reply = await ReadPlainAsync(0x01, 43, ct).ConfigureAwait(false);
        var size = reply[2] switch { 1 => 52, 2 => 56, 3 or 4 => 64, _ => 40 };
        if (reply.Length < 3 + size)
        {
            throw new InvalidDataException("short public key reply");
        }

        (_key, _iv) = EcoFlowCrypto.InitialKey(exchange.DeriveSharedSecret(reply.AsSpan(3, size)));

        await SendAsync(EcoFlowFrame.Build(EcoFlowFrame.TypeCommand, [0x02]), ct).ConfigureAwait(false);
        var keyInfo = EcoFlowCrypto.Decrypt(_key, _iv, (await ReadPlainAsync(0x02, 33, ct).ConfigureAwait(false)).AsSpan(1));
        if (keyInfo.Length < 18)
        {
            throw new InvalidDataException("short session key reply");
        }

        _key = EcoFlowCrypto.SessionKey(keyTable, keyInfo.AsSpan(16, 2), keyInfo.AsSpan(0, 16));

        await SendPacketAsync(new EcoFlowPacket(AppAddress, AuthAddress, AuthCmdSet, AuthStatusCmd, []), ct).ConfigureAwait(false);
        await SendPacketAsync(new EcoFlowPacket(AppAddress, AuthAddress, AuthCmdSet, AuthCmd, EcoFlowCrypto.AuthToken(userId, serial)), ct).ConfigureAwait(false);

        using var timeout = new CancellationTokenSource(StepTimeout, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            while (true)
            {
                var packet = await ReadPacketAsync(linked.Token).ConfigureAwait(false);
                if (packet.Src != AuthAddress || packet.CmdSet != AuthCmdSet)
                {
                    // Data before the auth reply also means the station accepted us.
                    _early.Enqueue(packet);
                    return;
                }

                if (packet.CmdId != AuthCmd)
                {
                    continue;
                }

                var code = packet.Payload.Length > 0 ? packet.Payload[0] : (byte)0xFF;
                if (code == AuthOk)
                {
                    return;
                }

                throw code == AuthWrongKey
                    ? new StationAuthException(LinkStatus.KeyRejected, "the station rejected the account key (06)")
                    : new InvalidOperationException($"authentication failed with code {code:X2}");
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException("no authentication reply");
        }
    }

    private async Task<byte[]> ReadPlainAsync(byte type, int minLength, CancellationToken ct)
    {
        using var timeout = new CancellationTokenSource(StepTimeout, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            while (true)
            {
                var (_, payload) = await ReadFrameAsync(linked.Token).ConfigureAwait(false);
                // A station may repeat the previous step; only this step's reply counts.
                if (payload.Length >= minLength && payload[0] == type)
                {
                    return payload;
                }
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException($"no reply to handshake step {type:X2}");
        }
    }

    private async Task<(byte Type, byte[] Payload)> ReadFrameAsync(CancellationToken ct)
    {
        while (true)
        {
            if (_reader.TryTake(out var type, out var payload))
            {
                return (type, payload);
            }

            _reader.Append(await _channel.Notifications.ReadAsync(ct).ConfigureAwait(false));
        }
    }

    private Task SendPacketAsync(EcoFlowPacket packet, CancellationToken ct) =>
        SendAsync(EcoFlowFrame.Build(EcoFlowFrame.TypeProtocol, EcoFlowCrypto.Encrypt(_key, _iv, packet.ToBytes())), ct);

    private async Task SendAsync(byte[] frame, CancellationToken ct)
    {
        if (!await _channel.WriteAsync(frame, withResponse: true, ct).ConfigureAwait(false))
        {
            throw new IOException("write to the station failed");
        }
    }
}
