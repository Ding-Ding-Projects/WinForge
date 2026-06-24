using System;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 機密加密輔助 · Secrets crypto helper — encrypts a UTF-8 JSON blob with AES-256-GCM under a
/// password-derived key (PBKDF2 / Rfc2898DeriveBytes, SHA-256). The on-disk layout is a single
/// self-describing byte stream so it can be decrypted with nothing but the password:
///
///   [4 magic "WFS1"][1 ver=1][16 salt][12 nonce][16 tag][N ciphertext]
///
/// 鹽同 nonce 都係隨機產生並前置喺密文，password 唔會儲存。
/// The salt and nonce are random and prepended; the password itself is never stored.
/// Wrong password → AuthenticationTagMismatchException, surfaced as a clean bilingual error.
/// </summary>
public static class SecretsCrypto
{
    private static readonly byte[] Magic = { 0x57, 0x46, 0x53, 0x31 }; // "WFS1"
    private const byte FormatVersion = 1;

    private const int SaltLen = 16;
    private const int NonceLen = 12;   // AES-GCM standard nonce
    private const int TagLen = 16;     // AES-GCM 128-bit tag
    private const int KeyLen = 32;     // AES-256
    private const int Pbkdf2Iterations = 210_000; // OWASP-ish for PBKDF2-HMAC-SHA256

    /// <summary>密碼太弱就揾唔到 · Minimum password length we accept in the UI/crypto layer.</summary>
    public const int MinPasswordLength = 4;

    /// <summary>加密純文字成自描述位元組串 · Encrypt plaintext into the self-describing blob above.</summary>
    public static byte[] Encrypt(string plaintext, string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("A password is required to encrypt secrets.", nameof(password));

        var plain = Encoding.UTF8.GetBytes(plaintext ?? string.Empty);

        var salt = RandomNumberGenerator.GetBytes(SaltLen);
        var nonce = RandomNumberGenerator.GetBytes(NonceLen);
        var key = DeriveKey(password, salt);

        var cipher = new byte[plain.Length];
        var tag = new byte[TagLen];
        try
        {
            using var gcm = new AesGcm(key, TagLen);
            gcm.Encrypt(nonce, plain, cipher, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plain);
        }

        var outBuf = new byte[Magic.Length + 1 + SaltLen + NonceLen + TagLen + cipher.Length];
        int o = 0;
        Buffer.BlockCopy(Magic, 0, outBuf, o, Magic.Length); o += Magic.Length;
        outBuf[o++] = FormatVersion;
        Buffer.BlockCopy(salt, 0, outBuf, o, SaltLen); o += SaltLen;
        Buffer.BlockCopy(nonce, 0, outBuf, o, NonceLen); o += NonceLen;
        Buffer.BlockCopy(tag, 0, outBuf, o, TagLen); o += TagLen;
        Buffer.BlockCopy(cipher, 0, outBuf, o, cipher.Length);
        return outBuf;
    }

    /// <summary>
    /// 解密 · Decrypt the blob back to its UTF-8 string. Throws on a wrong password
    /// (AuthenticationTagMismatchException) or a malformed/foreign blob (FormatException).
    /// </summary>
    public static string Decrypt(byte[] blob, string password)
    {
        if (blob is null || blob.Length < Magic.Length + 1 + SaltLen + NonceLen + TagLen)
            throw new FormatException("Secrets blob is too short or malformed.");

        int o = 0;
        for (int i = 0; i < Magic.Length; i++)
            if (blob[o++] != Magic[i])
                throw new FormatException("Not a WinForge secrets blob.");

        byte ver = blob[o++];
        if (ver != FormatVersion)
            throw new FormatException($"Unsupported secrets format version {ver}.");

        var salt = new byte[SaltLen];
        Buffer.BlockCopy(blob, o, salt, 0, SaltLen); o += SaltLen;
        var nonce = new byte[NonceLen];
        Buffer.BlockCopy(blob, o, nonce, 0, NonceLen); o += NonceLen;
        var tag = new byte[TagLen];
        Buffer.BlockCopy(blob, o, tag, 0, TagLen); o += TagLen;

        int cipherLen = blob.Length - o;
        var cipher = new byte[cipherLen];
        Buffer.BlockCopy(blob, o, cipher, 0, cipherLen);

        var key = DeriveKey(password, salt);
        var plain = new byte[cipherLen];
        try
        {
            using var gcm = new AesGcm(key, TagLen);
            gcm.Decrypt(nonce, cipher, tag, plain); // throws AuthenticationTagMismatchException on wrong password
            return Encoding.UTF8.GetString(plain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeyLen);
}
