using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

public static class Win11ProTweaks
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // --- inputintl (20) ---
        Tweak.RegToggle("w11p.inputintl.mouse-accel", "Disable mouse acceleration", "停用滑鼠加速",
            "Turn off Enhance Pointer Precision so mouse movement is 1:1.", "關閉「增強指標精確度」，令滑鼠移動係 1:1。",
            RegRoot.HKCU, "Control Panel\\Mouse", "MouseSpeed", "0", "1",
            RegistryValueKind.String, keywords: "mouse,acceleration,pointer,滑鼠,加速"),
        
        Tweak.RegToggle("w11p.inputintl.mouse-threshold1", "Mouse threshold 1 off", "滑鼠閾值 1 關閉",
            "Set MouseThreshold1 to 0 as part of disabling pointer acceleration.", "將 MouseThreshold1 設為 0，配合停用指標加速。",
            RegRoot.HKCU, "Control Panel\\Mouse", "MouseThreshold1", "0", "6",
            RegistryValueKind.String, keywords: "mouse,threshold,滑鼠,閾值"),
        
        Tweak.RegToggle("w11p.inputintl.mouse-threshold2", "Mouse threshold 2 off", "滑鼠閾值 2 關閉",
            "Set MouseThreshold2 to 0 as part of disabling pointer acceleration.", "將 MouseThreshold2 設為 0，配合停用指標加速。",
            RegRoot.HKCU, "Control Panel\\Mouse", "MouseThreshold2", "0", "10",
            RegistryValueKind.String, keywords: "mouse,threshold,滑鼠,閾值"),
        
        Tweak.RegChoice("w11p.inputintl.keyboard-delay", "Keyboard repeat delay", "鍵盤重複延遲",
            "How long to hold a key before it repeats (0 = shortest).", "撳住一個鍵幾耐先開始重複（0 = 最短）。",
            RegRoot.HKCU, "Control Panel\\Keyboard", "KeyboardDelay", RegistryValueKind.String,
            new (string en, string zh, object value)[] { ("Short (0)", "短 (0)", "0"), ("Medium-short (1)", "中短 (1)", "1"), ("Medium-long (2)", "中長 (2)", "2"), ("Long (3)", "長 (3)", "3") },
            keywords: "keyboard,repeat,delay,鍵盤,延遲"),
        
        Tweak.RegChoice("w11p.inputintl.keyboard-speed", "Keyboard repeat rate", "鍵盤重複速度",
            "How fast a held key repeats (0 = slowest, 31 = fastest).", "撳住一個鍵重複嘅速度（0 = 最慢，31 = 最快）。",
            RegRoot.HKCU, "Control Panel\\Keyboard", "KeyboardSpeed", RegistryValueKind.String,
            new (string en, string zh, object value)[] { ("Slowest (0)", "最慢 (0)", "0"), ("Medium (16)", "中等 (16)", "16"), ("Fast (24)", "快 (24)", "24"), ("Fastest (31)", "最快 (31)", "31") },
            keywords: "keyboard,repeat,rate,speed,鍵盤,速度"),
        
        Tweak.RegToggle("w11p.inputintl.filter-keys", "Filter Keys", "篩選鍵",
            "Ignore brief or repeated keystrokes and slow the repeat rate.", "忽略短暫或重複嘅按鍵，並減慢重複速度。",
            RegRoot.HKCU, "Control Panel\\Accessibility\\Keyboard Response", "Flags", "27", "126",
            RegistryValueKind.String, keywords: "filter,keys,accessibility,篩選鍵,協助工具"),
        
        Tweak.RegToggle("w11p.inputintl.sticky-keys", "Sticky Keys", "相黏鍵",
            "Let modifier keys (Shift, Ctrl, Alt) stay active without holding them.", "等修飾鍵（Shift、Ctrl、Alt）唔使撳住都保持作用。",
            RegRoot.HKCU, "Control Panel\\Accessibility\\StickyKeys", "Flags", "511", "510",
            RegistryValueKind.String, keywords: "sticky,keys,accessibility,相黏鍵,協助工具"),
        
        Tweak.RegToggle("w11p.inputintl.toggle-keys", "Toggle Keys", "切換鍵",
            "Play a tone when Caps Lock, Num Lock or Scroll Lock is pressed.", "撳 Caps Lock、Num Lock 或 Scroll Lock 時播放提示音。",
            RegRoot.HKCU, "Control Panel\\Accessibility\\ToggleKeys", "Flags", "63", "62",
            RegistryValueKind.String, keywords: "toggle,keys,accessibility,切換鍵,協助工具"),
        
        Tweak.RegChoice("w11p.inputintl.first-day-week", "First day of week", "一週嘅第一日",
            "Choose which day the calendar week starts on.", "揀日曆每週由邊一日開始。",
            RegRoot.HKCU, "Control Panel\\International", "iFirstDayOfWeek", RegistryValueKind.String,
            new (string en, string zh, object value)[] { ("Monday", "星期一", "0"), ("Tuesday", "星期二", "1"), ("Wednesday", "星期三", "2"), ("Thursday", "星期四", "3"), ("Friday", "星期五", "4"), ("Saturday", "星期六", "5"), ("Sunday", "星期日", "6") },
            keywords: "first,day,week,calendar,一週,日曆"),
        
        Tweak.RegChoice("w11p.inputintl.short-date", "Short date format", "短日期格式",
            "Pick how short dates are displayed throughout Windows.", "揀 Windows 顯示短日期嘅格式。",
            RegRoot.HKCU, "Control Panel\\International", "sShortDate", RegistryValueKind.String,
            new (string en, string zh, object value)[] { ("yyyy-MM-dd", "yyyy-MM-dd", "yyyy-MM-dd"), ("dd/MM/yyyy", "dd/MM/yyyy", "dd/MM/yyyy"), ("MM/dd/yyyy", "MM/dd/yyyy", "MM/dd/yyyy"), ("d/M/yyyy", "d/M/yyyy", "d/M/yyyy") },
            keywords: "short,date,format,日期,格式"),
        
        Tweak.RegChoice("w11p.inputintl.short-time", "Time format (24h / 12h)", "時間格式（24 / 12 小時）",
            "Switch the clock between 24-hour and 12-hour display.", "將時鐘喺 24 小時同 12 小時之間切換。",
            RegRoot.HKCU, "Control Panel\\International", "sShortTime", RegistryValueKind.String,
            new (string en, string zh, object value)[] { ("24-hour (HH:mm)", "24 小時 (HH:mm)", "HH:mm"), ("12-hour (h:mm tt)", "12 小時 (h:mm tt)", "h:mm tt") },
            keywords: "time,format,24h,12h,時間,格式"),
        
        Tweak.RegChoice("w11p.inputintl.caret-blink", "Cursor blink rate", "游標閃爍速度",
            "How fast the text caret blinks, in milliseconds.", "文字游標閃爍嘅速度，以毫秒計。",
            RegRoot.HKCU, "Control Panel\\Desktop", "CursorBlinkRate", RegistryValueKind.String,
            new (string en, string zh, object value)[] { ("Fast (200ms)", "快 (200ms)", "200"), ("Default (530ms)", "預設 (530ms)", "530"), ("Slow (1200ms)", "慢 (1200ms)", "1200"), ("No blink", "唔閃爍", "-1") },
            keywords: "cursor,caret,blink,游標,閃爍"),
        
        Tweak.RegChoice("w11p.inputintl.double-click-speed", "Double-click speed", "連按速度",
            "Maximum time between two clicks to count as a double-click (ms).", "兩下撳之間算作連按嘅最長時間（毫秒）。",
            RegRoot.HKCU, "Control Panel\\Mouse", "DoubleClickSpeed", RegistryValueKind.String,
            new (string en, string zh, object value)[] { ("Fast (200ms)", "快 (200ms)", "200"), ("Default (500ms)", "預設 (500ms)", "500"), ("Slow (900ms)", "慢 (900ms)", "900") },
            keywords: "double,click,speed,連按,雙擊,速度"),
        
        Tweak.RegToggle("w11p.inputintl.swap-mouse-buttons", "Swap mouse buttons", "對調滑鼠按鍵",
            "Switch primary and secondary buttons for left-handed use.", "對調左右鍵，方便左手使用。",
            RegRoot.HKCU, "Control Panel\\Mouse", "SwapMouseButtons", "1", "0",
            RegistryValueKind.String, keywords: "swap,mouse,buttons,left-handed,對調,滑鼠,左手"),
        
        Tweak.RegChoice("w11p.inputintl.wheel-scroll-lines", "Wheel scroll lines", "滾輪捲動行數",
            "Number of lines scrolled per mouse-wheel notch.", "滑鼠滾輪每格捲動嘅行數。",
            RegRoot.HKCU, "Control Panel\\Desktop", "WheelScrollLines", RegistryValueKind.String,
            new (string en, string zh, object value)[] { ("1 line", "1 行", "1"), ("3 lines (default)", "3 行（預設）", "3"), ("5 lines", "5 行", "5"), ("One screen", "一個畫面", "-1") },
            keywords: "wheel,scroll,lines,滾輪,捲動,行數"),
        
        Tweak.RegChoice("w11p.inputintl.caret-width", "Caret (cursor) width", "游標闊度",
            "Width in pixels of the blinking text caret.", "閃爍文字游標嘅闊度（像素）。",
            RegRoot.HKCU, "Control Panel\\Desktop", "CaretWidth", RegistryValueKind.DWord,
            new (string en, string zh, object value)[] { ("Thin (1px)", "幼 (1px)", 1), ("Medium (2px)", "中 (2px)", 2), ("Thick (5px)", "粗 (5px)", 5) },
            keywords: "caret,width,cursor,游標,闊度"),
        
        Tweak.Shell("w11p.inputintl.open-mouse-touchpad", "Open Mouse & touchpad settings", "開啟滑鼠同觸控板設定",
            "Launch the Mouse and touchpad Settings page.", "開啟「滑鼠同觸控板」設定版面。",
            "Open", "開啟", "cmd.exe", "/c start ms-settings:mousetouchpad",
            keywords: "mouse,touchpad,settings,滑鼠,觸控板,設定"),
        
        Tweak.Shell("w11p.inputintl.open-region-format", "Open Region format settings", "開啟地區格式設定",
            "Launch the Region formatting Settings page.", "開啟「地區格式」設定版面。",
            "Open", "開啟", "cmd.exe", "/c start ms-settings:regionformatting",
            keywords: "region,format,locale,地區,格式,設定"),
        
        Tweak.Shell("w11p.inputintl.open-eoa-keyboard", "Open Keyboard accessibility", "開啟鍵盤協助工具",
            "Launch the keyboard ease-of-access Settings page (Sticky/Filter/Toggle keys).", "開啟鍵盤「輕鬆存取」設定（相黏／篩選／切換鍵）。",
            "Open", "開啟", "cmd.exe", "/c start ms-settings:easeofaccess-keyboard",
            keywords: "keyboard,accessibility,easeofaccess,鍵盤,協助工具"),
        
        Tweak.Shell("w11p.inputintl.open-typing", "Open Typing settings", "開啟輸入設定",
            "Launch the Typing Settings page (autocorrect, suggestions, text input).", "開啟「輸入」設定版面（自動更正、建議、文字輸入）。",
            "Open", "開啟", "cmd.exe", "/c start ms-settings:typing",
            keywords: "typing,autocorrect,suggestions,輸入,打字,設定"),

        // --- storagenotif (20) ---
        Tweak.RegToggle("w11p.storagenotif.storage-sense", "Storage Sense", "儲存空間感知功能",
            "Automatically free up space by deleting temporary files and emptying the recycle bin.", "自動刪除暫存檔同清空回收筒嚟釋放空間。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "01", 1, 0,
            keywords: "storage,sense,cleanup,儲存,清理"),
        
        Tweak.RegChoice("w11p.storagenotif.recyclebin-retention", "Recycle Bin retention", "回收筒保留期",
            "Choose how long files stay in the Recycle Bin before Storage Sense deletes them.", "揀回收筒入面嘅檔案幾耐之後俾儲存空間感知功能刪除。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "256", RegistryValueKind.DWord,
            new (string en, string zh, object value)[] {
                ("Never", "永不", 0),
                ("1 day", "1 日", 1),
                ("14 days", "14 日", 14),
                ("30 days", "30 日", 30),
                ("60 days", "60 日", 60)
            },
            keywords: "recycle,bin,retention,回收筒,保留"),
        
        Tweak.RegToggle("w11p.storagenotif.notifications-global", "All notifications", "所有通知",
            "Master switch for toast notifications from apps and the system.", "應用程式同系統通知嘅總開關。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings", "NOC_GLOBAL_SETTING_TOASTS_ENABLED", 1, 0,
            keywords: "notifications,toast,通知"),
        
        Tweak.RegToggle("w11p.storagenotif.notification-sound", "Notification sound", "通知聲音",
            "Play a sound when a notification arrives.", "通知嚟到嗰陣播聲音提示。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings", "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND", 1, 0,
            keywords: "notification,sound,聲音,通知"),
        
        Tweak.RegToggle("w11p.storagenotif.lockscreen-notifications", "Lock-screen notifications", "鎖定畫面通知",
            "Show notifications on the lock screen.", "喺鎖定畫面顯示通知。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings", "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK", 1, 0,
            keywords: "lockscreen,notifications,鎖定,通知"),
        
        Tweak.Shell("w11p.storagenotif.open-focus", "Open Focus settings", "開啟專注設定",
            "Open the Focus / quiet hours settings page.", "開啟專注（勿擾）設定頁面。",
            "Open", "開啟", "explorer.exe", "ms-settings:quiethours",
            keywords: "focus,quiet,hours,專注,勿擾"),
        
        Tweak.Shell("w11p.storagenotif.open-notifications", "Open Notifications settings", "開啟通知設定",
            "Open the Notifications settings page.", "開啟通知設定頁面。",
            "Open", "開啟", "explorer.exe", "ms-settings:notifications",
            keywords: "notifications,settings,通知,設定"),
        
        Tweak.RegToggle("w11p.storagenotif.snap-assist", "Snap Assist", "貼齊助手",
            "Show suggested windows to fill the rest of the screen when you snap a window.", "貼齊視窗嗰陣建議其他視窗填滿剩餘螢幕。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "SnapAssist", 1, 0,
            restart: RestartScope.Explorer, keywords: "snap,assist,貼齊,助手"),
        
        Tweak.RegToggle("w11p.storagenotif.snap-fill", "Snap fill", "貼齊自動填充",
            "When snapping a window, automatically resize it to fill available space.", "貼齊視窗嗰陣自動調整大細填滿可用空間。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "SnapFill", 1, 0,
            restart: RestartScope.Explorer, keywords: "snap,fill,貼齊,填充"),
        
        Tweak.RegToggle("w11p.storagenotif.joint-resize", "Joint resize snapped windows", "同時調整貼齊視窗",
            "Resize two snapped windows at the same time by dragging the divider.", "拖動分隔線同時調整兩個貼齊視窗嘅大細。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "JointResize", 1, 0,
            restart: RestartScope.Explorer, keywords: "joint,resize,snap,貼齊,調整"),
        
        Tweak.RegToggle("w11p.storagenotif.snap-bar", "Snap layout bar", "貼齊版面工具列",
            "Show the snap layouts bar at the top of the screen when dragging a window.", "拖動視窗嗰陣喺螢幕頂顯示貼齊版面工具列。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "EnableSnapBar", 1, 0,
            restart: RestartScope.Explorer, keywords: "snap,bar,layout,貼齊,工具列"),
        
        Tweak.RegToggle("w11p.storagenotif.snap-flyout", "Snap layout flyout", "貼齊版面飛出視窗",
            "Show snap layouts when you hover over a window's maximize button.", "滑鼠停喺最大化掣上面嗰陣顯示貼齊版面。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "EnableSnapAssistFlyout", 1, 0,
            restart: RestartScope.Explorer, keywords: "snap,flyout,maximize,貼齊,飛出"),
        
        Tweak.RegToggle("w11p.storagenotif.window-arrangement", "Window arrangement (animations)", "視窗排列動畫",
            "Enable smooth animations when minimizing, maximizing and arranging windows.", "最小化、最大化同排列視窗嗰陣啟用平滑動畫。",
            RegRoot.HKCU, @"Control Panel\Desktop", "WindowArrangementActive", "1", "0", RegistryValueKind.String,
            restart: RestartScope.Explorer, keywords: "window,arrangement,animation,視窗,排列,動畫"),
        
        Tweak.RegToggle("w11p.storagenotif.snap-suggestions", "Show app suggestions in Snap", "貼齊顯示應用程式建議",
            "When snapping, show recently used apps as suggestions to fill the layout.", "貼齊嗰陣顯示最近用過嘅應用程式作為填充建議。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "SnapAssistSuggestions", 1, 0,
            restart: RestartScope.Explorer, keywords: "snap,suggestions,貼齊,建議"),
        
        Tweak.Shell("w11p.storagenotif.open-multitasking", "Open Multitasking settings", "開啟多工處理設定",
            "Open the Multitasking settings page (snap, alt-tab, virtual desktops).", "開啟多工處理設定頁面（貼齊、Alt-Tab、虛擬桌面）。",
            "Open", "開啟", "explorer.exe", "ms-settings:multitasking",
            keywords: "multitasking,snap,altab,多工"),
        
        Tweak.Cmd("w11p.storagenotif.onedrive-shutdown", "Shut down OneDrive", "關閉 OneDrive",
            "Quit the OneDrive sync client for this session.", "喺今次工作階段關閉 OneDrive 同步程式。",
            "Shut down", "關閉", "\"%LOCALAPPDATA%\\Microsoft\\OneDrive\\OneDrive.exe\" /shutdown",
            keywords: "onedrive,shutdown,同步"),
        
        Tweak.Shell("w11p.storagenotif.open-storage", "Open Storage settings", "開啟儲存空間設定",
            "Open the Storage / Storage Sense settings page.", "開啟儲存空間（儲存空間感知功能）設定頁面。",
            "Open", "開啟", "explorer.exe", "ms-settings:storagesense",
            keywords: "storage,sense,儲存,設定"),
        
        Tweak.RegToggle("w11p.storagenotif.vd-all-monitors", "Virtual desktops on all monitors", "所有螢幕顯示虛擬桌面",
            "When switching virtual desktops, switch on all monitors instead of just one.", "切換虛擬桌面嗰陣同時喺所有螢幕切換而唔係淨係一個。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "VirtualDesktopAllMonitorsEnabled", 1, 0,
            restart: RestartScope.Explorer, keywords: "virtual,desktop,monitors,虛擬桌面,螢幕"),
        
        Tweak.RegChoice("w11p.storagenotif.alttab-tabs", "Alt-Tab shows browser tabs", "Alt-Tab 顯示瀏覽器分頁",
            "Choose how many Microsoft Edge tabs appear in Alt-Tab alongside open windows.", "揀 Alt-Tab 入面除咗視窗仲顯示幾多個 Microsoft Edge 分頁。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "MultiTaskingAltTabFilter", RegistryValueKind.DWord,
            new (string en, string zh, object value)[] {
                ("Open windows only", "淨係開啟嘅視窗", 3),
                ("20 most recent tabs", "最近 20 個分頁", 0),
                ("5 most recent tabs", "最近 5 個分頁", 1),
                ("3 most recent tabs", "最近 3 個分頁", 2)
            },
            restart: RestartScope.Explorer, keywords: "alttab,tabs,edge,分頁,切換"),
        
        Tweak.RegToggle("w11p.storagenotif.show-snap-tips", "Show snap layout tips", "顯示貼齊版面提示",
            "Show a tip flyout reminding you snap layouts are available.", "顯示提示飛出視窗提醒你貼齊版面功能可用。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "EnableSnapAssistFlyout2", 1, 0,
            restart: RestartScope.Explorer, keywords: "snap,tips,layout,貼齊,提示"),

        // --- perfboot (20) ---
        Tweak.Cmd("w11p.perfboot.free-reserved", "Disable reserved storage", "停用保留儲存空間",
            "Free the ~7GB Windows reserves for updates by disabling reserved storage.", "釋放 Windows 為更新預留嘅約 7GB 空間，停用保留儲存。",
            "Disable", "停用", "DISM /Online /Set-ReservedStorageState /State:Disabled",
            requiresAdmin: true, keywords: "dism,reserved,storage,保留,儲存,空間"),
        
        Tweak.RegChoice("w11p.perfboot.processor-scheduling", "Processor scheduling", "處理器排程",
            "Optimise CPU scheduling for foreground programs or background services.", "為前景程式定後台服務優化 CPU 排程。",
            RegRoot.HKLM, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", RegistryValueKind.DWord,
            new (string en, string zh, object value)[] {
                ("Programs (foreground)", "程式（前景）", 0x26),
                ("Background services", "後台服務", 0x18)
            },
            requiresAdmin: true, keywords: "processor,scheduling,priority,排程,優先,前景,後台"),
        
        Tweak.Cmd("w11p.perfboot.usb-selective-suspend", "Disable USB selective suspend", "停用 USB 選擇性暫停",
            "Stop Windows from powering down USB devices on the active power plan.", "唔俾 Windows 喺目前電源計劃熄 USB 裝置電。",
            "Disable", "停用", "powercfg /SETACVALUEINDEX SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0 && powercfg /SETDCVALUEINDEX SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0 && powercfg /SETACTIVE SCHEME_CURRENT",
            requiresAdmin: true, keywords: "usb,selective,suspend,powercfg,暫停,電源"),
        
        Tweak.Powershell("w11p.perfboot.network-private", "Set network to Private", "設網絡為私人",
            "Mark the active network connection profile as Private for better local performance and discovery.", "將目前網絡連線設為私人，改善本地效能同裝置探索。",
            "Set Private", "設私人", "Get-NetConnectionProfile | Set-NetConnectionProfile -NetworkCategory Private",
            requiresAdmin: true, keywords: "network,private,profile,網絡,私人"),
        
        Tweak.Cmd("w11p.perfboot.hyperv-launch-off", "Hypervisor launch off (bcdedit)", "關閉虛擬器啟動（bcdedit）",
            "Use bcdedit to set hypervisorlaunchtype to Off; can improve game performance. Reboot required.", "用 bcdedit 將 hypervisorlaunchtype 設為 Off；可提升遊戲效能。要重啟先生效。",
            "Set Off", "設 Off", "bcdedit /set hypervisorlaunchtype off",
            requiresAdmin: true, restart: RestartScope.Reboot, keywords: "bcdedit,hypervisor,off,虛擬,啟動,遊戲"),
        
        Tweak.Cmd("w11p.perfboot.hyperv-launch-auto", "Hypervisor launch auto (bcdedit)", "自動虛擬器啟動（bcdedit）",
            "Use bcdedit to set hypervisorlaunchtype to Auto, re-enabling virtualization features. Reboot required.", "用 bcdedit 將 hypervisorlaunchtype 設為 Auto，重新啟用虛擬化功能。要重啟先生效。",
            "Set Auto", "設 Auto", "bcdedit /set hypervisorlaunchtype auto",
            requiresAdmin: true, restart: RestartScope.Reboot, keywords: "bcdedit,hypervisor,auto,虛擬,啟動"),
        
        Tweak.Cmd("w11p.perfboot.rebuild-perf-counters", "Rebuild performance counters", "重建效能計數器",
            "Rebuild the Windows performance counter registry from system backups using lodctr /R.", "用 lodctr /R 由系統備份重建 Windows 效能計數器登錄。",
            "Rebuild", "重建", "lodctr /R",
            requiresAdmin: true, keywords: "lodctr,performance,counters,計數器,效能"),
        
        Tweak.Powershell("w11p.perfboot.memory-compression-off", "Disable memory compression", "停用記憶體壓縮",
            "Turn off MMAgent memory compression; can reduce CPU use on systems with plenty of RAM.", "關閉 MMAgent 記憶體壓縮；RAM 充足嘅機可減少 CPU 用量。",
            "Disable", "停用", "Disable-MMAgent -MemoryCompression",
            requiresAdmin: true, restart: RestartScope.Reboot, keywords: "memory,compression,mmagent,記憶體,壓縮"),
        
        Tweak.Powershell("w11p.perfboot.memory-compression-on", "Enable memory compression", "啟用記憶體壓縮",
            "Turn on MMAgent memory compression to fit more data in RAM and reduce paging.", "開啟 MMAgent 記憶體壓縮，喺 RAM 裝多啲資料、減少分頁。",
            "Enable", "啟用", "Enable-MMAgent -MemoryCompression",
            requiresAdmin: true, restart: RestartScope.Reboot, keywords: "memory,compression,mmagent,記憶體,壓縮"),
        
        Tweak.RegToggle("w11p.perfboot.hags", "Hardware-accelerated GPU scheduling", "硬件加速 GPU 排程",
            "Toggle HAGS, letting the GPU manage its own video memory for lower latency.", "切換 HAGS，等 GPU 自行管理顯示記憶體，降低延遲。",
            RegRoot.HKLM, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, 1,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.Reboot, keywords: "hags,gpu,scheduling,排程,顯示,加速"),
        
        Tweak.RegChoice("w11p.perfboot.crash-dump", "Crash dump type", "當機傾印類型",
            "Set the kind of memory dump Windows writes on a blue screen; minidump is smallest.", "設 Windows 藍畫面時寫嘅記憶體傾印類型；minidump 最細。",
            RegRoot.HKLM, @"SYSTEM\CurrentControlSet\Control\CrashControl", "CrashDumpEnabled", RegistryValueKind.DWord,
            new (string en, string zh, object value)[] {
                ("Small (minidump)", "細（minidump）", 3),
                ("Kernel", "核心", 2),
                ("Complete", "完整", 1),
                ("None", "無", 0)
            },
            requiresAdmin: true, keywords: "crash,dump,minidump,當機,傾印,藍畫面"),
        
        Tweak.Cmd("w11p.perfboot.disable-sysmain", "Disable SysMain (Superfetch)", "停用 SysMain（Superfetch）",
            "Set the SysMain service to disabled; can help on SSDs by cutting background disk activity.", "將 SysMain 服務設為停用；SSD 機可減少背景磁碟活動。",
            "Disable", "停用", "sc config SysMain start= disabled && sc stop SysMain",
            requiresAdmin: true, destructive: true, keywords: "sysmain,superfetch,service,服務,prefetch"),
        
        Tweak.RegToggle("w11p.perfboot.tdr-delay", "GPU TDR delay", "GPU TDR 延遲",
            "Increase the GPU timeout detection and recovery delay to 8 seconds to reduce driver-reset crashes.", "將 GPU 逾時偵測復原（TDR）延遲增至 8 秒，減少驅動重置當機。",
            RegRoot.HKLM, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "TdrDelay", 8, null,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.Reboot, keywords: "tdr,delay,gpu,driver,延遲,驅動"),
        
        Tweak.RegToggle("w11p.perfboot.rebuild-search-index", "Rebuild search index", "重建搜尋索引",
            "Flag the Windows Search index for a full rebuild on next service start.", "標記 Windows 搜尋索引，喺服務下次啟動時完整重建。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows Search", "SetupCompletedSuccessfully", 0, 1,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.Reboot, keywords: "search,index,rebuild,搜尋,索引,重建"),
        
        Tweak.RegToggle("w11p.perfboot.startup-delay", "Disable startup app delay", "停用啟動程式延遲",
            "Remove the artificial delay before startup apps launch after sign-in.", "移除登入後啟動程式開始前嘅人為延遲。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0, null,
            RegistryValueKind.DWord, restart: RestartScope.Explorer, keywords: "startup,delay,boot,啟動,延遲"),
        
        Tweak.Cmd("w11p.perfboot.ntfs-lastaccess", "Disable NTFS last-access", "停用 NTFS 最後存取",
            "Stop NTFS from updating the last-access timestamp on every file read to cut disk overhead.", "唔俾 NTFS 每次讀檔都更新最後存取時間，減少磁碟負擔。",
            "Disable", "停用", "fsutil behavior set disablelastaccess 1",
            requiresAdmin: true, keywords: "ntfs,lastaccess,fsutil,存取,時間"),
        
        Tweak.Powershell("w11p.perfboot.disable-hpet", "Disable HPET timer", "停用 HPET 計時器",
            "Disable the High Precision Event Timer device; some systems see lower latency. Re-enable in Device Manager if unstable.", "停用高精度事件計時器（HPET）裝置；部分機可降低延遲。如唔穩定喺裝置管理員重新啟用。",
            "Disable", "停用", "Get-PnpDevice -FriendlyName 'High precision event timer' | Disable-PnpDevice -Confirm:$false",
            requiresAdmin: true, destructive: true, restart: RestartScope.Reboot, keywords: "hpet,timer,latency,計時器,延遲"),
        
        Tweak.Cmd("w11p.perfboot.power-high-performance", "Activate High Performance plan", "啟用高效能電源計劃",
            "Switch the active power plan to High Performance to keep the CPU at full speed.", "將目前電源計劃切換到高效能，令 CPU 維持全速。",
            "Activate", "啟用", "powercfg /SETACTIVE 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
            requiresAdmin: true, keywords: "power,plan,high,performance,電源,效能"),
        
        Tweak.Cmd("w11p.perfboot.disable-fast-startup", "Disable Fast Startup", "停用快速啟動",
            "Turn off hybrid Fast Startup so shutdown fully clears state; fixes some boot and driver issues.", "關閉混合快速啟動，令關機完整清除狀態；解決部分開機同驅動問題。",
            "Disable", "停用", "powercfg /hibernate off",
            requiresAdmin: true, restart: RestartScope.Reboot, keywords: "fast,startup,hibernate,快速,啟動,休眠"),
        
        Tweak.Cmd("w11p.perfboot.disable-boot-logo", "Disable boot animation", "停用開機動畫",
            "Skip the spinning boot logo animation via bcdedit for a marginally faster, plainer boot.", "用 bcdedit 跳過開機旋轉標誌動畫，開機略快、更簡潔。",
            "Disable", "停用", "bcdedit /set bootuxdisabled on",
            requiresAdmin: true, restart: RestartScope.Reboot, keywords: "boot,logo,animation,bootux,開機,動畫"),

        // --- explorermore (20) ---
        Tweak.RegToggle("w11p.explorermore.tray-all-icons", "Always show all tray icons (legacy)", "永遠顯示所有系統匣圖示（舊版）",
            "Note: Win11 has no single registry switch; use Settings > Personalization > Taskbar > Other system tray icons. This flips the legacy EnableAutoTray flag (affects older shells).", "注意：Win11 冇單一登錄機碼，要去設定 > 個人化 > 工作列 > 其他系統匣圖示。呢個掣係改舊版 EnableAutoTray 旗標（影響舊版殼層）。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer", "EnableAutoTray", 0, 1,
            RegistryValueKind.DWord, restart: RestartScope.Explorer, keywords: "tray,icons,系統匣,圖示,autotray"),
        
        Tweak.RegToggle("w11p.explorermore.remove-3d-objects", "Remove 3D Objects folder", "移除 3D 物件資料夾",
            "Hide the 3D Objects folder from This PC by setting its namespace ThisPCPolicy to Hide.", "由我的電腦度隱藏 3D 物件資料夾，方法係將佢命名空間嘅 ThisPCPolicy 設做 Hide。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}", "ThisPCPolicy", "Hide", "Show",
            RegistryValueKind.String, requiresAdmin: true, restart: RestartScope.Explorer, keywords: "3d,objects,thispc,3d物件,我的電腦"),
        
        Tweak.RegToggle("w11p.explorermore.expand-ribbon", "Expand the legacy ribbon by default", "預設展開舊版功能區",
            "Keep the legacy/Win10-style File Explorer ribbon expanded instead of minimized.", "令檔案總管嘅舊版/Win10 樣式功能區一開就展開，唔好縮埋。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Ribbon", "MinimizedStateTabletModeOff", 0, 1,
            RegistryValueKind.DWord, restart: RestartScope.Explorer, keywords: "ribbon,expand,功能區,展開"),
        
        Tweak.RegToggle("w11p.explorermore.separate-process", "Launch folder windows in a separate process", "用獨立處理序開資料夾視窗",
            "Run each Explorer window in its own process so one crash doesn't take down the rest.", "每個總管視窗用自己嘅處理序行，一個崩潰唔會拖冚其他。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "SeparateProcess", 1, 0,
            RegistryValueKind.DWord, restart: RestartScope.Explorer, keywords: "separate,process,獨立,處理序,explorer"),
        
        Tweak.RegChoice("w11p.explorermore.drive-letters-first", "Drive letter display order", "磁碟機代號顯示次序",
            "Choose where the drive letter appears relative to the volume label in This PC.", "揀磁碟機代號喺我的電腦度相對於磁碟區標籤擺邊。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "ShowDriveLettersFirst", RegistryValueKind.DWord,
            new (string en, string zh, object value)[] {
                ("Letter after label (default)", "代號喺標籤後（預設）", 0),
                ("Letter before label, network only", "代號喺前，淨係網路磁碟", 1),
                ("Hide all drive letters", "隱藏所有代號", 2),
                ("Letter before label, all drives", "代號喺前，所有磁碟", 4)
            }, requiresAdmin: true, restart: RestartScope.Explorer, keywords: "drive,letter,代號,磁碟,次序"),
        
        Tweak.RegToggle("w11p.explorermore.restore-folders", "Restore folder windows at logon", "登入時還原資料夾視窗",
            "Reopen the Explorer windows that were open when you last signed out.", "登入返時，重新開返你上次登出前開住嘅總管視窗。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "PersistBrowsers", 1, 0,
            RegistryValueKind.DWord, restart: RestartScope.Explorer, keywords: "restore,persist,還原,登入,folders"),
        
        Tweak.RegToggle("w11p.explorermore.disable-thumb-cache", "Disable thumbnail caching", "停用縮圖快取",
            "Stop Explorer from caching thumbnails in thumbcache_*.db; thumbnails regenerate each time.", "唔再喺 thumbcache_*.db 度快取縮圖，每次重新產生縮圖。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "DisableThumbnailCache", 1, 0,
            RegistryValueKind.DWord, restart: RestartScope.Explorer, keywords: "thumbnail,cache,縮圖,快取"),
        
        Tweak.RegToggle("w11p.explorermore.max-icon-cache", "Maximise icon cache size", "加大圖示快取容量",
            "Raise the icon cache limit to 4096 KB so large icon sets stay cached and load faster.", "將圖示快取上限調到 4096 KB，等大量圖示留喺快取度，載入快啲。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "Max Cached Icons", "4096", "2000",
            RegistryValueKind.String, requiresAdmin: true, restart: RestartScope.Explorer, keywords: "icon,cache,圖示,快取,max"),
        
        Tweak.RegToggle("w11p.explorermore.disable-start-websearch", "Disable web search suggestions in Start", "停用開始功能表網路搜尋建議",
            "Block Bing/web suggestions in the Start menu search box via the Explorer policy.", "用 Explorer 原則封住開始功能表搜尋框嘅 Bing/網路建議。",
            RegRoot.HKCU, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, 0,
            RegistryValueKind.DWord, restart: RestartScope.Explorer, keywords: "start,web,search,suggestions,開始,網路,建議"),
        
        Tweak.RegToggle("w11p.explorermore.disable-lockscreen", "Disable the lock screen", "停用鎖定畫面",
            "Note: skip the lock screen so logon goes straight to the password prompt. Requires the Personalization policy key (no-op on some Home builds).", "注意：跳過鎖定畫面，登入直接去密碼提示。要 Personalization 原則機碼（部分家用版唔生效）。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Personalization", "NoLockScreen", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.Reboot, keywords: "lockscreen,lock,鎖定,畫面"),
        
        Tweak.RegToggle("w11p.explorermore.disable-bing-search", "Disable Bing in Windows Search", "停用 Windows 搜尋嘅 Bing",
            "Turn off Bing/web results in Search via BingSearchEnabled.", "用 BingSearchEnabled 關閉搜尋度嘅 Bing/網路結果。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, 1,
            RegistryValueKind.DWord, restart: RestartScope.Explorer, keywords: "bing,search,搜尋,網路"),
        
        Tweak.RegToggle("w11p.explorermore.file-op-details", "Show details in file operation dialogs", "檔案操作對話框顯示詳細資料",
            "Always expand the More details view in copy/move/delete progress dialogs.", "喺複製/移動/刪除進度對話框度永遠展開「更多詳細資料」檢視。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\OperationStatusManager", "EnthusiastMode", 1, 0,
            RegistryValueKind.DWord, restart: RestartScope.Explorer, keywords: "file,operation,details,複製,詳細,enthusiast"),
        
        Tweak.RegToggle("w11p.explorermore.confirm-file-delete", "Always confirm before deleting files", "刪除檔案前永遠確認",
            "Show the delete confirmation dialog when sending files to the Recycle Bin.", "將檔案掉入資源回收筒時，顯示刪除確認對話框。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ConfirmFileDelete", 1, 0,
            RegistryValueKind.DWord, restart: RestartScope.Explorer, keywords: "confirm,delete,recycle,確認,刪除,回收筒"),
        
        Tweak.RegToggle("w11p.explorermore.disable-shake", "Disable Aero Shake to minimise", "停用 Aero 搖晃最小化",
            "Stop other windows from minimising when you shake the active window's title bar.", "搖動使用中視窗嘅標題列時，唔好再最小化其他視窗。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "DisallowShaking", 1, 0,
            RegistryValueKind.DWord, restart: RestartScope.Explorer, keywords: "shake,aero,搖晃,最小化"),
        
        Tweak.RegToggle("w11p.explorermore.classic-volume-mixer", "Restore classic volume mixer", "還原傳統音量混音器",
            "Note: Win11 routes the speaker icon to the modern Settings mixer; this MTCUVC flag restores the classic SndVol flyout where the build still supports it.", "注意：Win11 將喇叭圖示帶去新版設定混音器；呢個 MTCUVC 旗標喺仲支援嘅組建度還原傳統 SndVol 飛出視窗。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\MTCUVC", "EnableMtcUvc", 0, 1,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.Explorer, keywords: "volume,mixer,音量,混音器,sndvol"),
        
        Tweak.RegToggle("w11p.explorermore.clock-seconds", "Show seconds in the system clock", "系統時鐘顯示秒數",
            "Display seconds on the taskbar clock (costs a little extra battery/CPU).", "喺工作列時鐘度顯示秒數（會多用少少電/CPU）。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSecondsInSystemClock", 1, 0,
            RegistryValueKind.DWord, restart: RestartScope.Explorer, keywords: "seconds,clock,秒,時鐘"),
        
        Tweak.RegToggle("w11p.explorermore.no-recent-quickaccess", "Hide recent files in Quick Access", "快速存取隱藏最近檔案",
            "Stop Quick Access from listing recently used files in Explorer.", "唔好喺快速存取度列出最近用過嘅檔案。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowRecent", 0, 1,
            RegistryValueKind.DWord, restart: RestartScope.Explorer, keywords: "recent,quick,access,最近,快速存取"),
        
        Tweak.RegToggle("w11p.explorermore.no-frequent-quickaccess", "Hide frequent folders in Quick Access", "快速存取隱藏常用資料夾",
            "Stop Quick Access from listing frequently used folders in Explorer.", "唔好喺快速存取度列出常用嘅資料夾。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowFrequent", 0, 1,
            RegistryValueKind.DWord, restart: RestartScope.Explorer, keywords: "frequent,quick,access,常用,快速存取"),
        
        Tweak.RegChoice("w11p.explorermore.mm-taskbar-mode", "Taskbar on multiple displays", "多顯示器工作列模式",
            "Choose where taskbar buttons appear when you use more than one monitor.", "揀用多過一部顯示器時，工作列按鈕喺邊度出現。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "MMTaskbarMode", RegistryValueKind.DWord,
            new (string en, string zh, object value)[] {
                ("All taskbars", "所有工作列", 0),
                ("Main taskbar + where window is open", "主工作列加視窗所在", 1),
                ("Taskbar where window is open", "視窗所在嗰個工作列", 2)
            }, restart: RestartScope.Explorer, keywords: "taskbar,multiple,displays,工作列,多顯示器,mmtaskbar"),
        
        Tweak.RegChoice("w11p.explorermore.taskbar-labels", "Combine taskbar buttons / show labels", "合併工作列按鈕／顯示標籤",
            "Note: Win11 22H2+ controls labels via TaskbarGlomLevel; pick when buttons combine and labels show.", "注意：Win11 22H2+ 用 TaskbarGlomLevel 控制標籤；揀幾時合併按鈕同顯示標籤。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarGlomLevel", RegistryValueKind.DWord,
            new (string en, string zh, object value)[] {
                ("Always combine, hide labels", "永遠合併，隱藏標籤", 0),
                ("Combine when taskbar is full", "工作列滿時先合併", 1),
                ("Never combine, show labels", "永不合併，顯示標籤", 2)
            }, restart: RestartScope.Explorer, keywords: "labels,combine,taskbar,標籤,合併,glom"),

        // --- settingslinks (20) ---
        Tweak.Cmd("w11p.settingslinks.bluetooth", "Open Bluetooth & devices", "開啟藍牙同裝置",
            "Jump straight to the Bluetooth & devices settings page.", "直接跳去藍牙同裝置嘅設定版面。",
            "Open", "開啟", "start ms-settings:bluetooth", keywords: "bluetooth,devices,藍牙,裝置"),
        
        Tweak.Cmd("w11p.settingslinks.display", "Open Display settings", "開啟顯示器設定",
            "Open the Display settings page for resolution, scaling and arrangement.", "開啟顯示器設定，調解像度、縮放同排列。",
            "Open", "開啟", "start ms-settings:display", keywords: "display,resolution,scaling,顯示,解像度"),
        
        Tweak.Cmd("w11p.settingslinks.nightlight", "Open Night light settings", "開啟夜燈設定",
            "Open the Night light page to warm the screen colours at night.", "開啟夜燈版面，夜晚調暖屏幕色溫。",
            "Open", "開啟", "start ms-settings:nightlight", keywords: "night light,blue light,夜燈,藍光"),
        
        Tweak.Cmd("w11p.settingslinks.sound", "Open Sound settings", "開啟音效設定",
            "Open the Sound page to pick output, input and volume.", "開啟音效版面，揀輸出、輸入同音量。",
            "Open", "開啟", "start ms-settings:sound", keywords: "sound,audio,volume,音效,音量"),
        
        Tweak.Cmd("w11p.settingslinks.powersleep", "Open Power & battery", "開啟電源同電池",
            "Open the Power & battery (Power & sleep) page for sleep, screen and battery options.", "開啟電源同電池（電源同睡眠）版面，設定睡眠、屏幕同電池選項。",
            "Open", "開啟", "start ms-settings:powersleep", keywords: "power,battery,sleep,電源,電池,睡眠"),
        
        Tweak.Cmd("w11p.settingslinks.storagesense", "Open Storage settings", "開啟儲存空間設定",
            "Open the Storage page with Storage Sense and disk usage.", "開啟儲存空間版面，有自動清理同磁碟用量。",
            "Open", "開啟", "start ms-settings:storagesense", keywords: "storage,disk,cleanup,儲存,磁碟"),
        
        Tweak.Cmd("w11p.settingslinks.crossdevice", "Open Nearby sharing", "開啟附近共用",
            "Open the Cross-device / Nearby sharing page to share files between nearby devices.", "開啟跨裝置／附近共用版面，喺附近裝置之間分享檔案。",
            "Open", "開啟", "start ms-settings:crossdevice", keywords: "nearby sharing,crossdevice,shared experiences,附近共用,跨裝置"),
        
        Tweak.Cmd("w11p.settingslinks.clipboard", "Open Clipboard settings", "開啟剪貼簿設定",
            "Open the Clipboard page for history and sync across devices.", "開啟剪貼簿版面，設定歷史記錄同跨裝置同步。",
            "Open", "開啟", "start ms-settings:clipboard", keywords: "clipboard,history,sync,剪貼簿,歷史"),
        
        Tweak.Cmd("w11p.settingslinks.multitasking", "Open Multitasking settings", "開啟多工處理設定",
            "Open the Multitasking page for Snap layouts and window behaviour.", "開啟多工處理版面，設定貼齊版面同視窗行為。",
            "Open", "開啟", "start ms-settings:multitasking", keywords: "multitasking,snap,windows,多工,貼齊"),
        
        Tweak.Cmd("w11p.settingslinks.developers", "Open For developers", "開啟開發人員設定",
            "Open the For developers page for Developer Mode and related options.", "開啟開發人員版面，開啟開發人員模式同相關選項。",
            "Open", "開啟", "start ms-settings:developers", keywords: "developers,developer mode,開發人員"),
        
        Tweak.Cmd("w11p.settingslinks.optionalfeatures", "Open Optional features", "開啟選用功能",
            "Open the Optional features page to add or remove Windows features.", "開啟選用功能版面，加裝或者移除 Windows 功能。",
            "Open", "開啟", "start ms-settings:optionalfeatures", keywords: "optional features,capabilities,選用功能"),
        
        Tweak.Cmd("w11p.settingslinks.windowsupdate", "Open Windows Update", "開啟 Windows Update",
            "Open the Windows Update page to check for and install updates.", "開啟 Windows Update 版面，檢查同安裝更新。",
            "Open", "開啟", "start ms-settings:windowsupdate", keywords: "windows update,patches,更新"),
        
        Tweak.Cmd("w11p.settingslinks.windowsdefender", "Open Windows Security", "開啟 Windows 安全性",
            "Open the Windows Security page for virus, firewall and device protection.", "開啟 Windows 安全性版面，管理病毒、防火牆同裝置保護。",
            "Open", "開啟", "start ms-settings:windowsdefender", keywords: "windows security,defender,antivirus,安全性,防毒"),
        
        Tweak.Cmd("w11p.settingslinks.backup", "Open Backup settings", "開啟備份設定",
            "Open the Windows Backup / Sync page to back up files and settings.", "開啟 Windows 備份／同步版面，備份檔案同設定。",
            "Open", "開啟", "start ms-settings:backup", keywords: "backup,sync,onedrive,備份,同步"),
        
        Tweak.Cmd("w11p.settingslinks.activation", "Open Activation settings", "開啟啟用設定",
            "Open the Activation page to check the Windows licence status.", "開啟啟用版面，查 Windows 授權狀態。",
            "Open", "開啟", "start ms-settings:activation", keywords: "activation,licence,product key,啟用,授權"),
        
        Tweak.Cmd("w11p.settingslinks.about", "Open About this PC", "開啟關於此電腦",
            "Open the About page with device name, specs and Windows version.", "開啟關於版面，睇裝置名稱、規格同 Windows 版本。",
            "Open", "開啟", "start ms-settings:about", keywords: "about,system info,specs,關於,系統資訊"),
        
        Tweak.Cmd("w11p.settingslinks.dateandtime", "Open Date & time", "開啟日期同時間",
            "Open the Date & time page for time zone and clock sync.", "開啟日期同時間版面，設定時區同時鐘同步。",
            "Open", "開啟", "start ms-settings:dateandtime", keywords: "date,time,timezone,日期,時間,時區"),
        
        Tweak.Cmd("w11p.settingslinks.regionlanguage", "Open Language settings", "開啟語言設定",
            "Open the Language & region page to add display languages.", "開啟語言同地區版面，加裝顯示語言。",
            "Open", "開啟", "start ms-settings:regionlanguage", keywords: "language,region,locale,語言,地區"),
        
        Tweak.Cmd("w11p.settingslinks.themes", "Open Themes settings", "開啟佈景主題設定",
            "Open the Themes page to switch wallpaper, colours and sounds.", "開啟佈景主題版面，換桌布、色彩同音效。",
            "Open", "開啟", "start ms-settings:themes", keywords: "themes,wallpaper,personalization,佈景主題,桌布"),
        
        Tweak.Cmd("w11p.settingslinks.taskbar", "Open Taskbar settings", "開啟工作列設定",
            "Open the Taskbar page to tweak alignment, icons and behaviour.", "開啟工作列版面，調整對齊、圖示同行為。",
            "Open", "開啟", "start ms-settings:taskbar", keywords: "taskbar,system tray,工作列,系統匣"),
    };
}
