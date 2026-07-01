using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// HTML → Markdown 轉換器 · Paste HTML on the left, get best-effort Markdown live on the right.
/// Pure-managed (own converter in <see cref="HtmlToMdService"/>), never throws, bilingual. No redirect.
/// </summary>
public sealed partial class HtmlToMdModule : Page
{
    private bool _hasOutput;

    public HtmlToMdModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Render();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = P("HTML to Markdown", "HTML 轉 Markdown");
        HeaderBlurb.Text = P("Paste HTML on the left and get clean Markdown on the right, updated as you type. Best-effort — handles headings, bold/italic, links, lists, quotes, code, rules and entities, and won't choke on messy markup.",
            "喺左邊貼 HTML，右邊即刻出乾淨嘅 Markdown，打字即時更新。盡力而為 — 支援標題、粗斜體、連結、清單、引言、程式碼、分隔線同實體，就算標記亂七八糟都唔會當機。");
        InputLabel.Text = P("HTML input", "HTML 輸入");
        OutputLabel.Text = P("Markdown output", "Markdown 輸出");
        CopyButton.Content = P("Copy Markdown", "複製 Markdown");
        UpdateStatus();
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            string md = HtmlToMdService.Convert(InputBox.Text);
            OutputBox.Text = md;
            _hasOutput = md.Length > 0;
        }
        catch
        {
            OutputBox.Text = string.Empty;
            _hasOutput = false;
        }
        UpdateStatus();
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
            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Couldn't copy: ", "複製唔到：") + ex.Message;
        }
    }

    private void UpdateStatus()
    {
        if (string.IsNullOrWhiteSpace(InputBox.Text))
            StatusText.Text = P("Paste some HTML to begin.", "貼啲 HTML 就開始。");
        else if (_hasOutput)
            StatusText.Text = P($"Converted — {OutputBox.Text.Length} characters.", $"已轉換 — {OutputBox.Text.Length} 個字元。");
        else
            StatusText.Text = P("No convertible content found.", "搵唔到可以轉換嘅內容。");
    }
}
