using System;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// 一個常見供應商嘅伺服器預設 · A known provider's server preset, matched by the email domain.
/// </summary>
public sealed record MailProviderPreset(
    string Key, string En, string Zh,
    string ImapHost, int ImapPort, bool ImapSsl,
    string SmtpHost, int SmtpPort, bool SmtpSsl,
    bool OAuth, string OAuthProvider, string[] Domains);

/// <summary>
/// 供應商自動偵測 · Provider auto-detect: maps an email domain to IMAP/SMTP defaults so the
/// account wizard can pre-fill the host/port/TLS fields (Gmail / Outlook / iCloud / Yahoo / …).
/// </summary>
public static class MailProviders
{
    public static readonly MailProviderPreset[] All =
    {
        new("gmail", "Gmail (Google)", "Gmail（Google）",
            "imap.gmail.com", 993, true, "smtp.gmail.com", 587, false,
            true, "google", new[] { "gmail.com", "googlemail.com" }),
        new("outlook", "Outlook / Microsoft 365", "Outlook／Microsoft 365",
            "outlook.office365.com", 993, true, "smtp.office365.com", 587, false,
            true, "microsoft", new[] { "outlook.com", "hotmail.com", "live.com", "msn.com", "office365.com" }),
        new("icloud", "iCloud Mail", "iCloud 郵件",
            "imap.mail.me.com", 993, true, "smtp.mail.me.com", 587, false,
            false, "", new[] { "icloud.com", "me.com", "mac.com" }),
        new("yahoo", "Yahoo Mail", "Yahoo 郵件",
            "imap.mail.yahoo.com", 993, true, "smtp.mail.yahoo.com", 465, true,
            false, "", new[] { "yahoo.com", "ymail.com", "rocketmail.com" }),
        new("fastmail", "Fastmail", "Fastmail",
            "imap.fastmail.com", 993, true, "smtp.fastmail.com", 465, true,
            false, "", new[] { "fastmail.com", "fastmail.fm" }),
        new("gmx", "GMX", "GMX",
            "imap.gmx.com", 993, true, "smtp.gmx.com", 587, false,
            false, "", new[] { "gmx.com", "gmx.net" }),
        new("zoho", "Zoho Mail", "Zoho 郵件",
            "imap.zoho.com", 993, true, "smtp.zoho.com", 465, true,
            false, "", new[] { "zoho.com", "zohomail.com" }),
        new("qq", "QQ Mail", "QQ 郵箱",
            "imap.qq.com", 993, true, "smtp.qq.com", 465, true,
            false, "", new[] { "qq.com", "foxmail.com" }),
        new("163", "NetEase 163", "網易 163",
            "imap.163.com", 993, true, "smtp.163.com", 465, true,
            false, "", new[] { "163.com" }),
    };

    /// <summary>由電郵地址猜供應商 · Guess a preset from an email address; null if unknown.</summary>
    public static MailProviderPreset? Detect(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var at = email.LastIndexOf('@');
        if (at < 0 || at == email.Length - 1) return null;
        var domain = email[(at + 1)..].Trim().ToLowerInvariant();
        return All.FirstOrDefault(p => p.Domains.Contains(domain));
    }
}
