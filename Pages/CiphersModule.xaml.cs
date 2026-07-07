using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 經典密碼 · Classic ciphers — ROT13, Caesar, Atbash, Vigenère, A1Z26 and Morse. Encode/decode text,
/// copy the result. All transforms live in <see cref="CiphersService"/>; this page never throws and
/// reports bad input (e.g. an empty Vigenère key) through a bilingual status line. Bilingual, no redirect.
/// </summary>
public sealed partial class CiphersModule : Page
{
    private bool _ready;

    public CiphersModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_ready)
        {
            for (int i = 0; i < 6; i++) ModeBox.Items.Add(new ComboBoxItem());
            ModeBox.SelectedIndex = 0;
            _ready = true;
        }
        Render();
        UpdateVisibility();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLanguageChanged;

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Classic Ciphers · 經典密碼";
        HeaderBlurb.Text = P("Encode or decode text with classic ciphers — ROT13, Caesar, Atbash, Vigenère, A1Z26 and Morse. Runs entirely on your PC.",
            "用經典密碼將文字加密或解密 — ROT13、凱撒、阿特巴希、維吉尼亞、A1Z26 同摩斯電碼。全部喺你部電腦本機運行。");

        ModeLabel.Text = P("Cipher", "密碼");
        ShiftLabel.Text = P("Shift", "位移");
        KeyLabel.Text = P("Key (letters only)", "密鑰（只限英文字母）");
        InputLabel.Text = P("Input", "輸入");
        OutputLabel.Text = P("Output", "輸出");

        EncodeBtn.Content = P("Encode", "加密");
        DecodeBtn.Content = P("Decode", "解密");
        CopyBtn.Content = P("Copy output", "複製輸出");
        ClearBtn.Content = P("Clear", "清除");

        string[] en = { "ROT13", "Caesar", "Atbash", "Vigenère", "A1Z26", "Morse code" };
        string[] zh = { "ROT13", "凱撒密碼", "阿特巴希", "維吉尼亞", "A1Z26", "摩斯電碼" };
        for (int i = 0; i < ModeBox.Items.Count; i++)
            if (ModeBox.Items[i] is ComboBoxItem it) it.Content = P(en[i], zh[i]);

        if (StatusText.Text.Length == 0) StatusText.Text = P("Ready.", "準備就緒。");
    }

    private CiphersService.Mode CurrentMode() => (CiphersService.Mode)Math.Max(0, ModeBox.SelectedIndex);

    private void Mode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        UpdateVisibility();
        StatusText.Text = P("Ready.", "準備就緒。");
    }

    private void UpdateVisibility()
    {
        var m = CurrentMode();
        ShiftPanel.Visibility = m == CiphersService.Mode.Caesar ? Visibility.Visible : Visibility.Collapsed;
        KeyPanel.Visibility = m == CiphersService.Mode.Vigenere ? Visibility.Visible : Visibility.Collapsed;

        // ROT13, Atbash and Morse have a single direction / are self-inverse — one button reads clearer.
        bool selfInverse = m is CiphersService.Mode.Rot13 or CiphersService.Mode.Atbash;
        DecodeBtn.Visibility = selfInverse ? Visibility.Collapsed : Visibility.Visible;
        EncodeBtn.Content = selfInverse ? P("Transform", "轉換") : P("Encode", "加密");
    }

    private void Encode_Click(object sender, RoutedEventArgs e) => Run(true);
    private void Decode_Click(object sender, RoutedEventArgs e) => Run(false);

    private void Run(bool encode)
    {
        int shift = (int)(double.IsNaN(ShiftBox.Value) ? 0 : ShiftBox.Value);
        var r = CiphersService.Transform(CurrentMode(), InputBox.Text, encode, shift, KeyBox.Text);
        if (r.Ok)
        {
            OutputBox.Text = r.Text;
            StatusText.Text = P("Done.", "完成。");
        }
        else
        {
            StatusText.Text = P(r.ErrorEn, r.ErrorZh);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (OutputBox.Text.Length == 0)
            {
                StatusText.Text = P("Nothing to copy yet.", "暫時未有嘢可以複製。");
                return;
            }
            var dp = new DataPackage();
            dp.SetText(OutputBox.Text);
            Clipboard.SetContent(dp);
            StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Copy failed: " + ex.Message, "複製失敗：" + ex.Message);
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        InputBox.Text = string.Empty;
        OutputBox.Text = string.Empty;
        StatusText.Text = P("Cleared.", "已清除。");
    }
}
