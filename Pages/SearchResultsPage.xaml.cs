using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 總搜尋結果 · Master search results — matching module pages (navigable) AND matching settings/tweaks,
/// the latter rendered as live, working TweakCards so toggles actually work right in the results.
/// </summary>
public sealed partial class SearchResultsPage : Page
{
    private const int MaxTweaks = 120;

    public SearchResultsPage()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => { RenderLabels(); Run(SearchBox.Text); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        RenderLabels();
        var q = e.Parameter as string ?? "";
        SearchBox.Text = q;
        Run(q);
    }

    private void RenderLabels()
    {
        Header.Title = "Search · 搜尋";
        SearchBox.PlaceholderText = P("Search every page and setting…", "搜尋所有頁面同設定…");
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) Run(sender.Text);
    }

    private void Run(string query)
    {
        query = (query ?? "").Trim();

        // ---- Pages ----
        var pages = ModuleRegistry.Search(query).ToList();
        PagesGrid.ItemsSource = pages;
        PagesLabel.Text = P($"Pages — {pages.Count}", $"頁面 — {pages.Count}");

        // ---- Settings & tweaks (live, working) ----
        TweaksPanel.Children.Clear();
        int tweakCount = 0;
        if (query.Length >= 2)
        {
            var tweaks = TweakCatalog.Search(query).Take(MaxTweaks).ToList();
            tweakCount = tweaks.Count;
            foreach (var t in tweaks)
            {
                var card = new TweakCard();
                card.SetTweak(t);
                TweaksPanel.Children.Add(card);
            }
            TweaksLabel.Text = P($"Settings & tweaks — {tweakCount} (toggle right here)", $"設定同調校 — {tweakCount}（喺度直接切換）");
        }
        else
        {
            TweaksLabel.Text = P("Settings & tweaks — type 2+ letters to search settings", "設定同調校 — 打 2 個字以上嚟搜尋設定");
        }

        EmptyText.Text = (pages.Count == 0 && tweakCount == 0 && query.Length > 0)
            ? P("No pages or settings match your search.", "冇頁面或者設定符合你嘅搜尋。")
            : "";
    }

    private void Pages_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModuleInfo m)
            Navigator.GoToModule?.Invoke(m.Tag);
    }
}
