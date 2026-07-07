using System;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 顯示某個分類嘅全部調校項目 · Lists every tweak in one category, with an in-category filter.
/// </summary>
public sealed partial class CategoryPage : Page
{
    private AppCategory? _category;
    private readonly ControlRowList _rows = new(); // one reusable list, re-populated on every rebuild

    public CategoryPage()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RenderHeader();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _category = e.Parameter as AppCategory ?? Categories.Appearance;
        RenderHeader();
        Populate(string.Empty);
    }

    private void RenderHeader()
    {
        if (_category is null) return;
        Header.Glyph = _category.Glyph;
        Header.Title = $"{_category.Name.En} · {_category.Name.Zh}";
        HeaderBlurb.Text = $"{_category.Blurb.En}\n{_category.Blurb.Zh}";
        FilterBox.PlaceholderText = Loc.I.Pick("Filter this section…", "篩選呢個分類…");
    }

    private void Populate(string filter)
    {
        if (_category is null) return;
        CardsPanel.Children.Clear();

        var tweaks = TweakCatalog.ByCategory(_category);
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            tweaks = tweaks.Where(t => t.SearchHaystack.Contains(f));
        }

        var list = tweaks.ToList();
        if (list.Count == 0)
        {
            _rows.Clear();
            CardsPanel.Children.Add(new TextBlock
            {
                Text = Loc.I.Pick("No matches.", "搵唔到。"),
                Opacity = 0.6,
                Margin = new Microsoft.UI.Xaml.Thickness(4, 12, 0, 0),
            });
            return;
        }

        // Reuse the one ControlRowList — re-populate it and (re)attach it to the panel.
        _rows.SetTweaks(list);
        CardsPanel.Children.Add(_rows);
    }

    private void FilterBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            Populate(sender.Text);
    }
}
