using System;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 總搜尋結果 · Master search results — matching module pages (navigable) AND matching settings/tweaks,
/// the latter rendered as a live, working ControlRowList so toggles actually work right in the results.
/// </summary>
public sealed partial class SearchResultsPage : Page
{
    private const int MaxTweaks = 120;

    public SearchResultsPage()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) { RenderLabels(); Run(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        RenderLabels();
        var q = e.Parameter as string ?? "";
        SearchBox.Text = q;
        Run();
    }

    private void RenderLabels()
    {
        Header.Title = "Search · 搜尋";
        SearchBox.PlaceholderText = P("Search every page and setting…", "搜尋所有頁面同設定…");
    }

    private void SearchBox_PatternChanged(object? sender, EventArgs args) => Run();

    private void Run()
    {
        string query = SearchBox.Text ?? string.Empty;
        bool hasQuery = SearchBox.IsRegexMode ? query.Length > 0 : !string.IsNullOrWhiteSpace(query);

        // ---- Pages ----
        var pages = SearchPatternService.Filter(
            ModuleRegistry.All,
            module => module.Haystack,
            SearchBox.Spec).ToList();
        PagesGrid.ItemsSource = pages;
        PagesLabel.Text = P($"Pages — {pages.Count}", $"頁面 — {pages.Count}");

        // ---- Settings & tweaks (live, working) ----
        int tweakCount = 0;
        bool canSearchTweaks = SearchBox.IsRegexMode ? query.Length > 0 : query.Trim().Length >= 2;
        if (canSearchTweaks)
        {
            var tweaks = SearchPatternService.Filter(
                TweakCatalog.All,
                tweak => tweak.SearchHaystack,
                SearchBox.Spec).Take(MaxTweaks).ToList();
            tweakCount = tweaks.Count;
            TweaksList.SetTweaks(tweaks);
            TweaksLabel.Text = P($"Settings & tweaks — {tweakCount} (toggle right here)", $"設定同調校 — {tweakCount}（喺度直接切換）");
        }
        else
        {
            TweaksList.Clear();
            TweaksLabel.Text = P("Settings & tweaks — type 2+ letters to search settings", "設定同調校 — 打 2 個字以上嚟搜尋設定");
        }

        EmptyText.Text = (pages.Count == 0 && tweakCount == 0 && hasQuery)
            ? P("No pages or settings match your search.", "冇頁面或者設定符合你嘅搜尋。")
            : "";
    }

    private void Pages_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModuleInfo m)
            Navigator.GoToModule?.Invoke(m.Tag);
    }
}
