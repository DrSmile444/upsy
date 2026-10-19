using Upsy.Core.Settings;
using Upsy.Core.Station;

namespace Upsy.Core.Tests;

public sealed class RegistryTests
{
    private static DiscoveredStation Eb3a(ulong address = 0xA1B2C3D4E5F6) => new(StationCatalog.Eb3a, address, "2240000000001", "EB3A2240000000001", null, -60);

    private static DiscoveredStation Delta2(string serial = "R331EXAMPLE00001", ulong address = 0x01) => new(StationCatalog.Delta2, address, serial, "R33-0001", 84, -60);

    [Fact]
    public void Different_models_are_named_by_model()
    {
        var registry = new StationRegistry([], null);
        var eb3a = registry.Add(Eb3a());
        var delta2 = registry.Add(Delta2());
        Assert.Equal("EB3A", registry.DisplayName(eb3a));
        Assert.Equal("DELTA 2", registry.DisplayName(delta2));
    }

    [Fact]
    public void Same_model_twice_gets_a_serial_suffix()
    {
        var registry = new StationRegistry([], null);
        var a = registry.Add(Delta2("R331EXAMPLE00140", 0x01));
        var b = registry.Add(Delta2("R331EXAMPLE00455", 0x02));
        Assert.Equal("DELTA 2 ·0140", registry.DisplayName(a));
        Assert.Equal("DELTA 2 ·0455", registry.DisplayName(b));
    }

    [Fact]
    public void Adding_a_listed_station_again_keeps_one_entry()
    {
        var registry = new StationRegistry([], null);
        registry.Add(Delta2(address: 0x01));
        registry.Add(Delta2(address: 0x09));
        Assert.Single(registry.Stations);
    }

    [Fact]
    public void Tray_station_is_chosen_remembered_and_falls_back_when_forgotten()
    {
        var registry = new StationRegistry([], null);
        var eb3a = registry.Add(Eb3a());
        var delta2 = registry.Add(Delta2());
        Assert.Equal(eb3a, registry.TrayStation);

        registry.SetTrayStation(delta2.Id);
        Assert.Equal(delta2, registry.TrayStation);
        Assert.Equal([delta2, eb3a], registry.Ordered);

        registry.Forget(delta2.Id);
        Assert.Equal(eb3a, registry.TrayStation);
        Assert.Single(registry.Stations);
    }

    [Fact]
    public void Stations_of_unknown_models_are_dropped_on_load()
    {
        var registry = new StationRegistry([new StationEntry("x", "toaster", 1)], "x");
        Assert.Empty(registry.Stations);
        Assert.Null(registry.TrayStation);
    }
}

public sealed class SettingsTests
{
    private sealed class ReversingProtector : ISecretProtector
    {
        public string Protect(string secret) => "enc:" + new string([.. secret.Reverse()]);

        public string? Unprotect(string protectedSecret) =>
            protectedSecret.StartsWith("enc:", StringComparison.Ordinal) ? new string([.. protectedSecret[4..].Reverse()]) : null;
    }

    [Fact]
    public void Version_1_with_an_eb3a_becomes_one_tray_station()
    {
        var settings = SettingsCodec.Load("""{ "StationAddress": 177789668337142, "LowPercent": 25 }""", out var migrated);
        Assert.True(migrated);
        var entry = Assert.Single(settings.Stations);
        Assert.Equal((StationCatalog.Eb3a.Key, 177789668337142UL), (entry.ProfileKey, entry.Address));
        Assert.Equal(entry.Id, settings.TrayStationId);
        Assert.Equal(25, settings.LowPercent);
    }

    [Fact]
    public void Version_1_without_a_station_keeps_the_threshold()
    {
        var settings = SettingsCodec.Load("""{ "StationAddress": null, "LowPercent": 15 }""", out var migrated);
        Assert.True(migrated);
        Assert.Empty(settings.Stations);
        Assert.Equal(15, settings.LowPercent);
    }

    [Fact]
    public void Version_2_round_trips_without_migration()
    {
        var protector = new ReversingProtector();
        var original = new UpsySettings(
            [new StationEntry("A1B2C3D4E5F6", "eb3a", 0xA1B2C3D4E5F6), new StationEntry("R331EXAMPLE00001", "delta2", 0x01, "R331EXAMPLE00001")],
            "R331EXAMPLE00001",
            20).WithEcoFlowUserId("1234567890123456789", protector);
        var loaded = SettingsCodec.Load(SettingsCodec.Save(original), out var migrated);
        Assert.False(migrated);
        Assert.Equal(original.Stations, loaded.Stations);
        Assert.Equal(original.TrayStationId, loaded.TrayStationId);
        Assert.Equal("1234567890123456789", loaded.EcoFlowUserId(protector));
    }

    [Fact]
    public void The_file_never_holds_the_plain_user_id_and_sign_out_removes_it()
    {
        var protector = new ReversingProtector();
        var signedIn = UpsySettings.Empty.WithEcoFlowUserId("1234567890123456789", protector);
        var json = SettingsCodec.Save(signedIn);
        Assert.DoesNotContain("1234567890123456789", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Profile\"", json, StringComparison.Ordinal);

        var signedOut = SettingsCodec.Load(json, out _).WithEcoFlowUserId(null, protector);
        Assert.Null(signedOut.ProtectedEcoFlowUserId);
        Assert.Null(signedOut.EcoFlowUserId(protector));
    }

    [Fact]
    public void A_user_id_another_account_cannot_decrypt_reads_as_signed_out()
    {
        var settings = UpsySettings.Empty with { ProtectedEcoFlowUserId = "garbage" };
        Assert.Null(settings.EcoFlowUserId(new ReversingProtector()));
    }
}
