using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace WinForge.Services;

/// <summary>
/// 原生 KDBX 引擎（KDBX 3.1 / 4）· Native KDBX engine (KeePass 2.x .kdbx, versions 3.1 and 4).
///
/// 純 C#／.NET — 完全唔會啟動、外呼或者綁住 KeePass.exe 或任何外部程式。
/// Pure managed C#/.NET — never launches, shells out to, or bundles KeePass.exe or any external program.
///
/// 支援 · Supported:
///   • 容器 · Outer container: KDBX 3.1 (sig2 0x00030001) read/write, KDBX 4.x (0x00040000/0x00040001) read/write.
///   • 對稱加密 · Ciphers: AES-256-CBC, ChaCha20 (RFC 7539 / IETF 96-bit nonce + 32-bit counter).
///   • 金鑰衍生 · KDF: AES-KDF (rounds) for v3; AES-KDF or Argon2d (Konscious) for v4.
///   • 完整性 · Integrity (v4): HMAC-SHA-256 header HMAC + per-block HMAC stream.
///   • 內部串流保護 · Inner random stream for protected fields: Salsa20 (v3) and ChaCha20 (v4).
///   • 內部 XML gzip 壓縮 · gzip-compressed inner XML payload.
///   • 主鑰 · Composite key from master password and/or key file (XML/v2 hash, 32-byte raw, or hex/SHA-256 fallback).
///
/// 仰賴嘅受管理程式庫 · Managed dependency: Konscious.Security.Cryptography.Argon2 (pure managed, MIT) for Argon2d,
/// because Argon2 is not in the BCL. AES/ChaCha20/SHA/HMAC all come from System.Security.Cryptography.
/// </summary>
public sealed class KeePassDatabase
{
    // ── KDBX magic ───────────────────────────────────────────────────────────
    private const uint Sig1 = 0x9AA2D903;
    private const uint Sig2 = 0xB54BFB67;

    // Outer header field ids.
    private const byte HEnd = 0, HComment = 1, HCipherId = 2, HCompression = 3,
        HMasterSeed = 4, HTransformSeed = 5, HTransformRounds = 6, HEncryptionIv = 7,
        HInnerRandomStreamKey = 8, HStreamStartBytes = 9, HInnerRandomStreamId = 10, HKdfParameters = 11;

    // Inner header field ids (v4).
    private const byte IEnd = 0, IInnerRandomStreamId = 1, IInnerRandomStreamKey = 2, IBinary = 3;

    // Cipher UUIDs.
    private static readonly Guid AesCipherId = new(new byte[]
        { 0x31, 0xC1, 0xF2, 0xE6, 0xBF, 0x71, 0x43, 0x50, 0xBE, 0x58, 0x05, 0x21, 0x6A, 0xFC, 0x5A, 0xFF });
    private static readonly Guid ChaCha20CipherId = new(new byte[]
        { 0xD6, 0x03, 0x8A, 0x2B, 0x8B, 0x6F, 0x4C, 0xB5, 0xA5, 0x24, 0x33, 0x9A, 0x31, 0xDB, 0xB5, 0x9A });

    // KDF UUIDs.
    private static readonly Guid AesKdf = new(new byte[]
        { 0xC9, 0xD9, 0xF3, 0x9A, 0x62, 0x8A, 0x44, 0x60, 0xBF, 0x74, 0x0D, 0x08, 0xC1, 0x8A, 0x4F, 0xEA });
    private static readonly Guid Argon2dKdf = new(new byte[]
        { 0xEF, 0x63, 0x6D, 0xDF, 0x8C, 0x29, 0x44, 0x4B, 0x91, 0xF7, 0xA9, 0xA4, 0x03, 0xE3, 0x0A, 0x0C });
    private static readonly Guid Argon2idKdf = new(new byte[]
        { 0x9E, 0x29, 0x8B, 0x19, 0x56, 0xDB, 0x47, 0x73, 0xB2, 0x3D, 0xFC, 0x3E, 0xC6, 0xF0, 0xA1, 0xE6 });

    // ── State ────────────────────────────────────────────────────────────────
    public KpGroup Root { get; private set; } = new() { Name = "Root" };
    public string? FilePath { get; set; }
    public uint Major { get; private set; } = 4;
    public uint Minor { get; private set; }

    // Re-used header material so a save can reproduce the same crypto choices.
    private Guid _cipher = ChaCha20CipherId;
    private uint _compression = 1;             // 0 = none, 1 = gzip
    private Guid _kdfUuid = Argon2dKdf;
    private ulong _aesKdfRounds = 60000;
    private ulong _argonMem = 64 * 1024 * 1024; // bytes
    private ulong _argonIters = 2;
    private uint _argonParallel = 2;
    private uint _argonVersion = 0x13;
    private int _innerStreamId = 3;            // 2 = Salsa20, 3 = ChaCha20

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>建立一個新嘅空白資料庫（KDBX 4 + ChaCha20 + Argon2d）· Create a fresh empty KDBX 4 database.</summary>
    public static KeePassDatabase CreateNew(string dbName)
    {
        var db = new KeePassDatabase
        {
            Major = 4,
            Minor = 1,
            _cipher = ChaCha20CipherId,
            _kdfUuid = Argon2dKdf,
            _innerStreamId = 3,
        };
        db.Root = new KpGroup { Name = string.IsNullOrWhiteSpace(dbName) ? "Database" : dbName.Trim() };
        db.Root.Groups.Add(new KpGroup { Name = "General" });
        return db;
    }

    /// <summary>由位元組開啟 · Open a KDBX database from bytes with a composite key.</summary>
    public static KeePassDatabase Load(byte[] data, string? password, byte[]? keyFileBytes)
    {
        var db = new KeePassDatabase();
        db.ReadInternal(data, BuildCompositeKey(password, keyFileBytes));
        return db;
    }

    /// <summary>序列化做 .kdbx 位元組 · Serialize the database to .kdbx bytes.</summary>
    public byte[] Save(string? password, byte[]? keyFileBytes)
        => WriteInternal(BuildCompositeKey(password, keyFileBytes));

    // ── Composite key (password + key file) ─────────────────────────────────────

    /// <summary>
    /// 合成主鑰 · Build the 32-byte composite key: SHA-256( SHA-256(pw) || keyFileKey ).
    /// 空白密碼但有鎖匙檔嗰陣只用鎖匙檔嘅部分 · If only a key file is given, only its part is hashed.
    /// </summary>
    private static byte[] BuildCompositeKey(string? password, byte[]? keyFileBytes)
    {
        using var sha = SHA256.Create();
        var parts = new List<byte[]>();
        if (!string.IsNullOrEmpty(password))
            parts.Add(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
        if (keyFileBytes is { Length: > 0 })
            parts.Add(ParseKeyFile(keyFileBytes));
        if (parts.Count == 0)
            throw new InvalidOperationException("A master password or key file is required · 需要主密碼或者鎖匙檔");
        var concat = parts.SelectMany(p => p).ToArray();
        return SHA256.HashData(concat);
    }

    /// <summary>解析鎖匙檔（KeePass XML / 32-byte / 64-hex / 任意檔案 SHA-256）· Parse a key file to its 32-byte key.</summary>
    public static byte[] ParseKeyFile(byte[] bytes)
    {
        // 1) KeePass 2.x XML key file.
        try
        {
            var text = Encoding.UTF8.GetString(bytes).TrimStart('﻿', ' ', '\r', '\n', '\t');
            if (text.StartsWith("<?xml") || text.Contains("<KeyFile"))
            {
                var doc = XDocument.Parse(text);
                var keyEl = doc.Root?.Element("Key");
                var dataEl = keyEl?.Element("Data");
                if (dataEl is not null)
                {
                    var verAttr = (string?)doc.Root?.Element("Meta")?.Element("Version");
                    var hashAttr = (string?)dataEl.Attribute("Hash");
                    var raw = Convert.FromBase64String(dataEl.Value.Trim());
                    // v2 stores hex with a 4-byte hash; v1 stores raw base64 of the 32-byte key.
                    if (verAttr is not null && verAttr.StartsWith("2"))
                    {
                        var hex = dataEl.Value.Trim().Replace("\n", "").Replace("\r", "").Replace(" ", "");
                        var key = FromHex(hex);
                        return key.Length == 32 ? key : SHA256.HashData(key);
                    }
                    return raw.Length == 32 ? raw : SHA256.HashData(raw);
                }
            }
        }
        catch { /* fall through to binary handling */ }

        // 2) Exactly 32 raw bytes → use as-is.
        if (bytes.Length == 32) return (byte[])bytes.Clone();
        // 3) Exactly 64 hex chars → decode.
        if (bytes.Length == 64 && IsHex(bytes))
            return FromHex(Encoding.ASCII.GetString(bytes));
        // 4) Anything else → SHA-256 of the whole file.
        return SHA256.HashData(bytes);
    }

    private static bool IsHex(byte[] b) => b.All(c =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));

    private static byte[] FromHex(string hex)
    {
        hex = hex.Trim();
        if (hex.Length % 2 != 0) return SHA256.HashData(Encoding.UTF8.GetBytes(hex));
        var b = new byte[hex.Length / 2];
        for (int i = 0; i < b.Length; i++)
            b[i] = byte.Parse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return b;
    }

    // ── READ ─────────────────────────────────────────────────────────────────

    private void ReadInternal(byte[] data, byte[] compositeKey)
    {
        using var ms = new MemoryStream(data, writable: false);
        using var br = new BinaryReader(ms);

        if (br.ReadUInt32() != Sig1 || br.ReadUInt32() != Sig2)
            throw new InvalidDataException("Not a KDBX file · 唔係 KDBX 檔");
        Minor = br.ReadUInt16();
        Major = br.ReadUInt16();
        if (Major is not (3 or 4))
            throw new InvalidDataException($"Unsupported KDBX version {Major}.{Minor} · 唔支援嘅版本");

        // Outer header (TLV). v3 uses 16-bit lengths; v4 uses 32-bit lengths.
        byte[]? masterSeed = null, transformSeed = null, encryptionIv = null,
            innerKey = null, streamStart = null, kdfParams = null;
        ulong transformRounds = 0;
        long headerStart = ms.Position;

        while (true)
        {
            byte id = br.ReadByte();
            int len = Major >= 4 ? br.ReadInt32() : br.ReadUInt16();
            byte[] buf = br.ReadBytes(len);
            if (id == HEnd) break;
            switch (id)
            {
                case HCipherId: _cipher = new Guid(buf); break;
                case HCompression: _compression = BitConverter.ToUInt32(buf, 0); break;
                case HMasterSeed: masterSeed = buf; break;
                case HTransformSeed: transformSeed = buf; break;
                case HTransformRounds: transformRounds = BitConverter.ToUInt64(buf, 0); break;
                case HEncryptionIv: encryptionIv = buf; break;
                case HInnerRandomStreamKey: innerKey = buf; break;        // v3 only
                case HStreamStartBytes: streamStart = buf; break;          // v3 only
                case HInnerRandomStreamId: _innerStreamId = BitConverter.ToInt32(buf, 0); break; // v3 only
                case HKdfParameters: kdfParams = buf; break;               // v4 only
            }
        }
        long headerEnd = ms.Position;
        byte[] headerBytes = data[(int)headerStart..(int)headerEnd];
        // Include the 12-byte magic + version in the header range for v4 HMAC/SHA over the full header.
        byte[] fullHeader = data[0..(int)headerEnd];

        if (masterSeed is null || encryptionIv is null)
            throw new InvalidDataException("Corrupt KDBX header · KDBX 標頭損壞");

        // Resolve KDF parameters.
        if (Major >= 4 && kdfParams is not null) ParseKdfParameters(kdfParams);
        else { _kdfUuid = AesKdf; _aesKdfRounds = transformRounds; }

        byte[] transformedKey = DeriveTransformedKey(compositeKey, transformSeed, masterSeed);
        // Final encryption key = SHA-256( masterSeed || transformedKey ).
        byte[] finalKey = SHA256.HashData(masterSeed.Concat(transformedKey).ToArray());
        // HMAC base key (v4) = SHA-512( masterSeed || transformedKey || 0x01 ).
        byte[] hmacBaseKey = SHA512.HashData(masterSeed.Concat(transformedKey).Concat(new byte[] { 0x01 }).ToArray());

        byte[] payload;
        if (Major >= 4)
        {
            // v4: after the header come the 32-byte header SHA-256, the 32-byte header HMAC, then HMAC blocks.
            ms.Position = headerEnd;
            byte[] storedHeaderSha = br.ReadBytes(32);
            if (!CryptographicOperations.FixedTimeEquals(storedHeaderSha, SHA256.HashData(fullHeader)))
                throw new InvalidDataException("Header checksum mismatch (file corrupt) · 標頭檢查碼不符");
            byte[] storedHeaderHmac = br.ReadBytes(32);
            byte[] hmacKeyHeader = HmacBlockKey(ulong.MaxValue, hmacBaseKey);
            using (var h = new HMACSHA256(hmacKeyHeader))
            {
                var calc = h.ComputeHash(fullHeader);
                if (!CryptographicOperations.FixedTimeEquals(storedHeaderHmac, calc))
                    throw new InvalidOperationException("Wrong master password or key file · 主密碼或鎖匙檔錯誤");
            }
            byte[] cipherText = ReadHmacBlocks(br, hmacBaseKey);
            payload = Decrypt(cipherText, finalKey, encryptionIv);
        }
        else
        {
            // v3: the remaining bytes are the ciphertext; after decryption the first 32 bytes must equal streamStart.
            ms.Position = headerEnd;
            byte[] cipherText = br.ReadBytes((int)(ms.Length - ms.Position));
            byte[] plain = Decrypt(cipherText, finalKey, encryptionIv);
            if (streamStart is null || plain.Length < 32 ||
                !CryptographicOperations.FixedTimeEquals(plain[..32], streamStart))
                throw new InvalidOperationException("Wrong master password or key file · 主密碼或鎖匙檔錯誤");
            payload = HashedBlocksToPlain(plain[32..]);
        }

        // Decompress.
        byte[] xmlBytes = _compression == 1 ? GunzipBytes(payload) : payload;

        // v4 inner header precedes the XML.
        byte[] v4InnerKey = innerKey ?? Array.Empty<byte>();
        int innerStreamId = _innerStreamId;
        if (Major >= 4)
        {
            int pos = 0;
            while (true)
            {
                byte id = xmlBytes[pos++];
                int len = BitConverter.ToInt32(xmlBytes, pos); pos += 4;
                var buf = xmlBytes[pos..(pos + len)]; pos += len;
                if (id == IEnd) break;
                if (id == IInnerRandomStreamId) innerStreamId = BitConverter.ToInt32(buf, 0);
                else if (id == IInnerRandomStreamKey) v4InnerKey = buf;
                // IBinary attachments are accepted but not surfaced in this UI.
            }
            xmlBytes = xmlBytes[pos..];
            _innerStreamId = innerStreamId;
        }

        var protect = CreateInnerStream(innerStreamId, v4InnerKey);
        Root = ParseXml(Encoding.UTF8.GetString(xmlBytes), protect);
    }

    private byte[] DeriveTransformedKey(byte[] compositeKey, byte[]? transformSeed, byte[] masterSeed)
    {
        if (_kdfUuid == AesKdf)
        {
            // v4 carries the AES-KDF seed in its VariantDictionary ("S"); v3 uses the header transform-seed field.
            byte[]? seed = _aesKdfSeed ?? transformSeed;
            if (seed is null) throw new InvalidDataException("Missing transform seed · 缺少轉換種子");
            return AesKdfTransform(compositeKey, seed, _aesKdfRounds);
        }
        if (_kdfUuid == Argon2dKdf || _kdfUuid == Argon2idKdf)
            return Argon2Transform(compositeKey);
        throw new NotSupportedException("Unsupported KDF · 唔支援嘅金鑰衍生函數");
    }

    // ── WRITE ──────────────────────────────────────────────────────────────────

    private byte[] WriteInternal(byte[] compositeKey)
    {
        var rng = RandomNumberGenerator.Create();
        byte[] masterSeed = RandBytes(rng, 32);
        byte[] encryptionIv = RandBytes(rng, _cipher == ChaCha20CipherId ? 12 : 16);
        byte[] transformSeed = RandBytes(rng, 32);
        byte[] innerKey = RandBytes(rng, _cipher == ChaCha20CipherId ? 64 : 32);
        if (Major < 4 && _innerStreamId == 2) innerKey = RandBytes(rng, 32); // Salsa20 takes 32-byte key seed

        // Derive keys (mirror of read). For Argon2 the random transform seed doubles as the salt.
        byte[] transformedKey;
        if (_kdfUuid == AesKdf) transformedKey = AesKdfTransform(compositeKey, transformSeed, _aesKdfRounds);
        else { _argonVersion = 0x13; _argonSalt = transformSeed; transformedKey = Argon2Transform(compositeKey); }

        byte[] finalKey = SHA256.HashData(masterSeed.Concat(transformedKey).ToArray());
        byte[] hmacBaseKey = SHA512.HashData(masterSeed.Concat(transformedKey).Concat(new byte[] { 0x01 }).ToArray());

        // Build the XML body.
        var protect = CreateInnerStream(_innerStreamId, innerKey);
        string xml = BuildXml(protect);
        byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);

        byte[] innerPayload;
        if (Major >= 4)
        {
            using var inner = new MemoryStream();
            WriteInnerHeaderField(inner, IInnerRandomStreamId, BitConverter.GetBytes(_innerStreamId));
            WriteInnerHeaderField(inner, IInnerRandomStreamKey, innerKey);
            WriteInnerHeaderField(inner, IEnd, Array.Empty<byte>());
            inner.Write(xmlBytes);
            innerPayload = inner.ToArray();
        }
        else innerPayload = xmlBytes;

        byte[] compressed = _compression == 1 ? GzipBytes(innerPayload) : innerPayload;

        // Outer header.
        byte[] kdfParams = _kdfUuid == AesKdf ? BuildAesKdfParams(transformSeed) : BuildArgonParams(transformSeed);
        using var outMs = new MemoryStream();
        using (var bw = new BinaryWriter(outMs, Encoding.UTF8, leaveOpen: true))
        {
            bw.Write(Sig1); bw.Write(Sig2);
            bw.Write((ushort)(Major >= 4 ? 1u : 1u)); // minor
            bw.Write((ushort)Major);

            void Field(byte id, byte[] v)
            {
                bw.Write(id);
                if (Major >= 4) bw.Write(v.Length); else bw.Write((ushort)v.Length);
                bw.Write(v);
            }

            Field(HCipherId, _cipher.ToByteArray());
            Field(HCompression, BitConverter.GetBytes(_compression));
            Field(HMasterSeed, masterSeed);
            if (Major >= 4) Field(HKdfParameters, kdfParams);
            else { Field(HTransformSeed, transformSeed); Field(HTransformRounds, BitConverter.GetBytes(_aesKdfRounds)); }
            Field(HEncryptionIv, encryptionIv);
            if (Major < 4)
            {
                Field(HInnerRandomStreamKey, innerKey);
                byte[] streamStart = RandBytes(rng, 32);
                Field(HStreamStartBytes, streamStart);
                Field(HInnerRandomStreamId, BitConverter.GetBytes(_innerStreamId));
                Field(HEnd, new byte[] { 0x0D, 0x0A, 0x0D, 0x0A });
                bw.Flush();
                byte[] header = outMs.ToArray();

                // v3 payload = streamStart || hashedBlocks(compressed); then encrypt whole thing.
                byte[] plain = streamStart.Concat(PlainToHashedBlocks(compressed)).ToArray();
                byte[] cipherText = Encrypt(plain, finalKey, encryptionIv);
                using var final3 = new MemoryStream();
                final3.Write(header);
                final3.Write(cipherText);
                return final3.ToArray();
            }
            else
            {
                Field(HEnd, new byte[] { 0x0D, 0x0A, 0x0D, 0x0A });
            }
        }

        // v4 tail: header SHA-256 + header HMAC + HMAC blocks of encrypted payload.
        byte[] fullHeader = outMs.ToArray();
        byte[] cipher = Encrypt(compressed, finalKey, encryptionIv);

        using var final = new MemoryStream();
        final.Write(fullHeader);
        final.Write(SHA256.HashData(fullHeader));
        byte[] hmacKeyHeader = HmacBlockKey(ulong.MaxValue, hmacBaseKey);
        using (var h = new HMACSHA256(hmacKeyHeader))
            final.Write(h.ComputeHash(fullHeader));
        WriteHmacBlocks(final, cipher, hmacBaseKey);
        return final.ToArray();
    }

    // ── KDF parameter (de)serialization (VariantDictionary) ─────────────────────

    private void ParseKdfParameters(byte[] data)
    {
        var dict = VdRead(data);
        if (dict.TryGetValue("$UUID", out var u) && u is byte[] uuid && uuid.Length == 16)
            _kdfUuid = new Guid(uuid);
        if (_kdfUuid == AesKdf)
        {
            if (dict.TryGetValue("R", out var r)) _aesKdfRounds = Convert.ToUInt64(r);
            // 'S' (seed) is the transform seed; carry it on a side channel.
            if (dict.TryGetValue("S", out var s) && s is byte[] seed) _aesKdfSeed = seed;
        }
        else // Argon2
        {
            if (dict.TryGetValue("S", out var s) && s is byte[] seed) _argonSalt = seed;
            if (dict.TryGetValue("M", out var m)) _argonMem = Convert.ToUInt64(m);
            if (dict.TryGetValue("I", out var i)) _argonIters = Convert.ToUInt64(i);
            if (dict.TryGetValue("P", out var p)) _argonParallel = Convert.ToUInt32(p);
            if (dict.TryGetValue("V", out var v)) _argonVersion = Convert.ToUInt32(v);
        }
    }

    private byte[]? _aesKdfSeed;
    private byte[]? _argonSalt;

    private byte[] BuildAesKdfParams(byte[] transformSeed)
    {
        var d = new List<(string, byte, object)>
        {
            ("$UUID", (byte)0x42, AesKdf.ToByteArray()),
            ("R", (byte)0x05, _aesKdfRounds),
            ("S", (byte)0x42, transformSeed),
        };
        return VdWrite(d);
    }

    private byte[] BuildArgonParams(byte[] salt)
    {
        _argonSalt = salt;
        var d = new List<(string, byte, object)>
        {
            ("$UUID", (byte)0x42, _kdfUuid.ToByteArray()),
            ("S", (byte)0x42, salt),
            ("V", (byte)0x04, _argonVersion),
            ("M", (byte)0x05, _argonMem),
            ("I", (byte)0x05, _argonIters),
            ("P", (byte)0x04, _argonParallel),
        };
        return VdWrite(d);
    }

    // VariantDictionary (KeePass): u16 version, then [type:u8][nameLen:i32][name][valLen:i32][val], terminated by type 0.
    private static Dictionary<string, object> VdRead(byte[] data)
    {
        var dict = new Dictionary<string, object>();
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);
        br.ReadUInt16(); // version
        while (true)
        {
            byte type = br.ReadByte();
            if (type == 0) break;
            int nameLen = br.ReadInt32();
            string name = Encoding.UTF8.GetString(br.ReadBytes(nameLen));
            int valLen = br.ReadInt32();
            byte[] val = br.ReadBytes(valLen);
            object boxed = type switch
            {
                0x04 => BitConverter.ToUInt32(val, 0),
                0x05 => BitConverter.ToUInt64(val, 0),
                0x08 => val[0] != 0,
                0x0C => BitConverter.ToInt32(val, 0),
                0x0D => BitConverter.ToInt64(val, 0),
                0x18 => Encoding.UTF8.GetString(val),
                0x42 => val,
                _ => val,
            };
            dict[name] = boxed;
        }
        return dict;
    }

    private static byte[] VdWrite(List<(string name, byte type, object val)> items)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write((ushort)0x0100);
        foreach (var (name, type, val) in items)
        {
            bw.Write(type);
            var nameBytes = Encoding.UTF8.GetBytes(name);
            bw.Write(nameBytes.Length);
            bw.Write(nameBytes);
            byte[] vb = type switch
            {
                0x04 => BitConverter.GetBytes(Convert.ToUInt32(val)),
                0x05 => BitConverter.GetBytes(Convert.ToUInt64(val)),
                0x08 => new[] { (byte)((bool)val ? 1 : 0) },
                0x0C => BitConverter.GetBytes(Convert.ToInt32(val)),
                0x0D => BitConverter.GetBytes(Convert.ToInt64(val)),
                0x18 => Encoding.UTF8.GetBytes((string)val),
                _ => (byte[])val,
            };
            bw.Write(vb.Length);
            bw.Write(vb);
        }
        bw.Write((byte)0);
        return ms.ToArray();
    }

    // ── Key derivation primitives ────────────────────────────────────────────

    private byte[] AesKdfTransform(byte[] compositeKey, byte[] seed, ulong rounds)
    {
        // ECB-encrypt the 32-byte composite key 'rounds' times with the seed as key, then SHA-256.
        byte[] key = (byte[])compositeKey.Clone();
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.KeySize = 256;
        aes.Key = seed;
        using var enc = aes.CreateEncryptor();
        var block = new byte[32];
        for (ulong r = 0; r < rounds; r++)
        {
            enc.TransformBlock(key, 0, 16, block, 0);
            enc.TransformBlock(key, 16, 16, block, 16);
            Array.Copy(block, key, 32);
        }
        return SHA256.HashData(key);
    }

    private byte[] Argon2Transform(byte[] compositeKey)
    {
        byte[] salt = _argonSalt ?? throw new InvalidDataException("Missing Argon2 salt · 缺少 Argon2 鹽值");
        // Konscious.Security.Cryptography — pure managed Argon2d.
        using var argon = new Konscious.Security.Cryptography.Argon2d(compositeKey)
        {
            Salt = salt,
            DegreeOfParallelism = (int)_argonParallel,
            MemorySize = (int)(_argonMem / 1024), // KiB
            Iterations = (int)_argonIters,
        };
        return argon.GetBytes(32);
    }

    // ── Cipher (AES-256-CBC / ChaCha20) ──────────────────────────────────────

    private byte[] Decrypt(byte[] cipherText, byte[] key, byte[] iv)
    {
        if (_cipher == AesCipherId)
        {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key; aes.IV = iv;
            using var dec = aes.CreateDecryptor();
            return dec.TransformFinalBlock(cipherText, 0, cipherText.Length);
        }
        if (_cipher == ChaCha20CipherId)
            return ChaCha20.Crypt(key, iv, cipherText);
        throw new NotSupportedException("Unsupported cipher · 唔支援嘅加密法");
    }

    private byte[] Encrypt(byte[] plain, byte[] key, byte[] iv)
    {
        if (_cipher == AesCipherId)
        {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key; aes.IV = iv;
            using var enc = aes.CreateEncryptor();
            return enc.TransformFinalBlock(plain, 0, plain.Length);
        }
        if (_cipher == ChaCha20CipherId)
            return ChaCha20.Crypt(key, iv, plain);
        throw new NotSupportedException("Unsupported cipher · 唔支援嘅加密法");
    }

    // ── v4 HMAC block stream ─────────────────────────────────────────────────

    private static byte[] HmacBlockKey(ulong index, byte[] baseKey)
        => SHA512.HashData(BitConverter.GetBytes(index).Concat(baseKey).ToArray());

    private static byte[] ReadHmacBlocks(BinaryReader br, byte[] baseKey)
    {
        using var outMs = new MemoryStream();
        ulong index = 0;
        while (true)
        {
            byte[] storedHmac = br.ReadBytes(32);
            int len = br.ReadInt32();
            byte[] block = len > 0 ? br.ReadBytes(len) : Array.Empty<byte>();
            byte[] blockKey = HmacBlockKey(index, baseKey);
            using (var h = new HMACSHA256(blockKey))
            {
                // HMAC content: index(8) || len(4) || block.
                var content = BitConverter.GetBytes(index)
                    .Concat(BitConverter.GetBytes(len)).Concat(block).ToArray();
                var calc = h.ComputeHash(content);
                if (!CryptographicOperations.FixedTimeEquals(storedHmac, calc))
                    throw new InvalidDataException("Block HMAC mismatch (file corrupt) · 區塊 HMAC 不符");
            }
            if (len == 0) break;
            outMs.Write(block);
            index++;
        }
        return outMs.ToArray();
    }

    private static void WriteHmacBlocks(Stream outStream, byte[] data, byte[] baseKey)
    {
        const int blockSize = 1024 * 1024;
        ulong index = 0;
        int offset = 0;
        while (offset < data.Length)
        {
            int len = Math.Min(blockSize, data.Length - offset);
            byte[] block = data[offset..(offset + len)];
            byte[] blockKey = HmacBlockKey(index, baseKey);
            using var h = new HMACSHA256(blockKey);
            var content = BitConverter.GetBytes(index)
                .Concat(BitConverter.GetBytes(len)).Concat(block).ToArray();
            outStream.Write(h.ComputeHash(content));
            outStream.Write(BitConverter.GetBytes(len));
            outStream.Write(block);
            offset += len;
            index++;
        }
        // Terminating empty block.
        byte[] termKey = HmacBlockKey(index, baseKey);
        using (var h = new HMACSHA256(termKey))
        {
            var content = BitConverter.GetBytes(index).Concat(BitConverter.GetBytes(0)).ToArray();
            outStream.Write(h.ComputeHash(content));
        }
        outStream.Write(BitConverter.GetBytes(0));
    }

    // ── v3 hashed-block stream ───────────────────────────────────────────────

    private static byte[] HashedBlocksToPlain(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);
        using var outMs = new MemoryStream();
        while (true)
        {
            br.ReadUInt32(); // block index
            byte[] hash = br.ReadBytes(32);
            int len = br.ReadInt32();
            if (len == 0) break;
            byte[] block = br.ReadBytes(len);
            if (!CryptographicOperations.FixedTimeEquals(hash, SHA256.HashData(block)))
                throw new InvalidDataException("Hashed block mismatch · 雜湊區塊不符");
            outMs.Write(block);
        }
        return outMs.ToArray();
    }

    private static byte[] PlainToHashedBlocks(byte[] data)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        const int blockSize = 1024 * 1024;
        uint index = 0;
        int offset = 0;
        while (offset < data.Length)
        {
            int len = Math.Min(blockSize, data.Length - offset);
            byte[] block = data[offset..(offset + len)];
            bw.Write(index);
            bw.Write(SHA256.HashData(block));
            bw.Write(len);
            bw.Write(block);
            offset += len; index++;
        }
        bw.Write(index);
        bw.Write(new byte[32]);
        bw.Write(0);
        return ms.ToArray();
    }

    // ── Inner random stream (protected fields) ───────────────────────────────

    private static IInnerStream CreateInnerStream(int id, byte[] key) => id switch
    {
        2 => new Salsa20Stream(SHA256.HashData(key)),
        3 => new ChaCha20Stream(key),
        _ => new NullStream(),
    };

    // ── Compression ──────────────────────────────────────────────────────────

    private static byte[] GunzipBytes(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        using var outMs = new MemoryStream();
        gz.CopyTo(outMs);
        return outMs.ToArray();
    }

    private static byte[] GzipBytes(byte[] data)
    {
        using var outMs = new MemoryStream();
        using (var gz = new GZipStream(outMs, CompressionLevel.Optimal, leaveOpen: true))
            gz.Write(data, 0, data.Length);
        return outMs.ToArray();
    }

    private static byte[] RandBytes(RandomNumberGenerator rng, int n)
    {
        var b = new byte[n];
        rng.GetBytes(b);
        return b;
    }

    // ── XML parse / build ────────────────────────────────────────────────────

    private static void WriteInnerHeaderField(Stream s, byte id, byte[] data)
    {
        s.WriteByte(id);
        s.Write(BitConverter.GetBytes(data.Length));
        s.Write(data);
    }

    private KpGroup ParseXml(string xml, IInnerStream protect)
    {
        var doc = XDocument.Parse(xml);
        var rootEl = doc.Root?.Element("Root")?.Element("Group");
        if (rootEl is null) return new KpGroup { Name = "Root" };
        return ParseGroup(rootEl, protect);
    }

    private KpGroup ParseGroup(XElement el, IInnerStream protect)
    {
        var g = new KpGroup
        {
            Name = (string?)el.Element("Name") ?? "",
            Uuid = (string?)el.Element("UUID") ?? Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            Notes = (string?)el.Element("Notes") ?? "",
        };
        foreach (var sub in el.Elements("Group"))
            g.Groups.Add(ParseGroup(sub, protect));
        foreach (var entryEl in el.Elements("Entry"))
            g.Entries.Add(ParseEntry(entryEl, protect));
        return g;
    }

    private KpEntry ParseEntry(XElement el, IInnerStream protect)
    {
        var e = new KpEntry
        {
            Uuid = (string?)el.Element("UUID") ?? Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
        };
        foreach (var str in el.Elements("String"))
        {
            string key = (string?)str.Element("Key") ?? "";
            var valEl = str.Element("Value");
            string value = valEl?.Value ?? "";
            bool prot = string.Equals((string?)valEl?.Attribute("Protected"), "True", StringComparison.OrdinalIgnoreCase);
            if (prot && value.Length > 0)
            {
                try { value = Encoding.UTF8.GetString(protect.Decrypt(Convert.FromBase64String(value))); }
                catch { /* leave as-is on failure */ }
            }
            switch (key)
            {
                case "Title": e.Title = value; break;
                case "UserName": e.UserName = value; break;
                case "Password": e.Password = value; e.PasswordProtected = prot; break;
                case "URL": e.Url = value; break;
                case "Notes": e.Notes = value; break;
                default: e.CustomFields[key] = new KpField(value, prot); break;
            }
        }
        return e;
    }

    private string BuildXml(IInnerStream protect)
    {
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false), OmitXmlDeclaration = false };
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        using (var w = XmlWriter.Create(sw, settings))
        {
            w.WriteStartElement("KeePassFile");
            w.WriteStartElement("Meta");
            w.WriteElementString("Generator", "WinForge");
            w.WriteElementString("DatabaseName", Root.Name);
            w.WriteElementString("HeaderHash", "");
            w.WriteEndElement(); // Meta
            w.WriteStartElement("Root");
            WriteGroup(w, Root, protect);
            w.WriteEndElement(); // Root
            w.WriteEndElement(); // KeePassFile
        }
        return sb.ToString();
    }

    private void WriteGroup(XmlWriter w, KpGroup g, IInnerStream protect)
    {
        w.WriteStartElement("Group");
        w.WriteElementString("UUID", g.Uuid);
        w.WriteElementString("Name", g.Name);
        if (!string.IsNullOrEmpty(g.Notes)) w.WriteElementString("Notes", g.Notes);
        foreach (var e in g.Entries) WriteEntry(w, e, protect);
        foreach (var sub in g.Groups) WriteGroup(w, sub, protect);
        w.WriteEndElement();
    }

    private void WriteEntry(XmlWriter w, KpEntry e, IInnerStream protect)
    {
        w.WriteStartElement("Entry");
        w.WriteElementString("UUID", e.Uuid);
        WriteString(w, "Title", e.Title, false, protect);
        WriteString(w, "UserName", e.UserName, false, protect);
        WriteString(w, "Password", e.Password, true, protect);
        WriteString(w, "URL", e.Url, false, protect);
        WriteString(w, "Notes", e.Notes, false, protect);
        foreach (var kv in e.CustomFields)
            WriteString(w, kv.Key, kv.Value.Value, kv.Value.Protected, protect);
        w.WriteEndElement();
    }

    private void WriteString(XmlWriter w, string key, string value, bool prot, IInnerStream protect)
    {
        w.WriteStartElement("String");
        w.WriteElementString("Key", key);
        w.WriteStartElement("Value");
        if (prot)
        {
            w.WriteAttributeString("Protected", "True");
            if (!string.IsNullOrEmpty(value))
                w.WriteString(Convert.ToBase64String(protect.Encrypt(Encoding.UTF8.GetBytes(value))));
        }
        else w.WriteString(value ?? "");
        w.WriteEndElement(); // Value
        w.WriteEndElement(); // String
    }
}

// ── Data model ─────────────────────────────────────────────────────────────────

/// <summary>一個群組（樹狀）· One group node in the KDBX tree.</summary>
public sealed class KpGroup
{
    public string Uuid { get; set; } = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    public string Name { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<KpGroup> Groups { get; } = new();
    public List<KpEntry> Entries { get; } = new();
}

/// <summary>自訂欄位（值 + 是否保護）· One custom field (value + protected flag).</summary>
public sealed record KpField(string Value, bool Protected);

/// <summary>一個項目 · One vault entry.</summary>
public sealed class KpEntry
{
    public string Uuid { get; set; } = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    public string Title { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public bool PasswordProtected { get; set; } = true;
    public string Url { get; set; } = "";
    public string Notes { get; set; } = "";
    public Dictionary<string, string> CustomFieldsRaw => CustomFields.ToDictionary(k => k.Key, v => v.Value.Value);
    public Dictionary<string, KpField> CustomFields { get; } = new();
}

// ── Inner random stream implementations ─────────────────────────────────────────

/// <summary>內部串流介面 · The inner random stream used to protect/unprotect field values.</summary>
public interface IInnerStream
{
    byte[] Encrypt(byte[] plain);
    byte[] Decrypt(byte[] cipher);
}

/// <summary>無保護（CrsNone）· No protection.</summary>
public sealed class NullStream : IInnerStream
{
    public byte[] Encrypt(byte[] plain) => plain;
    public byte[] Decrypt(byte[] cipher) => cipher;
}

/// <summary>
/// Salsa20 內部串流（KDBX 3.1）· Salsa20 keystream with KeePass's fixed 8-byte IV.
/// Encryption and decryption are the same XOR-with-keystream operation, applied in order.
/// </summary>
public sealed class Salsa20Stream : IInnerStream
{
    private static readonly byte[] FixedIv = { 0xE8, 0x30, 0x09, 0x4B, 0x97, 0x20, 0x5D, 0x2A };
    private readonly Salsa20 _enc;
    private readonly Salsa20 _dec;

    public Salsa20Stream(byte[] key)
    {
        _enc = new Salsa20(key, FixedIv);
        _dec = new Salsa20(key, FixedIv);
    }

    public byte[] Encrypt(byte[] plain) => _enc.Xor(plain);
    public byte[] Decrypt(byte[] cipher) => _dec.Xor(cipher);
}

/// <summary>
/// ChaCha20 內部串流（KDBX 4）· ChaCha20 keystream. Key = SHA-512(innerKey)[0..32], nonce = next 12 bytes.
/// Encrypt/decrypt run as separate keystreams in field order.
/// </summary>
public sealed class ChaCha20Stream : IInnerStream
{
    private readonly ChaCha20Cipher _enc;
    private readonly ChaCha20Cipher _dec;

    public ChaCha20Stream(byte[] innerKey)
    {
        byte[] h = SHA512.HashData(innerKey);
        byte[] key = h[0..32];
        byte[] nonce = h[32..44];
        _enc = new ChaCha20Cipher(key, nonce);
        _dec = new ChaCha20Cipher(key, nonce);
    }

    public byte[] Encrypt(byte[] plain) => _enc.Xor(plain);
    public byte[] Decrypt(byte[] cipher) => _dec.Xor(cipher);
}
