using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 關於頁：雙語介紹、功能數目、免責聲明、原始碼連結。
/// About: bilingual intro, feature count, disclaimer and source link.
/// </summary>
public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Build();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Build();

    private void Build()
    {
        Root.Children.Clear();
        Root.MaxWidth = 760;

        Root.Children.Add(new TextBlock
        {
            Text = "WinForge · 視窗鑄造",
            Style = (Style)Application.Current.Resources["TitleTextBlockStyle"],
        });
        Root.Children.Add(new TextBlock
        {
            Text = $"Windows 11 · {FeatureCountService.FullFeatureCount} {Loc.I.Pick("features", "項功能")} · {FeatureCountService.ModuleCount} {Loc.I.Pick("modules", "個模組")} · {FeatureCountService.CategoryCount} {Loc.I.Pick("categories", "個分類")} · WinUI 3",
            Foreground = ReadableTextBrush(),
            Opacity = 0.72,
        });

        Root.Children.Add(Para(
            "WinForge is an all-in-one control center for Windows 11. Every feature is shown in both " +
            "English and Cantonese (粵語), and every toggle and action truly changes the system — " +
            "registry keys, power plans, network stack, privacy settings, cleanup and more.",
            "WinForge 係一個 Windows 11 全方位控制中心。每一項功能都同時用英文同粵語顯示，" +
            "而且每個開關同動作都會真正改到系統 — 登錄檔、電源計劃、網絡堆疊、私隱設定、清理等等。"));

        Root.Children.Add(Heading(Loc.I.Pick("Safety", "安全"), null));
        Root.Children.Add(Para(
            "These tweaks modify real Windows settings. Changes are reversible where possible, but please " +
            "read each description first. Some require administrator rights or a restart to take effect.",
            "呢啲調校會改到真實嘅 Windows 設定。可逆嘅都做咗可逆，但請先睇清楚每段說明。" +
            "部分需要管理員權限，或者要重啟先生效。"));

        Root.Children.Add(Heading(Loc.I.Pick("Source code", "原始碼"), null));
        var link = new Button
        {
            Content = "github.com/Ding-Ding-Projects/WinForge",
            Padding = new Thickness(0),
        };
        link.Click += (_, _) => CopyText("https://github.com/Ding-Ding-Projects/WinForge");
        Root.Children.Add(link);

        var licenses = new Button
        {
            Content = Loc.I.Pick("Licenses & source notices · 授權與原始碼聲明", "授權與原始碼聲明 · Licenses & source notices"),
        };
        licenses.Click += (_, _) => Navigator.GoToPage?.Invoke("licenses");
        Root.Children.Add(licenses);

        Root.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            Text = Loc.I.Pick("Version 1.0.0  ·  MIT License  ·  Built with .NET + WinUI 3", "版本 1.0.0  ·  MIT 授權  ·  用 .NET + WinUI 3 整"),
            FontSize = 12,
            Foreground = ReadableTextBrush(),
            Opacity = 0.72,
        });

        BuildOfflineChangelog();
    }

    private void BuildOfflineChangelog()
    {
        Root.Children.Add(Heading(
            Loc.I.Pick("Offline changelog", "離線變更紀錄"),
            Loc.I.Pick("Every entry is bundled with the app. Search is plain text first; the adjacent builder enables the full .NET regex engine locally.",
                "每項紀錄都隨 app 一齊打包。搜尋預設係純文字；隔籬砌法可以喺本機開完整 .NET 正則。")));

        ChangelogService.LoadResult loaded = ChangelogService.Load();
        var search = new SearchPatternBox
        {
            PlaceholderText = Loc.I.Pick("Search changelog text", "搜尋變更紀錄文字"),
            AutomationName = Loc.I.Pick("Changelog search", "變更紀錄搜尋"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var from = new CalendarDatePicker
        {
            Header = Loc.I.Pick("From date (optional)", "開始日期（可選）"),
            PlaceholderText = Loc.I.Pick("Any date", "任何日期"),
            MinWidth = 180,
        };
        var to = new CalendarDatePicker
        {
            Header = Loc.I.Pick("To date (optional)", "結束日期（可選）"),
            PlaceholderText = Loc.I.Pick("Any date", "任何日期"),
            MinWidth = 180,
        };
        var dateRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        dateRow.Children.Add(from);
        dateRow.Children.Add(to);

        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        var entries = new StackPanel { Spacing = 10 };
        var export = new Button { Content = Loc.I.Pick("Export filtered changelog…", "匯出已篩選變更紀錄…"), MinHeight = 40 };

        IReadOnlyList<ChangelogService.Entry> filtered = Array.Empty<ChangelogService.Entry>();
        void Render()
        {
            DateOnly? start = from.Date is DateTimeOffset startDate ? DateOnly.FromDateTime(startDate.Date) : null;
            DateOnly? end = to.Date is DateTimeOffset endDate ? DateOnly.FromDateTime(endDate.Date) : null;
            if (start is not null && end is not null && start > end)
            {
                status.Text = Loc.I.Pick("The start date must not be after the end date.", "開始日期唔可以遲過結束日期。");
                entries.Children.Clear();
                filtered = Array.Empty<ChangelogService.Entry>();
                return;
            }

            filtered = ChangelogService.Filter(loaded.Entries, search.Spec, start, end, out string? error);
            status.Text = loaded.Error is not null
                ? Loc.I.Pick($"Offline changelog unavailable: {loaded.Error}", $"離線變更紀錄不可用：{loaded.Error}")
                : error is not null
                    ? Loc.I.Pick($"Search error: {error}", $"搜尋錯誤：{error}")
                    : Loc.I.Pick($"Showing {filtered.Count} of {loaded.Entries.Count} entries. Entries without a recorded date are excluded when a date filter is active.",
                        $"顯示緊 {filtered.Count} / {loaded.Entries.Count} 項。開啟日期篩選時，冇記錄日期嘅項目會排除。 ");
            entries.Children.Clear();
            foreach (ChangelogService.Entry entry in filtered)
            {
                var card = new Border
                {
                    Padding = new Thickness(14),
                    Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                };
                var panel = new StackPanel { Spacing = 6 };
                panel.Children.Add(new TextBlock
                {
                    Text = entry.Heading,
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                });
                panel.Children.Add(new TextBlock
                {
                    Text = entry.Date is null
                        ? Loc.I.Pick("Release date: not recorded", "發佈日期：未有記錄")
                        : Loc.I.Pick($"Release date: {entry.Date:yyyy-MM-dd}", $"發佈日期：{entry.Date:yyyy-MM-dd}"),
                    FontSize = 12,
                    Foreground = ReadableTextBrush(),
                });
                panel.Children.Add(new TextBlock
                {
                    Text = entry.PlainBody,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                });
                if (entry.CommitUrl is not null)
                {
                    var commit = new Button { Content = Loc.I.Pick($"Commit {entry.CommitSha}", $"Commit {entry.CommitSha}"), MinHeight = 36 };
                    commit.Click += (_, _) => CopyText(entry.CommitUrl);
                    ToolTipService.SetToolTip(commit, entry.CommitUrl);
                    panel.Children.Add(commit);
                }
                else
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = Loc.I.Pick("Commit link: not recorded in the source changelog", "Commit 連結：來源變更紀錄未有記錄"),
                        FontSize = 12,
                        Foreground = ReadableTextBrush(),
                    });
                }
                card.Child = panel;
                entries.Children.Add(card);
            }
        }

        search.PatternChanged += (_, _) => Render();
        from.DateChanged += (_, _) => Render();
        to.DateChanged += (_, _) => Render();
        export.Click += async (_, _) =>
        {
            string? path = await FileDialogs.SaveFileAsync("winforge-changelog", ".md", ".txt");
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                DateOnly? start = from.Date is DateTimeOffset startDate ? DateOnly.FromDateTime(startDate.Date) : null;
                DateOnly? end = to.Date is DateTimeOffset endDate ? DateOnly.FromDateTime(endDate.Date) : null;
                File.WriteAllText(path, ChangelogService.ExportMarkdown(filtered, start, end));
                AppNotificationService.Publish(new AppNoticeDraft(
                    "Changelog exported",
                    "變更紀錄已匯出",
                    path,
                    path,
                    AppNoticeSeverity.Success));
            }
            catch (Exception ex)
            {
                AppNotificationService.Publish(new AppNoticeDraft(
                    "Changelog export failed",
                    "變更紀錄匯出失敗",
                    ex.Message,
                    ex.Message,
                    AppNoticeSeverity.Error));
            }
        };

        Root.Children.Add(search);
        Root.Children.Add(dateRow);
        Root.Children.Add(export);
        Root.Children.Add(status);
        Root.Children.Add(entries);
        Render();
    }

    private static StackPanel Heading(string title, string? subtitle)
    {
        var p = new StackPanel { Spacing = 1, Margin = new Thickness(0, 6, 0, 0) };
        p.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 16 });
        if (!string.IsNullOrEmpty(subtitle))
            p.Children.Add(new TextBlock { Text = subtitle, FontSize = 12, Opacity = 0.7 });
        return p;
    }

    private StackPanel Para(string en, string zh)
    {
        var p = new StackPanel { Spacing = 4 };
        p.Children.Add(new TextBlock { Text = en, TextWrapping = TextWrapping.Wrap });
        p.Children.Add(new TextBlock
        {
            Text = zh,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ReadableTextBrush(),
            Opacity = 0.78,
        });
        return p;
    }

    private Brush ReadableTextBrush()
    {
        Color color = Root.ActualTheme == ElementTheme.Light
            ? Color.FromArgb(255, 23, 38, 27)
            : Color.FromArgb(255, 237, 243, 237);
        return new SolidColorBrush(color);
    }

    private static void CopyText(string text)
    {
        var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dp.SetText(text);
        Clipboard.SetContent(dp);
        Clipboard.Flush();
    }
}
