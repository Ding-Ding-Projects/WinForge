using System;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// TOTP/HOTP 驗證器 · RFC 6238 authenticator — enter a Base32 secret (or paste an
/// <c>otpauth://totp/...</c> URI), pick digits / period / algorithm and see the live code
/// with a countdown ring. Pure managed C# (System.Security.Cryptography). Bilingual, never throws.
/// </summary>
public sealed partial class TotpModule : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string _lastCode = "";

    public TotpModule()
    {
        InitializeComponent();
        _timer.Tick += OnTick;
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (AlgoBox.SelectedIndex < 0) AlgoBox.SelectedIndex = 0;
        Render();
        Refresh();
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        _timer.Tick -= OnTick;
        _timer.Stop();
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private void OnTick(object? sender, object e) => Refresh();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "TOTP Authenticator · TOTP 驗證器";
        HeaderBlurb.Text = P("Generate RFC 6238 time-based one-time codes from a Base32 secret — the same six-digit codes as Google Authenticator, Authy or Microsoft Authenticator. All local; nothing leaves this PC.",
            "由 Base32 密鑰計出 RFC 6238 時間型一次性驗證碼 — 同 Google Authenticator、Authy 或 Microsoft Authenticator 一樣嘅六位數。全部喺本機計，冇任何嘢送出電腦。");
        CodeLabel.Text = P("Current code", "目前驗證碼");
        CopyBtn.Content = P("Copy", "複製");
        SecretLabel.Text = P("Base32 secret", "Base32 密鑰");
        UriLabel.Text = P("otpauth:// URI (optional)", "otpauth:// 連結（可選）");
        ImportBtn.Content = P("Import", "匯入");
        DigitsLabel.Text = P("Digits", "位數");
        PeriodLabel.Text = P("Period (s)", "週期（秒）");
        AlgoLabel.Text = P("Algorithm", "演算法");
        Refresh();
    }

    private static TotpService.HashAlgo AlgoFromIndex(int index) => index switch
    {
        1 => TotpService.HashAlgo.Sha256,
        2 => TotpService.HashAlgo.Sha512,
        _ => TotpService.HashAlgo.Sha1,
    };

    private void Refresh()
    {
        try
        {
            int digits = (int)(double.IsNaN(DigitsBox.Value) ? 6 : DigitsBox.Value);
            int period = (int)(double.IsNaN(PeriodBox.Value) ? 30 : PeriodBox.Value);
            var algo = AlgoFromIndex(AlgoBox.SelectedIndex);
            string secret = SecretBox.Text ?? "";

            if (string.IsNullOrWhiteSpace(secret))
            {
                _lastCode = "";
                CodeText.Text = "— — —";
                CountText.Text = "—";
                CountRing.Value = 0;
                StatusText.Text = P("Enter a Base32 secret to start.", "輸入 Base32 密鑰就可以開始。");
                return;
            }

            if (TotpService.DecodeBase32(secret) is null)
            {
                _lastCode = "";
                CodeText.Text = "— — —";
                CountText.Text = "—";
                CountRing.Value = 0;
                StatusText.Text = P("Invalid Base32 secret — allowed characters are A–Z and 2–7.", "Base32 密鑰無效 — 只可以用 A–Z 同 2–7。");
                return;
            }

            long now = TotpService.UnixNow();
            string? code = TotpService.Compute(secret, digits, period, algo, now);
            if (code is null)
            {
                _lastCode = "";
                CodeText.Text = "— — —";
                StatusText.Text = P("Could not generate a code — check the parameters.", "無法產生驗證碼 — 請檢查參數。");
                return;
            }

            _lastCode = code;
            CodeText.Text = Spaced(code);

            int remaining = TotpService.SecondsRemaining(period, now);
            CountText.Text = remaining.ToString();
            CountRing.Value = period > 0 ? Math.Clamp(remaining * 100.0 / period, 0, 100) : 0;
            StatusText.Text = P($"Refreshes in {remaining}s · {digits} digits · {period}s step · {algo.ToString().ToUpperInvariant()}",
                $"{remaining} 秒後更新 · {digits} 位 · {period} 秒週期 · {algo.ToString().ToUpperInvariant()}");
        }
        catch
        {
            StatusText.Text = P("Something went wrong generating the code.", "產生驗證碼時發生錯誤。");
        }
    }

    private static string Spaced(string code)
    {
        if (code.Length == 6) return code.Substring(0, 3) + " " + code.Substring(3);
        if (code.Length == 8) return code.Substring(0, 4) + " " + code.Substring(4);
        return code;
    }

    private void Config_Changed(object sender, TextChangedEventArgs e) => Refresh();

    private void Number_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) => Refresh();

    private void Algo_Changed(object sender, SelectionChangedEventArgs e) => Refresh();

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_lastCode))
            {
                StatusText.Text = P("No code to copy yet.", "暫時未有驗證碼可以複製。");
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(_lastCode);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Code copied to clipboard.", "驗證碼已複製到剪貼簿。");
        }
        catch
        {
            StatusText.Text = P("Could not copy to the clipboard.", "無法複製到剪貼簿。");
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var parsed = TotpService.ParseUri(UriBox.Text);
            if (parsed is null)
            {
                StatusText.Text = P("Not a valid otpauth://totp/ URI.", "唔係有效嘅 otpauth://totp/ 連結。");
                return;
            }

            SecretBox.Text = parsed.Secret;
            DigitsBox.Value = parsed.Digits;
            PeriodBox.Value = parsed.Period;
            AlgoBox.SelectedIndex = parsed.Algorithm switch
            {
                TotpService.HashAlgo.Sha256 => 1,
                TotpService.HashAlgo.Sha512 => 2,
                _ => 0,
            };

            string who = parsed.Issuer ?? parsed.Label ?? "";
            StatusText.Text = string.IsNullOrEmpty(who)
                ? P("Imported from URI.", "已由連結匯入。")
                : P($"Imported \"{who}\" from URI.", $"已由連結匯入「{who}」。");
            Refresh();
        }
        catch
        {
            StatusText.Text = P("Could not import that URI.", "無法匯入呢個連結。");
        }
    }
}
