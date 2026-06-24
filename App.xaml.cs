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

    public App()
    {
        InitializeComponent();
    }

    private static string? _exportDocsDir;
    private static bool _takeSnapshot;
    private static bool _applyTheme;

    /// <summary>由命令列 "--minimized" 設定：開機自啟動時收入系統匣 · Start hidden in the tray (login startup).</summary>
    public static bool StartMinimized { get; private set; }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // 全域例外處理：任何模組出錯都唔會冧 app · Install global crash handling first of all.
        CrashLogger.Install(this);

        ParseArgs();

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

        Shell = new MainWindow();
        ApplyThemeFromSettings();
        // 若用戶上次開咗活動追蹤，喺啟動時自動恢復。
        // Resume activity tracking on startup if the user had it enabled (privacy default: off).
        try { Services.ActivityTrackerService.I.InitFromPrefs(); } catch { }
        if (StartMinimized && Shell is MainWindow mw)
            mw.StartHiddenInTray();      // login startup → stay in the tray, background services still run
        else
            Shell.Activate();
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

            // Flags that take the next token as a value.
            if (i >= argv.Length - 1) continue;
            if (string.Equals(argv[i], "--page", StringComparison.OrdinalIgnoreCase))
                StartPage = argv[i + 1].Trim().ToLowerInvariant();
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
