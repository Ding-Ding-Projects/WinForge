using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 設定：語言、佈景主題、管理員、關於。
/// Settings: language, theme, administrator and about.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private bool _suppress;

    public SettingsPage()
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

        Root.Children.Add(new TextBlock
        {
            Text = Loc.I.Pick("Settings", "設定"),
            Style = (Style)Application.Current.Resources["TitleTextBlockStyle"],
        });

        Root.Children.Add(BuildBrandingCard());
        Root.Children.Add(BuildLanguageCard());
        Root.Children.Add(BuildThemeCard());
        Root.Children.Add(BuildBackupCard());
        Root.Children.Add(BuildAdminCard());
        Root.Children.Add(BuildAboutCard());
    }

    private Border BuildBackupCard()
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(Heading(
            Loc.I.Pick("Import / export settings", "匯入／匯出設定"),
            Loc.I.Pick("Save WinForge's settings to a file, or load them back.", "將 WinForge 嘅設定存做檔案，或者載返入嚟。")));

        var bar = new InfoBar { IsClosable = true, IsOpen = false };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        var export = new Button { Content = Loc.I.Pick("Export…", "匯出…") };
        export.Click += async (_, _) =>
        {
            try
            {
                var path = await FileDialogs.SaveFileAsync("winforge-settings", ".json");
                if (path is not null)
                {
                    SettingsStore.ExportTo(path);
                    Show(bar, InfoBarSeverity.Success, Loc.I.Pick("Exported.", "已匯出。"), path);
                }
            }
            catch (Exception ex) { Show(bar, InfoBarSeverity.Error, Loc.I.Pick("Export failed", "匯出失敗"), ex.Message); }
        };

        var import = new Button { Content = Loc.I.Pick("Import…", "匯入…") };
        import.Click += async (_, _) =>
        {
            try
            {
                var path = await FileDialogs.OpenFileAsync(".json");
                if (path is not null)
                {
                    int n = SettingsStore.ImportFrom(path);
                    App.ApplyThemeFromSettings();
                    Show(bar, InfoBarSeverity.Success,
                        Loc.I.Pick($"Imported {n} setting(s).", $"已匯入 {n} 項設定。"),
                        Loc.I.Pick("Restart WinForge to fully apply.", "重啟 WinForge 完全生效。"));
                }
            }
            catch (Exception ex) { Show(bar, InfoBarSeverity.Error, Loc.I.Pick("Import failed", "匯入失敗"), ex.Message); }
        };

        row.Children.Add(export);
        row.Children.Add(import);
        panel.Children.Add(row);
        panel.Children.Add(bar);
        return Card(panel);
    }

    private static void Show(InfoBar bar, InfoBarSeverity sev, string title, string msg)
    {
        bar.Severity = sev; bar.Title = title; bar.Message = msg; bar.IsOpen = true;
    }

    private Border BuildLanguageCard()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Heading(
            Loc.I.Pick("Language", "語言"),
            Loc.I.Pick("Show both languages, Cantonese only, or English only.",
                "顯示雙語、只顯示粵語，或者只顯示英文。")));

        _suppress = true;
        var radios = new RadioButtons();
        radios.Items.Add(Loc.I.Pick("Bilingual (English + Cantonese)", "雙語（英文 + 粵語）"));
        radios.Items.Add(Loc.I.Pick("Cantonese only", "只顯示粵語"));
        radios.Items.Add(Loc.I.Pick("English only", "English only"));
        radios.SelectedIndex = Loc.I.Language switch
        {
            AppLanguage.Cantonese => 1,
            AppLanguage.English => 2,
            _ => 0,
        };
        radios.SelectionChanged += (_, _) =>
        {
            if (_suppress) return;
            Loc.I.Language = radios.SelectedIndex switch
            {
                1 => AppLanguage.Cantonese,
                2 => AppLanguage.English,
                _ => AppLanguage.Bilingual,
            };
        };
        _suppress = false;
        panel.Children.Add(radios);
        return Card(panel);
    }

    private Border BuildBrandingCard()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Heading(
            Loc.I.Pick("App name (branding)", "應用程式名稱（品牌）"),
            Loc.I.Pick("Rename the app to your own name. Shown in the title bar and dashboard; your data folder and internal IDs stay unchanged.",
                "將 app 改成你自己嘅名。會喺標題列同概覽顯示；資料夾同內部識別碼維持不變。")));

        var enBox = new TextBox
        {
            Header = Loc.I.Pick("Name (English)", "名稱（英文）"),
            Text = BrandingService.NameEn,
            PlaceholderText = BrandingService.DefaultEn,
            MaxWidth = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var zhBox = new TextBox
        {
            Header = Loc.I.Pick("Name (Chinese)", "名稱（中文）"),
            Text = BrandingService.NameZh,
            PlaceholderText = BrandingService.DefaultZh,
            MaxWidth = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var status = new TextBlock { FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };

        var apply = new Button { Content = Loc.I.Pick("Apply name", "套用名稱"), Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        apply.Click += (_, _) =>
        {
            BrandingService.Set(enBox.Text, zhBox.Text);
            enBox.Text = BrandingService.NameEn;
            zhBox.Text = BrandingService.NameZh;
            status.Text = Loc.I.Pick($"Applied — now \"{BrandingService.NameEn} · {BrandingService.NameZh}\".",
                $"已套用 — 而家係「{BrandingService.NameEn} · {BrandingService.NameZh}」。");
        };
        var reset = new Button { Content = Loc.I.Pick("Reset to WinForge", "還原做 WinForge") };
        reset.Click += (_, _) =>
        {
            BrandingService.Reset();
            enBox.Text = BrandingService.NameEn;
            zhBox.Text = BrandingService.NameZh;
            status.Text = Loc.I.Pick("Reset to the default name.", "已還原做預設名稱。");
        };

        panel.Children.Add(enBox);
        panel.Children.Add(zhBox);
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { apply, reset } });
        panel.Children.Add(status);
        return Card(panel);
    }

    private Border BuildThemeCard()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Heading(
            Loc.I.Pick("App theme", "應用程式主題"),
            Loc.I.Pick("Light, dark or follow Windows.", "淺色、深色或者跟 Windows。")));

        var current = SettingsStore.Get("theme", "Default");
        var radios = new RadioButtons();
        radios.Items.Add(Loc.I.Pick("Use system setting", "跟系統設定"));
        radios.Items.Add(Loc.I.Pick("Light", "淺色"));
        radios.Items.Add(Loc.I.Pick("Dark", "深色"));
        radios.SelectedIndex = current switch { "Light" => 1, "Dark" => 2, _ => 0 };
        radios.SelectionChanged += (_, _) =>
        {
            var (key, theme) = radios.SelectedIndex switch
            {
                1 => ("Light", ElementTheme.Light),
                2 => ("Dark", ElementTheme.Dark),
                _ => ("Default", ElementTheme.Default),
            };
            SettingsStore.Set("theme", key);
            App.SetTheme(theme);
        };
        panel.Children.Add(radios);
        return Card(panel);
    }

    private Border BuildAdminCard()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Heading(
            Loc.I.Pick("Administrator rights", "管理員權限"),
            Loc.I.Pick("Needed for system-wide tweaks (HKLM, services, power).",
                "全系統調校需要（HKLM、服務、電源）。")));

        if (AdminHelper.IsElevated)
        {
            panel.Children.Add(new TextBlock
            {
                Text = Loc.I.Pick("✓ Running as administrator.", "✓ 正以管理員身分運行。"),
                Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"],
            });
        }
        else
        {
            var b = new Button { Content = Loc.I.Pick("Relaunch as administrator", "以管理員身分重新啟動") };
            b.Click += (_, _) =>
            {
                if (AdminHelper.RelaunchElevated())
                    Application.Current.Exit();
            };
            panel.Children.Add(b);
        }
        return Card(panel);
    }

    private Border BuildAboutCard()
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(Heading("WinForge · 視窗調校", null));
        panel.Children.Add(Muted(Loc.I.Pick(
            $"{TweakCatalog.Count} bilingual features for Windows 11.",
            $"{TweakCatalog.Count} 項 Windows 11 雙語功能。")));
        panel.Children.Add(Muted("Version 1.0.0"));
        panel.Children.Add(Muted(Loc.I.Pick(
            "Always review what a tweak does before applying it.",
            "套用之前，請睇清楚每項調校做乜。")));
        return Card(panel);
    }

    // ---- small builders ----
    private static StackPanel Heading(string title, string? subtitle)
    {
        var p = new StackPanel { Spacing = 1 };
        p.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 15 });
        if (!string.IsNullOrEmpty(subtitle))
            p.Children.Add(Muted(subtitle));
        return p;
    }

    private static TextBlock Muted(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        FontSize = 12,
        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
    };

    private static Border Card(UIElement content) => new()
    {
        Padding = new Thickness(16, 14, 16, 14),
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Child = content,
    };
}
