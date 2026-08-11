using System;
using System.IO;
using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("disclosure is explicit and local-only", DisclosureIsLocalOnly);
Run("ticket creation is bounded and persists locally", TicketCreation);
Run("status progression is monotonic and bounded", StatusProgression);
Run("resolution requests opening without deleting", ResolutionDoesNotDelete);
Run("invalid descriptions are rejected", InvalidDescriptions);
Run("bulk actions and complete exports are bounded", BulkActionsAndExports);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} support-ticket contract tests");
    return 0;
}

foreach (string failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} support-ticket contract tests");
return 1;

void Run(string name, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {name}: {exception.Message}");
    }
}

void DisclosureIsLocalOnly()
{
    Assert(SupportTicketService.LocalOnlyDisclosure.Contains("Nothing is sent anywhere", StringComparison.Ordinal), "English disclosure does not state that nothing is sent");
    Assert(SupportTicketService.LocalOnlyDisclosure.Contains("only on this machine", StringComparison.Ordinal), "English disclosure does not state local storage");
    Assert(SupportTicketService.LocalOnlyDisclosureZh.Contains("唔會傳送出去", StringComparison.Ordinal), "Cantonese disclosure does not state that nothing is sent");
    Assert(SupportTicketService.RecoveryInstructions.Contains("never deletes anything", StringComparison.Ordinal), "resolution disclosure does not state non-deletion");
}

void TicketCreation()
{
    string root = CreateTempDirectory();
    string storage = Path.Combine(root, "tickets.json");
    try
    {
        var service = new SupportTicketService(storage, _ => true);
        SupportTicket ticket = service.CreateTicket(SupportTicketCategory.DataRecovery, "Please explain the local recovery path.", SupportTicketSeverity.High);
        Assert(ticket.TicketNumber.StartsWith("WF-TKT-", StringComparison.Ordinal), "ticket number is not locally generated");
        Assert(ticket.Status == SupportTicketStatus.New, "new ticket did not start in New status");
        Assert(!string.IsNullOrWhiteSpace(ticket.FirstResponse), "canned first response is missing");
        Assert(File.Exists(storage), "ticket was not persisted to the local path");
        Assert(new SupportTicketService(storage, _ => true).Tickets.Count == 1, "persisted ticket was not restored");
    }
    finally { DeleteTempDirectory(root); }
}

void StatusProgression()
{
    string root = CreateTempDirectory();
    try
    {
        var service = new SupportTicketService(Path.Combine(root, "tickets.json"), _ => true);
        SupportTicket ticket = service.CreateTicket(SupportTicketCategory.General, "Track this local ticket.", SupportTicketSeverity.Normal);
        SupportTicketStatus[] expected = { SupportTicketStatus.Acknowledged, SupportTicketStatus.InProgress, SupportTicketStatus.Resolved };
        foreach (SupportTicketStatus status in expected)
        {
            Assert(service.TryAdvanceStatus(ticket.Id, out SupportTicket? updated), $"could not advance to {status}");
            Assert(updated?.Status == status, $"expected {status}, got {updated?.Status}");
        }
        Assert(!service.TryAdvanceStatus(ticket.Id, out SupportTicket? resolved), "resolved ticket advanced again");
        Assert(resolved?.Status == SupportTicketStatus.Resolved, "resolved ticket changed while refusing advancement");
    }
    finally { DeleteTempDirectory(root); }
}

void ResolutionDoesNotDelete()
{
    string root = CreateTempDirectory();
    string sentinel = Path.Combine(root, "keep-me.txt");
    File.WriteAllText(sentinel, "must remain");
    string? openedPath = null;
    try
    {
        var service = new SupportTicketService(Path.Combine(root, "tickets.json"), path => { openedPath = path; return true; });
        SupportTicketFolderRequest request = service.RequestOpenApplicationDataFolder();
        Assert(request.OpenRequested, "folder opener was not requested");
        Assert(openedPath == request.Path, "folder opener received a different path");
        Assert(request.Path.EndsWith(Path.Combine("WinForge"), StringComparison.OrdinalIgnoreCase), "resolution path is not the WinForge app-data folder");
        Assert(File.Exists(sentinel), "resolution action deleted unrelated local data");
    }
    finally { DeleteTempDirectory(root); }
}

void InvalidDescriptions()
{
    string root = CreateTempDirectory();
    try
    {
        var service = new SupportTicketService(Path.Combine(root, "tickets.json"), _ => true);
        AssertThrows<ArgumentException>(() => service.CreateTicket(SupportTicketCategory.General, " ", SupportTicketSeverity.Low));
        AssertThrows<ArgumentException>(() => service.CreateTicket(SupportTicketCategory.General, new string('x', 4_001), SupportTicketSeverity.Low));
    }
    finally { DeleteTempDirectory(root); }
}

void BulkActionsAndExports()
{
    string root = CreateTempDirectory();
    try
    {
        string storage = Path.Combine(root, "tickets.json");
        var service = new SupportTicketService(storage, _ => true);
        SupportTicket first = service.CreateTicket(SupportTicketCategory.General, "First ticket", SupportTicketSeverity.Low);
        SupportTicket second = service.CreateTicket(SupportTicketCategory.Update, "Second ticket", SupportTicketSeverity.High);
        Guid[] ids = { first.Id, second.Id };

        int advanced = service.AdvanceTickets(ids, out int alreadyResolved, out string advanceError);
        Assert(advanceError.Length == 0 && advanced == 2 && alreadyResolved == 0, "bulk advancement did not update both tickets");

        foreach ((string extension, TicketExporter exporter) in new[]
        {
            (".json", (TicketExporter)SupportTicketService.ExportJson),
            (".csv", SupportTicketService.ExportCsv),
            (".md", SupportTicketService.ExportMarkdown),
            (".html", SupportTicketService.ExportHtml),
        })
        {
            string path = Path.Combine(root, "export" + extension);
            Assert(exporter(path, service.Tickets, out string error), $"export {extension} failed: {error}");
            Assert(File.Exists(path) && new FileInfo(path).Length > 0, $"export {extension} was empty");
        }

        int deleted = service.DeleteTickets(new[] { first.Id }, out string deleteError);
        Assert(deleteError.Length == 0 && deleted == 1, "bulk delete did not remove one ticket");
        Assert(service.Tickets.Count == 1 && service.Tickets[0].Id == second.Id, "bulk delete removed the wrong ticket");
    }
    finally { DeleteTempDirectory(root); }
}

static string CreateTempDirectory()
{
    string path = Path.Combine(Path.GetTempPath(), "WinForge-SupportTickets-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

static void DeleteTempDirectory(string path)
{
    try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void AssertThrows<T>(Action action) where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        return;
    }
    throw new InvalidOperationException($"expected {typeof(T).Name}");
}

delegate bool TicketExporter(string path, IEnumerable<SupportTicket> tickets, out string error);
