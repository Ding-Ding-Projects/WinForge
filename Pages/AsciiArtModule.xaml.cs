using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// ASCII 橫幅產生器 · ASCII banner generator — type text, pick a style (Block / Outline) and get a
/// large 5-row ASCII-art banner in a monospace box. Copy to clipboard. Pure managed, never-throw,
/// bilingual (粵語). No redirect. Anti-leak: named handler + Unloaded unsubscribe.
/// </summary>
public sealed partial class AsciiArtModule : Page
{
    private bool _suppress;

    public AsciiArtModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildStyles();
        Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void BuildStyles()
    {
        _suppress = true;
        try
        {
            int keep = StyleBox.SelectedIndex < 0 ? 0 : StyleBox.SelectedIndex;
            StyleBox.Items.Clear();
            StyleBox.Items.Add(P("Block (solid)", "實心 Block"));
            StyleBox.Items.Add(P("Outline (thin)", "描邊 Outline"));
            StyleBox.SelectedIndex = keep < StyleBox.Items.Count ? keep : 0;
        }
        catch { /* never throw from UI setup */ }
        finally { _suppress = false; }
    }

    private void Render()
    {
        try
        {
            Header.Title = "ASCII Banner · ASCII 橫幅";
            HeaderBlurb.Text = P("Turn any short line of text into a big ASCII-art banner. Uppercase A–Z, digits and a few symbols are supported; unknown characters become blanks.",
                "將一句短文字變成一個大大嘅 ASCII 藝術橫幅。支援大階 A–Z、數字同少少符號；唔識嘅字元會變空白。");
            InputLabel.Text = P("Text to render", "要轉換嘅文字");
            StyleLabel.Text = P("Style", "樣式");
            CopyBtn.Content = P("Copy banner", "複製橫幅");
            BuildStyles();
        }
        catch { /* ignore */ }
        RenderBanner();
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        RenderBanner();
    }

    private void Style_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        RenderBanner();
    }

    private void RenderBanner()
    {
        try
        {
            string input = InputBox?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                OutputBox.Text = string.Empty;
                StatusText.Text = P("Type some text to generate a banner.", "輸入啲文字嚟產生橫幅。");
                return;
            }

            var style = StyleBox.SelectedIndex == 1
                ? AsciiArtService.Style.Outline
                : AsciiArtService.Style.Block;

            string banner = AsciiArtService.Render(input, style);
            OutputBox.Text = banner;
            StatusText.Text = string.IsNullOrEmpty(banner)
                ? P("Nothing to show.", "冇嘢可以顯示。")
                : P($"{input.Trim().Length} chars · {AsciiArtService.Height} rows tall",
                    $"{input.Trim().Length} 個字元 · 高 {AsciiArtService.Height} 行");
        }
        catch
        {
            OutputBox.Text = string.Empty;
            StatusText.Text = P("Could not render the banner.", "產生唔到橫幅。");
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox?.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                StatusText.Text = P("Nothing to copy yet.", "暫時冇嘢好複製。");
                return;
            }

            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            Clipboard.Flush();
            StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch
        {
            StatusText.Text = P("Copy failed.", "複製失敗。");
        }
    }
}
