using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// HTML 表格轉換 · HTML table converter. Two directions:
/// Data→HTML (CSV/TSV/pipe → clean &lt;table&gt; with thead/tbody) and
/// HTML→Data (parse an HTML &lt;table&gt; → CSV / TSV / Markdown). Pure managed, tolerant, bilingual.
/// </summary>
public sealed partial class HtmlTableModule : Page
{
    private bool _suppress;

    public HtmlTableModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); Convert(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private bool DataToHtml => DirCombo.SelectedIndex <= 0;

    private void Render()
    {
        _suppress = true;
        try
        {
            Header.Title = "HTML Table Convert · HTML 表格轉換";
            HeaderBlurb.Text = P(
                "Convert between delimited data and HTML tables. Paste CSV/TSV/pipe data to generate a clean <table>, or paste an HTML table to extract it as CSV, TSV or Markdown.",
                "喺分隔資料同 HTML 表格之間互轉。貼上 CSV/TSV/直線資料就會產生乾淨嘅 <table>，或者貼上 HTML 表格提取做 CSV、TSV 或者 Markdown。");

            DirLabel.Text = P("Direction", "方向");
            RebuildCombo(DirCombo, DirCombo.SelectedIndex < 0 ? 0 : DirCombo.SelectedIndex,
                P("Data → HTML", "資料 → HTML"),
                P("HTML → Data", "HTML → 資料"));

            DelimLabel.Text = P("Delimiter", "分隔符");
            RebuildCombo(DelimCombo, DelimCombo.SelectedIndex < 0 ? 0 : DelimCombo.SelectedIndex,
                P("Auto-detect", "自動偵測"),
                P("Comma (,)", "逗號 (,)"),
                P("Tab", "定位符 (Tab)"),
                P("Pipe (|)", "直線 (|)"));

            HeaderSwitch.Header = P("First row is the header", "第一列做標題");
            EscapeSwitch.Header = P("Escape cell HTML", "轉義儲存格 HTML");
            ClassLabel.Text = P("CSS class (optional)", "CSS 類別（可選）");

            OutFmtLabel.Text = P("Output format", "輸出格式");
            RebuildCombo(OutFmtCombo, OutFmtCombo.SelectedIndex < 0 ? 0 : OutFmtCombo.SelectedIndex,
                "CSV", "TSV", P("Markdown table", "Markdown 表格"));

            bool d2h = DataToHtml;
            InputTitle.Text = d2h ? P("Delimited data in", "輸入分隔資料") : P("HTML table in", "輸入 HTML 表格");
            OutputTitle.Text = d2h ? P("HTML output", "HTML 輸出") : P("Data output", "資料輸出");
            DataOptions.Visibility = d2h ? Visibility.Visible : Visibility.Collapsed;
            HtmlOptions.Visibility = d2h ? Visibility.Collapsed : Visibility.Visible;

            CopyButton.Content = P("Copy output", "複製輸出");
        }
        catch { /* tolerant */ }
        _suppress = false;
        Convert();
    }

    private static void RebuildCombo(ComboBox combo, int keep, params string[] items)
    {
        combo.Items.Clear();
        foreach (var it in items) combo.Items.Add(it);
        combo.SelectedIndex = (keep >= 0 && keep < items.Length) ? keep : 0;
    }

    private void Dir_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        Render();
    }

    private void Opt_Changed(object sender, object e)
    {
        if (_suppress) return;
        Convert();
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Convert();
    }

    private void Convert()
    {
        try
        {
            string input = InputBox?.Text ?? "";
            HtmlTableService.Grid grid;
            string output;

            if (DataToHtml)
            {
                var delim = (HtmlTableService.Delimiter)Math.Max(0, DelimCombo.SelectedIndex);
                grid = HtmlTableService.ParseDelimited(input, delim);
                output = HtmlTableService.ToHtml(
                    grid,
                    HeaderSwitch.IsOn,
                    EscapeSwitch.IsOn,
                    ClassBox?.Text);
            }
            else
            {
                grid = HtmlTableService.ParseHtml(input);
                var fmt = (HtmlTableService.OutFormat)Math.Max(0, OutFmtCombo.SelectedIndex);
                output = HtmlTableService.ToData(grid, fmt);
            }

            if (OutputBox != null) OutputBox.Text = output;
            StatsText.Text = P($"{grid.RowCount} rows × {grid.ColCount} cols",
                $"{grid.RowCount} 列 × {grid.ColCount} 欄");
        }
        catch
        {
            if (OutputBox != null) OutputBox.Text = "";
            StatsText.Text = P("Could not parse the input.", "無法解析輸入。");
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox?.Text ?? "";
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            CopyButton.Content = P("Copied ✓", "已複製 ✓");
        }
        catch { /* tolerant */ }
    }
}
