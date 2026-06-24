using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using WinForge.Models;

namespace WinForge.Services;

// =====================================================================================
// 無界滑鼠協定 · Mouse Without Borders wire protocol + AES channel crypto.
//
// 線上格式（每個封包，加密前）· On-wire framing (each packet, before encryption):
//   [1  Type][4 Seq][32 Sender name UTF-8 padded][PayloadLen 4][Payload...]
// 然後成個封包用 AES-CBC 加密，前置隨機 IV，再前置 4-byte 長度框。
// The whole frame is then AES-256-CBC encrypted with a random per-packet IV and length-prefixed.
//
//   stream:  [4 cipherLen][16 IV][cipher...]
//
// 個 AES 金鑰由共用安全密鑰透過 PBKDF2(SHA-256, 100k) 衍生，鹽 = 固定 app 標籤。
// The AES key is derived from the shared security key with PBKDF2 so the human key never
// appears on the wire. Two paired machines that share the same key can talk; nobody else can.
// =====================================================================================

/// <summary>協定常數同序列化 · Protocol constants and (de)serialization.</summary>
public static class MwbProtocol
{
    /// <summary>預設控制埠 · Default TCP control port.</summary>
    public const int DefaultPort = 15100;

    /// <summary>安全密鑰長度（字元）· Security-key length in characters.</summary>
    public const int KeyLength = 16;

    /// <summary>機器名喺封包入面嘅固定欄位長度 · Fixed field width for the sender name.</summary>
    private const int NameFieldLen = 32;

    private static readonly char[] KeyAlphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray(); // no confusing 0/O/1/I/L

    /// <summary>
    /// 產生一個易讀嘅安全密鑰 · Generate a human-readable, crypto-random security key
    /// (16 chars from an unambiguous alphabet).
    /// </summary>
    public static string GenerateKey()
    {
        var sb = new StringBuilder(KeyLength);
        Span<byte> buf = stackalloc byte[KeyLength];
        RandomNumberGenerator.Fill(buf);
        for (int i = 0; i < KeyLength; i++)
            sb.Append(KeyAlphabet[buf[i] % KeyAlphabet.Length]);
        return sb.ToString();
    }

    /// <summary>
    /// 把一個封包序列化成位元組（未加密）· Serialize a packet to its plaintext byte frame.
    /// </summary>
    public static byte[] Serialize(MwbPacket p)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        w.Write((byte)p.Type);
        w.Write(p.Seq);

        var nameBytes = new byte[NameFieldLen];
        var src = Encoding.UTF8.GetBytes(p.SenderName ?? "");
        Array.Copy(src, nameBytes, Math.Min(src.Length, NameFieldLen));
        w.Write(nameBytes);

        var payload = p.Payload ?? Array.Empty<byte>();
        w.Write(payload.Length);
        w.Write(payload);
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>位元組反序列化返封包 · Deserialize a plaintext frame back to a packet.</summary>
    public static MwbPacket Deserialize(byte[] frame)
    {
        using var ms = new MemoryStream(frame);
        using var r = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);
        var p = new MwbPacket
        {
            Type = (MwbPacketType)r.ReadByte(),
            Seq = r.ReadInt32(),
        };
        var nameBytes = r.ReadBytes(NameFieldLen);
        p.SenderName = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0', ' ');
        int len = r.ReadInt32();
        p.Payload = len > 0 ? r.ReadBytes(len) : Array.Empty<byte>();
        return p;
    }

    // ---- typed payload helpers ----------------------------------------------------

    /// <summary>滑鼠資料轉位元組 · Pack a mouse event into the packet payload.</summary>
    public static byte[] PackMouse(MwbMouseData d)
    {
        var b = new byte[16];
        BitConverter.GetBytes(d.X).CopyTo(b, 0);
        BitConverter.GetBytes(d.Y).CopyTo(b, 4);
        BitConverter.GetBytes(d.WheelDelta).CopyTo(b, 8);
        BitConverter.GetBytes(d.Flags).CopyTo(b, 12);
        return b;
    }

    /// <summary>位元組轉滑鼠資料 · Unpack a mouse event from a payload.</summary>
    public static MwbMouseData UnpackMouse(byte[] b) => new()
    {
        X = BitConverter.ToInt32(b, 0),
        Y = BitConverter.ToInt32(b, 4),
        WheelDelta = BitConverter.ToInt32(b, 8),
        Flags = BitConverter.ToInt32(b, 12),
    };

    /// <summary>鍵盤資料轉位元組 · Pack a keyboard event into the payload.</summary>
    public static byte[] PackKey(MwbKeyboardData d)
    {
        var b = new byte[12];
        BitConverter.GetBytes(d.VirtualKey).CopyTo(b, 0);
        BitConverter.GetBytes(d.ScanCode).CopyTo(b, 4);
        BitConverter.GetBytes(d.Flags).CopyTo(b, 8);
        return b;
    }

    /// <summary>位元組轉鍵盤資料 · Unpack a keyboard event.</summary>
    public static MwbKeyboardData UnpackKey(byte[] b) => new()
    {
        VirtualKey = BitConverter.ToInt32(b, 0),
        ScanCode = BitConverter.ToInt32(b, 4),
        Flags = BitConverter.ToInt32(b, 8),
    };

    /// <summary>文字轉 UTF-8 payload · UTF-8 encode a string payload (clipboard / names).</summary>
    public static byte[] PackText(string s) => Encoding.UTF8.GetBytes(s ?? "");

    /// <summary>UTF-8 payload 轉文字 · Decode a UTF-8 string payload.</summary>
    public static string UnpackText(byte[] b) => Encoding.UTF8.GetString(b ?? Array.Empty<byte>());
}

/// <summary>
/// 一個協定封包 · A single protocol packet exchanged over the encrypted channel.
/// </summary>
public sealed class MwbPacket
{
    public MwbPacketType Type { get; set; } = MwbPacketType.Invalid;
    public int Seq { get; set; }
    public string SenderName { get; set; } = "";
    public byte[] Payload { get; set; } = Array.Empty<byte>();

    public MwbPacket() { }

    public MwbPacket(MwbPacketType type, byte[]? payload = null)
    {
        Type = type;
        Payload = payload ?? Array.Empty<byte>();
    }
}

/// <summary>
/// AES-256-CBC 頻道加密 · AES-256-CBC channel cipher keyed by the shared security key (via PBKDF2).
/// Each WriteFrame uses a fresh random IV that is sent in the clear ahead of the ciphertext.
/// </summary>
public sealed class MwbChannelCipher
{
    private readonly byte[] _key; // 32 bytes

    // 固定 app 標籤做鹽 · A fixed app-scoped salt: both peers derive the same key from the same
    // human security key. (A per-connection salt would need an extra handshake round; the human
    // key itself is the secret, and the channel still gets a fresh IV per frame.)
    private static readonly byte[] Salt =
        Encoding.UTF8.GetBytes("WinForge.MouseWithoutBorders.v1");

    private const int Pbkdf2Iterations = 100_000;
    private const int IvLen = 16;

    public MwbChannelCipher(string securityKey)
    {
        _key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(securityKey ?? ""), Salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);
    }

    /// <summary>加密一個明文框 · Encrypt a plaintext frame → [16 IV][cipher...].</summary>
    public byte[] Encrypt(byte[] plain)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();
        var iv = aes.IV;
        using var enc = aes.CreateEncryptor();
        var cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
        var outBuf = new byte[IvLen + cipher.Length];
        Buffer.BlockCopy(iv, 0, outBuf, 0, IvLen);
        Buffer.BlockCopy(cipher, 0, outBuf, IvLen, cipher.Length);
        return outBuf;
    }

    /// <summary>解密 [16 IV][cipher] → 明文 · Decrypt back to the plaintext frame.</summary>
    public byte[] Decrypt(byte[] ivAndCipher)
    {
        if (ivAndCipher.Length < IvLen)
            throw new CryptographicException("Frame too short for IV.");
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        var iv = new byte[IvLen];
        Buffer.BlockCopy(ivAndCipher, 0, iv, 0, IvLen);
        aes.IV = iv;
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(ivAndCipher, IvLen, ivAndCipher.Length - IvLen);
    }
}
