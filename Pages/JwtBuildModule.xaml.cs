using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// JWT 建立 + 驗證（HMAC）· JWT builder + verifier for HS256/HS384/HS512, all local.
/// Build: edit header/payload JSON, pick alg, quick-add standard claims, sign to a
/// compact Base64Url token. Verify: paste a token + secret + alg → recompute the HMAC,
/// pretty-print header + payload, flag exp/nbf. Fully bilingual, never throws.
/// </summary>
public sealed partial class JwtBuildModule : Page
{
    private const string DefaultHeader = "{\n  \"alg\": \"HS256\",\n  \"typ\": \"JWT\"\n}";
    private const string DefaultPayload = "{\n  \"sub\": \"1234567890\",\n  \"name\": \"WinForge\"\n}";

    public JwtBuildModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(HeaderJsonBox.Text)) HeaderJsonBox.Text = DefaultHeader;
        if (string.IsNullOrWhiteSpace(PayloadJsonBox.Text)) PayloadJsonBox.Text = DefaultPayload;
        if (AlgBox.SelectedIndex < 0) AlgBox.SelectedIndex = 0;
        if (VerifyAlgBox.SelectedIndex < 0) VerifyAlgBox.SelectedIndex = 0;
        Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "JWT Builder · JWT 建立同驗證";
        HeaderBlurb.Text = P("Build and verify JSON Web Tokens signed with HMAC (HS256/384/512), all on this PC. Nothing leaves your machine.",
            "喺呢部電腦度整同驗證用 HMAC（HS256/384/512）簽名嘅 JSON Web Token，全程本機處理，冇嘢會傳出去。");

        BuildTitle.Text = P("Build & sign", "建立同簽名");
        AlgLabel.Text = P("Algorithm", "演算法");
        HeaderLabel.Text = P("Header JSON", "標頭 JSON");
        PayloadLabel.Text = P("Payload / claims JSON", "內容／宣告 JSON");
        QuickAddLabel.Text = P("Quick-add standard claims", "快速加入標準宣告");
        SecretLabel.Text = P("Secret", "密鑰");
        SignBtn.Content = P("Sign", "簽名");
        CopyTokenBtn.Content = P("Copy", "複製");
        OutputLabel.Text = P("Token (header.payload.signature)", "權杖（標頭.內容.簽名）");

        VerifyTitle.Text = P("Verify", "驗證");
        TokenInLabel.Text = P("Token to verify", "要驗證嘅權杖");
        VerifyAlgLabel.Text = P("Algorithm", "演算法");
        VerifySecretLabel.Text = P("Secret", "密鑰");
        VerifyBtn.Content = P("Verify signature", "驗證簽名");
        DecHeaderLabel.Text = P("Decoded header", "已解碼標頭");
        DecPayloadLabel.Text = P("Decoded payload", "已解碼內容");
    }

    private string SelectedAlg(ComboBox box)
    {
        return (box.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "HS256";
    }

    // Keep the header "alg" in step with the ComboBox where it's still the default-ish JSON.
    private void Alg_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (HeaderJsonBox == null) return;
        string alg = SelectedAlg(AlgBox);
        // Keep the header's "alg" in step with the picker. Leave the header untouched on any
        // parse issue so the user's editing is never clobbered.
        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(HeaderJsonBox.Text);
            if (node is System.Text.Json.Nodes.JsonObject obj)
            {
                obj["alg"] = alg;
                HeaderJsonBox.Text = obj.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch { /* header stays as-is */ }
    }

    private void QuickAdd_Click(object sender, RoutedEventArgs e)
    {
        string claim = (sender as FrameworkElement)?.Tag?.ToString() ?? string.Empty;
        var result = JwtBuildService.PatchClaim(PayloadJsonBox.Text, claim);
        if (result == null)
        {
            ShowBuild(InfoBarSeverity.Error,
                P("Payload isn't valid JSON — fix it before adding claims.",
                  "內容唔係有效嘅 JSON — 加宣告之前請先修正。"));
            return;
        }
        PayloadJsonBox.Text = result;
        ShowBuild(InfoBarSeverity.Success, P($"Added claim: {claim}", $"已加入宣告：{claim}"));
    }

    private void Sign_Click(object sender, RoutedEventArgs e)
    {
        var r = JwtBuildService.Sign(HeaderJsonBox.Text, PayloadJsonBox.Text, SelectedAlg(AlgBox), SecretBox.Text);
        if (!r.Ok)
        {
            string msg = r.BadField switch
            {
                "header" => P("Header isn't valid JSON.", "標頭唔係有效嘅 JSON。"),
                "payload" => P("Payload isn't valid JSON.", "內容唔係有效嘅 JSON。"),
                "secret" => P("Enter a secret to sign with.", "請輸入用嚟簽名嘅密鑰。"),
                _ => P("Couldn't sign the token.", "無法簽發權杖。"),
            };
            TokenBox.Text = string.Empty;
            CopyTokenBtn.IsEnabled = false;
            ShowBuild(InfoBarSeverity.Error, msg);
            return;
        }
        TokenBox.Text = r.Token;
        CopyTokenBtn.IsEnabled = true;
        ShowBuild(InfoBarSeverity.Success, P("Signed. Token is ready below.", "已簽名，權杖喺下面。"));
    }

    private void CopyToken_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(TokenBox.Text)) return;
            var dp = new DataPackage();
            dp.SetText(TokenBox.Text);
            Clipboard.SetContent(dp);
            ShowBuild(InfoBarSeverity.Success, P("Copied to clipboard.", "已複製到剪貼簿。"));
        }
        catch
        {
            ShowBuild(InfoBarSeverity.Error, P("Couldn't access the clipboard.", "無法存取剪貼簿。"));
        }
    }

    private void Verify_Click(object sender, RoutedEventArgs e)
    {
        var r = JwtBuildService.Verify(VerifyTokenBox.Text, SelectedAlg(VerifyAlgBox), VerifySecretBox.Text);
        if (!r.Ok)
        {
            DecodedPanel.Visibility = Visibility.Collapsed;
            string msg = r.Error switch
            {
                "format" => P("Not a well-formed token (need header.payload.signature).", "唔係格式正確嘅權杖（要 標頭.內容.簽名）。"),
                "header" => P("Token header isn't valid JSON.", "權杖標頭唔係有效嘅 JSON。"),
                "payload" => P("Token payload isn't valid JSON.", "權杖內容唔係有效嘅 JSON。"),
                "secret" => P("Enter the secret to verify against.", "請輸入用嚟驗證嘅密鑰。"),
                _ => P("Couldn't verify the token.", "無法驗證權杖。"),
            };
            ShowVerify(InfoBarSeverity.Error, msg);
            return;
        }

        DecHeaderBox.Text = r.HeaderPretty;
        DecPayloadBox.Text = r.PayloadPretty;
        DecodedPanel.Visibility = Visibility.Visible;

        // Compose a status line covering signature + exp/nbf.
        string sigLine = r.SignatureValid
            ? P("Signature VALID.", "簽名有效。")
            : P("Signature INVALID — wrong secret or algorithm.", "簽名無效 — 密鑰或演算法唔啱。");

        string timeNote = string.Empty;
        if (r.Expired == true) timeNote += " " + P($"Expired ({r.Exp:u}).", $"已過期（{r.Exp:u}）。");
        else if (r.Expired == false) timeNote += " " + P($"Not expired (exp {r.Exp:u}).", $"未過期（exp {r.Exp:u}）。");
        if (r.NotYetValid == true) timeNote += " " + P($"Not yet valid (nbf {r.Nbf:u}).", $"尚未生效（nbf {r.Nbf:u}）。");

        var sev = r.SignatureValid && r.Expired != true && r.NotYetValid != true
            ? InfoBarSeverity.Success
            : (r.SignatureValid ? InfoBarSeverity.Warning : InfoBarSeverity.Error);
        ShowVerify(sev, (sigLine + timeNote).Trim());
    }

    private void ShowBuild(InfoBarSeverity sev, string msg)
    {
        BuildInfo.Severity = sev;
        BuildInfo.Message = msg;
        BuildInfo.IsOpen = true;
    }

    private void ShowVerify(InfoBarSeverity sev, string msg)
    {
        VerifyInfo.Severity = sev;
        VerifyInfo.Message = msg;
        VerifyInfo.IsOpen = true;
    }
}
