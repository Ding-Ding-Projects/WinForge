using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI; // Color (avoid the bare-Color ambiguity under Microsoft.UI)
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 語意化版本範圍測試器 · Semver range tester. Enter a node-semver range and a list of versions;
/// see which satisfy it (coloured badge + reason), sort by precedence, and read the highest match.
/// Pure managed, never throws — all parse failures surface as bilingual status. Bilingual (Cantonese).
/// </summary>
public sealed partial class SemverRangeModule : Page
{
    /// <summary>Row shown in the results ListView. Uses classic {Binding} (no x:Bind in DataTemplates).</summary>
    public sealed class Row
    {
        public string Title { get; set; } = string.Empty;
        public string Badge { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public Brush BadgeBrush { get; set; } = new SolidColorBrush(Colors.Gray);
    }

    private static readonly Brush PassBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x2E, 0x7D, 0x32)); // green
    private static readonly Brush FailBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xB0, 0x00, 0x20)); // red
    private static readonly Brush BadBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x8A, 0x6D, 0x00));  // amber

    private readonly ObservableCollection<Row> _rows = new();

    public SemverRangeModule()
    {
        InitializeComponent();
        Results.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Semver Range Tester · 語意化版本範圍測試器";
        HeaderBlurb.Text = P("Test versions against a node-semver range — exact, >= > <= <, caret ^, tilde ~, x-ranges (1.2.x), hyphen ranges (1.2.3 - 2.3.4), OR with ||, AND by space, prereleases. Everything is parsed here, offline.",
            "用 node-semver 範圍嚟測試版本 — 精確、>= > <= <、caret ^、tilde ~、x 範圍（1.2.x）、連字號範圍（1.2.3 - 2.3.4）、用 || 做「或」、用空格做「且」、支援預發布標籤。全部喺本機離線分析。");
        RangeLabel.Text = P("Range", "範圍");
        VersionsLabel.Text = P("Versions (one per line)", "版本（每行一個）");
        TestBtn.Content = P("Test", "測試");
        SortBtn.Content = P("Sort versions", "排序版本");
        CopyBtn.Content = P("Copy results", "複製結果");
        HighestLabel.Text = P("Highest satisfying version", "最高符合版本");
        if (string.IsNullOrEmpty(HighestValue.Text)) HighestValue.Text = "—";
        UpdateNormalized();
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) => UpdateNormalized();

    private void UpdateNormalized()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(RangeBox.Text))
            {
                NormalizedText.Text = string.Empty;
                return;
            }
            if (SemverRangeService.TryParseRange(RangeBox.Text, out var range, out var err))
                NormalizedText.Text = P("Normalized: ", "正規化：") + range.Normalized;
            else
                NormalizedText.Text = P("Cannot parse range: ", "無法解析範圍：") + err;
        }
        catch (Exception ex) { NormalizedText.Text = "⚠ " + ex.Message; }
    }

    private static IEnumerable<string> Lines(string? text)
        => (text ?? string.Empty)
            .Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0);

    private void ShowStatus(InfoBarSeverity sev, string title)
    {
        Status.Severity = sev;
        Status.Title = title;
        Status.IsOpen = true;
    }

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _rows.Clear();
            HighestValue.Text = "—";

            if (!SemverRangeService.TryParseRange(RangeBox.Text ?? string.Empty, out var range, out var rerr))
            {
                ShowStatus(InfoBarSeverity.Error, P("Invalid range: ", "範圍無效：") + rerr);
                return;
            }

            var lines = Lines(VersionsBox.Text).ToList();
            if (lines.Count == 0)
            {
                ShowStatus(InfoBarSeverity.Warning, P("Enter at least one version to test.", "請至少輸入一個版本嚟測試。"));
                return;
            }

            int pass = 0, invalid = 0;
            SemverRangeService.SemVer? highest = null;

            foreach (var line in lines)
            {
                if (!SemverRangeService.TryParse(line, out var v, out var verr))
                {
                    _rows.Add(new Row
                    {
                        Title = line,
                        Badge = P("BAD", "無效"),
                        Reason = P("Not a valid semver — ", "唔係有效語意化版本 — ") + verr,
                        BadgeBrush = BadBrush,
                    });
                    invalid++;
                    continue;
                }

                bool ok = range.Satisfies(v);
                if (ok)
                {
                    pass++;
                    if (highest is null || v.CompareTo(highest) > 0) highest = v;
                }

                _rows.Add(new Row
                {
                    Title = v.ToString(),
                    Badge = ok ? P("PASS", "符合") : P("FAIL", "唔符合"),
                    Reason = ok
                        ? P("Satisfies ", "符合 ") + range.Normalized
                        : P("Does not satisfy ", "唔符合 ") + range.Normalized
                          + (v.IsPrerelease ? P(" (prerelease not allowed by this range)", "（此範圍唔容許預發布版本）") : string.Empty),
                    BadgeBrush = ok ? PassBrush : FailBrush,
                });
            }

            HighestValue.Text = highest?.ToString() ?? P("(none)", "（無）");

            var summary = new StringBuilder();
            summary.Append(P($"{pass} satisfy", $"{pass} 個符合"))
                   .Append(" · ")
                   .Append(P($"{lines.Count - invalid} valid", $"{lines.Count - invalid} 個有效"));
            if (invalid > 0) summary.Append(" · ").Append(P($"{invalid} invalid", $"{invalid} 個無效"));
            ShowStatus(pass > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Informational, summary.ToString());
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Unexpected error: ", "發生錯誤：") + ex.Message);
        }
    }

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var lines = Lines(VersionsBox.Text).ToList();
            if (lines.Count == 0)
            {
                ShowStatus(InfoBarSeverity.Warning, P("Enter some versions to sort.", "請輸入啲版本嚟排序。"));
                return;
            }

            var sorted = SemverRangeService.SortVersions(lines, descending: false);
            int invalid = lines.Count - sorted.Count;
            if (sorted.Count == 0)
            {
                ShowStatus(InfoBarSeverity.Error, P("No valid versions to sort.", "無有效版本可排序。"));
                return;
            }

            VersionsBox.Text = string.Join(Environment.NewLine, sorted.Select(v => v.ToString()));

            var msg = P($"Sorted {sorted.Count} versions (ascending, prerelease-aware)", $"已排序 {sorted.Count} 個版本（升序，含預發布優先次序）");
            if (invalid > 0) msg += P($" · dropped {invalid} invalid", $" · 丟棄咗 {invalid} 個無效");
            ShowStatus(InfoBarSeverity.Success, msg);
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Unexpected error: ", "發生錯誤：") + ex.Message);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_rows.Count == 0)
            {
                ShowStatus(InfoBarSeverity.Warning, P("Nothing to copy — run a test first.", "無嘢可複製 — 請先做測試。"));
                return;
            }
            var sb = new StringBuilder();
            foreach (var r in _rows)
                sb.Append('[').Append(r.Badge).Append("] ").Append(r.Title).Append(" — ").AppendLine(r.Reason);

            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(sb.ToString());
            Clipboard.SetContent(dp);
            ShowStatus(InfoBarSeverity.Success, P("Results copied to clipboard.", "結果已複製到剪貼簿。"));
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Copy failed: ", "複製失敗：") + ex.Message);
        }
    }
}
