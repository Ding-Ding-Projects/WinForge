using System;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Controls;

/// <summary>
/// 原生 app 啟動卡 · The reusable UI for one "cannot be stuffed" app: a status line, a one-click
/// "install everything" chain (dependencies-and-all, via <see cref="ExternalAppService.InstallAllAsync"/>
/// with the streaming <see cref="InstallProgress"/> control) and a Launch button that opens the REAL app in
/// its native window. Hosted by <c>AppLauncherWindow</c> as a popup, and droppable inline in any module page.
/// Fully bilingual; the named <see cref="Loc"/> handler is unsubscribed on Unloaded (no leak).
/// </summary>
public sealed class AppLauncherCard : UserControl
{
    private readonly ExternalAppSpec _spec;

    private readonly TextBlock _title;
    private readonly TextBlock _category;
    private readonly TextBlock _description;
    private readonly TextBlock _includes;
    private readonly Border _statusPill;
    private readonly TextBlock _statusPillText;
    private readonly InstallProgress _install;
    private readonly Button _launch;
    private readonly Button _browse;
    private readonly Button _homepage;
    private readonly InfoBar _info;
    private bool _locSubscribed;

    public AppLauncherCard(ExternalAppSpec spec)
    {
        _spec = spec;

        var root = new StackPanel { Spacing = 12 };

        // ── Header: glyph · name/category · status pill ──
        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new FontIcon
        {
            Glyph = string.IsNullOrEmpty(spec.Glyph) ? ((char)0xE71D).ToString() : spec.Glyph,
            FontSize = 26,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(icon, 0);
        header.Children.Add(icon);

        var titles = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        _title = new TextBlock { FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        _category = new TextBlock { FontSize = 12, Foreground = SafeBrush("TextFillColorTertiaryBrush", Colors.Gray) };
        titles.Children.Add(_title);
        titles.Children.Add(_category);
        Grid.SetColumn(titles, 1);
        header.Children.Add(titles);

        _statusPillText = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold };
        _statusPill = new Border
        {
            Padding = new Thickness(10, 4, 10, 4),
            CornerRadius = new CornerRadius(999),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _statusPillText,
        };
        Grid.SetColumn(_statusPill, 2);
        header.Children.Add(_statusPill);
        root.Children.Add(header);

        // ── Description + dependency list ──
        _description = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = SafeBrush("TextFillColorSecondaryBrush", Colors.Gray),
        };
        root.Children.Add(_description);

        _includes = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = SafeBrush("TextFillColorTertiaryBrush", Colors.Gray),
        };
        root.Children.Add(_includes);

        // ── One-click install of the WHOLE chain (streaming progress) ──
        _install = InstallProgress.Create("Install & launch", "安裝並啟動", async (progress, ct) =>
        {
            var r = await ExternalAppService.InstallAllAsync(_spec, progress, ct);
            try { DispatcherQueue?.TryEnqueue(RefreshStatus); } catch { }
            return r.Success ? ExternalAppService.Launch(_spec) : r;
        });
        root.Children.Add(_install);

        // ── Actions: Launch (native window) + Homepage ──
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _launch = new Button { MinWidth = 150, Style = TrySafeStyle("AccentButtonStyle") };
        _launch.Click += OnLaunchClick;
        _browse = new Button();
        _browse.Click += OnBrowseClick;
        _homepage = new Button();
        _homepage.Click += OnHomepageClick;
        actions.Children.Add(_launch);
        actions.Children.Add(_browse);
        if (!string.IsNullOrWhiteSpace(spec.Homepage)) actions.Children.Add(_homepage);
        root.Children.Add(actions);

        // ── Persistent status/result InfoBar ──
        _info = new InfoBar { IsOpen = false, IsClosable = true, Margin = new Thickness(0, 2, 0, 0) };
        root.Children.Add(_info);

        Content = root;

        RenderText();
        RefreshStatus();

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
        RefreshStatus();
    }

    private void RenderText()
    {
        _title.Text = $"{_spec.NameEn} · {_spec.NameZh}";
        _category.Text = $"{_spec.CategoryEn} · {_spec.CategoryZh}";
        _description.Text = P(_spec.DescriptionEn, _spec.DescriptionZh);

        var deps = _spec.Dependencies
            .Select(d => P(d.En, d.Zh) + (d.Optional ? P(" (optional)", "（可選）") : ""))
            .ToList();
        _includes.Text = deps.Count == 0
            ? ""
            : P("Auto-installs: ", "自動安裝：") + string.Join(" → ", deps);

        _launch.Content = P($"Launch {_spec.NameEn}", $"啟動 {_spec.NameZh}");
        _browse.Content = P("Locate executable", "指定執行檔");
        _homepage.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new FontIcon { Glyph = ((char)0xE774).ToString(), FontSize = 13 },
                new TextBlock { Text = P("Website", "官網") },
            },
        };
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RenderText();
        RefreshStatus();
    }

    /// <summary>重新偵測安裝狀態，更新狀態章同啟動按鈕 · Re-detect install state; update the pill + Launch button.</summary>
    public void RefreshStatus()
    {
        ExternalAppService.Rescan(_spec);
        var exe = ExternalAppService.ResolveExe(_spec);
        bool installed = exe is not null;

        _launch.IsEnabled = installed;
        _statusPillText.Text = installed ? P("Installed", "已安裝") : P("Not installed", "未安裝");
        _statusPill.Background = installed
            ? SafeBrush("SystemFillColorSuccessBackgroundBrush", Color.FromArgb(0x33, 0x2E, 0x7D, 0x32))
            : SafeBrush("CardStrokeColorDefaultBrush", Color.FromArgb(0x22, 0x88, 0x88, 0x88));
        _statusPillText.Foreground = installed
            ? SafeBrush("SystemFillColorSuccessBrush", Color.FromArgb(0xFF, 0x2E, 0x7D, 0x32))
            : SafeBrush("TextFillColorSecondaryBrush", Colors.Gray);

        if (installed)
        {
            _info.Severity = InfoBarSeverity.Success;
            _info.Message = P($"Ready — detected at {exe}", $"已就緒 — 偵測到：{exe}");
            _info.IsOpen = true;
        }
        else
        {
            _info.IsOpen = false;
        }
    }

    private void OnLaunchClick(object sender, RoutedEventArgs e)
        => Launch();

    public TweakResult Launch()
    {
        var r = ExternalAppService.Launch(_spec);
        _info.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        _info.Message = r.Message?.Get(Loc.I.Language) ?? "";
        _info.IsOpen = true;
        return r;
    }

    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".exe");
        if (path is null) return;
        ExternalAppService.SetPathOverride(_spec, path);
        RefreshStatus();
    }

    private void OnHomepageClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!UserProcessLauncher.TryStart(_spec.Homepage, "", "", out var error))
                throw new InvalidOperationException(error);
        }
        catch (Exception ex)
        {
            _info.Severity = InfoBarSeverity.Error;
            _info.Message = ex.Message;
            _info.IsOpen = true;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_locSubscribed)
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            _locSubscribed = false;
        }
    }

    // ── helpers ──
    private static Brush SafeBrush(string key, Color fallback)
    {
        try
        {
            if (Application.Current?.Resources?.TryGetValue(key, out var v) == true && v is Brush b) return b;
        }
        catch { }
        return new SolidColorBrush(fallback);
    }

    private static Style? TrySafeStyle(string key)
    {
        try
        {
            if (Application.Current?.Resources?.TryGetValue(key, out var v) == true && v is Style s) return s;
        }
        catch { }
        return null;
    }
}
