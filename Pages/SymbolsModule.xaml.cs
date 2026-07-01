using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 特殊符號調色盤 · Symbols palette — curated Unicode glyph sets (arrows, math, currency,
/// punctuation, Greek, box-drawing, stars, fractions, super/subscript) grouped by category.
/// Pick a category, search by name, click a symbol to copy it. Bilingual. Never throws.
/// </summary>
public sealed partial class SymbolsModule : Page
{
    private bool _suppress;
    private int _copyCount;

    public SymbolsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        PopulateCategories();
        ApplyFilter();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        PopulateCategories();
        ApplyFilter();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Symbols Palette · 特殊符號調色盤";
            HeaderBlurb.Text = P(
                "A palette of special characters — arrows, maths, currency, Greek, box-drawing and more. Pick a category or search by name, then click a symbol to copy it.",
                "特殊符號調色盤 — 箭嘴、數學、貨幣、希臘字母、框線等等。揀個分類或者用名搜尋，撳一下就複製。");
            SearchBox.PlaceholderText = P("Search by name…", "用名搜尋…");
            UpdateStatus();
        }
        catch { }
    }

    private void PopulateCategories()
    {
        try
        {
            _suppress = true;
            object? prev = CategoryBox.SelectedItem;
            string? prevTag = (prev as ComboBoxItem)?.Tag as string;

            CategoryBox.Items.Clear();
            CategoryBox.Items.Add(new ComboBoxItem { Content = P("All categories", "全部分類"), Tag = "" });
            foreach (var c in SymbolsService.Categories)
                CategoryBox.Items.Add(new ComboBoxItem { Content = c.Display, Tag = c.En });

            int idx = 0;
            if (!string.IsNullOrEmpty(prevTag))
            {
                for (int i = 0; i < CategoryBox.Items.Count; i++)
                    if ((CategoryBox.Items[i] as ComboBoxItem)?.Tag as string == prevTag) { idx = i; break; }
            }
            CategoryBox.SelectedIndex = idx;
        }
        catch { }
        finally { _suppress = false; }
    }

    private void Category_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        ApplyFilter();
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        try
        {
            string catEn = (CategoryBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            var items = SymbolsService.Filter(catEn, SearchBox.Text);
            SymbolList.ItemsSource = items;
            UpdateStatus(items.Count);
        }
        catch { }
    }

    private void Symbol_Clicked(object sender, ItemClickEventArgs e)
    {
        Copy(e.ClickedItem as SymbolsService.SymbolItem);
    }

    private void Symbol_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        if (e.AddedItems.FirstOrDefault() is SymbolsService.SymbolItem item)
            Copy(item);
    }

    private void Copy(SymbolsService.SymbolItem? item)
    {
        if (item is null || string.IsNullOrEmpty(item.Symbol)) return;
        try
        {
            var dp = new DataPackage();
            dp.SetText(item.Symbol);
            Clipboard.SetContent(dp);
            _copyCount++;
            StatusText.Text = P($"Copied “{item.Symbol}” ×{_copyCount}", $"已複製「{item.Symbol}」×{_copyCount}");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Copy failed — try again.", "複製失敗 — 再試一次。") + " " + ex.Message;
        }
    }

    private void UpdateStatus(int count = -1)
    {
        try
        {
            if (count < 0) count = SymbolList.Items?.Count ?? SymbolsService.All.Count;
            if (_copyCount > 0)
                StatusText.Text = P($"{count} shown · copied ×{_copyCount}", $"顯示 {count} 個 · 已複製 ×{_copyCount}");
            else
                StatusText.Text = P($"{count} symbols", $"{count} 個符號");
        }
        catch { }
    }
}
