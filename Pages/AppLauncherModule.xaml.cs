using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生 app 啟動器中心 · Hub for apps that cannot be reimplemented in-app ("stuffed"). Each entry opens a
/// popup (<see cref="AppLauncherWindow"/>) that auto-installs the whole dependency chain via winget and then
/// launches the REAL app in its native window. This page is intentionally about launching upstream programs
/// — it is the counterpart to the native-only OSS clone hub.
/// </summary>
public sealed partial class AppLauncherModule : Page
{
    private readonly List<string> _categoryKeys = ExternalApps.CategoryKeys.ToList();
    private string _query = "";
    private string _category = "";
    private bool _buildingCategories;
    private bool _locSubscribed;

    public AppLauncherModule()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_locSubscribed)
        {
            Loc.I.LanguageChanged += OnLanguageChanged;
            _locSubscribed = true;
        }
        RenderText();
        BuildCategories();
        RenderApps();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_locSubscribed) return;
        Loc.I.LanguageChanged -= OnLanguageChanged;
        _locSubscribed = false;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RenderText();
        BuildCategories();
        RenderApps();
    }

    private void RenderText()
    {
        Header.Title = "App Launcher · 原生 App 啟動器";
        HeaderBlurb.Text = P(
            "Apps that are too large to reimplement inside WinForge are launched in their own native window. Pick one and WinForge fully-automatically installs the app AND all its dependencies (via winget), then launches the real program — no 'go download X' detour. The native OSS Clones hub covers the apps WinForge remade as in-app C# tabs instead.",
            "有啲 app 太龐大，無法喺 WinForge 內重製，就以佢哋自己嘅原生視窗啟動。揀一個，WinForge 就會全自動安裝 app 同埋佢所有相依項（經 winget），再啟動真正嘅程式 — 唔使自己去下載。至於 WinForge 重製成 app 內 C# 分頁嘅 app，就喺「開源原生分頁」中心。");
        ClearLabel.Text = P("Clear", "清除");
        SearchBox.PlaceholderText = P("Search launchable native apps…", "搜尋可啟動嘅原生 app…");
    }

    private void BuildCategories()
    {
        _buildingCategories = true;
        try
        {
            CategoryBox.Items.Clear();
            CategoryBox.Items.Add(P("All apps", "全部 app"));
            foreach (var key in _categoryKeys)
            {
                var first = ExternalApps.All.First(a => a.CategoryEn == key);
                CategoryBox.Items.Add($"{first.CategoryEn} · {first.CategoryZh}");
            }

            var idx = string.IsNullOrEmpty(_category)
                ? 0
                : _categoryKeys.FindIndex(c => c.Equals(_category, StringComparison.OrdinalIgnoreCase)) + 1;
            CategoryBox.SelectedIndex = idx <= 0 ? 0 : idx;
        }
        finally { _buildingCategories = false; }
    }

    private void RenderApps()
    {
        AppsPanel.Children.Clear();
        var apps = FilteredApps().ToList();
        int installed = ExternalApps.All.Count(ExternalAppService.IsInstalled);
        SummaryText.Text = P(
            $"{apps.Count} shown · {ExternalApps.All.Count} launchable apps · {installed} installed",
            $"顯示 {apps.Count} 個 · 共 {ExternalApps.All.Count} 個可啟動 app · 已安裝 {installed} 個");

        if (apps.Count == 0)
        {
            AppsPanel.Children.Add(Card(new TextBlock
            {
                Text = P("No apps match the current filters.", "目前篩選無符合嘅 app。"),
                TextWrapping = TextWrapping.Wrap,
            }));
            return;
        }

        foreach (var app in apps)
            AppsPanel.Children.Add(BuildAppCard(app));
    }

    private IEnumerable<ExternalAppSpec> FilteredApps()
    {
        var q = _query.Trim().ToLowerInvariant();
        return ExternalApps.All
            .Where(a => string.IsNullOrEmpty(_category) || a.CategoryEn.Equals(_category, StringComparison.OrdinalIgnoreCase))
            .Where(a => q.Length == 0 || a.SearchHaystack.Contains(q))
            .OrderBy(a => a.CategoryEn)
            .ThenBy(a => a.NameEn);
    }

    private Border BuildAppCard(ExternalAppSpec app)
    {
        bool installed = ExternalAppService.IsInstalled(app);

        var root = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var icon = new FontIcon
        {
            Glyph = string.IsNullOrEmpty(app.Glyph) ? ((char)0xE71D).ToString() : app.Glyph,
            FontSize = 20,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
        };
        Grid.SetColumn(icon, 0);
        Grid.SetRow(icon, 0);
        Grid.SetRowSpan(icon, 2);
        root.Children.Add(icon);

        var text = new StackPanel { Spacing = 3 };
        text.Children.Add(new TextBlock
        {
            Text = $"{app.NameEn} · {app.NameZh}",
            FontWeight = FontWeights.SemiBold,
            FontSize = 15,
            TextWrapping = TextWrapping.Wrap,
        });
        text.Children.Add(new TextBlock
        {
            Text = P(app.DescriptionEn, app.DescriptionZh),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        });
        text.Children.Add(new TextBlock
        {
            Text = $"{app.CategoryEn} · {app.CategoryZh}    winget: {app.PrimaryWingetId}",
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        });
        Grid.SetColumn(text, 1);
        Grid.SetRow(text, 0);
        Grid.SetRowSpan(text, 2);
        root.Children.Add(text);

        var status = new Border
        {
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(999),
            VerticalAlignment = VerticalAlignment.Top,
            Background = (Brush)Application.Current.Resources[installed
                ? "SystemFillColorSuccessBackgroundBrush"
                : "CardStrokeColorDefaultBrush"],
            Child = new TextBlock
            {
                Text = installed ? P("Installed", "已安裝") : P("Not installed", "未安裝"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
            },
        };
        Grid.SetColumn(status, 2);
        Grid.SetRow(status, 0);
        root.Children.Add(status);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
        };

        if (installed)
        {
            var launch = new Button { Padding = new Thickness(10, 5, 10, 5) };
            launch.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Children =
                {
                    new FontIcon { Glyph = ((char)0xE768).ToString(), FontSize = 13 },
                    new TextBlock { Text = P("Launch", "啟動") },
                },
            };
            launch.Click += (_, _) => AppLauncherWindow.Show(app, launch: true);
            buttons.Children.Add(launch);
        }

        var open = new Button
        {
            Padding = new Thickness(10, 5, 10, 5),
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
        };
        open.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Children =
            {
                new FontIcon { Glyph = ((char)0xE8A7).ToString(), FontSize = 13 },
                new TextBlock { Text = installed ? P("Manage", "管理") : P("Install & launch", "安裝並啟動") },
            },
        };
        AutomationProperties.SetName(open, P($"Open the {app.NameEn} launcher popup", $"開啟 {app.NameZh} 啟動彈窗"));
        open.Click += (_, _) => AppLauncherWindow.Show(app);
        buttons.Children.Add(open);

        Grid.SetColumn(buttons, 2);
        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);

        var card = Card(root);
        AutomationProperties.SetName(card, $"{app.NameEn} · {app.NameZh} · {app.DescriptionEn}");
        return card;
    }

    private static Border Card(UIElement content) => new()
    {
        Padding = new Thickness(14),
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Child = content,
    };

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _query = sender.Text ?? "";
        RenderApps();
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        _query = args.QueryText ?? sender.Text ?? "";
        RenderApps();
    }

    private void CategoryBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_buildingCategories) return;
        _category = CategoryBox.SelectedIndex <= 0 ? "" : _categoryKeys[CategoryBox.SelectedIndex - 1];
        RenderApps();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _query = "";
        _category = "";
        SearchBox.Text = "";
        CategoryBox.SelectedIndex = 0;
        RenderApps();
    }
}
