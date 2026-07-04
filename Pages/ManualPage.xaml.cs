using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內建使用手冊 · The in-app Instruction Manual / User Guide.
/// 左邊目錄 + 手冊內搜尋；右邊雙語教學內容，可一鍵跳去對應工具。
/// Left: table of contents + in-manual search. Right: bilingual how-to content with jump-to-tool buttons.
/// </summary>
public sealed partial class ManualPage : Page
{
    private ManualSection _section = ManualContent.Sections[0];
    private string _filter = "";
    private readonly Dictionary<ManualEntry, FrameworkElement> _entryViews = new();

    public ManualPage()
    {
        InitializeComponent();
        Loaded += (_, _) => { RenderLabels(); BuildToc(); RenderContent(); };
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // 可深層連結去某段，例如 "manual:files-disks" · Optional deep-link to a section id.
        if (e.Parameter is string id && !string.IsNullOrWhiteSpace(id))
        {
            var s = ManualContent.Sections.FirstOrDefault(x => x.Id == id);
            if (s is not null) _section = s;
        }
    }

    private void OnLang(object? sender, EventArgs e)
    {
        RenderLabels();
        BuildToc();
        RenderContent();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void RenderLabels()
    {
        HeaderTitle.Text = "Instruction Manual · 使用手冊";
        HeaderSubtitle.Text = P(
            $"{ManualContent.Sections.Count} sections · {ManualContent.FeatureCount} how-to guides · English + 粵語",
            $"{ManualContent.Sections.Count} 個章節 · {ManualContent.FeatureCount} 篇教學 · 英文 + 粵語");
        FilterBox.PlaceholderText = P("Search the manual…", "搜尋手冊…");
        LangToggleText.Text = Loc.I.IsCantonesePrimary ? "粵 / EN" : "EN / 粵";
    }

    private void LangToggle_Click(object sender, RoutedEventArgs e) => Loc.I.Toggle();

    // ===================== Table of contents · 目錄 =====================

    private void BuildToc()
    {
        TocPanel.Children.Clear();

        if (_filter.Length > 0)
        {
            // 搜尋模式：列出所有符合嘅條目 · Search mode: flat list of matching entries.
            var hits = ManualContent.AllEntries.Where(en => en.Haystack.Contains(_filter)).ToList();
            TocPanel.Children.Add(new TextBlock
            {
                Text = P($"{hits.Count} result(s)", $"{hits.Count} 個結果"),
                FontSize = 12,
                Margin = new Thickness(4, 4, 0, 6),
                Foreground = Brush("TextFillColorSecondaryBrush"),
            });
            foreach (var en in hits)
                TocPanel.Children.Add(EntryButton(en, indent: false));
            return;
        }

        foreach (var s in ManualContent.Sections)
        {
            var header = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = ReferenceEquals(s, _section) ? Brush("SubtleFillColorSecondaryBrush") : null,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 7, 8, 7),
                Margin = new Thickness(0, 4, 0, 0),
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children =
                    {
                        new FontIcon { Glyph = s.Glyph, FontSize = 15, VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock
                        {
                            Text = P(s.TitleEn, s.TitleZh),
                            FontWeight = FontWeights.SemiBold,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                        },
                    },
                },
            };
            header.Click += (_, _) => { _section = s; BuildToc(); RenderContent(); };
            TocPanel.Children.Add(header);

            if (ReferenceEquals(s, _section))
                foreach (var en in s.Entries)
                    TocPanel.Children.Add(EntryButton(en, indent: true));
        }
    }

    private Button EntryButton(ManualEntry en, bool indent)
    {
        var b = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = null,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(indent ? 30 : 10, 5, 8, 5),
            Content = new TextBlock
            {
                Text = P(en.TitleEn, en.TitleZh),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush("TextFillColorSecondaryBrush"),
            },
        };
        b.Click += (_, _) =>
        {
            // 確保 condition：去返佢所屬章節，再 scroll 去嗰條目。
            var owner = ManualContent.Sections.First(s => s.Entries.Contains(en));
            if (!ReferenceEquals(owner, _section)) { _section = owner; }
            if (_filter.Length > 0) { _filter = ""; FilterBox.Text = ""; }
            BuildToc();
            RenderContent();
            if (_entryViews.TryGetValue(en, out var fe))
                fe.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0 });
        };
        return b;
    }

    // ===================== Content · 內容 =====================

    private void RenderContent()
    {
        ContentPanel.Children.Clear();
        _entryViews.Clear();

        if (_filter.Length > 0)
        {
            var hits = ManualContent.AllEntries.Where(en => en.Haystack.Contains(_filter)).ToList();
            ContentPanel.Children.Add(new TextBlock
            {
                Text = P($"Search results — {hits.Count}", $"搜尋結果 — {hits.Count}"),
                Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            });
            if (hits.Count == 0)
                ContentPanel.Children.Add(Muted(P("No how-to matches your search.", "冇教學符合你嘅搜尋。")));
            foreach (var en in hits)
            {
                var card = EntryCard(en);
                _entryViews[en] = card;
                ContentPanel.Children.Add(card);
            }
            ContentScroller.ChangeView(null, 0, null, true);
            return;
        }

        // 章節標題 + 簡介 · Section title + intro.
        ContentPanel.Children.Add(new TextBlock
        {
            Text = P(_section.TitleEn, _section.TitleZh),
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
        });
        AddBilingualBody(ContentPanel, _section.IntroEn, _section.IntroZh, secondaryMuted: true);

        foreach (var en in _section.Entries)
        {
            var card = EntryCard(en);
            _entryViews[en] = card;
            ContentPanel.Children.Add(card);
        }

        ContentScroller.ChangeView(null, 0, null, true);
    }

    private Border EntryCard(ManualEntry en)
    {
        var panel = new StackPanel { Spacing = 8 };

        // 標題 + 開啟掣 · Title row + optional "Open" button.
        var titleRow = new Grid();
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var glyph = ResolveGlyph(en);
        if (!string.IsNullOrEmpty(glyph))
            titleStack.Children.Add(new FontIcon { Glyph = glyph, FontSize = 18, VerticalAlignment = VerticalAlignment.Center });
        titleStack.Children.Add(new TextBlock
        {
            Text = P(en.TitleEn, en.TitleZh),
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        });
        Grid.SetColumn(titleStack, 0);
        titleRow.Children.Add(titleStack);

        if (!string.IsNullOrEmpty(en.Tag))
        {
            var open = new Button
            {
                VerticalAlignment = VerticalAlignment.Top,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new FontIcon { Glyph = "", FontSize = 13 },
                        new TextBlock { Text = P("Open", "開啟") },
                    },
                },
            };
            var tag = en.Tag;
            open.Click += (_, _) => OpenTarget(tag);
            Grid.SetColumn(open, 1);
            titleRow.Children.Add(open);
        }
        panel.Children.Add(titleRow);

        // 一句簡介 · Summary (both languages).
        AddBilingualBody(panel, en.SummaryEn, en.SummaryZh, secondaryMuted: true);

        // 步驟 · Steps (numbered, both languages).
        var steps = Loc.I.IsCantonesePrimary
            ? new[] { (en.StepsZh, false), (en.StepsEn, true) }
            : new[] { (en.StepsEn, false), (en.StepsZh, true) };
        foreach (var (list, muted) in steps)
        {
            if (list is null || list.Length == 0) continue;
            var sp = new StackPanel { Spacing = 3, Margin = new Thickness(0, 2, 0, 0) };
            for (int i = 0; i < list.Length; i++)
                sp.Children.Add(StepLine(i + 1, list[i], muted));
            panel.Children.Add(sp);
        }

        // 貼士 · Tip.
        if (!string.IsNullOrEmpty(en.TipEn) || !string.IsNullOrEmpty(en.TipZh))
        {
            var tip = new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = InfoBarSeverity.Informational,
                Title = P("Tip", "貼士"),
                Message = $"{P(en.TipEn ?? "", en.TipZh ?? "")}\n{P(en.TipZh ?? "", en.TipEn ?? "")}".Trim(),
                Margin = new Thickness(0, 4, 0, 0),
            };
            panel.Children.Add(tip);
        }

        return Card(panel);
    }

    private static Grid StepLine(int n, string text, bool muted)
    {
        var g = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var num = new TextBlock
        {
            Text = n + ".",
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Top,
            Foreground = Brush(muted ? "TextFillColorTertiaryBrush" : "AccentTextFillColorPrimaryBrush"),
        };
        Grid.SetColumn(num, 0);
        var body = new TextBlock
        {
            Text = text,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = muted ? Brush("TextFillColorSecondaryBrush") : null,
        };
        Grid.SetColumn(body, 1);
        g.Children.Add(num);
        g.Children.Add(body);
        return g;
    }

    private void AddBilingualBody(Panel host, string en, string zh, bool secondaryMuted)
    {
        var primary = Loc.I.Pick(en, zh);
        var secondary = Loc.I.Pick(zh, en);
        if (!string.IsNullOrEmpty(primary))
            host.Children.Add(new TextBlock { Text = primary, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrEmpty(secondary))
            host.Children.Add(new TextBlock
            {
                Text = secondary,
                TextWrapping = TextWrapping.Wrap,
                Foreground = secondaryMuted ? Brush("TextFillColorSecondaryBrush") : null,
            });
    }

    // ===================== Open a target tool · 開啟對應工具 =====================

    private static void OpenTarget(string tag)
    {
        if (tag.StartsWith("module.", StringComparison.Ordinal))
        {
            Navigator.GoToModule?.Invoke(tag);
            return;
        }
        switch (tag)
        {
            case "settings": Navigator.GoToSettings?.Invoke(); break;
            case "manual": break; // already here
            default:
                var cat = Catalog.Categories.All.FirstOrDefault(c => c.Id == tag);
                if (cat is not null) Navigator.GoToCategory?.Invoke(cat);
                break;
        }
    }

    // ===================== Search · 搜尋 =====================

    private void FilterBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _filter = (sender.Text ?? "").Trim().ToLowerInvariant();
        BuildToc();
        RenderContent();
    }

    // ===================== Small builders · 小工具 =====================

    /// <summary>模組圖示由登記表解析，確保同導覽一致 · Resolve module glyphs from the registry so icons match the nav.</summary>
    private static string ResolveGlyph(ManualEntry en)
    {
        if (en.Tag.StartsWith("module.", StringComparison.Ordinal))
        {
            var m = ModuleRegistry.All.FirstOrDefault(x => x.Tag == en.Tag);
            if (m is not null && !string.IsNullOrEmpty(m.Glyph)) return m.Glyph;
        }
        return en.Glyph;
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    private static TextBlock Muted(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        FontSize = 13,
        Foreground = Brush("TextFillColorSecondaryBrush"),
    };

    private static Border Card(UIElement content) => new()
    {
        Padding = new Thickness(18, 16, 18, 16),
        Background = Brush("CardBackgroundFillColorDefaultBrush"),
        BorderBrush = Brush("CardStrokeColorDefaultBrush"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Child = content,
    };
}
