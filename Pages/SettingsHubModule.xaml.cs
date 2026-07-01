using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 設定與控制台 · Settings &amp; Control Panel — two ways: change settings <b>in-app</b> (the app's own
/// live, current-state-reading tweak catalog, grouped into categories) or <b>open in Windows</b> (every
/// ms-settings: page and Control Panel applet, grouped &amp; searchable). Bilingual.
/// </summary>
public sealed partial class SettingsHubModule : Page
{
    private int _mode; // 0 = in-app settings, 1 = open in Windows

    private static readonly (string en, string zh)[] Cats =
    {
        ("System", "系統"), ("Devices", "裝置"), ("Network", "網絡"), ("Personalization", "個人化"),
        ("Apps", "應用程式"), ("Accounts", "帳戶"), ("Time & Language", "時間與語言"), ("Gaming", "遊戲"),
        ("Accessibility", "協助工具"), ("Privacy & Security", "私隱與安全"), ("Update & Recovery", "更新與復原"),
        ("Control Panel", "控制台"), ("Other", "其他"),
    };

    public SettingsHubModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Unloaded += OnUnloaded;
        Loaded += (_, _) => { Render(); BuildModeCombo(); ModeCombo.SelectedIndex = 0; Apply(""); };
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Render();
        BuildModeCombo();
        Apply(FilterBox.Text ?? "");
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
        Unloaded -= OnUnloaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Settings & Control Panel · 設定與控制台";
        HeaderBlurb.Text = P(
            "Change Windows settings right here — the in-app catalog reads each setting's current value before showing it — or open any Settings page / Control Panel applet, grouped and searchable.",
            "喺呢度直接改 Windows 設定 — 應用程式內目錄顯示前會先讀取每項設定嘅目前值 — 又或者打開任何設定頁／控制台 applet，已分類同可搜尋。");
        FilterBox.PlaceholderText = P("Search settings…", "搜尋設定…");
    }

    private void BuildModeCombo()
    {
        int sel = ModeCombo.SelectedIndex;
        ModeCombo.Items.Clear();
        ModeCombo.Items.Add(P("Change here (in-app)", "喺度改（應用內）"));
        ModeCombo.Items.Add(P("Open in Windows", "喺 Windows 打開"));
        ModeCombo.SelectedIndex = sel < 0 ? 0 : sel;
    }

    private void Mode_Changed(object sender, SelectionChangedEventArgs e)
    {
        _mode = ModeCombo.SelectedIndex < 0 ? 0 : ModeCombo.SelectedIndex;
        Apply(FilterBox.Text ?? "");
    }

    private void Filter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) Apply(sender.Text ?? "");
    }

    private void Apply(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) { BuildSections(); return; }
        if (_mode == 0) BuildTweakSearch(filter.Trim());
        else BuildLauncherSearch(filter.Trim());
    }

    // ===== sectioned (no filter) =====

    private void BuildSections()
    {
        Sections.Children.Clear();
        if (_mode == 0)
        {
            int total = 0;
            foreach (var cat in Categories.All)
            {
                int count = TweakCatalog.CountFor(cat);
                if (count == 0) continue;
                total += count;
                Sections.Children.Add(TweakCategoryExpander(cat, count));
            }
            CountText.Text = P($"{total} settings", $"{total} 項設定");
        }
        else
        {
            var groups = SettingsHubCatalog.All
                .GroupBy(CatIndex)
                .OrderBy(g => g.Key);
            foreach (var g in groups)
            {
                var (en, zh) = Cats[g.Key];
                Sections.Children.Add(LauncherGroupExpander(en, zh, g.ToList()));
            }
            CountText.Text = P($"{SettingsHubCatalog.All.Count} items", $"{SettingsHubCatalog.All.Count} 個項目");
        }
    }

    private Expander TweakCategoryExpander(AppCategory cat, int count)
    {
        var inner = new StackPanel { Spacing = 2 };
        bool built = false;
        var exp = new Expander
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Header = GroupHeader(cat.Glyph, $"{cat.Name.En} · {cat.Name.Zh}", count),
            Content = inner,
        };
        exp.Expanding += (_, _) =>
        {
            if (built) return;
            built = true;
            // One control-row list per category — reads each setting's current value as it renders.
            var list = new ControlRowList();
            list.SetTweaks(TweakCatalog.ByCategory(cat));
            inner.Children.Add(list);
        };
        return exp;
    }

    private Expander LauncherGroupExpander(string en, string zh, List<SettingsHubEntry> entries)
    {
        var inner = new StackPanel { Spacing = 6 };
        foreach (var entry in entries) inner.Children.Add(LauncherCard(entry));
        return new Expander
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Header = GroupHeader("", $"{en} · {zh}", entries.Count),
            Content = inner,
        };
    }

    private static StackPanel GroupHeader(string glyph, string text, int count)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (!string.IsNullOrEmpty(glyph)) sp.Children.Add(new FontIcon { Glyph = glyph, FontSize = 16 });
        sp.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        sp.Children.Add(new TextBlock
        {
            Text = count.ToString(), FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        });
        return sp;
    }

    // ===== search (flat) =====

    private void BuildTweakSearch(string filter)
    {
        Sections.Children.Clear();
        var f = filter.ToLowerInvariant();
        var hits = TweakCatalog.All.Where(t => t.SearchHaystack.Contains(f)).Take(300).ToList();
        CountText.Text = P($"{hits.Count} settings", $"{hits.Count} 項設定");
        if (hits.Count == 0) { Sections.Children.Add(EmptyNote()); return; }
        // A single control-row list holds the whole flat, filtered result set.
        var list = new ControlRowList();
        list.SetTweaks(hits);
        Sections.Children.Add(list);
    }

    private void BuildLauncherSearch(string filter)
    {
        Sections.Children.Clear();
        var hits = SettingsHubCatalog.Search(filter).ToList();
        CountText.Text = P($"{hits.Count} items", $"{hits.Count} 個項目");
        foreach (var entry in hits) Sections.Children.Add(LauncherCard(entry));
        if (hits.Count == 0) Sections.Children.Add(EmptyNote());
    }

    private TextBlock EmptyNote() => new()
    {
        Text = P("Nothing matches your search.", "冇項目符合你嘅搜尋。"),
        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        Margin = new Thickness(4, 8, 0, 0),
    };

    private Border LauncherCard(SettingsHubEntry entry)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var texts = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock { Text = $"{entry.En} · {entry.Zh}", FontWeight = FontWeights.SemiBold, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis });
        texts.Children.Add(new TextBlock { Text = entry.CommandText, FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"], TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(texts, 0);
        grid.Children.Add(texts);

        var btn = new Button { Content = P("Open", "開啟"), Padding = new Thickness(14, 5, 14, 5), VerticalAlignment = VerticalAlignment.Center };
        btn.Click += (_, _) =>
        {
            bool ok = Launch(entry);
            ResultBar.IsOpen = true;
            ResultBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            ResultBar.Title = ok ? P("Opened", "已開啟") : P("Failed", "失敗");
            ResultBar.Message = ok ? $"{entry.En} · {entry.Zh} — {entry.CommandText}" : P($"Couldn't open {entry.En}.", $"開唔到 {entry.En}。");
        };
        Grid.SetColumn(btn, 1);
        grid.Children.Add(btn);

        return new Border
        {
            Padding = new Thickness(14, 10, 14, 10),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = grid,
        };
    }

    /// <summary>用 Process.Start 啟動真實目標 · Launch the real target with Process.Start.</summary>
    private static bool Launch(SettingsHubEntry entry)
    {
        try
        {
            switch (entry.Kind)
            {
                case SettingsHubKind.Settings:
                    Process.Start(new ProcessStartInfo { FileName = entry.Target, UseShellExecute = true });
                    return true;
                case SettingsHubKind.ControlApplet:
                    Process.Start(new ProcessStartInfo { FileName = "control.exe", Arguments = $"/name {entry.Target}", UseShellExecute = true });
                    return true;
                case SettingsHubKind.Cpl:
                    if (entry.Target.Equals("control.exe", StringComparison.OrdinalIgnoreCase))
                        Process.Start(new ProcessStartInfo { FileName = "control.exe", UseShellExecute = true });
                    else
                        Process.Start(new ProcessStartInfo { FileName = "control.exe", Arguments = entry.Target, UseShellExecute = true });
                    return true;
                default:
                    return false;
            }
        }
        catch { return false; }
    }

    /// <summary>把一個啟動項目分到一個分類（啟發式）· Heuristically bucket a launcher entry into a category.</summary>
    private static int CatIndex(SettingsHubEntry e)
    {
        string h = $"{e.Target} {e.Keywords} {e.En}".ToLowerInvariant();
        bool C(params string[] ks) => ks.Any(h.Contains);

        if (C("windowsupdate", "windows update", "recovery", "activation", "backup", "troubleshoot")) return 10;
        if (C("privacy", "permission", "location", "microphone", "camera-privacy", "diagnostic", "defender", "windowssecurity", "windows security", "firewall", "webcam")) return 9;
        if (C("accessib", "ease of access", "easeofaccess", "narrator", "magnifier", "contrast", "eyecontrol")) return 8;
        if (C("gaming", "game bar", "gamebar", "gamemode", "game mode", "xbox")) return 7;
        if (C("language", "region", "timedate", "date and time", "speech", "keyboard layout", "datetime")) return 6;
        if (C("account", "sign-in", "signin", "yourinfo", "family", "work or school", "windowsanywhere", "sync", "windows hello", "otherusers", "email")) return 5;
        if (C("appsfeatures", "default apps", "defaultapps", "optionalfeatures", "optional features", "startupapps", "uninstall", "maps", "appvolume")) return 4;
        if (C("personaliz", "background", "colors", "colours", "themes", "lockscreen", "lock screen", "startmenu", "taskbar", "fonts")) return 3;
        if (C("network", "wifi", "wi-fi", "ethernet", "vpn", "proxy", "airplane", "hotspot", "dns", "ncpa", "dial", "mobilehotspot")) return 2;
        if (C("bluetooth", "devices", "printers", "mouse", "pen", "touchpad", "autoplay", "usb", "scanner", "camera", "typing")) return 1;
        if (C("system", "display", "sound", "notifications", "power", "battery", "storage", "multitask", "clipboard", "remotedesktop", "remote desktop", "about", "nightlight", "night light", "projection", "sysdm", "wscui")) return 0;
        if (e.Kind != SettingsHubKind.Settings) return 11; // Control Panel
        return 12; // Other
    }
}
