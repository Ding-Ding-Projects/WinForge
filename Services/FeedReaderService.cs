using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WinForge.Services;

public sealed record FeedSubscription
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTimeOffset LastRefreshUtc { get; set; }
}

public sealed record FeedArticle
{
    public string FeedId { get; init; } = "";
    public string FeedTitle { get; init; } = "";
    public string Title { get; init; } = "";
    public string Link { get; init; } = "";
    public string Author { get; init; } = "";
    public string Summary { get; init; } = "";
    public DateTimeOffset? Published { get; init; }
    public string PublishedLabel => Published?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "";
}

public sealed class FeedReaderService
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge");
    private static readonly string StorePath = Path.Combine(Dir, "feed-reader.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20),
        DefaultRequestHeaders =
        {
            { "User-Agent", "WinForge Feed Reader/1.0" },
        },
    };

    private readonly List<FeedSubscription> _feeds = new();

    public FeedReaderService()
    {
        Load();
    }

    public IReadOnlyList<FeedSubscription> Feeds => _feeds;

    public FeedSubscription Add(string url)
    {
        url = (url ?? "").Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("Feed URL must be an absolute http(s) URL.");
        if (_feeds.Any(f => f.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("This feed is already subscribed.");

        var sub = new FeedSubscription
        {
            Title = uri.Host,
            Url = url,
        };
        _feeds.Add(sub);
        Save();
        return sub;
    }

    public void Remove(string id)
    {
        _feeds.RemoveAll(f => f.Id == id);
        Save();
    }

    public void Rename(string id, string title)
    {
        var f = _feeds.FirstOrDefault(x => x.Id == id);
        if (f is null) return;
        f.Title = string.IsNullOrWhiteSpace(title) ? f.Url : title.Trim();
        Save();
    }

    public async Task<List<FeedArticle>> RefreshAsync(FeedSubscription feed, CancellationToken ct = default)
    {
        var xml = await Http.GetStringAsync(feed.Url, ct);
        var parsed = Parse(xml, feed);
        if (parsed.feedTitle.Length > 0 && (feed.Title.Length == 0 || feed.Title.Equals(new Uri(feed.Url).Host, StringComparison.OrdinalIgnoreCase)))
            feed.Title = parsed.feedTitle;
        feed.LastRefreshUtc = DateTimeOffset.UtcNow;
        Save();
        return parsed.articles;
    }

    public async Task<List<FeedArticle>> RefreshAllAsync(CancellationToken ct = default)
    {
        var all = new List<FeedArticle>();
        foreach (var feed in _feeds.ToList())
        {
            ct.ThrowIfCancellationRequested();
            try { all.AddRange(await RefreshAsync(feed, ct)); }
            catch
            {
                // Keep one failing feed from blocking the rest; the page reports if no articles load.
            }
        }
        return all.OrderByDescending(a => a.Published ?? DateTimeOffset.MinValue).ToList();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(StorePath)) return;
            var data = JsonSerializer.Deserialize<List<FeedSubscription>>(File.ReadAllText(StorePath));
            if (data is null) return;
            _feeds.Clear();
            _feeds.AddRange(data.Where(f => !string.IsNullOrWhiteSpace(f.Url)));
        }
        catch
        {
            _feeds.Clear();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(StorePath, JsonSerializer.Serialize(_feeds, JsonOptions));
    }

    private static (string feedTitle, List<FeedArticle> articles) Parse(string xml, FeedSubscription feed)
    {
        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        var root = doc.Root ?? throw new InvalidOperationException("Feed XML has no root element.");

        if (root.Name.LocalName.Equals("rss", StringComparison.OrdinalIgnoreCase) ||
            root.Descendants().Any(e => e.Name.LocalName.Equals("channel", StringComparison.OrdinalIgnoreCase)))
            return ParseRss(root, feed);

        if (root.Name.LocalName.Equals("feed", StringComparison.OrdinalIgnoreCase))
            return ParseAtom(root, feed);

        throw new InvalidOperationException("Unsupported feed format. RSS and Atom are supported.");
    }

    private static (string feedTitle, List<FeedArticle> articles) ParseRss(XElement root, FeedSubscription feed)
    {
        var channel = Desc(root, "channel").FirstOrDefault() ?? root;
        var feedTitle = Text(channel, "title");
        var items = Desc(channel, "item").Select(item => new FeedArticle
        {
            FeedId = feed.Id,
            FeedTitle = string.IsNullOrWhiteSpace(feedTitle) ? feed.Title : feedTitle,
            Title = Text(item, "title"),
            Link = Text(item, "link"),
            Author = FirstText(item, "author", "creator", "dc:creator"),
            Summary = CleanHtml(FirstText(item, "description", "summary", "content", "encoded")),
            Published = ParseDate(FirstText(item, "pubDate", "published", "updated", "date")),
        }).Where(a => a.Title.Length > 0 || a.Link.Length > 0).ToList();
        return (feedTitle, items.OrderByDescending(a => a.Published ?? DateTimeOffset.MinValue).ToList());
    }

    private static (string feedTitle, List<FeedArticle> articles) ParseAtom(XElement root, FeedSubscription feed)
    {
        var feedTitle = Text(root, "title");
        var entries = Desc(root, "entry").Select(entry => new FeedArticle
        {
            FeedId = feed.Id,
            FeedTitle = string.IsNullOrWhiteSpace(feedTitle) ? feed.Title : feedTitle,
            Title = Text(entry, "title"),
            Link = AtomLink(entry),
            Author = Desc(entry, "author").Select(a => Text(a, "name")).FirstOrDefault(s => s.Length > 0) ?? "",
            Summary = CleanHtml(FirstText(entry, "summary", "content")),
            Published = ParseDate(FirstText(entry, "published", "updated")),
        }).Where(a => a.Title.Length > 0 || a.Link.Length > 0).ToList();
        return (feedTitle, entries.OrderByDescending(a => a.Published ?? DateTimeOffset.MinValue).ToList());
    }

    private static IEnumerable<XElement> Desc(XElement root, string localName) =>
        root.Descendants().Where(e => Matches(e, localName));

    private static bool Matches(XElement e, string name)
    {
        var local = name.Contains(':') ? name[(name.IndexOf(':') + 1)..] : name;
        return e.Name.LocalName.Equals(local, StringComparison.OrdinalIgnoreCase);
    }

    private static string Text(XElement root, string localName) =>
        root.Elements().FirstOrDefault(e => Matches(e, localName))?.Value.Trim() ?? "";

    private static string FirstText(XElement root, params string[] names)
    {
        foreach (var name in names)
        {
            var v = Text(root, name);
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        return "";
    }

    private static string AtomLink(XElement entry)
    {
        var links = entry.Elements().Where(e => Matches(e, "link")).ToList();
        var alt = links.FirstOrDefault(l => (l.Attribute("rel")?.Value ?? "alternate").Equals("alternate", StringComparison.OrdinalIgnoreCase))
                  ?? links.FirstOrDefault();
        return alt?.Attribute("href")?.Value ?? alt?.Value.Trim() ?? "";
    }

    private static DateTimeOffset? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dto))
            return dto;
        return null;
    }

    private static string CleanHtml(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var noTags = Regex.Replace(value, "<[^>]+>", " ");
        var decoded = WebUtility.HtmlDecode(noTags);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }
}
