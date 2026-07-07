using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Ascii85 / Base85 · 編碼工具. Encode UTF-8 text or hex bytes to Adobe Ascii85, Z85 (ZeroMQ RFC 32),
/// or RFC 1924, and decode any of them back to text + hex. All work goes through
/// <see cref="Ascii85Service"/>, which never throws — this page just wires up controls, bilingual
/// labels and clipboard copy. Named language handler unsubscribed on Unloaded (no leak).
/// </summary>
public sealed partial class Ascii85Module : Page
{
    private bool _ready;

    public Ascii85Module()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        _ready = false;

        Header.Title = "Ascii85 / Base85 · 八十五進位編碼";
        Header.Subtitle = P("Adobe · Z85 · RFC 1924", "Adobe · Z85 · RFC 1924");
        HeaderBlurb.Text = P(
            "Encode text or raw bytes into compact base85, or decode base85 back to text and hex. Pick a variant — Adobe Ascii85 (PostScript/PDF, with optional <~ ~> wrapping and the 'z' all-zero shortcut), Z85 (ZeroMQ), or RFC 1924 (IPv6).",
            "將文字或者位元組壓成 base85，又或者將 base85 解返做文字同十六進位。揀個變體 — Adobe Ascii85（PostScript/PDF，可加 <~ ~> 包裝同 'z' 全零縮寫）、Z85（ZeroMQ）或者 RFC 1924（IPv6）。");

        int variant = Math.Max(0, VariantBox.SelectedIndex);
        int input = Math.Max(0, InputBox.SelectedIndex);

        VariantLabel.Text = P("Base85 variant", "Base85 變體");
        VariantBox.Items.Clear();
        VariantBox.Items.Add(P("Ascii85 (Adobe)", "Ascii85（Adobe）"));
        VariantBox.Items.Add(P("Z85 (ZeroMQ RFC 32)", "Z85（ZeroMQ RFC 32）"));
        VariantBox.Items.Add(P("RFC 1924 (IPv6)", "RFC 1924（IPv6）"));
        VariantBox.SelectedIndex = variant;

        InputLabel.Text = P("Interpret input as", "輸入解讀為");
        InputBox.Items.Clear();
        InputBox.Items.Add(P("UTF-8 text", "UTF-8 文字"));
        InputBox.Items.Add(P("Hex bytes", "十六進位位元組"));
        InputBox.SelectedIndex = input;

        WrapChk.Content = P("Wrap with <~ ~>", "加 <~ ~> 包裝");
        ZChk.Content = P("Use 'z' zero-run shortcut", "用 'z' 全零縮寫");

        EncodeTitle.Text = P("Encode  →  Base85", "編碼  →  Base85");
        PlainBox.PlaceholderText = P("Type text, or hex like 48 65 6C 6C 6F", "輸入文字，或者十六進位如 48 65 6C 6C 6F");
        EncodedBox.PlaceholderText = P("Base85 output appears here", "Base85 結果喺呢度");
        EncodeBtn.Content = P("Encode", "編碼");
        CopyEncodedBtn.Content = P("Copy Base85", "複製 Base85");

        DecodeTitle.Text = P("Decode  →  text + hex", "解碼  →  文字 + 十六進位");
        CipherBox.PlaceholderText = P("Paste Base85 here", "喺呢度貼上 Base85");
        DecodeTextLabel.Text = P("As UTF-8 text", "UTF-8 文字");
        DecodeHexLabel.Text = P("As hex bytes", "十六進位位元組");
        DecodeBtn.Content = P("Decode", "解碼");
        CopyTextBtn.Content = P("Copy text", "複製文字");
        CopyHexBtn.Content = P("Copy hex", "複製十六進位");

        UpdateVariantUi();
        SetStatus(P("Ready.", "準備就緒。"), InfoBarSeverity.Informational);
        _ready = true;
    }

    private Ascii85Service.Variant CurrentVariant() => VariantBox.SelectedIndex switch
    {
        1 => Ascii85Service.Variant.Z85,
        2 => Ascii85Service.Variant.Rfc1924,
        _ => Ascii85Service.Variant.Adobe,
    };

    private Ascii85Service.InputKind CurrentInput()
        => InputBox.SelectedIndex == 1 ? Ascii85Service.InputKind.HexBytes : Ascii85Service.InputKind.Utf8Text;

    private void UpdateVariantUi()
    {
        bool adobe = CurrentVariant() == Ascii85Service.Variant.Adobe;
        AdobeOptions.Visibility = adobe ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetStatus(string message, InfoBarSeverity severity)
    {
        Status.Message = message;
        Status.Severity = severity;
        Status.IsOpen = true;
    }

    // ---- events ----

    private void Variant_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        UpdateVariantUi();
    }

    private void Input_Changed(object sender, SelectionChangedEventArgs e) { /* re-read on next encode */ }

    private void Adobe_Changed(object sender, RoutedEventArgs e) { /* applied on next encode */ }

    private void Plain_Changed(object sender, TextChangedEventArgs e) { /* live encode intentionally off */ }

    private void Encode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var toBytes = Ascii85Service.InputToBytes(PlainBox.Text, CurrentInput());
            if (!toBytes.Ok || toBytes.Bytes is null)
            {
                EncodedBox.Text = string.Empty;
                SetStatus(toBytes.Error ?? P("Invalid input.", "輸入無效。"), InfoBarSeverity.Error);
                return;
            }

            var enc = Ascii85Service.Encode(toBytes.Bytes, CurrentVariant(), WrapChk.IsChecked == true, ZChk.IsChecked == true);
            if (!enc.Ok || enc.Text is null)
            {
                EncodedBox.Text = string.Empty;
                SetStatus(enc.Error ?? P("Encoding failed.", "編碼失敗。"), InfoBarSeverity.Error);
                return;
            }

            EncodedBox.Text = enc.Text;
            int n = toBytes.Bytes.Length;
            EncodeInfo.Text = P($"{n} byte(s) in  →  {enc.Text.Length} char(s) out", $"入 {n} 位元組  →  出 {enc.Text.Length} 字元");
            SetStatus(P("Encoded.", "已編碼。"), InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            SetStatus(P("Unexpected error while encoding. ", "編碼時發生非預期錯誤。 ") + ex.Message, InfoBarSeverity.Error);
        }
    }

    private void Decode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dec = Ascii85Service.Decode(CipherBox.Text, CurrentVariant());
            if (!dec.Ok || dec.Bytes is null)
            {
                DecodedTextBox.Text = string.Empty;
                DecodedHexBox.Text = string.Empty;
                SetStatus(dec.Error ?? P("Invalid Base85.", "Base85 無效。"), InfoBarSeverity.Error);
                return;
            }

            DecodedTextBox.Text = Ascii85Service.BytesToUtf8(dec.Bytes);
            DecodedHexBox.Text = Ascii85Service.BytesToHex(dec.Bytes);
            DecodeInfo.Text = P($"{dec.Bytes.Length} byte(s) decoded", $"解出 {dec.Bytes.Length} 位元組");
            SetStatus(P("Decoded.", "已解碼。"), InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            SetStatus(P("Unexpected error while decoding. ", "解碼時發生非預期錯誤。 ") + ex.Message, InfoBarSeverity.Error);
        }
    }

    private void CopyEncoded_Click(object sender, RoutedEventArgs e) => Copy(EncodedBox.Text, P("Base85", "Base85"));
    private void CopyText_Click(object sender, RoutedEventArgs e) => Copy(DecodedTextBox.Text, P("text", "文字"));
    private void CopyHex_Click(object sender, RoutedEventArgs e) => Copy(DecodedHexBox.Text, P("hex", "十六進位"));

    private void Copy(string? value, string what)
    {
        try
        {
            if (string.IsNullOrEmpty(value))
            {
                SetStatus(P($"Nothing to copy — run it first.", "冇嘢可以複製 — 先執行一次。"), InfoBarSeverity.Warning);
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(value);
            Clipboard.SetContent(pkg);
            SetStatus(P($"Copied {what} to the clipboard.", $"已複製{what}到剪貼簿。"), InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            SetStatus(P("Could not copy to the clipboard. ", "複製到剪貼簿失敗。 ") + ex.Message, InfoBarSeverity.Error);
        }
    }
}
