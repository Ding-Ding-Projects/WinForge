using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// JSON 分析器 · JSON Analyzer — paste JSON and get live structural stats (node/object/array counts,
/// nesting depth, key inventory with occurrence counts, value-type breakdown, sizes). Pure managed
/// System.Text.Json via <see cref="JsonStatService"/>. Never throws; invalid JSON shows a clear
/// bilingual error. Bilingual (English + 粵語). No redirect, no external process.
/// </summary>
public sealed partial class JsonStatModule : Page
{
    /// <summary>Row for the stats ListView — classic {Binding}, so plain public properties.</summary>
    public sealed class StatRow
    {
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
    }

    private JsonStatService.JsonStatResult? _last;

    public JsonStatModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) { Render(); Analyze(); }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) { Render(); RefreshResult(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "JSON Analyzer · JSON 分析器";
        HeaderBlurb.Text = P("Paste JSON to see live structural stats — node counts, nesting depth, value-type breakdown, sizes, and every unique key with its occurrence count.",
            "貼上 JSON 即刻睇到結構統計 — 節點數目、巢狀深度、值類型分佈、大細，同埋每個唯一鍵出現咗幾多次。");
        InputLabel.Text = P("JSON input", "JSON 輸入");
        CopyBtn.Content = P("Copy report", "複製報告");
        StatsLabel.Text = P("Statistics", "統計");
        KeysLabel.Text = P("Unique keys (by occurrences)", "唯一鍵（按出現次數）");
    }

    private void Json_TextChanged(object sender, TextChangedEventArgs e) => Analyze();

    private void Analyze()
    {
        try
        {
            _last = JsonStatService.Analyze(JsonBox?.Text);
        }
        catch (Exception ex)
        {
            _last = new JsonStatService.JsonStatResult { Ok = false, Error = ex.Message };
        }
        RefreshResult();
    }

    private void RefreshResult()
    {
        var r = _last;
        var rows = new List<StatRow>();

        if (r == null || (!r.Ok && r.Error == "empty"))
        {
            StatusText.Text = P("Waiting for JSON…", "等緊 JSON…");
            StatsList.ItemsSource = rows;
            KeysList.ItemsSource = new List<JsonStatService.KeyCount>();
            KeysEmpty.Text = "";
            return;
        }

        if (!r.Ok)
        {
            StatusText.Text = P("Invalid JSON — check the syntax. ", "JSON 無效 — 檢查下語法。 ")
                + (r.Error ?? "");
            StatsList.ItemsSource = rows;
            KeysList.ItemsSource = new List<JsonStatService.KeyCount>();
            KeysEmpty.Text = "";
            return;
        }

        StatusText.Text = P("Valid JSON — analyzed.", "JSON 有效 — 已分析。");

        rows.Add(new StatRow { Label = P("Total nodes", "節點總數"), Value = r.TotalNodes.ToString("N0") });
        rows.Add(new StatRow { Label = P("Objects", "物件數"), Value = r.ObjectCount.ToString("N0") });
        rows.Add(new StatRow { Label = P("Arrays", "陣列數"), Value = r.ArrayCount.ToString("N0") });
        rows.Add(new StatRow { Label = P("Maximum nesting depth", "最大巢狀深度"), Value = r.MaxDepth.ToString("N0") });
        rows.Add(new StatRow { Label = P("Total keys", "鍵總數"), Value = r.TotalKeys.ToString("N0") });
        rows.Add(new StatRow { Label = P("Unique keys", "唯一鍵數"), Value = r.UniqueKeys.ToString("N0") });
        rows.Add(new StatRow { Label = P("Strings", "字串"), Value = r.StringCount.ToString("N0") });
        rows.Add(new StatRow { Label = P("Numbers", "數字"), Value = r.NumberCount.ToString("N0") });
        rows.Add(new StatRow { Label = P("Booleans", "布林值"), Value = r.BooleanCount.ToString("N0") });
        rows.Add(new StatRow { Label = P("Nulls", "空值 (null)"), Value = r.NullCount.ToString("N0") });
        rows.Add(new StatRow { Label = P("Largest array length", "最長陣列長度"), Value = r.LargestArray.ToString("N0") });
        rows.Add(new StatRow { Label = P("Total string characters", "字串字元總數"), Value = r.StringChars.ToString("N0") });
        rows.Add(new StatRow { Label = P("Byte size (UTF-8)", "位元組大細 (UTF-8)"), Value = r.ByteSize.ToString("N0") });

        StatsList.ItemsSource = rows;

        KeysList.ItemsSource = r.Keys;
        KeysEmpty.Text = r.Keys.Count == 0
            ? P("No keys — the JSON has no objects.", "冇鍵 — 呢個 JSON 冇物件。")
            : "";
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var r = _last;
            if (r == null)
            {
                StatusText.Text = P("Nothing to copy.", "冇嘢可以複製。");
                return;
            }

            var sb = new StringBuilder();
            if (!r.Ok)
            {
                sb.AppendLine(P("Invalid JSON.", "JSON 無效。"));
                if (r.Error != null && r.Error != "empty") sb.AppendLine(r.Error);
            }
            else
            {
                sb.AppendLine(P("JSON Analyzer report", "JSON 分析器報告"));
                sb.AppendLine(P($"Total nodes: {r.TotalNodes}", $"節點總數：{r.TotalNodes}"));
                sb.AppendLine(P($"Objects: {r.ObjectCount}", $"物件數：{r.ObjectCount}"));
                sb.AppendLine(P($"Arrays: {r.ArrayCount}", $"陣列數：{r.ArrayCount}"));
                sb.AppendLine(P($"Maximum nesting depth: {r.MaxDepth}", $"最大巢狀深度：{r.MaxDepth}"));
                sb.AppendLine(P($"Total keys: {r.TotalKeys}", $"鍵總數：{r.TotalKeys}"));
                sb.AppendLine(P($"Unique keys: {r.UniqueKeys}", $"唯一鍵數：{r.UniqueKeys}"));
                sb.AppendLine(P($"Strings: {r.StringCount}", $"字串：{r.StringCount}"));
                sb.AppendLine(P($"Numbers: {r.NumberCount}", $"數字：{r.NumberCount}"));
                sb.AppendLine(P($"Booleans: {r.BooleanCount}", $"布林值：{r.BooleanCount}"));
                sb.AppendLine(P($"Nulls: {r.NullCount}", $"空值：{r.NullCount}"));
                sb.AppendLine(P($"Largest array length: {r.LargestArray}", $"最長陣列長度：{r.LargestArray}"));
                sb.AppendLine(P($"Total string characters: {r.StringChars}", $"字串字元總數：{r.StringChars}"));
                sb.AppendLine(P($"Byte size (UTF-8): {r.ByteSize}", $"位元組大細 (UTF-8)：{r.ByteSize}"));
                sb.AppendLine();
                sb.AppendLine(P("Unique keys (occurrences):", "唯一鍵（出現次數）："));
                foreach (var k in r.Keys)
                    sb.AppendLine($"  {k.Key} × {k.Count}");
            }

            var pkg = new DataPackage();
            pkg.SetText(sb.ToString());
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Report copied to clipboard.", "報告已複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Copy failed: ", "複製失敗：") + ex.Message;
        }
    }
}
