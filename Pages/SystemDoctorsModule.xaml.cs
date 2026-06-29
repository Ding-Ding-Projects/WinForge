using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 系統醫生 · System Doctors — guided in-app rescue routines for common Windows 11 breakages
/// (print queue, network/DNS, sleep/wake, taskbar &amp; Start, search index, Explorer, caches,
/// take-ownership). Every action runs real commands; diagnostics are parsed into native bilingual
/// lists, not raw dumps. Fully bilingual, no redirects.
/// </summary>
public sealed partial class SystemDoctorsModule : Page
{
    private bool _busy;
    private string _ownPath = "";

    public SystemDoctorsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Build();
        Loaded += (_, _) => Build();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private static Brush Sub => (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
    private static Brush Tert => (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];

    private void Build()
    {
        Header.Title = "System Doctors · 系統醫生";
        Header.Subtitle = P("Guided rescue routines that really fix Windows 11 — diagnose, then repair, all in-app.",
            "真正修復 Windows 11 嘅引導式急救流程 — 先診斷、再修復，全程喺 app 內。");

        if (!AdminHelper.IsElevated)
        {
            AdminBar.Severity = InfoBarSeverity.Warning;
            AdminBar.Title = P("Some doctors need administrator rights", "部分醫生需要管理員權限");
            AdminBar.Message = P("Spooler, wake/sleep, fast startup and take-ownership need elevation. Relaunch as admin for full effect.",
                "列印多工、喚醒／睡眠、快速啟動同取得擁有權需要提升權限。以管理員身分重開先有完整效果。");
            var relaunch = new Button { Content = P("Relaunch as admin", "以管理員身分重新啟動") };
            relaunch.Click += (_, _) => { if (AdminHelper.RelaunchElevated()) Application.Current.Exit(); };
            AdminBar.ActionButton = relaunch;
            AdminBar.IsOpen = true;
        }
        else
        {
            AdminBar.IsOpen = false;
        }

        DoctorsPanel.Children.Clear();
        BuildPrintDoctor();
        BuildNetworkDoctor();
        BuildSleepWakeDoctor();
        BuildShellDoctor();
        BuildSearchDoctor();
        BuildExplorerDoctor();
        BuildCacheDoctor();
        BuildOwnershipDoctor();
    }

    // ===================== shared card scaffolding =====================

    /// <summary>建立一張醫生卡（Expander）· Build one doctor card and return its body panel.</summary>
    private (Expander card, StackPanel body) NewCard(string glyph, string titleEn, string titleZh, string descEn, string descZh)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        header.Children.Add(new FontIcon { Glyph = glyph, FontSize = 20, VerticalAlignment = VerticalAlignment.Center });
        var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        t.Children.Add(new TextBlock { Text = $"{titleEn} · {titleZh}", FontWeight = FontWeights.SemiBold });
        t.Children.Add(new TextBlock { Text = P(descEn, descZh), FontSize = 12, Foreground = Sub, TextWrapping = TextWrapping.Wrap });
        header.Children.Add(t);

        var body = new StackPanel { Spacing = 10 };
        var card = new Expander
        {
            Header = header,
            Content = body,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0),
        };
        DoctorsPanel.Children.Add(card);
        return (card, body);
    }

    /// <summary>一行動作按鈕 · A horizontal row of action buttons.</summary>
    private static WrapButtons Buttons() => new();

    private sealed class WrapButtons : StackPanel
    {
        public WrapButtons()
        {
            Orientation = Orientation.Horizontal;
            Spacing = 8;
        }
    }

    private Button MakeButton(string en, string zh, string glyph, Func<Task> onClick, bool destructive = false)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14 });
        content.Children.Add(new TextBlock { Text = $"{en} · {zh}", FontSize = 13 });
        var b = new Button { Content = content, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 0, 4) };
        if (destructive) b.Background = (Brush)Application.Current.Resources["SystemFillColorCautionBackgroundBrush"];
        b.Click += async (_, _) => await Guard(onClick);
        return b;
    }

    private async Task Guard(Func<Task> work)
    {
        if (_busy) return;
        _busy = true;
        try { await work(); }
        catch (Exception ex) { ShowResult(false, P("Error", "出錯"), ex.Message); }
        finally { _busy = false; }
    }

    /// <summary>顯示動作結果（橫額）· Show an action result in the bottom InfoBar.</summary>
    private void ShowResult(bool ok, string title, string message)
    {
        ResultBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = title;
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }

    private void ShowTweakResult(TweakResult r, string verb, StackPanel? outputHost = null)
    {
        bool needAdmin = !r.Success && !AdminHelper.IsElevated;
        ShowResult(r.Success, r.Success ? P("Done", "完成") : P("Failed", "失敗"),
            needAdmin
                ? P($"{verb} needs administrator rights.", $"{verb}需要管理員權限。")
                : $"{verb} — {(r.Success ? "OK" : (r.Message?.Primary ?? ""))}");
        if (outputHost is not null && !string.IsNullOrWhiteSpace(r.Output))
            RenderOutputPane(outputHost, r.Output!);
    }

    /// <summary>一個等寬、可捲動、可複製嘅輸出面板 · A monospace, scrollable, copyable output pane.</summary>
    private void RenderOutputPane(StackPanel host, string text)
    {
        host.Children.Clear();
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        bar.Children.Add(new TextBlock { Text = P("Output", "輸出"), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Foreground = Sub });
        var copy = new Button { Content = P("Copy", "複製"), Padding = new Thickness(10, 3, 10, 3) };
        copy.Click += (_, _) =>
        {
            var dp = new DataPackage();
            dp.SetText(text);
            Clipboard.SetContent(dp);
        };
        bar.Children.Add(copy);
        host.Children.Add(bar);

        var box = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            MaxHeight = 220,
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            BorderThickness = new Thickness(1),
        };
        ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(box, ScrollBarVisibility.Auto);
        host.Children.Add(box);
    }

    /// <summary>渲染診斷清單 · Render a parsed diagnostic report into a host panel.</summary>
    private void RenderReport(StackPanel host, DoctorReport rep, Func<DoctorRow, Button?>? rowAction = null)
    {
        host.Children.Clear();
        host.Children.Add(new TextBlock
        {
            Text = Loc.I.Pick(rep.Summary.En, rep.Summary.Zh),
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });

        foreach (var row in rep.Rows)
        {
            var grid = new Grid { Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 2, 0, 0) };
            grid.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"];
            grid.CornerRadius = new CornerRadius(6);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new FontIcon { Glyph = string.IsNullOrEmpty(row.Glyph) ? ((char)0xE73E).ToString() : row.Glyph, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            var texts = new StackPanel();
            texts.Children.Add(new TextBlock { Text = row.Primary, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            if (!string.IsNullOrWhiteSpace(row.Secondary))
                texts.Children.Add(new TextBlock { Text = row.Secondary, FontSize = 12, Foreground = Sub, TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(texts, 1);
            grid.Children.Add(texts);

            var act = rowAction?.Invoke(row);
            if (act is not null)
            {
                Grid.SetColumn(act, 2);
                act.VerticalAlignment = VerticalAlignment.Center;
                grid.Children.Add(act);
            }
            host.Children.Add(grid);
        }

        if (rep.Rows.Count == 0 && !string.IsNullOrWhiteSpace(rep.RawOutput) && rep.RawOutput!.Trim().Length > 0
            && !rep.RawOutput.Trim().StartsWith("[") && rep.RawOutput.Trim() != "{}")
        {
            RenderOutputPane(host, rep.RawOutput!.Trim());
        }
    }

    private static StackPanel ResultHost()
        => new() { Spacing = 6 };

    // ===================== 1) Print Spooler & queue rescue =====================

    private void BuildPrintDoctor()
    {
        var (_, body) = NewCard(((char)0xE749).ToString(), "Print Spooler & queue rescue", "列印多工與佇列救援",
            "Clear stuck print jobs and revive the spooler.", "清走卡住嘅列印工作、救返個多工緩衝處理器。");

        var report = ResultHost();
        var output = ResultHost();

        var btns = Buttons();
        btns.Children.Add(MakeButton("Diagnose queue", "診斷佇列", ((char)0xE721).ToString(), async () =>
        {
            var rep = await SystemDoctors.ListPrintJobsAsync();
            RenderReport(report, rep, row =>
            {
                if (row.Tag is null) return null;
                var b = new Button { Content = P("Cancel", "取消"), Padding = new Thickness(10, 3, 10, 3) };
                b.Click += async (_, _) => await Guard(async () =>
                {
                    var r = await SystemDoctors.CancelPrintJobAsync(row.Tag!);
                    ShowTweakResult(r, P("Cancel job", "取消工作"), output);
                    RenderReport(report, await SystemDoctors.ListPrintJobsAsync());
                });
                return b;
            });
        }));
        btns.Children.Add(MakeButton("Rescue spooler (purge queue)", "救援（清佇列）", ((char)0xE777).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.RescueSpoolerAsync(), P("Rescue spooler", "救援多工緩衝"), output), destructive: true));
        btns.Children.Add(MakeButton("Restart spooler only", "只重啟", ((char)0xE72C).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.RestartSpoolerAsync(), P("Restart spooler", "重啟多工緩衝"), output)));

        body.Children.Add(btns);
        body.Children.Add(report);
        body.Children.Add(output);
    }

    // ===================== 2) Network / DNS doctor =====================

    private void BuildNetworkDoctor()
    {
        var (_, body) = NewCard(((char)0xE968).ToString(), "Network / DNS doctor", "網絡 / DNS 醫生",
            "Reset Winsock/TCP-IP, flush DNS, renew lease, bounce adapters, one-click repair.",
            "重設 Winsock／TCP-IP、清 DNS、重續租約、重啟介面卡、一鍵修復。");

        var report = ResultHost();
        var output = ResultHost();

        var diag = Buttons();
        diag.Children.Add(MakeButton("List adapters", "列出介面卡", ((char)0xE721).ToString(), async () =>
        {
            var rep = await SystemDoctors.ListAdaptersAsync();
            RenderReport(report, rep, row =>
            {
                if (row.Tag is null) return null;
                var b = new Button { Content = P("Bounce", "重啟"), Padding = new Thickness(10, 3, 10, 3) };
                b.Click += async (_, _) => await Guard(async () =>
                {
                    var r = await SystemDoctors.BounceAdapterAsync(row.Tag!);
                    ShowTweakResult(r, P("Bounce adapter", "重啟介面卡"), output);
                    RenderReport(report, await SystemDoctors.ListAdaptersAsync());
                });
                return b;
            });
        }));
        body.Children.Add(diag);

        var ops = Buttons();
        ops.Children.Add(MakeButton("Flush DNS", "清 DNS", ((char)0xE74D).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.FlushDnsAsync(), P("Flush DNS", "清 DNS"), output)));
        ops.Children.Add(MakeButton("Reset Winsock", "重設 Winsock", ((char)0xE72C).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.ResetWinsockAsync(), P("Reset Winsock", "重設 Winsock"), output)));
        ops.Children.Add(MakeButton("Reset TCP/IP", "重設 TCP/IP", ((char)0xE72C).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.ResetTcpIpAsync(), P("Reset TCP/IP", "重設 TCP/IP"), output)));
        ops.Children.Add(MakeButton("Release + renew", "釋放＋重續", ((char)0xE895).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.ReleaseRenewAsync(), P("Release/renew", "釋放／重續"), output)));
        body.Children.Add(ops);

        var repair = Buttons();
        repair.Children.Add(MakeButton("Repair connection (all of the above)", "修復連線（全部）", ((char)0xE90F).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.RepairConnectionAsync(), P("Repair connection", "修復連線"), output), destructive: true));
        body.Children.Add(repair);

        body.Children.Add(report);
        body.Children.Add(output);
    }

    // ===================== 3) Sleep / Wake doctor =====================

    private void BuildSleepWakeDoctor()
    {
        var (_, body) = NewCard(((char)0xE708).ToString(), "Sleep / Wake doctor", "睡眠 / 喚醒醫生",
            "Find what blocks sleep or wakes the PC, disarm wake sources, tune fast startup & power scheme.",
            "搵出乜嘢阻住睡眠或整醒部機、解除喚醒來源、調整快速啟動同電源計劃。");

        var report = ResultHost();
        var output = ResultHost();

        var diag = Buttons();
        diag.Children.Add(MakeButton("What blocks sleep", "乜阻住睡眠", ((char)0xE721).ToString(), async () =>
            RenderReport(report, await SystemDoctors.SleepBlockersAsync())));
        diag.Children.Add(MakeButton("Last wake source", "最近喚醒", ((char)0xE7C1).ToString(), async () =>
            RenderReport(report, await SystemDoctors.LastWakeAsync())));
        diag.Children.Add(MakeButton("Wake timers", "喚醒計時器", ((char)0xE823).ToString(), async () =>
            RenderReport(report, await SystemDoctors.WakeTimersAsync())));
        diag.Children.Add(MakeButton("Wake-armed devices", "可喚醒裝置", ((char)0xE975).ToString(), async () =>
        {
            var rep = await SystemDoctors.WakeArmedDevicesAsync();
            RenderReport(report, rep, row =>
            {
                if (row.Tag is null) return null;
                var b = new Button { Content = P("Disarm", "解除"), Padding = new Thickness(10, 3, 10, 3) };
                b.Click += async (_, _) => await Guard(async () =>
                {
                    var r = await SystemDoctors.DisarmWakeDeviceAsync(row.Tag!);
                    ShowTweakResult(r, P("Disarm wake", "解除喚醒"), output);
                    RenderReport(report, await SystemDoctors.WakeArmedDevicesAsync(), null);
                });
                return b;
            });
        }));
        diag.Children.Add(MakeButton("Fast startup state", "快速啟動狀態", ((char)0xE945).ToString(), async () =>
            RenderReport(report, await SystemDoctors.FastStartupStateAsync())));
        body.Children.Add(diag);

        var ops = Buttons();
        ops.Children.Add(MakeButton("Disable all wake timers", "停用全部喚醒計時器", ((char)0xE711).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.DisableWakeTimersAsync(), P("Disable wake timers", "停用喚醒計時器"), output)));
        ops.Children.Add(MakeButton("Turn off fast startup", "關閉快速啟動", ((char)0xE711).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.SetFastStartupAsync(false), P("Disable fast startup", "關閉快速啟動"), output)));
        ops.Children.Add(MakeButton("Turn on fast startup", "開啟快速啟動", ((char)0xE73E).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.SetFastStartupAsync(true), P("Enable fast startup", "開啟快速啟動"), output)));
        ops.Children.Add(MakeButton("Unlock Ultimate Performance", "解鎖終極效能", ((char)0xE945).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.UnlockUltimatePerformanceAsync(), P("Unlock Ultimate Performance", "解鎖終極效能"), output)));
        body.Children.Add(ops);

        body.Children.Add(report);
        body.Children.Add(output);
    }

    // ===================== 4) Shell recovery — Fix taskbar & Start =====================

    private void BuildShellDoctor()
    {
        var (_, body) = NewCard(((char)0xE71D).ToString(), "Fix taskbar & Start", "修復工作列與開始功能表",
            "Clear the Start/IrisService cache, re-register shell packages, restart Explorer.",
            "清開始功能表／IrisService 快取、重新註冊外殼套件、重啟檔案總管。");

        var output = ResultHost();
        var btns = Buttons();
        btns.Children.Add(MakeButton("Repair taskbar & Start", "修復工作列與開始", ((char)0xE90F).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.FixTaskbarAndStartAsync(), P("Fix taskbar & Start", "修復工作列與開始"), output), destructive: true));
        body.Children.Add(new TextBlock
        {
            Text = P("Your screen will flash as Explorer restarts. Open apps stay running.",
                "重啟檔案總管時畫面會閃一閃，已開嘅程式照樣運行。"),
            FontSize = 12, Foreground = Sub, TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(btns);
        body.Children.Add(output);
    }

    // ===================== 5) Search index governor =====================

    private void BuildSearchDoctor()
    {
        var (_, body) = NewCard(((char)0xE721).ToString(), "Search index governor", "搜尋索引管理",
            "Pause/resume or rebuild the search index, and kill web (Bing) results in Start search.",
            "暫停／繼續或重建搜尋索引，並關閉開始功能表嘅網頁（Bing）結果。");

        var report = ResultHost();
        var output = ResultHost();

        var diag = Buttons();
        diag.Children.Add(MakeButton("Check search state", "檢查搜尋狀態", ((char)0xE721).ToString(), async () =>
            RenderReport(report, await SystemDoctors.SearchStateAsync())));
        body.Children.Add(diag);

        var ops = Buttons();
        ops.Children.Add(MakeButton("Pause search", "暫停搜尋", ((char)0xE769).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.PauseSearchAsync(), P("Pause search", "暫停搜尋"), output)));
        ops.Children.Add(MakeButton("Resume search", "繼續搜尋", ((char)0xE768).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.ResumeSearchAsync(), P("Resume search", "繼續搜尋"), output)));
        ops.Children.Add(MakeButton("Rebuild index", "重建索引", ((char)0xE72C).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.RebuildSearchIndexAsync(), P("Rebuild index", "重建索引"), output), destructive: true));
        body.Children.Add(ops);

        var web = Buttons();
        web.Children.Add(MakeButton("Disable web results", "關閉網頁結果", ((char)0xE711).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.DisableWebResultsAsync(), P("Disable web results", "關閉網頁結果"), output)));
        web.Children.Add(MakeButton("Enable web results", "開啟網頁結果", ((char)0xE73E).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.EnableWebResultsAsync(), P("Enable web results", "開啟網頁結果"), output)));
        body.Children.Add(web);

        body.Children.Add(report);
        body.Children.Add(output);
    }

    // ===================== 6) Explorer perf tuner =====================

    private void BuildExplorerDoctor()
    {
        var (_, body) = NewCard(((char)0xE8B7).ToString(), "Explorer perf tuner", "檔案總管效能調校",
            "Run folder windows in a separate process and clear ghost Explorer instances.",
            "用獨立程序開啟資料夾視窗、清走鬼影 Explorer 程序。");

        var report = ResultHost();
        var output = ResultHost();

        var diag = Buttons();
        diag.Children.Add(MakeButton("Check Explorer state", "檢查狀態", ((char)0xE721).ToString(), async () =>
            RenderReport(report, await SystemDoctors.ExplorerStateAsync())));
        body.Children.Add(diag);

        var ops = Buttons();
        ops.Children.Add(MakeButton("Separate process: ON", "獨立程序：開", ((char)0xE73E).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.SetSeparateProcessAsync(true), P("Separate process ON", "獨立程序開"), output)));
        ops.Children.Add(MakeButton("Separate process: OFF", "獨立程序：關", ((char)0xE711).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.SetSeparateProcessAsync(false), P("Separate process OFF", "獨立程序關"), output)));
        ops.Children.Add(MakeButton("Kill ghost Explorers", "清鬼影程序", ((char)0xE74D).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.KillGhostExplorersAsync(), P("Kill ghost Explorers", "清鬼影程序"), output)));
        body.Children.Add(ops);

        body.Children.Add(report);
        body.Children.Add(output);
    }

    // ===================== 7) Icon / thumbnail cache rebuilder =====================

    private void BuildCacheDoctor()
    {
        var (_, body) = NewCard(((char)0xE8B9).ToString(), "Icon & thumbnail cache rebuilder", "圖示與縮圖快取重建",
            "Fix blank, wrong or corrupt icons and thumbnails.", "修復空白、錯誤或損壞嘅圖示同縮圖。");

        var output = ResultHost();
        var btns = Buttons();
        btns.Children.Add(MakeButton("Rebuild icon cache", "重建圖示快取", ((char)0xE72C).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.RebuildIconCacheAsync(), P("Rebuild icon cache", "重建圖示快取"), output)));
        btns.Children.Add(MakeButton("Rebuild thumbnail cache", "重建縮圖快取", ((char)0xE72C).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.RebuildThumbnailCacheAsync(), P("Rebuild thumbnail cache", "重建縮圖快取"), output)));
        body.Children.Add(btns);
        body.Children.Add(output);
    }

    // ===================== 8) Take ownership / reset permissions =====================

    private void BuildOwnershipDoctor()
    {
        var (_, body) = NewCard(((char)0xE72E).ToString(), "Take ownership / reset permissions", "取得擁有權 / 重設權限",
            "Take ownership of a locked file/folder and grant yourself full control — with one-click undo.",
            "對鎖死嘅檔案／資料夾取得擁有權並賦予自己完整控制 — 一鍵還原。");

        var output = ResultHost();

        var pathBox = new TextBox { PlaceholderText = P("Path to a file or folder…", "檔案或資料夾路徑…"), Text = _ownPath };
        pathBox.TextChanged += (_, _) => _ownPath = pathBox.Text;

        var pickRow = new Grid { ColumnSpacing = 8 };
        pickRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pickRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pickRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(pathBox, 0);
        pickRow.Children.Add(pathBox);

        var pickFolder = new Button { Content = P("Browse folder…", "瀏覽資料夾…"), Padding = new Thickness(10, 6, 10, 6) };
        pickFolder.Click += async (_, _) =>
        {
            var f = await PickFolder();
            if (f is not null) { _ownPath = f; pathBox.Text = f; }
        };
        Grid.SetColumn(pickFolder, 1);
        pickRow.Children.Add(pickFolder);

        var pickFile = new Button { Content = P("Browse file…", "瀏覽檔案…"), Padding = new Thickness(10, 6, 10, 6) };
        pickFile.Click += async (_, _) =>
        {
            var f = await PickFile();
            if (f is not null) { _ownPath = f; pathBox.Text = f; }
        };
        Grid.SetColumn(pickFile, 2);
        pickRow.Children.Add(pickFile);
        body.Children.Add(pickRow);

        var recurse = new CheckBox { Content = P("Apply to all contents (recursive)", "套用到所有內容（遞迴）"), IsChecked = true };
        body.Children.Add(recurse);

        var btns = Buttons();
        btns.Children.Add(MakeButton("Take ownership + full control", "取得擁有權＋完整控制", ((char)0xE72E).ToString(), async () =>
        {
            if (string.IsNullOrWhiteSpace(_ownPath)) { ShowResult(false, P("No path", "未選路徑"), P("Pick a file or folder first.", "請先揀檔案或資料夾。")); return; }
            ShowTweakResult(await SystemDoctors.TakeOwnershipAsync(_ownPath, recurse.IsChecked == true), P("Take ownership", "取得擁有權"), output);
        }, destructive: true));
        btns.Children.Add(MakeButton("Undo — reset permissions", "還原 — 重設權限", ((char)0xE7A7).ToString(), async () =>
        {
            if (string.IsNullOrWhiteSpace(_ownPath)) { ShowResult(false, P("No path", "未選路徑"), P("Pick a file or folder first.", "請先揀檔案或資料夾。")); return; }
            ShowTweakResult(await SystemDoctors.ResetPermissionsAsync(_ownPath, recurse.IsChecked == true), P("Reset permissions", "重設權限"), output);
        }));
        body.Children.Add(btns);
        body.Children.Add(output);
    }

    private static async Task<string?> PickFolder()
        => await FileDialogs.OpenFolderAsync();

    private static async Task<string?> PickFile()
        => await FileDialogs.OpenFileAsync();
}
