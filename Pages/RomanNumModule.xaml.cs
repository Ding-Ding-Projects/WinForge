using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 羅馬數字轉換 · Roman-numeral converter. Two-way live conversion:
///   • number → Roman (1..3999 standard; Extended toggle → 1..3,999,999 via vinculum overlines);
///   • Roman → number with strict well-formedness validation (rejects IIII, VV, IC, …).
/// Shows the additive breakdown, copy buttons, bilingual status. Never throws.
/// </summary>
public sealed partial class RomanNumModule : Page
{
    private string _lastRoman = "";
    private string _lastNumber = "";

    public RomanNumModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Roman Numerals · 羅馬數字";
            HeaderBlurb.Text = P("Convert whole numbers to Roman numerals and back. Standard range is 1–3999; turn on Extended for values up to 3,999,999 using vinculum (overline ×1000) notation.",
                "整數同羅馬數字互相轉換。標準範圍係 1–3999；開啟「擴充」可以用橫線（overline ×1000）寫法去到 3,999,999。");
            ExtendedSwitch.Header = P("Extended range (vinculum)", "擴充範圍（橫線記法）");
            ExtendedNote.Text = P("A bar over a letter multiplies it ×1000 (e.g. M̅ = 1,000,000). Input also accepts the (x)×1000 parenthetical form.",
                "字母上面加橫線即係 ×1000（例如 M̅ = 1,000,000）。輸入亦接受 (x)×1000 括號寫法。");
            N2RTitle.Text = P("Number → Roman", "數字 → 羅馬");
            R2NTitle.Text = P("Roman → Number", "羅馬 → 數字");
            CopyRomanBtn.Content = P("Copy", "複製");
            CopyNumberBtn.Content = P("Copy", "複製");

            // Re-run both conversions so localized labels/errors refresh.
            ConvertNumberToRoman();
            ConvertRomanToNumber();
        }
        catch { /* never throw from UI */ }
    }

    private bool Extended => ExtendedSwitch.IsOn;

    private void Extended_Toggled(object sender, RoutedEventArgs e)
    {
        ConvertNumberToRoman();
        ConvertRomanToNumber();
    }

    private void Number_Changed(object sender, TextChangedEventArgs e) => ConvertNumberToRoman();

    private void Roman_Changed(object sender, TextChangedEventArgs e) => ConvertRomanToNumber();

    private void ConvertNumberToRoman()
    {
        try
        {
            string text = NumberInput?.Text ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                _lastRoman = "";
                RomanOut.Text = "";
                RomanBreakdown.Text = "";
                CopyRomanBtn.IsEnabled = false;
                return;
            }

            if (!RomanNumService.TryParseInt(text, out long n))
            {
                _lastRoman = "";
                RomanOut.Text = "—";
                RomanBreakdown.Text = "";
                CopyRomanBtn.IsEnabled = false;
                ShowError(P("Enter a whole number.", "請輸入整數。"));
                return;
            }

            var r = RomanNumService.ToRoman(n, Extended);
            if (!r.Ok)
            {
                _lastRoman = "";
                RomanOut.Text = "—";
                RomanBreakdown.Text = "";
                CopyRomanBtn.IsEnabled = false;
                ShowError(P(r.ReasonEn, r.ReasonZh));
                return;
            }

            _lastRoman = r.Roman;
            RomanOut.Text = r.Roman;
            RomanBreakdown.Text = P($"{n:N0} = {r.Breakdown}", $"{n:N0} = {r.Breakdown}");
            CopyRomanBtn.IsEnabled = true;
            Status.IsOpen = false;
        }
        catch (Exception ex)
        {
            ShowError(P("Conversion error: " + ex.Message, "轉換錯誤：" + ex.Message));
        }
    }

    private void ConvertRomanToNumber()
    {
        try
        {
            string text = RomanInput?.Text ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                _lastNumber = "";
                NumberOut.Text = "";
                NumberBreakdown.Text = "";
                CopyNumberBtn.IsEnabled = false;
                return;
            }

            var r = RomanNumService.ToNumber(text, Extended);
            if (!r.Ok)
            {
                _lastNumber = "";
                NumberOut.Text = "—";
                NumberBreakdown.Text = "";
                CopyNumberBtn.IsEnabled = false;
                ShowError(P(r.ReasonEn, r.ReasonZh));
                return;
            }

            _lastNumber = r.Value.ToString("N0");
            NumberOut.Text = _lastNumber;
            NumberBreakdown.Text = string.IsNullOrEmpty(r.Breakdown) ? "" : P($"= {r.Breakdown}", $"= {r.Breakdown}");
            CopyNumberBtn.IsEnabled = true;
            Status.IsOpen = false;
        }
        catch (Exception ex)
        {
            ShowError(P("Parse error: " + ex.Message, "解析錯誤：" + ex.Message));
        }
    }

    private void ShowError(string msg)
    {
        try
        {
            Status.Severity = InfoBarSeverity.Warning;
            Status.Message = msg;
            Status.IsOpen = true;
        }
        catch { }
    }

    private void CopyRoman_Click(object sender, RoutedEventArgs e) => CopyToClipboard(_lastRoman);

    private void CopyNumber_Click(object sender, RoutedEventArgs e) => CopyToClipboard(_lastNumber);

    private void CopyToClipboard(string value)
    {
        try
        {
            if (string.IsNullOrEmpty(value)) return;
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(value);
            Clipboard.SetContent(dp);
            Status.Severity = InfoBarSeverity.Success;
            Status.Message = P($"Copied: {value}", $"已複製：{value}");
            Status.IsOpen = true;
        }
        catch (Exception ex)
        {
            ShowError(P("Copy failed: " + ex.Message, "複製失敗：" + ex.Message));
        }
    }
}
