using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 行排序同去重 · Line Sort &amp; Dedupe — paste lines, pick a sort mode and toggles (dedupe,
/// case-insensitive, natural order, reverse, shuffle, drop blanks, trim), see live output plus
/// in/out line counts and duplicates removed. Copy result to clipboard. Pure managed, never throws.
/// </summary>
public sealed partial class TextSortModule : Page
{
    private bool _suppress;

    public TextSortModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); Recompute(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Line Sort & Dedupe · 行排序同去重";
            HeaderBlurb.Text = P(
                "Paste lines, then sort, de-duplicate, reverse, shuffle or clean them. Natural sort keeps numbers in human order (file2 before file10). Everything updates live — copy the result when you're happy.",
                "貼上一堆行，然後排序、去重、反轉、打亂或者清理。自然排序會令數字跟人腦順序排（file2 排喺 file10 前面）。全部即時更新 — 啱嘅時候撳複製就得。");

            ModeLabel.Text = P("Sort mode", "排序模式");
            int keep = ModeCombo.SelectedIndex;
            _suppress = true;
            ModeCombo.Items.Clear();
            ModeCombo.Items.Add(P("No sort (keep order)", "唔排序（保留原順序）"));
            ModeCombo.Items.Add(P("Sort A → Z", "排序 A → Z"));
            ModeCombo.Items.Add(P("Sort Z → A", "排序 Z → A"));
            ModeCombo.Items.Add(P("Natural sort (file2 < file10)", "自然排序（file2 < file10）"));
            ModeCombo.SelectedIndex = keep < 0 ? 1 : keep;
            _suppress = false;

            CaseChk.Header = P("Case-insensitive", "唔分大細楷");
            DedupeChk.Header = P("Remove duplicates", "移除重複行");
            TrimCompareChk.Header = P("Trim before comparing (dedupe)", "比較前先修剪（去重）");
            ReverseChk.Header = P("Reverse lines", "反轉行順序");
            ShuffleChk.Header = P("Shuffle (random)", "隨機打亂");
            RemoveBlankChk.Header = P("Remove blank lines", "移除空白行");
            TrimEachChk.Header = P("Trim each line", "修剪每一行");

            ApplyBtn.Content = P("Apply", "套用");
            ReshuffleBtn.Content = P("Re-shuffle", "再打亂");
            CopyBtn.Content = P("Copy output", "複製結果");
            ClearBtn.Content = P("Clear", "清除");

            InputLabel.Text = P("Input", "輸入");
            OutputLabel.Text = P("Output", "輸出");

            Recompute();
        }
        catch { /* never throw */ }
    }

    private TextSortService.SortMode CurrentMode() => ModeCombo.SelectedIndex switch
    {
        1 => TextSortService.SortMode.Ascending,
        2 => TextSortService.SortMode.Descending,
        3 => TextSortService.SortMode.Natural,
        _ => TextSortService.SortMode.None,
    };

    private void Recompute()
    {
        if (_suppress) return;
        try
        {
            var r = TextSortService.Transform(
                InputBox?.Text,
                CurrentMode(),
                caseInsensitive: CaseChk?.IsOn == true,
                removeDuplicates: DedupeChk?.IsOn == true,
                trimBeforeCompare: TrimCompareChk?.IsOn == true,
                reverse: ReverseChk?.IsOn == true,
                shuffle: ShuffleChk?.IsOn == true,
                removeBlank: RemoveBlankChk?.IsOn == true,
                trimEach: TrimEachChk?.IsOn == true);

            if (OutputBox != null) OutputBox.Text = r.Text;
            if (StatsText != null)
                StatsText.Text = P(
                    $"Lines in: {r.LinesIn}   ·   Lines out: {r.LinesOut}   ·   Duplicates removed: {r.DuplicatesRemoved}",
                    $"輸入行數：{r.LinesIn}   ·   輸出行數：{r.LinesOut}   ·   移除重複：{r.DuplicatesRemoved}");
        }
        catch { /* never throw */ }
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) => Recompute();

    private void Options_Changed(object sender, RoutedEventArgs e) => Recompute();

    private void Apply_Click(object sender, RoutedEventArgs e) => Recompute();

    private void Reshuffle_Click(object sender, RoutedEventArgs e) => Recompute();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        try { if (InputBox != null) InputBox.Text = string.Empty; } catch { }
        Recompute();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dp = new DataPackage();
            dp.SetText(OutputBox?.Text ?? string.Empty);
            Clipboard.SetContent(dp);
            if (StatsText != null) StatsText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch { /* never throw */ }
    }
}
