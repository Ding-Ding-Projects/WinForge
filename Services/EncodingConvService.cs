using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 文字編碼／換行轉換 · Text encoding &amp; line-ending converter. Pure-managed (System.Text.Encoding):
/// sniffs the source encoding by BOM and detects line endings, then re-encodes text to a chosen target
/// encoding and line-ending. All file IO is async and guarded — nothing here throws to the caller.
/// </summary>
public static class EncodingConvService
{
    /// <summary>偵測到嘅編碼 · A detected/target text encoding kind.</summary>
    public enum EncKind { Utf8, Utf8Bom, Utf16Le, Utf16Be, Ascii, Latin1, Unknown }

    /// <summary>換行方式 · Line-ending kind.</summary>
    public enum Eol { Lf, CrLf, Cr, Mixed, None }

    /// <summary>讀檔結果 · Result of reading a file: the decoded text plus what was detected.</summary>
    public readonly record struct ReadResult(bool Ok, string Text, EncKind Encoding, Eol LineEnding, string Message);

    /// <summary>寫檔結果 · Result of writing a file.</summary>
    public readonly record struct WriteResult(bool Ok, string Message);

    /// <summary>由 BOM 嗅探編碼並解碼檔案 · Read a file, sniff its BOM to pick an encoding, and decode.</summary>
    public static async Task<ReadResult> ReadFileAsync(string path)
    {
        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            EncKind enc = SniffBom(bytes, out int bomLen);
            if (enc == EncKind.Unknown)
                enc = LooksAscii(bytes) ? EncKind.Ascii : EncKind.Utf8; // no BOM → best-effort
            string text = Decode(bytes, bomLen, enc);
            Eol eol = DetectEol(text);
            return new ReadResult(true, text, enc, eol, "Loaded " + bytes.Length + " bytes");
        }
        catch (Exception ex)
        {
            return new ReadResult(false, string.Empty, EncKind.Unknown, Eol.None, ex.Message);
        }
    }

    /// <summary>偵測貼上文字嘅換行 · Detect the line-ending style of an in-memory string.</summary>
    public static Eol DetectEol(string text)
    {
        if (string.IsNullOrEmpty(text)) return Eol.None;
        bool crlf = false, lf = false, cr = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') { crlf = true; i++; }
                else cr = true;
            }
            else if (c == '\n') lf = true;
        }
        int kinds = (crlf ? 1 : 0) + (lf ? 1 : 0) + (cr ? 1 : 0);
        if (kinds == 0) return Eol.None;
        if (kinds > 1) return Eol.Mixed;
        if (crlf) return Eol.CrLf;
        if (lf) return Eol.Lf;
        return Eol.Cr;
    }

    /// <summary>轉換換行 · Normalise every line-ending in the text to the chosen style.</summary>
    public static string ConvertLineEndings(string text, Eol target)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string nl = target switch { Eol.CrLf => "\r\n", Eol.Cr => "\r", _ => "\n" };
        var sb = new StringBuilder(text.Length + 16);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') i++; // consume CRLF pair
                sb.Append(nl);
            }
            else if (c == '\n') sb.Append(nl);
            else sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>把文字用目標編碼寫入磁碟 · Encode text with the target encoding and write it to disk.</summary>
    public static async Task<WriteResult> SaveFileAsync(string path, string text, EncKind target)
    {
        try
        {
            byte[] bytes = Encode(text, target);
            await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
            return new WriteResult(true, "Saved " + bytes.Length + " bytes");
        }
        catch (Exception ex)
        {
            return new WriteResult(false, ex.Message);
        }
    }

    /// <summary>把文字轉成目標編碼嘅位元組 · Encode text into bytes for the target encoding (with/without BOM).</summary>
    public static byte[] Encode(string text, EncKind target)
    {
        text ??= string.Empty;
        return target switch
        {
            EncKind.Utf8Bom => Prepend(Utf8NoBom.GetBytes(text), Utf8Preamble),
            EncKind.Utf8 => Utf8NoBom.GetBytes(text),
            EncKind.Utf16Le => Prepend(new UnicodeEncoding(false, false).GetBytes(text), Utf16LePreamble),
            EncKind.Utf16Be => Prepend(new UnicodeEncoding(true, false).GetBytes(text), Utf16BePreamble),
            EncKind.Ascii => Encoding.ASCII.GetBytes(text),
            EncKind.Latin1 => Encoding.Latin1.GetBytes(text),
            _ => Utf8NoBom.GetBytes(text),
        };
    }

    /// <summary>編碼嘅顯示名 · Friendly bilingual-neutral label for an encoding kind.</summary>
    public static string Label(EncKind k) => k switch
    {
        EncKind.Utf8 => "UTF-8",
        EncKind.Utf8Bom => "UTF-8 with BOM",
        EncKind.Utf16Le => "UTF-16 LE",
        EncKind.Utf16Be => "UTF-16 BE",
        EncKind.Ascii => "ASCII",
        EncKind.Latin1 => "Latin-1",
        _ => "Unknown",
    };

    /// <summary>換行嘅顯示名 · Friendly label for a line-ending kind.</summary>
    public static string Label(Eol e) => e switch
    {
        Eol.Lf => "LF",
        Eol.CrLf => "CRLF",
        Eol.Cr => "CR",
        Eol.Mixed => "Mixed",
        _ => "None",
    };

    // ===== internals =====

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false, false);
    private static readonly byte[] Utf8Preamble = { 0xEF, 0xBB, 0xBF };
    private static readonly byte[] Utf16LePreamble = { 0xFF, 0xFE };
    private static readonly byte[] Utf16BePreamble = { 0xFE, 0xFF };

    private static EncKind SniffBom(byte[] b, out int bomLen)
    {
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) { bomLen = 3; return EncKind.Utf8Bom; }
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) { bomLen = 2; return EncKind.Utf16Le; }
        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) { bomLen = 2; return EncKind.Utf16Be; }
        bomLen = 0;
        return EncKind.Unknown;
    }

    private static bool LooksAscii(byte[] b)
    {
        for (int i = 0; i < b.Length; i++) if (b[i] > 0x7F) return false;
        return true;
    }

    private static string Decode(byte[] b, int bomLen, EncKind enc)
    {
        int len = b.Length - bomLen;
        return enc switch
        {
            EncKind.Utf16Le => new UnicodeEncoding(false, false).GetString(b, bomLen, len),
            EncKind.Utf16Be => new UnicodeEncoding(true, false).GetString(b, bomLen, len),
            EncKind.Ascii => Encoding.ASCII.GetString(b, bomLen, len),
            EncKind.Latin1 => Encoding.Latin1.GetString(b, bomLen, len),
            _ => Utf8NoBom.GetString(b, bomLen, len),
        };
    }

    private static byte[] Prepend(byte[] body, byte[] preamble)
    {
        var outp = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, outp, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, outp, preamble.Length, body.Length);
        return outp;
    }
}
