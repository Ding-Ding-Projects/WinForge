using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// HAR 分析器 · HAR (HTTP Archive) analyzer — open or paste a .har file, parse log.entries with
/// System.Text.Json, then show a summary (totals, status-class + type breakdown, page load time)
/// and a filterable / sortable list of requests with coloured status codes, highlighting the
/// slowest and largest. Copy a text report. 全部託管 C#，出錯只會顯示狀態，唔會拋例外。
/// Pure managed; every failure surfaces as a bilingual status and never throws.
/// </summary>
public sealed partial class HarAnalyzerModule : Page
{
    private readonly ObservableCollection<HarAnalyzerService.HarEntry> _view = new();
    private HarAnalyzerService.HarResult? _result;
    private bool _ready;

    public HarAnalyzerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { EntriesList.ItemsSource = _view; Render(); _ready = true; };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "HAR Analyzer · HAR 分析器";
        HeaderBlurb.Text = P(
            "Open or paste an HTTP Archive (.har) — captured from a browser's Network tab — and analyze the requests: totals, status-code and resource-type breakdowns, page load time, and a filterable, sortable list with the slowest and largest calls highlighted.",
            "打開或者貼一個 HTTP Archive（.har）檔 — 由瀏覽器嘅網絡分頁匯出嘅 — 幫你分析啲請求：總數、狀態碼同資源類型分佈、頁面載入時間，仲有一個可以篩選同排序嘅列表，會標示最慢同最大嘅請求。");

        InputTitle.Text = P("Load a HAR", "載入 HAR");
        OpenBtn.Content = P("Open .har…", "打開 .har…");
        AnalyzePasteBtn.Content = P("Analyze pasted JSON", "分析貼上嘅 JSON");
        ClearBtn.Content = P("Clear", "清除");
        PasteBox.PlaceholderText = P("…or paste HAR JSON here", "…或者喺度貼 HAR JSON");

        SummaryTitle.Text = P("Summary", "摘要");
        FilterBox.PlaceholderText = P("Filter by URL contains…", "篩選 URL 包含…");
        ErrorsToggle.Header = P("4xx / 5xx only", "只睇 4xx / 5xx");
        CopyBtn.Content = P("Copy report", "複製報告");

        ColMethod.Text = P("Method", "方法");
        ColStatus.Text = P("Status", "狀態");
        ColUrl.Text = "URL";
        ColMime.Text = P("Type", "類型");
        ColSize.Text = P("Size", "大小");
        ColTime.Text = P("Time", "時間");

        BuildSortBox();
        if (_result is { Ok: true }) RenderSummary();
    }

    private void BuildSortBox()
    {
        int keep = SortBox.SelectedIndex;
        SortBox.Items.Clear();
        SortBox.Items.Add(P("Sort: original order", "排序：原本次序"));
        SortBox.Items.Add(P("Sort: largest first", "排序：最大先"));
        SortBox.Items.Add(P("Sort: slowest first", "排序：最慢先"));
        SortBox.SelectedIndex = keep < 0 ? 0 : keep;
    }

    private void ShowStatus(InfoBarSeverity sev, string message)
    {
        Status.Severity = sev;
        Status.Message = message;
        Status.IsOpen = true;
    }

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            OpenBtn.IsEnabled = false;
            var path = await FileDialogs.OpenFileAsync(new[] { new FileDialogs.Filter("HAR files", "*.har"), new FileDialogs.Filter("JSON files", "*.json"), new FileDialogs.Filter("All files", "*.*") }, P("Open a HAR file", "打開 HAR 檔"));
            if (string.IsNullOrEmpty(path)) return;
            ShowStatus(InfoBarSeverity.Informational, P("Reading…", "讀緊…"));
            var result = await HarAnalyzerService.AnalyzeFileAsync(path);
            Apply(result);
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Open failed: ", "打開失敗：") + ex.Message);
        }
        finally
        {
            OpenBtn.IsEnabled = true;
        }
    }

    private void AnalyzePaste_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = HarAnalyzerService.Analyze(PasteBox.Text);
            Apply(result);
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Analyze failed: ", "分析失敗：") + ex.Message);
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _result = null;
        _view.Clear();
        PasteBox.Text = "";
        SummaryCard.Visibility = Visibility.Collapsed;
        ToolsRow.Visibility = Visibility.Collapsed;
        ListCard.Visibility = Visibility.Collapsed;
        Status.IsOpen = false;
    }

    private void Apply(HarAnalyzerService.HarResult result)
    {
        _result = result;
        if (!result.Ok)
        {
            _view.Clear();
            SummaryCard.Visibility = Visibility.Collapsed;
            ToolsRow.Visibility = Visibility.Collapsed;
            ListCard.Visibility = Visibility.Collapsed;
            ShowStatus(InfoBarSeverity.Error, P("Could not analyze: ", "分析唔到：") + result.Error);
            return;
        }

        RenderSummary();
        SummaryCard.Visibility = Visibility.Visible;
        ToolsRow.Visibility = Visibility.Visible;
        ListCard.Visibility = Visibility.Visible;
        RefreshList();
        ShowStatus(InfoBarSeverity.Success,
            P($"Analyzed {result.TotalRequests} requests.", $"分析咗 {result.TotalRequests} 個請求。"));
    }

    private void RenderSummary()
    {
        var r = _result!;
        SummaryLine.Text = P(
            $"{r.TotalRequests} requests · {HarAnalyzerService.HumanBytes(r.TotalBytes)} transferred · {r.TotalTimeMs:0} ms total"
                + (r.PageLoadMs > 0 ? $" · page load {r.PageLoadMs:0} ms" : ""),
            $"{r.TotalRequests} 個請求 · 傳咗 {HarAnalyzerService.HumanBytes(r.TotalBytes)} · 合共 {r.TotalTimeMs:0} 毫秒"
                + (r.PageLoadMs > 0 ? $" · 頁面載入 {r.PageLoadMs:0} 毫秒" : ""));

        StatusBreakdown.Text = P(
            $"2xx: {r.C2xx}    3xx: {r.C3xx}    4xx: {r.C4xx}    5xx: {r.C5xx}" + (r.COther > 0 ? $"    other: {r.COther}" : ""),
            $"2xx：{r.C2xx}    3xx：{r.C3xx}    4xx：{r.C4xx}    5xx：{r.C5xx}" + (r.COther > 0 ? $"    其他：{r.COther}" : ""));

        var types = string.Join("    ", r.ByType.OrderByDescending(k => k.Value).Select(k => $"{k.Key}: {k.Value}"));
        TypeBreakdown.Text = (r.ByType.Count == 0) ? "" : P("By type — ", "按類型 — ") + types;

        string hi = "";
        if (r.Slowest is not null)
            hi += P($"Slowest: {r.Slowest.TimeDisplay} ({Short(r.Slowest.Url)})", $"最慢：{r.Slowest.TimeDisplay}（{Short(r.Slowest.Url)}）");
        if (r.Largest is not null)
            hi += (hi.Length > 0 ? "    " : "") + P($"Largest: {r.Largest.SizeDisplay} ({Short(r.Largest.Url)})", $"最大：{r.Largest.SizeDisplay}（{Short(r.Largest.Url)}）");
        HighlightLine.Text = hi;
    }

    private static string Short(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        return url.Length <= 60 ? url : url[..57] + "…";
    }

    private void Filter_Changed(object sender, RoutedEventArgs e) => RefreshList();
    private void Filter_Changed(object sender, TextChangedEventArgs e) => RefreshList();
    private void Sort_Changed(object sender, SelectionChangedEventArgs e) => RefreshList();

    private void RefreshList()
    {
        if (!_ready || _result is not { Ok: true } r) return;

        IEnumerable<HarAnalyzerService.HarEntry> items = r.Entries;

        var needle = (FilterBox.Text ?? "").Trim();
        if (needle.Length > 0)
            items = items.Where(x => x.Url.Contains(needle, StringComparison.OrdinalIgnoreCase));

        if (ErrorsToggle.IsOn)
            items = items.Where(x => x.Status >= 400 && x.Status < 600);

        items = SortBox.SelectedIndex switch
        {
            1 => items.OrderByDescending(x => x.Size),
            2 => items.OrderByDescending(x => x.TimeMs),
            _ => items,
        };

        _view.Clear();
        foreach (var it in items) _view.Add(it);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_result is not { Ok: true } r)
            {
                ShowStatus(InfoBarSeverity.Warning, P("Nothing to copy yet.", "暫時冇嘢可以複製。"));
                return;
            }
            var report = HarAnalyzerService.BuildReport(r, P);
            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(report);
            Clipboard.SetContent(pkg);
            ShowStatus(InfoBarSeverity.Success, P("Report copied to clipboard.", "報告已複製到剪貼簿。"));
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Copy failed: ", "複製失敗：") + ex.Message);
        }
    }
}
