using System;
using Microsoft.UI.Xaml;
using WinForge.Services;

namespace WinForge;

/// <summary>
/// 應用程式進入點 · Application entry point and global theme handling.
/// </summary>
public partial class App : Application
{
    public static Window? Shell { get; private set; }

    /// <summary>由命令列 "--page &lt;id&gt;" 設定嘅起始頁 · Start page from the command line.</summary>
    public static string? StartPage { get; private set; }

    /// <summary>
    /// 由命令列 "--path &lt;file|folder&gt;" 設定嘅目標路徑 · Target path from the command line.
    /// 由檔案總管右鍵選單嘅 verb 帶入（右鍵揀中嘅檔案／資料夾）。
    /// Passed in by the Explorer right-click verbs (the file/folder that was right-clicked).
    /// 模組可以喺啟動時讀呢個值嚟對住目標執行。Modules can read this at startup to act on the target.
    /// </summary>
    public static string? StartPath { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    private static string? _exportDocsDir;
    private static bool _takeSnapshot;
    private static bool _applyTheme;

    /// <summary>由命令列 "--minimized" 設定：開機自啟動時收入系統匣 · Start hidden in the tray (login startup).</summary>
    public static bool StartMinimized { get; private set; }

    /// <summary>由命令列 "--reactor" 設定：直接開旗艦反應堆 · Open the flagship reactor directly.</summary>
    public static bool StartReactor { get; private set; }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // 全域例外處理：任何模組出錯都唔會冧 app · Install global crash handling first of all.
        CrashLogger.Install(this);
        CrashLogger.Mark("App: OnLaunched start");

        ParseArgs();

        // 無頭模式："Copy as path" 右鍵動作：直接複製路徑入剪貼簿然後退出，唔開視窗。
        // Headless mode: the "Copy as path" right-click verb just copies the path to the clipboard and exits,
        // never showing a window (matches the native shell behaviour).
        if (string.Equals(StartPage, "copypath", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(StartPath))
        {
            try
            {
                var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dp.SetText($"\"{StartPath}\"");      // Explorer's own "Copy as path" wraps in quotes
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
                Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
            }
            catch { /* best effort */ }
            Exit();
            return;
        }

        // 無頭模式：匯出每個功能嘅 Markdown 然後退出 · headless docs export then exit.
        if (_exportDocsDir is not null)
        {
            try
            {
                int n = WinForge.Services.DocsExporter.Export(_exportDocsDir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(_exportDocsDir, "_export_count.txt"), n.ToString());
            }
            catch { /* best effort */ }
            Exit();
            return;
        }

        // 無頭模式：影一個設定快照然後退出（畀每日排程備份用）。
        // Headless mode: take one config snapshot then exit (used by the daily scheduled backup).
        if (_takeSnapshot)
        {
            try { WinForge.Services.ConfigBackupService.TakeSnapshot("scheduled").GetAwaiter().GetResult(); }
            catch { /* best effort */ }
            Exit();
            return;
        }

        // 無頭模式：按 LightSwitch 排程套用主題然後退出（畀每分鐘背景排程工作用）。
        // Headless mode: evaluate the LightSwitch schedule, apply the theme, then exit
        // (used by the per-minute background scheduled task so switching works when the app is closed).
        if (_applyTheme)
        {
            WinForge.Services.LightSwitchService.RunHeadlessApply();
            Exit();
            return;
        }

        CrashLogger.Mark("App: before new MainWindow");
        Shell = new MainWindow();
        CrashLogger.Mark("App: after new MainWindow");

        // 自動啟動對外反應堆狀態 API（具名管道＋記憶體映射），令依賴反應堆嘅其他 app 永遠有得讀。
        // Auto-start the public reactor status API (named pipe + MMF) so dependent apps always have
        // it available even before the reactor page is opened. Default ON; toggle on the reactor page.
        // Exception-safe and self-cleaning (AppDomain.ProcessExit) — never blocks app startup.
        try { WinForge.Services.ReactorStatusApiService.I.Start(); } catch { }

        // --reactor (from the keep-alive launcher) opens the flagship reactor page on launch.
        if (StartReactor) StartPage ??= "reactor";

        // A normal (non-quit) launch clears any stale user-quit flag the watchdog left behind.
        try
        {
            var flag = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinForge", "reactor.userquit");
            if (System.IO.File.Exists(flag)) System.IO.File.Delete(flag);
        }
        catch { /* best effort */ }
        ApplyThemeFromSettings();
        CrashLogger.Mark("App: after ApplyTheme");

        if (StartMinimized && Shell is MainWindow mw)
            mw.StartHiddenInTray();      // login startup → stay in the tray, background services still run
        else
            Shell.Activate();
        CrashLogger.Mark("App: after Activate");

        // 啟動成功 → 解除最早期載入器診斷，免得正常運作時嘅良性 first-chance 例外洗版 crash.log。
        // Startup succeeded → detach the early loader diagnostics so benign first-chance loader exceptions
        // during normal operation don't spam crash.log.
        StartupDiagnostics.Disarm();

        // 其餘背景服務（進階貼上熱鍵、活動追蹤、滑鼠工具覆蓋層）延後到視窗顯示之後先啟動，每個都包住，
        // 避免喺 XAML 初始化嘅脆弱時段同全域掛鈎／覆蓋層競爭而間歇性閃退（stowed exception）。
        // Defer the remaining background services (Advanced Paste hotkey, activity tracking, Mouse Utilities
        // overlays) until after the window is shown — each guarded — so they never race the fragile XAML
        // init, which was causing intermittent stowed-exception crashes at launch.
        var dq = Shell.DispatcherQueue;
        dq.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            try
            {
                AdvancedPasteService.PaletteRequested += () => AdvancedPastePalette.Show(dq);
                if (AdvancedPasteService.HotkeyEnabledSetting)
                    AdvancedPasteService.EnableHotkey(dq);
            }
            catch { /* best effort — never block startup */ }
            // Resume activity tracking if the user had it enabled (privacy default: off).
            try { Services.ActivityTrackerService.I.InitFromPrefs(); } catch { }
            // Restore Mouse Utilities (Find My Mouse / Highlighter / Crosshairs / Jump) if enabled.
            try { Services.MouseUtilsService.LoadSettings(); Services.MouseUtilsService.Sync(); } catch { }
        });
    }

    private static void ParseArgs()
    {
        var argv = Environment.GetCommandLineArgs();
        for (int i = 1; i < argv.Length; i++)
        {
            // Standalone flags (no value).
            if (string.Equals(argv[i], "--snapshot", StringComparison.OrdinalIgnoreCase))
            {
                _takeSnapshot = true;
                continue;
            }
            if (string.Equals(argv[i], "--apply-theme", StringComparison.OrdinalIgnoreCase))
            {
                _applyTheme = true;
                continue;
            }
            if (string.Equals(argv[i], "--minimized", StringComparison.OrdinalIgnoreCase))
            {
                StartMinimized = true;
                continue;
            }
            if (string.Equals(argv[i], "--reactor", StringComparison.OrdinalIgnoreCase))
            {
                StartReactor = true;
                continue;
            }

            // Flags that take the next token as a value.
            if (i >= argv.Length - 1) continue;
            if (string.Equals(argv[i], "--page", StringComparison.OrdinalIgnoreCase))
                StartPage = argv[i + 1].Trim().ToLowerInvariant();
            else if (string.Equals(argv[i], "--path", StringComparison.OrdinalIgnoreCase))
                StartPath = argv[i + 1];      // keep exact case / spaces — it's a filesystem path
            else if (string.Equals(argv[i], "--export-docs", StringComparison.OrdinalIgnoreCase))
                _exportDocsDir = argv[i + 1];
        }
    }

    /// <summary>套用使用者揀選嘅佈景主題 · Apply the user's saved theme to the window root.</summary>
    public static void ApplyThemeFromSettings()
    {
        var theme = SettingsStore.Get("theme", "Default");
        SetTheme(theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        });
    }

    public static void SetTheme(ElementTheme theme)
    {
        if (Shell?.Content is FrameworkElement root)
            root.RequestedTheme = theme;
    }
}
