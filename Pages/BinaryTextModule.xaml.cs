using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 文字 ↔ 二進位／編碼 · Text ↔ binary / numeric-code converter. Encodes text's UTF-8 bytes as
/// space-separated codes in a chosen base (Binary/Decimal/Octal/Hex, binary padded to 8 bits) and
/// parses them back. Pure managed, never throws — malformed input just shows a bilingual status.
/// </summary>
public sealed partial class BinaryTextModule : Page
{
    private bool _suppress;

    public BinaryTextModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Render();

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Text to Binary · 文字轉二進位";
        HeaderBlurb.Text = P("Turn text into space-separated numeric codes and back. Encodes the text's UTF-8 bytes in the base you pick — binary, decimal, octal or hex.",
            "將文字轉成用空格分隔嘅數字碼，又轉得返轉頭。用你揀嘅進位（二進位、十進位、八進位或十六進位）將文字嘅 UTF-8 位元組編碼。");

        BaseLabel.Text = P("Numeric base", "數字進位");
        int keep = BaseCombo.SelectedIndex < 0 ? 0 : BaseCombo.SelectedIndex;
        _suppress = true;
        BaseCombo.Items.Clear();
        BaseCombo.Items.Add(P("Binary (base 2)", "二進位（2 進）"));
        BaseCombo.Items.Add(P("Decimal (base 10)", "十進位（10 進）"));
        BaseCombo.Items.Add(P("Octal (base 8)", "八進位（8 進）"));
        BaseCombo.Items.Add(P("Hex (base 16)", "十六進位（16 進）"));
        BaseCombo.SelectedIndex = keep;
        _suppress = false;

        EncodingNote.Text = P("Codes represent raw UTF-8 bytes (0–255). Binary is padded to 8 bits per byte; separate codes with spaces.",
            "數字碼代表原始 UTF-8 位元組（0–255）。二進位每個位元組補足 8 個位；各個碼用空格分隔。");

        InputLabel.Text = P("Input", "輸入");
        OutputLabel.Text = P("Output", "輸出");
        EncodeBtn.Content = P("Text → codes", "文字 → 數字碼");
        DecodeBtn.Content = P("Codes → text", "數字碼 → 文字");
        SwapBtn.Content = P("Move output to input", "將輸出搬去輸入");
        CopyBtn.Content = P("Copy output", "複製輸出");

        if (string.IsNullOrEmpty(StatusText.Text))
            StatusText.Text = P("Ready.", "準備好。");
    }

    private BinaryTextService.NumBase SelectedBase() => BaseCombo.SelectedIndex switch
    {
        1 => BinaryTextService.NumBase.Decimal,
        2 => BinaryTextService.NumBase.Octal,
        3 => BinaryTextService.NumBase.Hex,
        _ => BinaryTextService.NumBase.Binary,
    };

    private void Base_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        Status(P("Base changed — press a convert button.", "已轉進位 — 撳一下轉換掣。"), false);
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        // Passive: don't auto-convert (input could be either direction). Just note readiness.
    }

    private void Encode_Click(object sender, RoutedEventArgs e)
    {
        var r = BinaryTextService.Encode(InputBox.Text, SelectedBase());
        if (r.Ok)
        {
            OutputBox.Text = r.Text;
            Status(P("Encoded to codes.", "已編碼成數字碼。"), false);
        }
        else
        {
            Status(P("Could not encode the text.", "無法編碼呢段文字。"), true);
        }
    }

    private void Decode_Click(object sender, RoutedEventArgs e)
    {
        var r = BinaryTextService.Decode(InputBox.Text, SelectedBase());
        if (r.Ok)
        {
            OutputBox.Text = r.Text;
            Status(P("Decoded back to text.", "已解碼返做文字。"), false);
        }
        else
        {
            OutputBox.Text = string.Empty;
            Status(P("Some codes were not valid for this base — nothing decoded.", "有啲數字碼喺呢個進位無效 — 冇解碼到。"), true);
        }
    }

    private void Swap_Click(object sender, RoutedEventArgs e)
    {
        _suppress = true;
        InputBox.Text = OutputBox.Text ?? string.Empty;
        OutputBox.Text = string.Empty;
        _suppress = false;
        Status(P("Output moved to input.", "已將輸出搬去輸入。"), false);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                Status(P("Nothing to copy.", "冇嘢可以複製。"), true);
                return;
            }
            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            Status(P("Output copied to clipboard.", "已將輸出複製去剪貼簿。"), false);
        }
        catch
        {
            Status(P("Could not access the clipboard.", "無法存取剪貼簿。"), true);
        }
    }

    private void Status(string text, bool warn)
    {
        StatusText.Text = text;
        StatusText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
            warn ? "SystemFillColorCautionBrush" : "TextFillColorSecondaryBrush"];
    }
}
