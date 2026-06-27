using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using WinForge.Services;

namespace WinForge.Pages;

public sealed partial class LicensesPage : Page
{
    private readonly List<string> _categoryKeys = LicenseCatalogService.CategoryKeys.ToList();
    private string _query = "";
    private string _category = "";
    private bool _buildingCategories;

    public LicensesPage()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) =>
        {
            FitContentWidth();
            RenderText();
            BuildCategories();
            RenderNotices();
        };
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RenderText();
        BuildCategories();
        RenderNotices();
    }

    private void RenderText()
    {
        HeaderTitle.Text = "Licenses & Source · 授權與原始碼";
        HeaderBlurb.Text = P(
            "WinForge is open source. This page lists bundled libraries and open-source or source-available upstream projects used as in-app modules, libraries, integrations or behavioral references. GPL, AGPL, LGPL, MPL and source-available entries are called out here so feature pages can stay transparent as they grow.",
            "WinForge 係開源。呢頁列出已捆綁程式庫，以及作為 app 內模組、程式庫、整合或行為參考嘅開源／source-available 上游項目。GPL、AGPL、LGPL、MPL 同 source-available 項目會喺呢度清楚標示，等功能頁越做越完整時都保持透明。");
        SearchBox.PlaceholderText = P("Search names, licenses, modules or source URLs…", "搜尋名稱、授權、模組或來源 URL…");
        ClearLabel.Text = P("Clear", "清除");
        CopyleftOnlySwitch.OnContent = "GPL/AGPL/source";
        CopyleftOnlySwitch.OffContent = P("All", "全部");
        CopyleftOnlySwitch.Header = P("Focus", "聚焦");
    }

    private void BuildCategories()
    {
        _buildingCategories = true;
        try
        {
            CategoryBox.Items.Clear();
            CategoryBox.Items.Add(P("All notices", "全部聲明"));
            foreach (var key in _categoryKeys)
            {
                var first = LicenseCatalogService.Notices.First(n => n.CategoryEn == key);
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

    private void RenderNotices()
    {
        NoticesPanel.Children.Clear();
        var notices = LicenseCatalogService.Search(_query, _category, CopyleftOnlySwitch.IsOn).ToList();
        var flagged = LicenseCatalogService.Notices.Count(n => n.IsCopyleftOrSourceAvailable);
        SummaryText.Text = P(
            $"{notices.Count} shown · {LicenseCatalogService.Notices.Length} notices · {flagged} GPL/AGPL/LGPL/MPL/source-available entries",
            $"顯示 {notices.Count} 個 · 共 {LicenseCatalogService.Notices.Length} 個聲明 · {flagged} 個 GPL/AGPL/LGPL/MPL/source-available 項目");

        if (notices.Count == 0)
        {
            NoticesPanel.Children.Add(Card(new TextBlock
            {
                Text = P("No license notices match the current filters.", "目前篩選無符合嘅授權聲明。"),
                TextWrapping = TextWrapping.Wrap,
            }));
            return;
        }

        foreach (var notice in notices)
            NoticesPanel.Children.Add(BuildNoticeCard(notice));
    }

    private Border BuildNoticeCard(LicenseNotice notice)
    {
        var root = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleStack = new StackPanel { Spacing = 4 };
        titleStack.Children.Add(new TextBlock
        {
            Text = notice.Name,
            FontWeight = FontWeights.SemiBold,
            FontSize = 16,
            TextWrapping = TextWrapping.Wrap,
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = $"{notice.CategoryEn} · {notice.CategoryZh}    {P("License", "授權")}: {notice.License}",
            Foreground = Brush("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        });
        Grid.SetColumn(titleStack, 0);
        Grid.SetRow(titleStack, 0);
        root.Children.Add(titleStack);

        var badge = new Border
        {
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(999),
            Background = Brush(notice.IsCopyleftOrSourceAvailable ? "SystemFillColorCautionBackgroundBrush" : "SystemFillColorSuccessBackgroundBrush"),
            Child = new TextBlock
            {
                Text = notice.IsCopyleftOrSourceAvailable ? "GPL/AGPL/source" : P("Permissive/runtime", "寬鬆／執行階段"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
            },
        };
        Grid.SetColumn(badge, 1);
        Grid.SetRow(badge, 0);
        root.Children.Add(badge);

        var body = new StackPanel { Spacing = 5 };
        if (!string.IsNullOrWhiteSpace(notice.UseEn) || !string.IsNullOrWhiteSpace(notice.UseZh))
            body.Children.Add(Line(P("Use", "用途"), $"{notice.UseEn} · {notice.UseZh}"));
        if (!string.IsNullOrWhiteSpace(notice.ModuleTag))
            body.Children.Add(Line(P("In app", "App 內"), notice.ModuleTag));
        if (!string.IsNullOrWhiteSpace(notice.ObligationEn) || !string.IsNullOrWhiteSpace(notice.ObligationZh))
            body.Children.Add(Line(P("Notice", "聲明"), $"{notice.ObligationEn} · {notice.ObligationZh}"));
        body.Children.Add(Line(P("Source", "原始碼"), notice.SourceUrl));
        if (!string.IsNullOrWhiteSpace(notice.LicenseUrl))
            body.Children.Add(Line(P("License text", "授權全文"), notice.LicenseUrl));
        Grid.SetColumn(body, 0);
        Grid.SetRow(body, 1);
        Grid.SetColumnSpan(body, 2);
        root.Children.Add(body);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(ActionButton(P("Copy source", "複製來源"), "\uE8C8", () => Copy(notice.SourceUrl)));
        if (!string.IsNullOrWhiteSpace(notice.LicenseUrl))
            actions.Children.Add(ActionButton(P("Copy license", "複製授權"), "\uE8D7", () => Copy(notice.LicenseUrl)));
        if (!string.IsNullOrWhiteSpace(notice.ModuleTag))
            actions.Children.Add(ActionButton(P("Open module", "開啟模組"), "\uE8A7", () => Navigator.GoToModule?.Invoke(notice.ModuleTag)));
        Grid.SetColumn(actions, 0);
        Grid.SetRow(actions, 2);
        Grid.SetColumnSpan(actions, 2);
        root.Children.Add(actions);

        var card = Card(root);
        AutomationProperties.SetName(card, $"{notice.Name} {notice.License} {notice.SourceUrl}");
        return card;
    }

    private static Grid Line(string label, string value)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = Brush("TextFillColorTertiaryBrush"),
        };
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);
        var right = new TextBlock
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }

    private static Button ActionButton(string text, string glyph, Action action)
    {
        var button = new Button
        {
            Padding = new Thickness(10, 5, 10, 5),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Children =
                {
                    new FontIcon { Glyph = glyph, FontSize = 13 },
                    new TextBlock { Text = text },
                },
            },
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static void Copy(string text)
    {
        var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dp.SetText(text);
        Clipboard.SetContent(dp);
        Clipboard.Flush();
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    private static Border Card(UIElement content) => new()
    {
        Padding = new Thickness(14),
        Background = Brush("CardBackgroundFillColorDefaultBrush"),
        BorderBrush = Brush("CardStrokeColorDefaultBrush"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Child = content,
    };

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _query = sender.Text ?? "";
        RenderNotices();
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        _query = args.QueryText ?? sender.Text ?? "";
        RenderNotices();
    }

    private void CategoryBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_buildingCategories) return;
        _category = CategoryBox.SelectedIndex <= 0 ? "" : _categoryKeys[CategoryBox.SelectedIndex - 1];
        RenderNotices();
    }

    private void CopyleftOnly_Toggled(object sender, RoutedEventArgs e) => RenderNotices();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _query = "";
        _category = "";
        SearchBox.Text = "";
        CopyleftOnlySwitch.IsOn = false;
        CategoryBox.SelectedIndex = 0;
        RenderNotices();
    }

    private void RootScroll_SizeChanged(object sender, SizeChangedEventArgs e) => FitContentWidth();

    private void FitContentWidth()
    {
        var available = RootScroll.ActualWidth - RootScroll.Padding.Left - RootScroll.Padding.Right;
        if (RootScroll.XamlRoot is { } root)
        {
            try
            {
                var offset = RootScroll.TransformToVisual(null).TransformPoint(new Point(0, 0));
                available = Math.Min(available, root.Size.Width - offset.X - RootScroll.Padding.Left - RootScroll.Padding.Right);
            }
            catch
            {
                // Some design-time or early-load paths cannot transform yet; ActualWidth is still useful there.
            }
        }

        available = Math.Max(320, available);
        RootStack.Width = Math.Min(560, available);
    }
}
