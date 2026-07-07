using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Emoji 選擇器 · Emoji picker — browse ~300 common emoji, filter by category + search, click to copy to
/// the clipboard. Keeps a small "recent" strip. Pure managed, never-throws. Bilingual chrome.
/// </summary>
public sealed partial class EmojiModule : Page
{
    private const int RecentMax = 12;
    private readonly List<string> _recent = new();
    private bool _suppress;

    public EmojiModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    /// <summary>Localized display label for a category key (null/empty => the synthetic "All" entry).</summary>
    private string CatLabel(string? key) => key switch
    {
        null or "" or "All" => P("All", "全部"),
        EmojiService.CatSmileys => P("Smileys & Emotion", "表情同情緒"),
        EmojiService.CatPeople => P("People", "人物"),
        EmojiService.CatAnimals => P("Animals & Nature", "動物同自然"),
        EmojiService.CatFood => P("Food & Drink", "食物同飲品"),
        EmojiService.CatTravel => P("Travel & Places", "旅遊同地方"),
        EmojiService.CatActivities => P("Activities", "活動"),
        EmojiService.CatObjects => P("Objects", "物件"),
        EmojiService.CatSymbols => P("Symbols", "符號"),
        EmojiService.CatFlags => P("Flags", "旗幟"),
        _ => key
    };

    private void Render()
    {
        try
        {
            Header.Title = "Emoji Picker · Emoji 選擇器";
            HeaderBlurb.Text = P(
                "Browse about 300 common emoji, filter by category or search by name, then click one to copy it to the clipboard.",
                "瀏覽大約 300 個常用 emoji，可以揀分類或者打名嚟搵，撳一下就複製去剪貼簿。");
            RecentLabel.Text = P("Recent", "最近用過");
            SearchBox.PlaceholderText = P("Search by name or keyword…", "打名或者關鍵字嚟搵…");

            // Rebuild the category combo, preserving the current selection index.
            int keep = CategoryBox.SelectedIndex;
            _suppress = true;
            CategoryBox.Items.Clear();
            CategoryBox.Items.Add(CatLabel("All"));
            foreach (var c in EmojiService.Categories)
                CategoryBox.Items.Add(CatLabel(c));
            CategoryBox.SelectedIndex = keep < 0 ? 0 : Math.Min(keep, CategoryBox.Items.Count - 1);
            _suppress = false;

            RebuildRecentStrip();
            if (EmojiList.Items.Count == 0)
                Refresh();
            else
                SetIdleStatus();
        }
        catch (Exception ex)
        {
            SetStatus(P("Could not draw the picker: ", "畫唔到選擇器：") + ex.Message);
        }
    }

    private string? SelectedCategoryKey()
    {
        int i = CategoryBox.SelectedIndex;
        if (i <= 0) return null; // "All"
        int idx = i - 1;
        if (idx >= 0 && idx < EmojiService.Categories.Length) return EmojiService.Categories[idx];
        return null;
    }

    private void Refresh()
    {
        try
        {
            var items = EmojiService.Filter(SelectedCategoryKey(), SearchBox?.Text);
            _suppress = true;
            EmojiList.ItemsSource = items;
            _suppress = false;
            SetIdleStatus(items.Count);
        }
        catch (Exception ex)
        {
            SetStatus(P("Filter failed: ", "篩選失敗：") + ex.Message);
        }
    }

    private void Category_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        Refresh();
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Refresh();
    }

    private void Emoji_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        if (EmojiList.SelectedItem is EmojiService.EmojiItem item)
            Copy(item);
    }

    private void Emoji_Clicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is EmojiService.EmojiItem item)
            Copy(item);
    }

    private void Copy(EmojiService.EmojiItem item)
    {
        try
        {
            var pkg = new DataPackage();
            pkg.SetText(item.Emoji);
            Clipboard.SetContent(pkg);
            AddRecent(item.Emoji);
            SetStatus(P("copied ", "已複製 ") + item.Emoji);
        }
        catch (Exception ex)
        {
            SetStatus(P("Copy failed: ", "複製失敗：") + ex.Message);
        }
    }

    private void AddRecent(string emoji)
    {
        _recent.RemoveAll(x => string.Equals(x, emoji, StringComparison.Ordinal));
        _recent.Insert(0, emoji);
        if (_recent.Count > RecentMax) _recent.RemoveRange(RecentMax, _recent.Count - RecentMax);
        RebuildRecentStrip();
    }

    private void RebuildRecentStrip()
    {
        try
        {
            RecentStrip.Children.Clear();
            if (_recent.Count == 0)
            {
                RecentStrip.Children.Add(new TextBlock
                {
                    Text = P("(nothing yet)", "（未有）"),
                    FontSize = 12,
                    Opacity = 0.6
                });
                return;
            }
            foreach (var emoji in _recent)
            {
                var capture = emoji;
                var btn = new Button
                {
                    Content = new TextBlock { Text = emoji, FontSize = 22 },
                    Padding = new Thickness(6, 2, 6, 2),
                    Background = null,
                    BorderThickness = new Thickness(0)
                };
                btn.Click += (_, _) => Copy(new EmojiService.EmojiItem(capture, "recent", "Recent"));
                RecentStrip.Children.Add(btn);
            }
        }
        catch
        {
            // never throw from UI rebuild
        }
    }

    private void SetIdleStatus() => SetIdleStatus(EmojiList.Items.Count);

    private void SetIdleStatus(int count) =>
        SetStatus(P($"{count} emoji shown — click one to copy.", $"顯示緊 {count} 個 emoji — 撳一下就複製。"));

    private void SetStatus(string text)
    {
        if (StatusText != null) StatusText.Text = text;
    }
}
