using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// CSV 檢查同修復（RFC 4180）· CSV linter + repairer. Paste CSV, pick a delimiter (or
/// auto-detect), and check for ragged rows, unbalanced quotes, stray CR/LF, a UTF-8 BOM,
/// trailing whitespace, empty trailing lines and mixed line endings. The Repair button
/// rewrites a compliant document (quoting, escaping, CRLF, BOM strip, padded rows).
/// Pure managed, bilingual, never throws.
/// </summary>
public sealed partial class CsvLintModule : Page
{
    private bool _suppress;

    public CsvLintModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? s, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            _suppress = true;

            Header.Title = "CSV Linter · CSV 檢查修復";
            HeaderBlurb.Text = P(
                "Paste CSV and check it against RFC 4180 — ragged rows, unbalanced quotes, stray CR/LF, a UTF-8 BOM, trailing whitespace, empty trailing lines and mixed line endings. Then hit Repair to rewrite a clean, compliant file. Everything runs locally.",
                "貼上 CSV，對照 RFC 4180 檢查 — 欄位數目唔啱、引號唔對稱、多餘 CR/LF、UTF-8 BOM、尾部空白、空白行同埋換行符號唔一致。㩒「修復」就會重寫成乾淨合規嘅檔案。全部喺你部機本地運行。");

            DelimiterLabel.Text = P("Delimiter", "分隔符");
            InputLabel.Text = P("CSV input", "CSV 輸入");
            IssuesLabel.Text = P("Issues", "問題");
            OutputLabel.Text = P("Repaired output", "修復輸出");
            LintBtn.Content = P("Lint", "檢查");
            RepairBtn.Content = P("Repair", "修復");
            CopyBtn.Content = P("Copy", "複製");

            RebuildCombo(DelimiterBox, new[]
            {
                P("Comma ,", "逗號 ,"),
                P("Semicolon ;", "分號 ;"),
                P("Tab", "Tab 定位"),
                P("Pipe |", "直線 |"),
                P("Auto-detect", "自動偵測"),
            });

            _suppress = false;
            RunLint();
        }
        catch (Exception ex)
        {
            _suppress = false;
            SafeStatus(ex.Message);
        }
    }

    private static void RebuildCombo(ComboBox box, string[] items)
    {
        int keep = box.SelectedIndex;
        box.Items.Clear();
        foreach (var it in items) box.Items.Add(it);
        box.SelectedIndex = keep >= 0 && keep < items.Length ? keep : 0;
    }

    private char CurrentDelimiter(string input)
    {
        switch (DelimiterBox.SelectedIndex)
        {
            case 0: return ',';
            case 1: return ';';
            case 2: return '\t';
            case 3: return '|';
            case 4: return CsvLintService.DetectDelimiter(input);
            default: return ',';
        }
    }

    private void Option_Changed(object sender, RoutedEventArgs e) { if (!_suppress) RunLint(); }
    private void Lint_Click(object sender, RoutedEventArgs e) => RunLint();

    private void RunLint()
    {
        try
        {
            IssuesList.ItemsSource = null;
            string input = InputBox?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                StatusText.Text = P("Paste or type CSV to begin.", "貼上或者打入 CSV 開始。");
                return;
            }

            char delim = CurrentDelimiter(input);
            CsvLintService.LintResult r = CsvLintService.Lint(input, delim);

            if (r.Error != null)
            {
                SafeStatus(r.Error);
                return;
            }

            IssuesList.ItemsSource = r.Issues;

            if (r.IssueCount == 0)
                StatusText.Text = P($"Clean · {r.Rows} rows × {r.Cols} columns · no issues found.",
                                    $"乾淨 · {r.Rows} 行 × {r.Cols} 欄 · 冇發現問題。");
            else
                StatusText.Text = P($"{r.Rows} rows × {r.Cols} columns · {r.IssueCount} issue(s) found.",
                                    $"{r.Rows} 行 × {r.Cols} 欄 · 發現 {r.IssueCount} 個問題。");
        }
        catch (Exception ex)
        {
            SafeStatus(ex.Message);
        }
    }

    private void Repair_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string input = InputBox?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                OutputBox.Text = string.Empty;
                StatusText.Text = P("Nothing to repair yet.", "暫時冇嘢可以修復。");
                return;
            }

            char delim = CurrentDelimiter(input);
            CsvLintService.RepairResult r = CsvLintService.Repair(input, delim);

            if (r.Error != null)
            {
                OutputBox.Text = string.Empty;
                SafeStatus(r.Error);
                return;
            }

            OutputBox.Text = r.Output;

            string note = string.Empty;
            if (r.PaddedRows > 0) note += P($" · padded {r.PaddedRows} short row(s)", $" · 補齊咗 {r.PaddedRows} 行");
            if (r.TruncatedRows > 0) note += P($" · truncated {r.TruncatedRows} long row(s)", $" · 截短咗 {r.TruncatedRows} 行");

            StatusText.Text = P($"Repaired · {r.Rows} rows × {r.Cols} columns{note}",
                                $"已修復 · {r.Rows} 行 × {r.Cols} 欄{note}");
        }
        catch (Exception ex)
        {
            OutputBox.Text = string.Empty;
            SafeStatus(ex.Message);
        }
    }

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
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            StatusText.Text = P("Copied repaired CSV to clipboard.", "已經複製修復咗嘅 CSV 去剪貼簿。");
        }
        catch (Exception ex)
        {
            SafeStatus(ex.Message);
        }
    }

    private void SafeStatus(string msg)
    {
        try { StatusText.Text = P($"Error: {msg}", $"錯誤：{msg}"); } catch { }
    }
}
