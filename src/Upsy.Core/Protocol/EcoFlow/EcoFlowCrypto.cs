// EcoFlow BLE session crypto (encryption type 7). Protocol knowledge from rabits/ha-ef-ble (Apache-2.0), see THIRD-PARTY-NOTICES.md.
using System.Security.Cryptography;
using System.Text;

namespace Upsy.Core.Protocol.EcoFlow;

/// <summary>
/// ECDH on secp160r1 with the station. Windows CNG exports named-curve coordinates padded to 21 bytes (a trailing zero),
/// so keys go in and out through explicit curve parameters, which use the 20-byte form the station expects.
/// </summary>
public sealed class EcoFlowKeyExchange : IDisposable
{
    public const int CoordinateLength = 20;
    private readonly ECDiffieHellman _ecdh = ECDiffieHellman.Create(ECCurve.CreateFromFriendlyName("secp160r1"));
    private readonly ECCurve _curve;

    public EcoFlowKeyExchange()
    {
        var parameters = _ecdh.ExportExplicitParameters(includePrivateParameters: false);
        _curve = parameters.Curve;
        PublicKey = [.. Fixed(parameters.Q.X!), .. Fixed(parameters.Q.Y!)];
    }

    /// <summary>X‖Y, 20 bytes each.</summary>
    public byte[] PublicKey { get; }

    /// <summary>The 20-byte X coordinate of the shared point, from the station's X‖Y public key.</summary>
    public byte[] DeriveSharedSecret(ReadOnlySpan<byte> stationPublicKey)
    {
        var half = stationPublicKey.Length / 2;
        var q = new ECPoint { X = Fixed(stationPublicKey[..half].ToArray()), Y = Fixed(stationPublicKey[half..].ToArray()) };
        using var peer = ECDiffieHellman.Create(new ECParameters { Curve = _curve, Q = q });
        return Fixed(_ecdh.DeriveRawSecretAgreement(peer.PublicKey));
    }

    public void Dispose() => _ecdh.Dispose();

    private static byte[] Fixed(byte[] value) =>
        value.Length == CoordinateLength ? value
        : value.Length > CoordinateLength ? value[^CoordinateLength..]
        : [.. new byte[CoordinateLength - value.Length], .. value];
}

#pragma warning disable CA5351 // MD5 is what the station's protocol uses for key derivation and the auth token; no choice here.
public static class EcoFlowCrypto
{
    /// <summary>The key and IV for the session-key request: the first 16 bytes of the shared secret and its MD5.</summary>
    public static (byte[] Key, byte[] Iv) InitialKey(ReadOnlySpan<byte> sharedSecret) =>
        (sharedSecret[..16].ToArray(), MD5.HashData(sharedSecret));

    /// <summary>Offset of the 16 key-table bytes selected by the station's 2-byte seed.</summary>
    public static int KeyTableOffset(ReadOnlySpan<byte> seed) => (seed[0] * 0x10) + (((seed[1] - 1) & 0xFF) * 0x100);

    /// <summary>Session key = MD5(keyTable[offset..+16] ‖ srand[16]).</summary>
    public static byte[] SessionKey(EcoFlowKeyTable table, ReadOnlySpan<byte> seed, ReadOnlySpan<byte> srand)
    {
        ArgumentNullException.ThrowIfNull(table);
        var offset = KeyTableOffset(seed);
        return MD5.HashData([.. table.Slice(offset, 16), .. srand[..16]]);
    }

    /// <summary>The authentication token: upper-case hex of MD5(userId ‖ serial), as ASCII.</summary>
    public static byte[] AuthToken(string userId, string serial) =>
        Encoding.ASCII.GetBytes(Convert.ToHexString(MD5.HashData(Encoding.ASCII.GetBytes(userId + serial))));

    public static byte[] Encrypt(byte[] key, byte[] iv, ReadOnlySpan<byte> plain)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return aes.EncryptCbc(plain, iv, PaddingMode.PKCS7);
    }

    /// <summary>Decrypts whole blocks only (the station drops a trailing partial block) and removes PKCS7 padding when it is valid.</summary>
    public static byte[] Decrypt(byte[] key, byte[] iv, ReadOnlySpan<byte> cipher)
    {
        var aligned = cipher.Length - (cipher.Length % 16);
        if (aligned == 0)
        {
            return cipher.ToArray();
        }

        using var aes = Aes.Create();
        aes.Key = key;
        var plain = aes.DecryptCbc(cipher[..aligned], iv, PaddingMode.None);
        var pad = plain[^1];
        return pad is > 0 and <= 16 && plain.AsSpan(plain.Length - pad).IndexOfAnyExcept(pad) < 0 ? plain[..^pad] : plain;
    }
}

#pragma warning restore CA5351

/// <summary>
/// The constant table EcoFlow's app uses to derive session keys (65,280 bytes). It is EcoFlow's, so it is never
/// committed: tools/get-ecoflow-keydata.ps1 fetches it from a pinned upstream commit and checks this hash.
/// </summary>
public sealed class EcoFlowKeyTable
{
    public const int Length = 65_280;
    public const string Sha256 = "3d1ba59b40c46bc86ba35fa231a3db196b448bbdfb1635c88ddd42f24abd4b2a";
    private readonly byte[] _bytes;

    private EcoFlowKeyTable(byte[] bytes) => _bytes = bytes;

    /// <summary>Validates size and hash; null when the bytes are not the expected table.</summary>
    public static EcoFlowKeyTable? FromBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return bytes.Length == Length && Convert.ToHexStringLower(SHA256.HashData(bytes)) == Sha256 ? new EcoFlowKeyTable(bytes) : null;
    }

    /// <summary>Accepts any table of the right size; tests use synthetic tables because the real one is not in the repo.</summary>
    internal static EcoFlowKeyTable ForTests(byte[] bytes) => new(bytes);

    internal ReadOnlySpan<byte> Slice(int offset, int length) => _bytes.AsSpan(offset, length);
}

/// <summary>Manufacturer data of an EcoFlow advertisement (company id 0xB5B5, version 0x13 layout).</summary>
/// <param name="EncryptType">0 plain, 1 serial-derived key, 7 ECDH session.</param>
public sealed record EcoFlowAdvertisement(string Serial, int? BatteryPercent, int EncryptType)
{
    public const ushort ManufacturerId = 0xB5B5;

    /// <summary>[0] version, [1..16] ASCII serial, [17] battery %, [22] flags with the encryption type in bits 3..5.</summary>
    public static EcoFlowAdvertisement? TryParse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 23 || data[0] != 0x13)
        {
            return null;
        }

        var serial = Encoding.ASCII.GetString(data[1..17]);
        if (serial.Any(c => c is < '0' or > 'Z' || c is > '9' and < 'A'))
        {
            return null;
        }

        var percent = data[17] <= 100 ? data[17] : (int?)null;
        return new EcoFlowAdvertisement(serial, percent, (data[22] & 0b0011_1000) >> 3);
    }
}
