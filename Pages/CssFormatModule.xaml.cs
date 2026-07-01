using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// CSS 格式化 / 壓縮 · CSS Formatter / Minifier — paste CSS, beautify it (one selector line,
/// one declaration per indented line, blank lines between rules, nested @media/@keyframes) or
/// minify it to a compact form. Own tokenizer, pure managed, never throws. Bilingual.
/// </summary>
public sealed partial class CssFormatModule : Page
{
    public CssFormatModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Render();

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLanguageChanged;

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "CSS Formatter · CSS 格式化";
        HeaderBlurb.Text = P("Paste CSS and beautify it — one selector per line, one declaration per indented line, blank lines between rules, nested @media / @keyframes — or minify it down to a compact form. Comments are preserved when formatting.",
            "貼低 CSS 就可以美化 — 每個選擇器一行、每條宣告縮排各佔一行、規則之間有空行、識得處理巢狀嘅 @media / @keyframes，又或者壓縮成精簡格式。美化嗰陣會保留註解。");
        FormatBtn.Content = P("Format", "美化");
        MinifyBtn.Content = P("Minify", "壓縮");
        CopyBtn.Content = P("Copy output", "複製結果");
        ClearBtn.Content = P("Clear", "清除");
        IndentLabel.Text = P("Indent spaces", "縮排空格數");
        InputLabel.Text = P("Input CSS", "輸入 CSS");
        OutputLabel.Text = P("Output", "輸出");
        if (string.IsNullOrEmpty(StatusText.Text))
            StatusText.Text = P("Ready.", "準備就緒。");
    }

    private int IndentSize()
    {
        double v = IndentBox.Value;
        if (double.IsNaN(v)) return 2;
        int iv = (int)v;
        if (iv < 0) iv = 0;
        if (iv > 8) iv = 8;
        return iv;
    }

    private void Format_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string input = InputBox.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                OutputBox.Text = string.Empty;
                StatusText.Text = P("Nothing to format — paste some CSS first.", "冇嘢可以美化 — 請先貼低 CSS。");
                return;
            }
            OutputBox.Text = CssFormatService.Format(input, IndentSize());
            StatusText.Text = P($"Formatted — {OutputBox.Text.Length:N0} characters.", $"已美化 — {OutputBox.Text.Length:N0} 個字元。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Could not format: ", "無法美化：") + ex.Message;
        }
    }

    private void Minify_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string input = InputBox.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                OutputBox.Text = string.Empty;
                StatusText.Text = P("Nothing to minify — paste some CSS first.", "冇嘢可以壓縮 — 請先貼低 CSS。");
                return;
            }
            OutputBox.Text = CssFormatService.Minify(input);
            int saved = Math.Max(0, input.Length - OutputBox.Text.Length);
            StatusText.Text = P($"Minified — {OutputBox.Text.Length:N0} characters ({saved:N0} smaller).",
                $"已壓縮 — {OutputBox.Text.Length:N0} 個字元（細咗 {saved:N0}）。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Could not minify: ", "無法壓縮：") + ex.Message;
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                StatusText.Text = P("Nothing to copy yet.", "暫時冇嘢可以複製。");
                return;
            }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            Clipboard.Flush();
            StatusText.Text = P("Copied output to the clipboard.", "已將結果複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Could not copy: ", "無法複製：") + ex.Message;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            InputBox.Text = string.Empty;
            OutputBox.Text = string.Empty;
            StatusText.Text = P("Cleared.", "已清除。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Could not clear: ", "無法清除：") + ex.Message;
        }
    }
}
