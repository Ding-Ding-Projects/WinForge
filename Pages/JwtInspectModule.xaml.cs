using System;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// JWT inspector &amp; verifier · JWT 檢查同驗證. Paste a token → decode header/payload as pretty JSON, read
/// standard claims with human times, show valid/expired/not-yet-valid, and verify HMAC (HS256/384/512)
/// signatures locally. Pure managed; never throws — malformed tokens surface as bilingual status.
/// </summary>
public sealed partial class JwtInspectModule : Page
{
    private static readonly Color GreenColor = Color.FromArgb(0xFF, 0x2E, 0x7D, 0x32);
    private static readonly Color RedColor = Color.FromArgb(0xFF, 0xC6, 0x28, 0x28);
    private static readonly Color AmberColor = Color.FromArgb(0xFF, 0xB2, 0x6A, 0x00);
    private static readonly Color GrayColor = Color.FromArgb(0xFF, 0x60, 0x60, 0x60);

    public JwtInspectModule()
    {
        InitializeComponent();
        AlgBox.SelectedIndex = 0;
        Loc.I.LanguageChanged += OnLang;
        Unloaded += OnUnloaded;
        Loaded += (_, _) => { Render(); Refresh(); };
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Unloaded -= OnUnloaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "JWT Inspector · JWT 檢查器";
        HeaderBlurb.Text = P(
            "Paste a JSON Web Token to decode its header and payload, read the standard claims, and verify an HMAC signature — all locally. Nothing leaves this PC.",
            "貼一個 JSON Web Token（JWT）落嚟，解碼佢嘅標頭同內容、睇標準聲明，仲可以本機驗證 HMAC 簽名。所有嘢都唔會離開部電腦。");
        TokenLabel.Text = P("Token", "權杖 Token");
        ClaimsTitle.Text = P("Standard claims", "標準聲明");
        HeaderJsonTitle.Text = P("Header", "標頭 Header");
        PayloadJsonTitle.Text = P("Payload", "內容 Payload");
        SignatureTitle.Text = P("Signature (raw)", "簽名（原始）");
        VerifyTitle.Text = P("Verify signature (HMAC only)", "驗證簽名（只支援 HMAC）");
        VerifyNote.Text = P(
            "Enter the shared secret and pick the algorithm. RSA (RS*) and ECDSA (ES*) verification is not supported.",
            "輸入共用密鑰再揀演算法。唔支援 RSA（RS*）同 ECDSA（ES*）驗證。");
        AlgLabel.Text = P("Algorithm", "演算法");
        SecretBox.PlaceholderText = P("HMAC secret", "HMAC 密鑰");
        Refresh();
    }

    private void Token_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void Verify_Changed(object sender, object e) => RefreshVerify(SafeDecode());

    private JwtInspectService.DecodedJwt SafeDecode()
    {
        try { return JwtInspectService.Decode(TokenBox.Text); }
        catch { return new JwtInspectService.DecodedJwt { Ok = false, Error = "unknown" }; }
    }

    private void Refresh()
    {
        try
        {
            var text = TokenBox?.Text ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                StatusText.Text = P("Paste a token above to begin.", "喺上面貼一個權杖開始。");
                HideResults();
                return;
            }

            var d = JwtInspectService.Decode(text);
            if (!d.Ok)
            {
                StatusText.Text = d.Error switch
                {
                    "parts" => P("Not a JWT — expected three dot-separated segments.", "唔係 JWT — 應該有三段用點（.）分隔。"),
                    "b64" => P("A segment is not valid base64url.", "有一段唔係有效嘅 base64url。"),
                    "hdrjson" => P("The header is not valid JSON.", "標頭唔係有效嘅 JSON。"),
                    "payjson" => P("The payload is not valid JSON.", "內容唔係有效嘅 JSON。"),
                    "empty" => P("Paste a token above to begin.", "喺上面貼一個權杖開始。"),
                    _ => P("This does not look like a valid JWT.", "呢個唔似係有效嘅 JWT。"),
                };
                HideResults();
                return;
            }

            StatusText.Text = P("Decoded successfully.", "已成功解碼。");

            HeaderJsonBlock.Text = d.HeaderJson;
            PayloadJsonBlock.Text = d.PayloadJson;
            SignatureBlock.Text = string.IsNullOrEmpty(d.Signature)
                ? P("(no signature)", "（冇簽名）")
                : d.Signature;
            HeaderJsonCard.Visibility = Visibility.Visible;
            PayloadJsonCard.Visibility = Visibility.Visible;
            SignatureCard.Visibility = Visibility.Visible;

            RenderClaims(d);
            RefreshVerify(d);
        }
        catch
        {
            StatusText.Text = P("Could not read this token.", "讀唔到呢個權杖。");
            HideResults();
        }
    }

    private void HideResults()
    {
        ClaimsCard.Visibility = Visibility.Collapsed;
        HeaderJsonCard.Visibility = Visibility.Collapsed;
        PayloadJsonCard.Visibility = Visibility.Collapsed;
        SignatureCard.Visibility = Visibility.Collapsed;
        VerifyBadge.Visibility = Visibility.Collapsed;
        VerifyStatus.Text = "";
    }

    private void RenderClaims(JwtInspectService.DecodedJwt d)
    {
        var sb = new StringBuilder();
        AddClaim(sb, "iss", P("Issuer", "簽發者"), d.Iss);
        AddClaim(sb, "sub", P("Subject", "主體"), d.Sub);
        AddClaim(sb, "aud", P("Audience", "受眾"), d.Aud);
        AddClaim(sb, "jti", P("JWT ID", "JWT ID"), d.Jti);
        AddTime(sb, "iat", P("Issued at", "簽發時間"), d.Iat);
        AddTime(sb, "nbf", P("Not before", "生效時間"), d.Nbf);
        AddTime(sb, "exp", P("Expires", "到期時間"), d.Exp);

        if (sb.Length == 0)
            sb.Append(P("No standard claims present.", "冇標準聲明。"));

        ClaimsBlock.Text = sb.ToString().TrimEnd();

        var state = JwtInspectService.EvaluateTime(d);
        switch (state)
        {
            case JwtInspectService.TimeState.Valid:
                SetBadge(ValidityBadge, ValidityText, GreenColor, P("VALID", "有效"));
                break;
            case JwtInspectService.TimeState.Expired:
                SetBadge(ValidityBadge, ValidityText, RedColor, P("EXPIRED", "已過期"));
                break;
            case JwtInspectService.TimeState.NotYetValid:
                SetBadge(ValidityBadge, ValidityText, AmberColor, P("NOT YET VALID", "尚未生效"));
                break;
            default:
                SetBadge(ValidityBadge, ValidityText, GrayColor, P("NO EXPIRY", "冇時效"));
                break;
        }

        ClaimsCard.Visibility = Visibility.Visible;
    }

    private void AddClaim(StringBuilder sb, string key, string label, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        sb.Append(label).Append(" (").Append(key).Append("):  ").Append(value).Append('\n');
    }

    private void AddTime(StringBuilder sb, string key, string label, long? unixSeconds)
    {
        if (unixSeconds is not long secs) return;
        try
        {
            var dto = DateTimeOffset.FromUnixTimeSeconds(secs);
            var local = dto.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");
            var utc = dto.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
            sb.Append(label).Append(" (").Append(key).Append("):  ")
              .Append(local).Append("  /  ").Append(utc).Append('\n');
        }
        catch
        {
            sb.Append(label).Append(" (").Append(key).Append("):  ").Append(secs).Append('\n');
        }
    }

    private void RefreshVerify(JwtInspectService.DecodedJwt d)
    {
        try
        {
            if (!d.Ok || string.IsNullOrEmpty(SecretBox.Text))
            {
                VerifyBadge.Visibility = Visibility.Collapsed;
                VerifyStatus.Text = d.Ok
                    ? P("Enter a secret to verify.", "輸入密鑰嚟驗證。")
                    : "";
                return;
            }

            var alg = AlgBox.SelectedIndex switch
            {
                1 => JwtInspectService.HmacAlg.HS384,
                2 => JwtInspectService.HmacAlg.HS512,
                _ => JwtInspectService.HmacAlg.HS256,
            };

            var result = JwtInspectService.VerifyHmac(d, SecretBox.Text, alg);
            switch (result)
            {
                case JwtInspectService.VerifyResult.Valid:
                    SetBadge(VerifyBadge, VerifyBadgeText, GreenColor, P("SIGNATURE VALID", "簽名有效"));
                    VerifyStatus.Text = P("The HMAC signature matches.", "HMAC 簽名相符。");
                    break;
                case JwtInspectService.VerifyResult.Invalid:
                    SetBadge(VerifyBadge, VerifyBadgeText, RedColor, P("SIGNATURE INVALID", "簽名無效"));
                    VerifyStatus.Text = P("The signature does not match this secret/algorithm.", "簽名同呢個密鑰／演算法唔相符。");
                    break;
                default:
                    VerifyBadge.Visibility = Visibility.Collapsed;
                    VerifyStatus.Text = P("Could not verify with the given input.", "用呢啲輸入驗證唔到。");
                    break;
            }
        }
        catch
        {
            VerifyBadge.Visibility = Visibility.Collapsed;
            VerifyStatus.Text = P("Could not verify with the given input.", "用呢啲輸入驗證唔到。");
        }
    }

    private static void SetBadge(Border badge, TextBlock text, Color color, string label)
    {
        badge.Background = new SolidColorBrush(color);
        text.Text = label;
        badge.Visibility = Visibility.Visible;
    }
}
