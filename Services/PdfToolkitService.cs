using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;
using UglyToad.PdfPig;
using WinForge.Models;
using PdfDocument = PdfSharp.Pdf.PdfDocument;
using PigDocument = UglyToad.PdfPig.PdfDocument;

namespace WinForge.Services;

/// <summary>
/// 原生 PDF 工具箱引擎（純 C#）· Fully-managed PDF toolkit engine.
/// 用 PDFsharp 做操作（合併／分割／旋轉／刪頁／重排／抽取／浮水印／加密／解密／圖片轉 PDF）
/// 同 PdfPig 抽取文字。完全唔會啟動 / 呼叫 Stirling-PDF、Ghostscript 或者任何外部工具。
/// Uses PDFsharp for manipulation and PdfPig for text extraction — no external tool is ever launched.
/// </summary>
public static class PdfToolkitService
{
    public sealed record PdfPageSummary(int PageNumber, double WidthPoints, double HeightPoints, int Rotation);

    public sealed record PdfMetadata(
        string Path,
        long FileBytes,
        DateTime Modified,
        int PageCount,
        string Version,
        string? Title,
        string? Author,
        string? Subject,
        string? Keywords,
        string? Creator,
        string? Producer,
        string? Created,
        string? PdfModified,
        IReadOnlyList<PdfPageSummary> PageSummaries);

    public sealed record PdfSearchHit(int PageNumber, int MatchCount, string Snippet);

    // ─────────────────────────────────────────────────────────────────────────
    // 共用：頁碼範圍解析（1-based，"1-3,5,8-" 之類）· Parse a 1-based page-range spec.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 解析頁碼範圍字串成為 0-based 索引清單（保留次序，可重複）。
    /// Parse a spec like "1-3, 5, 8-10" into 0-based page indices (order preserved).
    /// Returns null if the spec is malformed; empty spec → all pages 0..count-1.
    /// </summary>
    public static List<int>? ParseRanges(string? spec, int pageCount)
    {
        var result = new List<int>();
        if (string.IsNullOrWhiteSpace(spec))
        {
            for (int i = 0; i < pageCount; i++) result.Add(i);
            return result;
        }
        foreach (var rawPart in spec.Split(new[] { ',', '；', ';', '，' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var part = rawPart.Trim();
            if (part.Length == 0) continue;
            var dash = part.IndexOf('-');
            if (dash < 0)
            {
                if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return null;
                if (n < 1 || n > pageCount) return null;
                result.Add(n - 1);
            }
            else
            {
                var lo = part[..dash].Trim();
                var hi = part[(dash + 1)..].Trim();
                int from = 1, to = pageCount;
                if (lo.Length > 0 && !int.TryParse(lo, NumberStyles.Integer, CultureInfo.InvariantCulture, out from)) return null;
                if (hi.Length > 0 && !int.TryParse(hi, NumberStyles.Integer, CultureInfo.InvariantCulture, out to)) return null;
                if (from < 1 || to > pageCount || from > to) return null;
                for (int i = from; i <= to; i++) result.Add(i - 1);
            }
        }
        return result;
    }

    /// <summary>頁數（開檔讀取）· Get the page count of a PDF (may need a password).</summary>
    public static int PageCount(string path, string? password = null)
    {
        using var doc = Open(path, password, PdfDocumentOpenMode.Import);
        return doc.PageCount;
    }

    private static PdfDocument Open(string path, string? password, PdfDocumentOpenMode mode)
    {
        if (string.IsNullOrEmpty(password))
            return PdfReader.Open(path, mode);
        return PdfReader.Open(path, password, mode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 檢視器資料 · Viewer metadata + text search
    // ─────────────────────────────────────────────────────────────────────────

    public static Task<PdfMetadata> InspectAsync(string input, string? password = null)
        => Task.Run(() =>
        {
            using var doc = Open(input, password, PdfDocumentOpenMode.Import);
            var fi = new FileInfo(input);
            var summaries = new List<PdfPageSummary>();
            int take = Math.Min(doc.PageCount, 24);
            for (int i = 0; i < take; i++)
            {
                var p = doc.Pages[i];
                summaries.Add(new PdfPageSummary(i + 1, p.Width.Point, p.Height.Point, p.Rotate));
            }

            return new PdfMetadata(
                input,
                fi.Exists ? fi.Length : 0,
                fi.Exists ? fi.LastWriteTime : DateTime.MinValue,
                doc.PageCount,
                doc.Version.ToString(CultureInfo.InvariantCulture),
                BlankToNull(doc.Info.Title),
                BlankToNull(doc.Info.Author),
                BlankToNull(doc.Info.Subject),
                BlankToNull(doc.Info.Keywords),
                BlankToNull(doc.Info.Creator),
                BlankToNull(doc.Info.Producer),
                DateOrNull(doc.Info.CreationDate),
                DateOrNull(doc.Info.ModificationDate),
                summaries);
        });

    public static Task<IReadOnlyList<PdfSearchHit>> SearchTextAsync(
        string input,
        string query,
        string? password = null,
        bool caseSensitive = false,
        int maxHits = 80)
        => Task.Run<IReadOnlyList<PdfSearchHit>>(() =>
        {
            var hits = new List<PdfSearchHit>();
            if (string.IsNullOrWhiteSpace(query)) return hits;

            var opts = string.IsNullOrEmpty(password) ? new ParsingOptions() : new ParsingOptions { Password = password };
            var comparison = caseSensitive ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            using var pdf = PigDocument.Open(input, opts);
            foreach (var page in pdf.GetPages())
            {
                var text = page.Text ?? "";
                int first = -1;
                int count = 0;
                int at = 0;
                while (at < text.Length)
                {
                    int found = text.IndexOf(query, at, comparison);
                    if (found < 0) break;
                    if (first < 0) first = found;
                    count++;
                    at = found + Math.Max(1, query.Length);
                }

                if (count > 0)
                {
                    hits.Add(new PdfSearchHit(page.Number, count, Snippet(text, first, query.Length)));
                    if (hits.Count >= maxHits) break;
                }
            }
            return hits;
        });

    // ─────────────────────────────────────────────────────────────────────────
    // 合併 · Merge
    // ─────────────────────────────────────────────────────────────────────────

    public static Task<TweakResult> MergeAsync(IReadOnlyList<string> inputs, string output)
        => Task.Run(() =>
        {
            try
            {
                if (inputs.Count == 0)
                    return TweakResult.Fail("No input files selected.", "未揀任何輸入檔。");
                using var outDoc = new PdfDocument();
                int total = 0;
                foreach (var path in inputs)
                {
                    using var src = PdfReader.Open(path, PdfDocumentOpenMode.Import);
                    for (int i = 0; i < src.PageCount; i++) { outDoc.AddPage(src.Pages[i]); total++; }
                }
                outDoc.Save(output);
                return TweakResult.Ok(
                    $"Merged {inputs.Count} file(s) → {total} pages.", $"已合併 {inputs.Count} 個檔案 → {total} 頁。", output);
            }
            catch (Exception ex) { return Err(ex); }
        });

    // ─────────────────────────────────────────────────────────────────────────
    // 分割 · Split — either one file per page, or by ranges (each range → one file).
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>每頁一個檔 · One output PDF per page, written into <paramref name="outputFolder"/>.</summary>
    public static Task<TweakResult> SplitPerPageAsync(string input, string outputFolder, string? password = null)
        => Task.Run(() =>
        {
            try
            {
                using var src = Open(input, password, PdfDocumentOpenMode.Import);
                var stem = Path.GetFileNameWithoutExtension(input);
                int pad = src.PageCount.ToString().Length;
                for (int i = 0; i < src.PageCount; i++)
                {
                    using var one = new PdfDocument();
                    one.AddPage(src.Pages[i]);
                    var name = $"{stem}_p{(i + 1).ToString().PadLeft(pad, '0')}.pdf";
                    one.Save(Path.Combine(outputFolder, name));
                }
                return TweakResult.Ok($"Split into {src.PageCount} single-page files.",
                    $"已分割成 {src.PageCount} 個單頁檔案。", outputFolder);
            }
            catch (Exception ex) { return Err(ex); }
        });

    /// <summary>按範圍分割（每個逗號分隔嘅範圍 → 一個檔）· One output file per comma-separated range.</summary>
    public static Task<TweakResult> SplitByRangesAsync(string input, string outputFolder, string rangesSpec, string? password = null)
        => Task.Run(() =>
        {
            try
            {
                using var src = Open(input, password, PdfDocumentOpenMode.Import);
                var stem = Path.GetFileNameWithoutExtension(input);
                var groups = rangesSpec.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
                if (groups.Length == 0) return TweakResult.Fail("Enter at least one page range.", "請輸入至少一個頁碼範圍。");
                int made = 0, gi = 0;
                foreach (var g in groups)
                {
                    var idx = ParseRanges(g.Trim(), src.PageCount);
                    if (idx is null || idx.Count == 0)
                        return TweakResult.Fail($"Invalid range: \"{g.Trim()}\" (document has {src.PageCount} pages).",
                            $"範圍無效：「{g.Trim()}」（文件共 {src.PageCount} 頁）。");
                    using var part = new PdfDocument();
                    foreach (var i in idx) part.AddPage(src.Pages[i]);
                    gi++;
                    var name = $"{stem}_part{gi}.pdf";
                    part.Save(Path.Combine(outputFolder, name));
                    made++;
                }
                return TweakResult.Ok($"Created {made} file(s) from the ranges.", $"已由範圍建立 {made} 個檔案。", outputFolder);
            }
            catch (Exception ex) { return Err(ex); }
        });

    // ─────────────────────────────────────────────────────────────────────────
    // 旋轉 · Rotate
    // ─────────────────────────────────────────────────────────────────────────

    public static Task<TweakResult> RotateAsync(string input, string output, int degrees, string? rangesSpec, string? password = null)
        => Task.Run(() =>
        {
            try
            {
                degrees = ((degrees % 360) + 360) % 360;
                using var doc = Open(input, password, PdfDocumentOpenMode.Modify);
                var idx = ParseRanges(rangesSpec, doc.PageCount);
                if (idx is null) return TweakResult.Fail("Invalid page range.", "頁碼範圍無效。");
                var set = new HashSet<int>(idx);
                int touched = 0;
                for (int i = 0; i < doc.PageCount; i++)
                {
                    if (!set.Contains(i)) continue;
                    var page = doc.Pages[i];
                    page.Rotate = (page.Rotate + degrees) % 360;
                    touched++;
                }
                doc.Save(output);
                return TweakResult.Ok($"Rotated {touched} page(s) by {degrees}°.", $"已將 {touched} 頁旋轉 {degrees}°。", output);
            }
            catch (Exception ex) { return Err(ex); }
        });

    // ─────────────────────────────────────────────────────────────────────────
    // 刪頁 / 抽頁 / 重排 · Delete / Extract / Reorder pages
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>刪除指定頁 · Delete the pages named in the spec; keep the rest.</summary>
    public static Task<TweakResult> DeletePagesAsync(string input, string output, string rangesSpec, string? password = null)
        => Task.Run(() =>
        {
            try
            {
                using var src = Open(input, password, PdfDocumentOpenMode.Import);
                var del = ParseRanges(rangesSpec, src.PageCount);
                if (del is null || del.Count == 0) return TweakResult.Fail("Invalid page range.", "頁碼範圍無效。");
                var remove = new HashSet<int>(del);
                if (remove.Count >= src.PageCount) return TweakResult.Fail("Cannot delete every page.", "唔可以刪除全部頁。");
                using var outDoc = new PdfDocument();
                for (int i = 0; i < src.PageCount; i++)
                    if (!remove.Contains(i)) outDoc.AddPage(src.Pages[i]);
                outDoc.Save(output);
                return TweakResult.Ok($"Deleted {remove.Count} page(s); {outDoc.PageCount} remain.",
                    $"已刪除 {remove.Count} 頁；剩 {outDoc.PageCount} 頁。", output);
            }
            catch (Exception ex) { return Err(ex); }
        });

    /// <summary>抽取指定頁成新檔（保留指定次序）· Extract the named pages into a new PDF (order preserved).</summary>
    public static Task<TweakResult> ExtractPagesAsync(string input, string output, string rangesSpec, string? password = null)
        => Task.Run(() =>
        {
            try
            {
                using var src = Open(input, password, PdfDocumentOpenMode.Import);
                var idx = ParseRanges(rangesSpec, src.PageCount);
                if (idx is null || idx.Count == 0) return TweakResult.Fail("Invalid page range.", "頁碼範圍無效。");
                using var outDoc = new PdfDocument();
                foreach (var i in idx) outDoc.AddPage(src.Pages[i]);
                outDoc.Save(output);
                return TweakResult.Ok($"Extracted {outDoc.PageCount} page(s).", $"已抽取 {outDoc.PageCount} 頁。", output);
            }
            catch (Exception ex) { return Err(ex); }
        });

    /// <summary>
    /// 重排頁面：spec 係新次序（必須係 1..N 嘅一個排列）· Reorder by an explicit new order
    /// (the spec must list every page exactly once, e.g. "3,1,2,4").
    /// </summary>
    public static Task<TweakResult> ReorderAsync(string input, string output, string orderSpec, string? password = null)
        => Task.Run(() =>
        {
            try
            {
                using var src = Open(input, password, PdfDocumentOpenMode.Import);
                var idx = ParseRanges(orderSpec, src.PageCount);
                if (idx is null || idx.Count == 0) return TweakResult.Fail("Invalid order.", "次序無效。");
                if (idx.Count != src.PageCount || idx.Distinct().Count() != src.PageCount)
                    return TweakResult.Fail(
                        $"The order must list each of the {src.PageCount} pages exactly once.",
                        $"次序必須將全部 {src.PageCount} 頁各列出一次。");
                using var outDoc = new PdfDocument();
                foreach (var i in idx) outDoc.AddPage(src.Pages[i]);
                outDoc.Save(output);
                return TweakResult.Ok("Pages reordered.", "已重新排序頁面。", output);
            }
            catch (Exception ex) { return Err(ex); }
        });

    // ─────────────────────────────────────────────────────────────────────────
    // 浮水印 · Text watermark on every page
    // ─────────────────────────────────────────────────────────────────────────

    public static Task<TweakResult> WatermarkAsync(string input, string output, string text,
        double opacity, double angleDegrees, double fontSize, string? password = null)
        => Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text)) return TweakResult.Fail("Watermark text is empty.", "浮水印文字係空。");
                opacity = Math.Clamp(opacity, 0.02, 1.0);
                using var doc = Open(input, password, PdfDocumentOpenMode.Modify);
                var font = new XFont("Arial", fontSize <= 0 ? 48 : fontSize, XFontStyleEx.Bold);
                var brush = new XSolidBrush(XColor.FromArgb((int)(opacity * 255), 128, 128, 128));
                foreach (var page in doc.Pages.OfType<PdfPage>())
                {
                    using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    var size = gfx.MeasureString(text, font);
                    gfx.TranslateTransform(page.Width.Point / 2, page.Height.Point / 2);
                    gfx.RotateTransform(-angleDegrees);
                    gfx.DrawString(text, font, brush, new XPoint(-size.Width / 2, size.Height / 4));
                }
                doc.Save(output);
                return TweakResult.Ok($"Watermark applied to {doc.PageCount} page(s).",
                    $"已為 {doc.PageCount} 頁加上浮水印。", output);
            }
            catch (Exception ex) { return Err(ex); }
        });

    // ─────────────────────────────────────────────────────────────────────────
    // 加密 / 解密 · Encrypt / Decrypt
    // ─────────────────────────────────────────────────────────────────────────

    public static Task<TweakResult> EncryptAsync(string input, string output, string userPassword, string? ownerPassword)
        => Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrEmpty(userPassword) && string.IsNullOrEmpty(ownerPassword))
                    return TweakResult.Fail("Enter at least one password.", "請輸入至少一個密碼。");
                using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
                var sec = doc.SecuritySettings;
                if (!string.IsNullOrEmpty(userPassword)) sec.UserPassword = userPassword;
                sec.OwnerPassword = string.IsNullOrEmpty(ownerPassword) ? userPassword : ownerPassword;
                doc.Save(output);
                return TweakResult.Ok("Encrypted (password set).", "已加密（已設定密碼）。", output);
            }
            catch (Exception ex) { return Err(ex); }
        });

    public static Task<TweakResult> DecryptAsync(string input, string output, string password)
        => Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrEmpty(password)) return TweakResult.Fail("Enter the PDF's password.", "請輸入 PDF 密碼。");
                using var doc = PdfReader.Open(input, password, PdfDocumentOpenMode.Modify);
                // Clearing both passwords removes the security on save → an unprotected copy.
                doc.SecuritySettings.UserPassword = string.Empty;
                doc.SecuritySettings.OwnerPassword = string.Empty;
                doc.Save(output);
                return TweakResult.Ok("Decrypted (password removed).", "已解密（已移除密碼）。", output);
            }
            catch (Exception ex) { return Err(ex, wrongPwHint: true); }
        });

    // ─────────────────────────────────────────────────────────────────────────
    // 抽取文字 · Extract all text (PdfPig)
    // ─────────────────────────────────────────────────────────────────────────

    public static Task<TweakResult> ExtractTextAsync(string input, string outputTxt, string? password = null)
        => Task.Run(() =>
        {
            try
            {
                var opts = string.IsNullOrEmpty(password) ? new ParsingOptions() : new ParsingOptions { Password = password };
                var sb = new StringBuilder();
                int pages = 0;
                using (var pdf = PigDocument.Open(input, opts))
                {
                    foreach (var page in pdf.GetPages())
                    {
                        sb.AppendLine(page.Text);
                        sb.AppendLine();
                        pages++;
                    }
                }
                File.WriteAllText(outputTxt, sb.ToString(), new UTF8Encoding(true));
                return TweakResult.Ok($"Extracted text from {pages} page(s).", $"已由 {pages} 頁抽取文字。", outputTxt);
            }
            catch (Exception ex) { return Err(ex); }
        });

    // ─────────────────────────────────────────────────────────────────────────
    // 圖片轉 PDF · Images → PDF (one image per page, scaled to fit)
    // ─────────────────────────────────────────────────────────────────────────

    public static Task<TweakResult> ImagesToPdfAsync(IReadOnlyList<string> images, string output)
        => Task.Run(() =>
        {
            try
            {
                if (images.Count == 0) return TweakResult.Fail("No images selected.", "未揀任何圖片。");
                using var doc = new PdfDocument();
                int ok = 0;
                foreach (var imgPath in images)
                {
                    XImage? img = null;
                    try { img = XImage.FromFile(imgPath); }
                    catch { continue; }
                    using (img)
                    {
                        var page = doc.AddPage();
                        // Page sized to the image (96 dpi → points): keep aspect, A-fit on its own page.
                        double wPt = img.PixelWidth * 72.0 / Math.Max(1, img.HorizontalResolution);
                        double hPt = img.PixelHeight * 72.0 / Math.Max(1, img.VerticalResolution);
                        if (double.IsNaN(wPt) || wPt <= 0) wPt = img.PixelWidth * 0.75;
                        if (double.IsNaN(hPt) || hPt <= 0) hPt = img.PixelHeight * 0.75;
                        page.Width = XUnit.FromPoint(wPt);
                        page.Height = XUnit.FromPoint(hPt);
                        using var gfx = XGraphics.FromPdfPage(page);
                        gfx.DrawImage(img, 0, 0, wPt, hPt);
                        ok++;
                    }
                }
                if (ok == 0) return TweakResult.Fail("None of the selected files could be read as images.", "揀咗嘅檔案都讀唔到做圖片。");
                doc.Save(output);
                return TweakResult.Ok($"Created a PDF from {ok} image(s).", $"已由 {ok} 張圖片建立 PDF。", output);
            }
            catch (Exception ex) { return Err(ex); }
        });

    // ─────────────────────────────────────────────────────────────────────────

    private static TweakResult Err(Exception ex, bool wrongPwHint = false)
    {
        var msg = ex.Message;
        bool pw = wrongPwHint || msg.Contains("password", StringComparison.OrdinalIgnoreCase)
                              || ex is PdfReaderException;
        return pw
            ? TweakResult.Fail($"Failed: {msg}  (wrong password, or the PDF is protected?)",
                               $"失敗：{msg}（密碼錯誤，或者 PDF 受保護？）", msg)
            : TweakResult.Fail($"Failed: {msg}", $"失敗：{msg}", msg);
    }

    private static string? BlankToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? DateOrNull(DateTime value)
        => value == DateTime.MinValue ? null : value.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);

    private static string Snippet(string text, int first, int queryLength)
    {
        if (first < 0) return "";
        int start = Math.Max(0, first - 80);
        int length = Math.Min(text.Length - start, queryLength + 160);
        var slice = CollapseWhitespace(text.Substring(start, length));
        if (start > 0) slice = "… " + slice;
        if (start + length < text.Length) slice += " …";
        return slice;
    }

    private static string CollapseWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        bool pendingSpace = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }
            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }
            sb.Append(c);
        }
        return sb.ToString().Trim();
    }
}
