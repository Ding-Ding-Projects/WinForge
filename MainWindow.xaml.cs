using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Pages;
using WinForge.Services;

namespace WinForge;

public sealed partial class MainWindow : Window
{
    private readonly Dictionary<object, (string en, string zh)> _navOriginalLabels = new();
    private const string AllAppsPickerKey = "shell.allapps";

    public MainWindow()
    {
        CrashLogger.Mark("MW: ctor start");
        InitializeComponent();
        CrashLogger.Mark("MW: after InitializeComponent");
        AppUpdateService.Notice += OnAppUpdateNotice;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        CrashLogger.Mark("MW: after titlebar+icon");

        // 視窗模式（預設，約 82% 螢幕）＋ F11 切換全螢幕，會記住。
        // Windowed by default (~82% of the screen); F11 toggles full screen and the choice is remembered.
        ApplyWindowMode(SettingsStore.Get("fullscreen", "False") == "True");
        var f11 = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = Windows.System.VirtualKey.F11 };
        f11.Invoked += (_, e) => { ToggleFullScreen(); e.Handled = true; };
        RootGrid.KeyboardAccelerators.Add(f11);

        BuildCategoryMenu();
        CrashLogger.Mark("MW: after BuildCategoryMenu");
        BuildTitleMap();
        WireNavigator();
        Loc.I.LanguageChanged += OnLanguageChanged;
        BrandingService.Changed += (_, _) => { try { ApplyLanguageToShell(); } catch { } };
        ApplyLanguageToShell();
        CrashLogger.Mark("MW: after WireNavigator");

        RestoreSessionOrDefault();
        CrashLogger.Mark("MW: after RestoreSessionOrDefault");
        ApplyStartPage();
        CrashLogger.Mark("MW: after ApplyStartPage");

        // Ctrl+T 開新分頁選擇器、Ctrl+W 關閉分頁 · Ctrl+T new-tab app picker, Ctrl+W close tab.
        AddAccel(Windows.System.VirtualKey.T, () => _ = ShowNewTabPickerAsync());
        AddAccel(Windows.System.VirtualKey.W, CloseActiveTab);
        AppState.RepoChanged += (_, _) => SaveSession();

        AppWindow.Closing += OnAppWindowClosing;

        // Title-bar reactor status pill — reflects the live reactor bus (WinForge.dc.html design).
        StartReactorPill();

        // 背景服務（剪貼簿、全域熱鍵泵、ZoomIt、快速重音、快捷鍵指南、指令面板、系統匣）延後到首次版面完成
        // 先啟動，而且每個都用 CrashLogger.Guard 包住——避免喺 XAML 初始化嘅脆弱時段同全域掛鈎／覆蓋層競爭，
        // 以免間歇性 stowed-exception 閃退；亦確保任何單一服務出錯都唔會拖冧開機。
        // Defer background services (clipboard monitor, global hotkey pump, ZoomIt/Command-Palette hotkeys,
        // Quick Accent, Shortcut Guide, tray) until AFTER first layout, each wrapped in CrashLogger.Guard —
        // so global hooks/overlays never race the fragile XAML init (the cause of intermittent
        // stowed-exception crashes at launch) and one faulty service can never abort startup.
        RootGrid.Loaded += StartBackgroundServicesOnce;
    }

    private int _appUpdateNoticeSerial;

    private void OnAppUpdateNotice(AppUpdateService.AppUpdateNotice notice)
    {
        try { DispatcherQueue.TryEnqueue(() => ShowAppUpdateNotice(notice)); } catch { }
    }

    private void ShowAppUpdateNotice(AppUpdateService.AppUpdateNotice notice)
    {
        int serial = ++_appUpdateNoticeSerial;
        AppUpdateBar.Severity = notice.Severity switch
        {
            AppUpdateService.NoticeSeverity.Success => InfoBarSeverity.Success,
            AppUpdateService.NoticeSeverity.Warning => InfoBarSeverity.Warning,
            AppUpdateService.NoticeSeverity.Error => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational,
        };
        AppUpdateBar.Title = Loc.I.Pick(notice.TitleEn, notice.TitleZh);
        AppUpdateBar.Message = Loc.I.Pick(notice.MessageEn, notice.MessageZh);
        AppUpdateBar.IsOpen = true;

        // Show a live progress bar while an update is in flight (the persistent "downloading /
        // installing" notices use AutoDismissMs == 0) so the update never looks silent.
        bool inProgress = notice.AutoDismissMs <= 0
            && notice.Severity == AppUpdateService.NoticeSeverity.Info;
        AppUpdateProgress.Visibility = inProgress ? Visibility.Visible : Visibility.Collapsed;
        AppUpdateProgress.IsIndeterminate = inProgress;

        if (notice.AutoDismissMs > 0)
            _ = AutoDismissAppUpdateNotice(serial, notice.AutoDismissMs);
    }

    private async Task AutoDismissAppUpdateNotice(int serial, int delayMs)
    {
        try { await Task.Delay(delayMs); }
        catch { return; }
        try
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_appUpdateNoticeSerial == serial)
                    AppUpdateBar.IsOpen = false;
            });
        }
        catch { }
    }

    private bool _bgStarted;
    private void StartBackgroundServicesOnce(object sender, RoutedEventArgs e)
    {
        if (_bgStarted) return;
        _bgStarted = true;
        RootGrid.Loaded -= StartBackgroundServicesOnce;
        CrashLogger.Mark("MW: RootGrid.Loaded fired");
        _ = StartBackgroundServicesAsync();
    }

    private async Task StartBackgroundServicesAsync()
    {
        try
        {
            await Task.Delay(75);

            var background = new[]
            {
                StartServiceInBackground("svc: hotkeys", "startup:hotkeys", HotkeyMacroService.StartHotkeys),
                StartServiceInBackground("svc: zoomit", "startup:zoomit", ZoomItService.StartHotkeys),
                StartServiceInBackground("svc: quickaccent", "startup:quickaccent", QuickAccentService.Apply),
            };

            await StartServiceOnUiAsync("svc: clipboard", "startup:clipboard", () => ClipboardService.Start(DispatcherQueue));
            await Task.Delay(75);
            await StartServiceOnUiAsync("svc: shortcutguide", "startup:shortcutguide", () => ShortcutGuideService.Init(DispatcherQueue));
            await Task.Delay(75);
            await StartServiceOnUiAsync("svc: cmdpalette", "startup:cmdpalette", () => CommandPaletteService.Start(DispatcherQueue));
            await Task.Delay(75);
            await StartServiceOnUiAsync("svc: tray", "startup:tray", () => TrayService.Install(ShowFromTray, QuitFromTray, "WinForge · 視窗調校"));

            await Task.WhenAll(background);
            CrashLogger.Mark("svc: all started");
        }
        catch (Exception ex)
        {
            CrashLogger.Log("startup:background-services", ex);
        }
    }

    private static Task StartServiceInBackground(string mark, string source, Action body)
        => Task.Run(() =>
        {
            CrashLogger.Mark(mark);
            CrashLogger.Guard(source, body);
        });

    private Task StartServiceOnUiAsync(string mark, string source, Action body)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                try
                {
                    CrashLogger.Mark(mark);
                    CrashLogger.Guard(source, body);
                }
                finally
                {
                    tcs.TrySetResult();
                }
            }))
        {
            tcs.TrySetResult();
        }
        return tcs.Task;
    }

    private bool _reallyQuit;

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_reallyQuit || !TrayService.IsInstalled)
        {
            CloseDetachedTabs();
            // Genuinely closing → flush all volatile state before the process tears down.
            CrashLogger.Guard("activity:close", () => Services.ActivityTrackerService.I.FlushForShutdown());
            CrashLogger.Guard("persistence:close", () => Services.PersistenceService.I.Flush());
            return;
        }
        args.Cancel = true;       // don't exit — hide to the tray so background work continues
        CrashLogger.Guard("persistence:hide-to-tray", () => Services.PersistenceService.I.Flush());
        AppWindow.Hide();
    }

    private void ShowFromTray()
    {
        AppWindow.Show();
        Activate();
    }

    /// <summary>開機自啟動：唔顯示視窗，淨係坐喺系統匣（背景服務照跑）· Login startup: stay hidden in the tray.</summary>
    public void StartHiddenInTray()
    {
        try { AppWindow.Hide(); } catch { /* tray icon already installed; services already running */ }
    }

    private void QuitFromTray()
    {
        _reallyQuit = true;
        // Reactor keep-alive sentinel: if persistence is on, tell the watchdog this was a deliberate
        // user-quit so it never respawns the reactor. Honouring the quit is mandatory (never unkillable).
        try
        {
            if (WinForge.Services.ReactorPersistence.Enabled)
            {
                var flag = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WinForge", "reactor.userquit");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(flag)!);
                System.IO.File.WriteAllText(flag, DateTime.UtcNow.ToString("o"));
            }
        }
        catch { /* best effort */ }
        try { WinForge.Pages.ReactorWindowManager.CloseAll(); } catch { }
        try { WinForge.Services.ReactorAudioEngine.I.Dispose(); } catch { }
        // Flush volatile state before exiting from the tray (ProcessExit also flushes as a backstop).
        CrashLogger.Guard("activity:quit", () => Services.ActivityTrackerService.I.FlushForShutdown());
        CrashLogger.Guard("persistence:quit", () => Services.PersistenceService.I.Flush());
        TrayService.Remove();
        CloseDetachedTabs();
        Application.Current.Exit();
    }

    private void ApplyWindowMode(bool fullscreen)
    {
        if (fullscreen)
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            return;
        }
        AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        try
        {
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            int w = (int)(area.WorkArea.Width * 0.82);
            int h = (int)(area.WorkArea.Height * 0.86);
            AppWindow.Resize(new Windows.Graphics.SizeInt32(w, h));
            AppWindow.Move(new Windows.Graphics.PointInt32(
                area.WorkArea.X + (area.WorkArea.Width - w) / 2,
                area.WorkArea.Y + (area.WorkArea.Height - h) / 2));
        }
        catch { }
    }

    private void ToggleFullScreen()
    {
        bool full = AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        ApplyWindowMode(!full);
        SettingsStore.Set("fullscreen", (!full).ToString());
    }

    private void ApplyStartPage()
    {
        if (App.StartPage is string sp && sp.StartsWith("search:", StringComparison.OrdinalIgnoreCase))
        {
            var query = sp.Substring("search:".Length);
            NavView.Loaded += (_, _) => DispatcherQueue.TryEnqueue(() =>
            {
                NavView.SelectedItem = null;
                NavigateActive("search:" + query);
            });
            return;
        }
        // Deep-link form: --page "weblogin?url=https://…" opens the in-app login on that URL.
        if (App.StartPage is string wl && wl.StartsWith("weblogin?", StringComparison.OrdinalIgnoreCase))
        {
            Navigator.GoToModule?.Invoke("module.weblogin");
            return;
        }
        switch (App.StartPage)
        {
            case "passwordstrength":
            case "pwstrength":
                Navigator.GoToModule?.Invoke("module.passwordstrength");
                break;
            case "habittracker":
            case "habits":
                Navigator.GoToModule?.Invoke("module.habittracker");
                break;
            case "expensesplit":
            case "splitbill":
                Navigator.GoToModule?.Invoke("module.expensesplit");
                break;
            case "namegen":
            case "namegenerator":
                Navigator.GoToModule?.Invoke("module.namegen");
                break;
            case "countdownevent":
            case "eventcountdown":
                Navigator.GoToModule?.Invoke("module.countdownevent");
                break;
            case "textcolumns":
            case "columns":
                Navigator.GoToModule?.Invoke("module.textcolumns");
                break;
            case "jsonflatten":
            case "flatten":
                Navigator.GoToModule?.Invoke("module.jsonflatten");
                break;
            case "unitprice":
            case "priceper":
                Navigator.GoToModule?.Invoke("module.unitprice");
                break;
            case "markdowntoc":
            case "toc":
                Navigator.GoToModule?.Invoke("module.markdowntoc");
                break;
            case "jsontots":
            case "jsontype":
                Navigator.GoToModule?.Invoke("module.jsontots");
                break;
            case "colorname":
            case "namedcolors":
                Navigator.GoToModule?.Invoke("module.colorname");
                break;
            case "numberformat":
            case "numfmt":
                Navigator.GoToModule?.Invoke("module.numberformat");
                break;
            case "textredact":
            case "redact":
                Navigator.GoToModule?.Invoke("module.textredact");
                break;
            case "stringcompare":
            case "similarity":
                Navigator.GoToModule?.Invoke("module.stringcompare");
                break;
            case "jsonstat":
            case "jsonanalyze":
                Navigator.GoToModule?.Invoke("module.jsonstat");
                break;
            case "aspectratio":
            case "aspect":
                Navigator.GoToModule?.Invoke("module.aspectratio");
                break;
            case "scinotation":
            case "scinot":
                Navigator.GoToModule?.Invoke("module.scinotation");
                break;
            case "colorblind":
            case "cvd":
                Navigator.GoToModule?.Invoke("module.colorblind");
                break;
            case "texttemplate":
            case "template":
                Navigator.GoToModule?.Invoke("module.texttemplate");
                break;
            case "asciitable":
            case "ascii":
                Navigator.GoToModule?.Invoke("module.asciitable");
                break;
            case "metatags":
            case "meta":
                Navigator.GoToModule?.Invoke("module.metatags");
                break;
            case "unixperm":
            case "chmod":
                Navigator.GoToModule?.Invoke("module.unixperm");
                break;
            case "htmlformat":
            case "htmltidy":
                Navigator.GoToModule?.Invoke("module.htmlformat");
                break;
            case "cssformat":
                Navigator.GoToModule?.Invoke("module.cssformat");
                break;
            case "emoji":
                Navigator.GoToModule?.Invoke("module.emoji");
                break;
            case "symbols":
            case "glyphs":
                Navigator.GoToModule?.Invoke("module.symbols");
                break;
            case "binarytext":
            case "textbinary":
                Navigator.GoToModule?.Invoke("module.binarytext");
                break;
            case "textescape":
            case "escape":
                Navigator.GoToModule?.Invoke("module.textescape");
                break;
            case "linetools":
            case "lines":
                Navigator.GoToModule?.Invoke("module.linetools");
                break;
            case "gradient":
                Navigator.GoToModule?.Invoke("module.gradient");
                break;
            case "leet":
            case "fancytext":
                Navigator.GoToModule?.Invoke("module.leet");
                break;
            case "tally":
            case "counter":
                Navigator.GoToModule?.Invoke("module.tallycounter");
                break;
            case "jsondiff":
            case "jsoncompare":
                Navigator.GoToModule?.Invoke("module.jsondiff");
                break;
            case "textdiff":
                Navigator.GoToModule?.Invoke("module.textdiff");
                break;
            case "iniedit":
            case "ini":
                Navigator.GoToModule?.Invoke("module.iniedit");
                break;
            case "imgbase64":
            case "base64img":
                Navigator.GoToModule?.Invoke("module.imgbase64");
                break;
            case "caseconvert":
            case "casing":
                Navigator.GoToModule?.Invoke("module.caseconvert");
                break;
            case "htmltomd":
            case "html2md":
                Navigator.GoToModule?.Invoke("module.htmltomd");
                break;
            case "colorpalette":
            case "palettegen":
                Navigator.GoToModule?.Invoke("module.colorpalette");
                break;
            case "numseq":
            case "sequence":
                Navigator.GoToModule?.Invoke("module.numseq");
                break;
            case "textreplace":
            case "findreplace":
                Navigator.GoToModule?.Invoke("module.textreplace");
                break;
            case "durationcalc":
            case "duration":
                Navigator.GoToModule?.Invoke("module.durationcalc");
                break;
            case "jsonpath":
                Navigator.GoToModule?.Invoke("module.jsonpath");
                break;
            case "sqlformat":
            case "sql":
                Navigator.GoToModule?.Invoke("module.sqlformat");
                break;
            case "xpath":
                Navigator.GoToModule?.Invoke("module.xpathtester");
                break;
            case "strinspect":
            case "stringinspect":
                Navigator.GoToModule?.Invoke("module.stringinspector");
                break;
            case "gitignore":
                Navigator.GoToModule?.Invoke("module.gitignore");
                break;
            case "percent":
            case "percentage":
                Navigator.GoToModule?.Invoke("module.percentcalc");
                break;
            case "loan":
            case "mortgage":
                Navigator.GoToModule?.Invoke("module.loancalc");
                break;
            case "cipher":
            case "ciphers":
                Navigator.GoToModule?.Invoke("module.ciphers");
                break;
            case "checkdigit":
            case "luhn":
                Navigator.GoToModule?.Invoke("module.checkdigit");
                break;
            case "totp":
            case "2fa":
                Navigator.GoToModule?.Invoke("module.totp");
                break;
            case "uuid5":
            case "uuidv5":
                Navigator.GoToModule?.Invoke("module.uuidv5");
                break;
            case "tableformat":
                Navigator.GoToModule?.Invoke("module.tableformat");
                break;
            case "textstats":
                Navigator.GoToModule?.Invoke("module.textstats");
                break;
            case "htmlpreview":
            case "html":
                Navigator.GoToModule?.Invoke("module.htmlpreview");
                break;
            case "recyclebin":
            case "trash":
                Navigator.GoToModule?.Invoke("module.recyclebin");
                break;
            case "filesplit":
                Navigator.GoToModule?.Invoke("module.filesplit");
                break;
            case "encodingconv":
            case "encoding":
                Navigator.GoToModule?.Invoke("module.encodingconv");
                break;
            case "bmi":
            case "health":
                Navigator.GoToModule?.Invoke("module.bmi");
                break;
            case "asciiart":
            case "banner":
                Navigator.GoToModule?.Invoke("module.asciiart");
                break;
            case "mimetype":
            case "mime":
                Navigator.GoToModule?.Invoke("module.mimetypes");
                break;
            case "pathdoctor":
            case "path":
                Navigator.GoToModule?.Invoke("module.pathdoctor");
                break;
            case "subnetcalc":
            case "subnet":
                Navigator.GoToModule?.Invoke("module.subnetcalc");
                break;
            case "ping":
            case "traceroute":
                Navigator.GoToModule?.Invoke("module.ping");
                break;
            case "portscan":
            case "ports":
                Navigator.GoToModule?.Invoke("module.portscan");
                break;
            case "wol":
            case "wakeonlan":
                Navigator.GoToModule?.Invoke("module.wol");
                break;
            case "dnslookup":
            case "dns":
                Navigator.GoToModule?.Invoke("module.dnslookup");
                break;
            case "mactools":
            case "mac":
                Navigator.GoToModule?.Invoke("module.mactools");
                break;
            case "base32":
            case "base58":
                Navigator.GoToModule?.Invoke("module.base32");
                break;
            case "jwtinspect":
            case "jwt":
                Navigator.GoToModule?.Invoke("module.jwtinspect");
                break;
            case "envdiff":
                Navigator.GoToModule?.Invoke("module.envdiff");
                break;
            case "httpheaders":
            case "headers":
                Navigator.GoToModule?.Invoke("module.httpheaders");
                break;
            case "ipinfo":
            case "netinfo":
                Navigator.GoToModule?.Invoke("module.ipinfo");
                break;
            case "cronbuilder":
            case "cron":
                Navigator.GoToModule?.Invoke("module.cronbuilder");
                break;
            case "faker":
            case "lorem":
                Navigator.GoToModule?.Invoke("module.faker");
                break;
            case "csvjson":
            case "csv":
                Navigator.GoToModule?.Invoke("module.csvjson");
                break;
            case "timer":
            case "stopwatch":
            case "pomodoro":
                Navigator.GoToModule?.Invoke("module.timer");
                break;
            case "worldclock":
                Navigator.GoToModule?.Invoke("module.worldclock");
                break;
            case "notes":
            case "scratchpad":
                Navigator.GoToModule?.Invoke("module.notes");
                break;
            case "calculator":
            case "calc":
                Navigator.GoToModule?.Invoke("module.calculator");
                break;
            case "randomizer":
            case "random":
                Navigator.GoToModule?.Invoke("module.randomizer");
                break;
            case "datecalc":
                Navigator.GoToModule?.Invoke("module.datecalc");
                break;
            case "urltools":
            case "url":
                Navigator.GoToModule?.Invoke("module.urltools");
                break;
            case "markdown":
            case "md":
                Navigator.GoToModule?.Invoke("module.markdown");
                break;
            case "numwords":
                Navigator.GoToModule?.Invoke("module.numwords");
                break;
            case "guidgen":
                Navigator.GoToModule?.Invoke("module.guidgen");
                break;
            case "hasher":
                Navigator.GoToModule?.Invoke("module.hasher");
                break;
            case "encoder":
                Navigator.GoToModule?.Invoke("module.encoder");
                break;
            case "jsontools":
                Navigator.GoToModule?.Invoke("module.jsontools");
                break;
            case "regextester":
            case "regex":
                Navigator.GoToModule?.Invoke("module.regextester");
                break;
            case "passgen":
            case "password":
                Navigator.GoToModule?.Invoke("module.passgen");
                break;
            case "texttools":
                Navigator.GoToModule?.Invoke("module.texttools");
                break;
            case "baseconvert":
                Navigator.GoToModule?.Invoke("module.baseconvert");
                break;
            case "epoch":
                Navigator.GoToModule?.Invoke("module.epoch");
                break;
            case "unitconvert":
                Navigator.GoToModule?.Invoke("module.unitconvert");
                break;
            case "charmap":
                Navigator.GoToModule?.Invoke("module.charmap");
                break;
            case "colortools":
                Navigator.GoToModule?.Invoke("module.colortools");
                break;
            case "git":
            case "github":
                Navigator.GoToModule?.Invoke("module.git");
                break;
            case "ai":
            case "aiagents":
            case "claude":
            case "codex":
                Navigator.GoToModule?.Invoke("module.aiagents");
                break;
            case "resume":
            case "cv":
            case "coverletter":
                Navigator.GoToModule?.Invoke("module.resume");
                break;
            case "vscode":
            case "code":
            case "vs":
                Navigator.GoToModule?.Invoke("module.vscode");
                break;
            case "aichat":
            case "chat":
                Navigator.GoToModule?.Invoke("module.aichat");
                break;
            case "cloudflare":
            case "tunnel":
            case "cloudflared":
            case "warp":
                Navigator.GoToModule?.Invoke("module.cloudflare");
                break;
            case "weblogin":
            case "login":
            case "webview":
            case "signin":
                Navigator.GoToModule?.Invoke("module.weblogin");
                break;
            case "camoufox":
            case "antidetect":
            case "fingerprint":
                Navigator.GoToModule?.Invoke("module.camoufox");
                break;
            case "fileserver":
            case "ftpserver":
            case "sftpserver":
            case "hostshare":
                Navigator.GoToModule?.Invoke("module.fileserver");
                break;
            case "ocr":
            case "textocr":
            case "textextractor":
            case "normcap":
                Navigator.GoToModule?.Invoke("module.textocr");
                break;
            case "packer":
            case "image":
            case "imagebuilder":
                Navigator.GoToModule?.Invoke("module.packer");
                break;
            case "webcloner":
            case "clone":
            case "website":
            case "sitecloner":
                Navigator.GoToModule?.Invoke("module.webcloner");
                break;
            case "pgadmin":
            case "postgres":
            case "postgresql":
            case "psql":
                Navigator.GoToModule?.Invoke("module.pgadmin");
                break;
            case "sqlite":
            case "db":
            case "sqlitebrowser":
            case "database":
                Navigator.GoToModule?.Invoke("module.sqlitebrowser");
                break;
            case "filezilla":
            case "ftp":
            case "sftp":
            case "ftps":
                Navigator.GoToModule?.Invoke("module.filezilla");
                break;
            case "dockerssh":
            case "remotedocker":
            case "containers-ssh":
                Navigator.GoToModule?.Invoke("module.dockerssh");
                break;
            case "ollama":
            case "llm":
            case "llama":
                Navigator.GoToModule?.Invoke("module.ollama");
                break;
            case "api":
            case "apiclient":
            case "postman":
            case "rest":
            case "http":
                Navigator.GoToModule?.Invoke("module.apiclient");
                break;
            case "connectors":
            case "connector":
            case "integrations":
                Navigator.GoToModule?.Invoke("module.connectors");
                break;
            case "bitwarden":
            case "bw":
            case "passwords":
                Navigator.GoToModule?.Invoke("module.bitwarden");
                break;
            case "keepass":
            case "kdbx":
            case "pwvault":
                Navigator.GoToModule?.Invoke("module.keepass");
                break;
            case "quicktype":
            case "jsontotype":
            case "codegen":
                Navigator.GoToModule?.Invoke("module.quicktype");
                break;
            case "aws":
            case "awscli":
            case "s3":
            case "ec2":
                Navigator.GoToModule?.Invoke("module.aws");
                break;
            case "peek":
            case "preview":
            case "quicklook":
                Navigator.GoToModule?.Invoke("module.peek");
                break;
            case "archives":
            case "archive":
                Navigator.GoToModule?.Invoke("module.archives");
                break;
            case "media":
                Navigator.GoToModule?.Invoke("module.media");
                break;
            case "audioeditor":
            case "audacity":
                Navigator.GoToModule?.Invoke("module.audioeditor");
                break;
            case "mediaplayer":
            case "player":
            case "vlc":
                Navigator.GoToModule?.Invoke("module.mediaplayer");
                break;
            case "ytdlp":
            case "youtube":
            case "download":
            case "downloader":
                Navigator.GoToModule?.Invoke("module.ytdlp");
                break;
            case "blender":
            case "render":
            case "3d":
                Navigator.GoToModule?.Invoke("module.blender");
                break;
            case "libreoffice":
            case "soffice":
            case "convert":
            case "documents":
                Navigator.GoToModule?.Invoke("module.libreoffice");
                break;
            case "pdf":
            case "pdftoolkit":
            case "stirling":
            case "mergepdf":
                Navigator.GoToModule?.Invoke("module.pdftoolkit");
                break;
            case "regedit":
            case "registry":
                Navigator.GoToModule?.Invoke("module.regedit");
                break;
            case "doctors":
            case "systemdoctors":
            case "doctor":
                Navigator.GoToModule?.Invoke("module.doctors");
                break;
            case "services":
                Navigator.GoToModule?.Invoke("module.services");
                break;
            case "tasks":
            case "scheduledtasks":
                Navigator.GoToModule?.Invoke("module.tasks");
                break;
            case "devices":
                Navigator.GoToModule?.Invoke("module.devices");
                break;
            case "vivetool":
            case "vive":
            case "featureflags":
                Navigator.GoToModule?.Invoke("module.vivetool");
                break;
            case "startup":
                Navigator.GoToModule?.Invoke("module.startup");
                break;
            case "rename":
                Navigator.GoToModule?.Invoke("module.rename");
                break;
            case "bulkops":
            case "bulk":
                Navigator.GoToModule?.Invoke("module.bulkops");
                break;
            case "newplus":
            case "templates":
            case "newfile":
                Navigator.GoToModule?.Invoke("module.newplus");
                break;
            case "duplicates":
            case "dupes":
                Navigator.GoToModule?.Invoke("module.duplicates");
                break;
            case "everything":
            case "search":
            case "filesearch":
            case "find":
                Navigator.GoToModule?.Invoke("module.everything");
                break;
            case "filelocksmith":
            case "lockedfile":
            case "whatslocking":
                Navigator.GoToModule?.Invoke("module.filelocksmith");
                break;
            case "disk":
            case "diskanalyzer":
                Navigator.GoToModule?.Invoke("module.disk");
                break;
            case "drives":
                Navigator.GoToModule?.Invoke("module.drives");
                break;
            case "diskhealth":
            case "smart":
            case "crystaldiskinfo":
            case "diskinfo":
                Navigator.GoToModule?.Invoke("module.diskhealth");
                break;
            case "diskbench":
            case "benchmark":
            case "diskbenchmark":
            case "crystaldiskmark":
            case "cdm":
                Navigator.GoToModule?.Invoke("module.diskbench");
                break;
            case "testdisk":
            case "photorec":
            case "recovery":
            case "recover":
            case "undelete":
                Navigator.GoToModule?.Invoke("module.testdisk");
                break;
            case "uninstall":
            case "apps":
                Navigator.GoToModule?.Invoke("module.uninstall");
                break;
            case "windows":
            case "windowmanager":
                Navigator.GoToModule?.Invoke("module.windows");
                break;
            case "workspaces":
            case "workspace":
            case "applayout":
            case "desktoplayout":
                Navigator.GoToModule?.Invoke("module.workspaces");
                break;
            case "fancyzones":
            case "zones":
            case "powertoys-zones":
                Navigator.GoToModule?.Invoke("module.fancyzones");
                break;
            case "altsnap":
            case "altdrag":
            case "module.altsnap":
                Navigator.GoToModule?.Invoke("module.altsnap");
                break;
            case "komorebi":
            case "tiling":
            case "komorebic":
                Navigator.GoToModule?.Invoke("module.komorebi");
                break;
            case "glazewm":
            case "glaze":
                Navigator.GoToModule?.Invoke("module.glazewm");
                break;
            case "keyboard":
            case "remap":
                Navigator.GoToModule?.Invoke("module.keyboard");
                break;
            case "hotkeys":
            case "hotkey":
            case "macro":
            case "expander":
                Navigator.GoToModule?.Invoke("module.hotkeys");
                break;
            case "quickaccent":
            case "accent":
            case "diacritics":
                Navigator.GoToModule?.Invoke("module.quickaccent");
                break;
            case "shortcutguide":
            case "shortcuts":
            case "winkey":
                Navigator.GoToModule?.Invoke("module.shortcutguide");
                break;
            case "cmdpalette":
            case "commandpalette":
            case "run":
            case "launcher":
            case "palette":
                Navigator.GoToModule?.Invoke("module.cmdpalette");
                break;
            case "hosts":
                Navigator.GoToModule?.Invoke("module.hosts");
                break;
            case "mouse":
                Navigator.GoToModule?.Invoke("module.mouse");
                break;
            case "mwb":
            case "mousewithoutborders":
            case "kvm":
                Navigator.GoToModule?.Invoke("module.mwb");
                break;
            case "recorder":
            case "record":
                Navigator.GoToModule?.Invoke("module.recorder");
                break;
            case "capture":
            case "snip":
            case "screenshot":
                Navigator.GoToModule?.Invoke("module.capture");
                break;
            case "cropandlock":
            case "croplock":
            case "windowcrop":
                Navigator.GoToModule?.Invoke("module.cropandlock");
                break;
            case "giflab":
            case "gif":
            case "screentogif":
                Navigator.GoToModule?.Invoke("module.giflab");
                break;
            case "zoomit":
            case "zoom":
            case "annotate":
                Navigator.GoToModule?.Invoke("module.zoomit");
                break;
            case "monitor":
            case "sysmon":
                Navigator.GoToModule?.Invoke("module.monitor");
                break;
            case "procexp":
            case "processexplorer":
            case "processes":
            case "process":
            case "taskmgr":
            case "systeminformer":
                Navigator.GoToModule?.Invoke("module.procexp");
                break;
            case "battery":
            case "thermal":
            case "powercfg":
            case "module.battery":
                Navigator.GoToModule?.Invoke("module.battery");
                break;
            case "winfetch":
            case "sysinfo":
            case "systeminfo":
            case "neofetch":
            case "fetch":
                Navigator.GoToModule?.Invoke("module.winfetch");
                break;
            case "connections":
            case "netstat":
            case "tcp":
                Navigator.GoToModule?.Invoke("module.connections");
                break;
            case "wireshark":
            case "packetcapture":
            case "pcap":
            case "tshark":
            case "dumpcap":
                Navigator.GoToModule?.Invoke("module.wireshark");
                break;
            case "nmap":
            case "scan":
                Navigator.GoToModule?.Invoke("module.nmap");
                break;
            case "events":
            case "eventlog":
            case "eventviewer":
                Navigator.GoToModule?.Invoke("module.events");
                break;
            case "mixer":
            case "volume":
            case "audio":
                Navigator.GoToModule?.Invoke("module.mixer");
                break;
            case "contextmenu":
            case "rightclick":
                Navigator.GoToModule?.Invoke("module.contextmenu");
                break;
            case "shellmenu":
            case "explorermenu":
            case "winforgemenu":
            case "shellcontextmenu":
                Navigator.GoToModule?.Invoke("module.shellmenu");
                break;
            case "nilesoftshell":
            case "nilesoft":
            case "nss":
            case "module.nilesoftshell":
                Navigator.GoToModule?.Invoke("module.nilesoftshell");
                break;
            case "awake":
                Navigator.GoToModule?.Invoke("module.awake");
                break;
            case "lightswitch":
            case "autotheme":
            case "darkmode":
                Navigator.GoToModule?.Invoke("module.lightswitch");
                break;
            case "colorpicker":
            case "color":
                Navigator.GoToModule?.Invoke("module.colorpicker");
                break;
            case "pixeleditor":
            case "pixel":
            case "aseprite":
            case "sprite":
                Navigator.GoToModule?.Invoke("module.pixeleditor");
                break;
            case "imageeditor":
            case "paint":
            case "photo":
            case "gimp":
                Navigator.GoToModule?.Invoke("module.imageeditor");
                break;
            case "envvars":
            case "env":
                Navigator.GoToModule?.Invoke("module.envvars");
                break;
            case "clipboard":
            case "clip":
                Navigator.GoToModule?.Invoke("module.clipboard");
                break;
            case "advancedpaste":
            case "pastetransform":
            case "smartpaste":
                Navigator.GoToModule?.Invoke("module.advancedpaste");
                break;
            case "ossapps":
            case "opensource":
            case "open-source":
            case "open-source-apps":
            case "apphub":
            case "foss":
                Navigator.GoToModule?.Invoke("module.ossapps");
                break;
            case "packages":
            case "winget":
            case "install":
                Navigator.GoToModule?.Invoke("module.packages");
                break;
            case "adb":
            case "android":
                Navigator.GoToModule?.Invoke("module.adb");
                break;
            case "fastboot":
            case "flasher":
                Navigator.GoToModule?.Invoke("module.fastboot");
                break;
            case "emulator":
            case "avd":
                Navigator.GoToModule?.Invoke("module.emulator");
                break;
            case "vpn":
            case "nordvpn":
            case "tailscale":
                Navigator.GoToModule?.Invoke("module.vpn");
                break;
            case "comms":
            case "communications":
            case "teams":
            case "discord":
            case "telegram":
            case "slack":
                Navigator.GoToModule?.Invoke("module.comms");
                break;
            case "mail":
            case "email":
            case "imap":
            case "thunderbird":
            case "mailclient":
                Navigator.GoToModule?.Invoke("module.mail");
                break;
            case "configbackup":
            case "backup":
            case "config":
                Navigator.GoToModule?.Invoke("module.configbackup");
                break;
            case "native":
            case "pinvoke":
            case "system32":
                Navigator.GoToModule?.Invoke("module.native");
                break;
            case "powertoys":
            case "extras":
            case "imageresizer":
                Navigator.GoToModule?.Invoke("module.powertoys");
                break;
            case "windhawk":
            case "mods":
            case "mod":
                Navigator.GoToModule?.Invoke("module.windhawk");
                break;
            case "wsl":
            case "vm":
            case "sandbox":
                Navigator.GoToModule?.Invoke("module.wslvm");
                break;
            case "virtualbox":
            case "vbox":
            case "vboxmanage":
                Navigator.GoToModule?.Invoke("module.virtualbox");
                break;
            case "proxmox":
            case "pve":
            case "vmhost":
                Navigator.GoToModule?.Invoke("module.proxmox");
                break;
            case "terminal":
            case "wt":
            case "windowsterminal":
            case "conpty":
                Navigator.GoToModule?.Invoke("module.terminal");
                break;
            case "ssh":
            case "scp":
            case "openssh":
            case "module.ssh":
                Navigator.GoToModule?.Invoke("module.ssh");
                break;
            case "onedrive":
                Navigator.GoToModule?.Invoke("module.onedrive");
                break;
            case "richpreview":
            case "previewpane":
            case "filepreview":
                Navigator.GoToModule?.Invoke("module.richpreview");
                break;
            case "time":
            case "timezone":
            case "clock":
            case "unit":
                Navigator.GoToModule?.Invoke("module.timeunit");
                break;
            case "settingshub":
            case "controlpanel":
            case "mssettings":
                Navigator.GoToModule?.Invoke("module.settingshub");
                break;
            case "imaging":
            case "rpi":
            case "raspberrypi":
            case "minecraft":
                Navigator.GoToModule?.Invoke("module.imaging");
                break;
            case "amulet":
            case "worldeditor":
            case "mapeditor":
                Navigator.GoToModule?.Invoke("module.amulet");
                break;
            case "minecraftworldtools":
            case "worldtools":
            case "chunker":
            case "bluemap":
                Navigator.GoToModule?.Invoke("module.minecraftworldtools");
                break;
            case "minecraftserver":
            case "mcserver":
            case "paper":
            case "spigot":
                Navigator.GoToModule?.Invoke("module.minecraftserver");
                break;
            case "minecraftlauncher":
            case "mclauncher":
                Navigator.GoToModule?.Invoke("module.minecraftlauncher");
                break;
            case "viaproxy":
            case "viaversion":
            case "viabackwards":
            case "viarewind":
            case "module.viaproxy":
                Navigator.GoToModule?.Invoke("module.viaproxy");
                break;
            case "voice":
            case "tts":
            case "speak":
                Navigator.GoToModule?.Invoke("module.voice");
                break;
            case "announce":
            case "announcements":
            case "pa":
            case "tts-announce":
                Navigator.GoToModule?.Invoke("module.announcements");
                break;
            case "timelens":
            case "activity":
            case "timeline":
                Navigator.GoToModule?.Invoke("module.timelens");
                break;
            case "fonts":
            case "font":
                Navigator.GoToModule?.Invoke("module.fonts");
                break;
            case "homeassistant":
            case "ha":
            case "smarthome":
                Navigator.GoToModule?.Invoke("module.homeassistant");
                break;
            case "vault":
            case "vault-volumes":
            case "vaultvolumes":
            case "encrypt":
                Navigator.GoToModule?.Invoke("module.vault-volumes");
                break;
            case "qbittorrent":
            case "qbit":
            case "torrents":
                Navigator.GoToModule?.Invoke("module.qbittorrent");
                break;
            case "torrent":
            case "bittorrent":
            case "nativetorrent":
            case "downloads":
                Navigator.GoToModule?.Invoke("module.torrent");
                break;
            case "docker":
            case "containers":
                Navigator.GoToModule?.Invoke("module.docker");
                break;
            case "hex":
            case "hexeditor":
            case "hxd":
            case "binary":
                Navigator.GoToModule?.Invoke("module.hexeditor");
                break;
            case "tags":
            case "audiotags":
            case "mp3tag":
            case "tagging":
                Navigator.GoToModule?.Invoke("module.audiotagger");
                break;
            case "diagram":
            case "drawio":
            case "flowchart":
            case "diagrams":
                Navigator.GoToModule?.Invoke("module.diagram");
                break;
            case "flashcards":
            case "anki":
            case "srs":
            case "study":
                Navigator.GoToModule?.Invoke("module.flashcards");
                break;
            case "decompiler":
            case "ilspy":
            case "dotnetdecompiler":
            case "disassembler":
                Navigator.GoToModule?.Invoke("module.decompiler");
                break;
            case "rss":
            case "feed":
            case "feeds":
            case "feedreader":
            case "reader":
            case "fluentreader":
            case "quiterss":
                Navigator.GoToModule?.Invoke("module.feedreader");
                break;
            case "worldmonitor":
            case "world":
            case "wm":
            case "news":
            case "geopolitics":
                Navigator.GoToModule?.Invoke("module.worldmonitor");
                break;
            case "taskbar":
            case "taskbartweaker":
            case "taskbar-tweaker":
            case "module.taskbar-tweaker":
                Navigator.GoToModule?.Invoke("module.taskbar-tweaker");
                break;
            case "rainmeter":
            case "skins":
            case "widgets":
                Navigator.GoToModule?.Invoke("module.rainmeter");
                break;
            case "rustdesk":
            case "remote":
            case "remotedesktop":
                Navigator.GoToModule?.Invoke("module.rustdesk");
                break;
            case "screenruler":
            case "ruler":
            case "measure":
                Navigator.GoToModule?.Invoke("module.screenruler");
                break;
            case "mouseutils":
            case "mouseutilities":
            case "findmymouse":
            case "crosshairs":
            case "highlighter":
            case "mousehighlighter":
            case "mousejump":
            case "mousecrosshairs":
                Navigator.GoToModule?.Invoke("module.mouseutils");
                break;
            case "cmdnotfound":
            case "commandnotfound":
            case "winget-suggest":
                Navigator.GoToModule?.Invoke("module.cmdnotfound");
                break;
            case "cake":
            case "cakefactory":
            case "bakery":
            case "farm":
            case "ingredients":
                Navigator.GoToModule?.Invoke("module.cakefactory");
                break;
            case "reactor":
            case "nuclear":
            case "npp":
            case "meltdown":
                Navigator.GoToModule?.Invoke("module.reactor");
                break;
            case "module.reactor#startup":
            case "module.reactor#startup-checklist":
            case "reactor#startup":
            case "reactor#startup-checklist":
            case "reactor/startup":
            case "reactor/startup-checklist":
            case "reactor-startup":
            case "reactor-startup-checklist":
            case "reactor-checklist":
            case "startup-checklist":
                Navigator.GoToModule?.Invoke("module.reactor#startup");
                break;
            case "reactorsettings":
            case "reactor-settings":
                Navigator.GoToModule?.Invoke("module.reactorsettings");
                break;
            case "diff":
            case "merge":
            case "winmerge":
            case "compare":
            case "diffmerge":
                Navigator.GoToModule?.Invoke("module.diffmerge");
                break;
            case null:
            case "":
                break;
            case "dashboard":
                NavigateActive("dashboard");
                break;
            case "about":
                NavigateActive("about");
                break;
            case "settings":
                NavigateActive("settings");
                break;
            case "manual":
            case "help":
            case "guide":
            case "userguide":
                NavigateActive("manual");
                break;
            case "licenses":
            case "license":
            case "licence":
            case "osslicenses":
            case "notices":
            case "source":
                NavigateActive("licenses");
                break;
            default:
                var cat = Categories.All.FirstOrDefault(c => c.Id == App.StartPage);
                if (cat is not null)
                    Navigator.GoToCategory?.Invoke(cat);
                break;
        }
    }

    private void BuildCategoryMenu()
    {
        // 將分類收納入可摺疊嘅分組，令導覽唔會太逼。
        // Nest tweak categories under collapsible groups so the pane stays tidy.
        foreach (var cat in Categories.All)
        {
            var parent = cat.Group switch
            {
                "recipes" => RecipesGroup,
                "tools" => ToolsGroup,
                _ => TweaksGroup,
            };
            parent.MenuItems.Add(new NavigationViewItem
            {
                Content = cat.Name.Display,
                Tag = cat.Id,
                Icon = new FontIcon { Glyph = cat.Glyph },
            });
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        BuildTitleMap();
        ApplyLanguageToShell();
        RefreshAllTabHeaders();
    }

    private void ApplyLanguageToShell()
    {
        AppTitleBar.Title = BrandingService.Display;
        try { Title = BrandingService.Display; } catch { }
        SearchBox.PlaceholderText = Loc.I.Pick("Search everything", "搜尋全部");
        RelabelNavItems(NavView.MenuItems);
        RelabelNavItems(NavView.FooterMenuItems);
    }

    private void RelabelNavItems(System.Collections.Generic.IList<object> items)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case NavigationViewItemHeader header:
                    var hp = HeaderPair(header);
                    header.Content = BuildNavHeader(hp.en, hp.zh);
                    break;
                case NavigationViewItem nav:
                    var pair = NavPair(nav);
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(nav, Loc.I.Pick(pair.en, pair.zh));
                    if (nav.Tag is string t0 && !string.IsNullOrWhiteSpace(t0))
                        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(nav, "ShellNavItem_" + AutomationSafeKey(t0));
                    else if (!string.IsNullOrWhiteSpace(nav.Name))
                        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(nav, "ShellNavGroup_" + nav.Name);
                    nav.Content = BuildNavContent(pair.en, pair.zh);
                    // Count badge on collapsible groups (the reference's 17/19/21 numerals).
                    if (nav.MenuItems.Count > 0)
                    {
                        int count = nav.MenuItems.OfType<NavigationViewItem>().Count();
                        if (count > 0)
                            nav.InfoBadge = new Microsoft.UI.Xaml.Controls.InfoBadge { Value = count };
                        RelabelNavItems(nav.MenuItems);
                    }
                    break;
            }
        }
    }

    /// <summary>解析導覽項目嘅 (英,中) 標籤對 · Resolve a nav item's (en, zh) label pair (design = stacked bilingual).</summary>
    private (string en, string zh) NavPair(NavigationViewItem nav)
    {
        var tag = nav.Tag as string;
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var m = ModuleRegistry.All.FirstOrDefault(x => x.Tag == tag);
            if (m is not null) return (m.En, m.Zh);
            var c = Categories.All.FirstOrDefault(x => x.Id == tag);
            if (c is not null) return (c.Name.En, c.Name.Zh);
            switch (tag)
            {
                case "dashboard": return ("Dashboard", "概覽");
                case "manual": return ("Manual", "使用手冊");
                case "licenses": return ("Licenses", "授權");
                case "about": return ("About", "關於");
                case "settings": return ("Settings", "設定");
            }
        }
        if (_navOriginalLabels.TryGetValue(nav, out var cached)) return cached;
        if (nav.Content is string s) { var p = SplitBilingual(s); _navOriginalLabels[nav] = p; return p; }
        return ("", "");
    }

    /// <summary>解析群組標題嘅 (英,中) 對 · Resolve a nav group header's (en, zh) pair.</summary>
    private (string en, string zh) HeaderPair(NavigationViewItemHeader header)
    {
        if (_navOriginalLabels.TryGetValue(header, out var cached)) return cached;
        if (header.Content is string s) { var p = SplitBilingual(s); _navOriginalLabels[header] = p; return p; }
        return ("", "");
    }

    /// <summary>砌個細楷、加字距嘅群組標題（SUITE 套件）· Build the uppercase, letter-spaced group header.</summary>
    private UIElement BuildNavHeader(string en, string zh)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new TextBlock
        {
            Text = en.ToUpperInvariant(),
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            CharacterSpacing = 160,   // ~0.13em letter-spacing (1/1000 em units)
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (!string.IsNullOrEmpty(zh) && zh != en)
            sp.Children.Add(new TextBlock
            {
                Text = zh,
                FontSize = 9,
                Opacity = 0.7,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                VerticalAlignment = VerticalAlignment.Center,
            });
        return sp;
    }

    /// <summary>砌個直疊雙語導覽內容 · Build stacked bilingual nav content (EN over ZH) per the design.</summary>
    private UIElement BuildNavContent(string en, string zh)
    {
        // The reference nav is always stacked-bilingual (EN over ZH); keep that design element in every
        // language mode — Cantonese mode just flips which line is primary. Page content still respects Loc.
        bool zhPrimary = Loc.I.IsCantonesePrimary;
        var primary = zhPrimary ? zh : en;
        var secondary = zhPrimary ? en : zh;
        var sp = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new TextBlock
        {
            Text = string.IsNullOrEmpty(primary) ? secondary : primary,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        });
        if (!string.IsNullOrEmpty(secondary) && secondary != primary)
            sp.Children.Add(new TextBlock
            {
                Text = secondary,
                FontSize = 10,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
            });
        return sp;
    }

    private void ApplyNavAutomation(NavigationViewItem nav)
    {
        if (nav.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
        {
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(nav, "ShellNavItem_" + AutomationSafeKey(tag));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(nav, nav.Content?.ToString() ?? tag);
        }
        else if (!string.IsNullOrWhiteSpace(nav.Name))
        {
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(nav, "ShellNavGroup_" + nav.Name);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(nav, nav.Content?.ToString() ?? nav.Name);
        }
    }

    private static string AutomationSafeKey(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        return new string(chars);
    }

    private string LocalizedNavLabel(object owner, string? tag, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var module = ModuleRegistry.All.FirstOrDefault(m => m.Tag == tag);
            if (module is not null) return Loc.I.Pick(module.En, module.Zh);

            var category = Categories.All.FirstOrDefault(c => c.Id == tag);
            if (category is not null) return category.Name.Display;

            return tag switch
            {
                "dashboard" => Loc.I.Pick("Dashboard", "概覽"),
                "manual" => Loc.I.Pick("Manual", "使用手冊"),
                "licenses" => Loc.I.Pick("Licenses", "授權"),
                "about" => Loc.I.Pick("About", "關於"),
                _ => LocalizeKnownText(owner, fallback),
            };
        }

        return LocalizeKnownText(owner, fallback);
    }

    private string LocalizeKnownText(object owner, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        if (!_navOriginalLabels.TryGetValue(owner, out var parts))
        {
            parts = SplitBilingual(text);
            _navOriginalLabels[owner] = parts;
        }
        return Loc.I.Pick(parts.en, parts.zh);
    }

    private static (string en, string zh) SplitBilingual(string text)
    {
        var marker = text.IndexOf(" · ", StringComparison.Ordinal);
        if (marker < 0) return (text, text);
        return (text[..marker], text[(marker + 3)..]);
    }

    private void WireNavigator()
    {
        Navigator.GoToCategory = cat =>
        {
            var item = FindByTag(cat.Id);
            if (item is not null) NavView.SelectedItem = item;
        };

        Navigator.GoToSettings = () => NavigateActive("settings");
        Navigator.GoToPage = key =>
        {
            var navKey = BaseNavKey(key);
            var item = FindByTag(navKey);
            if (item is not null && !ReferenceEquals(NavView.SelectedItem, item))
            {
                _syncingTabs = true;
                try { NavView.SelectedItem = item; } finally { _syncingTabs = false; }
            }
            NavigateActive(key);
        };

        Navigator.GoToModule = key =>
        {
            var navKey = BaseNavKey(key);
            var item = FindByTag(navKey);
            if (item is not null && !ReferenceEquals(NavView.SelectedItem, item))
            {
                _syncingTabs = true;
                try { NavView.SelectedItem = item; } finally { _syncingTabs = false; }
            }
            NavigateActive(key);
        };
    }

    /// <summary>Resolve a nav item by Tag, searching nested groups recursively (pane + footer).</summary>
    private NavigationViewItem? FindByTag(string tag)
        => FindByTag(NavView.MenuItems, tag) ?? FindByTag(NavView.FooterMenuItems, tag);

    private static NavigationViewItem? FindByTag(System.Collections.Generic.IList<object> items, string tag)
    {
        foreach (var o in items)
        {
            if (o is NavigationViewItem nvi)
            {
                if ((nvi.Tag as string) == tag) return nvi;
                var child = FindByTag(nvi.MenuItems, tag);
                if (child is not null) return child;
            }
        }
        return null;
    }

    // ───────────────────────── reactor status pill (title bar) ─────────────────────────
    private readonly Microsoft.UI.Xaml.DispatcherTimer _reactorPillTimer = new();

    private void StartReactorPill()
    {
        try
        {
            UpdateReactorPill();
            _reactorPillTimer.Interval = TimeSpan.FromSeconds(2);
            _reactorPillTimer.Tick += (_, _) => UpdateReactorPill();
            _reactorPillTimer.Start();
        }
        catch (Exception ex) { CrashLogger.Log("reactorpill.start", ex); }
    }

    private void UpdateReactorPill()
    {
        try
        {
            if (ReactorPillText is null) return;
            var snap = ReactorStatusApiService.I.LastSnapshot;
            string text;
            Windows.UI.Color color;
            if (snap.IsMeltdown) { text = "REACTOR MELTDOWN"; color = Windows.UI.Color.FromArgb(255, 0xFF, 0x5F, 0x5F); }
            else if (snap.IsScrammed) { text = "REACTOR SCRAM"; color = Windows.UI.Color.FromArgb(255, 0xF7, 0xB0, 0x34); }
            else if (snap.IsGenerating) { text = "REACTOR ONLINE"; color = Windows.UI.Color.FromArgb(255, 0x54, 0xE0, 0x7E); }
            else { text = "REACTOR STANDBY"; color = Windows.UI.Color.FromArgb(255, 0xF7, 0xB0, 0x34); }

            ReactorPillText.Text = text;
            var solid = new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
            ReactorPillText.Foreground = solid;
            ReactorPillDot.Fill = solid;
            ReactorPill.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0x40, color.R, color.G, color.B));
            ReactorPill.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0x14, color.R, color.G, color.B));
        }
        catch (Exception ex) { CrashLogger.Log("reactorpill.update", ex); }
    }

    private static Type MapType(string key) => key switch
    {
        "module.reactor" => typeof(ReactorModule),
        "module.reactorsettings" => typeof(ReactorSettingsModule),
        "module.cakefactory" => typeof(CakeFactoryModule),
        "module.git" => typeof(GitHubModule),
        "module.vscode" => typeof(VsCodeModule),
        "module.aiagents" => typeof(AiAgentsModule),
        "module.resume" => typeof(ResumeWriterModule),
        "module.ollama" => typeof(OllamaModule),
        "module.aichat" => typeof(AiChatModule),
        "module.cloudflare" => typeof(CloudflareModule),
        "module.weblogin" => typeof(WebLoginModule),
        "module.ssh" => typeof(SshModule),
        "module.packer" => typeof(PackerModule),
        "module.webcloner" => typeof(WebClonerModule),
        "module.rainmeter" => typeof(RainmeterModule),
        "module.pgadmin" => typeof(PgAdminModule),
        "module.sqlitebrowser" => typeof(SqliteBrowserModule),
        "module.filezilla" => typeof(FileZillaModule),
        "module.fileserver" => typeof(FileServerModule),
        "module.bitwarden" => typeof(BitwardenModule),
        "module.keepass" => typeof(KeePassModule),
        "module.feedreader" => typeof(FeedReaderModule),
        "module.quicktype" => typeof(QuickTypeModule),
        "module.aws" => typeof(AwsCliModule),
        "module.peek" => typeof(PeekModule),
        "module.cmdnotfound" => typeof(CmdNotFoundModule),
        "module.archives" => typeof(ArchivesModule),
        "module.media" => typeof(MediaModule),
        "module.audioeditor" => typeof(AudioEditorModule),
        "module.audiotagger" => typeof(AudioTaggerModule),
        "module.mediaplayer" => typeof(MediaPlayerModule),
        "module.ytdlp" => typeof(YtDlpModule),
        "module.blender" => typeof(BlenderModule),
        "module.libreoffice" => typeof(LibreOfficeModule),
        "module.pdftoolkit" => typeof(PdfToolkitModule),
        "module.regedit" => typeof(RegistryEditor),
        "module.doctors" => typeof(SystemDoctorsModule),
        "module.services" => typeof(ServicesModule),
        "module.tasks" => typeof(ScheduledTasksModule),
        "module.devices" => typeof(DevicesModule),
        "module.vivetool" => typeof(ViveToolModule),
        "module.startup" => typeof(StartupModule),
        "module.rename" => typeof(RenameModule),
        "module.bulkops" => typeof(BulkOpsModule),
        "module.newplus" => typeof(NewPlusModule),
        "module.duplicates" => typeof(DuplicatesModule),
        "module.everything" => typeof(EverythingSearchModule),
        "module.filelocksmith" => typeof(FileLocksmithModule),
        "module.disk" => typeof(DiskAnalyzerModule),
        "module.drives" => typeof(DrivesModule),
        "module.diskhealth" => typeof(DiskHealthModule),
        "module.diskbench" => typeof(DiskBenchmarkModule),
        "module.testdisk" => typeof(TestDiskModule),
        "module.uninstall" => typeof(AppUninstallerModule),
        "module.windows" => typeof(WindowManagerModule),
        "module.workspaces" => typeof(WorkspacesModule),
        "module.altsnap" => typeof(AltSnapModule),
        "module.fancyzones" => typeof(FancyZonesModule),
        "module.komorebi" => typeof(KomorebiModule),
        "module.glazewm" => typeof(GlazeWmModule),
        "module.keyboard" => typeof(KeyboardModule),
        "module.hotkeys" => typeof(HotkeyMacroModule),
        "module.quickaccent" => typeof(QuickAccentModule),
        "module.shortcutguide" => typeof(ShortcutGuideModule),
        "module.cmdpalette" => typeof(CommandPaletteModule),
        "module.hosts" => typeof(HostsEditorModule),
        "module.mouse" => typeof(MouseModule),
        "module.mouseutils" => typeof(MouseUtilsModule),
        "module.mwb" => typeof(MouseWithoutBordersModule),
        "module.recorder" => typeof(ScreenRecorderModule),
        "module.capture" => typeof(CaptureStudioModule),
        "module.textocr" => typeof(TextOcrModule),
        "module.cropandlock" => typeof(CropAndLockModule),
        "module.giflab" => typeof(GifLabModule),
        "module.zoomit" => typeof(ZoomItModule),
        "module.monitor" => typeof(SystemMonitorModule),
        "module.procexp" => typeof(ProcessExplorerModule),
        "module.winfetch" => typeof(WinfetchModule),
        "module.battery" => typeof(BatteryThermalModule),
        "module.connections" => typeof(ConnectionsModule),
        "module.wireshark" => typeof(WiresharkModule),
        "module.nmap" => typeof(NmapModule),
        "module.events" => typeof(EventViewerModule),
        "module.mixer" => typeof(VolumeMixerModule),
        "module.contextmenu" => typeof(ContextMenuModule),
        "module.shellmenu" => typeof(ShellMenuModule),
        "module.taskbar-tweaker" => typeof(TaskbarTweakerModule),
        "module.lightswitch" => typeof(LightSwitchModule),
        "module.nilesoftshell" => typeof(NilesoftShellModule),
        "module.awake" => typeof(AwakeModule),
        "module.colorpicker" => typeof(ColorPickerModule),
        "module.screenruler" => typeof(ScreenRulerModule),
        "module.pixeleditor" => typeof(PixelEditorModule),
        "module.imageeditor" => typeof(ImageEditorModule),
        "module.envvars" => typeof(EnvVarsModule),
        "module.clipboard" => typeof(ClipboardModule),
        "module.advancedpaste" => typeof(AdvancedPasteModule),
        "module.packages" => typeof(PackageManagerModule),
        "module.ossapps" => typeof(OpenSourceAppHubModule),
        "module.adb" => typeof(AndroidAdbModule),
        "module.fastboot" => typeof(FastbootModule),
        "module.emulator" => typeof(EmulatorModule),
        "module.vpn" => typeof(VpnMeshModule),
        "module.rustdesk" => typeof(RustDeskModule),
        "module.homeassistant" => typeof(HomeAssistantModule),
        "module.qbittorrent" => typeof(QBittorrentModule),
        "module.torrent" => typeof(TorrentModule),
        "module.dockerssh" => typeof(DockerSshModule),
        "module.docker" => typeof(DockerModule),
        "module.hexeditor" => typeof(HexEditorModule),
        "module.diagram" => typeof(DiagramEditorModule),
        "module.flashcards" => typeof(FlashcardsModule),
        "module.decompiler" => typeof(DecompilerModule),
        "module.comms" => typeof(CommunicationsModule),
        "module.mail" => typeof(MailModule),
        "module.configbackup" => typeof(ConfigBackupModule),
        "module.native" => typeof(NativeUtilitiesModule),
        "module.powertoys" => typeof(PowerToysExtrasModule),
        "module.windhawk" => typeof(WindhawkModule),
        "module.wslvm" => typeof(WslVmModule),
        "module.virtualbox" => typeof(VirtualBoxModule),
        "module.proxmox" => typeof(ProxmoxModule),
        "module.terminal" => typeof(TerminalModule),
        "module.fonts" => typeof(FontManagerModule),
        "module.onedrive" => typeof(OneDriveModule),
        "module.richpreview" => typeof(RichPreviewModule),
        "module.timeunit" => typeof(TimeUnitModule),
        "module.settingshub" => typeof(SettingsHubModule),
        "module.imaging" => typeof(ImagingGameModule),
        "module.amulet" => typeof(AmuletModule),
        "module.minecraftworldtools" => typeof(MinecraftWorldToolsModule),
        "module.viaproxy" => typeof(ViaProxyModule),
        "module.minecraftserver" => typeof(MinecraftServerModule),
        "module.minecraftlauncher" => typeof(MinecraftLauncherModule),
        "module.voice" => typeof(VoiceModule),
        "module.announcements" => typeof(AnnouncementsModule),
        "module.vault-volumes" => typeof(VaultVolumesModule),
        "module.camoufox" => typeof(CamoufoxModule),
        "module.worldmonitor" => typeof(WorldMonitorModule),
        "module.timelens" => typeof(TimeLensModule),
        "module.diffmerge" => typeof(DiffMergeModule),
        "module.apiclient" => typeof(ApiClientModule),
        "module.connectors" => typeof(ConnectorsModule),
        "module.guidgen" => typeof(GuidGenModule),
        "module.hasher" => typeof(HasherModule),
        "module.encoder" => typeof(EncoderModule),
        "module.jsontools" => typeof(JsonToolsModule),
        "module.regextester" => typeof(RegexTesterModule),
        "module.passgen" => typeof(PassGenModule),
        "module.texttools" => typeof(TextToolsModule),
        "module.baseconvert" => typeof(BaseConvertModule),
        "module.epoch" => typeof(EpochModule),
        "module.unitconvert" => typeof(UnitConvertModule),
        "module.charmap" => typeof(CharMapModule),
        "module.colortools" => typeof(ColorToolsModule),
        "module.cronbuilder" => typeof(CronBuilderModule),
        "module.faker" => typeof(FakerModule),
        "module.csvjson" => typeof(CsvJsonModule),
        "module.timer" => typeof(TimerModule),
        "module.worldclock" => typeof(WorldClockModule),
        "module.notes" => typeof(NotesModule),
        "module.calculator" => typeof(CalculatorModule),
        "module.randomizer" => typeof(RandomizerModule),
        "module.datecalc" => typeof(DateCalcModule),
        "module.urltools" => typeof(UrlToolsModule),
        "module.markdown" => typeof(MarkdownModule),
        "module.numwords" => typeof(NumberWordsModule),
        "module.pathdoctor" => typeof(PathDoctorModule),
        "module.subnetcalc" => typeof(SubnetCalcModule),
        "module.ping" => typeof(PingModule),
        "module.portscan" => typeof(PortScanModule),
        "module.wol" => typeof(WolModule),
        "module.dnslookup" => typeof(DnsLookupModule),
        "module.mactools" => typeof(MacToolsModule),
        "module.base32" => typeof(Base32Module),
        "module.jwtinspect" => typeof(JwtInspectModule),
        "module.envdiff" => typeof(EnvDiffModule),
        "module.httpheaders" => typeof(HttpHeadersModule),
        "module.ipinfo" => typeof(IpInfoModule),
        "module.jsonpath" => typeof(JsonPathModule),
        "module.sqlformat" => typeof(SqlFormatModule),
        "module.xpathtester" => typeof(XPathTesterModule),
        "module.stringinspector" => typeof(StringInspectorModule),
        "module.gitignore" => typeof(GitignoreModule),
        "module.percentcalc" => typeof(PercentCalcModule),
        "module.loancalc" => typeof(LoanCalcModule),
        "module.ciphers" => typeof(CiphersModule),
        "module.checkdigit" => typeof(CheckDigitModule),
        "module.totp" => typeof(TotpModule),
        "module.uuidv5" => typeof(UuidV5Module),
        "module.tableformat" => typeof(TableFormatModule),
        "module.textstats" => typeof(TextStatsModule),
        "module.htmlpreview" => typeof(HtmlPreviewModule),
        "module.recyclebin" => typeof(RecycleBinModule),
        "module.filesplit" => typeof(FileSplitModule),
        "module.encodingconv" => typeof(EncodingConvModule),
        "module.bmi" => typeof(BmiModule),
        "module.asciiart" => typeof(AsciiArtModule),
        "module.mimetypes" => typeof(MimeTypesModule),
        "module.caseconvert" => typeof(CaseConvertModule),
        "module.htmltomd" => typeof(HtmlToMdModule),
        "module.colorpalette" => typeof(ColorPaletteModule),
        "module.numseq" => typeof(NumSeqModule),
        "module.textreplace" => typeof(TextReplaceModule),
        "module.durationcalc" => typeof(DurationCalcModule),
        "module.jsondiff" => typeof(JsonDiffModule),
        "module.textdiff" => typeof(TextDiffModule),
        "module.iniedit" => typeof(IniEditModule),
        "module.imgbase64" => typeof(ImgBase64Module),
        "module.textescape" => typeof(TextEscapeModule),
        "module.linetools" => typeof(LineToolsModule),
        "module.gradient" => typeof(GradientModule),
        "module.leet" => typeof(LeetModule),
        "module.tallycounter" => typeof(TallyCounterModule),
        "module.htmlformat" => typeof(HtmlFormatModule),
        "module.cssformat" => typeof(CssFormatModule),
        "module.emoji" => typeof(EmojiModule),
        "module.symbols" => typeof(SymbolsModule),
        "module.binarytext" => typeof(BinaryTextModule),
        "module.texttemplate" => typeof(TextTemplateModule),
        "module.asciitable" => typeof(AsciiTableModule),
        "module.metatags" => typeof(MetaTagsModule),
        "module.unixperm" => typeof(UnixPermModule),
        "module.jsonstat" => typeof(JsonStatModule),
        "module.aspectratio" => typeof(AspectRatioModule),
        "module.scinotation" => typeof(SciNotationModule),
        "module.colorblind" => typeof(ColorBlindModule),
        "module.markdowntoc" => typeof(MarkdownTocModule),
        "module.jsontots" => typeof(JsonToTsModule),
        "module.colorname" => typeof(ColorNameModule),
        "module.numberformat" => typeof(NumberFormatModule),
        "module.textredact" => typeof(TextRedactModule),
        "module.stringcompare" => typeof(StringCompareModule),
        "module.passwordstrength" => typeof(PasswordStrengthModule),
        "module.habittracker" => typeof(HabitTrackerModule),
        "module.expensesplit" => typeof(ExpenseSplitModule),
        "module.namegen" => typeof(NameGenModule),
        "module.countdownevent" => typeof(CountdownEventModule),
        "module.textcolumns" => typeof(TextColumnsModule),
        "module.jsonflatten" => typeof(JsonFlattenModule),
        "module.unitprice" => typeof(UnitPriceModule),
        _ => typeof(DashboardPage),
    };

    private static string BaseNavKey(string key)
    {
        var hash = key.IndexOf('#');
        return hash >= 0 ? key[..hash] : key;
    }

    private static string? NavFragment(string key)
    {
        var hash = key.IndexOf('#');
        if (hash < 0 || hash == key.Length - 1) return null;
        var fragment = key[(hash + 1)..].Trim();
        return fragment.Length == 0 ? null : fragment;
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        var q = sender.Text ?? "";
        if (q.Trim().Length == 0) { sender.ItemsSource = null; return; }
        var suggestions = ShellSearchSuggestions(q).Take(10).ToList();
        sender.ItemsSource = suggestions.Count == 0 ? null : suggestions;
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is ShellSearchSuggestion suggestion)
        {
            OpenShellSearchSuggestion(suggestion);
            sender.Text = string.Empty;
            sender.ItemsSource = null;
            return;
        }

        var q = args.QueryText;
        if (!string.IsNullOrWhiteSpace(q)) NavigateActive("search:" + q);
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (ActiveFrame is { CanGoBack: true } f) { f.GoBack(); UpdateBackButton(); }
    }

    private async void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_syncingTabs) return;
        if (args.IsSettingsSelected) { NavigateActive("settings"); return; }
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            if (string.Equals(tag, AllAppsPickerKey, StringComparison.OrdinalIgnoreCase))
            {
                await OpenAllAppsPickerFromShellAsync();
                return;
            }

            NavigateActive(tag);
        }
    }

    // ===================== Browser-style tabs · 瀏覽器式分頁 =====================
    // Each tab owns its own navigation Frame. Tab metadata now includes grouping,
    // colors, custom names, font settings, and the local Git repo context.

    private bool _syncingTabs;
    private bool _restoring;
    private readonly Dictionary<Type, string> _titles = new();
    private readonly List<TabSessionService.TabGroupData> _tabGroups = new();

    private static readonly (string Name, string Hex)[] TabPalette =
    {
        ("Blue", "#FF0078D4"),
        ("Teal", "#FF038387"),
        ("Green", "#FF107C10"),
        ("Amber", "#FFFFB900"),
        ("Orange", "#FFD83B01"),
        ("Red", "#FFE81123"),
        ("Magenta", "#FFE3008C"),
        ("Purple", "#FF5C2D91"),
        ("Slate", "#FF68768A"),
    };

    private sealed class TabAppearanceEditor
    {
        public TextBox NameBox { get; init; } = new();
        public TextBox ColorBox { get; init; } = new();
        public ComboBox? GroupBox { get; init; }
        public TextBox FontFamilyBox { get; init; } = new();
        public TextBox FontSizeBox { get; init; } = new();
        public ComboBox WeightBox { get; init; } = new();
        public ComboBox StyleBox { get; init; } = new();
        public TextBox FontColorBox { get; init; } = new();
        public TextBox? RepoPathBox { get; init; }
    }

    private sealed class NewTabEntry
    {
        public string Key { get; init; } = "";
        public string Title { get; init; } = "";
        public string Subtitle { get; init; } = "";
        public string Glyph { get; init; } = "";
        public string CategoryId { get; init; } = "all";
    }

    private sealed class PickerCategory
    {
        public string Id { get; init; } = "";
        public string En { get; init; } = "";
        public string Zh { get; init; } = "";
        public string[] Keys { get; init; } = Array.Empty<string>();

        public string Title => Loc.I.Pick(En, Zh);
    }

    private sealed class ShellSearchSuggestion
    {
        public string Key { get; init; } = "";
        public string Title { get; init; } = "";
        public string Subtitle { get; init; } = "";

        public override string ToString() => Title;
    }

    private const string RecentModulesKey = "shell.recentModules";

    private static readonly string[] DefaultFrequentTabKeys =
    {
        "dashboard",
        "module.reactor",
        "module.git",
        "module.vscode",
        "module.packages",
        "module.monitor",
        "module.textocr",
        "module.terminal",
    };

    private static readonly string[] SuggestedTabKeys =
    {
        "module.apiclient",
        "module.connectors",
        "module.aichat",
        "module.keepass",
        "module.hexeditor",
        "module.worldmonitor",
        "module.docker",
        "module.settingshub",
        "module.feedreader",
    };

    private static readonly PickerCategory[] PickerCategories =
    {
        new() { Id = "all", En = "All apps", Zh = "所有 app" },
        new()
        {
            Id = "reactor",
            En = "Reactor suite",
            Zh = "反應堆套件",
            Keys = new[] { "module.reactor", "module.reactorsettings", "module.cakefactory" },
        },
        new()
        {
            Id = "files",
            En = "Files & disks",
            Zh = "檔案與磁碟",
            Keys = new[]
            {
                "module.peek", "module.newplus", "module.archives", "module.bulkops", "module.rename",
                "module.duplicates", "module.everything", "module.filelocksmith", "module.diffmerge",
                "module.hexeditor", "module.disk", "module.drives", "module.diskhealth", "module.diskbench",
                "module.testdisk", "module.onedrive", "module.richpreview",
            },
        },
        new()
        {
            Id = "system",
            En = "System",
            Zh = "系統",
            Keys = new[]
            {
                "module.doctors", "module.services", "module.tasks", "module.devices", "module.vivetool",
                "module.regedit", "module.startup", "module.events", "module.monitor", "module.procexp",
                "module.winfetch", "module.battery", "module.connections", "module.wireshark", "module.nmap",
                "module.native", "module.envvars", "module.clipboard", "module.settingshub",
            },
        },
        new()
        {
            Id = "media",
            En = "Media & capture",
            Zh = "媒體與擷取",
            Keys = new[]
            {
                "module.media", "module.audioeditor", "module.audiotagger", "module.mediaplayer", "module.ytdlp",
                "module.blender", "module.libreoffice", "module.pdftoolkit", "module.recorder", "module.capture",
                "module.textocr", "module.cropandlock", "module.giflab", "module.zoomit", "module.mixer",
                "module.colorpicker", "module.screenruler", "module.pixeleditor", "module.imageeditor",
                "module.timeunit", "module.timelens",
            },
        },
        new()
        {
            Id = "tweaks",
            En = "Tweaks & input",
            Zh = "調校與輸入",
            Keys = new[]
            {
                "module.hosts", "module.mouse", "module.mouseutils", "module.mwb", "module.keyboard",
                "module.hotkeys", "module.quickaccent", "module.shortcutguide", "module.cmdpalette",
                "module.contextmenu", "module.shellmenu", "module.taskbar-tweaker", "module.lightswitch",
                "module.nilesoftshell", "module.windows", "module.workspaces", "module.altsnap",
                "module.fancyzones", "module.komorebi", "module.glazewm", "module.fonts", "module.awake",
                "module.advancedpaste", "module.powertoys", "module.windhawk", "module.voice",
                "module.announcements", "module.rainmeter",
            },
        },
        new()
        {
            Id = "apps",
            En = "Apps & Git",
            Zh = "程式與 Git",
            Keys = new[]
            {
                "module.packages", "module.ossapps", "module.adb", "module.fastboot", "module.emulator",
                "module.vpn", "module.qbittorrent", "module.torrent", "module.dockerssh", "module.docker",
                "module.diagram", "module.flashcards", "module.rustdesk", "module.homeassistant",
                "module.comms", "module.mail", "module.wslvm", "module.virtualbox", "module.proxmox",
                "module.terminal", "module.uninstall", "module.imaging", "module.amulet",
                "module.minecraftworldtools", "module.viaproxy", "module.minecraftserver", "module.minecraftlauncher", "module.git",
                "module.vscode", "module.aiagents", "module.resume", "module.ollama", "module.aichat",
                "module.cloudflare", "module.weblogin", "module.ssh", "module.apiclient", "module.connectors", "module.packer",
                "module.worldmonitor", "module.webcloner", "module.pgadmin", "module.sqlitebrowser",
                "module.filezilla", "module.fileserver", "module.feedreader", "module.quicktype", "module.decompiler",
                "module.aws", "module.cmdnotfound", "module.configbackup",
            },
        },
        new()
        {
            Id = "security",
            En = "Security & privacy",
            Zh = "安全與私隱",
            Keys = new[] { "module.vault-volumes", "module.bitwarden", "module.keepass", "module.camoufox" },
        },
        new()
        {
            Id = "dev",
            En = "Developer tools",
            Zh = "開發者工具",
            Keys = new[]
            {
                "module.guidgen", "module.hasher", "module.encoder", "module.jsontools", "module.regextester",
                "module.passgen", "module.texttools", "module.baseconvert", "module.epoch", "module.unitconvert",
                "module.charmap", "module.colortools",
            },
        },
        new()
        {
            Id = "utility",
            En = "Utilities & time",
            Zh = "實用與時間",
            Keys = new[]
            {
                "module.cronbuilder", "module.faker", "module.csvjson", "module.timer", "module.worldclock",
                "module.notes", "module.calculator", "module.randomizer", "module.datecalc", "module.urltools",
                "module.markdown", "module.numwords",
            },
        },
        new()
        {
            Id = "network",
            En = "Network & dev",
            Zh = "網絡與開發",
            Keys = new[]
            {
                "module.subnetcalc", "module.ping", "module.portscan", "module.wol", "module.dnslookup",
                "module.mactools", "module.httpheaders", "module.ipinfo", "module.pathdoctor", "module.envdiff",
                "module.base32", "module.jwtinspect",
            },
        },
        new()
        {
            Id = "devformat",
            En = "Dev & format",
            Zh = "開發與格式",
            Keys = new[]
            {
                "module.jsonpath", "module.sqlformat", "module.xpathtester", "module.stringinspector",
                "module.uuidv5", "module.tableformat", "module.checkdigit", "module.totp", "module.ciphers",
                "module.gitignore", "module.encodingconv", "module.htmlpreview",
            },
        },
        new()
        {
            Id = "calctext",
            En = "Calculators & text",
            Zh = "計算與文字",
            Keys = new[]
            {
                "module.percentcalc", "module.loancalc", "module.bmi", "module.textstats", "module.asciiart",
                "module.mimetypes", "module.recyclebin", "module.filesplit",
            },
        },
        new()
        {
            Id = "moretools",
            En = "More tools",
            Zh = "更多工具",
            Keys = new[]
            {
                "module.caseconvert", "module.htmltomd", "module.textreplace", "module.numseq",
                "module.durationcalc", "module.colorpalette",
                "module.jsondiff", "module.textdiff", "module.iniedit", "module.imgbase64",
                "module.textescape", "module.linetools", "module.leet", "module.gradient", "module.tallycounter",
            },
        },
        new()
        {
            Id = "webtext",
            En = "Web, text & symbols",
            Zh = "網頁文字符號",
            Keys = new[]
            {
                "module.htmlformat", "module.cssformat", "module.binarytext", "module.emoji", "module.symbols",
                "module.metatags", "module.texttemplate", "module.asciitable", "module.unixperm",
                "module.jsonstat", "module.aspectratio", "module.scinotation", "module.colorblind",
            },
        },
        new()
        {
            Id = "textcode",
            En = "Text & code",
            Zh = "文字與程式碼",
            Keys = new[]
            {
                "module.markdowntoc", "module.jsontots", "module.numberformat", "module.stringcompare",
                "module.textredact", "module.colorname",
            },
        },
        new()
        {
            Id = "productivity",
            En = "Productivity & life",
            Zh = "生產力與生活",
            Keys = new[]
            {
                "module.habittracker", "module.countdownevent", "module.expensesplit", "module.unitprice",
                "module.namegen", "module.passwordstrength", "module.textcolumns", "module.jsonflatten",
            },
        },
    };

    private Frame? ActiveFrame => (Tabs?.SelectedItem as TabViewItem)?.Content as Frame;

    private void AddAccel(Windows.System.VirtualKey key, Action action)
    {
        var a = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = key,
            Modifiers = Windows.System.VirtualKeyModifiers.Control,
        };
        a.Invoked += (_, e) => { action(); e.Handled = true; };
        RootGrid.KeyboardAccelerators.Add(a);
    }

    private async Task ShowNewTabPickerAsync()
    {
        string? selectedKey = null;
        ContentDialog? dialog = null;
        var xamlSize = RootGrid.XamlRoot?.Size ?? new Windows.Foundation.Size(760, 760);
        var pickerWidth = Math.Clamp(xamlSize.Width - 96, 420, 760);
        var pickerScrollerHeight = Math.Clamp(xamlSize.Height - 330, 260, 520);

        var root = new StackPanel
        {
            Spacing = 12,
            MinWidth = 420,
            MaxWidth = 760,
            Width = pickerWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var search = new TextBox
        {
            PlaceholderText = Loc.I.Pick("Search all apps and tools", "搜尋所有 app 同工具"),
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(search, "NewTabPickerSearchBox");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(search, Loc.I.Pick("Search all apps and tools", "搜尋所有 app 同工具"));
        root.Children.Add(search);

        var categoryBox = new ComboBox
        {
            Header = Loc.I.Pick("Category", "分類"),
            ItemsSource = PickerCategories,
            DisplayMemberPath = nameof(PickerCategory.Title),
            SelectedIndex = 0,
            MinWidth = 220,
            MaxWidth = 360,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(categoryBox, "NewTabPickerCategoryBox");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(categoryBox, Loc.I.Pick("Filter by category", "按分類篩選"));
        root.Children.Add(categoryBox);

        var results = new StackPanel { Spacing = 12 };
        var renderedButtons = new List<Button>();
        var scroller = new ScrollViewer
        {
            Content = results,
            MaxHeight = pickerScrollerHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        root.Children.Add(scroller);

        void Open(string key)
        {
            selectedKey = key;
            dialog?.Hide();
        }

        void Render(string query)
        {
            results.Children.Clear();
            renderedButtons.Clear();
            query = (query ?? string.Empty).Trim();
            var selectedCategory = categoryBox.SelectedItem as PickerCategory ?? PickerCategories[0];
            var entries = FilterEntriesForCategory(AllPickerEntries(), selectedCategory);

            if (query.Length == 0)
            {
                if (selectedCategory.Id == "all")
                {
                    AddPickerSection(results, Loc.I.Pick("Frequently used", "常用"), FrequentEntries(), Open, renderedButtons);
                    AddPickerSection(results, Loc.I.Pick("You may like", "你可能會用"), EntriesFor(SuggestedTabKeys), Open, renderedButtons);
                    AddPickerSection(results, Loc.I.Pick("All apps", "所有 app"), entries.Take(24), Open, renderedButtons);
                }
                else
                {
                    AddPickerSection(results, selectedCategory.Title, entries, Open, renderedButtons);
                }
                return;
            }

            AddPickerSection(results,
                selectedCategory.Id == "all"
                    ? Loc.I.Pick("Search results", "搜尋結果")
                    : Loc.I.Pick($"{selectedCategory.En} search results", $"{selectedCategory.Zh}搜尋結果"),
                entries
                    .Where(e => MatchesPickerEntry(e, query))
                    .Take(40),
                Open,
                renderedButtons);
        }

        search.TextChanged += (_, _) => Render(search.Text);
        search.KeyDown += (_, e) =>
        {
            if (e.Key != Windows.System.VirtualKey.Enter || renderedButtons.Count == 0) return;
            if (renderedButtons[0].Tag is string key)
            {
                e.Handled = true;
                Open(key);
            }
        };
        categoryBox.SelectionChanged += (_, _) => Render(search.Text);
        Render(string.Empty);

        dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = Loc.I.Pick("Open new tab", "開新分頁"),
            Content = root,
            CloseButtonText = Loc.I.Pick("Cancel", "取消"),
            DefaultButton = ContentDialogButton.None,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(dialog, "NewTabPickerDialog");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(dialog, Loc.I.Pick("Open new tab", "開新分頁"));
        dialog.Opened += (_, _) => search.Focus(FocusState.Programmatic);

        await dialog.ShowAsync();
        if (!string.IsNullOrWhiteSpace(selectedKey))
        {
            RememberAppUse(selectedKey);
            AddTab(selectedKey);
        }
    }

    private async Task OpenAllAppsPickerFromShellAsync()
    {
        await ShowNewTabPickerAsync();
        SyncNavSelectionToActiveTab();
    }

    private void AddPickerSection(StackPanel target, string title, IEnumerable<NewTabEntry> entries, Action<string> open, IList<Button>? renderedButtons = null)
    {
        var list = entries
            .GroupBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        if (list.Count == 0) return;

        var header = new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 0),
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHeadingLevel(header, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level3);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(header, title);
        target.Children.Add(header);

        var buttons = list.Select(e => PickerButton(e, open)).ToList();
        if (renderedButtons is not null)
        {
            foreach (var button in buttons) renderedButtons.Add(button);
        }

        var grid = new ItemsRepeater
        {
            Layout = new UniformGridLayout
            {
                MinItemWidth = 260,
                MinItemHeight = 82,
                MinColumnSpacing = 6,
                MinRowSpacing = 6,
            },
            ItemsSource = buttons,
        };
        target.Children.Add(grid);
    }

    private Button PickerButton(NewTabEntry entry, Action<string> open)
    {
        var grid = new Grid
        {
            ColumnSpacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new FontIcon
        {
            Glyph = string.IsNullOrWhiteSpace(entry.Glyph) ? ((char)0xE8B7).ToString() : entry.Glyph,
            FontSize = 20,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = entry.Title,
            FontWeight = FontWeights.SemiBold,
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.WrapWholeWords,
        });
        text.Children.Add(new TextBlock
        {
            Text = entry.Subtitle,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.WrapWholeWords,
        });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var button = new Button
        {
            Content = grid,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = 82,
            Padding = new Thickness(12, 9, 12, 9),
            Tag = entry.Key,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(button, "NewTabPickerItem_" + AutomationSafeKey(entry.Key));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, $"{entry.Title} - {entry.Subtitle}");
        ToolTipService.SetToolTip(button, entry.Title);
        button.Click += (_, _) => open(entry.Key);
        return button;
    }

    private IEnumerable<NewTabEntry> FrequentEntries()
    {
        var recent = SettingsStore.Get(RecentModulesKey, "")
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return EntriesFor(recent.Concat(DefaultFrequentTabKeys)).Take(12);
    }

    private IEnumerable<NewTabEntry> EntriesFor(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            var entry = EntryFor(key);
            if (entry is not null) yield return entry;
        }
    }

    private IEnumerable<NewTabEntry> AllPickerEntries()
    {
        yield return new NewTabEntry
        {
            Key = "dashboard",
            Title = Loc.I.Pick("Dashboard", "概覽"),
            Subtitle = Loc.I.Pick("Home, search, and suggestions", "首頁、搜尋同建議"),
            Glyph = ((char)0xE80F).ToString(),
            CategoryId = "all",
        };
        yield return new NewTabEntry
        {
            Key = "settings",
            Title = Loc.I.Pick("Settings", "設定"),
            Subtitle = Loc.I.Pick("Language, theme, and app preferences", "語言、主題同 app 偏好"),
            Glyph = ((char)0xE713).ToString(),
            CategoryId = "all",
        };
        foreach (var module in ModuleRegistry.All.Where(m => m.Tag.StartsWith("module.", StringComparison.Ordinal)))
            yield return EntryFor(module)!;
    }

    private NewTabEntry? EntryFor(string key)
    {
        if (string.Equals(key, "dashboard", StringComparison.OrdinalIgnoreCase))
            return AllPickerEntries().First();
        if (string.Equals(key, "settings", StringComparison.OrdinalIgnoreCase))
            return AllPickerEntries().Skip(1).First();

        var module = ModuleRegistry.All.FirstOrDefault(m => string.Equals(m.Tag, key, StringComparison.OrdinalIgnoreCase));
        return module is null ? null : EntryFor(module);
    }

    private NewTabEntry EntryFor(ModuleInfo module)
        => new()
        {
            Key = module.Tag,
            Title = $"{module.En} · {module.Zh}",
            Subtitle = module.RequiresReactor
                ? Loc.I.Pick(module.ReactorRequirementBadge, module.ReactorRequirementBadge)
                : module.Keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(6).DefaultIfEmpty(module.En).Aggregate((a, b) => $"{a} {b}"),
            Glyph = module.Glyph,
            CategoryId = PickerCategoryIdFor(module.Tag),
        };

    private static bool MatchesPickerEntry(NewTabEntry entry, string query)
    {
        var category = PickerCategories.FirstOrDefault(c => c.Id == entry.CategoryId);
        var haystack = $"{entry.Title} {entry.Subtitle} {entry.Key} {category?.En} {category?.Zh}".ToLowerInvariant();
        return haystack.Contains(query.ToLowerInvariant());
    }

    private static IEnumerable<NewTabEntry> FilterEntriesForCategory(IEnumerable<NewTabEntry> entries, PickerCategory category)
        => category.Id == "all"
            ? entries
            : entries.Where(e => string.Equals(e.CategoryId, category.Id, StringComparison.OrdinalIgnoreCase));

    private static string PickerCategoryIdFor(string key)
    {
        foreach (var category in PickerCategories)
        {
            if (category.Id == "all") continue;
            if (category.Keys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
                return category.Id;
        }

        return "apps";
    }

    private IEnumerable<ShellSearchSuggestion> ShellSearchSuggestions(string query)
    {
        query = (query ?? string.Empty).Trim();
        if (query.Length == 0) yield break;

        if (MatchesText(Loc.I.Pick("All Apps", "所有 app"), query) || MatchesText("apps tools modules search picker", query))
        {
            yield return new ShellSearchSuggestion
            {
                Key = AllAppsPickerKey,
                Title = Loc.I.Pick("All Apps · 所有 app", "所有 app · All Apps"),
                Subtitle = Loc.I.Pick("Open the searchable app picker", "開啟可搜尋 app 選擇器"),
            };
        }

        foreach (var module in ModuleRegistry.Search(query)
                     .Where(m => m.Tag.StartsWith("module.", StringComparison.Ordinal))
                     .Take(6))
        {
            yield return new ShellSearchSuggestion
            {
                Key = module.Tag,
                Title = $"{module.En} · {module.Zh}",
                Subtitle = Loc.I.Pick("Open module", "開啟模組"),
            };
        }

        foreach (var category in Categories.All
                     .Where(c => MatchesText($"{c.Name.En} {c.Name.Zh} {c.Id}", query))
                     .Take(3))
        {
            yield return new ShellSearchSuggestion
            {
                Key = category.Id,
                Title = category.Name.Display,
                Subtitle = Loc.I.Pick("Open tweak category", "開啟調校分類"),
            };
        }
    }

    private void OpenShellSearchSuggestion(ShellSearchSuggestion suggestion)
    {
        if (string.Equals(suggestion.Key, AllAppsPickerKey, StringComparison.OrdinalIgnoreCase))
        {
            _ = OpenAllAppsPickerFromShellAsync();
            return;
        }

        NavigateActive(suggestion.Key);
    }

    private static bool MatchesText(string text, string query)
        => text.Contains(query, StringComparison.OrdinalIgnoreCase);

    private void SyncNavSelectionToActiveTab()
    {
        var key = Tabs?.SelectedItem is TabViewItem tab ? BaseNavKey(DataOf(tab).Key) : "dashboard";
        var item = FindByTag(key);
        if (item is null) return;

        _syncingTabs = true;
        try { NavView.SelectedItem = item; }
        finally { _syncingTabs = false; }
    }

    private void RememberAppUse(string key)
    {
        if (!key.StartsWith("module.", StringComparison.OrdinalIgnoreCase) && key != "dashboard" && key != "settings")
            return;

        var keys = SettingsStore.Get(RecentModulesKey, "")
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(k => !string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
            .Prepend(key)
            .Take(16);
        SettingsStore.Set(RecentModulesKey, string.Join("|", keys));
    }

    private void BuildTitleMap()
    {
        _titles.Clear();
        _titles[typeof(DashboardPage)] = Loc.I.Pick("Dashboard", "概覽");
        _titles[typeof(AboutPage)] = Loc.I.Pick("About", "關於");
        _titles[typeof(SettingsPage)] = Loc.I.Pick("Settings", "設定");
        _titles[typeof(SearchResultsPage)] = Loc.I.Pick("Search", "搜尋");
        _titles[typeof(ManualPage)] = Loc.I.Pick("Manual", "使用手冊");
        _titles[typeof(LicensesPage)] = Loc.I.Pick("Licenses", "授權");
        _titles[typeof(ReactorDependencyPage)] = Loc.I.Pick("Reactor required", "需要反應堆");
        foreach (var m in ModuleRegistry.All)
            _titles[MapType(m.Tag)] = Loc.I.Pick(m.En, m.Zh);
    }

    /// <summary>Resolve a tab/nav key into a page type + parameter.</summary>
    private (Type type, object? param) Resolve(string key)
    {
        var baseKey = BaseNavKey(key);
        var fragment = NavFragment(key);
        switch (baseKey)
        {
            case "dashboard": return (typeof(DashboardPage), null);
            case "about": return (typeof(AboutPage), null);
            case "settings": return (typeof(SettingsPage), null);
            case "manual": return (typeof(ManualPage), null);
            case "licenses": return (typeof(LicensesPage), null);
        }
        if (baseKey.StartsWith("manual:", StringComparison.OrdinalIgnoreCase))
            return (typeof(ManualPage), baseKey.Substring("manual:".Length));
        if (baseKey.StartsWith("search:", StringComparison.OrdinalIgnoreCase))
            return (typeof(SearchResultsPage), baseKey.Substring("search:".Length));
        if (baseKey.StartsWith("module.", StringComparison.Ordinal))
        {
            if (ReactorDependencyService.TryGet(baseKey, out var dependency))
            {
                var check = ReactorDependencyService.Evaluate(dependency, ReactorStatusApiService.I.LastSnapshot, ReactorStatusApiService.I.Enabled);
                if (!check.IsSatisfied)
                    return (typeof(ReactorDependencyPage), new ReactorDependencyPageContext(baseKey, dependency));
            }
            return (MapType(baseKey), fragment);
        }
        var cat = Categories.All.FirstOrDefault(c => c.Id == baseKey);
        if (cat is not null) return (typeof(CategoryPage), cat);
        return (typeof(DashboardPage), null);
    }

    private string TitleFor(string key, Type type, object? param)
    {
        if (type == typeof(CategoryPage) && param is AppCategory c) return c.Name.Display;
        if (type == typeof(SearchResultsPage)) return param is string q && q.Length > 0 ? Loc.I.Pick($"Search: {q}", $"搜尋：{q}") : Loc.I.Pick("Search", "搜尋");
        if (type == typeof(ReactorDependencyPage) && param is ReactorDependencyPageContext ctx)
            return Loc.I.Pick($"{ctx.Dependency.NameEn} - reactor required", $"{ctx.Dependency.NameZh} - 需要反應堆");
        return _titles.TryGetValue(type, out var t) ? t : "WinForge";
    }

    /// <summary>Navigate the active tab to a key; opens a tab if none exist.</summary>
    private void NavigateActive(string key)
    {
        if (Tabs.TabItems.Count == 0) { AddTab(key); return; }
        if (Tabs.SelectedItem is not TabViewItem tab || tab.Content is not Frame frame) { AddTab(key); return; }
        var (type, param) = Resolve(key);
        frame.Navigate(type, param);
        DataOf(tab).Key = key;
        RememberAppUse(key);
        RefreshTabHeader(tab);
        UpdateBackButton();
        SaveSession();
    }

    private TabViewItem AddTab(string key = "dashboard", bool select = true)
        => AddTab(new TabSessionService.TabData { Key = key }, select);

    private TabViewItem AddTab(TabSessionService.TabData source, bool select = true)
    {
        var data = TabSessionService.CloneTab(source);
        if (string.IsNullOrWhiteSpace(data.Key)) data.Key = "dashboard";
        if (select) RememberAppUse(data.Key);
        if (!string.IsNullOrWhiteSpace(data.GroupId) && GroupFor(data.GroupId) is null) data.GroupId = string.Empty;

        var (type, param) = Resolve(data.Key);
        var frame = new Frame();
        frame.Navigated += (_, _) => UpdateBackButton();
        var tab = new TabViewItem
        {
            Tag = data,
            Header = BuildTabHeader(data, type, param),
            Content = frame,
            IconSource = new SymbolIconSource { Symbol = Symbol.Document },
        };
        ApplyTabAutomation(tab, type, param);
        tab.ContextFlyout = BuildTabFlyout(tab);
        Tabs.TabItems.Add(tab);
        if (select) Tabs.SelectedItem = tab;
        frame.Navigate(type, param);
        UpdateBackButton();
        SaveSession();
        return tab;
    }

    private void CloseActiveTab()
    {
        if (Tabs.SelectedItem is TabViewItem tab) CloseTab(tab);
    }

    private void CloseTab(TabViewItem tab)
    {
        Tabs.TabItems.Remove(tab);
        if (Tabs.TabItems.Count == 0) AddTab("dashboard");
        SaveSession();
    }

    private void UpdateBackButton()
    {
        try { AppTitleBar.IsBackButtonVisible = ActiveFrame?.CanGoBack == true; } catch { }
    }

    private async void Tabs_AddTabButtonClick(TabView sender, object args) => await ShowNewTabPickerAsync();

    private void Tabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab is TabViewItem tab) CloseTab(tab);
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Tabs.SelectedItem is not TabViewItem tab) return;
        UpdateBackButton();
        var key = DataOf(tab).Key;
        if (string.IsNullOrEmpty(key)) return;
        var item = FindByTag(BaseNavKey(key));
        if (item is not null && !ReferenceEquals(NavView.SelectedItem, item))
        {
            _syncingTabs = true;
            try { NavView.SelectedItem = item; } finally { _syncingTabs = false; }
        }
        SaveSession();
    }

    // ===================== Session: persist / export / import =====================

    private void SaveSession()
    {
        if (Tabs is null || _restoring) return;
        TabSessionService.Save(CurrentSessionData());
    }

    private void RestoreSessionOrDefault()
    {
        if (!string.IsNullOrWhiteSpace(App.StartPage))
        {
            AddTab("dashboard");
            return;
        }
        var data = TabSessionService.Load();
        if (data is null || data.Tabs.Count == 0) { AddTab("dashboard"); return; }
        ReloadTabs(data);
    }

    private void ReloadTabs(TabSessionService.SessionData data)
    {
        _restoring = true;
        try
        {
            ApplyLocalGitState(data.LocalGit, selectActive: true);
            var tabs = LoadGroupsAndMapTabs(data);
            Tabs.TabItems.Clear();
            foreach (var tabData in tabs) AddTab(tabData, select: false);
            if (Tabs.TabItems.Count == 0) AddTab("dashboard", select: false);
            var active = (data.Active >= 0 && data.Active < Tabs.TabItems.Count) ? data.Active : 0;
            Tabs.SelectedItem = Tabs.TabItems[active];
        }
        finally { _restoring = false; }
        SaveSession();
    }

    private async void Session_NewTab(object sender, RoutedEventArgs e) => await ShowNewTabPickerAsync();

    private async void Session_CustomizeTab(object sender, RoutedEventArgs e)
        => await EditTabAsync(Tabs.SelectedItem as TabViewItem);

    private async void Session_NewGroup(object sender, RoutedEventArgs e)
        => await CreateGroupFromTabAsync(Tabs.SelectedItem as TabViewItem);

    private async void Session_CustomizeGroup(object sender, RoutedEventArgs e)
        => await EditCurrentGroupAsync(Tabs.SelectedItem as TabViewItem);

    private void Session_UngroupTab(object sender, RoutedEventArgs e)
    {
        if (Tabs.SelectedItem is not TabViewItem tab) return;
        DataOf(tab).GroupId = string.Empty;
        RefreshTabHeader(tab);
        SaveSession();
    }

    private async void Session_Export(object sender, RoutedEventArgs e)
    {
        SaveSession();
        var path = await FileDialogs.SaveFileAsync($"winforge-tabs-{DateTime.Now:yyyyMMdd-HHmm}", ".json");
        if (!string.IsNullOrEmpty(path)) { try { TabSessionService.ExportTo(path, CurrentSessionData()); } catch { } }
    }

    private async void Session_Import(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".json");
        if (string.IsNullOrEmpty(path)) return;
        var data = TabSessionService.ImportFrom(path);
        if (data is not null) ReloadTabs(data);
    }

    private async void Session_ExportGroup(object sender, RoutedEventArgs e)
    {
        if (Tabs.SelectedItem is not TabViewItem tab) return;
        var group = GroupFor(DataOf(tab).GroupId);
        if (group is null)
        {
            await ShowSessionMessageAsync("No group selected", "Put the current tab in a group before exporting a group.");
            return;
        }

        SaveSession();
        var safe = SafeFileName(string.IsNullOrWhiteSpace(group.Name) ? "winforge-tab-group" : group.Name);
        var filters = new[]
        {
            new FileDialogs.Filter("WinForge tab group (*.json)", "*.json"),
            new FileDialogs.Filter("All files", "*.*"),
        };
        var path = await FileDialogs.SaveFileAsync(safe, filters, "json", "Export tab group");
        if (!string.IsNullOrEmpty(path))
        {
            try { TabSessionService.ExportGroupTo(path, CurrentSessionData(), group.Id); } catch { }
        }
    }

    private async void Session_ImportGroup(object sender, RoutedEventArgs e)
    {
        var filters = new[]
        {
            new FileDialogs.Filter("WinForge tab group/session (*.json)", "*.json"),
            new FileDialogs.Filter("All files", "*.*"),
        };
        var path = await FileDialogs.OpenFileAsync(filters, "Import tab group");
        if (string.IsNullOrEmpty(path)) return;

        var data = TabSessionService.ImportGroupFrom(path);
        if (data is null)
        {
            await ShowSessionMessageAsync("Import failed", "That file is not a WinForge tab group.");
            return;
        }

        var group = TabSessionService.CloneGroup(data.Group);
        group.Id = UniqueGroupId(group.Id);
        if (string.IsNullOrWhiteSpace(group.Name)) group.Name = $"Imported group {_tabGroups.Count + 1}";
        _tabGroups.Add(group);

        ApplyLocalGitState(data.LocalGit, selectActive: false);
        if (!string.IsNullOrWhiteSpace(group.RepoPath) && Directory.Exists(group.RepoPath))
            RepoStore.Add(group.RepoPath);

        var importedTabs = data.Tabs.Count == 0
            ? new List<TabSessionService.TabData> { new() { Key = "dashboard", GroupId = group.Id } }
            : data.Tabs.Select(TabSessionService.CloneTab).ToList();

        TabViewItem? first = null;
        _restoring = true;
        try
        {
            foreach (var importedTab in importedTabs)
            {
                importedTab.GroupId = group.Id;
                var added = AddTab(importedTab, select: false);
                first ??= added;
            }
        }
        finally { _restoring = false; }

        if (first is not null) Tabs.SelectedItem = first;
        RefreshAllTabHeaders();
        SaveSession();
    }

    private void Session_Restore(object sender, RoutedEventArgs e)
    {
        var data = TabSessionService.Load();
        if (data is not null) ReloadTabs(data);
    }

    private void Session_OpenFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(TabSessionService.Folder);
            Clipboard.SetContent(dp);
            Clipboard.Flush();
        }
        catch { }
    }

    private TabSessionService.TabData DataOf(TabViewItem tab)
    {
        if (tab.Tag is TabSessionService.TabData data) return data;
        var created = new TabSessionService.TabData { Key = tab.Tag as string ?? "dashboard" };
        tab.Tag = created;
        return created;
    }

    private TabSessionService.TabGroupData? GroupFor(string? id)
        => string.IsNullOrWhiteSpace(id) ? null : _tabGroups.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));

    private TabSessionService.SessionData CurrentSessionData()
    {
        var repos = RepoStore.All
            .Select(r => r.Path)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(200)
            .ToList();

        return new TabSessionService.SessionData
        {
            Active = Tabs.SelectedIndex < 0 ? 0 : Tabs.SelectedIndex,
            Tabs = Tabs.TabItems.OfType<TabViewItem>().Select(t => TabSessionService.CloneTab(DataOf(t))).ToList(),
            Groups = _tabGroups.Select(TabSessionService.CloneGroup).ToList(),
            LocalGit = new TabSessionService.LocalGitData
            {
                CurrentRepoPath = AppState.CurrentRepoPath,
                SavedRepos = repos,
            },
        };
    }

    private List<TabSessionService.TabData> LoadGroupsAndMapTabs(TabSessionService.SessionData data)
    {
        _tabGroups.Clear();
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in data.Groups)
        {
            var group = TabSessionService.CloneGroup(source);
            var original = group.Id;
            group.Id = UniqueGroupId(group.Id);
            if (string.IsNullOrWhiteSpace(group.Name)) group.Name = $"Group {_tabGroups.Count + 1}";
            if (string.IsNullOrWhiteSpace(group.Color)) group.Color = NextGroupColor();
            if (string.IsNullOrWhiteSpace(group.RepoPath)) group.RepoPath = data.LocalGit.CurrentRepoPath;
            _tabGroups.Add(group);
            if (!string.IsNullOrWhiteSpace(original)) map[original] = group.Id;
            if (!string.IsNullOrWhiteSpace(group.RepoPath) && Directory.Exists(group.RepoPath)) RepoStore.Add(group.RepoPath);
        }

        var tabs = new List<TabSessionService.TabData>();
        foreach (var source in data.Tabs)
        {
            var tab = TabSessionService.CloneTab(source);
            if (!string.IsNullOrWhiteSpace(tab.GroupId) && map.TryGetValue(tab.GroupId, out var mapped)) tab.GroupId = mapped;
            if (!string.IsNullOrWhiteSpace(tab.GroupId) && GroupFor(tab.GroupId) is null) tab.GroupId = string.Empty;
            tabs.Add(tab);
        }
        return tabs;
    }

    private void ApplyLocalGitState(TabSessionService.LocalGitData? state, bool selectActive)
    {
        if (state is null) return;
        foreach (var repo in state.SavedRepos)
        {
            if (!string.IsNullOrWhiteSpace(repo) && Directory.Exists(repo)) RepoStore.Add(repo);
        }
        if (selectActive && !string.IsNullOrWhiteSpace(state.CurrentRepoPath) && Directory.Exists(state.CurrentRepoPath))
        {
            RepoStore.Add(state.CurrentRepoPath);
            RepoStore.Select(state.CurrentRepoPath);
        }
    }

    private object BuildTabHeader(TabSessionService.TabData data, Type type, object? param)
    {
        var group = GroupFor(data.GroupId);
        var title = string.IsNullOrWhiteSpace(data.Name) ? TitleFor(data.Key, type, param) : data.Name.Trim();
        var accent = FirstNonEmpty(data.Color, group?.Color, "#FF0078D4");
        var font = EffectiveFont(data.Font, group?.Font);

        var root = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 260,
        };

        root.Children.Add(new Border
        {
            Width = 4,
            Height = group is null ? 18 : 28,
            CornerRadius = new CornerRadius(2),
            Background = BrushFromHex(accent, Color.FromArgb(255, 0, 120, 212)),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var text = new StackPanel { Orientation = Orientation.Vertical, Spacing = 1, MaxWidth = 230 };
        if (group is not null)
        {
            var groupText = new TextBlock
            {
                Text = group.Name,
                FontSize = 10,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = BrushFromHex(group.Color, Color.FromArgb(255, 0, 120, 212)),
                MaxWidth = 220,
            };
            ApplyFont(groupText, group.Font, allowSize: false);
            text.Children.Add(groupText);
        }

        var titleText = new TextBlock
        {
            Text = title,
            FontSize = font.Size > 0 ? font.Size : 13,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 230,
        };
        ApplyFont(titleText, font, allowSize: true);
        text.Children.Add(titleText);
        root.Children.Add(text);
        ToolTipService.SetToolTip(root, group is null ? title : $"{group.Name} - {title}");
        return root;
    }

    private void RefreshTabHeader(TabViewItem tab)
    {
        var data = DataOf(tab);
        var (type, param) = Resolve(data.Key);
        tab.Header = BuildTabHeader(data, type, param);
        ApplyTabAutomation(tab, type, param);
        tab.ContextFlyout = BuildTabFlyout(tab);
    }

    private void ApplyTabAutomation(TabViewItem tab, Type type, object? param)
    {
        var data = DataOf(tab);
        var group = GroupFor(data.GroupId);
        var title = string.IsNullOrWhiteSpace(data.Name) ? TitleFor(data.Key, type, param) : data.Name.Trim();
        var label = group is null
            ? title
            : Loc.I.Pick($"{group.Name} tab: {title}", $"{group.Name} 分頁：{title}");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(tab, "ShellTab_" + AutomationSafeKey(data.Key));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(tab, label);
    }

    private void RefreshAllTabHeaders()
    {
        foreach (var tab in Tabs.TabItems.OfType<TabViewItem>()) RefreshTabHeader(tab);
    }

    private MenuFlyout BuildTabFlyout(TabViewItem tab)
    {
        var flyout = new MenuFlyout();
        AddFlyoutItem(flyout, "New tab · 新分頁", "\uE710", async (_, _) => await ShowNewTabPickerAsync());
        flyout.Items.Add(new MenuFlyoutSeparator());
        AddFlyoutItem(flyout, "Rename/style tab… · 分頁名稱／樣式…", "\uE8D2", async (_, _) => await EditTabAsync(tab));
        AddFlyoutItem(flyout, "New group from tab… · 由分頁新增分組…", "\uE8A5", async (_, _) => await CreateGroupFromTabAsync(tab));
        AddFlyoutItem(flyout, "Rename/style group… · 分組名稱／樣式…", "\uE713", async (_, _) => await EditCurrentGroupAsync(tab));
        AddFlyoutItem(flyout, "Remove tab from group · 分頁移出分組", "\uE711", (_, _) =>
        {
            DataOf(tab).GroupId = string.Empty;
            RefreshTabHeader(tab);
            SaveSession();
        });
        flyout.Items.Add(new MenuFlyoutSeparator());
        AddFlyoutItem(flyout, "Detach tab to window · 分離分頁做視窗", "\uE8A7", (_, _) => DetachTab(tab));
        flyout.Items.Add(new MenuFlyoutSeparator());
        AddFlyoutItem(flyout, "Close tab · 關閉分頁", "\uE8BB", (_, _) => CloseTab(tab));
        return flyout;
    }

    private static void AddFlyoutItem(MenuFlyout flyout, string text, string glyph, RoutedEventHandler click)
    {
        var item = new MenuFlyoutItem
        {
            Text = text,
            Icon = new FontIcon { Glyph = glyph, FontSize = 14 },
        };
        var enText = text.Split('·')[0].Trim();
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(item, "ShellTabAction_" + AutomationSafeKey(enText));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, text);
        item.Click += click;
        flyout.Items.Add(item);
    }

    private async Task EditTabAsync(TabViewItem? tab)
    {
        if (tab is null) return;
        var data = DataOf(tab);
        var panel = new StackPanel { Spacing = 10, Width = 420 };
        var editor = BuildAppearanceEditor(
            panel,
            nameHeader: "Tab name · 分頁名稱",
            namePlaceholder: "Blank uses the page title",
            name: data.Name,
            colorHeader: "Tab color · 分頁顏色",
            color: data.Color,
            font: data.Font,
            includeGroups: true,
            currentGroupId: data.GroupId,
            includeRepoPath: false,
            repoPath: null);

        var dialog = SessionDialog("Rename/style tab", panel);
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        data.Name = editor.NameBox.Text.Trim();
        data.Color = NormalizeHexOrEmpty(editor.ColorBox.Text);
        data.GroupId = (editor.GroupBox?.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;
        data.Font = ReadFont(editor);
        RefreshTabHeader(tab);
        SaveSession();
    }

    private async Task CreateGroupFromTabAsync(TabViewItem? tab)
    {
        if (tab is null) return;
        var group = new TabSessionService.TabGroupData
        {
            Id = UniqueGroupId(Guid.NewGuid().ToString("N")),
            Name = $"Group {_tabGroups.Count + 1}",
            Color = NextGroupColor(),
            RepoPath = AppState.CurrentRepoPath,
        };

        if (!await EditGroupAsync(group, isNew: true)) return;
        _tabGroups.Add(group);
        DataOf(tab).GroupId = group.Id;
        RefreshAllTabHeaders();
        SaveSession();
    }

    private async Task EditCurrentGroupAsync(TabViewItem? tab)
    {
        if (tab is null) return;
        var group = GroupFor(DataOf(tab).GroupId);
        if (group is null)
        {
            await CreateGroupFromTabAsync(tab);
            return;
        }

        if (!await EditGroupAsync(group, isNew: false)) return;
        RefreshAllTabHeaders();
        SaveSession();
    }

    private async Task<bool> EditGroupAsync(TabSessionService.TabGroupData group, bool isNew)
    {
        var panel = new StackPanel { Spacing = 10, Width = 420 };
        var editor = BuildAppearanceEditor(
            panel,
            nameHeader: "Group name · 分組名稱",
            namePlaceholder: "Group name",
            name: group.Name,
            colorHeader: "Group color · 分組顏色",
            color: group.Color,
            font: group.Font,
            includeGroups: false,
            currentGroupId: null,
            includeRepoPath: true,
            repoPath: group.RepoPath);

        var dialog = SessionDialog(isNew ? "New tab group" : "Rename/style group", panel);
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return false;

        group.Name = string.IsNullOrWhiteSpace(editor.NameBox.Text) ? "Tab group" : editor.NameBox.Text.Trim();
        group.Color = NormalizeHexOrEmpty(editor.ColorBox.Text);
        if (string.IsNullOrWhiteSpace(group.Color)) group.Color = NextGroupColor();
        group.Font = ReadFont(editor);
        group.RepoPath = editor.RepoPathBox?.Text.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(group.RepoPath) && Directory.Exists(group.RepoPath)) RepoStore.Add(group.RepoPath);
        return true;
    }

    private TabAppearanceEditor BuildAppearanceEditor(
        StackPanel panel,
        string nameHeader,
        string namePlaceholder,
        string name,
        string colorHeader,
        string color,
        TabSessionService.TabFontData font,
        bool includeGroups,
        string? currentGroupId,
        bool includeRepoPath,
        string? repoPath)
    {
        var nameBox = new TextBox
        {
            Header = nameHeader,
            PlaceholderText = namePlaceholder,
            Text = name ?? string.Empty,
        };
        panel.Children.Add(nameBox);

        ComboBox? groupBox = null;
        if (includeGroups)
        {
            groupBox = new ComboBox { Header = "Group · 分組", HorizontalAlignment = HorizontalAlignment.Stretch };
            groupBox.Items.Add(new ComboBoxItem { Content = "No group · 無分組", Tag = string.Empty });
            foreach (var group in _tabGroups)
                groupBox.Items.Add(new ComboBoxItem { Content = group.Name, Tag = group.Id });
            SelectComboTag(groupBox, currentGroupId ?? string.Empty);
            panel.Children.Add(groupBox);
        }

        var colorBox = new TextBox
        {
            Header = colorHeader,
            PlaceholderText = "#RRGGBB or #AARRGGBB",
            Text = color ?? string.Empty,
        };
        panel.Children.Add(ColorPresetRow(colorBox));

        var fontFamily = new TextBox
        {
            Header = "Font family · 字型",
            PlaceholderText = "Segoe UI, Cascadia Mono, Consolas",
            Text = font.Family ?? string.Empty,
        };
        panel.Children.Add(fontFamily);

        var fontRow = new Grid { ColumnSpacing = 8 };
        fontRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fontRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fontRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var fontSize = new TextBox
        {
            Header = "Size · 大小",
            PlaceholderText = "13",
            Text = font.Size > 0 ? font.Size.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty,
        };
        Grid.SetColumn(fontSize, 0);
        fontRow.Children.Add(fontSize);

        var weight = new ComboBox { Header = "Weight · 粗幼", HorizontalAlignment = HorizontalAlignment.Stretch };
        AddCombo(weight, "", "Inherit");
        AddCombo(weight, "Light", "Light");
        AddCombo(weight, "Normal", "Normal");
        AddCombo(weight, "SemiBold", "SemiBold");
        AddCombo(weight, "Bold", "Bold");
        SelectComboTag(weight, font.Weight);
        Grid.SetColumn(weight, 1);
        fontRow.Children.Add(weight);

        var style = new ComboBox { Header = "Style · 樣式", HorizontalAlignment = HorizontalAlignment.Stretch };
        AddCombo(style, "", "Inherit");
        AddCombo(style, "Normal", "Normal");
        AddCombo(style, "Italic", "Italic");
        AddCombo(style, "Oblique", "Oblique");
        SelectComboTag(style, font.Style);
        Grid.SetColumn(style, 2);
        fontRow.Children.Add(style);
        panel.Children.Add(fontRow);

        var fontColor = new TextBox
        {
            Header = "Font color · 字色",
            PlaceholderText = "#RRGGBB or #AARRGGBB",
            Text = font.Color ?? string.Empty,
        };
        panel.Children.Add(ColorPresetRow(fontColor));

        TextBox? repoBox = null;
        if (includeRepoPath)
        {
            repoBox = new TextBox
            {
                Header = "Local git repo for group · 分組本機 Git 倉",
                PlaceholderText = AppState.CurrentRepoPath,
                Text = repoPath ?? string.Empty,
            };
            panel.Children.Add(repoBox);
        }

        return new TabAppearanceEditor
        {
            NameBox = nameBox,
            ColorBox = colorBox,
            GroupBox = groupBox,
            FontFamilyBox = fontFamily,
            FontSizeBox = fontSize,
            WeightBox = weight,
            StyleBox = style,
            FontColorBox = fontColor,
            RepoPathBox = repoBox,
        };
    }

    private FrameworkElement ColorPresetRow(TextBox colorBox)
    {
        var root = new StackPanel { Spacing = 6 };
        root.Children.Add(colorBox);

        var presets = new ComboBox { Header = "Preset · 預設", HorizontalAlignment = HorizontalAlignment.Stretch };
        presets.Items.Add(new ComboBoxItem { Content = "Custom / inherit", Tag = string.Empty });
        foreach (var (name, hex) in TabPalette)
            presets.Items.Add(new ComboBoxItem { Content = $"{name}  {hex}", Tag = hex });
        presets.SelectionChanged += (_, _) =>
        {
            if ((presets.SelectedItem as ComboBoxItem)?.Tag is string hex)
                colorBox.Text = hex;
        };
        SelectComboTag(presets, NormalizeHexOrEmpty(colorBox.Text));
        root.Children.Add(presets);
        return root;
    }

    private static void AddCombo(ComboBox combo, string tag, string label)
        => combo.Items.Add(new ComboBoxItem { Tag = tag, Content = label });

    private static void SelectComboTag(ComboBox combo, string? value)
    {
        value ??= string.Empty;
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string ?? string.Empty, value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private ContentDialog SessionDialog(string title, UIElement content)
    {
        return new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = $"{title} · 分頁設定",
            Content = new ScrollViewer
            {
                Content = content,
                MaxHeight = 520,
                VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto,
            },
            PrimaryButtonText = "Save · 儲存",
            CloseButtonText = "Cancel · 取消",
            DefaultButton = ContentDialogButton.Primary,
        };
    }

    private async Task ShowSessionMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
        };
        await dialog.ShowAsync();
    }

    private TabSessionService.TabFontData ReadFont(TabAppearanceEditor editor)
    {
        var size = 0d;
        if (!string.IsNullOrWhiteSpace(editor.FontSizeBox.Text))
            _ = double.TryParse(editor.FontSizeBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out size);

        return new TabSessionService.TabFontData
        {
            Family = editor.FontFamilyBox.Text.Trim(),
            Size = Math.Clamp(size, 0, 48),
            Weight = (editor.WeightBox.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty,
            Style = (editor.StyleBox.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty,
            Color = NormalizeHexOrEmpty(editor.FontColorBox.Text),
        };
    }

    private static TabSessionService.TabFontData EffectiveFont(TabSessionService.TabFontData tab, TabSessionService.TabFontData? group)
    {
        group ??= new TabSessionService.TabFontData();
        return new TabSessionService.TabFontData
        {
            Family = FirstNonEmpty(tab.Family, group.Family),
            Size = tab.Size > 0 ? tab.Size : group.Size,
            Weight = FirstNonEmpty(tab.Weight, group.Weight),
            Style = FirstNonEmpty(tab.Style, group.Style),
            Color = FirstNonEmpty(tab.Color, group.Color),
        };
    }

    private static void ApplyFont(TextBlock text, TabSessionService.TabFontData font, bool allowSize)
    {
        if (!string.IsNullOrWhiteSpace(font.Family)) text.FontFamily = new FontFamily(font.Family.Trim());
        if (allowSize && font.Size > 0) text.FontSize = font.Size;
        text.FontWeight = FontWeightFor(font.Weight);
        text.FontStyle = FontStyleFor(font.Style);
        if (!string.IsNullOrWhiteSpace(font.Color))
            text.Foreground = BrushFromHex(font.Color, Color.FromArgb(255, 32, 32, 32));
    }

    private static Windows.UI.Text.FontWeight FontWeightFor(string? weight)
    {
        return (weight ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "light" => FontWeights.Light,
            "semibold" => FontWeights.SemiBold,
            "bold" => FontWeights.Bold,
            _ => FontWeights.Normal,
        };
    }

    private static Windows.UI.Text.FontStyle FontStyleFor(string? style)
    {
        return (style ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "italic" => Windows.UI.Text.FontStyle.Italic,
            "oblique" => Windows.UI.Text.FontStyle.Oblique,
            _ => Windows.UI.Text.FontStyle.Normal,
        };
    }

    private static SolidColorBrush BrushFromHex(string? hex, Color fallback)
        => new(TryParseHex(hex, out var color) ? color : fallback);

    private static string NormalizeHexOrEmpty(string? hex)
    {
        return TryParseHex(hex, out var color)
            ? $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}"
            : string.Empty;
    }

    private static bool TryParseHex(string? input, out Color color)
    {
        color = default;
        var hex = (input ?? string.Empty).Trim();
        if (hex.StartsWith("#", StringComparison.Ordinal)) hex = hex[1..];
        if (hex.Length == 6) hex = "FF" + hex;
        if (hex.Length != 8) return false;
        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)) return false;
        color = Color.FromArgb(
            (byte)((value >> 24) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF));
        return true;
    }

    private string UniqueGroupId(string? id)
    {
        var candidate = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id.Trim();
        while (_tabGroups.Any(g => string.Equals(g.Id, candidate, StringComparison.OrdinalIgnoreCase)))
            candidate = Guid.NewGuid().ToString("N");
        return candidate;
    }

    private string NextGroupColor()
        => TabPalette[_tabGroups.Count % TabPalette.Length].Hex;

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private static string SafeFileName(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string((text ?? string.Empty).Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "winforge-tab-group" : cleaned;
    }
}
