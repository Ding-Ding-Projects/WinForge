using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 十六進位傾印檢視器 · Hex-dump viewer — feed it UTF-8 text, pasted hex bytes, or a file
/// (capped at ~1 MB) and get a classic offset │ hex │ ASCII dump. Bytes-per-row, uppercase
/// and offset-column options; copy the dump. Pure managed, robust — never throws at the user.
/// </summary>
public sealed partial class HexDumpModule : Page
{
    private enum Src { Text, Hex, File }

    private byte[] _bytes = Array.Empty<byte>();
    private string? _filePath;
    private bool _fileTruncated;
    private bool _suppress;

    public HexDumpModule()
    {
        InitializeComponent();
        // The self-contained XAML runtime can reject typed IsOn literals.
        _suppress = true;
        OffsetSwitch.IsOn = true;
        _suppress = false;
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { InitCombo(); Render(); Recompute(); };
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) { Render(); Recompute(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void InitCombo()
    {
        _suppress = true;
        if (SourceCombo.Items.Count == 0)
        {
            SourceCombo.Items.Add(P("UTF-8 text", "UTF-8 文字"));
            SourceCombo.Items.Add(P("Hex bytes", "十六進位位元組"));
            SourceCombo.Items.Add(P("File", "檔案"));
        }
        if (SourceCombo.SelectedIndex < 0) SourceCombo.SelectedIndex = 0;
        _suppress = false;
    }

    private Src Source => (Src)Math.Max(0, SourceCombo.SelectedIndex);

    private void Render()
    {
        Header.Title = P("Hex Dump · 十六進位傾印", "十六進位傾印 · Hex Dump");
        HeaderBlurb.Text = P(
            "Turn text, pasted hex, or a file into a classic hex dump — offset, hex columns and an ASCII gutter. Files are read up to ~1 MB.",
            "將文字、貼上嘅十六進位或者檔案變成經典嘅十六進位傾印 — 偏移、十六進位欄同 ASCII 對照。檔案最多讀 ~1 MB。");
        SourceLabel.Text = P("Source", "來源");
        PerRowLabel.Text = P("Bytes per row", "每行位元組");
        UpperSwitch.Header = P("Uppercase hex", "大寫十六進位");
        OffsetSwitch.Header = P("Show offset", "顯示偏移");
        BrowseBtn.Content = P("Browse a file…", "瀏覽檔案…");
        OutputLabel.Text = P("Dump", "傾印");
        CopyBtn.Content = P("Copy dump", "複製傾印");

        // Refresh the source ComboBox labels for the current language.
        int sel = SourceCombo.SelectedIndex;
        _suppress = true;
        SourceCombo.Items.Clear();
        SourceCombo.Items.Add(P("UTF-8 text", "UTF-8 文字"));
        SourceCombo.Items.Add(P("Hex bytes", "十六進位位元組"));
        SourceCombo.Items.Add(P("File", "檔案"));
        SourceCombo.SelectedIndex = sel < 0 ? 0 : sel;
        _suppress = false;

        ApplySourceUi();
        UpdateFilePathText();
    }

    private void ApplySourceUi()
    {
        var s = Source;
        InputBox.Visibility = s == Src.File ? Visibility.Collapsed : Visibility.Visible;
        FilePanel.Visibility = s == Src.File ? Visibility.Visible : Visibility.Collapsed;
        InputBox.PlaceholderText = s == Src.Hex
            ? P("Paste hex, e.g. 48 65 6C 6C 6F  (0x / commas / whitespace ignored)",
                "貼上十六進位，例如 48 65 6C 6C 6F（0x／逗號／空白會略去）")
            : P("Type or paste UTF-8 text…", "輸入或者貼上 UTF-8 文字…");
    }

    private void Source_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        ApplySourceUi();
        Recompute();
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Recompute();
    }

    private void Options_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        RenderDump();
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BrowseBtn.IsEnabled = false;
            var path = await FileDialogs.OpenFileAsync();
            if (!string.IsNullOrEmpty(path))
            {
                _filePath = path;
                var (bytes, truncated, err) = await HexDumpService.FromFileAsync(path);
                _bytes = bytes;
                _fileTruncated = truncated;
                UpdateFilePathText(err);
                RenderDump();
            }
        }
        catch { /* never throw at the user */ }
        finally { try { BrowseBtn.IsEnabled = true; } catch { } }
    }

    private void UpdateFilePathText(string? err = null)
    {
        try
        {
            if (Source != Src.File) return;
            if (err is not null)
                FilePathText.Text = P("Could not read file: ", "讀取檔案失敗：") + err;
            else if (string.IsNullOrEmpty(_filePath))
                FilePathText.Text = P("No file chosen.", "未揀檔案。");
            else
                FilePathText.Text = _filePath!;
        }
        catch { }
    }

    /// <summary>由來源重新取得位元組（文字／十六進位），或者沿用已讀嘅檔案 · Recompute bytes
    /// from the current source (text/hex re-parse; file keeps last read).</summary>
    private void Recompute()
    {
        try
        {
            switch (Source)
            {
                case Src.Text:
                    _bytes = HexDumpService.FromText(InputBox.Text);
                    _fileTruncated = false;
                    break;
                case Src.Hex:
                    _bytes = HexDumpService.FromHex(InputBox.Text);
                    _fileTruncated = false;
                    break;
                case Src.File:
                    UpdateFilePathText();
                    break;
            }
            RenderDump();
        }
        catch { /* robust */ }
    }

    private void RenderDump()
    {
        try
        {
            int perRow = PerRowFromCombo();
            bool upper = UpperSwitch.IsOn;
            bool showOffset = OffsetSwitch.IsOn;
            OutputBox.Text = HexDumpService.Render(_bytes, perRow, upper, showOffset);

            string count = P($"{_bytes.Length:N0} bytes", $"{_bytes.Length:N0} 位元組");
            if (_fileTruncated)
                count += P($"  ·  truncated to {HexDumpService.MaxBytes / 1024} KB",
                           $"  ·  已截短至 {HexDumpService.MaxBytes / 1024} KB");
            CountText.Text = count;
        }
        catch { /* robust */ }
    }

    private int PerRowFromCombo()
    {
        try
        {
            if (PerRowCombo.SelectedItem is ComboBoxItem it && it.Content is string s
                && int.TryParse(s, out int v)) return v;
        }
        catch { }
        return 16;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pkg = new DataPackage();
            pkg.SetText(OutputBox.Text ?? string.Empty);
            Clipboard.SetContent(pkg);
        }
        catch { /* clipboard may be busy — ignore */ }
    }
}
