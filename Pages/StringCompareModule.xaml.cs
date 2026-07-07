using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 字串相似度 · String Compare module — enter two strings (A / B), toggle case-insensitive and
/// ignore-whitespace, and see Levenshtein / similarity % / Damerau–Levenshtein / Hamming /
/// Jaro–Winkler / longest common substring / longest common subsequence. Live compute, copy report.
/// Pure managed C#; never throws. Bilingual (粵語).
/// </summary>
public sealed partial class StringCompareModule : Page
{
    /// <summary>Row shown in the metrics list; bound by the DataTemplate.</summary>
    public sealed class MetricRow
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    private readonly List<(string Label, string Value)> _lastRows = new();

    public StringCompareModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        Recompute();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        Recompute();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "String Compare · 字串相似度";
        HeaderBlurb.Text = P("Compare two strings and see how similar they are — edit distance, similarity %, and several classic string metrics. Everything is computed locally, live as you type.",
            "比較兩段文字，睇下佢哋有幾似 — 編輯距離、相似度百分比同幾個經典字串指標。全部喺本機即時計算，一邊打一邊出。");
        LabelA.Text = P("String A", "字串 A");
        LabelB.Text = P("String B", "字串 B");
        CaseSwitch.Header = P("Case-insensitive", "唔理大小寫");
        CaseSwitch.OnContent = CaseSwitch.OffContent = string.Empty;
        WhitespaceSwitch.Header = P("Ignore whitespace", "唔理空白字元");
        WhitespaceSwitch.OnContent = WhitespaceSwitch.OffContent = string.Empty;
        MetricsTitle.Text = P("Metrics", "指標");
        CopyButton.Content = P("Copy report", "複製報告");
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) => Recompute();

    private void Option_Changed(object sender, RoutedEventArgs e) => Recompute();

    private void Recompute()
    {
        try
        {
            string a = InputA?.Text ?? string.Empty;
            string b = InputB?.Text ?? string.Empty;
            bool ignoreCase = CaseSwitch?.IsOn == true;
            bool ignoreWs = WhitespaceSwitch?.IsOn == true;

            var m = StringCompareService.Compute(a, b, ignoreCase, ignoreWs);

            _lastRows.Clear();
            _lastRows.Add((P("Length A / B", "長度 A / B"), $"{m.LenA} / {m.LenB}"));

            if (m.Truncated)
            {
                StatusText.Text = P($"One or both strings exceed {StringCompareService.MaxLen:N0} characters — the distance metrics are skipped to stay responsive. Hamming and Jaro–Winkler are still shown.",
                    $"其中一段（或兩段）超過 {StringCompareService.MaxLen:N0} 個字元 — 為咗保持流暢，略過咗距離指標。Hamming 同 Jaro–Winkler 照樣顯示。");
                _lastRows.Add((P("Levenshtein distance", "Levenshtein 編輯距離"), P("skipped", "已略過")));
                _lastRows.Add((P("Similarity", "相似度"), P("n/a", "n/a")));
                _lastRows.Add((P("Damerau–Levenshtein", "Damerau–Levenshtein 距離"), P("skipped", "已略過")));
            }
            else
            {
                _lastRows.Add((P("Levenshtein distance", "Levenshtein 編輯距離"), m.Levenshtein.ToString(CultureInfo.InvariantCulture)));
                _lastRows.Add((P("Similarity", "相似度"), double.IsNaN(m.SimilarityPct) ? P("n/a", "n/a") : m.SimilarityPct.ToString("0.0", CultureInfo.InvariantCulture) + "%"));
                _lastRows.Add((P("Damerau–Levenshtein", "Damerau–Levenshtein 距離"), m.Damerau.ToString(CultureInfo.InvariantCulture)));
                StatusText.Text = P("Computed locally — nothing leaves your PC.", "喺本機計算 — 冇任何資料離開你部電腦。");
            }

            _lastRows.Add((P("Hamming distance", "Hamming 距離"),
                m.Hamming < 0 ? P("n/a (lengths differ)", "n/a（長度唔同）") : m.Hamming.ToString(CultureInfo.InvariantCulture)));
            _lastRows.Add((P("Jaro–Winkler similarity", "Jaro–Winkler 相似度"),
                double.IsNaN(m.JaroWinkler) ? P("n/a", "n/a") : (m.JaroWinkler * 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "%"));

            if (!m.Truncated)
            {
                _lastRows.Add((P("Longest common substring", "最長共同子字串"), m.LongestCommonSubstring.ToString(CultureInfo.InvariantCulture)));
                _lastRows.Add((P("Longest common subsequence", "最長共同子序列"), m.LongestCommonSubsequence.ToString(CultureInfo.InvariantCulture)));
            }

            var items = new List<MetricRow>(_lastRows.Count);
            foreach (var (label, value) in _lastRows)
                items.Add(new MetricRow { Label = label, Value = value });
            MetricsList.ItemsSource = items;
        }
        catch
        {
            try { StatusText.Text = P("Could not compute — check your input.", "計唔到 — 請檢查輸入。"); }
            catch { /* never throw from the UI thread */ }
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string report = StringCompareService.BuildReport(_lastRows);
            var dp = new DataPackage();
            dp.SetText(report);
            Clipboard.SetContent(dp);
            StatusText.Text = P("Report copied to the clipboard.", "報告已複製到剪貼簿。");
        }
        catch
        {
            try { StatusText.Text = P("Could not copy the report.", "複製唔到報告。"); }
            catch { /* swallow */ }
        }
    }
}
