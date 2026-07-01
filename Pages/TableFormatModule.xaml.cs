using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 表格排版 · Table Formatter — paste a delimited (CSV / TSV / pipe) or Markdown / ASCII table and re-emit
/// it as an aligned monospace ASCII table or a clean Markdown table. First-row-header toggle, left/right/
/// center alignment, auto delimiter detection, CJK-width aware. Robust on ragged rows. Copy to clipboard.
/// Pure managed C#, no redirect, no external process. Bilingual (粵語).
/// </summary>
public sealed partial class TableFormatModule : Page
{
    private bool _suppress;

    public TableFormatModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildCombos();
        Render();
        if (string.IsNullOrEmpty(InputBox.Text))
            InputBox.Text = P(
                "Name, Role, Score\nAda, Engineer, 98\nGrace, Admiral, 100",
                "名, 職位, 分數\nAda, 工程師, 98\nGrace, 海軍上將, 100");
        Reformat();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) { Render(); Reformat(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void BuildCombos()
    {
        _suppress = true;
        try
        {
            int d = DelimBox.SelectedIndex, f = FormatBox.SelectedIndex, a = AlignBox.SelectedIndex;

            DelimBox.Items.Clear();
            DelimBox.Items.Add(P("Auto-detect", "自動偵測"));
            DelimBox.Items.Add(P("Comma (,)", "逗號 (,)"));
            DelimBox.Items.Add(P("Tab", "定位符 (Tab)"));
            DelimBox.Items.Add(P("Pipe (|)", "直線 (|)"));
            DelimBox.SelectedIndex = d < 0 ? 0 : d;

            FormatBox.Items.Clear();
            FormatBox.Items.Add(P("ASCII table", "ASCII 表格"));
            FormatBox.Items.Add(P("Markdown table", "Markdown 表格"));
            FormatBox.SelectedIndex = f < 0 ? 0 : f;

            AlignBox.Items.Clear();
            AlignBox.Items.Add(P("Left", "靠左"));
            AlignBox.Items.Add(P("Right", "靠右"));
            AlignBox.Items.Add(P("Center", "置中"));
            AlignBox.SelectedIndex = a < 0 ? 0 : a;
        }
        finally { _suppress = false; }
    }

    private void Render()
    {
        Header.Title = "Table Formatter · 表格排版";
        HeaderBlurb.Text = P(
            "Paste a messy CSV, TSV, pipe or Markdown / ASCII table — get a tidy, aligned ASCII or Markdown table back. Quotes tolerated, ragged rows padded, Chinese widths handled.",
            "貼一個亂糟糟嘅 CSV、TSV、直線或者 Markdown / ASCII 表格 — 幫你整返個對齊靚仔嘅 ASCII 或者 Markdown 表格。容忍引號、補齊唔齊嘅列、處理中文寬度。");
        InputLabel.Text = P("Input table", "輸入表格");
        DelimLabel.Text = P("Delimiter", "分隔符");
        FormatLabel.Text = P("Output format", "輸出格式");
        AlignLabel.Text = P("Alignment", "對齊方式");
        HeaderChk.Content = P("First row is a header", "第一列係標題");
        OutputLabel.Text = P("Formatted output", "排版結果");
        CopyBtn.Content = P("Copy", "複製");
        // Default header on first render only if unset (checkbox has no third state here).
        if (HeaderChk.IsChecked == null) HeaderChk.IsChecked = true;
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) { if (!_suppress) Reformat(); }

    private void Option_Changed(object sender, RoutedEventArgs e) { if (!_suppress) Reformat(); }

    private void Option_Changed(object sender, SelectionChangedEventArgs e) { if (!_suppress) Reformat(); }

    private void Reformat()
    {
        try
        {
            var delim = (TableFormatService.Delimiter)Math.Max(0, DelimBox.SelectedIndex);
            var format = (TableFormatService.OutputFormat)Math.Max(0, FormatBox.SelectedIndex);
            var align = (TableFormatService.Align)Math.Max(0, AlignBox.SelectedIndex);
            bool header = HeaderChk.IsChecked == true;

            List<List<string>> grid = TableFormatService.Parse(InputBox.Text, delim);
            string outText = TableFormatService.Render(grid, format, align, header);
            OutputBox.Text = outText;

            if (grid.Count == 0)
                StatusText.Text = P("Waiting for a table…", "等緊你貼表格…");
            else
            {
                int cols = grid[0].Count;
                StatusText.Text = P($"{grid.Count} rows × {cols} columns", $"{grid.Count} 列 × {cols} 欄");
            }
        }
        catch
        {
            StatusText.Text = P("Could not format that input.", "呢個輸入排唔到版。");
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? string.Empty;
            if (text.Length == 0) { StatusText.Text = P("Nothing to copy yet.", "而家冇嘢可以複製。"); return; }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            StatusText.Text = P("Copied to clipboard.", "已經複製到剪貼簿。");
        }
        catch
        {
            StatusText.Text = P("Copy failed.", "複製失敗。");
        }
    }
}
