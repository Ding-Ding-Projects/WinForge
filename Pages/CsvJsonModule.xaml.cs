using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// CSV ⇄ JSON 轉換 · Two-way converter between CSV and JSON. RFC 4180-correct CSV
/// parsing (quoted fields, "" escapes, embedded delimiters/newlines) and pretty
/// System.Text.Json output. Live-converts as you type. Pure managed, bilingual.
/// </summary>
public sealed partial class CsvJsonModule : Page
{
    private bool _suppress;

    public CsvJsonModule()
    {
        InitializeComponent();
        // The self-contained runtime did not reliably convert ToggleSwitch.IsOn
        // from XAML. Preserve the default under the existing event guard.
        _suppress = true;
        HeaderSwitch.IsOn = true;
        _suppress = false;
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

            Header.Title = "CSV ⇄ JSON · CSV/JSON 轉換";
            HeaderBlurb.Text = P(
                "Convert between CSV and JSON, both ways. The CSV parser is RFC 4180-correct — quoted fields, escaped \"\" quotes, and delimiters or newlines inside quotes all work. Output is pretty-printed. Everything runs locally.",
                "喺 CSV 同 JSON 之間雙向轉換。個 CSV 解析器跟足 RFC 4180 — 引號欄位、「\"\"」轉義引號、引號入面嘅分隔符或者換行都處理到。輸出會靚仔排版。全部喺你部機本地運行。");

            DirectionLabel.Text = P("Direction", "方向");
            DelimiterLabel.Text = P("Delimiter", "分隔符");
            HeaderToggleLabel.Text = P("First row is header", "第一行係標題");
            InputLabel.Text = P("Input", "輸入");
            OutputLabel.Text = P("Output", "輸出");
            ConvertBtn.Content = P("Convert", "轉換");
            CopyBtn.Content = P("Copy", "複製");

            RebuildCombo(DirectionBox, new[]
            {
                P("CSV → JSON", "CSV → JSON"),
                P("JSON → CSV", "JSON → CSV"),
            });

            RebuildCombo(DelimiterBox, new[]
            {
                P("Comma ,", "逗號 ,"),
                P("Tab", "Tab 定位"),
                P("Semicolon ;", "分號 ;"),
                P("Pipe |", "直線 |"),
                P("Auto-detect", "自動偵測"),
            });

            _suppress = false;
            Convert();
        }
        catch (Exception ex)
        {
            _suppress = false;
            SafeStatus(ex.Message);
        }
    }

    // Rebuild a combo's items while preserving the selected index.
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
            case 1: return '\t';
            case 2: return ';';
            case 3: return '|';
            case 4: return CsvJsonService.DetectDelimiter(input);
            default: return ',';
        }
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) { if (!_suppress) Convert(); }
    private void Option_Changed(object sender, RoutedEventArgs e) { if (!_suppress) Convert(); }
    private void Convert_Click(object sender, RoutedEventArgs e) => Convert();

    private void Convert()
    {
        try
        {
            string input = InputBox?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                OutputBox.Text = string.Empty;
                StatusText.Text = P("Paste or type CSV / JSON to begin.", "貼上或者打入 CSV / JSON 開始。");
                return;
            }

            bool csvToJson = DirectionBox.SelectedIndex == 0;
            bool header = HeaderSwitch.IsOn;
            char delim = CurrentDelimiter(input);

            CsvJsonService.Result r = csvToJson
                ? CsvJsonService.CsvToJson(input, delim, header)
                : CsvJsonService.JsonToCsv(input, delim, header);

            if (r.Error != null)
            {
                OutputBox.Text = string.Empty;
                StatusText.Text = FriendlyError(r.Error, csvToJson);
                return;
            }

            OutputBox.Text = r.Output;
            StatusText.Text = P($"OK · {r.Rows} rows × {r.Cols} columns", $"完成 · {r.Rows} 行 × {r.Cols} 欄");
        }
        catch (Exception ex)
        {
            OutputBox.Text = string.Empty;
            SafeStatus(ex.Message);
        }
    }

    private string FriendlyError(string code, bool csvToJson)
    {
        switch (code)
        {
            case "root-not-array":
                return P("JSON must be an array — either of objects or of arrays.",
                         "JSON 一定要係一個陣列 — 物件陣列或者陣列嘅陣列都得。");
            case "mixed-array-shapes":
                return P("The JSON array mixes objects and arrays — use one shape throughout.",
                         "個 JSON 陣列又有物件又有陣列 — 由頭到尾用同一種形狀。");
            default:
                return csvToJson
                    ? P($"Could not parse CSV: {code}", $"無法解析 CSV：{code}")
                    : P($"Could not parse JSON: {code}", $"無法解析 JSON：{code}");
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
            StatusText.Text = P("Copied output to clipboard.", "已經複製輸出去剪貼簿。");
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
