using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>Complete local feature/wiki article browser; no network fetch is needed to read bundled docs.</summary>
public sealed partial class OfflineDocsPage : Page
{
    private bool _webReady;
    private bool _subscribed;
    private OfflineDocumentationService.Article? _selected;

    public OfflineDocsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_subscribed)
        {
            Loc.I.LanguageChanged += OnLanguageChanged;
            SearchBox.PatternChanged += SearchBox_PatternChanged;
            _subscribed = true;
        }
        RefreshCopy();
        RefreshArticles();
        await EnsureWebAsync();
        RenderSelected();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_subscribed) return;
        Loc.I.LanguageChanged -= OnLanguageChanged;
        SearchBox.PatternChanged -= SearchBox_PatternChanged;
        if (_webReady && Web.CoreWebView2 is not null) Web.CoreWebView2.NavigationStarting -= Web_NavigationStarting;
        _subscribed = false;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RefreshCopy();
        RefreshArticles();
        RenderSelected();
    }

    private void SearchBox_PatternChanged(object? sender, EventArgs e) => RefreshArticles();

    private void RefreshCopy()
    {
        Header.Title = Loc.I.Pick("Offline Documentation", "離線文件");
        Header.Subtitle = Loc.I.Pick(
            "Every bundled feature and wiki article is rendered locally; external links stay blocked.",
            "所有捆綁功能同 wiki 文章都喺本機渲染；外部連結會封鎖。 ");
        AutomationProperties.SetName(ArticlesList, Loc.I.Pick("Offline documentation article list", "離線文件文章清單"));
    }

    private void RefreshArticles()
    {
        IReadOnlyList<OfflineDocumentationService.Article> articles = OfflineDocumentationService.Articles;
        SearchPatternService.Matcher matcher = SearchBox.CompileMatcher();
        var visible = articles.Where(article => MatchesArticle(matcher, article)).ToArray();
        ArticlesList.ItemsSource = visible;
        CountText.Text = Loc.I.Pick(
            $"Showing {visible.Length} of {articles.Count} bundled article(s).",
            $"顯示緊 {articles.Count} 篇捆綁文章入面嘅 {visible.Length} 篇。");
        if (_selected is null || !visible.Any(article => article.RelativePath == _selected.RelativePath))
        {
            ArticlesList.SelectedIndex = visible.Length == 0 ? -1 : 0;
            _selected = ArticlesList.SelectedItem as OfflineDocumentationService.Article;
        }
        AutomationProperties.SetName(ArticlesList,
            Loc.I.Pick($"Offline documentation article list, {visible.Length} visible", $"離線文件文章清單，顯示 {visible.Length} 篇"));
        StatusText.Text = matcher.Error is null
            ? Loc.I.Pick("Documentation is available offline.", "文件可以離線使用。")
            : matcher.Error;
        RenderSelected();
    }

    private static bool MatchesArticle(SearchPatternService.Matcher matcher, OfflineDocumentationService.Article article)
    {
        if (!matcher.Ok) return false;
        SearchPatternService.MatchResult heading = matcher.MatchAny(new[] { article.Title, article.RelativePath });
        if (heading.IsMatch) return true;
        foreach (string line in article.Body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            if (matcher.Match(line).IsMatch) return true;
        return matcher.Spec.Query.Length == 0;
    }

    private async System.Threading.Tasks.Task EnsureWebAsync()
    {
        if (_webReady) return;
        try
        {
            await Web.EnsureCoreWebView2Async();
            if (Web.CoreWebView2 is null) return;
            Web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Web.CoreWebView2.NavigationStarting += Web_NavigationStarting;
            _webReady = true;
        }
        catch (Exception exception)
        {
            _webReady = false;
            StatusText.Text = Loc.I.Pick(
                "The offline article list is available, but WebView2 could not render the article: " + exception.Message,
                "離線文章清單可以使用，但 WebView2 無法渲染文章：" + exception.Message);
        }
    }

    private void Web_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        const string prefix = "winforge-doc:///";
        if (args.Uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            args.Cancel = true;
            string path = string.Join("/", args.Uri[prefix.Length..].Split('/').Select(Uri.UnescapeDataString));
            OfflineDocumentationService.Article? article = OfflineDocumentationService.Find(path);
            if (article is not null)
            {
                _selected = article;
                ArticlesList.SelectedItem = article;
                RenderSelected();
            }
            return;
        }

        if (!args.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase)
            && !args.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
        {
            args.Cancel = true;
            StatusText.Text = Loc.I.Pick("External navigation is blocked; this browser is offline-only.", "外部導覽已封鎖；呢個瀏覽器只支援離線文件。");
        }
    }

    private void ArticlesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = ArticlesList.SelectedItem as OfflineDocumentationService.Article;
        RenderSelected();
    }

    private void ArticlesList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is ListViewItem item && args.Item is OfflineDocumentationService.Article article)
            AutomationProperties.SetName(item, article.Title + ". " + article.RelativePath);
    }

    private void RenderSelected()
    {
        if (_selected is null || !_webReady || Web.CoreWebView2 is null) return;
        try
        {
            bool dark = Application.Current.RequestedTheme == ApplicationTheme.Dark;
            Web.NavigateToString(OfflineDocumentationService.RenderHtml(_selected, dark));
            StatusText.Text = Loc.I.Pick("Rendering bundled article: " + _selected.Title, "渲染緊捆綁文章：" + _selected.Title);
        }
        catch (Exception exception)
        {
            StatusText.Text = Loc.I.Pick("Article render failed: " + exception.Message, "文章渲染失敗：" + exception.Message);
        }
    }
}
