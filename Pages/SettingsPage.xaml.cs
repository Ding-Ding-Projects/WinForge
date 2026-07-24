using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
    private bool _subscriptionsActive;
    private TextBlock? _tonePreview;
    private Slider? _englishToneSlider;
    private Slider? _cantoneseToneSlider;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += OnActualThemeChanged;
    }

    private void OnLang(object? sender, EventArgs e) => Build();

    private void OnToneChanged(object? sender, EventArgs e)
    {
        if (_englishToneSlider is not null)
            _englishToneSlider.Value = FunnyLevelSettings.I.EnglishLevel;
        if (_cantoneseToneSlider is not null)
            _cantoneseToneSlider.Value = FunnyLevelSettings.I.CantoneseLevel;
        UpdateTonePreview();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        if (IsLoaded) Build();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_subscriptionsActive)
        {
            Loc.I.LanguageChanged += OnLang;
            FunnyLevelSettings.I.Changed += OnToneChanged;
            _subscriptionsActive = true;
        }

        Build();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_subscriptionsActive) return;
        Loc.I.LanguageChanged -= OnLang;
        FunnyLevelSettings.I.Changed -= OnToneChanged;
        _subscriptionsActive = false;
    }

    private void Build()
    {
        _tonePreview = null;
        _englishToneSlider = null;
        _cantoneseToneSlider = null;
        Root.Children.Clear();

        Root.Children.Add(new TextBlock
        {
            Text = Loc.I.Pick("Settings", "設定"),
            Style = (Style)Application.Current.Resources["TitleTextBlockStyle"],
        });

        Root.Children.Add(BuildLanguageCard());
        Root.Children.Add(BuildToneCard());
        Root.Children.Add(BuildBrandingCard());
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
                    FunnyLevelSettings.I.ReloadFromSettings();
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

    private Border BuildToneCard()
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(Heading(
            Loc.I.Pick("Funny level (tone)", "搞笑等級（語氣）"),
            Loc.I.Pick(
                "Choose English and Cantonese playfulness independently. Level 1 is fully serious; level 5 is the most playful.",
                "英文同粵語可以分開揀玩味程度。第 1 級完全正經；第 5 級最玩得。")));

        panel.Children.Add(BuildToneLevelControl(isEnglish: true));
        panel.Children.Add(BuildToneLevelControl(isEnglish: false));

        var previewPanel = new StackPanel { Spacing = 4 };
        previewPanel.Children.Add(new TextBlock
        {
            Text = Loc.I.Pick("Live non-safety preview", "非安全訊息即時預覽"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        _tonePreview = Muted(string.Empty);
        _tonePreview.FontSize = 13;
        AutomationProperties.SetName(_tonePreview, Loc.I.Pick("Funny-level live preview", "搞笑等級即時預覽"));
        AutomationProperties.SetLiveSetting(_tonePreview, AutomationLiveSetting.Polite);
        previewPanel.Children.Add(_tonePreview);

        var previewBorder = new Border
        {
            Padding = new Thickness(12),
            Background = SurfaceBrush(light: 0xFFF0F4F0, dark: 0xFF1A211B),
            CornerRadius = new CornerRadius(6),
            Child = previewPanel,
        };
        panel.Children.Add(previewBorder);
        panel.Children.Add(Muted(Loc.I.Pick(
            "Only explicitly authored, non-safety copy changes. Errors, security, destructive actions, and accessibility wording stay clear and exact.",
            "只會改明確寫好嘅非安全訊息；錯誤、安全、破壞性操作同無障礙文字永遠保持清楚準確。")));

        UpdateTonePreview();
        return Card(panel);
    }

    private StackPanel BuildToneLevelControl(bool isEnglish)
    {
        var settings = FunnyLevelSettings.I;
        var current = isEnglish ? settings.EnglishLevel : settings.CantoneseLevel;
        var language = isEnglish
            ? Loc.I.Pick("English", "英文")
            : Loc.I.Pick("Cantonese", "粵語");
        var value = Muted(LevelValueText(isEnglish, current));
        var slider = new Slider
        {
            Minimum = FunnyLevelSettings.MinimumLevel,
            Maximum = FunnyLevelSettings.MaximumLevel,
            Value = current,
            StepFrequency = 1,
            SnapsTo = SliderSnapsTo.StepValues,
            IsThumbToolTipEnabled = true,
            MinHeight = 44,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(slider, isEnglish
            ? Loc.I.Pick("English funny level, 1 serious to 5 most playful", "英文搞笑等級，由第 1 級正經到第 5 級最玩得")
            : Loc.I.Pick("Cantonese funny level, 1 serious to 5 most playful", "粵語搞笑等級，由第 1 級正經到第 5 級最玩得"));
        AutomationProperties.SetHelpText(slider, Loc.I.Pick(
            "Use Left and Right Arrow to choose an exact level from 1 through 5.",
            "用向左同向右方向鍵揀 1 至 5 嘅準確等級。"));
        ToolTipService.SetToolTip(slider, Loc.I.Pick(
            "1 = fully serious · 5 = most playful",
            "1 = 完全正經 · 5 = 最玩得"));

        if (isEnglish) _englishToneSlider = slider;
        else _cantoneseToneSlider = slider;

        slider.ValueChanged += (_, args) =>
        {
            var level = (int)Math.Round(args.NewValue, MidpointRounding.AwayFromZero);
            if (isEnglish) settings.EnglishLevel = level;
            else settings.CantoneseLevel = level;
            value.Text = LevelValueText(isEnglish, level);
        };

        return new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = language,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                },
                slider,
                value,
                Muted(Loc.I.Pick("1 = fully serious · 5 = most playful", "1 = 完全正經 · 5 = 最玩得")),
            },
        };
    }

    private static string LevelValueText(bool isEnglish, int level) => isEnglish
        ? Loc.I.Pick($"English funny level: {level} of 5.", $"英文搞笑等級：5 級入面第 {level} 級。")
        : Loc.I.Pick($"Cantonese funny level: {level} of 5.", $"粵語搞笑等級：5 級入面第 {level} 級。");

    private void UpdateTonePreview()
    {
        if (_tonePreview is null) return;
        _tonePreview.Text = FunnyLevelSettings.I.Pick(PlayfulCopy.DashboardHero, Loc.I.Language);
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

        var status = new TextBlock { FontSize = 12, Foreground = SecondaryTextBrush() };

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
                Foreground = SurfaceBrush(light: 0xFF0F6B3A, dark: 0xFF54E07E),
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
            $"{FeatureCountService.FullFeatureCount} bilingual features for Windows 11 ({FeatureCountService.ModuleCount} modules + {FeatureCountService.TweakFeatureCount} tweaks and ops).",
            $"{FeatureCountService.FullFeatureCount} 項 Windows 11 雙語功能（{FeatureCountService.ModuleCount} 個模組 + {FeatureCountService.TweakFeatureCount} 項調校／操作）。")));
        panel.Children.Add(Muted("Version 1.0.0"));
        panel.Children.Add(Muted(Loc.I.Pick(
            "Always review what a tweak does before applying it.",
            "套用之前，請睇清楚每項調校做乜。")));
        return Card(panel);
    }

    // ---- small builders ----
    private StackPanel Heading(string title, string? subtitle)
    {
        var p = new StackPanel { Spacing = 1 };
        p.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 15 });
        if (!string.IsNullOrEmpty(subtitle))
            p.Children.Add(Muted(subtitle));
        return p;
    }

    private TextBlock Muted(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        FontSize = 12,
        Foreground = SecondaryTextBrush(),
    };

    private Border Card(UIElement content) => new()
    {
        Padding = new Thickness(16, 14, 16, 14),
        Background = SurfaceBrush(light: 0xFFF7F9F7, dark: 0xFF131814),
        BorderBrush = SurfaceBrush(light: 0x330F6B3A, dark: 0x24FFFFFF),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Child = content,
    };

    private SolidColorBrush SurfaceBrush(uint light, uint dark)
    {
        var argb = ActualTheme == ElementTheme.Light ? light : dark;
        return new SolidColorBrush(Windows.UI.Color.FromArgb(
            (byte)(argb >> 24),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb));
    }

    private SolidColorBrush SecondaryTextBrush() =>
        SurfaceBrush(light: 0xFF3D5442, dark: 0xFFBFCBBF);
}
