using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 欄位文字工具 · Column text tools — paste delimited text, then extract / delete / reorder / align into a
/// fixed-width table / transpose / trim its columns, and re-join with a chosen delimiter. Pure managed,
/// ragged rows padded, bad input → bilingual status, never throws. No redirect.
/// </summary>
public sealed partial class TextColumnsModule : Page
{
    public TextColumnsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Render();
    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;
    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Column Tools · 欄位文字工具";
        HeaderBlurb.Text = P("Paste delimited text, then extract, delete, reorder, align, transpose or trim its columns — and re-join with any delimiter. Ragged rows are padded automatically.",
            "貼上有分隔符嘅文字，然後抽取、刪除、重排、對齊、行列互換或者修剪各欄，再用任何分隔符重新拼返。長短唔一嘅列會自動補齊。");

        InputLabel.Text = P("Input text", "輸入文字");
        InDelimLabel.Text = P("Input delimiter", "輸入分隔符");
        OutDelimLabel.Text = P("Output delimiter", "輸出分隔符");
        IndexLabel.Text = P("Columns (1-based, e.g. 1,3,5-7)", "欄位（由 1 起計，例如 1,3,5-7）");

        FillDelims(InDelimBox);
        FillDelims(OutDelimBox);

        ExtractBtn.Content = P("Extract columns", "抽取欄位");
        DeleteBtn.Content = P("Delete column(s)", "刪除欄位");
        ReorderBtn.Content = P("Reorder to this order", "依此次序重排");
        AlignBtn.Content = P("Align to fixed-width table", "對齊成固定寬度表");
        TransposeBtn.Content = P("Transpose (rows ↔ columns)", "行列互換");
        TrimBtn.Content = P("Trim each cell", "修剪每格");

        OutputLabel.Text = P("Result", "結果");
        CopyBtn.Content = P("Copy", "複製");

        if (string.IsNullOrEmpty(StatusText.Text))
            StatusText.Text = P("Ready.", "準備就緒。");
    }

    private void FillDelims(ComboBox box)
    {
        int sel = box.SelectedIndex;
        box.Items.Clear();
        box.Items.Add(P("Tab", "定位字元（Tab）"));
        box.Items.Add(P("Comma  ,", "逗號  ,"));
        box.Items.Add(P("Pipe  |", "直線  |"));
        box.Items.Add(P("Semicolon  ;", "分號  ;"));
        box.Items.Add(P("Whitespace", "空白"));
        box.SelectedIndex = sel < 0 ? 0 : sel;
    }

    private TextColumnsService.Delimiter InDelim() => (TextColumnsService.Delimiter)Math.Max(0, InDelimBox.SelectedIndex);
    private TextColumnsService.Delimiter OutDelim() => (TextColumnsService.Delimiter)Math.Max(0, OutDelimBox.SelectedIndex);

    private List<List<string>>? ReadGrid()
    {
        var rows = TextColumnsService.Parse(InputBox.Text, InDelim());
        if (rows.Count == 0)
        {
            StatusText.Text = P("Nothing to process — paste some text first.", "冇嘢處理 — 請先貼上文字。");
            return null;
        }
        return rows;
    }

    private void Show(List<List<string>> rows, string ok)
    {
        OutputBox.Text = TextColumnsService.Render(rows, OutDelim());
        StatusText.Text = ok;
    }

    private void Fail(Exception ex)
    {
        StatusText.Text = P($"Couldn't complete that: {ex.Message}", $"未能完成：{ex.Message}");
    }

    // Operations that need the index/range spec share this resolver.
    private bool TryCols(int width, out List<int> cols)
    {
        cols = new List<int>();
        if (!TextColumnsService.ParseIndexSpec(IndexBox.Text, width, out cols, out string? bad))
        {
            StatusText.Text = string.IsNullOrEmpty(bad)
                ? P("Enter a column spec like \"1,3,5-7\".", "請輸入欄位範圍，例如「1,3,5-7」。")
                : P($"\"{bad}\" isn't a valid column for {width} column(s).", $"「{bad}」對於 {width} 欄嚟講唔係有效欄位。");
            return false;
        }
        return true;
    }

    private void Extract_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var rows = ReadGrid(); if (rows == null) return;
            if (!TryCols(rows[0].Count, out var cols)) return;
            Show(TextColumnsService.ExtractColumns(rows, cols),
                P($"Extracted {cols.Count} column(s).", $"已抽取 {cols.Count} 欄。"));
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var rows = ReadGrid(); if (rows == null) return;
            int width = rows[0].Count;
            if (!TryCols(width, out var cols)) return;
            var result = TextColumnsService.DeleteColumns(rows, cols);
            Show(result, P($"Deleted {cols.Count} column(s); {(result.Count == 0 ? 0 : result[0].Count)} left.",
                $"已刪除 {cols.Count} 欄；剩返 {(result.Count == 0 ? 0 : result[0].Count)} 欄。"));
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Reorder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var rows = ReadGrid(); if (rows == null) return;
            if (!TryCols(rows[0].Count, out var order)) return;
            Show(TextColumnsService.Reorder(rows, order),
                P("Reordered columns.", "已重排欄位。"));
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Align_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var rows = ReadGrid(); if (rows == null) return;
            Show(TextColumnsService.Align(rows),
                P("Aligned into a fixed-width table.", "已對齊成固定寬度表。"));
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Transpose_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var rows = ReadGrid(); if (rows == null) return;
            var result = TextColumnsService.Transpose(rows);
            Show(result, P($"Transposed — {result.Count} row(s) × {(result.Count == 0 ? 0 : result[0].Count)} column(s).",
                $"已行列互換 — {result.Count} 列 × {(result.Count == 0 ? 0 : result[0].Count)} 欄。"));
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Trim_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var rows = ReadGrid(); if (rows == null) return;
            Show(TextColumnsService.TrimCells(rows),
                P("Trimmed every cell.", "已修剪每一格。"));
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? string.Empty;
            if (text.Length == 0)
            {
                StatusText.Text = P("Nothing to copy yet.", "暫時冇嘢好複製。");
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch (Exception ex) { Fail(ex); }
    }
}
