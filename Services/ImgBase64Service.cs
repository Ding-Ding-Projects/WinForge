using System;
using System.IO;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 圖片 ↔ Base64 · Image ↔ Base64 conversion. Pure managed (System.IO / System.Convert):
/// read an image file into a "data:image/&lt;ext&gt;;base64,…" data URI, or turn a data URI /
/// raw base64 string back into bytes. All IO runs off the UI thread (Task.Run) and every path
/// is guarded — the service never throws; callers get a result record with an error string instead.
/// </summary>
public static class ImgBase64Service
{
    /// <summary>Result of an encode/decode attempt — <see cref="Error"/> is null on success.</summary>
    public readonly record struct Result(byte[]? Bytes, string? Text, string? Ext, string? Error)
    {
        public bool Ok => Error is null;
    }

    /// <summary>Map a file extension to an image MIME subtype (defaults to "png").</summary>
    public static string MimeExtFor(string? path)
    {
        var e = (Path.GetExtension(path ?? "") ?? "").TrimStart('.').ToLowerInvariant();
        return e switch
        {
            "jpg" or "jpeg" => "jpeg",
            "png" => "png",
            "gif" => "gif",
            "bmp" => "bmp",
            "webp" => "webp",
            "" => "png",
            _ => e,
        };
    }

    /// <summary>Read an image file and produce a data URI. Returns the raw bytes too (for preview).</summary>
    public static async Task<Result> EncodeFileAsync(string path)
    {
        try
        {
            var bytes = await Task.Run(() => File.ReadAllBytes(path)).ConfigureAwait(true);
            var ext = MimeExtFor(path);
            var uri = "data:image/" + ext + ";base64," + Convert.ToBase64String(bytes);
            return new Result(bytes, uri, ext, null);
        }
        catch (Exception ex)
        {
            return new Result(null, null, null, ex.Message);
        }
    }

    /// <summary>Strip an optional "data:…;base64," prefix and decode. Returns bytes + detected ext.</summary>
    public static Result Decode(string? input)
    {
        try
        {
            var s = (input ?? "").Trim();
            if (s.Length == 0) return new Result(null, null, null, "empty");
            string? ext = null;
            int comma = s.IndexOf(',');
            if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
            {
                var header = s.Substring(5, comma - 5); // e.g. image/png;base64
                int slash = header.IndexOf('/');
                int semi = header.IndexOf(';');
                if (slash >= 0)
                {
                    int end = semi > slash ? semi : header.Length;
                    ext = header.Substring(slash + 1, end - slash - 1).Trim();
                }
                s = s.Substring(comma + 1);
            }
            s = s.Trim();
            var bytes = Convert.FromBase64String(s);
            if (bytes.Length == 0) return new Result(null, null, null, "empty");
            return new Result(bytes, null, ext, null);
        }
        catch (Exception ex)
        {
            return new Result(null, null, null, ex.Message);
        }
    }

    /// <summary>Write bytes to disk off the UI thread; returns an error string or null on success.</summary>
    public static async Task<string?> SaveBytesAsync(string path, byte[] bytes)
    {
        try
        {
            await Task.Run(() => File.WriteAllBytes(path, bytes)).ConfigureAwait(true);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
