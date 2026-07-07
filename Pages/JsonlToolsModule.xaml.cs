using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// JSONL / NDJSON 工具 · JSONL / NDJSON tools — validate, convert to/from a JSON array, pretty-print or
/// minify each line. Pure managed System.Text.Json; robust (a bad line is reported, never throws). Bilingual.
/// </summary>
public sealed partial class JsonlToolsModule : Page
{
    public JsonlToolsModule()
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
        Header.Title = "JSONL Tools · JSONL 工具";
        HeaderBlurb.Text = P("Work with JSONL / NDJSON — one JSON value per line. Validate every line, wrap them into a JSON array, split an array back into lines, or pretty-print / minify each record.",
            "處理 JSONL / NDJSON — 每行一個 JSON 值。逐行驗證、包成 JSON 陣列、將陣列拆返做多行，又或者將每筆記錄美化 / 壓縮。");
        InputLabel.Text = P("Input (one JSON value per line, or a JSON array)", "輸入（每行一個 JSON 值，或者一個 JSON 陣列）");
        OutputLabel.Text = P("Output", "輸出");
        ValidateBtn.Content = P("Validate", "驗證");
        ToArrayBtn.Content = P("JSONL → JSON array", "JSONL → JSON 陣列");
        FromArrayBtn.Content = P("JSON array → JSONL", "JSON 陣列 → JSONL");
        PrettyBtn.Content = P("Pretty each line", "逐行美化");
        MinifyBtn.Content = P("Minify each line", "逐行壓縮");
        CopyBtn.Content = P("Copy output", "複製輸出");
        UpdateRecordCount();
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e) => UpdateRecordCount();

    private void UpdateRecordCount()
    {
        try
        {
            string text = (InputBox?.Text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            int n = 0;
            foreach (string line in text.Split('\n'))
                if (line.Trim().Length > 0) n++;
            if (StatusText != null && (ErrorsList == null || ErrorsList.ItemsSource == null))
                StatusText.Text = P($"{n} non-blank line(s).", $"{n} 行（非空白）。");
        }
        catch { /* never throw from status */ }
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        var r = JsonlToolsService.Validate(InputBox?.Text);
        if (!r.Ok)
        {
            ShowErrors(r.Errors);
            StatusText.Text = P("Could not validate the input.", "無法驗證輸入。");
            return;
        }
        ShowErrors(r.Errors);
        if (r.InvalidLines == 0)
            StatusText.Text = P($"All {r.ValidLines} line(s) are valid JSON.", $"全部 {r.ValidLines} 行都係有效 JSON。");
        else
            StatusText.Text = P($"{r.ValidLines} valid, {r.InvalidLines} invalid line(s).", $"{r.ValidLines} 行有效，{r.InvalidLines} 行無效。");
    }

    private void ToArray_Click(object sender, RoutedEventArgs e)
        => Apply(JsonlToolsService.ToArray(InputBox?.Text), P("Wrapped into a JSON array", "已包成 JSON 陣列"));

    private void FromArray_Click(object sender, RoutedEventArgs e)
        => Apply(JsonlToolsService.FromArray(InputBox?.Text), P("Split into JSONL", "已拆做 JSONL"));

    private void Pretty_Click(object sender, RoutedEventArgs e)
        => Apply(JsonlToolsService.PrettyEach(InputBox?.Text), P("Pretty-printed each line", "已逐行美化"));

    private void Minify_Click(object sender, RoutedEventArgs e)
        => Apply(JsonlToolsService.MinifyEach(InputBox?.Text), P("Minified each line", "已逐行壓縮"));

    private void Apply(JsonlToolsService.JsonlResult r, string verb)
    {
        try
        {
            if (!r.Ok)
            {
                ShowErrors(r.Errors);
                StatusText.Text = P("Operation failed — check the input.", "操作失敗 — 請檢查輸入。");
                return;
            }
            OutputBox.Text = r.Output ?? string.Empty;
            ShowErrors(r.Errors);
            string tail = r.InvalidLines > 0
                ? P($" ({r.InvalidLines} bad line(s) reported below)", $"（下面報告咗 {r.InvalidLines} 行有問題）")
                : string.Empty;
            StatusText.Text = $"{verb} — {P($"{r.Records} record(s)", $"{r.Records} 筆記錄")}{tail}";
        }
        catch (Exception ex)
        {
            StatusText.Text = P($"Unexpected error: {ex.Message}", $"意外錯誤：{ex.Message}");
        }
    }

    private void ShowErrors(List<string> errors)
    {
        if (ErrorsList == null) return;
        ErrorsList.ItemsSource = (errors != null && errors.Count > 0) ? errors : null;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox?.Text ?? string.Empty;
            if (text.Length == 0)
            {
                StatusText.Text = P("Nothing to copy yet.", "暫時無嘢可以複製。");
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Output copied to the clipboard.", "已複製輸出到剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P($"Copy failed: {ex.Message}", $"複製失敗：{ex.Message}");
        }
    }
}
