using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Base32 / Base58 / Ascii85 編解碼器 · Encode/decode the UTF-8 bytes of the input with the chosen codec.
/// Base32 (RFC 4648, padded / no-pad), Base58 (Bitcoin), Ascii85 (Adobe). Never throws — malformed input
/// shows a bilingual status. Clipboard via DataPackage. Bilingual, no redirect.
/// </summary>
public sealed partial class Base32Module : Page
{
    private static readonly Base32Service.Codec[] Codecs =
    {
        Base32Service.Codec.Base32,
        Base32Service.Codec.Base32NoPad,
        Base32Service.Codec.Base58,
        Base32Service.Codec.Ascii85,
    };

    public Base32Module()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += OnUnloaded;
        Loaded += (_, _) => Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Unloaded -= OnUnloaded;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Base32 / 58 / 85 · 編解碼";
        HeaderBlurb.Text = P(
            "Encode or decode text with Base32 (RFC 4648), Base58 (Bitcoin) or Ascii85 (Adobe). Everything runs on the UTF-8 bytes of your input.",
            "用 Base32（RFC 4648）、Base58（比特幣）或者 Ascii85（Adobe）嚟編碼或者解碼文字。全部都係喺你輸入嘅 UTF-8 位元組上面運作。");
        ModeLabel.Text = P("Codec", "編碼方式");
        InputLabel.Text = P("Input", "輸入");
        OutputLabel.Text = P("Output", "輸出");
        EncodeBtn.Content = P("Encode", "編碼");
        DecodeBtn.Content = P("Decode", "解碼");
        SwapBtn.Content = P("Swap", "對調");
        CopyBtn.Content = P("Copy output", "複製輸出");
        EncodingNote.Text = P("Text encoding: UTF-8.", "文字編碼：UTF-8。");

        int sel = ModeCombo.SelectedIndex < 0 ? 0 : ModeCombo.SelectedIndex;
        ModeCombo.Items.Clear();
        ModeCombo.Items.Add(P("Base32 (RFC 4648, padded)", "Base32（RFC 4648，有填充）"));
        ModeCombo.Items.Add(P("Base32 (no padding)", "Base32（無填充）"));
        ModeCombo.Items.Add(P("Base58 (Bitcoin)", "Base58（比特幣）"));
        ModeCombo.Items.Add(P("Ascii85 (Adobe)", "Ascii85（Adobe）"));
        ModeCombo.SelectedIndex = sel;

        if (string.IsNullOrEmpty(StatusText.Text))
            StatusText.Text = P("Ready.", "準備就緒。");
    }

    private Base32Service.Codec Selected()
    {
        int i = ModeCombo.SelectedIndex;
        if (i < 0 || i >= Codecs.Length) i = 0;
        return Codecs[i];
    }

    private void Mode_Changed(object sender, SelectionChangedEventArgs e)
    {
        StatusText.Text = P("Codec changed.", "已切換編碼方式。");
    }

    private void Encode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            OutputBox.Text = Base32Service.Encode(InputBox.Text ?? string.Empty, Selected());
            StatusText.Text = P("Encoded.", "已編碼。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P($"Could not encode: {ex.Message}", $"無法編碼：{ex.Message}");
        }
    }

    private void Decode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            OutputBox.Text = Base32Service.Decode(InputBox.Text ?? string.Empty, Selected());
            StatusText.Text = P("Decoded.", "已解碼。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P($"Not valid {Selected()} input: {ex.Message}", $"唔係有效嘅 {Selected()} 輸入：{ex.Message}");
        }
    }

    private void Swap_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            (InputBox.Text, OutputBox.Text) = (OutputBox.Text ?? string.Empty, string.Empty);
            StatusText.Text = P("Output moved to input.", "已將輸出移去輸入。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P($"Could not swap: {ex.Message}", $"無法對調：{ex.Message}");
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? string.Empty;
            if (text.Length == 0)
            {
                StatusText.Text = P("Nothing to copy.", "冇嘢可以複製。");
                return;
            }
            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P($"Could not copy: {ex.Message}", $"無法複製：{ex.Message}");
        }
    }
}
