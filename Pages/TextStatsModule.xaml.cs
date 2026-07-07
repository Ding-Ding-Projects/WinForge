using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 文字統計同可讀性 · Text statistics &amp; readability module — paste/type text and get live counts
/// (characters, words, unique words, sentences, paragraphs), reading &amp; speaking time, Flesch Reading
/// Ease + Flesch–Kincaid grade, and a top-10 word-frequency list (stop-words optional). All maths in
/// <see cref="TextStatsService"/>; robust &amp; never-throw. Bilingual (English / 粵語). No redirect.
/// </summary>
public sealed partial class TextStatsModule : Page
{
    private bool _suppress;

    public TextStatsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); Recompute(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) { Render(); Recompute(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Text Statistics · 文字統計";
            HeaderBlurb.Text = P(
                "Paste or type any text to get live counts, reading and speaking time, and readability scores — no data leaves your PC.",
                "貼上或者打任何文字，即時睇字數、閱讀同朗讀時間，仲有可讀性評分 — 全部喺你部機度計，唔會外傳。");
            InputLabel.Text = P("Your text", "你嘅文字");
            InputBox.PlaceholderText = P("Type or paste text here…", "喺呢度打字或者貼上文字…");
            StopChk.Content = P("Ignore common stop-words in frequency", "字頻忽略常見虛詞");
            StatsTitle.Text = P("Statistics", "統計");
            FreqTitle.Text = P("Top words", "最常用字");
            Recompute(); // relabel value rows in the current language
        }
        catch { }
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Recompute();
    }

    private void Stop_Changed(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_suppress) return;
        Recompute();
    }

    private void Recompute()
    {
        try
        {
            bool ignore = StopChk.IsChecked == true;
            var s = TextStatsService.Analyze(InputBox.Text, ignore, 10);

            RowChars.Text = P($"Characters: {s.Characters:N0}", $"字元：{s.Characters:N0}");
            RowCharsNoSpace.Text = P($"Characters (no spaces): {s.CharactersNoSpaces:N0}", $"字元（不含空格）：{s.CharactersNoSpaces:N0}");
            RowWords.Text = P($"Words: {s.Words:N0}", $"字數：{s.Words:N0}");
            RowUnique.Text = P($"Unique words: {s.UniqueWords:N0}", $"不重複字：{s.UniqueWords:N0}");
            RowSentences.Text = P($"Sentences: {s.Sentences:N0}", $"句數：{s.Sentences:N0}");
            RowParagraphs.Text = P($"Paragraphs: {s.Paragraphs:N0}", $"段落：{s.Paragraphs:N0}");
            RowAvgWord.Text = P($"Avg word length: {s.AvgWordLength:0.0}", $"平均字長：{s.AvgWordLength:0.0}");
            RowAvgSentence.Text = P($"Avg sentence length: {s.AvgSentenceLength:0.0} words", $"平均句長：{s.AvgSentenceLength:0.0} 字");
            RowReading.Text = P($"Reading time (~200 wpm): {TextStatsService.FormatDuration(s.ReadingMinutes)}",
                                $"閱讀時間（約 200 字/分）：{TextStatsService.FormatDuration(s.ReadingMinutes)}");
            RowSpeaking.Text = P($"Speaking time (~130 wpm): {TextStatsService.FormatDuration(s.SpeakingMinutes)}",
                                 $"朗讀時間（約 130 字/分）：{TextStatsService.FormatDuration(s.SpeakingMinutes)}");
            RowEase.Text = P($"Flesch Reading Ease: {s.FleschReadingEase:0.0}", $"Flesch 易讀度：{s.FleschReadingEase:0.0}");
            RowGrade.Text = P($"Flesch–Kincaid grade: {s.FleschKincaidGrade:0.0}", $"Flesch–Kincaid 年級：{s.FleschKincaidGrade:0.0}");
            RowEaseHint.Text = EaseHint(s.FleschReadingEase, s.Words);

            var list = s.TopWords ?? new List<TextStatsService.WordCount>();
            FreqList.ItemsSource = list;
            FreqEmpty.Text = list.Count == 0
                ? P("No words yet — start typing above.", "仲未有字 — 喺上面開始打字。")
                : "";

            StatusText.Text = s.Words > 0
                ? P("Updated live.", "已即時更新。")
                : P("Ready.", "準備就緒。");
        }
        catch
        {
            try { StatusText.Text = P("Could not analyze text.", "無法分析文字。"); } catch { }
        }
    }

    private string EaseHint(double ease, int words)
    {
        if (words <= 0) return "";
        if (ease >= 90) return P("Very easy — 5th grade.", "非常易讀 — 約小五程度。");
        if (ease >= 70) return P("Easy — 6th–7th grade.", "易讀 — 約小六至中一程度。");
        if (ease >= 60) return P("Plain English — 8th–9th grade.", "淺白 — 約中二至中三程度。");
        if (ease >= 50) return P("Fairly hard — 10th–12th grade.", "略難 — 約高中程度。");
        if (ease >= 30) return P("Difficult — college level.", "困難 — 約大學程度。");
        return P("Very difficult — graduate level.", "非常困難 — 約研究生程度。");
    }
}
