using System;
using System.Collections.Generic;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Windows.UI;

namespace WinForge.Services;

/// <summary>
/// 共用文字匯出 · Shared plain-text / PDF export helper.
/// 由條款閱讀器抽出嚟，畀 <see cref="WinForge.Controls.RichTextToolbar"/> 同 <see cref="TermsService"/> 共用，
/// 避免重複實作 PDF 渲染、自動換行同字型清單。
/// Extracted from the terms reader so both the reusable rich-text toolbar and TermsService share one
/// implementation of PDF rendering, word-wrap and the curated font list (no duplication).
/// All work here is pure CPU/IO — callers should run <see cref="RenderPdf"/> on a background thread.
/// </summary>
public static class TextExportService
{
    /// <summary>精選字型清單（涵蓋西文同 CJK）· Curated font list (covers Latin + CJK surfaces).</summary>
    public static readonly string[] FontChoices =
    {
        "Segoe UI", "Segoe UI Variable", "Microsoft JhengHei UI", "Microsoft JhengHei",
        "PMingLiU", "DFKai-SB", "Cambria", "Georgia", "Times New Roman",
        "Consolas", "Cascadia Mono", "Arial", "Verdana", "Calibri",
    };

    /// <summary>
    /// 用 PdfSharp 將純文字渲染成 PDF，保留字型／字級／粗斜／刪除線／顏色 ·
    /// Render plain text to PDF honouring family / size / bold / italic / strikethrough / colour,
    /// with manual word-wrap, pagination and a CJK-safe font fallback.
    /// </summary>
    public static void RenderPdf(string path, string text, string family, double size,
        bool bold, bool italic, bool strike, Color? color)
    {
        var style = XFontStyleEx.Regular;
        if (bold) style |= XFontStyleEx.Bold;
        if (italic) style |= XFontStyleEx.Italic;
        if (strike) style |= XFontStyleEx.Strikeout;

        XFont font;
        try { font = new XFont(family, size, style); }
        catch { font = new XFont("Microsoft JhengHei", size, style); }   // CJK-safe fallback

        var brush = color is { } c ? new XSolidBrush(XColor.FromArgb(c.A, c.R, c.G, c.B)) : XBrushes.Black;

        using var doc = new PdfDocument();
        const double margin = 56;
        double lineHeight = size * 1.45;

        PdfPage page = doc.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        double maxWidth = page.Width.Point - margin * 2;
        double y = margin;

        foreach (var rawLine in (text ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            // 逐字／逐詞自動換行 · word-wrap each paragraph line to the page width.
            foreach (var visualLine in WrapLine(gfx, rawLine, font, maxWidth))
            {
                if (y + lineHeight > page.Height.Point - margin)
                {
                    gfx.Dispose();
                    page = doc.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = margin;
                }
                if (visualLine.Length > 0)
                    gfx.DrawString(visualLine, font, brush, new XPoint(margin, y));
                y += lineHeight;
            }
        }
        gfx.Dispose();
        doc.Save(path);
    }

    /// <summary>喺最近嘅空格切（西文），冇就硬切（中文）· Break at the last space (Latin), else hard-break (CJK).</summary>
    public static IEnumerable<string> WrapLine(XGraphics gfx, string line, XFont font, double maxWidth)
    {
        if (string.IsNullOrEmpty(line)) { yield return ""; yield break; }

        var current = new System.Text.StringBuilder();
        foreach (var ch in line)
        {
            var trial = current.ToString() + ch;
            if (gfx.MeasureString(trial, font).Width > maxWidth && current.Length > 0)
            {
                string s = current.ToString();
                int sp = s.LastIndexOf(' ');
                if (sp > 0)
                {
                    yield return s.Substring(0, sp);
                    current.Clear();
                    current.Append(s.Substring(sp + 1));
                }
                else
                {
                    yield return s;
                    current.Clear();
                }
            }
            current.Append(ch);
        }
        if (current.Length > 0) yield return current.ToString();
    }
}
