using System;
using System.Collections.Generic;
using System.Linq;
using MimeKit;

namespace WinForge.Services;

/// <summary>撰寫模式 · Compose mode.</summary>
public enum ComposeMode { New, Reply, ReplyAll, Forward }

/// <summary>一份草稿嘅資料 · The editable fields of a draft being composed.</summary>
public sealed class MailDraft
{
    public string To { get; set; } = "";
    public string Cc { get; set; } = "";
    public string Bcc { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public List<string> AttachmentPaths { get; } = new();
}

/// <summary>
/// 砌 MimeMessage · Builds MimeMessages for new / reply / reply-all / forward, with attachments.
/// 用 MimeKit 嘅 BodyBuilder；回覆會帶 In-Reply-To / References 同引文。
/// </summary>
public static class MailComposer
{
    /// <summary>由回覆／轉寄原文預填一份草稿 · Pre-fill a draft from the message being replied to / forwarded.</summary>
    public static MailDraft Prefill(ComposeMode mode, MailAccount self, MailMessageBody? src)
    {
        var d = new MailDraft();
        if (src?.Raw is null || mode == ComposeMode.New) return d;
        var raw = src.Raw;

        if (mode == ComposeMode.Reply || mode == ComposeMode.ReplyAll)
        {
            var replyTo = raw.ReplyTo.Count > 0 ? raw.ReplyTo : raw.From;
            d.To = string.Join(", ", replyTo.Mailboxes.Select(m => m.Address));
            if (mode == ComposeMode.ReplyAll)
            {
                var others = raw.To.Mailboxes.Concat(raw.Cc.Mailboxes)
                    .Where(m => !m.Address.Equals(self.Email, StringComparison.OrdinalIgnoreCase)
                             && !replyTo.Mailboxes.Any(r => r.Address.Equals(m.Address, StringComparison.OrdinalIgnoreCase)))
                    .Select(m => m.Address).Distinct();
                d.Cc = string.Join(", ", others);
            }
            d.Subject = Prefix(raw.Subject, "Re:");
            d.Body = "\n\n" + Quote(src);
        }
        else if (mode == ComposeMode.Forward)
        {
            d.Subject = Prefix(raw.Subject, "Fwd:");
            d.Body = "\n\n" + Quote(src);
        }
        return d;
    }

    private static string Prefix(string? subject, string prefix)
    {
        subject ??= "";
        return subject.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? subject : $"{prefix} {subject}";
    }

    private static string Quote(MailMessageBody src)
    {
        var header = $"On {src.Date.LocalDateTime:yyyy-MM-dd HH:mm}, {src.From} wrote:";
        var text = string.IsNullOrEmpty(src.TextBody)
            ? (src.HtmlBody is null ? "" : System.Text.RegularExpressions.Regex.Replace(src.HtmlBody, "<[^>]+>", " "))
            : src.TextBody;
        var quoted = string.Join("\n", text.Replace("\r", "").Split('\n').Select(l => "> " + l));
        return header + "\n" + quoted;
    }

    /// <summary>把一份草稿砌成可寄嘅 MimeMessage · Build a sendable MimeMessage from a draft.</summary>
    public static MimeMessage Build(MailAccount from, MailDraft draft, MailMessageBody? inReplyTo = null)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(from.DisplayName ?? from.Email, from.Email));
        AddAddresses(msg.To, draft.To);
        AddAddresses(msg.Cc, draft.Cc);
        AddAddresses(msg.Bcc, draft.Bcc);
        msg.Subject = draft.Subject ?? "";

        if (inReplyTo?.Raw is not null && !string.IsNullOrEmpty(inReplyTo.MessageId))
        {
            msg.InReplyTo = inReplyTo.MessageId;
            msg.References.Add(inReplyTo.MessageId);
        }

        var builder = new BodyBuilder { TextBody = draft.Body ?? "" };
        foreach (var path in draft.AttachmentPaths.Where(System.IO.File.Exists))
        {
            try { builder.Attachments.Add(path); } catch { }
        }
        msg.Body = builder.ToMessageBody();
        return msg;
    }

    private static void AddAddresses(InternetAddressList list, string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return;
        foreach (var part in csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var s = part.Trim();
            if (MailboxAddress.TryParse(s, out var mb)) list.Add(mb);
        }
    }
}
