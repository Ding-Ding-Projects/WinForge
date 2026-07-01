using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// HTML 格式化 / 壓縮 · HTML formatter / minifier. Own tokenizer (pure managed C#); pretty-prints
/// nested tags one-per-line with a configurable indent, or collapses insignificant whitespace to
/// minify. Preserves &lt;pre&gt;/&lt;script&gt;/&lt;style&gt;/&lt;textarea&gt; verbatim. Never throws.
/// </summary>
public sealed partial class HtmlFormatModule : Page
{
    public HtmlFormatModule()
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
        Header.Title = "HTML Formatter · HTML 格式化";
        HeaderBlurb.Text = P("Tidy up messy HTML — indent nested tags one per line, or minify by collapsing insignificant whitespace. <pre>, <script>, <style> and <textarea> are kept exactly as-is.",
            "執靚亂糟糟嘅 HTML — 逐個標籤縮排排好，或者壓縮走多餘嘅空白。<pre>、<script>、<style> 同 <textarea> 嘅內容原封不動保留。");
        InputLabel.Text = P("HTML input", "HTML 輸入");
        IndentLabel.Text = P("Indent size (spaces)", "縮排大小（空格數）");
        FormatBtn.Content = P("Format", "格式化");
        MinifyBtn.Content = P("Minify", "壓縮");
        CopyBtn.Content = P("Copy", "複製");
        OutputLabel.Text = P("Result", "結果");
        if (string.IsNullOrEmpty(StatusText.Text))
            StatusText.Text = P("Paste HTML above, then Format or Minify.", "喺上面貼 HTML，然後撳格式化或者壓縮。");
    }

    private int Indent()
    {
        double v = IndentBox.Value;
        if (double.IsNaN(v)) v = 2;
        return (int)v;
    }

    private void Format_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string input = InputBox.Text ?? string.Empty;
            if (input.Trim().Length == 0)
            {
                StatusText.Text = P("Nothing to format — the input is empty.", "冇嘢可以格式化 — 輸入係空嘅。");
                return;
            }
            OutputBox.Text = HtmlFormatService.Format(input, Indent());
            StatusText.Text = P($"Formatted — {OutputBox.Text.Length:N0} characters.", $"已格式化 — {OutputBox.Text.Length:N0} 個字元。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Could not format: ", "格式化失敗：") + ex.Message;
        }
    }

    private void Minify_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string input = InputBox.Text ?? string.Empty;
            if (input.Trim().Length == 0)
            {
                StatusText.Text = P("Nothing to minify — the input is empty.", "冇嘢可以壓縮 — 輸入係空嘅。");
                return;
            }
            int before = input.Length;
            OutputBox.Text = HtmlFormatService.Minify(input);
            int after = OutputBox.Text.Length;
            int saved = Math.Max(0, before - after);
            StatusText.Text = P($"Minified — {after:N0} characters ({saved:N0} saved).", $"已壓縮 — {after:N0} 個字元（慳咗 {saved:N0}）。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Could not minify: ", "壓縮失敗：") + ex.Message;
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? string.Empty;
            if (text.Length == 0)
            {
                StatusText.Text = P("Nothing to copy yet.", "暫時冇嘢可以複製。");
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Copy failed: ", "複製失敗：") + ex.Message;
        }
    }
}
