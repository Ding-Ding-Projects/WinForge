using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 文字工具工作臺 · Text Tools workbench — an input/output pair plus one-click string transforms
/// (case, whitespace, line ordering, dedup, shuffle, reverse, slugify) and a live stats card.
/// Pure managed C# via <see cref="TextToolsService"/>. Bilingual, never throws to the UI.
/// </summary>
public sealed partial class TextToolsModule : Page
{
    public TextToolsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => { Render(); UpdateStats(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Text Tools · 文字工具";
        HeaderBlurb.Text = P("A little workbench for wrangling text — change case, tidy whitespace, sort or dedupe lines, slugify and more. Nothing leaves this window.",
            "整理文字嘅細型工作臺 — 轉大細楷、清理空白、排序或去除重複行、slug 化等等。啲文字唔會離開呢個視窗。");

        InputLabel.Text = P("Input", "輸入");
        StatsTitle.Text = P("Stats", "統計");
        TransformsTitle.Text = P("Transforms", "轉換");
        OutputLabel.Text = P("Output", "輸出");

        BtnUpper.Content = P("UPPERCASE", "大楷");
        BtnLower.Content = P("lowercase", "細楷");
        BtnTitle.Content = P("Title Case", "字首大楷");
        BtnToggle.Content = P("tOGGLE", "反轉大細");

        BtnTrim.Content = P("Trim lines", "修剪每行");
        BtnBlank.Content = P("No blank lines", "去空行");
        BtnCollapse.Content = P("Collapse spaces", "合併空格");
        BtnDedup.Content = P("Dedupe", "去重複");

        BtnSortAsc.Content = P("Sort A→Z", "排序 A→Z");
        BtnSortDesc.Content = P("Sort Z→A", "排序 Z→A");
        BtnSortNum.Content = P("Sort #", "數字排序");
        BtnReverseLines.Content = P("Reverse lines", "行序倒轉");

        BtnShuffle.Content = P("Shuffle", "打亂");
        BtnReverseChars.Content = P("Reverse chars", "字元倒轉");
        BtnSlug.Content = P("Slugify", "Slug 化");

        BtnCopy.Content = P("Copy output", "複製輸出");
        BtnToInput.Content = P("Output → input", "輸出 → 輸入");

        UpdateStats();
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e) => UpdateStats();

    private void UpdateStats()
    {
        try
        {
            var s = TextToolsService.Analyze(InputBox?.Text ?? string.Empty);
            StatsText.Text = P(
                $"Characters {s.Characters}  ·  No spaces {s.CharactersNoSpaces}  ·  Words {s.Words}  ·  Lines {s.Lines}  ·  Sentences {s.Sentences}  ·  Avg word {s.AvgWordLength}",
                $"字元 {s.Characters}  ·  除空格 {s.CharactersNoSpaces}  ·  字詞 {s.Words}  ·  行 {s.Lines}  ·  句子 {s.Sentences}  ·  平均字長 {s.AvgWordLength}");
        }
        catch (Exception ex)
        {
            StatsText.Text = P("Couldn't compute stats.", "計唔到統計。");
            Status(ex);
        }
    }

    /// <summary>Run a transform against the input, write to output, and report any hiccup.</summary>
    private void Apply(Func<string, string> transform)
    {
        try
        {
            OutputBox.Text = transform(InputBox?.Text ?? string.Empty);
            StatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            Status(ex);
        }
    }

    private void Status(Exception ex) =>
        StatusText.Text = P("Something went wrong: ", "出咗啲問題：") + ex.Message;

    private void Upper_Click(object s, RoutedEventArgs e) => Apply(TextToolsService.Upper);
    private void Lower_Click(object s, RoutedEventArgs e) => Apply(TextToolsService.Lower);
    private void Title_Click(object s, RoutedEventArgs e) => Apply(TextToolsService.Title);
    private void Toggle_Click(object s, RoutedEventArgs e) => Apply(TextToolsService.Toggle);

    private void Trim_Click(object s, RoutedEventArgs e) => Apply(TextToolsService.TrimEachLine);
    private void Blank_Click(object s, RoutedEventArgs e) => Apply(TextToolsService.RemoveBlankLines);
    private void Collapse_Click(object s, RoutedEventArgs e) => Apply(TextToolsService.CollapseSpaces);
    private void Dedup_Click(object s, RoutedEventArgs e) => Apply(TextToolsService.Deduplicate);

    private void SortAsc_Click(object s, RoutedEventArgs e) => Apply(TextToolsService.SortAsc);
    private void SortDesc_Click(object s, RoutedEventArgs e) => Apply(TextToolsService.SortDesc);
    private void SortNum_Click(object s, RoutedEventArgs e) => Apply(TextToolsService.SortNumeric);
    private void ReverseLines_Click(object s, RoutedEventArgs e) => Apply(TextToolsService.ReverseLines);

    private void Shuffle_Click(object s, RoutedEventArgs e) => Apply(TextToolsService.ShuffleLines);
    private void ReverseChars_Click(object s, RoutedEventArgs e) => Apply(TextToolsService.ReverseChars);
    private void Slug_Click(object s, RoutedEventArgs e) => Apply(TextToolsService.Slugify);

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox?.Text ?? string.Empty;
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
            Status(ex);
        }
    }

    private void ToInput_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            InputBox.Text = OutputBox?.Text ?? string.Empty;
            StatusText.Text = string.Empty;
            UpdateStats();
        }
        catch (Exception ex)
        {
            Status(ex);
        }
    }
}
