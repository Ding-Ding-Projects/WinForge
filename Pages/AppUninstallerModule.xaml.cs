using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內解除安裝（商店／UWP）· In-app Store/UWP app uninstaller — list, search, silent per-row
/// uninstall via Remove-AppxPackage. Frameworks excluded. No Settings redirect. Bilingual.
/// </summary>
public sealed partial class AppUninstallerModule : Page
{
    private List<AppInfo> _all = new();
    private bool _busy;

    public AppUninstallerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += async (_, _) => { Render(); await Reload(); };
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "App Uninstaller · 應用程式解除安裝";
        HeaderBlurb.Text = P("Remove Store/UWP apps (bloatware) silently — shared frameworks are hidden so they can't be removed. Reinstall from the Store anytime.",
            "靜靜哋移除商店／UWP 應用程式（臃腫程式）— 共用框架已經收埋，唔會誤刪。隨時可以由商店重裝。");
        FilterBox.PlaceholderText = P("Filter apps (e.g. Xbox, Bing, Clipchamp)…", "篩選應用程式（例如 Xbox、Bing、Clipchamp）…");
        RefreshBtn.Content = P("Refresh", "重新整理");
    }

    private async Task Reload()
    {
        if (_busy) return;
        _busy = true;
        CountText.Text = P("Loading…", "載入緊…");
        try
        {
            _all = await UninstallManager.ListAsync();
            ApplyFilter(FilterBox.Text ?? string.Empty);
            EnrichSizes(_all);
        }
        finally { _busy = false; }
    }

    /// <summary>Measure each app's on-disk size off the UI thread and stream the results back into the rows.</summary>
    private void EnrichSizes(List<AppInfo> apps)
    {
        var dq = DispatcherQueue;
        var snapshot = apps.ToList();
        _ = Task.Run(() =>
        {
            foreach (var a in snapshot)
            {
                var text = UninstallManager.FormatSize(UninstallManager.MeasureSize(a));
                dq.TryEnqueue(() => a.SizeText = text);
            }
        });
    }

    private void ApplyFilter(string filter)
    {
        IEnumerable<AppInfo> shown = _all;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _all.Where(a => a.DisplayName.ToLowerInvariant().Contains(f)
                || a.Name.ToLowerInvariant().Contains(f)
                || a.Publisher.ToLowerInvariant().Contains(f));
        }
        var listed = shown.ToList();
        List.ItemsSource = listed;
        CountText.Text = P($"{listed.Count} / {_all.Count} apps", $"{listed.Count} / {_all.Count} 個應用程式");
        EmptyText.Text = _all.Count == 0
            ? P("Loading apps… or none found.", "載入緊應用程式…或者搵唔到。")
            : P("No apps match your filter.", "冇應用程式符合你嘅篩選。");
        EmptyText.Visibility = listed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Filter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ApplyFilter(sender.Text ?? string.Empty);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Reload();

    private void Actions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.DataContext is not AppInfo app) return;
        var mf = new MenuFlyout();

        var u = new MenuFlyoutItem { Text = P("Uninstall", "解除安裝"), Icon = new FontIcon { Glyph = ((char)0xE74D).ToString() } };
        u.Click += async (_, _) => await DoUninstall(app, deep: false);
        mf.Items.Add(u);

        var d = new MenuFlyoutItem
        {
            Text = P("Deep uninstall (clear leftovers)", "徹底解除安裝（清殘留）"),
            Icon = new FontIcon { Glyph = ((char)0xE74D).ToString() },
        };
        d.Click += async (_, _) => await DoUninstall(app, deep: true);
        mf.Items.Add(d);

        mf.ShowAt(b);
    }

    private async Task DoUninstall(AppInfo app, bool deep)
    {
        if (_busy) return;

        var body = deep
            ? P("Remove this app AND clear its leftover per-user data (%LocalAppData%\\Packages). You can reinstall it from the Store.",
                "移除呢個應用程式，並清埋佢喺 %LocalAppData%\\Packages 嘅殘留資料。之後可以由商店重裝。")
            : P("Remove this app for the current user? You can reinstall it from the Microsoft Store.",
                "幫目前使用者移除呢個應用程式？之後可以由 Microsoft Store 重裝。");
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = deep ? P("Deep uninstall?", "徹底解除安裝？") : P("Uninstall app?", "解除安裝？"),
            Content = $"{app.DisplayName}\n{app.Name}\n\n{body}",
            PrimaryButtonText = deep ? P("Deep uninstall", "徹底解除") : P("Uninstall", "解除安裝"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        _busy = true;
        try
        {
            var r = deep ? await UninstallManager.DeepUninstall(app) : await UninstallManager.Uninstall(app);
            bool needAdmin = !r.Success && !AdminHelper.IsElevated;
            ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            ResultBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
            ResultBar.Message = r.Success
                ? (r.Message is not null ? $"{r.Message.En}\n{r.Message.Zh}" : P($"Uninstalled {app.ShortName}.", $"已解除安裝 {app.ShortName}。"))
                : needAdmin
                    ? P($"'{app.ShortName}' may need administrator rights (system app).", $"「{app.ShortName}」可能需要管理員權限（系統應用程式）。")
                    : $"{app.ShortName} — {(r.Output ?? "").Split('\n').FirstOrDefault()}";
            ResultBar.IsOpen = true;
        }
        finally { _busy = false; }
        await Reload();
    }
}
