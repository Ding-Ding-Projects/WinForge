using WinForge.Services;

var failures = new List<string>();
var passed = 0;
Run("feature and wiki corpus is bundled in the source tree", CorpusInventory);
Run("internal article links resolve locally", InternalLinksResolve);
Run("rendered HTML rewrites local article links", RenderedLinksStayLocal);
Run("offline documentation has a direct start-page route", StartPageRoute);
Run("offline documentation allows only local generated navigation", NavigationBoundary);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} offline-documentation contract tests");
    return 0;
}
foreach (string failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} offline-documentation contract tests");
return 1;

void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception exception) { failures.Add($"FAIL {name}: {exception.Message}"); }
}

void CorpusInventory()
{
    int featureCount = Directory.EnumerateFiles(Path.Combine(Environment.CurrentDirectory, "docs", "features"), "*.md", SearchOption.AllDirectories).Count();
    int wikiCount = Directory.EnumerateFiles(Path.Combine(Environment.CurrentDirectory, "docs", "wiki"), "*.md", SearchOption.AllDirectories).Count();
    if (featureCount == 0 || wikiCount == 0) throw new InvalidOperationException($"empty source corpus: features={featureCount}, wiki={wikiCount}");
    if (OfflineDocumentationService.Articles.Count < featureCount + wikiCount)
        throw new InvalidOperationException($"offline service dropped articles: source={featureCount + wikiCount}, loaded={OfflineDocumentationService.Articles.Count}");
}

void InternalLinksResolve()
{
    OfflineDocumentationService.Article? article = OfflineDocumentationService.Articles.FirstOrDefault(item => item.RelativePath.EndsWith("docs/features/universal/support-tickets.md", StringComparison.OrdinalIgnoreCase));
    if (article is null) throw new InvalidOperationException("Support Tickets article was not loaded.");
    string? target = OfflineDocumentationService.ResolveInternalTarget(article.RelativePath, "shared-settings.md");
    if (target is null || !target.StartsWith("docs/features/universal/", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("relative feature link did not stay in the feature corpus.");
    if (OfflineDocumentationService.ResolveInternalTarget(article.RelativePath, "https://example.invalid") is not null)
        throw new InvalidOperationException("external URL was treated as an internal article.");
}

void RenderedLinksStayLocal()
{
    OfflineDocumentationService.Article article = OfflineDocumentationService.Articles.First(item => item.RelativePath.EndsWith("docs/features/universal/support-tickets.md", StringComparison.OrdinalIgnoreCase));
    string html = OfflineDocumentationService.RenderHtml(article, dark: false);
    if (!html.Contains("winforge-doc:///", StringComparison.Ordinal))
        throw new InvalidOperationException("rendered local article links were not rewritten to the in-app scheme.");
    if (html.Contains("https://example.invalid", StringComparison.Ordinal))
        throw new InvalidOperationException("unexpected external test link appeared in rendered article.");
}

void StartPageRoute()
{
    string source = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "MainWindow.xaml.cs"));
    if (!source.Contains("case \"offlinedocs\":", StringComparison.Ordinal)
        || !source.Contains("Navigator.GoToModule?.Invoke(\"module.offlinedocs\")", StringComparison.Ordinal))
        throw new InvalidOperationException("--page offlinedocs is not routed to the offline documentation module.");
}

void NavigationBoundary()
{
    string source = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "Pages", "OfflineDocsPage.xaml.cs"));
    if (!source.Contains("data:text/html", StringComparison.Ordinal)
        || !source.Contains("External navigation is blocked", StringComparison.Ordinal))
        throw new InvalidOperationException("offline documentation does not distinguish generated HTML from external navigation.");
}
