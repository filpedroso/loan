using System.Security.Cryptography;
using System.Text;
using Loans.Domain;
using Microsoft.Extensions.Configuration;

namespace Loans.Infrastructure.Security;

/// <summary>
/// Protects SSNs at rest with two keys: an HMAC-SHA256 hash for lookups (same SSN, same hash; can't be reversed
/// without the key) and an AES-GCM encrypted copy, which only the outbox worker decrypts to send to the CRM.
/// </summary>
public sealed class SsnProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] hashKey;
    private readonly byte[] encryptionKey;

    public SsnProtector(byte[] hashKey, byte[] encryptionKey)
    {
        if (hashKey.Length != 32 || encryptionKey.Length != 32)
            throw new ArgumentException("SSN keys must be 32 bytes.");

        this.hashKey = hashKey;
        this.encryptionKey = encryptionKey;
    }

    /// <summary>Reads the base64 keys from <c>Ssn:HashKey</c> and <c>Ssn:EncryptionKey</c>.</summary>
    public static SsnProtector FromConfiguration(IConfiguration config) =>
        new(Key(config, "Ssn:HashKey"), Key(config, "Ssn:EncryptionKey"));

    public byte[] Hash(Ssn ssn) => HMACSHA256.HashData(hashKey, Encoding.ASCII.GetBytes(ssn.Value));

    /// <summary>Returns nonce | ciphertext | tag. A fresh nonce each time, so equal SSNs encrypt differently.</summary>
    public byte[] Encrypt(Ssn ssn)
    {
        var plaintext = Encoding.ASCII.GetBytes(ssn.Value);
        var result = new byte[NonceSize + plaintext.Length + TagSize];
        var nonce = result.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(encryptionKey, TagSize);
        aes.Encrypt(nonce, plaintext, result.AsSpan(NonceSize, plaintext.Length), result.AsSpan(NonceSize + plaintext.Length));
        return result;
    }

    public Ssn Decrypt(byte[] data)
    {
        var plaintext = new byte[data.Length - NonceSize - TagSize];

        using var aes = new AesGcm(encryptionKey, TagSize);
        aes.Decrypt(
            data.AsSpan(0, NonceSize),
            data.AsSpan(NonceSize, plaintext.Length),
            data.AsSpan(NonceSize + plaintext.Length),
            plaintext);
        return Ssn.Parse(Encoding.ASCII.GetString(plaintext));
    }

    private static byte[] Key(IConfiguration config, string name) =>
        Convert.FromBase64String(config[name] ?? throw new InvalidOperationException($"Missing configuration: {name}."));
}
