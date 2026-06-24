using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 通訊深層連結啟動器 · Communications deep-link launcher.
/// 起草 Outlook 草稿、mailto:、Discord / Teams / Telegram / Slack / Phone Link 深層連結。
/// Builds &amp; launches drafts and protocol deep links — never auto-sends. No redirect to Settings UI
/// (except the explicit "pick default handler" entry, which is the feature there).
/// </summary>
public static class CommunicationsService
{
    /// <summary>RFC 3986 component encode · 編碼一個 URI 元件（query value 用）。</summary>
    public static string Enc(string? s) => Uri.EscapeDataString(s ?? string.Empty);

    /// <summary>
    /// 行時解析 classic Outlook (OUTLOOK.EXE) 路徑 · Resolve classic OUTLOOK.EXE at runtime.
    /// Office16 路徑會因為安裝方式／位元而唔同；揾唔到就 null（多數係 new Outlook，唔支援 /c）。
    /// Returns null when only the new Outlook is installed (it has no /c switch).
    /// </summary>
    public static string? ResolveOutlookExe()
    {
        // 1) App Paths registry (most reliable).
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var k = root.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\OUTLOOK.EXE");
                var path = k?.GetValue(null) as string;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    path = Environment.ExpandEnvironmentVariables(path.Trim('"'));
                    if (File.Exists(path)) return path;
                }
            }
            catch { /* ignore */ }
        }

        // 2) Common Click-to-Run / MSI install locations.
        var pf = new[]
        {
            Environment.GetEnvironmentVariable("ProgramFiles"),
            Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
            Environment.GetEnvironmentVariable("ProgramW6432"),
        }.Where(p => !string.IsNullOrEmpty(p)).Distinct();

        var office = new[] { "Office16", "Office15", "Office14" };
        foreach (var b in pf)
        foreach (var o in office)
        {
            var candidate = Path.Combine(b!, "Microsoft Office", "root", o, "OUTLOOK.EXE");
            if (File.Exists(candidate)) return candidate;
            var legacy = Path.Combine(b!, "Microsoft Office", o, "OUTLOOK.EXE");
            if (File.Exists(legacy)) return legacy;
        }

        return null;
    }

    /// <summary>classic Outlook 係咪安裝咗 · True when classic OUTLOOK.EXE is present.</summary>
    public static bool HasClassicOutlook() => ResolveOutlookExe() is not null;

    /// <summary>
    /// 啟動一個協定 URI（mailto:, discord://, tg://, slack://, tel:, https://…）。
    /// Launch a protocol URI through its registered handler (ShellExecute). Never sends.
    /// </summary>
    public static TweakResult LaunchUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return TweakResult.Fail("Nothing to launch.", "冇嘢可以啟動。");
        try
        {
            Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
            return TweakResult.Ok($"Launched: {uri}", $"已啟動：{uri}", uri);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(
                $"Could not launch — no handler for this scheme? ({ex.Message})",
                $"啟動唔到 — 可能冇 app 處理呢個 scheme？（{ex.Message}）", uri);
        }
    }

    /// <summary>
    /// 啟動一個有引數嘅可執行檔（例如 OUTLOOK.EXE）· Launch an exe with arguments (e.g. OUTLOOK.EXE).
    /// </summary>
    public static TweakResult LaunchExe(string exe, string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = exe, Arguments = args, UseShellExecute = true });
            return TweakResult.Ok($"Launched: {Path.GetFileName(exe)} {args}",
                $"已啟動：{Path.GetFileName(exe)} {args}", $"{exe} {args}");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not launch Outlook: {ex.Message}",
                $"啟動 Outlook 失敗：{ex.Message}", $"{exe} {args}");
        }
    }

    // ---------- Mail: mailto: query (RFC 6068) ----------

    /// <summary>
    /// 砌一條 RFC 6068 mailto: URI · Build a mailto: URI (subject/cc/bcc/body URL-encoded).
    /// </summary>
    public static string BuildMailto(string to, string subject, string cc, string bcc, string body)
    {
        var sb = new StringBuilder("mailto:");
        sb.Append(Uri.EscapeDataString(to ?? string.Empty).Replace("%40", "@").Replace("%2C", ","));
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(subject)) q.Add("subject=" + Enc(subject));
        if (!string.IsNullOrWhiteSpace(cc)) q.Add("cc=" + Enc(cc));
        if (!string.IsNullOrWhiteSpace(bcc)) q.Add("bcc=" + Enc(bcc));
        if (!string.IsNullOrWhiteSpace(body)) q.Add("body=" + Enc(body));
        if (q.Count > 0) sb.Append('?').Append(string.Join("&", q));
        return sb.ToString();
    }

    /// <summary>用預設信件 app 開 mailto: 草稿 · Open mailto: draft in default mail handler.</summary>
    public static TweakResult OpenMailto(string to, string subject, string cc, string bcc, string body)
        => LaunchUri(BuildMailto(to, subject, cc, bcc, body));

    /// <summary>
    /// classic Outlook 新郵件（/c ipm.note /m mailtoQuery）· New classic Outlook draft.
    /// /c ipm.note 強制開新郵件；/m 帶住 mailto query。只開草稿，永遠唔寄。
    /// </summary>
    public static TweakResult OutlookCompose(string to, string subject, string cc, string bcc, string body)
    {
        var exe = ResolveOutlookExe();
        if (exe is null)
            return TweakResult.Fail(
                "Classic Outlook (OUTLOOK.EXE) not found. Use the mailto: option for the new Outlook.",
                "揾唔到傳統 Outlook（OUTLOOK.EXE）。新版 Outlook 請改用 mailto: 選項。");

        // /m takes a mailto query *without* the leading "mailto:".
        var query = new StringBuilder();
        query.Append(to ?? string.Empty);
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(subject)) q.Add("subject=" + Enc(subject));
        if (!string.IsNullOrWhiteSpace(cc)) q.Add("cc=" + Enc(cc));
        if (!string.IsNullOrWhiteSpace(bcc)) q.Add("bcc=" + Enc(bcc));
        if (!string.IsNullOrWhiteSpace(body)) q.Add("body=" + Enc(body));
        if (q.Count > 0) query.Append('?').Append(string.Join("&", q));

        var args = $"/c ipm.note /m \"{query}\"";
        return LaunchExe(exe, args);
    }

    /// <summary>
    /// classic Outlook 新郵件 + 附件（/a file，可加 /m 預先收件人）。
    /// New classic Outlook message with a file attached (/a), optionally pre-addressed (/m).
    /// /a 一次只食一個檔案路徑。Classic Outlook only.
    /// </summary>
    public static TweakResult OutlookAttach(string filePath, string to, string subject, string body)
    {
        var exe = ResolveOutlookExe();
        if (exe is null)
            return TweakResult.Fail(
                "Classic Outlook (OUTLOOK.EXE) not found — /a attach is classic-Outlook only.",
                "揾唔到傳統 Outlook（OUTLOOK.EXE）— /a 附件只支援傳統 Outlook。");
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return TweakResult.Fail("Pick a file that exists.", "請揀一個存在嘅檔案。");

        var sb = new StringBuilder();
        sb.Append($"/a \"{filePath}\"");
        if (!string.IsNullOrWhiteSpace(to))
        {
            var query = new StringBuilder(to);
            var q = new List<string>();
            if (!string.IsNullOrWhiteSpace(subject)) q.Add("subject=" + Enc(subject));
            if (!string.IsNullOrWhiteSpace(body)) q.Add("body=" + Enc(body));
            if (q.Count > 0) query.Append('?').Append(string.Join("&", q));
            sb.Append($" /m \"{query}\"");
        }
        return LaunchExe(exe, sb.ToString());
    }

    /// <summary>
    /// classic Outlook 跳去指定資料夾（/select outlook:Folder）· Jump to an Outlook folder on launch.
    /// folder 例如 Calendar / Inbox / Contacts / Tasks / Notes / Drafts。
    /// </summary>
    public static TweakResult OutlookSelectFolder(string folder)
    {
        var exe = ResolveOutlookExe();
        if (exe is null)
            return TweakResult.Fail(
                "Classic Outlook (OUTLOOK.EXE) not found — /select is classic-Outlook only.",
                "揾唔到傳統 Outlook（OUTLOOK.EXE）— /select 只支援傳統 Outlook。");
        if (string.IsNullOrWhiteSpace(folder))
            return TweakResult.Fail("Pick a folder.", "請揀一個資料夾。");
        return LaunchExe(exe, $"/select outlook:{folder}");
    }

    /// <summary>傳統 Outlook 資料夾選項 · Classic Outlook folder namespace options.</summary>
    public static readonly (string Value, string En, string Zh)[] OutlookFolders =
    {
        ("Inbox", "Inbox", "收件匣"),
        ("Calendar", "Calendar", "行事曆"),
        ("Contacts", "Contacts", "連絡人"),
        ("Tasks", "Tasks", "工作"),
        ("Notes", "Notes", "記事"),
        ("Drafts", "Drafts", "草稿"),
    };

    // ---------- Discord ----------

    /// <summary>開個 Discord 頻道／server · discord://-/channels/&lt;guild&gt;/&lt;channel&gt;.</summary>
    public static TweakResult DiscordChannel(string guildId, string channelId)
    {
        guildId = (guildId ?? string.Empty).Trim();
        channelId = (channelId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(guildId))
            return TweakResult.Fail("Enter a server (guild) ID.", "請輸入 server（guild）ID。");
        var uri = string.IsNullOrEmpty(channelId)
            ? $"discord://-/channels/{guildId}"
            : $"discord://-/channels/{guildId}/{channelId}";
        return LaunchUri(uri);
    }

    /// <summary>開返個 Discord 私訊 · discord://-/channels/@me/&lt;dmChannelId&gt;.</summary>
    public static TweakResult DiscordDm(string dmChannelId)
    {
        dmChannelId = (dmChannelId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(dmChannelId))
            return TweakResult.Fail("Enter a DM channel ID.", "請輸入私訊頻道 ID。");
        return LaunchUri($"discord://-/channels/@me/{dmChannelId}");
    }

    /// <summary>開 Discord DM 主頁 · discord://-/channels/@me.</summary>
    public static TweakResult DiscordDmHome() => LaunchUri("discord://-/channels/@me");

    // ---------- Teams ----------

    /// <summary>Teams 1:1 / 群組傾偈 · https l/chat deep link (pre-fills, no auto-send).</summary>
    public static TweakResult TeamsChat(string users, string topic, string message)
    {
        users = (users ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(users))
            return TweakResult.Fail("Enter at least one user (UPN/email).", "請輸入至少一個使用者（UPN／電郵）。");
        var q = new List<string> { "users=" + Enc(users) };
        if (!string.IsNullOrWhiteSpace(topic)) q.Add("topicName=" + Enc(topic));
        if (!string.IsNullOrWhiteSpace(message)) q.Add("message=" + Enc(message));
        return LaunchUri($"https://teams.microsoft.com/l/chat/0/0?{string.Join("&", q)}");
    }

    /// <summary>排個新 Teams 會議 · https l/meeting/new deep link (form pre-filled, no auto-send).</summary>
    public static TweakResult TeamsMeeting(string subject, string attendees, string startIso, string endIso, string content)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(subject)) q.Add("subject=" + Enc(subject));
        if (!string.IsNullOrWhiteSpace(attendees)) q.Add("attendees=" + Enc(attendees.Trim()));
        if (!string.IsNullOrWhiteSpace(startIso)) q.Add("startTime=" + Enc(startIso.Trim()));
        if (!string.IsNullOrWhiteSpace(endIso)) q.Add("endTime=" + Enc(endIso.Trim()));
        if (!string.IsNullOrWhiteSpace(content)) q.Add("content=" + Enc(content));
        var qs = q.Count > 0 ? "?" + string.Join("&", q) : string.Empty;
        return LaunchUri($"https://teams.microsoft.com/l/meeting/new{qs}");
    }

    /// <summary>撳一下打 Teams 電話 · https l/call deep link.</summary>
    public static TweakResult TeamsCall(string users)
    {
        users = (users ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(users))
            return TweakResult.Fail("Enter a user (UPN/email) to call.", "請輸入要打俾邊個（UPN／電郵）。");
        return LaunchUri($"https://teams.microsoft.com/l/call/0/0?users=" + Enc(users));
    }

    // ---------- Telegram ----------

    /// <summary>分享 URL／文字去 Telegram · tg://msg_url?url=&amp;text= (or tg://msg?text= for text-only).</summary>
    public static TweakResult TelegramShare(string url, string text)
    {
        url = (url ?? string.Empty).Trim();
        text = text ?? string.Empty;
        if (string.IsNullOrEmpty(url) && string.IsNullOrWhiteSpace(text))
            return TweakResult.Fail("Enter a URL or some text.", "請輸入一條 URL 或者一段文字。");
        if (string.IsNullOrEmpty(url))
            return LaunchUri("tg://msg?text=" + Enc(text));
        var q = new List<string> { "url=" + Enc(url) };
        if (!string.IsNullOrWhiteSpace(text)) q.Add("text=" + Enc(text));
        return LaunchUri("tg://msg_url?" + string.Join("&", q));
    }

    /// <summary>開個 Telegram 傾偈（by username）· tg://resolve?domain=&lt;username&gt;[&amp;post=&lt;id&gt;].</summary>
    public static TweakResult TelegramResolve(string username, string post)
    {
        username = (username ?? string.Empty).Trim().TrimStart('@');
        if (string.IsNullOrEmpty(username))
            return TweakResult.Fail("Enter a Telegram username.", "請輸入 Telegram 使用者名稱。");
        var uri = "tg://resolve?domain=" + Enc(username);
        post = (post ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(post)) uri += "&post=" + Enc(post);
        return LaunchUri(uri);
    }

    // ---------- Slack ----------

    /// <summary>開 Slack 頻道 · slack://channel?team=&amp;id=.</summary>
    public static TweakResult SlackChannel(string team, string channelId)
    {
        team = (team ?? string.Empty).Trim();
        channelId = (channelId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(team) || string.IsNullOrEmpty(channelId))
            return TweakResult.Fail("Enter both a team ID and a channel ID.", "請輸入 team ID 同 channel ID。");
        return LaunchUri($"slack://channel?team={Enc(team)}&id={Enc(channelId)}");
    }

    /// <summary>開 Slack 私訊 · slack://user?team=&amp;id=.</summary>
    public static TweakResult SlackDm(string team, string userId)
    {
        team = (team ?? string.Empty).Trim();
        userId = (userId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(team) || string.IsNullOrEmpty(userId))
            return TweakResult.Fail("Enter both a team ID and a user ID.", "請輸入 team ID 同 user ID。");
        return LaunchUri($"slack://user?team={Enc(team)}&id={Enc(userId)}");
    }

    /// <summary>對焦 Slack workspace · slack://open?team=.</summary>
    public static TweakResult SlackOpen(string team)
    {
        team = (team ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(team))
            return TweakResult.Fail("Enter a team ID.", "請輸入 team ID。");
        return LaunchUri($"slack://open?team={Enc(team)}");
    }

    // ---------- Phone Link (tel: / sms:) ----------

    /// <summary>打電話 · tel:&lt;number&gt; (RFC 3966). Opens dialer; never auto-dials.</summary>
    public static TweakResult PhoneCall(string number)
    {
        number = CleanNumber(number);
        if (string.IsNullOrEmpty(number))
            return TweakResult.Fail("Enter a phone number.", "請輸入電話號碼。");
        return LaunchUri("tel:" + number);
    }

    /// <summary>傳 SMS · sms:&lt;number&gt;?body=. Opens compose; never auto-sends.</summary>
    public static TweakResult PhoneSms(string number, string body)
    {
        number = CleanNumber(number);
        if (string.IsNullOrEmpty(number))
            return TweakResult.Fail("Enter a phone number.", "請輸入電話號碼。");
        var uri = "sms:" + number;
        if (!string.IsNullOrWhiteSpace(body)) uri += "?body=" + Enc(body);
        return LaunchUri(uri);
    }

    private static string CleanNumber(string? n)
    {
        if (string.IsNullOrWhiteSpace(n)) return string.Empty;
        var sb = new StringBuilder();
        foreach (var c in n.Trim())
            if (char.IsDigit(c) || c == '+' || c == '*' || c == '#') sb.Append(c);
        return sb.ToString();
    }

    // ---------- Default handler picker ----------

    /// <summary>開「預設 App」設定頁畀使用者揀返 handler · ms-settings:defaultapps.</summary>
    public static TweakResult OpenDefaultAppsSettings() => LaunchUri("ms-settings:defaultapps");
}
