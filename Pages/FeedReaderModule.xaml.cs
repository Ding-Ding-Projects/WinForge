using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Native RSS/Atom reader inspired by QuiteRSS and Fluent Reader.
/// Everything runs in-app: feed storage is local JSON, refresh uses HttpClient, parsing uses managed XML.
/// </summary>
public sealed partial class FeedReaderModule : Page
{
    private readonly FeedReaderService _svc = new();
    private readonly ObservableCollection<FeedSubscription> _feeds = new();
    private readonly ObservableCollection<FeedArticle> _articles = new();
    private CancellationTokenSource? _refreshCts;
    private FeedArticle? _selectedArticle;

    public FeedReaderModule()
    {
        InitializeComponent();
        FeedList.ItemsSource = _feeds;
        ArticleList.ItemsSource = _articles;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) =>
        {
            Render();
            RefreshFeedList();
            ClearArticlePreview();
        };
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            _refreshCts?.Cancel();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private void Render()
    {
        HeaderTitle.Text = "Feed Reader · RSS 閱讀器";
        HeaderBlurb.Text = P(
            "A native QuiteRSS/Fluent Reader-style RSS and Atom reader. Add feeds, refresh them with HttpClient, read article summaries, and copy links without launching an external reader.",
            "原生 QuiteRSS／Fluent Reader 式 RSS 同 Atom 閱讀器。新增 feed、用 HttpClient 重新整理、閱讀文章摘要同複製連結，唔會啟動外部閱讀器。");
        FeedUrlBox.PlaceholderText = P("https://example.com/feed.xml", "https://example.com/feed.xml");
        AddFeedLabel.Text = P("Add", "新增");
        RefreshLabel.Text = P("Refresh", "重新整理");
        RemoveLabel.Text = P("Remove", "移除");
        FeedsHeader.Text = P("Subscriptions · 訂閱", "訂閱");
        CopyLinkLabel.Text = P("Copy link", "複製連結");
        if (_selectedArticle is null) ClearArticlePreview();
    }

    private void RefreshFeedList()
    {
        var selected = (FeedList.SelectedItem as FeedSubscription)?.Id;
        _feeds.Clear();
        foreach (var feed in _svc.Feeds.OrderBy(f => f.Title))
            _feeds.Add(feed);

        if (_feeds.Count == 0)
        {
            _articles.Clear();
            Show(InfoBarSeverity.Informational,
                P("No feeds yet", "未有 Feed"),
                P("Paste an RSS or Atom feed URL above and click Add.", "喺上面貼上 RSS 或 Atom feed 網址，再撳新增。"));
            return;
        }

        var idx = _feeds.ToList().FindIndex(f => f.Id == selected);
        FeedList.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private async void AddFeed_Click(object sender, RoutedEventArgs e)
        => await AddFeedAsync();

    private async void AddFeedAccel_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await AddFeedAsync();
    }

    private async Task AddFeedAsync()
    {
        try
        {
            var sub = _svc.Add(FeedUrlBox.Text);
            FeedUrlBox.Text = "";
            RefreshFeedList();
            FeedList.SelectedItem = _feeds.FirstOrDefault(f => f.Id == sub.Id);
            await RefreshSelectedAsync();
        }
        catch (Exception ex)
        {
            Show(InfoBarSeverity.Error, P("Could not add feed", "未能新增 Feed"), P(ex.Message, ex.Message));
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
        => await RefreshCurrentScopeAsync();

    private async void RefreshAccel_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await RefreshCurrentScopeAsync();
    }

    private async Task RefreshCurrentScopeAsync()
    {
        if (FeedList.SelectedItem is FeedSubscription)
            await RefreshSelectedAsync();
        else
            await RefreshAllAsync();
    }

    private void RemoveFeed_Click(object sender, RoutedEventArgs e)
    {
        if (FeedList.SelectedItem is not FeedSubscription feed) return;
        _svc.Remove(feed.Id);
        RefreshFeedList();
        ClearArticlePreview();
    }

    private async void FeedList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ClearArticlePreview();
        if (FeedList.SelectedItem is FeedSubscription)
            await RefreshSelectedAsync();
    }

    private async Task RefreshSelectedAsync()
    {
        if (FeedList.SelectedItem is not FeedSubscription feed) return;
        await RunRefresh(async ct => await _svc.RefreshAsync(feed, ct));
        RefreshFeedList();
    }

    private async Task RefreshAllAsync()
    {
        await RunRefresh(async ct => await _svc.RefreshAllAsync(ct));
        RefreshFeedList();
    }

    private async Task RunRefresh(Func<CancellationToken, Task<System.Collections.Generic.List<FeedArticle>>> op)
    {
        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        Busy.IsActive = true;
        AddFeedBtn.IsEnabled = RefreshBtn.IsEnabled = RemoveFeedBtn.IsEnabled = false;
        try
        {
            var loaded = await op(_refreshCts.Token);
            _articles.Clear();
            foreach (var article in loaded.OrderByDescending(a => a.Published ?? DateTimeOffset.MinValue))
                _articles.Add(article);

            Show(loaded.Count == 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success,
                loaded.Count == 0 ? P("No articles loaded", "未載入文章") : P("Feed refreshed", "Feed 已重新整理"),
                loaded.Count == 0
                    ? P("The feed returned no readable RSS/Atom entries.", "呢個 feed 無回傳可讀 RSS／Atom 項目。")
                    : P($"{loaded.Count} article(s) loaded.", $"已載入 {loaded.Count} 篇文章。"));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _articles.Clear();
            Show(InfoBarSeverity.Error, P("Refresh failed", "重新整理失敗"), P(ex.Message, ex.Message));
        }
        finally
        {
            Busy.IsActive = false;
            AddFeedBtn.IsEnabled = RefreshBtn.IsEnabled = RemoveFeedBtn.IsEnabled = true;
        }
    }

    private void ArticleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedArticle = ArticleList.SelectedItem as FeedArticle;
        if (_selectedArticle is null)
        {
            ClearArticlePreview();
            return;
        }

        ArticleTitle.Text = _selectedArticle.Title;
        ArticleMeta.Text = string.Join(" · ", new[]
        {
            _selectedArticle.FeedTitle,
            _selectedArticle.PublishedLabel,
            _selectedArticle.Author,
            _selectedArticle.Link,
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
        ArticleSummary.Text = string.IsNullOrWhiteSpace(_selectedArticle.Summary)
            ? P("No summary was included in this feed item.", "此 feed 項目無包含摘要。")
            : _selectedArticle.Summary;
        CopyLinkBtn.IsEnabled = !string.IsNullOrWhiteSpace(_selectedArticle.Link);
    }

    private void ClearArticlePreview()
    {
        _selectedArticle = null;
        ArticleTitle.Text = P("Select an article", "選取一篇文章");
        ArticleMeta.Text = "";
        ArticleSummary.Text = P("Article summaries appear here after you refresh a feed.", "重新整理 feed 後，文章摘要會喺呢度顯示。");
        CopyLinkBtn.IsEnabled = false;
    }

    private void CopyLink_Click(object sender, RoutedEventArgs e)
        => CopySelectedArticleLink();

    private void CopyLinkAccel_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        CopySelectedArticleLink();
    }

    private void CopySelectedArticleLink()
    {
        if (_selectedArticle is null || string.IsNullOrWhiteSpace(_selectedArticle.Link)) return;
        var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dp.SetText(_selectedArticle.Link);
        Clipboard.SetContent(dp);
        Clipboard.Flush();
        Show(InfoBarSeverity.Success, P("Copied", "已複製"), P("Article link copied.", "文章連結已複製。"));
    }

    private void Show(InfoBarSeverity severity, string title, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }
}
