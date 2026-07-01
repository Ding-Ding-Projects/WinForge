using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 字串檢查器 · String Inspector — paste text, see live char/byte/code-point/grapheme
/// stats, browse a per-code-point ListView (U+XXXX + char + Unicode category), and run
/// managed transforms (reverse, NFC/NFD/NFKC/NFKD, escape/unescape, strip diacritics,
/// remove non-ASCII). Pure-managed, robust, bilingual (粵語).
/// </summary>
public sealed partial class StringInspectorModule : Page
{
    private bool _suppress;

    public StringInspectorModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) { Render(); Recompute(); }
    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;
    private void OnLang(object? sender, EventArgs e) { Render(); Recompute(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "String Inspector · 字串檢查器";
        HeaderBlurb.Text = P(
            "Paste or type any text to see its length in characters, code points, graphemes, words and lines, plus UTF-8 / UTF-16 / UTF-32 byte sizes. Browse every code point and run handy transforms.",
            "貼上或者打任何文字，即刻睇到字元數、碼位、字素、字數、行數，仲有 UTF-8 / UTF-16 / UTF-32 位元組大細。逐個碼位睇曬，仲可以做各種轉換。");
        InputLabel.Text = P("Text", "文字");
        CopyBtn.Content = P("Copy", "複製");
        ClearBtn.Content = P("Clear", "清除");
        StatsTitle.Text = P("Statistics", "統計");
        TransformTitle.Text = P("Transforms", "轉換");
        ReverseBtn.Content = P("Reverse", "反轉");
        NfcBtn.Content = "NFC";
        NfdBtn.Content = "NFD";
        NfkcBtn.Content = "NFKC";
        NfkdBtn.Content = "NFKD";
        EscapeBtn.Content = P("Escape", "轉義");
        UnescapeBtn.Content = P("Unescape", "還原轉義");
        StripBtn.Content = P("Strip diacritics", "去除音標");
        AsciiBtn.Content = P("Remove non-ASCII", "移除非 ASCII");
        CodePointsTitle.Text = P("Code points", "碼位");
        // Re-render stats labels for the current text.
        if (!_suppress) Recompute();
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Recompute();
    }

    private string Text => InputBox?.Text ?? "";

    private void Recompute()
    {
        try
        {
            var s = StringInspectorService.Analyze(Text);
            StatsText.Text = string.Join("\n", new[]
            {
                P($"Characters (UTF-16 units): {s.Chars}", $"字元（UTF-16 單位）：{s.Chars}"),
                P($"Code points: {s.CodePoints}", $"碼位：{s.CodePoints}"),
                P($"Graphemes: {s.Graphemes}", $"字素：{s.Graphemes}"),
                P($"Words: {s.Words}    Lines: {s.Lines}", $"字數：{s.Words}    行數：{s.Lines}"),
                P($"UTF-8: {s.Utf8Bytes} B    UTF-16: {s.Utf16Bytes} B    UTF-32: {s.Utf32Bytes} B",
                  $"UTF-8：{s.Utf8Bytes} B    UTF-16：{s.Utf16Bytes} B    UTF-32：{s.Utf32Bytes} B"),
            });

            List<StringInspectorService.CodePointRow> rows = StringInspectorService.CodePoints(Text);
            CodePointList.ItemsSource = rows;

            if (s.Chars == 0)
                SetStatus(P("Enter some text above.", "喺上面輸入啲文字。"));
            else if (rows.Count >= 4096)
                SetStatus(P("Showing the first 4096 code points.", "只顯示頭 4096 個碼位。"));
            else
                SetStatus("");
        }
        catch (Exception ex)
        {
            SetStatus(P("Could not analyze the text: ", "無法分析文字：") + ex.Message);
        }
    }

    private void SetStatus(string msg) => StatusText.Text = msg;

    // ---- Transform button handlers (each replaces the text, robust) --------

    private void Apply(Func<string, string> transform, string okEn, string okZh)
    {
        try
        {
            string result = transform(Text);
            _suppress = true;
            InputBox.Text = result;
            InputBox.SelectionStart = result.Length;
            _suppress = false;
            Recompute();
            SetStatus(P(okEn, okZh));
        }
        catch (Exception ex)
        {
            _suppress = false;
            SetStatus(P("Transform failed: ", "轉換失敗：") + ex.Message);
        }
    }

    private void Reverse_Click(object sender, RoutedEventArgs e)
        => Apply(StringInspectorService.Reverse, "Reversed.", "已反轉。");

    private void Nfc_Click(object sender, RoutedEventArgs e)
        => Apply(t => StringInspectorService.Normalize(t, NormalizationForm.FormC), "Normalized to NFC.", "已正規化為 NFC。");

    private void Nfd_Click(object sender, RoutedEventArgs e)
        => Apply(t => StringInspectorService.Normalize(t, NormalizationForm.FormD), "Normalized to NFD.", "已正規化為 NFD。");

    private void Nfkc_Click(object sender, RoutedEventArgs e)
        => Apply(t => StringInspectorService.Normalize(t, NormalizationForm.FormKC), "Normalized to NFKC.", "已正規化為 NFKC。");

    private void Nfkd_Click(object sender, RoutedEventArgs e)
        => Apply(t => StringInspectorService.Normalize(t, NormalizationForm.FormKD), "Normalized to NFKD.", "已正規化為 NFKD。");

    private void Escape_Click(object sender, RoutedEventArgs e)
        => Apply(StringInspectorService.Escape, "Escaped.", "已轉義。");

    private void Unescape_Click(object sender, RoutedEventArgs e)
        => Apply(StringInspectorService.Unescape, "Unescaped.", "已還原轉義。");

    private void Strip_Click(object sender, RoutedEventArgs e)
        => Apply(StringInspectorService.StripDiacritics, "Diacritics stripped.", "已去除音標。");

    private void Ascii_Click(object sender, RoutedEventArgs e)
        => Apply(StringInspectorService.RemoveNonAscii, "Non-ASCII removed.", "已移除非 ASCII。");

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _suppress = true;
        InputBox.Text = "";
        _suppress = false;
        Recompute();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Text.Length == 0) { SetStatus(P("Nothing to copy.", "冇嘢可以複製。")); return; }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(Text);
            Clipboard.SetContent(dp);
            SetStatus(P("Copied to clipboard.", "已複製到剪貼簿。"));
        }
        catch (Exception ex)
        {
            SetStatus(P("Copy failed: ", "複製失敗：") + ex.Message);
        }
    }
}
