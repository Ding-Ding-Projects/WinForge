using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 密碼 / 通行短語產生器 · Password & passphrase generator. Cryptographically secure
/// (RandomNumberGenerator only), with entropy estimate, bulk output and one-click copy. Bilingual.
/// </summary>
public sealed partial class PassGenModule : Page
{
    private bool _suppress;
    private bool _loaded;

    public PassGenModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { _loaded = true; Render(); Regenerate(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private bool IsPassphrase => ModeRadios.SelectedIndex == 1;

    private void Render()
    {
        Header.Title = "Password Generator · 密碼產生器";
        HeaderBlurb.Text = P(
            "Create strong random passwords or memorable passphrases. All randomness comes from the OS cryptographic RNG — nothing leaves this PC.",
            "整強勁嘅隨機密碼或者易記嘅通行短語。所有隨機數都係用系統嘅加密級隨機產生器 — 完全唔會離開部電腦。");

        ModeLabel.Text = P("Mode", "模式");
        ModePasswordRadio.Content = P("Password", "密碼");
        ModePassphraseRadio.Content = P("Passphrase", "通行短語");

        PasswordCardTitle.Text = P("Password options", "密碼選項");
        LengthLabel.Text = P("Length (4–128)", "長度（4–128）");
        LowerChk.Content = P("Lowercase (a–z)", "細楷字母（a–z）");
        UpperChk.Content = P("UPPERCASE (A–Z)", "大楷字母（A–Z）");
        DigitsChk.Content = P("Digits (0–9)", "數字（0–9）");
        SymbolsChk.Content = P("Symbols (!@#…)", "符號（!@#…）");
        AmbiguousSwitch.Header = P("Avoid ambiguous characters (O 0 I l 1 |)", "避開易撈亂嘅字元（O 0 I l 1 |）");
        NoRepeatSwitch.Header = P("No repeated characters", "唔好有重複字元");

        PassphraseCardTitle.Text = P("Passphrase options", "通行短語選項");
        DictInfo.Text = P($"Dictionary: {PassGenService.DictionarySize} common English words.",
            $"字典：{PassGenService.DictionarySize} 個常用英文字。");
        WordCountLabel.Text = P("Word count (3–10)", "字數（3–10）");
        SeparatorLabel.Text = P("Separator", "分隔符");
        CapitalizeSwitch.Header = P("Capitalize each word", "每個字首字母大楷");
        AppendDigitSwitch.Header = P("Append a random digit", "尾加一個隨機數字");

        CountLabel.Text = P("How many (1–100)", "產生幾多個（1–100）");
        GenerateBtn.Content = P("Generate", "產生");
        CopyBtn.Content = P("Copy", "複製");

        RebuildSeparators();
        UpdateEntropy();
    }

    private void RebuildSeparators()
    {
        _suppress = true;
        int keep = SeparatorCombo.SelectedIndex < 0 ? 0 : SeparatorCombo.SelectedIndex;
        SeparatorCombo.Items.Clear();
        SeparatorCombo.Items.Add(P("Hyphen  -", "連字號  -"));
        SeparatorCombo.Items.Add(P("Dot  .", "句號  ."));
        SeparatorCombo.Items.Add(P("Space", "空格"));
        SeparatorCombo.Items.Add(P("Underscore  _", "底線  _"));
        SeparatorCombo.SelectedIndex = keep;
        _suppress = false;
    }

    private string SelectedSeparator() => SeparatorCombo.SelectedIndex switch
    {
        1 => ".",
        2 => " ",
        3 => "_",
        _ => "-",
    };

    private void Mode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        bool phrase = IsPassphrase;
        PasswordCard.Visibility = phrase ? Visibility.Collapsed : Visibility.Visible;
        PassphraseCard.Visibility = phrase ? Visibility.Visible : Visibility.Collapsed;
        Regenerate();
    }

    private void Options_Changed(object sender, object e)
    {
        if (_suppress || !_loaded) return;
        Regenerate();
    }

    private void Generate_Click(object sender, RoutedEventArgs e) => Regenerate();

    private int IntVal(NumberBox box, int fallback)
        => double.IsNaN(box.Value) ? fallback : (int)box.Value;

    private PassGenService.PasswordOptions ReadPasswordOptions() => new()
    {
        Length = IntVal(LengthBox, 16),
        Lower = LowerChk.IsChecked == true,
        Upper = UpperChk.IsChecked == true,
        Digits = DigitsChk.IsChecked == true,
        Symbols = SymbolsChk.IsChecked == true,
        AvoidAmbiguous = AmbiguousSwitch.IsOn,
        NoRepeats = NoRepeatSwitch.IsOn,
    };

    private PassGenService.PassphraseOptions ReadPassphraseOptions() => new()
    {
        WordCount = IntVal(WordCountBox, 4),
        Separator = SelectedSeparator(),
        Capitalize = CapitalizeSwitch.IsOn,
        AppendDigit = AppendDigitSwitch.IsOn,
    };

    private void Regenerate()
    {
        if (!_loaded) return;
        try
        {
            int count = Math.Clamp(IntVal(CountBox, 1), 1, 100);
            var lines = new List<string>(count);

            if (IsPassphrase)
            {
                var o = ReadPassphraseOptions();
                for (int i = 0; i < count; i++) lines.Add(PassGenService.GeneratePassphrase(o));
            }
            else
            {
                var o = ReadPasswordOptions();
                for (int i = 0; i < count; i++) lines.Add(PassGenService.GeneratePassword(o));
            }

            OutputBox.Text = string.Join(Environment.NewLine, lines);
            StatusText.Text = P($"Generated {count} · secure RNG.", $"已產生 {count} 個 · 加密級隨機。");
        }
        catch (Exception ex)
        {
            OutputBox.Text = string.Empty;
            StatusText.Text = P("Can't generate: ", "無法產生：") + Explain(ex);
        }
        UpdateEntropy();
    }

    private string Explain(Exception ex)
    {
        string m = ex.Message ?? string.Empty;
        if (m.Contains("No character")) return P("select at least one character set.", "至少要揀一種字元。");
        if (m.Contains("too short")) return P("length is too short to fit every selected set.", "長度太短，容納唔到所有揀咗嘅字元類別。");
        if (m.Contains("No-repeats")) return P("no-repeats needs a shorter length or a bigger pool.", "唔重複模式需要短啲嘅長度或者更大嘅字元池。");
        return m;
    }

    private void UpdateEntropy()
    {
        double bits;
        try
        {
            if (IsPassphrase)
            {
                var o = ReadPassphraseOptions();
                bits = PassGenService.PassphraseEntropyBits(o.WordCount, PassGenService.DictionarySize, o.AppendDigit);
            }
            else
            {
                var o = ReadPasswordOptions();
                int pool = PassGenService.BuildPool(o).Length;
                bits = PassGenService.PasswordEntropyBits(o.Length, pool);
            }
        }
        catch
        {
            bits = 0;
        }

        int rounded = (int)Math.Round(bits);
        EntropyBar.Value = Math.Min(EntropyBar.Maximum, bits);

        string label = bits switch
        {
            < 40 => P("Weak", "弱"),
            < 60 => P("Fair", "一般"),
            < 90 => P("Strong", "強"),
            _ => P("Excellent", "極強"),
        };
        EntropyLabel.Text = P($"Entropy: ~{rounded} bits · {label}", $"熵值：約 {rounded} 位元 · {label}");
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                StatusText.Text = P("Nothing to copy yet.", "未有嘢可以複製。");
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
