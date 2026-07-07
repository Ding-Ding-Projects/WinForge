using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Native OSS clone hub: a map of open-source-inspired features that are implemented as C# tabs in WinForge.
/// This page deliberately contains no installer or external-launch action.
/// </summary>
public sealed partial class OpenSourceAppHubModule : Page
{
    private readonly List<string> _categoryKeys = OpenSourceAppHubService.CategoryKeys.ToList();
    private string _query = "";
    private string _category = "";
    private bool _buildingCategories;

    public OpenSourceAppHubModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) =>
        {
            RenderText();
            BuildCategories();
            RenderApps();
        };
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RenderText();
        BuildCategories();
        RenderApps();
    }

    private void RenderText()
    {
        Header.Title = "Native OSS Clones · 開源原生分頁";
        HeaderBlurb.Text = P(
            "A native-only map of useful open-source app ideas that have been remade as C# tabs inside WinForge. These entries do not install or launch upstream apps; each one routes to an in-app module backed by managed code, Windows APIs or an embedded library. GPL, AGPL and source-available notices are tracked on the Licenses page.",
            "呢個係只限原生嘅開源 app 概念地圖：有用嘅開源應用程式想法已經重製成 WinForge 入面嘅 C# 分頁。呢啲項目唔會安裝或啟動上游 app；每一項都會去到一個由受控程式碼、Windows API 或內嵌程式庫支援嘅 app 內模組。GPL、AGPL 同 source-available 聲明會喺授權頁追蹤。");
        NativeOnlyLabel.Text = P("Native only", "只限原生");
        ClearLabel.Text = P("Clear", "清除");
        SearchBox.PlaceholderText = P("Search native clones, source inspirations or module aliases…",
            "搜尋原生複製、來源靈感或模組別名…");
        ToolTipService.SetToolTip(NativeOnlyMarker, P("This page lists in-app native modules only.", "此頁只列出 app 內原生模組。"));
    }

    private void BuildCategories()
    {
        _buildingCategories = true;
        try
        {
            CategoryBox.Items.Clear();
            CategoryBox.Items.Add(P("All native clones", "全部原生分頁"));
            foreach (var key in _categoryKeys)
            {
                var first = OpenSourceAppHubService.Catalog.First(a => a.CategoryEn == key);
                CategoryBox.Items.Add($"{first.CategoryEn} · {first.CategoryZh}");
            }

            var idx = string.IsNullOrEmpty(_category) ? 0 : _categoryKeys.FindIndex(c => c.Equals(_category, StringComparison.OrdinalIgnoreCase)) + 1;
            CategoryBox.SelectedIndex = idx <= 0 ? 0 : idx;
        }
        finally
        {
            _buildingCategories = false;
        }
    }

    private void RenderApps()
    {
        AppsPanel.Children.Clear();
        var apps = FilteredApps().ToList();
        SummaryText.Text = P(
            $"{apps.Count} shown · {OpenSourceAppHubService.Catalog.Length} native open-source-inspired tabs catalogued",
            $"顯示 {apps.Count} 個 · 已登記 {OpenSourceAppHubService.Catalog.Length} 個開源靈感原生分頁");

        AppsPanel.Children.Add(BuildLicenseNoticeCard());

        if (apps.Count == 0)
        {
            AppsPanel.Children.Add(Card(new TextBlock
            {
                Text = P("No native clone entries match the current filters.", "目前篩選無符合嘅原生複製項目。"),
                TextWrapping = TextWrapping.Wrap,
            }));
            return;
        }

        foreach (var app in apps)
            AppsPanel.Children.Add(BuildAppCard(app));
    }

    private IEnumerable<NativeOssCloneInfo> FilteredApps()
    {
        var q = _query.Trim().ToLowerInvariant();
        return OpenSourceAppHubService.Catalog
            .Where(a => string.IsNullOrEmpty(_category) || a.CategoryEn.Equals(_category, StringComparison.OrdinalIgnoreCase))
            .Where(a => q.Length == 0 || a.SearchHaystack.Contains(q))
            .OrderBy(a => a.CategoryEn)
            .ThenBy(a => a.NameEn);
    }

    private Border BuildLicenseNoticeCard()
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 3 };
        text.Children.Add(new TextBlock
        {
            Text = P("Source and license notices", "原始碼與授權聲明"),
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        text.Children.Add(new TextBlock
        {
            Text = P("GPL, AGPL, LGPL, MPL and source-available upstreams are called out in-app with copyable source/license links.",
                "GPL、AGPL、LGPL、MPL 同 source-available 上游會喺 app 內列明，並提供可複製嘅來源／授權連結。"),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        });
        Grid.SetColumn(text, 0);
        row.Children.Add(text);

        var button = new Button
        {
            VerticalAlignment = VerticalAlignment.Center,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = "\uE8D7", FontSize = 13 },
                    new TextBlock { Text = P("Licenses", "授權") },
                },
            },
        };
        button.Click += (_, _) => Navigator.GoToPage?.Invoke("licenses");
        Grid.SetColumn(button, 1);
        row.Children.Add(button);

        return Card(row);
    }

    private Border BuildAppCard(NativeOssCloneInfo app)
    {
        var root = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var icon = new FontIcon
        {
            Glyph = "\uE71D",
            FontSize = 18,
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
            Text = $"{app.DescriptionEn} · {app.DescriptionZh}",
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        });
        text.Children.Add(new TextBlock
        {
            Text = $"{P("Inspired by", "靈感來自")}: {app.InspiredBy}    {app.CategoryEn} · {app.CategoryZh}    --page {app.PageAlias}",
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        });
        text.Children.Add(new TextBlock
        {
            Text = $"{app.ImplementationEn} · {app.ImplementationZh}",
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        });
        Grid.SetColumn(text, 1);
        Grid.SetRow(text, 0);
        Grid.SetRowSpan(text, 2);
        root.Children.Add(text);

        var status = new Border
        {
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(999),
            Background = (Brush)Application.Current.Resources["SystemFillColorSuccessBackgroundBrush"],
            Child = new TextBlock
            {
                Text = $"{app.StatusEn} · {app.StatusZh}",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
            },
        };
        Grid.SetColumn(status, 2);
        Grid.SetRow(status, 0);
        root.Children.Add(status);

        var openBtn = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(10, 5, 10, 5),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Children =
                {
                    new FontIcon { Glyph = "\uE8A7", FontSize = 13 },
                    new TextBlock { Text = P("Open tab", "開啟分頁") },
                },
            },
        };
        AutomationProperties.SetName(openBtn, P($"Open {app.NameEn} in WinForge", $"喺 WinForge 開啟 {app.NameZh}"));
        ToolTipService.SetToolTip(openBtn, P($"Open {app.NameEn} as an in-app tab", $"以 app 內分頁開啟 {app.NameZh}"));
        openBtn.Click += (_, _) => Navigator.GoToModule?.Invoke(app.ModuleTag);
        Grid.SetColumn(openBtn, 2);
        Grid.SetRow(openBtn, 1);
        root.Children.Add(openBtn);

        var card = Card(root);
        AutomationProperties.SetName(card, $"{app.NameEn} · {app.NameZh} · {app.DescriptionEn} · {app.DescriptionZh}");
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
