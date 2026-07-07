using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 十六進位傾印引擎 · Hex-dump engine — turn bytes/text/hex/a file into a classic
/// offset │ hex │ ASCII dump. Pure managed, never throws (all public members swallow
/// failure and degrade gracefully). Reads files with a hard byte cap.
/// </summary>
public static class HexDumpService
{
    /// <summary>檔案讀取上限 · Hard read cap for files (~1 MB).</summary>
    public const int MaxBytes = 1024 * 1024;

    /// <summary>UTF-8 文字 → 位元組 · Decode UTF-8 text to bytes.</summary>
    public static byte[] FromText(string? text)
    {
        try { return Encoding.UTF8.GetBytes(text ?? string.Empty); }
        catch { return Array.Empty<byte>(); }
    }

    /// <summary>剖析貼上嘅十六進位（略去分隔符 / 0x / 空白）· Parse pasted hex, ignoring
    /// whitespace, commas, 0x prefixes and other separators. Odd trailing nibble is dropped.</summary>
    public static byte[] FromHex(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return Array.Empty<byte>();
        try
        {
            var digits = new StringBuilder(hex.Length);
            for (int i = 0; i < hex.Length; i++)
            {
                char c = hex[i];
                // skip a 0x / 0X prefix
                if ((c == 'x' || c == 'X') && digits.Length > 0 && digits[digits.Length - 1] == '0')
                {
                    digits.Length--; // drop the leading 0 of "0x"
                    continue;
                }
                if (Uri.IsHexDigit(c)) digits.Append(c);
            }
            int n = digits.Length / 2;
            var bytes = new byte[n];
            for (int i = 0; i < n; i++)
            {
                int hi = HexVal(digits[i * 2]);
                int lo = HexVal(digits[i * 2 + 1]);
                bytes[i] = (byte)((hi << 4) | lo);
            }
            return bytes;
        }
        catch { return Array.Empty<byte>(); }
    }

    private static int HexVal(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        return 0;
    }

    /// <summary>讀取檔案（有上限）· Read a file, capped at <see cref="MaxBytes"/>. Returns the
    /// bytes and whether the read was truncated. Never throws.</summary>
    public static async Task<(byte[] Bytes, bool Truncated, string? Error)> FromFileAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (Array.Empty<byte>(), false, null);
        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var fs = new FileStream(path!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    long len = 0;
                    try { len = fs.Length; } catch { }
                    int want = (int)Math.Min(len <= 0 ? MaxBytes : len, MaxBytes);
                    if (want <= 0) want = MaxBytes;
                    var buf = new byte[want];
                    int read = 0;
                    while (read < buf.Length)
                    {
                        int r = fs.Read(buf, read, buf.Length - read);
                        if (r <= 0) break;
                        read += r;
                    }
                    if (read != buf.Length) Array.Resize(ref buf, read);
                    bool truncated = false;
                    try { truncated = fs.Length > read; } catch { }
                    return (buf, truncated, (string?)null);
                }
                catch (Exception ex) { return (Array.Empty<byte>(), false, ex.Message); }
            });
        }
        catch (Exception ex) { return (Array.Empty<byte>(), false, ex.Message); }
    }

    /// <summary>整合傾印 · Render a classic hex dump. Never throws.</summary>
    /// <param name="bytes">來源位元組 · source bytes.</param>
    /// <param name="perRow">每行位元組數（8/16/32）· bytes per row.</param>
    /// <param name="upper">大寫十六進位 · uppercase hex.</param>
    /// <param name="showOffset">顯示偏移欄 · show the offset column.</param>
    public static string Render(byte[]? bytes, int perRow = 16, bool upper = false, bool showOffset = true)
    {
        try
        {
            bytes ??= Array.Empty<byte>();
            if (perRow != 8 && perRow != 16 && perRow != 32) perRow = 16;
            string hx = upper ? "X2" : "x2";
            int group = 8; // insert an extra space every 8 bytes
            var sb = new StringBuilder(bytes.Length * 4 + 64);

            for (int off = 0; off < bytes.Length; off += perRow)
            {
                if (showOffset)
                {
                    sb.Append(off.ToString(upper ? "X8" : "x8", CultureInfo.InvariantCulture));
                    sb.Append("  ");
                }

                // hex columns
                for (int i = 0; i < perRow; i++)
                {
                    int idx = off + i;
                    if (idx < bytes.Length)
                        sb.Append(bytes[idx].ToString(hx, CultureInfo.InvariantCulture));
                    else
                        sb.Append("  ");
                    sb.Append(' ');
                    if ((i + 1) % group == 0 && i + 1 < perRow) sb.Append(' ');
                }

                sb.Append(' ');
                // ASCII gutter
                for (int i = 0; i < perRow; i++)
                {
                    int idx = off + i;
                    if (idx >= bytes.Length) break;
                    byte b = bytes[idx];
                    sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
                }
                sb.Append('\n');
            }

            if (bytes.Length == 0)
                sb.Append("(no bytes)");
            return sb.ToString();
        }
        catch { return "(error rendering dump)"; }
    }
}
