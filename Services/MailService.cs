using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace WinForge.Services;

/// <summary>一個資料夾節點 · One folder node in the tree.</summary>
public sealed class MailFolderNode
{
    public string FullName { get; init; } = "";
    public string Name { get; init; } = "";
    public int Unread { get; init; }
    public int Total { get; init; }
    public string Glyph { get; init; } = ""; // generic folder
}

/// <summary>訊息摘要（清單用）· A message summary for the list pane.</summary>
public sealed class MailMessageSummary
{
    public uint Uid { get; init; }
    public string From { get; init; } = "";
    public string Subject { get; init; } = "";
    public DateTimeOffset Date { get; init; }
    public bool Seen { get; init; }
    public bool HasAttachments { get; init; }
    public string Preview { get; init; } = "";
    public string DateText => Date == default ? "" : Date.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
}

/// <summary>一個附件 · One attachment, with bytes ready to save.</summary>
public sealed record MailAttachment(string FileName, string ContentType, long Size, MimePart Part);

/// <summary>已開啟訊息嘅完整內容 · The fully fetched body of an opened message.</summary>
public sealed class MailMessageBody
{
    public uint Uid { get; init; }
    public string From { get; init; } = "";
    public string To { get; init; } = "";
    public string Cc { get; init; } = "";
    public string Subject { get; init; } = "";
    public DateTimeOffset Date { get; init; }
    public string? HtmlBody { get; init; }
    public string TextBody { get; init; } = "";
    public string MessageId { get; init; } = "";
    public List<MailAttachment> Attachments { get; init; } = new();
    public MimeMessage? Raw { get; init; }
}

/// <summary>
/// 原生電郵核心 · The native mail core: MailKit IMAP/SMTP wrappers, all async + cancellable so the UI
/// thread never blocks. 每次操作開新連線（簡單、無狀態）；長時間操作可取消。
/// Opens a fresh connection per operation (simple, stateless); long calls are cancellable.
/// 機密由 <see cref="MailAccountStore"/> DPAPI 加密；呢度只係喺記憶體解密用嚟連線，唔會 log。
/// </summary>
public static class MailService
{
    // Well-known special-folder glyphs for the tree.
    private static string GlyphFor(IMailFolder f)
    {
        if ((f.Attributes & FolderAttributes.Inbox) != 0) return "";   // inbox
        if ((f.Attributes & FolderAttributes.Sent) != 0) return "";    // sent
        if ((f.Attributes & FolderAttributes.Drafts) != 0) return "";  // drafts
        if ((f.Attributes & FolderAttributes.Trash) != 0) return "";   // trash
        if ((f.Attributes & FolderAttributes.Junk) != 0) return "";    // junk
        if ((f.Attributes & FolderAttributes.Archive) != 0) return ""; // archive
        return "";
    }

    // ----- connection helpers -----

    private static async Task<SaslMechanism?> OAuthMechAsync(MailAccount acc, CancellationToken ct)
    {
        if (acc.Auth != MailAuthKind.OAuth2) return null;
        // Refresh the access token if missing or near expiry.
        if (string.IsNullOrEmpty(acc.AccessToken) || acc.AccessTokenExpiry <= DateTimeOffset.UtcNow)
        {
            var refresh = MailAccountStore.Unprotect(acc.EncRefreshToken);
            if (string.IsNullOrEmpty(refresh)) throw new AuthenticationException("No OAuth refresh token stored.");
            var r = await MailOAuthService.RefreshAsync(acc.OAuthProvider, refresh);
            if (!r.Success) throw new AuthenticationException("OAuth refresh failed: " + r.Error);
            acc.AccessToken = r.AccessToken;
            acc.AccessTokenExpiry = r.Expiry;
            if (!string.IsNullOrEmpty(r.RefreshToken) && r.RefreshToken != refresh)
            {
                acc.EncRefreshToken = MailAccountStore.Protect(r.RefreshToken);
                MailAccountStore.Upsert(acc);
            }
        }
        return new SaslMechanismOAuth2(acc.ImapUser, acc.AccessToken!);
    }

    private static async Task<ImapClient> ConnectImapAsync(MailAccount acc, CancellationToken ct)
    {
        var client = new ImapClient { Timeout = 60_000 };
        var opt = acc.ImapSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(acc.ImapHost, acc.ImapPort, opt, ct);
        var mech = await OAuthMechAsync(acc, ct);
        if (mech is not null) await client.AuthenticateAsync(mech, ct);
        else await client.AuthenticateAsync(acc.ImapUser, MailAccountStore.Unprotect(acc.EncPassword), ct);
        return client;
    }

    private static async Task<SmtpClient> ConnectSmtpAsync(MailAccount acc, CancellationToken ct)
    {
        var client = new SmtpClient { Timeout = 60_000 };
        var opt = acc.SmtpSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(acc.SmtpHost, acc.SmtpPort, opt, ct);
        if (acc.Auth == MailAuthKind.OAuth2)
        {
            await OAuthMechAsync(acc, ct); // ensures a fresh access token
            await client.AuthenticateAsync(new SaslMechanismOAuth2(acc.SmtpUser, acc.AccessToken!), ct);
        }
        else await client.AuthenticateAsync(acc.SmtpUser, MailAccountStore.Unprotect(acc.EncPassword), ct);
        return client;
    }

    /// <summary>測試帳戶連線（IMAP + SMTP）· Test that both IMAP and SMTP connect and authenticate.</summary>
    public static async Task<(bool ok, string message)> TestAsync(MailAccount acc, CancellationToken ct = default)
    {
        try
        {
            using var imap = await ConnectImapAsync(acc, ct);
            await imap.DisconnectAsync(true, ct);
        }
        catch (Exception ex) { return (false, "IMAP: " + ex.Message); }
        try
        {
            using var smtp = await ConnectSmtpAsync(acc, ct);
            await smtp.DisconnectAsync(true, ct);
        }
        catch (Exception ex) { return (false, "SMTP: " + ex.Message); }
        return (true, "OK");
    }

    // ----- folders -----

    public static async Task<List<MailFolderNode>> GetFoldersAsync(MailAccount acc, CancellationToken ct = default)
    {
        var list = new List<MailFolderNode>();
        using var imap = await ConnectImapAsync(acc, ct);
        var personal = imap.GetFolder(imap.PersonalNamespaces[0]);
        var folders = await personal.GetSubfoldersAsync(false, ct);
        // Always surface the Inbox first.
        var inbox = imap.Inbox;
        await inbox.StatusAsync(StatusItems.Unread | StatusItems.Count, ct);
        list.Add(new MailFolderNode
        {
            FullName = inbox.FullName, Name = inbox.Name,
            Unread = inbox.Unread, Total = inbox.Count, Glyph = GlyphFor(inbox),
        });
        foreach (var f in folders.OrderBy(x => x.Name))
        {
            if (f.FullName == inbox.FullName) continue;
            if ((f.Attributes & FolderAttributes.NonExistent) != 0) continue;
            int unread = 0, total = 0;
            try { await f.StatusAsync(StatusItems.Unread | StatusItems.Count, ct); unread = f.Unread; total = f.Count; }
            catch { }
            list.Add(new MailFolderNode
            {
                FullName = f.FullName, Name = f.Name, Unread = unread, Total = total, Glyph = GlyphFor(f),
            });
        }
        await imap.DisconnectAsync(true, ct);
        return list;
    }

    // ----- message list -----

    /// <summary>攞最近嘅訊息（最新喺前，分頁）· Fetch the most-recent messages, newest first, paged.</summary>
    public static async Task<List<MailMessageSummary>> ListAsync(MailAccount acc, string folderFullName,
        int skip, int take, string? searchText = null, CancellationToken ct = default)
    {
        var result = new List<MailMessageSummary>();
        using var imap = await ConnectImapAsync(acc, ct);
        var folder = await GetFolderAsync(imap, folderFullName, ct);
        await folder.OpenAsync(FolderAccess.ReadOnly, ct);

        IList<UniqueId> uids;
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var query = SearchQuery.SubjectContains(searchText)
                .Or(SearchQuery.FromContains(searchText))
                .Or(SearchQuery.BodyContains(searchText));
            uids = await folder.SearchAsync(query, ct);
        }
        else
        {
            uids = (await folder.SearchAsync(SearchQuery.All, ct));
        }

        // newest first
        var ordered = uids.OrderByDescending(u => u.Id).Skip(skip).Take(take).ToList();
        if (ordered.Count == 0) { await imap.DisconnectAsync(true, ct); return result; }

        var summaries = await folder.FetchAsync(ordered,
            MessageSummaryItems.Envelope | MessageSummaryItems.Flags | MessageSummaryItems.BodyStructure, ct);

        foreach (var s in summaries.OrderByDescending(x => x.UniqueId.Id))
        {
            result.Add(new MailMessageSummary
            {
                Uid = s.UniqueId.Id,
                From = s.Envelope?.From?.ToString() ?? "",
                Subject = s.Envelope?.Subject ?? "(no subject)",
                Date = s.Envelope?.Date ?? s.InternalDate ?? default,
                Seen = s.Flags?.HasFlag(MessageFlags.Seen) ?? false,
                HasAttachments = s.Attachments?.Any() ?? false,
            });
        }
        await imap.DisconnectAsync(true, ct);
        return result;
    }

    // ----- open a message -----

    public static async Task<MailMessageBody?> OpenAsync(MailAccount acc, string folderFullName, uint uid,
        bool markSeen, CancellationToken ct = default)
    {
        using var imap = await ConnectImapAsync(acc, ct);
        var folder = await GetFolderAsync(imap, folderFullName, ct);
        await folder.OpenAsync(markSeen ? FolderAccess.ReadWrite : FolderAccess.ReadOnly, ct);
        var u = new UniqueId(uid);
        var msg = await folder.GetMessageAsync(u, ct);
        if (msg is null) { await imap.DisconnectAsync(true, ct); return null; }

        if (markSeen)
            try { await folder.AddFlagsAsync(u, MessageFlags.Seen, true, ct); } catch { }

        var atts = new List<MailAttachment>();
        foreach (var att in msg.Attachments)
        {
            if (att is MimePart part)
            {
                long size = 0;
                try { size = part.Content?.Stream?.Length ?? 0; } catch { }
                atts.Add(new MailAttachment(
                    part.FileName ?? "attachment", part.ContentType?.MimeType ?? "application/octet-stream", size, part));
            }
        }

        var body = new MailMessageBody
        {
            Uid = uid,
            From = msg.From?.ToString() ?? "",
            To = msg.To?.ToString() ?? "",
            Cc = msg.Cc?.ToString() ?? "",
            Subject = msg.Subject ?? "(no subject)",
            Date = msg.Date,
            HtmlBody = msg.HtmlBody,
            TextBody = msg.TextBody ?? "",
            MessageId = msg.MessageId ?? "",
            Attachments = atts,
            Raw = msg,
        };
        await imap.DisconnectAsync(true, ct);
        return body;
    }

    /// <summary>儲存附件去磁碟 · Save one attachment to disk.</summary>
    public static async Task SaveAttachmentAsync(MailAttachment att, string path, CancellationToken ct = default)
    {
        await using var fs = File.Create(path);
        var partContent = att.Part?.Content;
        if (partContent == null) return;
        await partContent.DecodeToAsync(fs, ct);
    }

    // ----- flags / delete -----

    public static async Task SetSeenAsync(MailAccount acc, string folderFullName, uint uid, bool seen,
        CancellationToken ct = default)
    {
        using var imap = await ConnectImapAsync(acc, ct);
        var folder = await GetFolderAsync(imap, folderFullName, ct);
        await folder.OpenAsync(FolderAccess.ReadWrite, ct);
        if (seen) await folder.AddFlagsAsync(new UniqueId(uid), MessageFlags.Seen, true, ct);
        else await folder.RemoveFlagsAsync(new UniqueId(uid), MessageFlags.Seen, true, ct);
        await imap.DisconnectAsync(true, ct);
    }

    /// <summary>刪除（移去垃圾桶，若無就硬刪）· Delete: move to Trash if there is one, else expunge.</summary>
    public static async Task DeleteAsync(MailAccount acc, string folderFullName, uint uid,
        CancellationToken ct = default)
    {
        using var imap = await ConnectImapAsync(acc, ct);
        var folder = await GetFolderAsync(imap, folderFullName, ct);
        await folder.OpenAsync(FolderAccess.ReadWrite, ct);
        var u = new UniqueId(uid);

        IMailFolder? trash = null;
        try { trash = imap.GetFolder(SpecialFolder.Trash); } catch { }
        if (trash is not null && folder.FullName != trash.FullName)
        {
            try { await folder.MoveToAsync(u, trash, ct); }
            catch
            {
                await folder.AddFlagsAsync(u, MessageFlags.Deleted, true, ct);
                try { await folder.ExpungeAsync(ct); } catch { }
            }
        }
        else
        {
            await folder.AddFlagsAsync(u, MessageFlags.Deleted, true, ct);
            try { await folder.ExpungeAsync(ct); } catch { }
        }
        await imap.DisconnectAsync(true, ct);
    }

    // ----- send -----

    /// <summary>寄出一封訊息（已砌好嘅 MimeMessage）· Send a fully-built MimeMessage via SMTP.</summary>
    public static async Task SendAsync(MailAccount acc, MimeMessage message, CancellationToken ct = default)
    {
        using var smtp = await ConnectSmtpAsync(acc, ct);
        await smtp.SendAsync(message, ct);
        await smtp.DisconnectAsync(true, ct);
        // Best-effort: append a copy to the Sent folder.
        try
        {
            using var imap = await ConnectImapAsync(acc, ct);
            var sent = imap.GetFolder(SpecialFolder.Sent);
            if (sent is not null)
            {
                await sent.OpenAsync(FolderAccess.ReadWrite, ct);
                await sent.AppendAsync(message, MessageFlags.Seen, ct);
            }
            await imap.DisconnectAsync(true, ct);
        }
        catch { /* Sent copy is best-effort */ }
    }

    private static async Task<IMailFolder> GetFolderAsync(ImapClient imap, string fullName, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(fullName) || string.Equals(fullName, "INBOX", StringComparison.OrdinalIgnoreCase))
            return imap.Inbox;
        try { return await imap.GetFolderAsync(fullName, ct); }
        catch { return imap.Inbox; }
    }
}
