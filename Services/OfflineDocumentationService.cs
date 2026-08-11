using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WinForge.Services;

namespace WinForge.Services;

/// <summary>
/// Reads the feature and wiki Markdown copied beside the executable. It never fetches a document
/// from the network; the build's wildcard content items are the source of the offline bundle.
/// </summary>
public static class OfflineDocumentationService
{
    private const int MaxArticleCharacters = 1_000_000;
    private static readonly Lazy<IReadOnlyList<Article>> Cached = new(Load);

    public sealed record Article(string RelativePath, string Title, string Body, string Category)
    {
        public string SearchHaystack => Title + "\n" + RelativePath + "\n" + Body;
    }

    public static IReadOnlyList<Article> Articles => Cached.Value;

    public static Article? Find(string relativePath)
    {
        string normalized = NormalizeRelative(relativePath);
        return Articles.FirstOrDefault(article =>
            string.Equals(article.RelativePath, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static string RenderHtml(Article article, bool dark)
    {
        string html = PeekService.MarkdownToHtml(article.Body, dark);
        return Regex.Replace(html, "href=(['\"])(?<href>[^'\"]+)\\1", match =>
        {
            string href = match.Groups["href"].Value;
            string? target = ResolveInternalTarget(article.RelativePath, href);
            if (target is null) return match.Value;
            string replacement = "winforge-doc:///" + Uri.EscapeDataString(target);
            return "href=" + match.Groups[1].Value + replacement + match.Groups[1].Value;
        }, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    public static string? ResolveInternalTarget(string currentRelativePath, string href)
    {
        if (string.IsNullOrWhiteSpace(href) ||
            href.StartsWith("#", StringComparison.Ordinal) ||
            href.Contains(':', StringComparison.Ordinal))
            return null;

        string pathPart = href.Split('#', '?')[0];
        if (string.IsNullOrWhiteSpace(pathPart) || !pathPart.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return null;

        string current = NormalizeRelative(currentRelativePath);
        string currentDirectory = Path.GetDirectoryName(current.Replace('/', Path.DirectorySeparatorChar))?
            .Replace('\\', '/') ?? string.Empty;
        string normalized = NormalizeRelative(currentDirectory + "/" + pathPart.Replace('\\', '/'));
        return Articles.Any(article => string.Equals(article.RelativePath, normalized, StringComparison.OrdinalIgnoreCase))
            ? normalized
            : null;
    }

    private static IReadOnlyList<Article> Load()
    {
        foreach (string root in CandidateRoots())
        {
            if (!Directory.Exists(root)) continue;
            var articles = new List<Article>();
            foreach (string path in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
            {
                try
                {
                    FileInfo info = new(path);
                    if (info.Length > MaxArticleCharacters * 4L) continue;
                    string body = File.ReadAllText(path);
                    string relative = NormalizeRelative(Path.GetRelativePath(root, path));
                    string section = relative.StartsWith("features/", StringComparison.OrdinalIgnoreCase)
                        ? "features"
                        : "wiki";
                    articles.Add(new Article(relative, TitleFrom(body, path), body, section));
                }
                catch
                {
                    // A single unreadable article must not hide the rest of the offline manual.
                }
            }
            if (articles.Count > 0)
                return articles.OrderBy(article => article.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        return Array.Empty<Article>();
    }

    private static IEnumerable<string> CandidateRoots()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "docs");
        yield return Path.Combine(Environment.CurrentDirectory, "docs");
    }

    private static string TitleFrom(string body, string path)
    {
        foreach (string line in body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
                return trimmed[2..].Trim();
        }
        return Path.GetFileNameWithoutExtension(path);
    }

    private static string NormalizeRelative(string value)
    {
        string raw = value.Replace('\\', '/').TrimStart('/');
        var parts = new List<string>();
        foreach (string part in raw.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                continue;
            }
            parts.Add(part);
        }
        string normalized = string.Join("/", parts);
        return normalized.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : "docs/" + normalized;
    }
}
