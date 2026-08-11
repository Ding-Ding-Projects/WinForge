using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// Loads the shipped CHANGELOG.md for the offline in-app viewer. The parser is deliberately
/// bounded and keeps the source local; it never fetches release notes while the app is running.
/// </summary>
public static class ChangelogService
{
    private static readonly Regex SectionPattern = new(
        @"^##\s+(.+?)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex DatePattern = new(
        @"\b(20\d{2})-(\d{2})-(\d{2})\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CommitPattern = new(
        @"(?<![0-9a-f])([0-9a-f]{7,40})(?![0-9a-f])", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex LinkPattern = new(
        @"\[([^\]]+)\]\([^)]*\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public sealed record Entry(
        string Heading,
        string Body,
        DateOnly? Date,
        string? CommitSha,
        string SourcePath)
    {
        public string SearchText => Heading + "\n" + Body;
        public string PlainBody => ToPlainText(Body);
        public string? CommitUrl => string.IsNullOrWhiteSpace(CommitSha)
            ? null
            : $"https://github.com/Ding-Ding-Projects/WinForge/commit/{CommitSha}";
    }

    public sealed record LoadResult(IReadOnlyList<Entry> Entries, string SourcePath, string? Error);

    public static LoadResult Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");
        try
        {
            if (!File.Exists(path))
                return new(Array.Empty<Entry>(), path, "The offline changelog file is missing from the build output.");

            string text = File.ReadAllText(path);
            var entries = Parse(text, path);
            return new(entries, path, null);
        }
        catch (Exception ex)
        {
            return new(Array.Empty<Entry>(), path, ex.Message);
        }
    }

    public static IReadOnlyList<Entry> Filter(
        IEnumerable<Entry> entries,
        string? query,
        bool useRegex,
        DateOnly? from,
        DateOnly? to,
        out string? error)
    {
        error = null;
        IEnumerable<Entry> result = entries;
        if (from is not null) result = result.Where(entry => entry.Date is not null && entry.Date.Value >= from.Value);
        if (to is not null) result = result.Where(entry => entry.Date is not null && entry.Date.Value <= to.Value);

        string pattern = (query ?? string.Empty).Trim();
        if (pattern.Length == 0) return result.ToArray();
        if (pattern.Length > 4096)
        {
            error = "Search patterns are limited to 4,096 characters.";
            return Array.Empty<Entry>();
        }

        if (!useRegex)
            return result.Where(entry => entry.SearchText.Contains(pattern, StringComparison.OrdinalIgnoreCase)).ToArray();

        Regex regex;
        try
        {
            regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(250));
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            error = ex.Message;
            return Array.Empty<Entry>();
        }

        try { return result.Where(entry => regex.IsMatch(entry.SearchText)).ToArray(); }
        catch (RegexMatchTimeoutException)
        {
            error = "The regular expression exceeded the 250 ms safety limit.";
            return Array.Empty<Entry>();
        }
    }

    public static string ExportMarkdown(IEnumerable<Entry> entries, DateOnly? from, DateOnly? to)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# WinForge changelog export");
        sb.Append("Filter: ").Append(from?.ToString("yyyy-MM-dd") ?? "any date")
            .Append(" through ").Append(to?.ToString("yyyy-MM-dd") ?? "any date").AppendLine();
        sb.AppendLine();
        foreach (Entry entry in entries)
        {
            sb.Append("## ").AppendLine(entry.Heading);
            if (entry.Date is not null) sb.Append("Date: ").AppendLine(entry.Date.Value.ToString("yyyy-MM-dd"));
            if (!string.IsNullOrWhiteSpace(entry.CommitSha))
                sb.Append("Commit: ").AppendLine(entry.CommitSha);
            sb.AppendLine().AppendLine(entry.Body.Trim()).AppendLine();
        }
        return sb.ToString();
    }

    private static IReadOnlyList<Entry> Parse(string text, string sourcePath)
    {
        var entries = new List<Entry>();
        string? heading = null;
        var body = new StringBuilder();
        foreach (string line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            Match section = SectionPattern.Match(line);
            if (section.Success)
            {
                AddEntry(entries, heading, body.ToString(), sourcePath);
                heading = section.Groups[1].Value.Trim();
                body.Clear();
                continue;
            }

            if (heading is not null) body.AppendLine(line);
        }
        AddEntry(entries, heading, body.ToString(), sourcePath);
        return entries;
    }

    private static void AddEntry(List<Entry> entries, string? heading, string body, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(heading)) return;
        string normalized = body.Trim();
        DateOnly? date = null;
        Match dateMatch = DatePattern.Match(normalized);
        if (dateMatch.Success && DateOnly.TryParse($"{dateMatch.Groups[1].Value}-{dateMatch.Groups[2].Value}-{dateMatch.Groups[3].Value}", out var parsed))
            date = parsed;

        string? commit = CommitPattern.Matches(normalized)
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .FirstOrDefault(value => value.Length >= 7 && !value.All(char.IsDigit));
        entries.Add(new Entry(heading, normalized, date, commit, sourcePath));
    }

    private static string ToPlainText(string value)
    {
        string plain = LinkPattern.Replace(value, "$1");
        plain = plain.Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal);
        return plain;
    }
}
