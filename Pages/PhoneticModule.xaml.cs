using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 拼讀字母表 · Phonetic Speller — type text and spell it out with a phonetic alphabet
/// (NATO/ICAO "Alpha Bravo Charlie", LAPD/police, or a plain word-per-letter set). Shows the
/// full spoken string plus a per-character list, and copies the spoken string. Robust; never throws.
/// </summary>
public sealed partial class PhoneticModule : Page
{
    private readonly ObservableCollection<PhoneticService.SpelledChar> _chars = new();
    private bool _ready;

    public PhoneticModule()
    {
        InitializeComponent();
        CharList.ItemsSource = _chars;
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { BuildAlphabets(); _ready = true; Render(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void BuildAlphabets()
    {
        try
        {
            if (AlphabetBox.Items.Count > 0) return;
            AlphabetBox.Items.Add(PhoneticService.DisplayName(PhoneticService.Alphabet.Nato));
            AlphabetBox.Items.Add(PhoneticService.DisplayName(PhoneticService.Alphabet.Police));
            AlphabetBox.Items.Add(PhoneticService.DisplayName(PhoneticService.Alphabet.Simple));
            AlphabetBox.SelectedIndex = 0;
            PunctChk.IsChecked = true;
        }
        catch { /* never throw */ }
    }

    private void Render()
    {
        try
        {
            Header.Title = "Phonetic Speller · 拼讀字母表";
            HeaderBlurb.Text = P(
                "Type any text and spell it out with a phonetic alphabet — NATO/ICAO (Alpha Bravo Charlie), LAPD/police, or plain words. Handy for reading codes, names or passwords over the radio or phone.",
                "打啲字，用拼讀字母表逐個字母讀出嚟 — 北約/ICAO（Alpha Bravo Charlie）、警察，或者普通英文字。喺無線電或者電話度讀代碼、名或者密碼好方便。");
            InputBox.Header = P("Text to spell", "要拼讀嘅文字");
            InputBox.PlaceholderText = P("e.g. ABC-123", "例如 ABC-123");
            AlphabetLabel.Text = P("Alphabet", "字母表");
            UpperChk.Content = P("Upper-case the characters", "字母轉大寫");
            PunctChk.Content = P("Keep punctuation & symbols", "保留標點同符號");
            SpokenTitle.Text = P("Spoken", "讀法");
            CopyButton.Content = P("Copy spoken", "複製讀法");
            SpellNow();
        }
        catch { /* never throw */ }
    }

    private PhoneticService.Alphabet Selected()
    {
        return AlphabetBox.SelectedIndex switch
        {
            1 => PhoneticService.Alphabet.Police,
            2 => PhoneticService.Alphabet.Simple,
            _ => PhoneticService.Alphabet.Nato,
        };
    }

    private void SpellNow()
    {
        try
        {
            var res = PhoneticService.Spell(
                InputBox.Text,
                Selected(),
                UpperChk.IsChecked == true,
                PunctChk.IsChecked == true);

            _chars.Clear();
            foreach (var c in res.Chars) _chars.Add(c);

            SpokenText.Text = string.IsNullOrEmpty(res.Spoken)
                ? P("(nothing to spell yet)", "（暫時冇嘢拼讀）")
                : res.Spoken;
        }
        catch { /* never throw */ }
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        SpellNow();
    }

    private void Options_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        SpellNow();
    }

    private void Options_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        SpellNow();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = SpokenText.Text ?? string.Empty;
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
        }
        catch { /* clipboard can transiently fail; never throw */ }
    }
}
