using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內 ViVeTool 功能旗標管理員 · In-app ViVeTool feature-flag manager — list the live Feature Store
/// (/query), search by id or human name, enable/disable/reset by id (resolved + shown before applying),
/// /fullreset (guarded), /export, /import, /lkgstatus, named toggles, scan available-but-disabled experiments,
/// and restart-explorer / reboot helpers. Everything runs in-app; no external Windows UI. Bilingual.
/// </summary>
public sealed partial class ViveToolModule : Page
{
    private List<ViveFeature> _all = new();
    private bool _busy;
    private bool _installed;

    public ViveToolModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) => { Render(); await DetectAndLoad(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "ViVeTool · 功能旗標";
        FilterBox.PlaceholderText = P("Filter by name or id…", "用名或 ID 篩選…");
        RefreshBtn.Content = P("Refresh", "重新整理");
        MoreText.Text = P("More", "更多");
        TogglesTitle.Text = P("Named toggles", "有名嘅切換");

        ScanItem.Text = P("Scan available-but-disabled experiments", "掃描可試但未開嘅實驗");
        LkgItem.Text = P("Last Known Good status", "Last Known Good 狀態");
        ExportItem.Text = P("Export profile…", "匯出設定檔…");
        ImportItem.Text = P("Import profile…", "匯入設定檔…");
        RestartExplorerItem.Text = P("Restart Explorer", "重啟檔案總管");
        RebootItem.Text = P("Reboot now", "立即重新開機");
        FullResetItem.Text = P("Full reset (wipe all flags)", "完全重設（清除全部旗標）");

        InstallBtn.Content = P("Install via winget", "用 winget 安裝");

        BuildToggles();
    }

    private async Task DetectAndLoad()
    {
        _installed = await ViveToolService.IsAvailable();
        if (!_installed)
        {
            InstallBar.Title = P("ViVeTool not found", "搵唔到 ViVeTool");
            InstallBar.Message = P(
                "ViVeTool.exe is required to manage feature flags. Install it (thebookisclosed.ViVeTool), then Refresh.",
                "管理功能旗標需要 ViVeTool.exe。請安裝（thebookisclosed.ViVeTool），然後重新整理。");
            InstallBar.IsOpen = true;
            SetEnabled(false);
            CountText.Text = "";
            EmptyText.Text = P("Install ViVeTool to see the live Feature Store.", "安裝 ViVeTool 後即可睇到本機 Feature Store。");
            EmptyText.Visibility = Visibility.Visible;
            List.ItemsSource = null;
            return;
        }
        InstallBar.IsOpen = false;
        SetEnabled(true);
        await Reload();
    }

    private void SetEnabled(bool on)
    {
        FilterBox.IsEnabled = on;
        MoreBtn.IsEnabled = on;
        foreach (var child in TogglesPanel.Children)
            if (child is Control c) c.IsEnabled = on;
    }

    // ---- named toggles ------------------------------------------------------------------

    private void BuildToggles()
    {
        TogglesPanel.Children.Clear();
        foreach (var t in ViveToolService.NamedToggles)
        {
            var btn = new Button { Padding = new Thickness(12, 8, 12, 8), Tag = t };
            var sp = new StackPanel { Spacing = 2 };
            sp.Children.Add(new TextBlock { Text = $"{t.En} · {t.Zh}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13 });
            var note = P(t.En2, t.Zh2);
            if (!string.IsNullOrEmpty(note))
                sp.Children.Add(new TextBlock { Text = note, FontSize = 11, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
            btn.Content = sp;
            btn.Click += Toggle_Click;
            TogglesPanel.Children.Add(btn);
        }
    }

    private async void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not ViveNamedToggle t) return;
        if (!await EnsureInstalled()) return;

        // Resolve the candidate ids against the live store — only act on ids actually present on THIS build.
        var present = _all.Select(f => f.Id).ToHashSet();
        var resolved = t.Ids.Where(present.Contains).ToList();
        var missing = t.Ids.Where(id => !present.Contains(id)).ToList();

        var idLine = resolved.Count > 0
            ? P($"Resolved ids on this build: {string.Join(", ", resolved)}", $"本機已解析嘅 ID：{string.Join("、", resolved)}")
            : P("None of the candidate ids exist on this build.", "本機並冇任何候選 ID。");
        var missLine = missing.Count > 0
            ? "\n" + P($"Not present (skipped): {string.Join(", ", missing)}", $"唔存在（略過）：{string.Join("、", missing)}")
            : "";

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"{t.En} · {t.Zh}",
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = P(
                    $"Enable this feature group? Requires admin and a reboot ({(t.ShellOnly ? "or just restart Explorer" : "store-level")}).\n\n",
                    $"啟用呢組功能？需要管理員權限同重新開機（{(t.ShellOnly ? "或者只重啟檔案總管" : "store-level")}）。\n\n")
                    + idLine + missLine,
            },
            PrimaryButtonText = P("Enable", "啟用"),
            SecondaryButtonText = P("Reset to default", "重設為預設"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (resolved.Count == 0) { dlg.IsPrimaryButtonEnabled = false; dlg.IsSecondaryButtonEnabled = false; }

        var res = await dlg.ShowAsync();
        if (res == ContentDialogResult.None || resolved.Count == 0) return;

        if (res == ContentDialogResult.Primary)
            await RunGlobal(() => ViveToolService.EnableMany(resolved), P($"Enable {t.En}", $"啟用 {t.Zh}"), t.ShellOnly);
        else
            await RunGlobal(() => ViveToolService.ResetMany(resolved), P($"Reset {t.En}", $"重設 {t.Zh}"), t.ShellOnly);
    }

    // ---- list ---------------------------------------------------------------------------

    private async Task Reload()
    {
        if (_busy) return;
        _busy = true;
        CountText.Text = P("Loading…", "載入緊…");
        try
        {
            _all = await ViveToolService.QueryAsync();
            // Sort: named features first, then by id.
            _all = _all.OrderByDescending(f => f.HasFriendly).ThenBy(f => f.Id).ToList();
            ApplyFilter(FilterBox.Text ?? string.Empty);
        }
        finally { _busy = false; }
    }

    private void ApplyFilter(string filter)
    {
        IEnumerable<ViveFeature> shown = _all;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _all.Where(x =>
                x.Id.ToString().Contains(f) ||
                x.FriendlyEn.ToLowerInvariant().Contains(f) ||
                x.FriendlyZh.Contains(filter.Trim()) ||
                x.State.ToLowerInvariant().Contains(f) ||
                x.Type.ToLowerInvariant().Contains(f));
        }
        var listed = shown.ToList();
        List.ItemsSource = listed;
        CountText.Text = P($"{listed.Count} / {_all.Count} features", $"{listed.Count} / {_all.Count} 個功能");
        EmptyText.Text = _all.Count == 0
            ? P("No features in the store (or ViVeTool returned nothing).", "Feature Store 冇任何功能（或者 ViVeTool 冇輸出）。")
            : P("No features match your filter.", "冇功能符合你嘅篩選。");
        EmptyText.Visibility = listed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Filter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ApplyFilter(sender.Text ?? string.Empty);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await DetectAndLoad();

    // ---- per-feature actions ------------------------------------------------------------

    private void Actions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.DataContext is not ViveFeature feat) return;
        var mf = new MenuFlyout();
        void Add(string en, string zh, string glyph, Func<uint, Task<TweakResult>> op, bool shellOnly)
        {
            var it = new MenuFlyoutItem { Text = $"{en} · {zh}", Icon = new FontIcon { Glyph = glyph } };
            it.Click += async (_, _) => await ConfirmAndRun(feat, en, zh, op, shellOnly);
            mf.Items.Add(it);
        }
        Add("Enable", "啟用", ((char)0xE73E).ToString(), id => ViveToolService.Enable(id), false);
        Add("Disable", "停用", ((char)0xE711).ToString(), id => ViveToolService.Disable(id), false);
        Add("Reset to default", "重設為預設", ((char)0xE777).ToString(), id => ViveToolService.Reset(id), false);
        mf.ShowAt(b);
    }

    private async Task ConfirmAndRun(ViveFeature feat, string en, string zh, Func<uint, Task<TweakResult>> op, bool shellOnly)
    {
        if (!await EnsureInstalled()) return;
        var name = feat.HasFriendly ? $"{feat.FriendlyEn} · {feat.FriendlyZh}" : P("(unnamed feature)", "（無名功能）");
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"{en} · {zh}",
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = P(
                    $"Apply to feature id {feat.Id} ({name})?\nCurrent state: {feat.State}. Requires admin + reboot to apply.",
                    $"套用到功能 ID {feat.Id}（{name}）？\n目前狀態：{feat.State}。需要管理員權限同重新開機先生效。"),
            },
            PrimaryButtonText = $"{en} · {zh}",
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        await RunGlobal(() => op(feat.Id), $"{P(en, zh)} {feat.Id}", shellOnly);
    }

    // ---- global verbs -------------------------------------------------------------------

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsureInstalled() || _busy) return;
        _busy = true;
        CountText.Text = P("Scanning…", "掃描緊…");
        try
        {
            var avail = await ViveToolService.ScanAvailableDisabled();
            FilterBox.Text = "";
            List.ItemsSource = avail;
            CountText.Text = P($"{avail.Count} available to try", $"{avail.Count} 個可試");
            EmptyText.Text = P("No known experiments are available-but-disabled on this build.", "本機冇任何已知但未開嘅實驗。");
            EmptyText.Visibility = avail.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Info(InfoBarSeverity.Informational, P("Scan complete", "掃描完成"),
                P($"Found {avail.Count} known experiments present but not enabled.", $"搵到 {avail.Count} 個本機存在但未啟用嘅已知實驗。"));
        }
        finally { _busy = false; }
    }

    private async void Lkg_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsureInstalled()) return;
        var status = await ViveToolService.LkgStatus();
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Last Known Good status", "Last Known Good 狀態"),
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(status) ? P("(no output)", "（冇輸出）") : status,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                    TextWrapping = TextWrapping.Wrap,
                },
                MaxHeight = 360,
            },
            CloseButtonText = P("Close", "關閉"),
        };
        await dlg.ShowAsync();
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsureInstalled()) return;
        var path = await FileDialogs.SaveFileAsync("WinForge-vivetool-profile", ".json", ".vto", ".bin");
        if (path is null) return;
        await RunGlobal(() => ViveToolService.Export(path), P("Export profile", "匯出設定檔"), null);
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsureInstalled()) return;
        var path = await FileDialogs.OpenFileAsync(".json", ".vto", ".bin");
        if (path is null) return;
        await RunGlobal(() => ViveToolService.Import(path), P("Import profile", "匯入設定檔"), false);
    }

    private async void RestartExplorer_Click(object sender, RoutedEventArgs e)
    {
        var dlg = Confirm(P("Restart Explorer", "重啟檔案總管"),
            P("This closes and restarts explorer.exe to apply shell-only features.", "呢個會關閉並重啟 explorer.exe 以套用 shell-only 功能。"),
            P("Restart", "重啟"));
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        await RunGlobal(() => ViveToolService.RestartExplorer(), P("Restart Explorer", "重啟檔案總管"), null);
    }

    private async void Reboot_Click(object sender, RoutedEventArgs e)
    {
        var dlg = Confirm(P("Reboot now", "立即重新開機"),
            P("Windows will restart immediately to apply store-level features. Save your work first.", "Windows 會即刻重新開機以套用 store-level 功能。請先儲存工作。"),
            P("Reboot", "重新開機"));
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        await RunGlobal(() => ViveToolService.Reboot(), P("Reboot", "重新開機"), null);
    }

    private async void FullReset_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsureInstalled()) return;
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Full reset", "完全重設"),
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = P(
                    "This removes EVERY custom feature configuration and returns the Feature Store to Windows defaults. All your prior toggles will be wiped. Requires admin + reboot. This cannot be undone.",
                    "呢個會移除你所有自訂功能配置，並將 Feature Store 還原為 Windows 預設。你之前所有嘅切換都會清除。需要管理員權限同重新開機，無法復原。"),
            },
            PrimaryButtonText = P("Wipe everything", "全部清除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        await RunGlobal(() => ViveToolService.FullReset(), P("Full reset", "完全重設"), false);
    }

    // ---- helpers ------------------------------------------------------------------------

    private ContentDialog Confirm(string title, string body, string primary) => new()
    {
        XamlRoot = XamlRoot,
        Title = title,
        Content = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap },
        PrimaryButtonText = primary,
        CloseButtonText = P("Cancel", "取消"),
        DefaultButton = ContentDialogButton.Primary,
    };

    private async Task<bool> EnsureInstalled()
    {
        if (_installed) return true;
        _installed = await ViveToolService.IsAvailable();
        if (!_installed)
            Info(InfoBarSeverity.Warning, P("ViVeTool not found", "搵唔到 ViVeTool"),
                P("Install ViVeTool first.", "請先安裝 ViVeTool。"));
        return _installed;
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        InstallBtn.IsEnabled = false;
        Info(InfoBarSeverity.Informational, P("Installing…", "安裝緊…"), P("Running winget…", "執行 winget…"));
        try
        {
            var r = await ViveToolService.InstallViaWinget();
            Info(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
                r.Success ? P("Installed", "已安裝") : P("Failed", "失敗"),
                r.Message is null ? "" : Loc.I.Pick(r.Message.En, r.Message.Zh));
        }
        finally { _busy = false; InstallBtn.IsEnabled = true; }
        await DetectAndLoad();
    }

    /// <summary>跑一個全域動作，完成後可選擇提示套用（重啟 explorer／重新開機）· Run a global op, then optionally offer apply.</summary>
    private async Task RunGlobal(Func<Task<TweakResult>> op, string verb, bool? shellOnly)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var r = await op();
            bool needAdmin = !r.Success && !AdminHelper.IsElevated;
            Info(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
                r.Success ? P("Done", "完成") : P("Failed", "失敗"),
                needAdmin
                    ? P($"{verb} needs administrator rights.", $"{verb}需要管理員權限。")
                    : $"{verb} — {(r.Success ? "OK" : (r.Output ?? Loc.I.Pick(r.Message?.En ?? "", r.Message?.Zh ?? "")))}");

            if (r.Success && shellOnly.HasValue)
                await OfferApply(shellOnly.Value);
        }
        finally { _busy = false; }
        if (_installed) await Reload();
    }

    /// <summary>套用提示（使用者確認，唔會預設執行）· Offer to apply (user confirms; never destructive by default).</summary>
    private async Task OfferApply(bool shellOnly)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Apply the change?", "要套用變更嗎？"),
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = shellOnly
                    ? P("This feature is shell-only — restarting Explorer is usually enough. Reboot for full effect.",
                        "呢個係 shell-only 功能 — 通常重啟檔案總管就夠。重新開機可完全生效。")
                    : P("This is a store-level feature — a reboot is needed to apply.",
                        "呢個係 store-level 功能 — 需要重新開機先生效。"),
            },
            PrimaryButtonText = shellOnly ? P("Restart Explorer", "重啟檔案總管") : P("Reboot now", "立即重新開機"),
            SecondaryButtonText = shellOnly ? P("Reboot now", "立即重新開機") : "",
            CloseButtonText = P("Later", "稍後"),
            DefaultButton = ContentDialogButton.Close,
        };
        var res = await dlg.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            if (shellOnly) await ViveToolService.RestartExplorer();
            else await ViveToolService.Reboot();
        }
        else if (res == ContentDialogResult.Secondary && shellOnly)
        {
            await ViveToolService.Reboot();
        }
    }

    private void Info(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev;
        ResultBar.Title = title;
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
