using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 編碼解碼工具箱 · Encode / Decode — Base64, Base64URL, URL percent-encoding, HTML entities,
/// hex bytes and JWT decode, both directions. Pure managed, fully bilingual, never throws to the UI.
/// </summary>
public sealed partial class EncoderModule : Page
{
    private enum Mode { Base64, Base64Url, Url, Html, Hex, Jwt }

    private bool _ready;

    public EncoderModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { BuildCombos(); _ready = true; Render(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void BuildCombos()
    {
        int mode = ModeBox.SelectedIndex;
        int enc = EncodingBox.SelectedIndex;

        ModeBox.Items.Clear();
        ModeBox.Items.Add("Base64");
        ModeBox.Items.Add("Base64URL");
        ModeBox.Items.Add(P("URL percent-encoding", "URL 百分比編碼"));
        ModeBox.Items.Add(P("HTML entities", "HTML 實體"));
        ModeBox.Items.Add(P("Hex bytes", "十六進位位元組"));
        ModeBox.Items.Add(P("JWT decode", "JWT 解碼"));
        ModeBox.SelectedIndex = mode < 0 ? 0 : mode;

        EncodingBox.Items.Clear();
        EncodingBox.Items.Add("UTF-8");
        EncodingBox.Items.Add("ASCII");
        EncodingBox.SelectedIndex = enc < 0 ? 0 : enc;
    }

    private void Render()
    {
        if (!_ready) return;
        Header.Title = P("Encode / Decode", "編碼 / 解碼");
        HeaderBlurb.Text = P("Convert text between Base64, Base64URL, URL percent-encoding, HTML entities and hex bytes — or decode a JWT into readable JSON. All conversions run locally.",
            "喺 Base64、Base64URL、URL 百分比編碼、HTML 實體同十六進位位元組之間互換文字 — 又或者將 JWT 解成易睇嘅 JSON。全部喺本機運算。");
        ModeLabel.Text = P("Mode", "模式");
        EncodingLabel.Text = P("Text encoding", "文字編碼");
        InputLabel.Text = P("Input", "輸入");
        OutputLabel.Text = P("Output", "輸出");
        EncodeBtn.Content = P("Encode", "編碼");
        DecodeBtn.Content = P("Decode", "解碼");
        SwapBtn.Content = P("Swap ↑", "對調 ↑");
        CopyBtn.Content = P("Copy output", "複製輸出");

        BuildCombos();
        UpdateModeAffordances();
        if (string.IsNullOrEmpty(StatusText.Text))
            StatusText.Text = P("Ready.", "準備好。");
    }

    private Mode CurrentMode => (Mode)Math.Max(0, ModeBox.SelectedIndex);

    private EncoderService.TextEncoding CurrentEncoding =>
        EncodingBox.SelectedIndex == 1 ? EncoderService.TextEncoding.Ascii : EncoderService.TextEncoding.Utf8;

    private void UpdateModeAffordances()
    {
        if (!_ready) return;
        var m = CurrentMode;
        // Text-encoding only matters for byte-based modes.
        bool byteBased = m is Mode.Base64 or Mode.Base64Url or Mode.Hex;
        EncodingBox.IsEnabled = byteBased;
        // JWT is decode-only.
        bool jwt = m == Mode.Jwt;
        EncodeBtn.IsEnabled = !jwt;
    }

    private void Mode_Changed(object sender, SelectionChangedEventArgs e) => UpdateModeAffordances();

    private void Encode_Click(object sender, RoutedEventArgs e) => Run(encode: true);

    private void Decode_Click(object sender, RoutedEventArgs e) => Run(encode: false);

    private void Run(bool encode)
    {
        if (!_ready) return;
        string input = InputBox.Text ?? string.Empty;
        var enc = CurrentEncoding;
        try
        {
            string result;
            switch (CurrentMode)
            {
                case Mode.Base64:
                    result = encode ? EncoderService.Base64Encode(input, enc) : EncoderService.Base64Decode(input, enc);
                    break;
                case Mode.Base64Url:
                    result = encode ? EncoderService.Base64UrlEncode(input, enc) : EncoderService.Base64UrlDecode(input, enc);
                    break;
                case Mode.Url:
                    result = encode ? EncoderService.UrlEncode(input) : EncoderService.UrlDecode(input);
                    break;
                case Mode.Html:
                    result = encode ? EncoderService.HtmlEncode(input) : EncoderService.HtmlDecode(input);
                    break;
                case Mode.Hex:
                    result = encode ? EncoderService.HexEncode(input, enc) : EncoderService.HexDecode(input, enc);
                    break;
                case Mode.Jwt:
                    result = FormatJwt(EncoderService.DecodeJwt(input));
                    break;
                default:
                    result = string.Empty;
                    break;
            }
            OutputBox.Text = result;
            StatusText.Text = P($"Done — {result.Length} characters.", $"完成 — {result.Length} 個字元。");
        }
        catch (FormatException fx)
        {
            OutputBox.Text = string.Empty;
            StatusText.Text = P($"Malformed input: {fx.Message}", $"輸入格式錯誤：{fx.Message}");
        }
        catch (Exception ex)
        {
            OutputBox.Text = string.Empty;
            StatusText.Text = P($"Could not convert — {ex.Message}", $"轉換唔到 — {ex.Message}");
        }
    }

    private string FormatJwt(EncoderService.JwtParts j)
    {
        string sig = j.SegmentCount == 3
            ? (j.Signature.Length == 0 ? P("(empty)", "（空白）") : j.Signature)
            : P("(none — unsigned / 2-segment token)", "（無 — 未簽署 / 2 段式權杖）");
        return P("HEADER", "標頭") + "\n" + j.Header + "\n\n"
             + P("PAYLOAD", "內容") + "\n" + j.Payload + "\n\n"
             + P("SIGNATURE", "簽章") + "\n" + sig;
    }

    private void Swap_Click(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        InputBox.Text = OutputBox.Text ?? string.Empty;
        OutputBox.Text = string.Empty;
        StatusText.Text = P("Moved output into input.", "已將輸出移返做輸入。");
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        try
        {
            string text = OutputBox.Text ?? string.Empty;
            if (text.Length == 0)
            {
                StatusText.Text = P("Nothing to copy.", "冇嘢可以複製。");
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P($"Copy failed — {ex.Message}", $"複製失敗 — {ex.Message}");
        }
    }
}
