using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 檢查碼／校驗碼驗證器 · Check-digit / checksum validator — pick a scheme (Luhn / credit card,
/// ISBN-10, ISBN-13, EAN-13, UPC-A, IBAN), type a value, and see valid/invalid plus the expected
/// check digit (and card brand for Luhn). Pure managed C#, never throws. Bilingual.
/// </summary>
public sealed partial class CheckDigitModule : Page
{
    private bool _suppress;

    private static readonly CheckDigitService.Scheme[] _schemes =
    {
        CheckDigitService.Scheme.Luhn,
        CheckDigitService.Scheme.Isbn10,
        CheckDigitService.Scheme.Isbn13,
        CheckDigitService.Scheme.Ean13,
        CheckDigitService.Scheme.UpcA,
        CheckDigitService.Scheme.Iban,
    };

    public CheckDigitModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) { Render(); Evaluate(); }
    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;
    private void OnLang(object? sender, EventArgs e) { Render(); Evaluate(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private static string SchemeName(CheckDigitService.Scheme s, Func<string, string, string> p) => s switch
    {
        CheckDigitService.Scheme.Luhn => p("Luhn (credit card)", "Luhn（信用卡）"),
        CheckDigitService.Scheme.Isbn10 => "ISBN-10",
        CheckDigitService.Scheme.Isbn13 => "ISBN-13",
        CheckDigitService.Scheme.Ean13 => "EAN-13",
        CheckDigitService.Scheme.UpcA => "UPC-A",
        CheckDigitService.Scheme.Iban => p("IBAN (mod-97)", "IBAN（mod-97）"),
        _ => s.ToString(),
    };

    private void Render()
    {
        Header.Title = "Check Digit Validator · 檢查碼驗證器";
        HeaderBlurb.Text = P("Validate a number's check digit or checksum and see the value it should be. Handles credit cards (Luhn, with brand detection), book & product barcodes and bank IBANs.",
            "驗證一個號碼嘅檢查碼／校驗碼，同埋顯示應該係咩。支援信用卡（Luhn，仲會認卡種）、書籍同商品條碼，以及銀行 IBAN。");
        SchemeLabel.Text = P("Scheme", "格式");
        InputLabel.Text = P("Value to check", "要檢查嘅數值");

        int keep = SchemeBox.SelectedIndex;
        _suppress = true;
        SchemeBox.Items.Clear();
        foreach (var s in _schemes) SchemeBox.Items.Add(SchemeName(s, P));
        SchemeBox.SelectedIndex = keep >= 0 ? keep : 0;
        _suppress = false;

        InputBox.PlaceholderText = P("e.g. 4111 1111 1111 1111", "例如 4111 1111 1111 1111");
    }

    private void Scheme_Changed(object sender, SelectionChangedEventArgs e) { if (!_suppress) Evaluate(); }
    private void Input_Changed(object sender, TextChangedEventArgs e) { if (!_suppress) Evaluate(); }

    private void Evaluate()
    {
        int idx = SchemeBox.SelectedIndex;
        if (idx < 0 || idx >= _schemes.Length) return;
        var scheme = _schemes[idx];

        string text = InputBox.Text ?? "";
        if (text.Trim().Length == 0)
        {
            SetBadge(null);
            DetailText.Text = "";
            StatusText.Text = P("Type a value above to check it.", "喺上面輸入數值嚟檢查。");
            return;
        }

        CheckDigitService.Result r;
        try { r = CheckDigitService.Validate(scheme, text); }
        catch (Exception ex)
        {
            SetBadge(null);
            DetailText.Text = "";
            StatusText.Text = P("Error: " + ex.Message, "錯誤：" + ex.Message);
            return;
        }

        if (!r.Ok)
        {
            SetBadge(null);
            DetailText.Text = "";
            StatusText.Text = P(r.Detail, r.DetailZh);
            return;
        }

        SetBadge(r.Valid);
        DetailText.Text = P(r.Detail, r.DetailZh);
        StatusText.Text = r.Valid
            ? P("Check digit / checksum matches.", "檢查碼／校驗碼吻合。")
            : P("Check digit / checksum does NOT match.", "檢查碼／校驗碼唔吻合。");
    }

    private void SetBadge(bool? valid)
    {
        if (valid is null)
        {
            BadgeText.Text = P("—", "—");
            Badge.Background = new SolidColorBrush(Colors.Gray) { Opacity = 0.35 };
            return;
        }
        if (valid.Value)
        {
            BadgeText.Text = P("VALID", "有效");
            Badge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x1E, 0x7A, 0x34));
        }
        else
        {
            BadgeText.Text = P("INVALID", "無效");
            Badge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x9B, 0x22, 0x26));
        }
    }
}
