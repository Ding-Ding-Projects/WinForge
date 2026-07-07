using System;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 數字 ⇄ 文字 · 羅馬數字 · Number ⇄ words &amp; Roman numerals. Live conversion cards:
/// number→English words (BigInteger, decimals, negatives), currency words, ordinals,
/// Roman↔integer (1..3999, validated), and best-effort words→number. Bilingual, never throws.
/// </summary>
public sealed partial class NumberWordsModule : Page
{
    public NumberWordsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
        Loaded += (_, _) => Render();
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = P("Number to Words · 數字轉文字", "數字轉文字 · Number to Words");
            HeaderBlurb.Text = P(
                "Turn numbers into English words, currency, ordinals and Roman numerals — and back again. Everything updates as you type.",
                "將數字變做英文文字、金額、序數同羅馬數字 — 亦可以反轉返。你打字嗰陣即時更新。");

            WordsTitle.Text = P("Number → English words", "數字 → 英文文字");
            MoneyTitle.Text = P("Number → currency words", "數字 → 金額文字");
            OrdinalTitle.Text = P("Ordinal (1st, 2nd, 21st…)", "序數（第一、第二、第廿一…）");
            RomanTitle.Text = P("Integer → Roman numeral (1–3999)", "整數 → 羅馬數字（1–3999）");
            UnromanTitle.Text = P("Roman numeral → integer", "羅馬數字 → 整數");
            ParseTitle.Text = P("Words → number (best effort)", "文字 → 數字（盡量嘗試）");

            string copy = P("Copy", "複製");
            WordsCopy.Content = copy; MoneyCopy.Content = copy; OrdinalCopy.Content = copy;
            RomanCopy.Content = copy; UnromanCopy.Content = copy; ParseCopy.Content = copy;

            StatusText.Text = P("Tip: try huge numbers — BigInteger handles quintillions and beyond.",
                "貼士：試下打好大嘅數 — BigInteger 應付到千京都得。");

            RenderAll();
        }
        catch { /* never throw from UI */ }
    }

    private void RenderAll()
    {
        WordsInput_Changed(WordsInput, null);
        MoneyInput_Changed(MoneyInput, null);
        OrdinalInput_Changed(OrdinalInput, null);
        RomanInput_Changed(RomanInput, null);
        UnromanInput_Changed(UnromanInput, null);
        ParseInput_Changed(ParseInput, null);
    }

    private string Empty => P("Enter a value above.", "喺上面輸入一個值。");
    private string Bad => P("Couldn't parse that — check the input.", "睇唔明 — 請檢查輸入。");

    // ---- Number -> words -------------------------------------------------
    private void WordsInput_Changed(object sender, TextChangedEventArgs? e)
    {
        try
        {
            string t = WordsInput.Text?.Trim() ?? "";
            if (t.Length == 0) { WordsOutput.Text = Empty; return; }
            WordsOutput.Text = NumberWordsService.TryNumberToWords(t, out var w) ? w : Bad;
        }
        catch { WordsOutput.Text = Bad; }
    }

    // ---- Currency --------------------------------------------------------
    private void MoneyInput_Changed(object sender, TextChangedEventArgs? e)
    {
        try
        {
            string t = MoneyInput.Text?.Trim() ?? "";
            if (t.Length == 0) { MoneyOutput.Text = Empty; return; }
            MoneyOutput.Text = NumberWordsService.TryCurrencyToWords(t, out var w) ? w : Bad;
        }
        catch { MoneyOutput.Text = Bad; }
    }

    // ---- Ordinal ---------------------------------------------------------
    private void OrdinalInput_Changed(object sender, TextChangedEventArgs? e)
    {
        try
        {
            string t = OrdinalInput.Text?.Trim() ?? "";
            if (t.Length == 0) { OrdinalOutput.Text = Empty; return; }
            OrdinalOutput.Text = NumberWordsService.TryOrdinal(t, out var w)
                ? w
                : P("Enter a non-negative whole number.", "請輸入一個非負整數。");
        }
        catch { OrdinalOutput.Text = Bad; }
    }

    // ---- Integer -> Roman ------------------------------------------------
    private void RomanInput_Changed(object sender, TextChangedEventArgs? e)
    {
        try
        {
            string t = RomanInput.Text?.Trim() ?? "";
            if (t.Length == 0) { RomanOutput.Text = Empty; return; }
            RomanOutput.Text = NumberWordsService.TryToRoman(t, out var r)
                ? r
                : P("Roman numerals cover 1 to 3999 only.", "羅馬數字只支援 1 至 3999。");
        }
        catch { RomanOutput.Text = Bad; }
    }

    // ---- Roman -> integer ------------------------------------------------
    private void UnromanInput_Changed(object sender, TextChangedEventArgs? e)
    {
        try
        {
            string t = UnromanInput.Text?.Trim() ?? "";
            if (t.Length == 0) { UnromanOutput.Text = Empty; return; }
            UnromanOutput.Text = NumberWordsService.TryFromRoman(t, out int v)
                ? v.ToString()
                : P("Not a valid Roman numeral (e.g. MMXXIV).", "唔係有效嘅羅馬數字（例如 MMXXIV）。");
        }
        catch { UnromanOutput.Text = Bad; }
    }

    // ---- Words -> number -------------------------------------------------
    private void ParseInput_Changed(object sender, TextChangedEventArgs? e)
    {
        try
        {
            string t = ParseInput.Text?.Trim() ?? "";
            if (t.Length == 0) { ParseOutput.Text = Empty; return; }
            ParseOutput.Text = NumberWordsService.TryWordsToNumber(t, out BigInteger v)
                ? v.ToString()
                : P("Couldn't read those words — try e.g. \"two hundred and five\".",
                    "讀唔到呢啲字 — 試下例如「two hundred and five」。");
        }
        catch { ParseOutput.Text = Bad; }
    }

    // ---- Copy buttons ----------------------------------------------------
    private void Copy(string text)
    {
        try
        {
            if (string.IsNullOrEmpty(text)) return;
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch { StatusText.Text = P("Couldn't copy to the clipboard.", "無法複製到剪貼簿。"); }
    }

    private void WordsCopy_Click(object sender, RoutedEventArgs e) => Copy(WordsOutput.Text);
    private void MoneyCopy_Click(object sender, RoutedEventArgs e) => Copy(MoneyOutput.Text);
    private void OrdinalCopy_Click(object sender, RoutedEventArgs e) => Copy(OrdinalOutput.Text);
    private void RomanCopy_Click(object sender, RoutedEventArgs e) => Copy(RomanOutput.Text);
    private void UnromanCopy_Click(object sender, RoutedEventArgs e) => Copy(UnromanOutput.Text);
    private void ParseCopy_Click(object sender, RoutedEventArgs e) => Copy(ParseOutput.Text);
}
