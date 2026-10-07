using System.Security.Cryptography;
using System.Text;

namespace HaladeHighSchool.Api.Services;

public sealed class SmartIDTokenKeyProtector
{
    private const string VersionPrefix = "v1:";
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private readonly byte[] _encryptionKey;

    public SmartIDTokenKeyProtector(string applicationKey)
    {
        _encryptionKey = SHA256.HashData(
            Encoding.UTF8.GetBytes($"HaladeHighSchool.SmartID.KeyProtection:{applicationKey}"));
    }

    public string Protect(byte[] key)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var ciphertext = new byte[key.Length];
        var tag = new byte[TagLength];
        using var aes = new AesGcm(_encryptionKey, TagLength);
        aes.Encrypt(nonce, key, ciphertext, tag);

        var protectedBytes = new byte[nonce.Length + tag.Length + ciphertext.Length];
        nonce.CopyTo(protectedBytes, 0);
        tag.CopyTo(protectedBytes, nonce.Length);
        ciphertext.CopyTo(protectedBytes, nonce.Length + tag.Length);
        return VersionPrefix + Convert.ToBase64String(protectedBytes);
    }

    public byte[] Unprotect(string protectedKey)
    {
        if (!protectedKey.StartsWith(VersionPrefix, StringComparison.Ordinal))
        {
            throw new CryptographicException("The Smart ID token key has an unsupported format.");
        }

        var protectedBytes = Convert.FromBase64String(protectedKey[VersionPrefix.Length..]);
        if (protectedBytes.Length <= NonceLength + TagLength)
        {
            throw new CryptographicException("The Smart ID token key is truncated.");
        }

        var nonce = protectedBytes.AsSpan(0, NonceLength);
        var tag = protectedBytes.AsSpan(NonceLength, TagLength);
        var ciphertext = protectedBytes.AsSpan(NonceLength + TagLength);
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(_encryptionKey, TagLength);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        CryptographicOperations.ZeroMemory(protectedBytes);
        return plaintext;
    }
}
