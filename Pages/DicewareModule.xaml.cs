using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 密語產生器 · Diceware passphrase generator — builds memorable passphrases from an embedded curated
/// word list using cryptographically unbiased selection (RandomNumberGenerator). Shows estimated
/// entropy and copies to the clipboard. Bilingual (粵語). Never throws.
/// </summary>
public sealed partial class DicewareModule : Page
{
    public DicewareModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); TryGenerate(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Passphrase Generator · 密語產生器";
            HeaderBlurb.Text = P(
                $"Build memorable passphrases from a {DicewareService.WordCount}-word list using unbiased, cryptographically-random selection. Longer phrases are stronger — check the entropy readout.",
                $"用 {DicewareService.WordCount} 個詞語嘅清單、加密級隨機（無偏差）揀字，砌出好記嘅通行短語。字愈多愈穩陣 — 睇下下面嘅熵值。");
            WordsLabel.Text = P("Words per phrase (3–12)", "每句幾多個詞（3–12）");
            SepLabel.Text = P("Separator", "分隔符");
            PhrasesLabel.Text = P("How many phrases (1–50)", "產生幾多句（1–50）");
            CapSwitch.Header = P("Capitalize each word", "每個詞首字大寫");
            NumSwitch.Header = P("Append a random number", "加一個隨機數字");
            GenBtn.Content = P("Generate", "產生");
            CopyBtn.Content = P("Copy", "複製");
            ResultsLabel.Text = P("Generated passphrases", "已產生嘅密語");

            RebuildSepBox();
            UpdateEntropy();
        }
        catch { /* never throw from UI */ }
    }

    private void RebuildSepBox()
    {
        try
        {
            int idx = SepBox.SelectedIndex < 0 ? 1 : SepBox.SelectedIndex;
            SepBox.Items.Clear();
            SepBox.Items.Add(P("Space", "空格"));
            SepBox.Items.Add(P("Hyphen ( - )", "連字號 ( - )"));
            SepBox.Items.Add(P("Dot ( . )", "點 ( . )"));
            SepBox.Items.Add(P("None", "無"));
            SepBox.SelectedIndex = Math.Clamp(idx, 0, 3);
        }
        catch { /* ignore */ }
    }

    private DicewareService.Separator CurrentSep() => SepBox.SelectedIndex switch
    {
        0 => DicewareService.Separator.Space,
        2 => DicewareService.Separator.Dot,
        3 => DicewareService.Separator.None,
        _ => DicewareService.Separator.Hyphen
    };

    private int WordCountValue() => (int)(double.IsNaN(WordsBox.Value) ? 6 : WordsBox.Value);
    private int PhraseCountValue() => (int)(double.IsNaN(PhrasesBox.Value) ? 5 : PhrasesBox.Value);

    private void Gen_Click(object sender, RoutedEventArgs e) => TryGenerate();

    private void TryGenerate()
    {
        try
        {
            var list = DicewareService.GenerateMany(
                PhraseCountValue(), WordCountValue(), CurrentSep(),
                CapSwitch.IsOn, NumSwitch.IsOn);
            ResultsBox.Text = string.Join(Environment.NewLine, list);
            UpdateEntropy();
        }
        catch { /* never throw */ }
    }

    private void UpdateEntropy()
    {
        try
        {
            double bits = DicewareService.EstimateBits(WordCountValue(), NumSwitch.IsOn);
            string strength =
                bits >= 100 ? P("very strong", "非常強") :
                bits >= 70 ? P("strong", "強") :
                bits >= 50 ? P("reasonable", "尚可") :
                P("weak — add more words", "偏弱 — 加多幾個詞");
            EntropyText.Text = P(
                $"Estimated entropy: {bits:0} bits ({strength}). List size {DicewareService.WordCount} → {DicewareService.BitsPerWord:0.0} bits/word.",
                $"估計熵值：{bits:0} 位（{strength}）。清單有 {DicewareService.WordCount} 個詞 → 每詞 {DicewareService.BitsPerWord:0.0} 位。");
        }
        catch { /* ignore */ }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = ResultsBox.Text ?? "";
            if (string.IsNullOrEmpty(text)) return;
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
        }
        catch { /* never throw */ }
    }
}
