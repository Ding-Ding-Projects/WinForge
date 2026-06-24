using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// 認證方式 · How an account authenticates to its mail servers.
/// </summary>
public enum MailAuthKind
{
    /// <summary>密碼／App 專用密碼 · Plain username + password (or app password).</summary>
    Password,
    /// <summary>OAuth2（Gmail／Outlook）· OAuth2 bearer token (Gmail / Outlook).</summary>
    OAuth2,
}

/// <summary>
/// 一個電郵帳戶嘅設定 · One mail account's settings.
/// 機密（密碼／OAuth refresh token）以 DPAPI 加密後先寫入磁碟。
/// Secrets (password / OAuth refresh token) are DPAPI-encrypted before being written to disk.
/// </summary>
public sealed class MailAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";

    // IMAP (incoming)
    public string ImapHost { get; set; } = "";
    public int ImapPort { get; set; } = 993;
    public bool ImapSsl { get; set; } = true;   // true = SSL/TLS on connect; false = STARTTLS
    public string ImapUser { get; set; } = "";

    // SMTP (outgoing)
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public bool SmtpSsl { get; set; } = false;  // false = STARTTLS (587); true = implicit SSL (465)
    public string SmtpUser { get; set; } = "";

    public MailAuthKind Auth { get; set; } = MailAuthKind.Password;

    /// <summary>OAuth provider key ("google" / "microsoft"), empty for password auth.</summary>
    public string OAuthProvider { get; set; } = "";

    // ----- secrets, persisted DPAPI-encrypted (never plaintext) -----
    /// <summary>DPAPI-encrypted password / app password (base64). Empty for OAuth.</summary>
    public string EncPassword { get; set; } = "";
    /// <summary>DPAPI-encrypted OAuth refresh token (base64). Empty for password auth.</summary>
    public string EncRefreshToken { get; set; } = "";

    // ----- runtime-only (never persisted) -----
    [System.Text.Json.Serialization.JsonIgnore] public string? AccessToken { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public DateTimeOffset AccessTokenExpiry { get; set; }

    public string Label => string.IsNullOrWhiteSpace(DisplayName) ? Email : $"{DisplayName} <{Email}>";
}

/// <summary>
/// 帳戶持久化 + DPAPI 機密加密 · Account persistence + DPAPI secret encryption.
/// 帳戶清單以 JSON 存喺 <see cref="SettingsStore"/>；機密用 <see cref="ProtectedData"/>
/// （CurrentUser scope）加密，所以匯出設定喺另一部機係解唔到密。
/// The account list is stored as JSON in <see cref="SettingsStore"/>; secrets are encrypted with
/// <see cref="ProtectedData"/> (CurrentUser scope), so exported settings cannot be decrypted elsewhere.
/// </summary>
public static class MailAccountStore
{
    private const string Key = "mail.accounts";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WinForge.Mail.v1");
    private static readonly object Gate = new();

    /// <summary>DPAPI 加密（CurrentUser）· DPAPI-encrypt a secret, returns base64; "" stays "".</summary>
    public static string Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        try
        {
            var enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc);
        }
        catch { return ""; }
    }

    /// <summary>DPAPI 解密 · DPAPI-decrypt; returns "" on any failure (e.g. settings moved machines).</summary>
    public static string Unprotect(string? enc)
    {
        if (string.IsNullOrEmpty(enc)) return "";
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(enc), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return ""; }
    }

    public static List<MailAccount> Load()
    {
        lock (Gate)
        {
            var json = SettingsStore.Get(Key, "");
            if (string.IsNullOrWhiteSpace(json)) return new();
            try { return JsonSerializer.Deserialize<List<MailAccount>>(json) ?? new(); }
            catch { return new(); }
        }
    }

    public static void Save(IEnumerable<MailAccount> accounts)
    {
        lock (Gate)
        {
            var json = JsonSerializer.Serialize(accounts.ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            SettingsStore.Set(Key, json);
        }
    }

    public static void Upsert(MailAccount account)
    {
        lock (Gate)
        {
            var list = Load();
            var idx = list.FindIndex(a => a.Id == account.Id);
            if (idx >= 0) list[idx] = account; else list.Add(account);
            Save(list);
        }
    }

    public static void Remove(string id)
    {
        lock (Gate)
        {
            var list = Load();
            list.RemoveAll(a => a.Id == id);
            Save(list);
        }
    }
}
