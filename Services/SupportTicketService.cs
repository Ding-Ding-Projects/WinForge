using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinForge.Services;

public enum SupportTicketCategory
{
    General,
    Installation,
    Update,
    Accessibility,
    DataRecovery,
}

public enum SupportTicketSeverity
{
    Low,
    Normal,
    High,
}

public enum SupportTicketStatus
{
    New,
    Acknowledged,
    InProgress,
    Resolved,
}

public sealed record SupportTicket(
    Guid Id,
    string TicketNumber,
    SupportTicketCategory Category,
    string Description,
    SupportTicketSeverity Severity,
    SupportTicketStatus Status,
    DateTimeOffset CreatedAt,
    string FirstResponse,
    string FirstResponseZh);

public sealed record SupportTicketFolderRequest(
    string Path,
    bool OpenRequested,
    string? Error);

/// <summary>
/// Local-only support ticket storage and the deliberately fictional support desk.
/// No network client, credential store, or external service is used here.
/// </summary>
public sealed class SupportTicketService
{
    public const string LocalOnlyDisclosure =
        "Nothing is sent anywhere. Tickets are stored only on this machine; no ticket exists outside this app.";

    public const string LocalOnlyDisclosureZh =
        "所有內容只會留喺呢部機。唔會傳送出去，呢個 app 之外唔會有工單。";

    public const string RecoveryInstructions =
        "Resolution opens the local application-data folder so you can delete it yourself if you need to reset a local UX lock. This action never deletes anything.";

    public const string RecoveryInstructionsZh =
        "處理方法會開啟本機 application-data 資料夾，方便你自行刪除資料來重設本機體驗鎖。呢個按鈕唔會刪除任何嘢。";

    private const int MaxTickets = 500;
    private const int MaxDescriptionLength = 4_000;
    private readonly string _storagePath;
    private readonly Func<string, bool> _folderOpener;
    private readonly List<SupportTicket> _tickets;
    private readonly object _sync = new();

    public SupportTicketService(string? storagePath = null, Func<string, bool>? folderOpener = null)
    {
        _storagePath = storagePath ?? GetDefaultStoragePath();
        _folderOpener = folderOpener ?? OpenFolderWithShell;
        _tickets = LoadTickets(_storagePath);
    }

    public static string ApplicationDataFolderPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge");

    public IReadOnlyList<SupportTicket> Tickets
    {
        get
        {
            lock (_sync) return _tickets.ToArray();
        }
    }

    public SupportTicket CreateTicket(
        SupportTicketCategory category,
        string description,
        SupportTicketSeverity severity)
    {
        string normalized = (description ?? string.Empty).Trim();
        if (normalized.Length is < 1 or > MaxDescriptionLength)
            throw new ArgumentException($"Description must contain 1–{MaxDescriptionLength} characters.", nameof(description));

        var ticket = new SupportTicket(
            Guid.NewGuid(),
            CreateTicketNumber(),
            category,
            normalized,
            severity,
            SupportTicketStatus.New,
            DateTimeOffset.UtcNow,
            "Your local support ticket was received. No network request was made, and the next step is available on this machine.",
            "你嘅本機支援工單已收到。冇發出網絡請求，下一步只會喺呢部機處理。");

        lock (_sync)
        {
            _tickets.Insert(0, ticket);
            if (_tickets.Count > MaxTickets) _tickets.RemoveRange(MaxTickets, _tickets.Count - MaxTickets);
            SaveTickets(_storagePath, _tickets);
        }

        return ticket;
    }

    public bool TryAdvanceStatus(Guid id, out SupportTicket? updated)
    {
        lock (_sync)
        {
            int index = _tickets.FindIndex(ticket => ticket.Id == id);
            if (index < 0 || _tickets[index].Status == SupportTicketStatus.Resolved)
            {
                updated = index < 0 ? null : _tickets[index];
                return false;
            }

            SupportTicket current = _tickets[index];
            SupportTicketStatus next = current.Status switch
            {
                SupportTicketStatus.New => SupportTicketStatus.Acknowledged,
                SupportTicketStatus.Acknowledged => SupportTicketStatus.InProgress,
                SupportTicketStatus.InProgress => SupportTicketStatus.Resolved,
                _ => SupportTicketStatus.Resolved,
            };
            updated = current with { Status = next };
            _tickets[index] = updated;
            SaveTickets(_storagePath, _tickets);
            return true;
        }
    }

    /// <summary>Advance every selected ticket that is not already resolved and persist once.</summary>
    public int AdvanceTickets(IEnumerable<Guid>? ids, out int alreadyResolved, out string error)
    {
        alreadyResolved = 0;
        error = string.Empty;
        Guid[] requested = (ids ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToArray();
        if (requested.Length == 0) return 0;

        lock (_sync)
        {
            var changed = new List<(int Index, SupportTicket Previous)>();
            foreach (Guid id in requested)
            {
                int index = _tickets.FindIndex(ticket => ticket.Id == id);
                if (index < 0 || _tickets[index].Status == SupportTicketStatus.Resolved)
                {
                    alreadyResolved++;
                    continue;
                }

                SupportTicket previous = _tickets[index];
                SupportTicketStatus next = previous.Status switch
                {
                    SupportTicketStatus.New => SupportTicketStatus.Acknowledged,
                    SupportTicketStatus.Acknowledged => SupportTicketStatus.InProgress,
                    SupportTicketStatus.InProgress => SupportTicketStatus.Resolved,
                    _ => SupportTicketStatus.Resolved,
                };
                _tickets[index] = previous with { Status = next };
                changed.Add((index, previous));
            }

            if (changed.Count == 0) return 0;
            try
            {
                SaveTickets(_storagePath, _tickets);
                return changed.Count;
            }
            catch (Exception exception)
            {
                foreach ((int index, SupportTicket previous) in changed) _tickets[index] = previous;
                error = exception.Message;
                return 0;
            }
        }
    }

    /// <summary>Delete selected tickets transactionally; failed persistence restores the in-memory list.</summary>
    public int DeleteTickets(IEnumerable<Guid>? ids, out string error)
    {
        error = string.Empty;
        Guid[] requested = (ids ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToArray();
        if (requested.Length == 0) return 0;

        lock (_sync)
        {
            List<SupportTicket> snapshot = _tickets.ToList();
            int removed = _tickets.RemoveAll(ticket => requested.Contains(ticket.Id));
            if (removed == 0) return 0;
            try
            {
                SaveTickets(_storagePath, _tickets);
                return removed;
            }
            catch (Exception exception)
            {
                _tickets.Clear();
                _tickets.AddRange(snapshot);
                error = exception.Message;
                return 0;
            }
        }
    }

    public static bool ExportJson(string path, IEnumerable<SupportTicket> tickets, out string error)
        => Export(path, tickets, values => JsonSerializer.Serialize(values, JsonOptions), out error);

    public static bool ExportCsv(string path, IEnumerable<SupportTicket> tickets, out string error)
        => Export(path, tickets, values =>
        {
            var builder = new StringBuilder("ticketNumber,category,description,severity,status,createdAt\n");
            foreach (SupportTicket ticket in values)
            {
                builder.Append(Csv(ticket.TicketNumber)).Append(',')
                    .Append(Csv(ticket.Category.ToString())).Append(',')
                    .Append(Csv(ticket.Description)).Append(',')
                    .Append(Csv(ticket.Severity.ToString())).Append(',')
                    .Append(Csv(ticket.Status.ToString())).Append(',')
                    .Append(Csv(ticket.CreatedAt.ToString("O"))).Append('\n');
            }
            return builder.ToString();
        }, out error);

    public static bool ExportMarkdown(string path, IEnumerable<SupportTicket> tickets, out string error)
        => Export(path, tickets, values =>
        {
            var builder = new StringBuilder("# Support Tickets\n\n| Ticket | Category | Severity | Status | Created | Description |\n|---|---|---|---|---|---|\n");
            foreach (SupportTicket ticket in values)
            {
                builder.Append('|').Append(Markdown(ticket.TicketNumber)).Append('|')
                    .Append(Markdown(ticket.Category.ToString())).Append('|')
                    .Append(Markdown(ticket.Severity.ToString())).Append('|')
                    .Append(Markdown(ticket.Status.ToString())).Append('|')
                    .Append(Markdown(ticket.CreatedAt.ToString("O"))).Append('|')
                    .Append(Markdown(ticket.Description)).Append('|').Append('\n');
            }
            return builder.ToString();
        }, out error);

    public static bool ExportHtml(string path, IEnumerable<SupportTicket> tickets, out string error)
        => Export(path, tickets, values =>
        {
            var builder = new StringBuilder("<!doctype html><meta charset=\"utf-8\"><title>Support Tickets</title><table><thead><tr><th>Ticket</th><th>Category</th><th>Severity</th><th>Status</th><th>Created</th><th>Description</th></tr></thead><tbody>");
            foreach (SupportTicket ticket in values)
            {
                builder.Append("<tr><td>").Append(Html(ticket.TicketNumber)).Append("</td><td>")
                    .Append(Html(ticket.Category.ToString())).Append("</td><td>")
                    .Append(Html(ticket.Severity.ToString())).Append("</td><td>")
                    .Append(Html(ticket.Status.ToString())).Append("</td><td>")
                    .Append(Html(ticket.CreatedAt.ToString("O"))).Append("</td><td>")
                    .Append(Html(ticket.Description)).Append("</td></tr>");
            }
            return builder.Append("</tbody></table>").ToString();
        }, out error);

    public SupportTicketFolderRequest RequestOpenApplicationDataFolder()
    {
        string path = ApplicationDataFolderPath;
        try
        {
            Directory.CreateDirectory(path);
            bool opened = _folderOpener(path);
            return new SupportTicketFolderRequest(path, opened, opened ? null : "The file manager did not accept the folder request.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new SupportTicketFolderRequest(path, false, exception.Message);
        }
    }

    public static string GetDefaultStoragePath() => Path.Combine(ApplicationDataFolderPath, "support-tickets", "tickets.json");

    private static string CreateTicketNumber()
        => $"WF-TKT-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..31].ToUpperInvariant();

    private static List<SupportTicket> LoadTickets(string path)
    {
        try
        {
            if (!File.Exists(path)) return new List<SupportTicket>();
            using FileStream stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<List<SupportTicket>>(stream, JsonOptions) ?? new List<SupportTicket>();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new List<SupportTicket>();
        }
    }

    private static void SaveTickets(string path, IReadOnlyList<SupportTicket> tickets)
    {
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory)) throw new IOException("Ticket storage path has no directory.");
        Directory.CreateDirectory(directory);
        string temporaryPath = path + ".tmp";
        try
        {
            using (FileStream stream = File.Create(temporaryPath))
                JsonSerializer.Serialize(stream, tickets, JsonOptions);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
        }
    }

    private static bool Export(
        string path,
        IEnumerable<SupportTicket> tickets,
        Func<IReadOnlyList<SupportTicket>, string> render,
        out string error)
    {
        error = string.Empty;
        try
        {
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory)) throw new IOException("Export path has no directory.");
            Directory.CreateDirectory(directory);
            IReadOnlyList<SupportTicket> snapshot = tickets.ToArray();
            File.WriteAllText(fullPath, render(snapshot), new UTF8Encoding(false));
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static string Csv(string? value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    private static string Markdown(string? value) => (value ?? string.Empty).Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    private static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static bool OpenFolderWithShell(string path)
    {
        using Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
        return process is not null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
