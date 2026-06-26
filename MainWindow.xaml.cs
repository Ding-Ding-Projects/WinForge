using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Pages;
using WinForge.Services;

namespace WinForge;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        CrashLogger.Mark("MW: ctor start");
        InitializeComponent();
        CrashLogger.Mark("MW: after InitializeComponent");

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
        CrashLogger.Mark("MW: after WireNavigator");

        RestoreSessionOrDefault();
        CrashLogger.Mark("MW: after RestoreSessionOrDefault");
        ApplyStartPage();
        CrashLogger.Mark("MW: after ApplyStartPage");

        // Ctrl+T 開新分頁、Ctrl+W 關閉分頁 · Ctrl+T new tab, Ctrl+W close tab.
        AddAccel(Windows.System.VirtualKey.T, () => AddTab("dashboard"));
        AddAccel(Windows.System.VirtualKey.W, CloseActiveTab);

        AppWindow.Closing += OnAppWindowClosing;

        // 背景服務（剪貼簿、全域熱鍵泵、ZoomIt、快速重音、快捷鍵指南、指令面板、系統匣）延後到首次版面完成
        // 先啟動，而且每個都用 CrashLogger.Guard 包住——避免喺 XAML 初始化嘅脆弱時段同全域掛鈎／覆蓋層競爭，
        // 以免間歇性 stowed-exception 閃退；亦確保任何單一服務出錯都唔會拖冧開機。
        // Defer background services (clipboard monitor, global hotkey pump, ZoomIt/Command-Palette hotkeys,
        // Quick Accent, Shortcut Guide, tray) until AFTER first layout, each wrapped in CrashLogger.Guard —
        // so global hooks/overlays never race the fragile XAML init (the cause of intermittent
        // stowed-exception crashes at launch) and one faulty service can never abort startup.
        RootGrid.Loaded += StartBackgroundServicesOnce;
    }

    private bool _bgStarted;
    private void StartBackgroundServicesOnce(object sender, RoutedEventArgs e)
    {
        if (_bgStarted) return;
        _bgStarted = true;
        RootGrid.Loaded -= StartBackgroundServicesOnce;
        CrashLogger.Mark("MW: RootGrid.Loaded fired");
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            CrashLogger.Mark("svc: clipboard");     CrashLogger.Guard("startup:clipboard",     () => ClipboardService.Start(DispatcherQueue));
            CrashLogger.Mark("svc: hotkeys");       CrashLogger.Guard("startup:hotkeys",       () => HotkeyMacroService.StartHotkeys());
            CrashLogger.Mark("svc: zoomit");        CrashLogger.Guard("startup:zoomit",        () => ZoomItService.StartHotkeys());
            CrashLogger.Mark("svc: quickaccent");   CrashLogger.Guard("startup:quickaccent",   () => QuickAccentService.Apply());
            CrashLogger.Mark("svc: shortcutguide"); CrashLogger.Guard("startup:shortcutguide", () => ShortcutGuideService.Init(DispatcherQueue));
            CrashLogger.Mark("svc: cmdpalette");    CrashLogger.Guard("startup:cmdpalette",    () => CommandPaletteService.Start(DispatcherQueue));
            CrashLogger.Mark("svc: tray");          CrashLogger.Guard("startup:tray",          () => TrayService.Install(ShowFromTray, QuitFromTray, "WinForge · 視窗調校"));
            CrashLogger.Mark("svc: all started");
        });
    }

    private bool _reallyQuit;

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_reallyQuit || !TrayService.IsInstalled)
        {
            // Genuinely closing → flush all volatile state before the process tears down.
            CrashLogger.Guard("persistence:close", () => Services.PersistenceService.I.Flush());
            return;
        }
        args.Cancel = true;       // don't exit — hide to the tray so background work continues
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
        CrashLogger.Guard("persistence:quit", () => Services.PersistenceService.I.Flush());
        TrayService.Remove();
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
            case "portscan":
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
            case "minecraftserver":
            case "mcserver":
            case "paper":
            case "spigot":
                Navigator.GoToModule?.Invoke("module.minecraftserver");
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
            case "dashboard":
                break;
            case "about":
                NavigateActive("about");
                break;
            case "settings":
                NavigateActive("settings");
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
                Content = $"{cat.Name.En} · {cat.Name.Zh}",
                Tag = cat.Id,
                Icon = new FontIcon { Glyph = cat.Glyph },
            });
        }
    }

    private void WireNavigator()
    {
        Navigator.GoToCategory = cat =>
        {
            var item = FindByTag(cat.Id);
            if (item is not null) NavView.SelectedItem = item;
        };

        Navigator.GoToSettings = () => NavigateActive("settings");

        Navigator.GoToModule = key =>
        {
            var item = FindByTag(key);
            if (item is not null && ReferenceEquals(NavView.SelectedItem, item)) NavigateActive(key); // already selected → re-navigate active tab
            else if (item is not null) NavView.SelectedItem = item;
            else NavigateActive(key); // fall back to direct navigation if not in the pane
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
        "module.viaproxy" => typeof(ViaProxyModule),
        "module.minecraftserver" => typeof(MinecraftServerModule),
        "module.voice" => typeof(VoiceModule),
        "module.announcements" => typeof(AnnouncementsModule),
        "module.vault-volumes" => typeof(VaultVolumesModule),
        "module.worldmonitor" => typeof(WorldMonitorModule),
        "module.timelens" => typeof(TimeLensModule),
        "module.diffmerge" => typeof(DiffMergeModule),
        "module.apiclient" => typeof(ApiClientModule),
        _ => typeof(DashboardPage),
    };

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        var q = sender.Text ?? "";
        if (q.Trim().Length == 0) { sender.ItemsSource = null; return; }
        var sugg = ModuleRegistry.Search(q).Select(m => $"{m.En} · {m.Zh}")
            .Concat(TweakCatalog.Search(q).Take(6).Select(t => $"{t.Title.En} · {t.Title.Zh}"))
            .Take(10).ToList();
        sender.ItemsSource = sugg;
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
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

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_syncingTabs) return;
        if (args.IsSettingsSelected) { NavigateActive("settings"); return; }
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag) NavigateActive(tag);
    }

    // ===================== Browser-style tabs · 瀏覽器式分頁 =====================
    // Each tab owns its own navigation Frame. The tab title is the page you're on,
    // a new tab opens the Dashboard, and the open tabs are mirrored to a local git
    // repo (TabSessionService) so the whole session can be exported / restored.

    private bool _syncingTabs;
    private bool _restoring;
    private readonly Dictionary<Type, string> _titles = new();

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

    private void BuildTitleMap()
    {
        _titles[typeof(DashboardPage)] = "Dashboard · 概覽";
        _titles[typeof(AboutPage)] = "About · 關於";
        _titles[typeof(SettingsPage)] = "Settings · 設定";
        _titles[typeof(SearchResultsPage)] = "Search · 搜尋";
        foreach (var m in ModuleRegistry.All)
            _titles[MapType(m.Tag)] = $"{m.En} · {m.Zh}";
    }

    /// <summary>Resolve a tab/nav key into a page type + parameter.</summary>
    private (Type type, object? param) Resolve(string key)
    {
        switch (key)
        {
            case "dashboard": return (typeof(DashboardPage), null);
            case "about": return (typeof(AboutPage), null);
            case "settings": return (typeof(SettingsPage), null);
        }
        if (key.StartsWith("search:", StringComparison.OrdinalIgnoreCase))
            return (typeof(SearchResultsPage), key.Substring("search:".Length));
        if (key.StartsWith("module.", StringComparison.Ordinal))
            return (MapType(key), null);
        var cat = Categories.All.FirstOrDefault(c => c.Id == key);
        if (cat is not null) return (typeof(CategoryPage), cat);
        return (typeof(DashboardPage), null);
    }

    private string TitleFor(string key, Type type, object? param)
    {
        if (type == typeof(CategoryPage) && param is AppCategory c) return $"{c.Name.En} · {c.Name.Zh}";
        if (type == typeof(SearchResultsPage)) return param is string q && q.Length > 0 ? $"Search: {q}" : "Search · 搜尋";
        return _titles.TryGetValue(type, out var t) ? t : "WinForge";
    }

    /// <summary>Navigate the active tab to a key; opens a tab if none exist.</summary>
    private void NavigateActive(string key)
    {
        if (Tabs.TabItems.Count == 0) { AddTab(key); return; }
        if (Tabs.SelectedItem is not TabViewItem tab || tab.Content is not Frame frame) { AddTab(key); return; }
        var (type, param) = Resolve(key);
        frame.Navigate(type, param);
        tab.Tag = key;
        tab.Header = TitleFor(key, type, param);
        UpdateBackButton();
        SaveSession();
    }

    private TabViewItem AddTab(string key = "dashboard", bool select = true)
    {
        var (type, param) = Resolve(key);
        var frame = new Frame();
        frame.Navigated += (_, _) => UpdateBackButton();
        var tab = new TabViewItem
        {
            Tag = key,
            Header = TitleFor(key, type, param),
            Content = frame,
            IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource { Symbol = Symbol.Document },
        };
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

    private void Tabs_AddTabButtonClick(TabView sender, object args) => AddTab("dashboard");

    private void Tabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab is TabViewItem tab) CloseTab(tab);
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Tabs.SelectedItem is not TabViewItem tab) return;
        UpdateBackButton();
        var key = tab.Tag as string;
        if (string.IsNullOrEmpty(key)) return;
        var item = FindByTag(key);
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
        var keys = Tabs.TabItems.OfType<TabViewItem>().Select(t => (t.Tag as string) ?? "dashboard");
        TabSessionService.Save(keys, Tabs.SelectedIndex < 0 ? 0 : Tabs.SelectedIndex);
    }

    private void RestoreSessionOrDefault()
    {
        var data = TabSessionService.Load();
        if (data is null || data.Tabs.Count == 0) { AddTab("dashboard"); return; }
        ReloadTabs(data);
    }

    private void ReloadTabs(TabSessionService.SessionData data)
    {
        _restoring = true;
        try
        {
            Tabs.TabItems.Clear();
            foreach (var key in data.Tabs) AddTab(key, select: false);
            if (Tabs.TabItems.Count == 0) AddTab("dashboard", select: false);
            var active = (data.Active >= 0 && data.Active < Tabs.TabItems.Count) ? data.Active : 0;
            Tabs.SelectedItem = Tabs.TabItems[active];
        }
        finally { _restoring = false; }
        SaveSession();
    }

    private void Session_NewTab(object sender, RoutedEventArgs e) => AddTab("dashboard");

    private async void Session_Export(object sender, RoutedEventArgs e)
    {
        SaveSession();
        var path = await FileDialogs.SaveFileAsync($"winforge-tabs-{DateTime.Now:yyyyMMdd-HHmm}", ".json");
        if (!string.IsNullOrEmpty(path)) { try { TabSessionService.ExportTo(path); } catch { } }
    }

    private async void Session_Import(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".json");
        if (string.IsNullOrEmpty(path)) return;
        var data = TabSessionService.ImportFrom(path);
        if (data is not null) ReloadTabs(data);
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
}
