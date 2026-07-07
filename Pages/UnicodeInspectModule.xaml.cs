using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Unicode 檢查器 · Unicode Inspector — type/paste text, see each code point enumerated by
/// <see cref="System.Text.Rune"/> (correct surrogate handling) with U+, decimal, UTF-8/UTF-16 bytes,
/// Unicode category and flags. Summarizes why "length" differs (code points vs UTF-16 vs bytes) and
/// flags hidden / confusable characters (zero-width, BOM, RTL marks, no-break space). Click a row to
/// copy its \u escape. Pure managed; robust (never throws). Bilingual (English + 粵語).
/// </summary>
public sealed partial class UnicodeInspectModule : Page
{
    private readonly ObservableCollection<UnicodeInspectService.CodePointInfo> _rows = new();

    public UnicodeInspectModule()
    {
        InitializeComponent();
        RowsList.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); RefreshView(); };
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) { Render(); RefreshView(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = P("Unicode Inspector · Unicode 檢查器", "Unicode 檢查器 · Unicode Inspector");
            HeaderBlurb.Text = P("Type or paste any text to see every code point broken down — glyph, U+ value, decimal, UTF-8 / UTF-16 bytes, Unicode category and flags. Understand why a string's \"length\" differs, and catch hidden or confusable characters.",
                "打字或者貼上任何文字，逐個碼位拆解 — 字形、U+ 值、十進、UTF-8／UTF-16 位元組、Unicode 類別同標記。睇清楚點解字串「長度」唔同，仲可以揪出隱藏或者易混淆嘅字元。");
            InputLabel.Text = P("Text to inspect", "要檢查嘅文字");
            SampleBtn.Content = P("Load tricky sample", "載入刁鑽範例");
            ClearBtn.Content = P("Clear", "清除");
            SummaryTitle.Text = P("Length breakdown", "長度分析");
            HintText.Text = P("Tip: click any row to copy its \\u escape to the clipboard.", "提示：撳任何一列，就會複製佢嘅 \\u 轉義碼去剪貼簿。");
        }
        catch { }
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e) => RefreshView();

    private void RefreshView()
    {
        try
        {
            string text = InputBox?.Text ?? "";
            var result = UnicodeInspectService.Inspect(text, P);

            _rows.Clear();
            foreach (var r in result.Rows) _rows.Add(r);

            var t = result.Totals;
            SummaryText.Text = P(
                $"{t.CodePoints} code point(s)  ·  {t.Utf16Units} UTF-16 unit(s) (string.Length)  ·  {t.Utf8Bytes} UTF-8 byte(s)",
                $"{t.CodePoints} 個碼位  ·  {t.Utf16Units} 個 UTF-16 單位（string.Length）  ·  {t.Utf8Bytes} 個 UTF-8 位元組");

            if (result.Totals.HiddenCount > 0)
            {
                Info.Severity = InfoBarSeverity.Warning;
                Info.Title = P($"{result.Totals.HiddenCount} hidden / confusable character(s) found",
                    $"揪到 {result.Totals.HiddenCount} 個隱藏／易混淆字元");
                Info.Message = string.Join("\n", result.HiddenNotes);
                Info.IsOpen = true;
            }
            else if (t.CodePoints == 0)
            {
                Info.Severity = InfoBarSeverity.Informational;
                Info.Title = P("Nothing to inspect yet", "仲未有嘢檢查");
                Info.Message = P("Type or paste some text above.", "喺上面打字或者貼上文字啦。");
                Info.IsOpen = true;
            }
            else
            {
                Info.IsOpen = false;
            }
        }
        catch { }
    }

    private void Sample_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Mix: ASCII, accented + combining, no-break space, zero-width space, RTL mark,
            // BOM, an astral emoji (surrogate pair), a flag (regional indicators), CJK.
            InputBox.Text = "Á b​c‏d﻿\U0001F600\U0001F1ED\U0001F1F0測試";
            RefreshView();
        }
        catch { }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        try { InputBox.Text = ""; RefreshView(); } catch { }
    }

    private void Rows_ItemClick(object sender, ItemClickEventArgs e)
    {
        try
        {
            if (e.ClickedItem is not UnicodeInspectService.CodePointInfo info) return;
            var pkg = new DataPackage();
            pkg.SetText(info.Escape);
            Clipboard.SetContent(pkg);

            Info.Severity = InfoBarSeverity.Success;
            Info.Title = P("Copied", "已複製");
            Info.Message = P($"{info.CodePoint} — copied \"{info.Escape}\" to the clipboard.",
                $"{info.CodePoint} — 已複製「{info.Escape}」去剪貼簿。");
            Info.IsOpen = true;
        }
        catch { }
    }
}
