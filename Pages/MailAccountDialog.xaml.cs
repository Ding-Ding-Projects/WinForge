using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 加帳戶精靈 · Add-account wizard (ContentDialog). 自動偵測常見供應商，支援 OAuth2（Gmail／Outlook）
/// 同 App 密碼，並可一鍵測試 IMAP／SMTP 連線。機密由 <see cref="MailAccountStore"/> DPAPI 加密。
/// Auto-detects common providers, supports OAuth2 (Gmail/Outlook) and app passwords, and tests the
/// IMAP/SMTP connection in one click. Secrets are DPAPI-encrypted by the store.
/// </summary>
public sealed partial class MailAccountDialog : ContentDialog
{
    private readonly MailAccount _acc = new();
    private MailProviderPreset? _preset;
    private bool _oauthDone;
    private bool _userEditedServers;

    public MailAccountDialog()
    {
        InitializeComponent();
        Render();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Title = P("Add mail account", "加電郵帳戶");
        PrimaryButtonText = P("Add", "加入");
        CloseButtonText = P("Cancel", "取消");
        DefaultButton = ContentDialogButton.Primary;

        DisplayNameBox.Header = P("Your name", "你嘅名");
        DisplayNameBox.PlaceholderText = P("e.g. Wong Tai Man", "例如：黃大文");
        EmailBox.Header = P("Email address", "電郵地址");
        EmailBox.PlaceholderText = "you@example.com";

        AuthLabel.Text = P("Sign-in method", "登入方式");
        AuthOAuthRadio.Content = P("OAuth2 (Gmail / Outlook — recommended)", "OAuth2（Gmail／Outlook，建議）");
        AuthPwdRadio.Content = P("Password / app password", "密碼／App 專用密碼");

        OAuthBtn.Content = P("Sign in with browser", "用瀏覽器登入");
        OAuthStatus.Text = "";

        PasswordBox.Header = P("Password", "密碼");
        PwdHint.Text = P(
            "For Gmail/Outlook, create an app password (normal passwords are blocked). Other providers accept your normal password if IMAP is enabled.",
            "Gmail／Outlook 請開「App 專用密碼」（普通密碼會被封鎖）。其他供應商若已啟用 IMAP，可用普通密碼。");

        AdvancedExpander.Header = P("Advanced — server settings", "進階 — 伺服器設定");
        ImapHeader.Text = P("Incoming (IMAP)", "收信（IMAP）");
        ImapHostBox.Header = P("IMAP server", "IMAP 伺服器");
        ImapHostBox.PlaceholderText = "imap.example.com";
        ImapUserBox.Header = P("IMAP username", "IMAP 使用者名稱");
        SmtpHeader.Text = P("Outgoing (SMTP)", "寄信（SMTP）");
        SmtpHostBox.Header = P("SMTP server", "SMTP 伺服器");
        SmtpHostBox.PlaceholderText = "smtp.example.com";
        SmtpUserBox.Header = P("SMTP username", "SMTP 使用者名稱");

        TestBtn.Content = P("Test connection", "測試連線");

        ImapHostBox.TextChanged += (_, _) => _userEditedServers = true;
        SmtpHostBox.TextChanged += (_, _) => _userEditedServers = true;
    }

    /// <summary>顯示精靈並回傳新帳戶（取消回 null）· Show the wizard; returns the new account or null.</summary>
    public async Task<MailAccount?> ShowWizardAsync()
    {
        var r = await ShowAsync();
        return r == ContentDialogResult.Primary ? _acc : null;
    }

    private void Email_Changed(object sender, TextChangedEventArgs e)
    {
        _preset = MailProviders.Detect(EmailBox.Text);
        if (_preset is null)
        {
            ProviderBar.IsOpen = false;
            return;
        }
        ProviderBar.Title = P("Detected provider", "偵測到供應商");
        ProviderBar.Message = $"{_preset.En} · {_preset.Zh}";
        ProviderBar.IsOpen = true;

        if (!_userEditedServers)
        {
            ImapHostBox.Text = _preset.ImapHost;
            ImapPortBox.Value = _preset.ImapPort;
            ImapSecBox.SelectedIndex = _preset.ImapSsl ? 0 : 1;
            ImapUserBox.Text = EmailBox.Text;
            SmtpHostBox.Text = _preset.SmtpHost;
            SmtpPortBox.Value = _preset.SmtpPort;
            SmtpSecBox.SelectedIndex = _preset.SmtpSsl ? 0 : 1;
            SmtpUserBox.Text = EmailBox.Text;
            _userEditedServers = false; // setting programmatically shouldn't count
        }

        // Default to OAuth for providers that support it.
        if (_preset.OAuth) AuthOAuthRadio.IsChecked = true;
    }

    private void Auth_Changed(object sender, SelectionChangedEventArgs e)
    {
        bool oauth = AuthOAuthRadio.IsChecked == true;
        OAuthPanel.Visibility = oauth ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        PwdPanel.Visibility = oauth ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
    }

    private async void OAuth_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var provider = _preset?.OAuthProvider;
        if (string.IsNullOrEmpty(provider))
        {
            // Fall back: choose by domain heuristics
            provider = (EmailBox.Text ?? "").Contains("gmail", StringComparison.OrdinalIgnoreCase) ? "google" : "microsoft";
        }
        Busy.IsActive = true;
        OAuthBtn.IsEnabled = false;
        try
        {
            var r = await MailOAuthService.SignInAsync(provider, XamlRoot);
            if (r.Success)
            {
                _oauthDone = true;
                _acc.Auth = MailAuthKind.OAuth2;
                _acc.OAuthProvider = provider;
                _acc.EncRefreshToken = MailAccountStore.Protect(r.RefreshToken);
                _acc.AccessToken = r.AccessToken;
                _acc.AccessTokenExpiry = r.Expiry;
                if (string.IsNullOrWhiteSpace(EmailBox.Text) && !string.IsNullOrEmpty(r.Email))
                    EmailBox.Text = r.Email;
                OAuthStatus.Text = P("Signed in ✓", "已登入 ✓") + (string.IsNullOrEmpty(r.Email) ? "" : "  " + r.Email);
            }
            else
            {
                OAuthStatus.Text = P("Sign-in failed: ", "登入失敗：") + r.Error;
            }
        }
        finally { Busy.IsActive = false; OAuthBtn.IsEnabled = true; }
    }

    private MailAccount? Collect()
    {
        if (string.IsNullOrWhiteSpace(EmailBox.Text)) { Warn(P("Enter an email address", "請輸入電郵地址")); return null; }
        _acc.DisplayName = DisplayNameBox.Text?.Trim() ?? "";
        _acc.Email = EmailBox.Text.Trim();
        _acc.ImapHost = ImapHostBox.Text?.Trim() ?? "";
        _acc.ImapPort = (int)ImapPortBox.Value;
        _acc.ImapSsl = ImapSecBox.SelectedIndex == 0;
        _acc.ImapUser = string.IsNullOrWhiteSpace(ImapUserBox.Text) ? _acc.Email : ImapUserBox.Text.Trim();
        _acc.SmtpHost = SmtpHostBox.Text?.Trim() ?? "";
        _acc.SmtpPort = (int)SmtpPortBox.Value;
        _acc.SmtpSsl = SmtpSecBox.SelectedIndex == 0;
        _acc.SmtpUser = string.IsNullOrWhiteSpace(SmtpUserBox.Text) ? _acc.Email : SmtpUserBox.Text.Trim();

        if (AuthOAuthRadio.IsChecked == true)
        {
            if (!_oauthDone) { Warn(P("Complete OAuth sign-in first", "請先完成 OAuth 登入")); return null; }
            _acc.Auth = MailAuthKind.OAuth2;
        }
        else
        {
            if (string.IsNullOrEmpty(PasswordBox.Password)) { Warn(P("Enter a password", "請輸入密碼")); return null; }
            _acc.Auth = MailAuthKind.Password;
            _acc.EncPassword = MailAccountStore.Protect(PasswordBox.Password);
        }
        if (string.IsNullOrEmpty(_acc.ImapHost) || string.IsNullOrEmpty(_acc.SmtpHost))
        { Warn(P("Set the IMAP and SMTP servers (Advanced)", "請設定 IMAP 同 SMTP 伺服器（進階）")); return null; }
        return _acc;
    }

    private async void Test_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var acc = Collect();
        if (acc is null) return;
        Busy.IsActive = true;
        TestBtn.IsEnabled = false;
        try
        {
            var (ok, msg) = await MailService.TestAsync(acc);
            Bar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            Bar.Title = ok ? P("Connection OK", "連線成功") : P("Connection failed", "連線失敗");
            Bar.Message = ok ? P("IMAP and SMTP both authenticated.", "IMAP 同 SMTP 都認證成功。") : msg;
            Bar.IsOpen = true;
        }
        finally { Busy.IsActive = false; TestBtn.IsEnabled = true; }
    }

    private void Primary_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (Collect() is null) args.Cancel = true;
    }

    private void Warn(string msg)
    {
        Bar.Severity = InfoBarSeverity.Warning;
        Bar.Title = P("Check the form", "請檢查表格");
        Bar.Message = msg;
        Bar.IsOpen = true;
    }
}
