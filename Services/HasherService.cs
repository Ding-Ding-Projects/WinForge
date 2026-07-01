using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 雜湊／校驗和引擎（純託管）· Hash / checksum engine (pure managed). Computes MD5, SHA1, SHA256,
/// SHA384, SHA512 and a from-scratch CRC32 (0xEDB88320 table) over text or streamed files.
/// Optional HMAC (keyed) mode swaps SHA256/SHA512 for HMACSHA256/HMACSHA512 over the text input.
/// No Process.Start, no extra NuGet — only System.Security.Cryptography + a hand-rolled CRC32.
/// </summary>
public static class HasherService
{
    /// <summary>一組六個雜湊結果（細楷十六進位）· One set of six hash results (lowercase hex).</summary>
    public sealed class HashSet
    {
        public string Md5 = "";
        public string Sha1 = "";
        public string Sha256 = "";
        public string Sha384 = "";
        public string Sha512 = "";
        public string Crc32 = "";
    }

    /// <summary>支援嘅文字編碼 · Supported text encodings.</summary>
    public enum TextEncodingKind { Utf8, Utf16, Ascii }

    public static Encoding EncodingFor(TextEncodingKind kind) => kind switch
    {
        TextEncodingKind.Utf16 => Encoding.Unicode,       // UTF-16 LE
        TextEncodingKind.Ascii => Encoding.ASCII,
        _ => new UTF8Encoding(false),                      // UTF-8, no BOM
    };

    // ===== CRC32 (standard reflected 0xEDB88320 polynomial) =====

    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        const uint poly = 0xEDB88320u;
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? poly ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }

    private sealed class Crc32Accumulator
    {
        private uint _crc = 0xFFFFFFFFu;
        public void Update(byte[] buffer, int count)
        {
            uint crc = _crc;
            for (int i = 0; i < count; i++)
                crc = Crc32Table[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
            _crc = crc;
        }
        public string Finish() => (_crc ^ 0xFFFFFFFFu).ToString("x8");
    }

    private static string Crc32Hex(byte[] data)
    {
        var acc = new Crc32Accumulator();
        acc.Update(data, data.Length);
        return acc.Finish();
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    // ===== Text hashing =====

    /// <summary>
    /// 計算文字嘅六個雜湊 · Compute the six hashes over text. When <paramref name="hmacKey"/> is
    /// non-empty, SHA256/SHA512 slots are replaced by HMAC-SHA256/HMAC-SHA512 over the same bytes.
    /// </summary>
    public static Task<HashSet> HashTextAsync(string text, TextEncodingKind encoding, string? hmacKey)
        => Task.Run(() =>
        {
            byte[] data = EncodingFor(encoding).GetBytes(text ?? "");
            var result = new HashSet
            {
                Md5 = ToHex(MD5.HashData(data)),
                Sha1 = ToHex(SHA1.HashData(data)),
                Sha384 = ToHex(SHA384.HashData(data)),
                Crc32 = Crc32Hex(data),
            };

            if (!string.IsNullOrEmpty(hmacKey))
            {
                byte[] key = EncodingFor(encoding).GetBytes(hmacKey);
                using var h256 = new HMACSHA256(key);
                using var h512 = new HMACSHA512(key);
                result.Sha256 = ToHex(h256.ComputeHash(data));
                result.Sha512 = ToHex(h512.ComputeHash(data));
            }
            else
            {
                result.Sha256 = ToHex(SHA256.HashData(data));
                result.Sha512 = ToHex(SHA512.HashData(data));
            }
            return result;
        });

    // ===== File hashing (streamed) =====

    /// <summary>檔案資料 · Basic file info paired with its hashes.</summary>
    public sealed class FileHashResult
    {
        public string Name = "";
        public long Size;
        public HashSet Hashes = new();
    }

    /// <summary>
    /// 串流讀取檔案再計六個雜湊 · Stream a file and compute the six hashes in one pass.
    /// Runs entirely on a background thread (Task.Run) so the UI never freezes.
    /// </summary>
    public static Task<FileHashResult> HashFileAsync(string path)
        => Task.Run(() =>
        {
            var info = new FileInfo(path);
            var result = new FileHashResult { Name = info.Name, Size = info.Length };

            using var md5 = MD5.Create();
            using var sha1 = SHA1.Create();
            using var sha256 = SHA256.Create();
            using var sha384 = SHA384.Create();
            using var sha512 = SHA512.Create();
            var crc = new Crc32Accumulator();

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                       bufferSize: 1 << 20, FileOptions.SequentialScan))
            {
                var buffer = new byte[1 << 20];
                int read;
                while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    md5.TransformBlock(buffer, 0, read, null, 0);
                    sha1.TransformBlock(buffer, 0, read, null, 0);
                    sha256.TransformBlock(buffer, 0, read, null, 0);
                    sha384.TransformBlock(buffer, 0, read, null, 0);
                    sha512.TransformBlock(buffer, 0, read, null, 0);
                    crc.Update(buffer, read);
                }
                md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                sha384.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                sha512.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            }

            result.Hashes = new HashSet
            {
                Md5 = ToHex(md5.Hash ?? Array.Empty<byte>()),
                Sha1 = ToHex(sha1.Hash ?? Array.Empty<byte>()),
                Sha256 = ToHex(sha256.Hash ?? Array.Empty<byte>()),
                Sha384 = ToHex(sha384.Hash ?? Array.Empty<byte>()),
                Sha512 = ToHex(sha512.Hash ?? Array.Empty<byte>()),
                Crc32 = crc.Finish(),
            };
            return result;
        });

    /// <summary>把「預期雜湊」正規化以作比對（去空白、細楷）· Normalize an expected hash for comparison.</summary>
    public static string Normalize(string? s)
        => (s ?? "").Replace(" ", "").Replace("\t", "").Replace("-", "").Trim().ToLowerInvariant();

    /// <summary>
    /// 「預期雜湊」有冇對到任何一個計出嘅雜湊 · Does the expected value match ANY computed hash?
    /// Case-insensitive, spaces/dashes ignored.
    /// </summary>
    public static bool Matches(HashSet set, string? expected)
    {
        var e = Normalize(expected);
        if (e.Length == 0) return false;
        return e == set.Md5 || e == set.Sha1 || e == set.Sha256
            || e == set.Sha384 || e == set.Sha512 || e == set.Crc32;
    }

    /// <summary>友善顯示檔案大小 · Human-friendly file size.</summary>
    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        int u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return u == 0 ? $"{bytes:N0} {units[u]}" : $"{v:0.##} {units[u]}";
    }
}
