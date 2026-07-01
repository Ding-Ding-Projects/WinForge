using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI; // Windows.UI.Color — qualify to avoid the bare Color ambiguity under Microsoft.UI
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 安全標頭計分 · HTTP security-header scorecard. Paste a raw "Name: Value" response header block,
/// Analyze grades the common browser-security headers (HSTS, CSP, X-Content-Type-Options, X-Frame-Options,
/// Referrer-Policy, Permissions-Policy, COOP, COEP), flags version-disclosure headers, and shows a coloured
/// letter grade plus a per-header ListView. All local; copies a text report. Never throws. Bilingual.
/// </summary>
public sealed partial class HeaderScoreModule : Page
{
    /// <summary>Binding-friendly row (classic {Binding} in the DataTemplate — no x:Bind).</summary>
    public sealed class RowVm
    {
        public string Header { get; set; } = "";
        public string Status { get; set; } = "";
        public string Value { get; set; } = "";
        public string Note { get; set; } = "";
        public string Advice { get; set; } = "";
        public Brush BadgeBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }

    private HeaderScoreService.Result? _last;

    public HeaderScoreModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Render();
        if (_last != null) ShowResult(_last); // relocalize existing result
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Security Header Score · 安全標頭計分";
        HeaderBlurb.Text = P(
            "Paste a website's raw HTTP response headers below, then Analyze. WinForge grades the browser-security headers (HSTS, CSP, X-Frame-Options and more), flags version-disclosure headers, and gives a letter grade. Everything runs locally — nothing is sent anywhere.",
            "喺下面貼上網站嘅原始 HTTP 回應標頭，再撳分析。WinForge 會為瀏覽器安全標頭（HSTS、CSP、X-Frame-Options 等）評分，標示洩露版本嘅標頭，並俾出等級。全部喺本機處理 — 唔會傳送任何資料。");
        InputLabel.Text = P("Raw response headers (one \"Name: Value\" per line)", "原始回應標頭（每行一條 \"Name: Value\"）");
        InputBox.PlaceholderText = P(
            "e.g.\nstrict-transport-security: max-age=63072000; includeSubDomains; preload\ncontent-security-policy: default-src 'self'\nx-content-type-options: nosniff",
            "例如：\nstrict-transport-security: max-age=63072000; includeSubDomains; preload\ncontent-security-policy: default-src 'self'\nx-content-type-options: nosniff");
        AnalyzeBtn.Content = P("Analyze", "分析");
        SampleBtn.Content = P("Load sample", "載入範例");
        CopyBtn.Content = P("Copy report", "複製報告");
    }

    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = HeaderScoreService.Analyze(InputBox.Text);
            _last = result;
            ShowResult(result);
            CopyBtn.IsEnabled = result.ParsedAny;
        }
        catch
        {
            // never throw to the UI
            CopyBtn.IsEnabled = false;
            SummaryText.Text = P("Could not analyze the input.", "無法分析輸入內容。");
            ResultCard.Visibility = Visibility.Visible;
        }
    }

    private void ShowResult(HeaderScoreService.Result result)
    {
        try
        {
            GradeText.Text = result.Grade;
            GradeBadge.Background = BrushFromHex(result.GradeHex);
            ScoreText.Text = P($"Score: {result.Score}/100", $"分數：{result.Score}/100");
            ScoreBar.Value = result.Score;
            ScoreBar.Foreground = BrushFromHex(result.GradeHex);
            SummaryText.Text = result.Summary;

            var items = new List<RowVm>();
            foreach (var r in result.Rows)
            {
                items.Add(new RowVm
                {
                    Header = r.Header,
                    Status = r.Status,
                    Value = r.Value,
                    Note = r.Note,
                    Advice = r.Advice,
                    BadgeBrush = BrushFromHex(r.BadgeHex)
                });
            }
            ResultsList.ItemsSource = items;
            ResultCard.Visibility = Visibility.Visible;
        }
        catch { /* never throw */ }
    }

    private void Sample_Click(object sender, RoutedEventArgs e)
    {
        InputBox.Text =
            "HTTP/2 200\n" +
            "server: nginx/1.24.0\n" +
            "content-type: text/html; charset=UTF-8\n" +
            "strict-transport-security: max-age=63072000; includeSubDomains; preload\n" +
            "content-security-policy: default-src 'self'; script-src 'self'\n" +
            "x-content-type-options: nosniff\n" +
            "x-frame-options: SAMEORIGIN\n" +
            "referrer-policy: strict-origin-when-cross-origin\n" +
            "x-powered-by: PHP/8.1.2\n";
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_last == null) return;
            var text = HeaderScoreService.BuildReport(_last);
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            CopyBtn.Content = P("Copied ✓", "已複製 ✓");
        }
        catch
        {
            CopyBtn.Content = P("Copy failed", "複製失敗");
        }
    }

    private static Brush BrushFromHex(string hex)
    {
        try
        {
            var c = ColorFromHex(hex);
            return new SolidColorBrush(c);
        }
        catch
        {
            return new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }
    }

    private static Color ColorFromHex(string hex)
    {
        hex = (hex ?? "").Trim().TrimStart('#');
        byte a = 0xFF, r = 0x80, g = 0x80, b = 0x80;
        if (hex.Length == 6)
        {
            r = Convert.ToByte(hex.Substring(0, 2), 16);
            g = Convert.ToByte(hex.Substring(2, 2), 16);
            b = Convert.ToByte(hex.Substring(4, 2), 16);
        }
        else if (hex.Length == 8)
        {
            a = Convert.ToByte(hex.Substring(0, 2), 16);
            r = Convert.ToByte(hex.Substring(2, 2), 16);
            g = Convert.ToByte(hex.Substring(4, 2), 16);
            b = Convert.ToByte(hex.Substring(6, 2), 16);
        }
        return Color.FromArgb(a, r, g, b);
    }
}
