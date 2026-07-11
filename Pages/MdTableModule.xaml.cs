using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Markdown 表格產生器 · Markdown Table generator — paste a delimited grid
/// (CSV/TSV/pipe/auto) or an existing Markdown table, choose per-column
/// alignment and padding, and get a GitHub-flavored Markdown table (or CSV/TSV
/// back out). Pure managed, robust, bilingual (粵語). Mirrors AwakeModule.
/// </summary>
public sealed partial class MdTableModule : Page
{
    /// <summary>One per column — a name + an alignment dropdown, bound in the DataTemplate.</summary>
    public sealed class ColAlign : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public List<string> Options { get; set; } = new();
        private int _selectedIndex;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set { if (_selectedIndex != value) { _selectedIndex = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedIndex))); } }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly ObservableCollection<ColAlign> _cols = new();
    private bool _suppress;

    public MdTableModule()
    {
        InitializeComponent();
        // The self-contained XAML runtime can reject typed IsOn literals.
        _suppress = true;
        HeaderSwitch.IsOn = true;
        PadSwitch.IsOn = true;
        _suppress = false;
        AlignHost.ItemsSource = _cols;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) { Render(); Regenerate(); }
    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLanguageChanged;
    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = P("Markdown Table", "Markdown 表格");
            HeaderBlurb.Text = P("Paste a CSV/TSV/pipe grid — or an existing Markdown table — and get a clean, aligned GitHub-flavored Markdown table. Reverse it back to CSV/TSV any time. Nothing leaves your PC.",
                "貼一個 CSV/TSV/直線分隔嘅表，或者貼返個現成 Markdown 表，就變出一個整齊、對齊好嘅 GitHub Markdown 表。想倒返轉去 CSV/TSV 都得。全部喺你部機度做，唔會外洩。");

            InputTitle.Text = P("Paste your data", "貼你嘅資料");
            DelimLabel.Text = P("Delimiter", "分隔符號");
            HeaderSwitch.Header = P("First row is a header", "第一行係標題");
            PadSwitch.Header = P("Pad columns for pretty source", "為靚嘅原始碼補上空格對齊");
            ReverseHintText.Text = P("Looks like a Markdown table — reverse mode: re-align it, or output as CSV/TSV below.",
                "睇落似個 Markdown 表 — 倒轉模式：可以重新對齊，或者喺下面輸出做 CSV/TSV。");

            AlignTitle.Text = P("Column alignment", "每欄對齊");
            AlignBlurb.Text = P("Set how each column aligns in the rendered table. Changes apply live.",
                "設定每一欄喺表入面點對齊。改咗即刻生效。");
            ApplyAlignBtn.Content = P("Re-generate", "重新產生");

            OutputTitle.Text = P("Result", "結果");
            CopyBtn.Content = P("Copy", "複製");
            OutputBox.PlaceholderText = P("Your table appears here.", "你嘅表會喺呢度出現。");
            InputBox.PlaceholderText = P("name, role, city\nAda, engineer, London", "姓名, 職位, 城市\nAda, 工程師, 倫敦");

            RebuildDelimCombo();
            RebuildOutFormatCombo();
            RebuildAlignOptions();
            RefreshCounts();
            SetStatus(P("Paste a grid or a Markdown table to begin.", "貼一個表或者 Markdown 表就可以開始。"), InfoBarSeverity.Informational);
        }
        catch { /* never throw from UI text */ }
    }

    private void RebuildDelimCombo()
    {
        _suppress = true;
        int sel = DelimCombo.SelectedIndex < 0 ? 3 : DelimCombo.SelectedIndex; // default Auto
        DelimCombo.Items.Clear();
        DelimCombo.Items.Add(P("Comma (,)", "逗號 (,)"));
        DelimCombo.Items.Add(P("Tab", "定位鍵 Tab"));
        DelimCombo.Items.Add(P("Pipe (|)", "直線 (|)"));
        DelimCombo.Items.Add(P("Auto-detect", "自動偵測"));
        DelimCombo.SelectedIndex = sel;
        _suppress = false;
    }

    private void RebuildOutFormatCombo()
    {
        _suppress = true;
        int sel = OutFormatCombo.SelectedIndex < 0 ? 0 : OutFormatCombo.SelectedIndex;
        OutFormatCombo.Items.Clear();
        OutFormatCombo.Items.Add(P("Markdown", "Markdown"));
        OutFormatCombo.Items.Add(P("CSV", "CSV"));
        OutFormatCombo.Items.Add(P("TSV", "TSV"));
        OutFormatCombo.SelectedIndex = sel;
        _suppress = false;
    }

    private List<string> AlignOptionLabels() => new()
    {
        P("Default", "預設"),
        P("Left", "靠左"),
        P("Center", "置中"),
        P("Right", "靠右"),
    };

    private void RebuildAlignOptions()
    {
        var labels = AlignOptionLabels();
        foreach (var c in _cols) c.Options = new List<string>(labels);
    }

    private MdTableService.Delimiter CurrentDelimiter() => DelimCombo.SelectedIndex switch
    {
        0 => MdTableService.Delimiter.Comma,
        1 => MdTableService.Delimiter.Tab,
        2 => MdTableService.Delimiter.Pipe,
        _ => MdTableService.Delimiter.Auto,
    };

    private static MdTableService.Align ToAlign(int index) => index switch
    {
        1 => MdTableService.Align.Left,
        2 => MdTableService.Align.Center,
        3 => MdTableService.Align.Right,
        _ => MdTableService.Align.None,
    };

    private static int ToIndex(MdTableService.Align a) => a switch
    {
        MdTableService.Align.Left => 1,
        MdTableService.Align.Center => 2,
        MdTableService.Align.Right => 3,
        _ => 0,
    };

    private void OnInputChanged(object sender, TextChangedEventArgs e) { if (!_suppress) Regenerate(); }
    private void OnOptionChanged(object sender, RoutedEventArgs e) { if (!_suppress) Regenerate(); }
    private void OnOptionChanged(object sender, SelectionChangedEventArgs e) { if (!_suppress) Regenerate(); }
    private void OnApplyAlign(object sender, RoutedEventArgs e) => Regenerate();

    /// <summary>Parse the current input, sync the per-column alignment list, render the output. Never throws.</summary>
    private void Regenerate()
    {
        try
        {
            string input = InputBox?.Text ?? string.Empty;
            bool reverse = MdTableService.LooksLikeMarkdownTable(input);
            if (ReverseHint != null) ReverseHint.Visibility = reverse ? Visibility.Visible : Visibility.Collapsed;

            MdTableService.Grid grid;
            List<MdTableService.Align> detectedAligns = new();
            bool firstRowHeader;

            if (reverse)
            {
                (grid, detectedAligns) = MdTableService.ParseMarkdown(input);
                firstRowHeader = true; // a markdown table always has a header row
            }
            else
            {
                char delim = MdTableService.ResolveDelimiter(CurrentDelimiter(), input);
                grid = MdTableService.ParseDelimited(input, delim);
                firstRowHeader = HeaderSwitch?.IsOn ?? true;
            }

            SyncColumns(grid.ColCount, detectedAligns, grid, firstRowHeader);

            var aligns = _cols.Select(c => ToAlign(c.SelectedIndex)).ToList();
            bool pad = PadSwitch?.IsOn ?? true;
            int outFormat = OutFormatCombo?.SelectedIndex ?? 0;

            string output = outFormat switch
            {
                1 => MdTableService.RenderDelimited(grid, ','),
                2 => MdTableService.RenderDelimited(grid, '\t'),
                _ => MdTableService.RenderMarkdown(grid, aligns, firstRowHeader, pad),
            };

            if (OutputBox != null) OutputBox.Text = output;
            RefreshCounts(grid, firstRowHeader, reverse);
        }
        catch (Exception ex)
        {
            SetStatus(P("Could not build the table: ", "整唔到個表：") + ex.Message, InfoBarSeverity.Error);
        }
    }

    /// <summary>Grow/shrink the per-column alignment editors to match the grid width.</summary>
    private void SyncColumns(int colCount, List<MdTableService.Align> detected, MdTableService.Grid grid, bool firstRowHeader)
    {
        var labels = AlignOptionLabels();
        List<string> headerNames = new();
        if (grid != null && grid.RowCount > 0 && firstRowHeader)
            headerNames = grid.Rows[0].Select(s => s ?? string.Empty).ToList();

        while (_cols.Count > colCount) _cols.RemoveAt(_cols.Count - 1);
        for (int i = 0; i < colCount; i++)
        {
            string name = i < headerNames.Count && headerNames[i].Trim().Length > 0
                ? headerNames[i].Trim()
                : P("Column ", "第 ") + (i + 1) + P("", " 欄");
            if (i < _cols.Count)
            {
                _cols[i].Name = name;
            }
            else
            {
                var col = new ColAlign { Name = name, Options = new List<string>(labels), SelectedIndex = 0 };
                if (i < detected.Count) col.SelectedIndex = ToIndex(detected[i]);
                _cols.Add(col);
            }
        }
        // Refresh names on existing items so header edits show through.
        for (int i = 0; i < _cols.Count && i < colCount; i++)
        {
            string name = i < headerNames.Count && headerNames[i].Trim().Length > 0
                ? headerNames[i].Trim()
                : P("Column ", "第 ") + (i + 1) + P("", " 欄");
            _cols[i].Name = name;
        }
    }

    private void RefreshCounts(MdTableService.Grid? grid = null, bool firstRowHeader = false, bool reverse = false)
    {
        try
        {
            if (CountsText == null) return;
            if (grid == null || grid.RowCount == 0)
            {
                CountsText.Text = P("0 rows · 0 columns", "0 行 · 0 欄");
                return;
            }
            int cols = grid.ColCount;
            int dataRows = firstRowHeader ? Math.Max(0, grid.RowCount - 1) : grid.RowCount;
            string mode = reverse ? P("  ·  reverse mode", "  ·  倒轉模式") : "";
            CountsText.Text = P($"{dataRows} data row(s) · {cols} column(s){mode}",
                                 $"{dataRows} 行資料 · {cols} 欄{mode}");
        }
        catch { /* ignore */ }
    }

    private async void OnCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox?.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                SetStatus(P("Nothing to copy yet.", "而家冇嘢可以複製。"), InfoBarSeverity.Warning);
                return;
            }
            var dp = new DataPackage();
            dp.SetText(text);
            Clipboard.SetContent(dp);
            SetStatus(P("Copied to clipboard.", "已複製到剪貼簿。"), InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            SetStatus(P("Copy failed: ", "複製失敗：") + ex.Message, InfoBarSeverity.Error);
        }
        await System.Threading.Tasks.Task.CompletedTask;
    }

    private void SetStatus(string message, InfoBarSeverity severity)
    {
        try
        {
            if (Status == null) return;
            Status.Message = message;
            Status.Severity = severity;
            Status.IsOpen = true;
        }
        catch { /* ignore */ }
    }
}
