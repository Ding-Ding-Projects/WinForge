using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 詞頻／字頻統計 · Word Frequency — paste text and rank word, bigram or character
/// frequencies with case, min-length, punctuation and stop-word options. Copy as CSV.
/// Pure managed, never throws. Bilingual (English + 粵語).
/// </summary>
public sealed partial class WordFreqModule : Page
{
    private bool _ready;
    private WordFreqService.Result _last = new();

    public WordFreqModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); _ready = true; Analyze(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = P("Word Frequency · 詞頻統計", "詞頻統計 · Word Frequency");
            HeaderBlurb.Text = P(
                "Paste any text and see which words, word-pairs or characters appear most. Ranked with counts, bars and percentages — copy the table as CSV.",
                "貼入任何文字，睇下邊啲詞、詞組或者字元出現得最多。附上次數、長條同百分比排名 — 可以複製成 CSV。");
            InputLabel.Text = P("Text to analyze", "要分析嘅文字");
            ModeLabel.Text = P("Count by", "統計對象");
            MinLenLabel.Text = P("Minimum length", "最短長度");
            CaseChk.Content = P("Case-insensitive (fold to lowercase)", "唔分大細楷（轉細楷）");
            PunctChk.Content = P("Strip punctuation", "去除標點");
            StopChk.Content = P("Remove common stop-words (English)", "移除常見停用詞（英文）");
            CopyBtn.Content = P("Copy as CSV", "複製為 CSV");

            // Preserve the current mode across a language re-render.
            int mode = ModeBox.SelectedIndex < 0 ? 0 : ModeBox.SelectedIndex;
            ModeBox.Items.Clear();
            ModeBox.Items.Add(P("Words", "詞語"));
            ModeBox.Items.Add(P("Bigrams (word pairs)", "詞組（兩字）"));
            ModeBox.Items.Add(P("Characters", "字元"));
            ModeBox.SelectedIndex = mode;

            UpdateTotals();
        }
        catch { /* never throw */ }
    }

    private WordFreqService.Mode CurrentMode() => ModeBox.SelectedIndex switch
    {
        1 => WordFreqService.Mode.Bigrams,
        2 => WordFreqService.Mode.Characters,
        _ => WordFreqService.Mode.Words,
    };

    private void Analyze()
    {
        if (!_ready) return;
        try
        {
            int minLen = (int)(double.IsNaN(MinLenBox.Value) ? 1 : MinLenBox.Value);
            _last = WordFreqService.Analyze(
                InputBox.Text,
                CurrentMode(),
                CaseChk.IsChecked == true,
                minLen,
                PunctChk.IsChecked == true,
                StopChk.IsChecked == true);

            ResultsList.ItemsSource = _last.Rows;
            UpdateTotals();
        }
        catch { /* never throw */ }
    }

    private void UpdateTotals()
    {
        try
        {
            double div = _last.Diversity * 100.0;
            TotalsText.Text = P(
                $"Total: {_last.TotalTokens:N0}   Unique: {_last.UniqueTokens:N0}   Lexical diversity: {div:0.0}%",
                $"總數：{_last.TotalTokens:N0}   不重複：{_last.UniqueTokens:N0}   詞彙多樣性：{div:0.0}%");
        }
        catch { /* never throw */ }
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) => Analyze();
    private void Options_Changed(object sender, RoutedEventArgs e) => Analyze();
    private void Options_Changed(object sender, SelectionChangedEventArgs e) => Analyze();
    private void MinLen_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) => Analyze();

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string csv = WordFreqService.ToCsv(_last);
            var pkg = new DataPackage();
            pkg.SetText(csv);
            Clipboard.SetContent(pkg);
            CopyBtn.Content = P("Copied!", "已複製！");
        }
        catch { /* never throw */ }
    }
}
