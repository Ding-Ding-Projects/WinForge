using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 概覽：系統摘要、全域搜尋、分類入口。
/// Dashboard: system summary, global bilingual search, and category tiles.
/// </summary>
public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
    }

    private void Render()
    {
        HeroSubEn.Text = "An all-in-one, fully bilingual control center that genuinely tunes Windows 11.";
        HeroSubZh.Text = "全方位、全雙語嘅控制中心，真係會幫你調校 Windows 11。";
        StatFeatures.Text = TweakCatalog.Count.ToString();
        StatCategories.Text = Categories.All.Length.ToString();
        StatModules.Text = ModuleRegistry.All.Count.ToString();

        RenderAdminBar();
        RenderModuleTiles();
        RenderStats();
        RenderCategoryTiles();

        ModulesHeader.Text = "Suite modules · 套件模組";
        StatsHeader.Text = "System at a glance · 系統一覽";
        BrowseHeader.Text = "Browse categories · 瀏覽分類";
        SearchBox.PlaceholderText = "Search all features · 搜尋全部功能 (EN / 粵語)…";
    }

    private void RenderAdminBar()
    {
        if (AdminHelper.IsElevated)
        {
            AdminBar.Severity = InfoBarSeverity.Success;
            AdminBar.Title = "Administrator · 管理員";
            AdminBar.Message = "Running elevated — every tweak is available.\n正以管理員身分運行 — 全部調校都用得。";
            AdminBar.ActionButton = null;
        }
        else
        {
            AdminBar.Severity = InfoBarSeverity.Warning;
            AdminBar.Title = "Standard user · 標準使用者";
            AdminBar.Message = "Some system-wide tweaks need administrator rights.\n部分全系統調校需要管理員權限。";
            var relaunch = new Button { Content = "Relaunch as admin · 以管理員身分重新啟動" };
            relaunch.Click += (_, _) =>
            {
                if (AdminHelper.RelaunchElevated())
                    Application.Current.Exit();
            };
            AdminBar.ActionButton = relaunch;
        }
    }

    private void RenderStats()
    {
        StatsPanel.Children.Clear();
        AddStat(((char)0xE7F4).ToString(), "Operating system", "作業系統", SystemInfo.OsFull);
        AddStat(((char)0xE950).ToString(), "Processor", "處理器", $"{SystemInfo.CpuName}  ({SystemInfo.LogicalProcessors} {Loc.I.Pick("threads", "執行緒")} · {SystemInfo.Architecture})");
        AddStat(((char)0xE950).ToString(), "Memory", "記憶體", SystemInfo.RamUsage);
        AddStat(((char)0xE7F4).ToString(), "Graphics", "顯示卡", SystemInfo.GpuName);
        AddStat(((char)0xEDA2).ToString(), "System drive", "系統磁碟", SystemInfo.SystemDrive);
        AddStat(((char)0xE823).ToString(), "Uptime", "運行時間", $"{SystemInfo.Uptime}  ({Loc.I.Pick("since", "由")} {SystemInfo.BootTime})");
    }

    private void AddStat(string glyph, string en, string zh, string value)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 120, MaxWidth = 220 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnSpacing = 10;

        var icon = new FontIcon { Glyph = glyph, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Left };
        Grid.SetColumn(icon, 0);

        var label = new TextBlock
        {
            Text = $"{en} · {zh}",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(label, 1);

        var val = new TextBlock
        {
            Text = value,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        Grid.SetColumn(val, 2);

        grid.Children.Add(icon);
        grid.Children.Add(label);
        grid.Children.Add(val);
        StatsPanel.Children.Add(grid);
    }

    private void RenderModuleTiles()
    {
        var tiles = new List<UIElement>
        {
            ReactorHeroTile(),
            ModuleTile("", "Git & GitHub", "Git 與 GitHub",
                Loc.I.Pick("Repos, commits, chunked upload, GitHub CLI", "儲存庫、提交、分批上載、GitHub CLI"),
                () => Navigator.GoToModule?.Invoke("module.git")),
            ModuleTile("", "Archives", "壓縮檔",
                Loc.I.Pick("Create/extract/test with 7-Zip, 100 ops", "用 7-Zip 建立／解壓／測試，100 項操作"),
                () => Navigator.GoToModule?.Invoke("module.archives")),
            ModuleTile("", "Media", "媒體",
                Loc.I.Pick("Convert/trim/extract with ffmpeg, 60 ops", "用 ffmpeg 轉檔／剪裁／抽聲，60 項操作"),
                () => Navigator.GoToModule?.Invoke("module.media")),
            ModuleTile("", "Registry Editor", "登錄編輯器",
                Loc.I.Pick("Browse & edit the registry in-app", "喺 app 內瀏覽同編輯登錄檔"),
                () => Navigator.GoToModule?.Invoke("module.regedit")),
            ModuleTile(((char)0xE95E).ToString(), "System Doctors", "系統醫生",
                Loc.I.Pick("Guided fixes: print, network, sleep, taskbar…", "引導式修復：列印、網絡、睡眠、工作列…"),
                () => Navigator.GoToModule?.Invoke("module.doctors")),
            ModuleTile("", "Services", "服務",
                Loc.I.Pick("Start/stop/configure services in-app", "喺 app 內啟動／停止／設定服務"),
                () => Navigator.GoToModule?.Invoke("module.services")),
            ModuleTile("", "Scheduled Tasks", "排程工作",
                Loc.I.Pick("Run/enable/disable tasks in-app", "喺 app 內執行／啟用／停用工作"),
                () => Navigator.GoToModule?.Invoke("module.tasks")),
            ModuleTile("", "Devices", "裝置",
                Loc.I.Pick("Enable/disable devices in-app", "喺 app 內啟用／停用裝置"),
                () => Navigator.GoToModule?.Invoke("module.devices")),
            ModuleTile("", "Startup Apps", "開機程式",
                Loc.I.Pick("Enable/disable boot programs in-app", "喺 app 內啟用／停用開機程式"),
                () => Navigator.GoToModule?.Invoke("module.startup")),
            ModuleTile("", "Batch Rename", "批次改名",
                Loc.I.Pick("Regex bulk file rename, in-app", "喺 app 內用正則批次改檔名"),
                () => Navigator.GoToModule?.Invoke("module.rename")),
            ModuleTile("", "Bulk File Ops", "批次檔案操作",
                Loc.I.Pick("Copy/move/recycle/organise by pattern", "按樣式複製／移動／回收／整理"),
                () => Navigator.GoToModule?.Invoke("module.bulkops")),
            ModuleTile("", "Duplicate Finder", "重複檔案搜尋",
                Loc.I.Pick("Find byte-identical files, recycle extras", "搵內容相同檔案，回收多餘"),
                () => Navigator.GoToModule?.Invoke("module.duplicates")),
            ModuleTile(((char)0xE72E).ToString(), "File Locksmith", "檔案鎖偵測",
                Loc.I.Pick("Find which process is locking a file/folder", "查邊個程序鎖住檔案／資料夾"),
                () => Navigator.GoToModule?.Invoke("module.filelocksmith")),
            ModuleTile("", "Disk Analyser", "磁碟分析",
                Loc.I.Pick("See what's using your disk space", "睇下空間用咗喺邊"),
                () => Navigator.GoToModule?.Invoke("module.disk")),
            ModuleTile("", "Drives", "磁碟機",
                Loc.I.Pick("Drive space, mount ISO/VHD, create VHD", "磁碟空間、掛載 ISO/VHD、建立 VHD"),
                () => Navigator.GoToModule?.Invoke("module.drives")),
            ModuleTile("", "App Uninstaller", "應用程式解除安裝",
                Loc.I.Pick("Remove Store/UWP bloatware in-app", "喺 app 內移除商店／UWP 臃腫程式"),
                () => Navigator.GoToModule?.Invoke("module.uninstall")),
            ModuleTile("", "Window Manager", "視窗管理",
                Loc.I.Pick("Snap windows to zones (FancyZones-style)", "將視窗貼去分區（FancyZones 式）"),
                () => Navigator.GoToModule?.Invoke("module.windows")),
            ModuleTile("", "Keyboard Remapper", "鍵盤重新對應",
                Loc.I.Pick("Remap or disable keys (SharpKeys-style)", "重新對應或停用按鍵（SharpKeys 式）"),
                () => Navigator.GoToModule?.Invoke("module.keyboard")),
            ModuleTile(((char)0xEDA7).ToString(), "Hotkey & Macro Runner", "熱鍵與巨集",
                Loc.I.Pick("Global chords + macros + text expander", "全域組合鍵 + 巨集 + 文字展開"),
                () => Navigator.GoToModule?.Invoke("module.hotkeys")),
            ModuleTile("", "Hosts Editor", "hosts 編輯器",
                Loc.I.Pick("Block/redirect domains in-app", "喺 app 內封鎖／重導域名"),
                () => Navigator.GoToModule?.Invoke("module.hosts")),
            ModuleTile("", "Mouse & Pointer", "滑鼠與指標",
                Loc.I.Pick("Disable accel, speed, swap — applies live", "熄加速、速度、交換 — 即時生效"),
                () => Navigator.GoToModule?.Invoke("module.mouse")),
            ModuleTile("", "Screen Recorder", "螢幕錄影",
                Loc.I.Pick("Record the whole desktop (ffmpeg)", "錄成個桌面（ffmpeg）"),
                () => Navigator.GoToModule?.Invoke("module.recorder")),
            ModuleTile(((char)0xE722).ToString(), "Capture Studio", "擷取工作室",
                Loc.I.Pick("Region record, snip & OCR", "區域錄影、截圖同 OCR"),
                () => Navigator.GoToModule?.Invoke("module.capture")),
            ModuleTile("", "System Monitor", "系統監察",
                Loc.I.Pick("Live CPU/RAM/network + end tasks", "即時 CPU／RAM／網絡 + 結束工作"),
                () => Navigator.GoToModule?.Invoke("module.monitor")),
ModuleTile(((char)0xE83E).ToString(), "Battery & Thermal", "電池與散熱",
                Loc.I.Pick("Charge, wear & live CPU/GPU temps", "電量、耗損同即時 CPU／GPU 溫度"),
                () => Navigator.GoToModule?.Invoke("module.battery")),
                        ModuleTile("", "Connections", "連線",
                Loc.I.Pick("Live TCP/UDP sockets + owning app", "即時 TCP／UDP 連線 + 擁有程式"),
                () => Navigator.GoToModule?.Invoke("module.connections")),
            ModuleTile("", "Event Viewer", "事件檢視器",
                Loc.I.Pick("Browse Windows logs in-app", "喺 app 內睇 Windows 記錄"),
                () => Navigator.GoToModule?.Invoke("module.events")),
            ModuleTile("", "Volume Mixer", "音量混合器",
                Loc.I.Pick("Per-app volume & mute", "每個 app 音量同靜音"),
                () => Navigator.GoToModule?.Invoke("module.mixer")),
            ModuleTile("", "Context Menu", "右鍵選單",
                Loc.I.Pick("Add/remove right-click commands", "增刪右鍵指令"),
                () => Navigator.GoToModule?.Invoke("module.contextmenu")),
            ModuleTile("", "Awake", "保持喚醒",
                Loc.I.Pick("Stop the PC sleeping", "唔畀電腦瞓覺"),
                () => Navigator.GoToModule?.Invoke("module.awake")),
            ModuleTile(((char)0xE767).ToString(), "Voice & Read-Aloud", "語音朗讀",
                Loc.I.Pick("Read text aloud / export WAV", "讀文字出聲／出 WAV"),
                () => Navigator.GoToModule?.Invoke("module.voice")),
            ModuleTile("", "Color Picker", "螢幕取色",
                Loc.I.Pick("Grab any pixel's colour", "攞螢幕任何一點顏色"),
                () => Navigator.GoToModule?.Invoke("module.colorpicker")),
            ModuleTile(((char)0xE823).ToString(), "Time & Unit Tools", "時間與單位工具",
                Loc.I.Pick("World clock, timezone & unit convert", "世界時鐘、時區同單位換算"),
                () => Navigator.GoToModule?.Invoke("module.timeunit")),
            ModuleTile("", "Environment Variables", "環境變數",
                Loc.I.Pick("Edit user/system variables", "編輯使用者／系統變數"),
                () => Navigator.GoToModule?.Invoke("module.envvars")),
            ModuleTile(((char)0xE77F).ToString(), "Clipboard", "剪貼簿",
                Loc.I.Pick("Clipboard history + auto-convert", "剪貼簿歷史 + 自動轉檔"),
                () => Navigator.GoToModule?.Invoke("module.clipboard")),
            ModuleTile(((char)0xECAA).ToString(), "Package Manager", "套件管理",
                Loc.I.Pick("Install apps & deps (winget)", "安裝 app 同相依（winget）"),
                () => Navigator.GoToModule?.Invoke("module.packages")),
            ModuleTile(((char)0xE8EA).ToString(), "Android (ADB)", "Android（ADB）",
                Loc.I.Pick("adb: files, APK backup, logcat, scrcpy", "adb：檔案、APK 備份、logcat、scrcpy"),
                () => Navigator.GoToModule?.Invoke("module.adb")),
            ModuleTile(((char)0xE7BA).ToString(), "Fastboot / Flasher", "Fastboot／刷機",
                Loc.I.Pick("Unlock, flash boot.img, factory, OTA", "解鎖、flash boot.img、原廠、OTA"),
                () => Navigator.GoToModule?.Invoke("module.fastboot")),
            ModuleTile(((char)0xE8EA).ToString(), "Android Emulator", "Android 模擬器",
                Loc.I.Pick("AVDs: create, launch, wipe, delete", "AVD：建立、啟動、清資料、刪除"),
                () => Navigator.GoToModule?.Invoke("module.emulator")),
            ModuleTile(((char)0xE945).ToString(), "VPN & Mesh", "VPN 與網狀網",
                Loc.I.Pick("NordVPN + Tailscale, in-app", "NordVPN + Tailscale，app 內"),
                () => Navigator.GoToModule?.Invoke("module.vpn")),
            ModuleTile(((char)0xE8BD).ToString(), "Communications", "通訊",
                Loc.I.Pick("Mail/Teams/Discord/Slack deep links", "信件／Teams／Discord／Slack 深層連結"),
                () => Navigator.GoToModule?.Invoke("module.comms")),
            ModuleTile(((char)0xE950).ToString(), "Native Utilities", "原生工具",
                Loc.I.Pick("Wi-Fi keys, SMB, brightness, certs, BT…", "Wi-Fi 密碼、SMB、亮度、憑證、藍牙…"),
                () => Navigator.GoToModule?.Invoke("module.native")),
            ModuleTile(((char)0xE945).ToString(), "PowerToys Extras", "PowerToys 額外工具",
                Loc.I.Pick("Resize images, OCR, always-on-top, plain paste", "縮圖、OCR、置頂、純文字貼上"),
                () => Navigator.GoToModule?.Invoke("module.powertoys")),
            ModuleTile(((char)0xEC7A).ToString(), "WSL & VM Launcher", "WSL 與 VM 啟動器",
                Loc.I.Pick("WSL distros + Windows Sandbox", "WSL 發行版 + Windows 沙盒"),
                () => Navigator.GoToModule?.Invoke("module.wslvm")),
            ModuleTile(((char)0xE8D2).ToString(), "Font Manager", "字型管理",
                Loc.I.Pick("Install / preview / uninstall fonts", "裝／睇／移除字型"),
                () => Navigator.GoToModule?.Invoke("module.fonts")),
            ModuleTile("", "Windows 11 control", "Windows 11 控制",
                Loc.I.Pick($"{Categories.All.Length - 1} tweak categories below", $"下面有 {Categories.All.Length - 1} 個調校分類"),
                () => Navigator.GoToCategory?.Invoke(Categories.Appearance)),
        };

        ModuleRepeater.Layout = new UniformGridLayout { MinItemWidth = 320, MinItemHeight = 96, MinRowSpacing = 4, MinColumnSpacing = 4 };
        ModuleRepeater.ItemsSource = tiles;
    }

    /// <summary>旗艦反應堆英雄磚 · The flagship reactor hero tile (accented Cherenkov-blue border).</summary>
    private Button ReactorHeroTile()
    {
        // Reactor-green flagship accent (WinForge.dc.html), not the old Cherenkov blue.
        var accent = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x54, 0xE0, 0x7E));
        var content = TileContent(
            ((char)0xEBC0).ToString(),
            "★ Nuclear Reactor · 核反應堆",
            Loc.I.Pick("FLAGSHIP - PWR simulator",
                       "旗艦 - 壓水堆模擬"),
            30,
            accent,
            accent);
        var button = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(18, 14, 18, 14),
            Margin = new Thickness(0, 0, 8, 8),
            MinWidth = 300,
            MinHeight = 96,
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x2F, 0xAF, 0x57)),
            BorderThickness = new Thickness(2),
        };
        button.Click += (_, _) => Navigator.GoToModule?.Invoke("module.reactor");
        return button;
    }

    private Button ModuleTile(string glyph, string titleEn, string titleZh, string sub, Action onClick)
    {
        var content = TileContent(
            ResolveModuleGlyph(glyph, titleEn),
            $"{titleEn} · {titleZh}",
            sub,
            24);
        var button = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 8, 8),
            MinWidth = 300,
            MinHeight = 76,
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private Grid TileContent(string glyph, string title, string subtitle, double iconSize, Brush? iconBrush = null, Brush? subtitleBrush = null)
    {
        var grid = new Grid
        {
            ColumnSpacing = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new FontIcon
        {
            Glyph = glyph,
            FontSize = iconSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = iconBrush ?? (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        Grid.SetColumn(icon, 0);

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        texts.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.WrapWholeWords,
        });
        texts.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 12,
            Foreground = subtitleBrush ?? (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.WrapWholeWords,
        });
        Grid.SetColumn(texts, 1);

        grid.Children.Add(icon);
        grid.Children.Add(texts);
        return grid;
    }

    private string ResolveModuleGlyph(string glyph, string titleEn)
    {
        if (!string.IsNullOrWhiteSpace(glyph))
            return glyph;

        var module = ModuleRegistry.All.FirstOrDefault(m =>
            string.Equals(m.En, titleEn, StringComparison.OrdinalIgnoreCase) ||
            m.En.StartsWith(titleEn, StringComparison.OrdinalIgnoreCase) ||
            titleEn.StartsWith(m.En, StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(module?.Glyph)
            ? ((char)0xE8B7).ToString()
            : module.Glyph;
    }

    private void RenderCategoryTiles()
    {
        var tiles = new List<UIElement>();
        foreach (var cat in Categories.All)
        {
            var content = TileContent(
                cat.Glyph,
                $"{cat.Name.En} · {cat.Name.Zh}",
                Loc.I.Pick($"{TweakCatalog.CountFor(cat)} features", $"{TweakCatalog.CountFor(cat)} 項功能"),
                22);

            var button = new Button
            {
                Content = content,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 8, 8),
                MinWidth = 280,
                MinHeight = 72,
            };
            var captured = cat;
            button.Click += (_, _) => Navigator.GoToCategory?.Invoke(captured);
            tiles.Add(button);
        }

        CategoryRepeater.Layout = new UniformGridLayout
        {
            MinItemWidth = 300,
            MinItemHeight = 82,
            MinRowSpacing = 4,
            MinColumnSpacing = 4,
        };
        CategoryRepeater.ItemsSource = tiles;
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        var query = sender.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(query))
        {
            SearchResults.Visibility = Visibility.Collapsed;
            BrowseSection.Visibility = Visibility.Visible;
            SearchResults.Children.Clear();
            return;
        }

        BrowseSection.Visibility = Visibility.Collapsed;
        SearchResults.Visibility = Visibility.Visible;
        SearchResults.Children.Clear();

        var matches = TweakCatalog.Search(query).Take(60).ToList();
        var header = new TextBlock
        {
            Text = Loc.I.Pick($"{matches.Count} result(s)", $"{matches.Count} 個結果"),
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        SearchResults.Children.Add(header);

        foreach (var t in matches)
        {
            var card = new TweakCard();
            card.SetTweak(t);
            SearchResults.Children.Add(card);
        }
    }
}
