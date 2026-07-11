using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Markdown 目錄產生器 · Markdown TOC generator — paste Markdown, get a nested table of contents of
/// GitHub-style anchor links. Live preview, bullets or numbered, min/max heading level, include-H1 toggle,
/// one-click copy. Pure managed C#, never throws. Bilingual (English + 粵語).
/// </summary>
public sealed partial class MarkdownTocModule : Page
{
    private bool _suppress;

    public MarkdownTocModule()
    {
        InitializeComponent();
        // The self-contained runtime cannot reliably convert these NumberBox
        // defaults from XAML. Preserve them without firing live regeneration.
        _suppress = true;
        MinBox.Value = 1;
        MaxBox.Value = 6;
        IncludeH1Chk.IsChecked = true;
        _suppress = false;
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildStyleItems();
        if (string.IsNullOrEmpty(InputBox.Text))
            InputBox.Text = SampleMarkdown();
        Render();
        Regenerate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e)
    {
        int idx = StyleCombo.SelectedIndex;
        BuildStyleItems();
        if (idx >= 0 && idx < StyleCombo.Items.Count) StyleCombo.SelectedIndex = idx;
        Render();
        Regenerate();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void BuildStyleItems()
    {
        _suppress = true;
        int keep = StyleCombo.SelectedIndex;
        StyleCombo.Items.Clear();
        StyleCombo.Items.Add(P("Bullets (-)", "項目符號 (-)"));
        StyleCombo.Items.Add(P("Numbered (1.)", "有序清單 (1.)"));
        StyleCombo.SelectedIndex = keep >= 0 && keep < 2 ? keep : 0;
        _suppress = false;
    }

    private void Render()
    {
        Header.Title = P("Markdown TOC", "Markdown 目錄");
        HeaderBlurb.Text = P("Paste Markdown and get a nested table of contents — GitHub-style anchor links, updated live as you type. Nothing leaves your PC.",
            "貼上 Markdown，即刻整出巢狀嘅目錄 — GitHub 式錨點連結，一邊打一邊更新。啲嘢唔會離開你部電腦。");
        OptionsTitle.Text = P("Options", "選項");
        MinLabel.Text = P("Min heading level", "最細標題層級");
        MaxLabel.Text = P("Max heading level", "最大標題層級");
        StyleLabel.Text = P("List style", "清單樣式");
        IncludeH1Chk.Content = P("Include H1 headings", "包含 H1 標題");
        InputLabel.Text = P("Markdown input", "Markdown 輸入");
        OutputLabel.Text = P("Table of contents", "目錄");
        CopyButton.Content = P("Copy", "複製");
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Regenerate();
    }

    private void Options_Changed(object sender, object e)
    {
        if (_suppress) return;
        Regenerate();
    }

    private void Regenerate()
    {
        try
        {
            var opts = new MarkdownTocService.TocOptions
            {
                MinLevel = ReadLevel(MinBox, 1),
                MaxLevel = ReadLevel(MaxBox, 6),
                IncludeH1 = IncludeH1Chk.IsChecked == true,
                Ordered = StyleCombo.SelectedIndex == 1
            };

            var result = MarkdownTocService.Generate(InputBox.Text, opts);
            OutputBox.Text = result.Markdown;

            if (result.HeadingCount == 0)
                StatusText.Text = P("No headings found (looking for '# …' outside code blocks).",
                    "搵唔到標題（喺程式碼區塊以外搵 '# …'）。");
            else
                StatusText.Text = P($"{result.HeadingCount} heading(s) in the table of contents.",
                    $"目錄有 {result.HeadingCount} 個標題。");
        }
        catch (Exception ex)
        {
            OutputBox.Text = "";
            StatusText.Text = P("Could not generate: ", "產生失敗：") + ex.Message;
        }
    }

    private static int ReadLevel(NumberBox box, int fallback)
    {
        double v = box.Value;
        if (double.IsNaN(v)) return fallback;
        int i = (int)Math.Round(v);
        if (i < 1) i = 1;
        if (i > 6) i = 6;
        return i;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? "";
            if (text.Length == 0)
            {
                StatusText.Text = P("Nothing to copy yet.", "而家未有嘢可以複製。");
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Copied the table of contents to the clipboard.", "已經將目錄複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Copy failed: ", "複製失敗：") + ex.Message;
        }
    }

    private static string SampleMarkdown() =>
        "# Getting Started\n\n" +
        "Intro text.\n\n" +
        "## Installation\n\n" +
        "```\n# this is code, not a heading\n```\n\n" +
        "## Usage\n\n" +
        "### Basic Usage\n\n" +
        "### Advanced Usage!\n\n" +
        "## Usage\n\n" +
        "# Reference\n";
}
