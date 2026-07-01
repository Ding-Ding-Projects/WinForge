using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 正則速查 · Regex cheatsheet — an embedded, comprehensive .NET-regex reference. Search + category
/// filter over grouped tokens; click a row to copy the token; a "ready-made patterns" list copies full
/// regexes. Reference tool only; never mutates anything and never throws. Bilingual (English + 粵語).
/// </summary>
public sealed partial class RegexCheatModule : Page
{
    // View-model rows use classic {Binding}, so plain public properties (no x:Bind).
    public sealed class EntryVm
    {
        public string Token { get; set; } = "";
        public string CategoryLabel { get; set; } = "";
        public string Desc { get; set; } = "";
        public string Example { get; set; } = "";
    }

    public sealed class RecipeVm
    {
        public string Name { get; set; } = "";
        public string Pattern { get; set; } = "";
    }

    private readonly ObservableCollection<EntryVm> _entries = new();
    private readonly ObservableCollection<RecipeVm> _recipes = new();

    // Parallel list of category English keys aligned to CategoryBox items (index 0 = "All").
    private readonly List<string?> _categoryKeys = new();
    private bool _suppress;

    public RegexCheatModule()
    {
        InitializeComponent();
        EntryList.ItemsSource = _entries;
        RecipeList.ItemsSource = _recipes;
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Regex Cheatsheet · 正則速查";
            HeaderBlurb.Text = P(
                "A comprehensive .NET regular-expression reference. Search or pick a category, then click any row to copy the token to the clipboard. Scroll down for ready-made full patterns.",
                "全面嘅 .NET 正則表達式參考。搜尋或者揀類別，撳任何一行就會將個符號複製到剪貼簿。落去仲有現成嘅完整模式。");
            SearchBox.PlaceholderText = P("Search tokens or descriptions…", "搜尋符號或者描述…");
            HintText.Text = P("Tip: click a row to copy its token; nothing here changes your system.",
                "貼士：撳一行就會複製個符號；呢度乜都唔會改到你部機。");
            RefTitle.Text = P("Reference", "參考");
            RecipeTitle.Text = P("Ready-made patterns", "現成模式");
            RecipeHint.Text = P("Click to copy a full, ready-to-use regex.", "撳一下就複製一個完整、即用嘅正則。");
            EmptyText.Text = P("No matches. Try a different search or category.", "冇符合。試下另一個搜尋或者類別。");

            RebuildCategories();
            RefreshEntries();
            RefreshRecipes();
        }
        catch { /* reference tool — never throw from render */ }
    }

    private void RebuildCategories()
    {
        try
        {
            var selectedKey = SelectedCategoryKey();
            _suppress = true;
            CategoryBox.Items.Clear();
            _categoryKeys.Clear();

            CategoryBox.Items.Add(P("All categories", "所有類別"));
            _categoryKeys.Add(null);

            int restore = 0;
            foreach (var (en, zh) in RegexCheatService.Categories())
            {
                CategoryBox.Items.Add(P(en, zh));
                _categoryKeys.Add(en);
                if (string.Equals(en, selectedKey, StringComparison.OrdinalIgnoreCase))
                    restore = _categoryKeys.Count - 1;
            }
            CategoryBox.SelectedIndex = restore;
            _suppress = false;
        }
        catch { _suppress = false; }
    }

    private string? SelectedCategoryKey()
    {
        int i = CategoryBox.SelectedIndex;
        return (i >= 0 && i < _categoryKeys.Count) ? _categoryKeys[i] : null;
    }

    private void RefreshEntries()
    {
        try
        {
            var query = SearchBox.Text;
            var cat = SelectedCategoryKey();
            var results = RegexCheatService.Filter(query, cat);

            _entries.Clear();
            foreach (var e in results)
            {
                _entries.Add(new EntryVm
                {
                    Token = e.Token,
                    CategoryLabel = P(e.Category, e.CategoryZh),
                    Desc = P(e.DescEn, e.DescZh),
                    Example = e.Example,
                });
            }
            EmptyText.Visibility = _entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch { /* never throw */ }
    }

    private void RefreshRecipes()
    {
        try
        {
            _recipes.Clear();
            foreach (var r in RegexCheatService.Recipes)
                _recipes.Add(new RecipeVm { Name = P(r.Name, r.NameZh), Pattern = r.Pattern });
        }
        catch { /* never throw */ }
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        RefreshEntries();
    }

    private void Category_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        RefreshEntries();
    }

    private void Entry_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is EntryVm vm) Copy(vm.Token, P("Copied token", "已複製符號"));
    }

    private void Recipe_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RecipeVm vm) Copy(vm.Pattern, P("Copied pattern", "已複製模式"));
    }

    private void Copy(string text, string what)
    {
        try
        {
            if (string.IsNullOrEmpty(text)) return;
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);

            Info.Title = what;
            Info.Message = text;
            Info.Severity = InfoBarSeverity.Success;
            Info.IsOpen = true;
        }
        catch { /* clipboard can transiently fail — swallow */ }
    }
}
