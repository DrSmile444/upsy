using System.Security.Cryptography;
using Microsoft.Extensions.Time.Testing;
using Upsy.Core.Link;
using Upsy.Core.Protocol.EcoFlow;
using Upsy.Core.Station;

namespace Upsy.Core.Tests;

public sealed class EcoFlowFramingTests
{
    // A version 2 packet published by nielsole/ecoflow-bt-reverse-engineering (MIT): both CRCs are the station's own.
    private static readonly byte[] KnownPacket = Convert.FromHexString("AA020700DE0D00000000000021052042" + "01FFFFFFFFFFFF6B11");

    [Fact]
    public void Crcs_match_a_known_packet()
    {
        Assert.Equal(0xDE, EcoFlowCrc.Crc8(KnownPacket.AsSpan(0, 4)));
        Assert.Equal(0x116B, EcoFlowCrc.Crc16(KnownPacket.AsSpan(0, KnownPacket.Length - 2)));
    }

    [Fact]
    public void Packet_round_trips_and_matches_the_known_bytes()
    {
        var packet = EcoFlowPacket.TryParse(KnownPacket)!;
        Assert.Equal((0x21, 0x05, 0x20, 0x42), (packet.Src, packet.Dst, packet.CmdSet, packet.CmdId));
        Assert.Equal(KnownPacket, packet.ToBytes());
    }

    [Fact]
    public void Payload_xor_with_the_first_seq_byte_is_undone()
    {
        var plain = new byte[] { 0x52, 0x00, 0x64 };
        var wire = new EcoFlowPacket(0x02, 0x21, 0x20, 0x02, [.. plain.Select(b => (byte)(b ^ 0x5C))], Seq: 0x5C).ToBytes();
        Assert.Equal(plain, EcoFlowPacket.TryParse(wire)!.Payload);
    }

    [Fact]
    public void Corrupt_packets_are_rejected()
    {
        var bad = (byte[])KnownPacket.Clone();
        bad[^1] ^= 1;
        Assert.Null(EcoFlowPacket.TryParse(bad));
        Assert.Null(EcoFlowPacket.TryParse(KnownPacket.AsSpan(0, 10)));
    }

    [Fact]
    public void Frames_are_joined_across_notifications_and_survive_garbage_and_bad_crcs()
    {
        var a = EcoFlowFrame.Build(EcoFlowFrame.TypeCommand, [0x01, 0x00, 0x00, 0xAB]);
        var b = EcoFlowFrame.Build(EcoFlowFrame.TypeProtocol, [0x10, 0x20]);
        var broken = (byte[])b.Clone();
        broken[^1] ^= 0xFF;
        byte[] stream = [0x00, 0x13, .. a, .. broken, .. b];
        var reader = new EcoFlowFrameReader();
        var frames = new List<(byte, byte[])>();
        foreach (var chunk in stream.Chunk(5))
        {
            reader.Append(chunk);
            while (reader.TryTake(out var type, out var payload))
            {
                frames.Add((type, payload));
            }
        }

        Assert.Equal(2, frames.Count);
        Assert.Equal(EcoFlowFrame.TypeCommand, frames[0].Item1);
        Assert.Equal([0x01, 0x00, 0x00, 0xAB], frames[0].Item2);
        Assert.Equal(EcoFlowFrame.TypeProtocol, frames[1].Item1);
        Assert.Equal([0x10, 0x20], frames[1].Item2);
    }

    [Fact]
    public void Advertisement_gives_serial_level_and_encryption_type()
    {
        // Layout of the captured advertisement, with a placeholder serial: battery 0x54 = 84%, flags 0x3E = type 7.
        var data = Convert.FromHexString("13" + Convert.ToHexString("R331EXAMPLE00001"u8) + "54000100003EA3");
        var ad = EcoFlowAdvertisement.TryParse(data)!;
        Assert.Equal(("R331EXAMPLE00001", 84, 7), (ad.Serial, ad.BatteryPercent!.Value, ad.EncryptType));

        var station = StationCatalog.Recognize(new Advertisement(0x01, "R33-0001", new Dictionary<ushort, byte[]> { [EcoFlowAdvertisement.ManufacturerId] = data }, -60));
        Assert.Equal(StationCatalog.Delta2, station!.Profile);
        Assert.Equal(84, station.AdvertisedPercent);
    }

    [Fact]
    public void Other_ecoflow_models_and_strangers_are_not_offered()
    {
        var river = Convert.FromHexString("13" + Convert.ToHexString("R601EXAMPLE00001"u8) + "54000100003EA3");
        Assert.Null(StationCatalog.Recognize(new Advertisement(0x01, null, new Dictionary<ushort, byte[]> { [EcoFlowAdvertisement.ManufacturerId] = river }, -60)));
        Assert.Null(StationCatalog.Recognize(new Advertisement(0x02, "Headphones", new Dictionary<ushort, byte[]>(), -60)));
        Assert.Equal(StationCatalog.Eb3a, StationCatalog.Recognize(new Advertisement(0x03, "EB3A2240000000001", new Dictionary<ushort, byte[]>(), -60))!.Profile);
    }
}

public sealed class EcoFlowCryptoTests
{
    [Theory]
    [InlineData(0x03, 0x02, 304)]
    [InlineData(0x01, 0x02, 272)]
    [InlineData(0x00, 0x02, 256)]
    [InlineData(0x0E, 0x02, 480)]
    public void Seed_selects_the_key_table_offset(byte s0, byte s1, int offset) =>
        Assert.Equal(offset, EcoFlowCrypto.KeyTableOffset([s0, s1]));

    [Fact]
    public void Session_key_is_md5_of_table_bytes_and_srand()
    {
        // Expected value computed independently with Python's hashlib.
        var key = EcoFlowCrypto.SessionKey(FakeDelta2.Table, [0x03, 0x02], [.. Enumerable.Range(0, 16).Select(i => (byte)i)]);
        Assert.Equal("7EFCD4C9E72FC3757248786CB4A354C5", Convert.ToHexString(key));
    }

    [Fact]
    public void Auth_token_is_upper_case_hex_of_md5_of_user_and_serial() =>
        Assert.Equal("65174ACC6FEB20D1D37744D4AC322671"u8.ToArray(), EcoFlowCrypto.AuthToken("1234567890123456789", "R331EXAMPLE00001"));

    [Fact]
    public void Key_exchange_uses_twenty_byte_coordinates_and_agrees_on_both_sides()
    {
        using var app = new EcoFlowKeyExchange();
        using var station = new EcoFlowKeyExchange();
        Assert.Equal(40, app.PublicKey.Length);
        Assert.Equal(app.DeriveSharedSecret(station.PublicKey), station.DeriveSharedSecret(app.PublicKey));
        Assert.Equal(20, app.DeriveSharedSecret(station.PublicKey).Length);
    }

    [Fact]
    public void Aes_round_trip_and_partial_trailing_block_is_ignored()
    {
        var key = new byte[16];
        var iv = new byte[16];
        var plain = "heartbeat payload"u8.ToArray();
        var cipher = EcoFlowCrypto.Encrypt(key, iv, plain);
        Assert.Equal(plain, EcoFlowCrypto.Decrypt(key, iv, cipher));
        Assert.Equal(plain, EcoFlowCrypto.Decrypt(key, iv, [.. cipher, 0x01, 0x02]));
    }

    [Fact]
    public void Key_table_must_be_the_real_one()
    {
        Assert.Null(EcoFlowKeyTable.FromBytes(new byte[EcoFlowKeyTable.Length]));
        Assert.Null(EcoFlowKeyTable.FromBytes(new byte[100]));
    }
}

public sealed class Delta2DecoderTests
{
    private static StationData Decode(byte[] pd, byte[] ems, byte[] inv, byte[] mppt)
    {
        var decoder = new Delta2Decoder("R331EXAMPLE00001");
        Assert.True(decoder.Apply(new EcoFlowPacket(0x03, 0x21, 0x20, 0x02, ems)));
        Assert.True(decoder.Apply(new EcoFlowPacket(0x04, 0x21, 0x20, 0x02, inv)));
        Assert.Null(decoder.Snapshot());
        Assert.True(decoder.Apply(new EcoFlowPacket(0x05, 0x21, 0x20, 0x02, mppt)));
        Assert.True(decoder.Apply(new EcoFlowPacket(0x02, 0x21, 0x20, 0x02, pd)));
        return decoder.Snapshot()!;
    }

    [Fact]
    public void On_battery_capture()
    {
        var d = Decode(Delta2Captures.BatteryPd, Delta2Captures.BatteryEms, Delta2Captures.BatteryInv, Delta2Captures.BatteryMppt);
        var t = d.Telemetry;
        Assert.Equal((81, 0, 121, 0.0), (t.BatteryPercent, t.AcInputW, t.AcOutputW, t.AcInputVolts));
        Assert.True(t.AcOutputOn);
        Assert.False(t.DcOutputOn);
        Assert.Equal(TimeSpan.FromMinutes(431), t.ToEmpty);
        Assert.Null(t.ToFull);
        Assert.False(Power.PowerReading.From(t).OnMains);
        Assert.Equal("DELTA 2", d.Identity!.Model);
        Assert.Equal("1.2.1.63", d.Identity.FirmwareText);
    }

    [Fact]
    public void On_mains_capture()
    {
        var d = Decode(Delta2Captures.MainsPd, Delta2Captures.MainsEms, Delta2Captures.MainsInv, Delta2Captures.MainsMppt);
        var t = d.Telemetry;
        Assert.Equal((80, 328, 101), (t.BatteryPercent, t.AcInputW, t.AcOutputW));
        Assert.Equal(233.337, t.AcInputVolts, 3);
        Assert.True(Power.PowerReading.From(t).OnMains);
        Assert.Equal(TimeSpan.FromMinutes(73), t.ToFull);
        Assert.Null(t.ToEmpty);

        // USB and 12 V switched on with nothing plugged in.
        Assert.True(t.DcOutputOn);
        Assert.Equal(0, t.DcOutputW);

        Assert.Equal(
            [
                new SettingReadout("Charge limit", "100%"),
                new SettingReadout("Discharge limit", "0%"),
                new SettingReadout("X-Boost", "On"),
                new SettingReadout("AC charging power", "200 W"),
            ],
            d.Settings!.Items);
    }

    [Fact]
    public void Short_or_unknown_packets_are_ignored()
    {
        var decoder = new Delta2Decoder("R331EXAMPLE00001");
        Assert.False(decoder.Apply(new EcoFlowPacket(0x02, 0x21, 0x20, 0x02, new byte[20])));
        Assert.False(decoder.Apply(new EcoFlowPacket(0x03, 0x21, 0x03, 0x0E, new byte[83])));
        Assert.Null(decoder.Snapshot());
    }
}

public sealed class EcoFlowSessionTests
{
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

    [Fact]
    public async Task Handshake_then_heartbeats_reach_the_monitor_as_telemetry()
    {
        var station = new FakeDelta2("1234567890123456789");
        var transport = new FakeTransport(_ => station.Connect());
        var driver = new EcoFlowDriver(FakeDelta2.Serial, () => "1234567890123456789", () => FakeDelta2.Table, _clock);
        using var monitor = new StationMonitor(transport, driver, _clock, 0x01);
        using var cts = new CancellationTokenSource();
        var run = monitor.RunAsync(cts.Token);

        await WaitFor(() => monitor.Current.HasData);
        Assert.Equal(80, monitor.Current.Telemetry!.BatteryPercent);
        Assert.Equal(FakeDelta2.Serial, monitor.Current.Identity!.Serial);
        Assert.True(station.Authenticated);
        Assert.All(station.Received, p => Assert.Equal((EcoFlowSession.AuthAddress, EcoFlowSession.AuthCmdSet), (p.Dst, p.CmdSet)));

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task Wrong_key_stops_and_reports_key_rejected()
    {
        var station = new FakeDelta2("1111111111111111111");
        var transport = new FakeTransport(_ => station.Connect());
        var driver = new EcoFlowDriver(FakeDelta2.Serial, () => "2222222222222222222", () => FakeDelta2.Table, _clock);
        using var monitor = new StationMonitor(transport, driver, _clock, 0x01);
        using var cts = new CancellationTokenSource();
        var run = monitor.RunAsync(cts.Token);

        await WaitFor(() => monitor.Current.Status == LinkStatus.KeyRejected);
        Assert.Equal(1, transport.Opens);

        await cts.CancelAsync();
        await run;
    }

    [Theory]
    [InlineData(false, true, LinkStatus.SignInNeeded)]
    [InlineData(true, false, LinkStatus.KeyTableMissing)]
    public async Task Missing_credentials_are_reported_without_a_handshake(bool hasUser, bool hasTable, LinkStatus expected)
    {
        var station = new FakeDelta2("1234567890123456789");
        var transport = new FakeTransport(_ => station.Connect());
        var driver = new EcoFlowDriver(FakeDelta2.Serial, () => hasUser ? "1234567890123456789" : null, () => hasTable ? FakeDelta2.Table : null, _clock);
        using var monitor = new StationMonitor(transport, driver, _clock, 0x01);
        using var cts = new CancellationTokenSource();
        var run = monitor.RunAsync(cts.Token);

        await WaitFor(() => monitor.Current.Status == expected);
        Assert.Empty(station.Received);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task A_silent_station_times_out_the_handshake()
    {
        var silent = new FakeChannel(0x01);
        var task = EcoFlowSession.OpenAsync(silent, "1", FakeDelta2.Serial, FakeDelta2.Table, _clock, TestContext.Current.CancellationToken);
        await WaitFor(() => silent.Written.Count == 1);
        _clock.Advance(EcoFlowSession.StepTimeout);
        await Assert.ThrowsAsync<TimeoutException>(() => task);
    }
}

/// <summary>
/// The station side of the DELTA 2 protocol, for tests: answers the handshake like the real station (ECDH,
/// encrypted srand and seed, auth status, auth with the account token) and then pushes the captured heartbeats.
/// </summary>
internal sealed class FakeDelta2(string boundUserId)
{
    public const string Serial = "R331EXAMPLE00001";
    internal static readonly EcoFlowKeyTable Table = EcoFlowKeyTable.ForTests([.. Enumerable.Range(0, EcoFlowKeyTable.Length).Select(i => (byte)((i * 31) + 7))]);

    public List<EcoFlowPacket> Received { get; } = [];

    public bool Authenticated { get; private set; }

    public FakeChannel Connect()
    {
        var channel = new FakeChannel(0x01);
        var exchange = new EcoFlowKeyExchange();
        var reader = new EcoFlowFrameReader();
        byte[] key = [], iv = [];
        channel.OnWrite = bytes =>
        {
            reader.Append(bytes);
            while (reader.TryTake(out var type, out var payload))
            {
                if (type == EcoFlowFrame.TypeCommand && payload[0] == 0x01)
                {
                    (key, iv) = EcoFlowCrypto.InitialKey(exchange.DeriveSharedSecret(payload.AsSpan(2, 40)));
                    channel.Notify(EcoFlowFrame.Build(EcoFlowFrame.TypeCommand, [0x01, 0x00, 0x00, .. exchange.PublicKey]));
                }
                else if (type == EcoFlowFrame.TypeCommand && payload[0] == 0x02)
                {
                    byte[] srand = RandomNumberGenerator.GetBytes(16);
                    byte[] seed = [0x03, 0x02];
                    channel.Notify(EcoFlowFrame.Build(EcoFlowFrame.TypeCommand, [0x02, .. EcoFlowCrypto.Encrypt(key, iv, [.. srand, .. seed])]));
                    key = EcoFlowCrypto.SessionKey(Table, seed, srand);
                }
                else if (type == EcoFlowFrame.TypeProtocol && EcoFlowPacket.TryParse(EcoFlowCrypto.Decrypt(key, iv, payload)) is { } packet)
                {
                    lock (Received)
                    {
                        Received.Add(packet);
                    }

                    if (packet.CmdId == EcoFlowSession.AuthStatusCmd)
                    {
                        Send(channel, key, iv, new EcoFlowPacket(0x35, 0x21, 0x35, 0x89, [0x01, 0x00]));
                    }
                    else if (packet.CmdId == EcoFlowSession.AuthCmd)
                    {
                        var ok = packet.Payload.SequenceEqual(EcoFlowCrypto.AuthToken(boundUserId, Serial));
                        Send(channel, key, iv, new EcoFlowPacket(0x35, 0x21, 0x35, 0x86, [ok ? EcoFlowSession.AuthOk : EcoFlowSession.AuthWrongKey]));
                        if (ok)
                        {
                            Authenticated = true;
                            // Heartbeats as the station pushes them, payload XORed with seq[0].
                            Send(channel, key, iv, new EcoFlowPacket(0x03, 0x21, 0x20, 0x02, Xor(Delta2Captures.MainsEms, 0x4A), Seq: 0x4A));
                            Send(channel, key, iv, new EcoFlowPacket(0x04, 0x21, 0x20, 0x02, Delta2Captures.MainsInv));
                            Send(channel, key, iv, new EcoFlowPacket(0x05, 0x21, 0x20, 0x02, Delta2Captures.MainsMppt));
                            Send(channel, key, iv, new EcoFlowPacket(0x02, 0x21, 0x20, 0x02, Delta2Captures.MainsPd));
                        }
                    }
                }
            }

            return true;
        };
        return channel;
    }

    private static byte[] Xor(byte[] data, byte with) => [.. data.Select(b => (byte)(b ^ with))];

    private static void Send(FakeChannel channel, byte[] key, byte[] iv, EcoFlowPacket packet)
    {
        var frame = EcoFlowFrame.Build(EcoFlowFrame.TypeProtocol, EcoFlowCrypto.Encrypt(key, iv, packet.ToBytes()));
        // Split like BLE notifications do.
        channel.Notify(frame[..7]);
        channel.Notify(frame[7..]);
    }
}
