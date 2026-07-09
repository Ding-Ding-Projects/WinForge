using System;
using System.Collections.Generic;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生 app 啟動彈窗 · A dedicated popup window for one "cannot be stuffed" app. It hosts a single
/// <see cref="AppLauncherCard"/> — install-the-whole-chain + launch-the-native-app — in its own AppWindow,
/// so the app is presented and run in its ORIGINAL native form (a popup outside the WinForge shell) while
/// WinForge still handles fully-automatic dependency installation. Windows are tracked in a static set so
/// they are not garbage-collected while open. Fully bilingual.
/// </summary>
public sealed class AppLauncherWindow : Window
{
    // Keep references so open windows are not collected (WinUI does not root secondary windows for us).
    private static readonly HashSet<AppLauncherWindow> Open = new();

    private readonly ExternalAppSpec _spec;

    private AppLauncherWindow(ExternalAppSpec spec)
    {
        _spec = spec;
        Title = $"{spec.NameEn} · {spec.NameZh}";
        try { AppWindow.SetIcon("Assets/AppIcon.ico"); } catch { }
        ExtendsContentIntoTitleBar = false;

        var presenter = OverlappedPresenter.Create();
        presenter.IsMaximizable = false;
        AppWindow.SetPresenter(presenter);

        Content = BuildContent();

        try
        {
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            const int w = 560, h = 620;
            AppWindow.Resize(new SizeInt32(w, h));
            AppWindow.Move(new PointInt32(
                area.WorkArea.X + Math.Max(0, (area.WorkArea.Width - w) / 2),
                area.WorkArea.Y + Math.Max(0, (area.WorkArea.Height - h) / 2)));
        }
        catch { }

        Open.Add(this);
        Loc.I.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            Open.Remove(this);
        };
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
        => Title = $"{_spec.NameEn} · {_spec.NameZh}";

    private FrameworkElement BuildContent()
    {
        var scroller = new ScrollViewer
        {
            Padding = new Thickness(20),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        var card = new Border
        {
            Padding = new Thickness(18),
            Background = Brush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Brush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = new AppLauncherCard(_spec),
        };

        scroller.Content = new StackPanel
        {
            MaxWidth = 520,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children = { card },
        };
        return scroller;
    }

    private static Brush? Brush(string key)
    {
        try
        {
            if (Application.Current?.Resources?.TryGetValue(key, out var v) == true && v is Brush b) return b;
        }
        catch { }
        return null;
    }

    // ── Public entry points ──

    /// <summary>開一個 app 啟動彈窗 · Open a launcher popup for the given app spec (activates it).</summary>
    public static void Show(ExternalAppSpec spec)
    {
        var w = new AppLauncherWindow(spec);
        w.Activate();
    }

    /// <summary>依 id 開彈窗（搵唔到就靜靜返回）· Open a launcher popup by app id (no-op if unknown).</summary>
    public static bool Show(string id)
    {
        var spec = ExternalApps.ById(id);
        if (spec is null) return false;
        Show(spec);
        return true;
    }
}
