using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 行工具 · Line Tools — input + output multiline boxes with a toolbar of per-line transforms
/// (number, prefix/suffix, quote, join/split, reverse, sort, dedupe, trim, shuffle). Live counts.
/// Pure managed via <see cref="LineToolsService"/>; every action is guarded and never throws. Bilingual.
/// </summary>
public sealed partial class LineToolsModule : Page
{
    public LineToolsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) { Render(); UpdateCount(); }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLanguageChanged;

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Line Tools · 行工具";
            HeaderBlurb.Text = P("Paste text into the input box and transform it line by line — number, prefix, quote, join, split, sort, dedupe, shuffle and more. Nothing leaves your PC.",
                "貼文字入輸入框，一行行咁處理 — 加編號、前綴、引號、合併、拆分、排序、去重、打亂等等。全程唔會離開你部電腦。");
            InputLabel.Text = P("Input", "輸入");
            OutputLabel.Text = P("Output", "輸出");
            PrefixLabel.Text = P("Prefix", "前綴");
            SuffixLabel.Text = P("Suffix", "後綴");
            DelimLabel.Text = P("Delimiter (join / split)", "分隔符（合併 / 拆分）");

            BtnNumberDot.Content = P("Number (1.)", "編號 (1.)");
            BtnNumberParen.Content = P("Number (1))", "編號 (1))");
            BtnRemoveNums.Content = P("Remove numbers", "移除編號");
            BtnPrefix.Content = P("Add prefix", "加前綴");
            BtnSuffix.Content = P("Add suffix", "加後綴");
            BtnQuotes.Content = P("Wrap in quotes", "加引號");
            BtnJoin.Content = P("Join lines", "合併行");
            BtnSplit.Content = P("Split on delimiter", "按分隔符拆分");
            BtnReverseChars.Content = P("Reverse chars", "反轉字元");
            BtnSort.Content = P("Sort A→Z", "排序 A→Z");
            BtnReverseOrder.Content = P("Reverse order", "反轉次序");
            BtnDedupe.Content = P("Deduplicate", "去重複");
            BtnRemoveEmpty.Content = P("Remove empty", "移除空行");
            BtnTrim.Content = P("Trim lines", "修剪空白");
            BtnShuffle.Content = P("Shuffle", "打亂");
            CopyBtn.Content = P("Copy output", "複製輸出");

            UpdateCount();
            if (string.IsNullOrEmpty(StatusText.Text))
                StatusText.Text = P("Ready.", "準備就緒。");
        }
        catch { /* never throw from UI render */ }
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e) => UpdateCount();

    private void UpdateCount()
    {
        try
        {
            var (lines, chars, words) = LineToolsService.Count(InputBox?.Text ?? string.Empty);
            CountText.Text = P($"{lines} lines · {words} words · {chars} chars",
                $"{lines} 行 · {words} 個字 · {chars} 個字元");
        }
        catch { /* ignore */ }
    }

    // Apply a transform: read input, run it, write output, report status.
    private void Apply(Func<string, string> op, string enName, string zhName)
    {
        try
        {
            string input = InputBox?.Text ?? string.Empty;
            OutputBox.Text = op(input) ?? string.Empty;
            var (lines, _, _) = LineToolsService.Count(OutputBox.Text);
            StatusText.Text = P($"{enName} — {lines} line(s) out.", $"{zhName} — 輸出 {lines} 行。");
        }
        catch
        {
            try { StatusText.Text = P("Something went wrong; input left unchanged.", "出咗少少問題；輸入冇改到。"); }
            catch { /* ignore */ }
        }
    }

    private void NumberDot_Click(object sender, RoutedEventArgs e) =>
        Apply(t => LineToolsService.NumberLines(t, false), "Numbered", "已加編號");

    private void NumberParen_Click(object sender, RoutedEventArgs e) =>
        Apply(t => LineToolsService.NumberLines(t, true), "Numbered", "已加編號");

    private void RemoveNums_Click(object sender, RoutedEventArgs e) =>
        Apply(LineToolsService.RemoveLineNumbers, "Removed line numbers", "已移除編號");

    private void Prefix_Click(object sender, RoutedEventArgs e) =>
        Apply(t => LineToolsService.AddPrefix(t, PrefixBox?.Text), "Prefixed", "已加前綴");

    private void Suffix_Click(object sender, RoutedEventArgs e) =>
        Apply(t => LineToolsService.AddSuffix(t, SuffixBox?.Text), "Suffixed", "已加後綴");

    private void Quotes_Click(object sender, RoutedEventArgs e) =>
        Apply(LineToolsService.WrapQuotes, "Quoted", "已加引號");

    private void Join_Click(object sender, RoutedEventArgs e) =>
        Apply(t => LineToolsService.JoinLines(t, DelimBox?.Text), "Joined", "已合併");

    private void Split_Click(object sender, RoutedEventArgs e) =>
        Apply(t => LineToolsService.SplitOn(t, DelimBox?.Text), "Split", "已拆分");

    private void ReverseChars_Click(object sender, RoutedEventArgs e) =>
        Apply(LineToolsService.ReverseChars, "Reversed chars", "已反轉字元");

    private void Sort_Click(object sender, RoutedEventArgs e) =>
        Apply(LineToolsService.Sort, "Sorted", "已排序");

    private void ReverseOrder_Click(object sender, RoutedEventArgs e) =>
        Apply(LineToolsService.ReverseOrder, "Reversed order", "已反轉次序");

    private void Dedupe_Click(object sender, RoutedEventArgs e) =>
        Apply(LineToolsService.Deduplicate, "Deduplicated", "已去重");

    private void RemoveEmpty_Click(object sender, RoutedEventArgs e) =>
        Apply(LineToolsService.RemoveEmpty, "Removed empty lines", "已移除空行");

    private void Trim_Click(object sender, RoutedEventArgs e) =>
        Apply(LineToolsService.TrimLines, "Trimmed", "已修剪");

    private void Shuffle_Click(object sender, RoutedEventArgs e) =>
        Apply(LineToolsService.Shuffle, "Shuffled", "已打亂");

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox?.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                StatusText.Text = P("Nothing to copy yet.", "暫時冇嘢可以複製。");
                return;
            }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            Clipboard.Flush();
            StatusText.Text = P("Output copied to the clipboard.", "已複製輸出到剪貼簿。");
        }
        catch
        {
            try { StatusText.Text = P("Couldn't reach the clipboard.", "用唔到剪貼簿。"); }
            catch { /* ignore */ }
        }
    }
}
