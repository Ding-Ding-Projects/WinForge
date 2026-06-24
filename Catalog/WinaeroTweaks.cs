using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// Winaero-Tweaker 風格嘅進階調校（generate + 對抗式驗證）· Advanced Winaero-Tweaker-style tweaks,
/// generated then adversarially verified. Real registry keys only; all reversible.
/// </summary>
public static class WinaeroTweaks
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.RegToggle("winaero.appearance.colored-title-bars", "Colored title bars (accent on window title bars and borders)", "彩色標題列（視窗標題列同邊框上色）",
            "Shows the accent color on active window title bars and borders instead of plain white/black. Mirrors the Settings > Personalization > Colors 'Title bars and window borders' toggle.",
            "喺作用中視窗嘅標題列同邊框顯示強調色，唔再淨係黑白。等同設定 > 個人化 > 色彩入面嘅「標題列同視窗邊框」開關。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\DWM", "ColorPrevalence", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.None, keywords: "title bar color accent colored window border ColorPrevalence DWM 標題列 彩色 強調色 邊框"),

        Tweak.RegToggle("winaero.appearance.accent-start-taskbar", "Show accent color on Start, taskbar and action center", "喺開始選單、工作列同操作中心顯示強調色",
            "Applies the accent color to the Start menu, taskbar and notification (action) center surfaces. This is the Personalize-key ColorPrevalence, separate from the title-bar one.",
            "將強調色套用到開始選單、工作列同通知（操作）中心。呢個係 Personalize 鍵下嘅 ColorPrevalence，同標題列嗰個分開。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "ColorPrevalence", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.None, keywords: "accent start menu taskbar action center ColorPrevalence Personalize 開始 工作列 強調色 操作中心"),

        Tweak.RegToggle("winaero.appearance.inactive-title-bar-color", "Inactive title bar color (accent on background windows)", "非作用中標題列顏色（背景視窗用強調色）",
            "Sets a custom color for the title bars of inactive (background) windows when colored title bars are on. Value is a 0x00BBGGRR color; 0x808080 (8421504) is mid-gray. Disabling removes the value so Windows uses its default inactive shade.",
            "當開咗彩色標題列時，為非作用中（背景）視窗嘅標題列設定自訂顏色。數值係 0x00BBGGRR 色碼，0x808080（8421504）係中灰色。停用會刪除呢個值，等 Windows 用返自己嘅預設非作用中色調。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\DWM", "AccentColorInactive", 8421504, null,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.None, keywords: "inactive title bar color AccentColorInactive DWM background window 非作用中 標題列 顏色 背景視窗"),

        Tweak.Slider("winaero.appearance.menu-show-delay", "Menu show delay (snappier menus)", "選單顯示延遲（選單更快彈出）",
            "Milliseconds Windows waits before opening cascaded/submenus. Default 400; drag to 0 to make menus appear instantly. Stored as a string under Control Panel\\Desktop\\MenuShowDelay.",
            "Windows 喺彈出層疊／子選單之前等幾多毫秒。預設 400；拉到 0 令選單即時彈出。以字串儲存於 Control Panel\\Desktop\\MenuShowDelay。",
            0, 4000, 50,
            () => double.TryParse(RegistryHelper.GetValue(RegRoot.HKCU, @"Control Panel\Desktop", "MenuShowDelay")?.ToString(), out var v) ? v : 400,
            v => RegistryHelper.SetValue(RegRoot.HKCU, @"Control Panel\Desktop", "MenuShowDelay", ((int)v).ToString(), RegistryValueKind.String),
            unitEn: "ms", unitZh: "毫秒",
            requiresAdmin: false, restart: RestartScope.SignOut, keywords: "menu show delay MenuShowDelay snappy fast menus submenu 選單 延遲 速度 子選單"),

        Tweak.RegToggle("winaero.appearance.disable-aero-shake", "Disable window shake (Aero Shake) to minimize", "停用搖晃視窗（Aero Shake）最小化",
            "Stops Windows from minimizing all other windows when you grab and shake a window's title bar. Sets DisallowShaking=1; default behavior returns when the value is 0/removed.",
            "當你抓住搖晃某視窗標題列時，唔再最小化其他所有視窗。設定 DisallowShaking=1；值為 0／移除時恢復預設行為。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "DisallowShaking", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.Explorer, keywords: "aero shake disable window shake minimize DisallowShaking title bar 搖晃 視窗 最小化 停用"),

        Tweak.RegToggle("winaero.appearance.legacy-balloon-notifications", "Use classic balloon tips instead of toast notifications", "用傳統氣球提示取代浮動通知（toast）",
            "Brings back old XP/Win7-style balloon tip notifications instead of modern toast banners. Sets EnableLegacyBalloonNotifications=1 under the Explorer policy key; removing/0 restores toasts.",
            "恢復舊式 XP／Win7 氣球提示通知，取代現代浮動橫額（toast）。喺 Explorer 政策鍵下設定 EnableLegacyBalloonNotifications=1；移除／設 0 恢復浮動通知。",
            RegRoot.HKCU, @"Software\Policies\Microsoft\Windows\Explorer", "EnableLegacyBalloonNotifications", 1, null,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.SignOut, keywords: "balloon notifications toast EnableLegacyBalloonNotifications classic XP Explorer policy 氣球 提示 通知 浮動"),

        Tweak.Slider("winaero.appearance.scrollbar-width", "Scrollbar width (thicker scroll bars)", "捲動列闊度（較粗捲動列）",
            "Width of classic scroll bars in pixels. Stored as ScrollWidth = -15 x pixels; default 17 px (-255). Drag higher for chunkier bars. Range 8-48 px.",
            "傳統捲動列嘅闊度（像素）。以 ScrollWidth = -15 × 像素 儲存；預設 17 像素（-255）。拉高令捲動列更粗。範圍 8 至 48 像素。",
            8, 48, 1,
            () => int.TryParse(RegistryHelper.GetValue(RegRoot.HKCU, @"Control Panel\Desktop\WindowMetrics", "ScrollWidth")?.ToString(), out var v) && v < 0 ? -v / 15.0 : 17,
            px => RegistryHelper.SetValue(RegRoot.HKCU, @"Control Panel\Desktop\WindowMetrics", "ScrollWidth", (-15 * (int)px).ToString(), RegistryValueKind.String),
            unitEn: "px", unitZh: "像素",
            requiresAdmin: false, restart: RestartScope.SignOut, keywords: "scrollbar width ScrollWidth WindowMetrics thicker scroll bar 捲動列 闊度 粗"),

        Tweak.Slider("winaero.appearance.icon-spacing", "Desktop icon horizontal spacing", "桌面圖示水平間距",
            "Horizontal spacing between desktop icons in pixels. Stored as IconSpacing = -15 x pixels; default 75 px (-1125). Drag higher to spread icons out. Range 32-182 px.",
            "桌面圖示之間嘅水平間距（像素）。以 IconSpacing = -15 × 像素 儲存；預設 75 像素（-1125）。拉高令圖示分得更開。範圍 32 至 182 像素。",
            32, 182, 1,
            () => int.TryParse(RegistryHelper.GetValue(RegRoot.HKCU, @"Control Panel\Desktop\WindowMetrics", "IconSpacing")?.ToString(), out var v) && v < 0 ? -v / 15.0 : 75,
            px => RegistryHelper.SetValue(RegRoot.HKCU, @"Control Panel\Desktop\WindowMetrics", "IconSpacing", (-15 * (int)px).ToString(), RegistryValueKind.String),
            unitEn: "px", unitZh: "像素",
            requiresAdmin: false, restart: RestartScope.SignOut, keywords: "icon spacing IconSpacing WindowMetrics desktop horizontal 圖示 間距 桌面 水平"),

        Tweak.RegToggle("winaero.behavior.shutdown-event-tracker", "Disable Shutdown Event Tracker", "停用關機事件追蹤器",
            "Removes the 'Why did the computer shut down unexpectedly?' reason prompt shown on shutdown/restart (mostly on Server/domain PCs). Off deletes the policy value to restore default.",
            "唔再彈出關機/重啟時要你揀原因嘅提示（多數喺 Server 或域內電腦先有）。關閉即刪除呢個原則值，回復預設。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows NT\Reliability", "ShutdownReasonUI", 0, null,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.SignOut, keywords: "shutdown event tracker reason ui reliability why did the computer shut down policies"),

        Tweak.RegToggle("winaero.behavior.auto-end-tasks", "Auto-End Tasks on Shutdown", "關機時自動結束工作",
            "Stops the 'This app is preventing you from shutting down' screen by automatically force-closing open apps at shutdown/sign-out instead of waiting. On sets AutoEndTasks=1; off sets 0 (default behaviour).",
            "唔再出現「呢個應用程式阻止你關機」嘅畫面，關機/登出時自動強制關閉開住嘅程式而唔等。開啟即設 AutoEndTasks=1；關閉即設 0（預設行為）。",
            RegRoot.HKCU, @"Control Panel\Desktop", "AutoEndTasks", "1", "0",
            RegistryValueKind.String, requiresAdmin: false, restart: RestartScope.SignOut, keywords: "this app is preventing shutdown auto end tasks force close hung apps control panel desktop"),

        Tweak.RegToggle("winaero.behavior.hung-app-timeout", "Hung App Timeout (1s)", "程式無回應等候時間（1 秒）",
            "How long Windows waits before treating a non-responding app as 'hung'. Lowers from the 5000 ms default to 1000 ms so the End Task prompt appears faster. Off removes the value (back to default).",
            "Windows 等幾耐先當一個無回應嘅程式為「當機」。由預設 5000 毫秒改為 1000 毫秒，令「結束工作」提示更快彈出。關閉即刪除（回復預設）。",
            RegRoot.HKCU, @"Control Panel\Desktop", "HungAppTimeout", "1000", null,
            RegistryValueKind.String, requiresAdmin: false, restart: RestartScope.SignOut, keywords: "hungapptimeout hung app responding end task timeout milliseconds control panel desktop"),

        Tweak.RegToggle("winaero.behavior.wait-to-kill-app-timeout", "Wait To Kill App Timeout (2s)", "強制結束程式等候時間（2 秒）",
            "How long Windows gives open apps to save and close at shutdown before offering to kill them. Lowers from the 20000 ms default to 2000 ms for a faster shutdown. Off removes the value (back to default).",
            "關機時 Windows 畀幾耐時間讓程式儲存同關閉，先至提出強制結束。由預設 20000 毫秒改為 2000 毫秒，加快關機。關閉即刪除（回復預設）。",
            RegRoot.HKCU, @"Control Panel\Desktop", "WaitToKillAppTimeout", "2000", null,
            RegistryValueKind.String, requiresAdmin: false, restart: RestartScope.SignOut, keywords: "waittokillapptimeout shutdown speed kill app timeout milliseconds control panel desktop"),

        Tweak.Slider("winaero.behavior.wait-to-kill-service-timeout", "Wait To Kill Service Timeout", "強制結束服務等候時間",
            "How long Windows waits for background services to stop at shutdown before killing them. Default 5000 ms; drag lower for a faster shutdown. Stored as a string under SYSTEM\\CurrentControlSet\\Control\\WaitToKillServiceTimeout.",
            "關機時 Windows 等幾耐讓背景服務停止，先至強制結束。預設 5000 毫秒；拉低令關機更快。以字串儲存於 SYSTEM\\CurrentControlSet\\Control\\WaitToKillServiceTimeout。",
            0, 20000, 500,
            () => double.TryParse(RegistryHelper.GetValue(RegRoot.HKLM, @"SYSTEM\CurrentControlSet\Control", "WaitToKillServiceTimeout")?.ToString(), out var v) ? v : 5000,
            v => RegistryHelper.SetValue(RegRoot.HKLM, @"SYSTEM\CurrentControlSet\Control", "WaitToKillServiceTimeout", ((int)v).ToString(), RegistryValueKind.String),
            unitEn: "ms", unitZh: "毫秒",
            requiresAdmin: true, restart: RestartScope.Reboot, keywords: "waittokillservicetimeout services shutdown timeout milliseconds currentcontrolset control"),

        Tweak.RegToggle("winaero.behavior.disable-win-l-lock", "Disable Win+L Lock", "停用 Win+L 鎖定",
            "Disables locking the workstation via Win+L and removes the Lock option from Ctrl+Alt+Del and the Start menu. On sets DisableLockWorkstation=1. Off removes the value to restore locking.",
            "停用 Win+L 鎖定電腦，並從 Ctrl+Alt+Del 同開始選單移除「鎖定」選項。開啟即設 DisableLockWorkstation=1；關閉即刪除該值，回復鎖定功能。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableLockWorkstation", 1, null,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.SignOut, keywords: "win+l lock workstation disablelockworkstation policies system disable lock"),

        Tweak.RegToggle("winaero.boot-logon.no-lock-screen", "Disable lock screen", "停用上鎖畫面",
            "Skips the lock screen and goes straight to the sign-in password prompt.",
            "略過上鎖畫面，開機直接去到登入密碼嗰版。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Personalization", "NoLockScreen", 1, null,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.SignOut, keywords: "lock screen nolockscreen logon boot disable bypass 上鎖 鎖屏 登入"),

        Tweak.RegToggle("winaero.boot-logon.display-last-logon-info", "Show last interactive logon info", "顯示上次互動登入資訊",
            "After sign-in, shows the last successful logon and any failed logon attempts (local accounts).",
            "登入之後顯示上次成功登入同失敗嘅登入嘗試（限本機帳戶）。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "DisplayLastLogonInfo", 1, null,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.SignOut, keywords: "last logon info display failed attempts security welcome 登入 紀錄 資訊 安全"),

        Tweak.RegToggle("winaero.boot-logon.disable-cad", "Disable \"Press Ctrl+Alt+Del\" at logon", "停用登入「按 Ctrl+Alt+Del」",
            "Removes the secure attention sequence so you don't have to press Ctrl+Alt+Del before signing in.",
            "移除安全注意序列，登入前唔使再按 Ctrl+Alt+Del。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "DisableCAD", 1, null,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.SignOut, keywords: "ctrl alt del cad secure sign-in attention sequence logon 登入 安全"),

        Tweak.RegToggle("winaero.boot-logon.disable-startup-sound", "Disable Windows startup sound", "停用 Windows 開機音效",
            "Turns off the chime that plays when Windows starts up.",
            "關閉 Windows 開機時播放嘅提示音。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\BootAnimation", "DisableStartupSound", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.None, keywords: "startup sound chime boot audio disable mute 開機 音效 聲"),

        Tweak.RegToggle("winaero.boot-logon.disable-auto-restart-signon", "Disable automatic restart sign-on (ARSO)", "停用自動重啟登入 (ARSO)",
            "Stops Windows from auto-signing-in the last user and re-locking after a Windows Update restart.",
            "阻止 Windows Update 重啟後自動登入上一位用戶再鎖機。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "DisableAutomaticRestartSignOn", 1, null,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.None, keywords: "arso automatic restart sign-on auto login update reboot privacy 自動 重啟 登入"),

        Tweak.RegToggle("winaero.boot-logon.disable-acrylic-blur-logon", "Disable blur on sign-in screen", "停用登入畫面模糊效果",
            "Removes the acrylic blur from the sign-in screen background, showing a sharp image.",
            "移除登入畫面背景嘅亞克力模糊效果，顯示清晰背景圖。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows\System", "DisableAcrylicBackgroundOnLogon", 1, null,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.SignOut, keywords: "acrylic blur sign-in login screen background sharp disable 模糊 登入 背景"),

        Tweak.RegToggle("winaero.boot-logon.disable-logon-background-image", "Disable sign-in screen background image", "停用登入畫面背景圖",
            "Replaces the sign-in screen background picture with the plain accent colour.",
            "將登入畫面嘅背景相換成純色強調色背景。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows\System", "DisableLogonBackgroundImage", 1, null,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.SignOut, keywords: "logon background image sign-in screen accent solid color disable 登入 背景 圖片"),

        Tweak.RegToggle("winaero.boot-logon.numlock-at-startup", "Enable Num Lock at startup", "開機時開啟 Num Lock",
            "Turns Num Lock on automatically at the sign-in screen before anyone logs in.",
            "喺登入畫面、任何人登入之前自動開啟 Num Lock。",
            RegRoot.HKU, @".DEFAULT\Control Panel\Keyboard", "InitialKeyboardIndicators", "2147483650", "2147483648",
            RegistryValueKind.String, requiresAdmin: true, restart: RestartScope.SignOut, keywords: "numlock num lock startup sign-in keyboard indicators default 開機 鍵盤"),

        Tweak.RegToggle("winaero.desktop-explorer.launch-to-this-pc", "Open Explorer to This PC", "檔案總管開啟「本機」",
            "Make File Explorer open to the classic This PC view instead of Home/Quick access.",
            "File Explorer 開啟時顯示傳統「本機」，而唔係 Home／快速存取。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "LaunchTo", 1, 2,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.Explorer, keywords: "launchto this pc home quick access explorer default folder"),

        Tweak.RegToggle("winaero.desktop-explorer.show-seconds-clock", "Show seconds in taskbar clock", "工作列時鐘顯示秒數",
            "Display seconds in the system tray clock (Windows 11 22621.1928+).",
            "喺系統匣時鐘顯示秒數（Windows 11 22621.1928 或以上）。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSecondsInSystemClock", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.Explorer, keywords: "showsecondsinsystemclock seconds taskbar clock system tray time"),

        Tweak.RegToggle("winaero.desktop-explorer.taskbar-end-task", "End task on taskbar right-click", "工作列右鍵加入「結束工作」",
            "Add an End task command to the taskbar right-click menu of running apps.",
            "喺執行中程式嘅工作列右鍵選單加入「結束工作」指令。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings", "TaskbarEndTask", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.Explorer, keywords: "taskbarendtask end task kill app taskbar context menu developer settings"),

        Tweak.RegToggle("winaero.desktop-explorer.disable-thumbnail-cache", "Disable thumbnail cache", "停用縮圖快取",
            "Stop Explorer from caching thumbnails in hidden thumbs.db / central cache files.",
            "唔再將縮圖儲存喺隱藏 thumbs.db／中央快取檔案。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "DisableThumbnailCache", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.Explorer, keywords: "thumbnail cache thumbs.db disablethumbnailcache disable thumbs explorer"),

        Tweak.RegToggle("winaero.desktop-explorer.expand-to-current-folder", "Expand to current folder", "導覽窗格展開至目前資料夾",
            "Auto-expand the navigation pane to the folder currently open in Explorer.",
            "導覽窗格自動展開到 Explorer 目前開啟嘅資料夾。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "NavPaneExpandToCurrentFolder", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.Explorer, keywords: "navpaneexpandtocurrentfolder navigation pane expand current folder tree sync"),

        Tweak.RegToggle("winaero.desktop-explorer.drive-letters-first", "Show drive letters first", "磁碟機代號顯示喺前",
            "Display drive letters before the drive label in This PC (e.g. (C:) Local Disk).",
            "喺「本機」將磁碟機代號顯示喺名稱之前（例如 (C:) 本機磁碟）。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "ShowDriveLettersFirst", 4, 0,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.Explorer, keywords: "showdrivelettersfirst drive letter before label this pc explorer"),

        Tweak.RegToggle("winaero.desktop-explorer.checkboxes-select", "Checkboxes for item selection", "用核取方塊選取項目",
            "Show selection checkboxes on files and folders in Explorer.",
            "喺 Explorer 嘅檔案同資料夾顯示選取核取方塊。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "AutoCheckSelect", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.Explorer, keywords: "autocheckselect checkboxes item check boxes selection explorer"),

        Tweak.RegToggle("winaero.desktop-explorer.compact-mode", "Use compact view (reduce spacing)", "使用精簡檢視（減少間距）",
            "Reduce row spacing in File Explorer for a denser, classic-style layout.",
            "減少 File Explorer 列間距，做出更密集嘅傳統排版。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "UseCompactMode", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.Explorer, keywords: "usecompactmode compact view spacing density explorer rows classic"),

        Tweak.RegToggle("winaero.desktop-explorer.show-status-bar", "Show Explorer status bar", "顯示檔案總管狀態列",
            "Show the status bar at the bottom of File Explorer windows.",
            "喺 File Explorer 視窗底部顯示狀態列。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowStatusBar", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.Explorer, keywords: "showstatusbar status bar bottom explorer item count"),

        Tweak.RegToggle("winaero.desktop-explorer.no-recent-files-explorer", "Hide recent files in Quick access", "唔顯示快速存取最近檔案",
            "Stop showing recently used files in Explorer Home / Quick access.",
            "唔再喺 Explorer Home／快速存取顯示最近用過嘅檔案。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowRecent", 0, 1,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.Explorer, keywords: "showrecent recent files quick access home explorer privacy mru"),

        Tweak.RegToggle("winaero.context-menu.remove-give-access-share", "Remove \"Give access to\" / Share from context menu", "context menu 移除「授權存取 / 共用」",
            "Hides the legacy \"Give access to\" (network sharing) submenu from the right-click context menu by blocking its shell extension. Disable to restore it.",
            "封鎖個 shell extension，喺右掣 menu 度收埋舊式「授權存取 / 共用」嘅 submenu。撳返 disable 就會還原。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", "{f81e9010-6ea4-11ce-a7ff-00aa003ca9f6}", "", null,
            RegistryValueKind.String, requiresAdmin: false, restart: RestartScope.SignOut, keywords: "give access to share sharing context menu network blocked shell extension"),

        Tweak.RegToggle("winaero.context-menu.remove-cast-to-device", "Remove \"Cast to Device\" from context menu", "context menu 移除「投放到裝置」",
            "Hides the \"Cast to Device\" (Play To) entry from the right-click menu of media files by blocking its shell extension. Disable to restore it.",
            "封鎖個 shell extension，喺媒體檔嘅右掣 menu 度收埋「投放到裝置」(Play To)。撳返 disable 就會還原。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", "{7AD84985-87B4-4a16-BE58-8B72A5B390F7}", "", null,
            RegistryValueKind.String, requiresAdmin: false, restart: RestartScope.SignOut, keywords: "cast to device play to media context menu blocked shell extension dlna"),

        Tweak.RegToggle("winaero.context-menu.remove-edit-with-photos", "Remove \"Edit with Photos\" from context menu", "context menu 移除「用相片編輯」",
            "Hides the \"Edit with Photos\" entry from the right-click menu of image files by blocking the Photos app shell extension. Disable to restore it.",
            "封鎖相片 App 個 shell extension，喺相片檔嘅右掣 menu 度收埋「用相片編輯」。撳返 disable 就會還原。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", "{BFE0E2A4-C70C-4AD7-AC3D-10D1ECEBB5B4}", "", null,
            RegistryValueKind.String, requiresAdmin: false, restart: RestartScope.SignOut, keywords: "edit with photos image picture context menu blocked shell extension microsoft photos"),

        Tweak.RegToggle("winaero.context-menu.remove-scan-with-defender", "Remove \"Scan with Microsoft Defender\" from context menu", "context menu 移除「用 Microsoft Defender 掃描」",
            "Hides the \"Scan with Microsoft Defender\" entry from the right-click menu by blocking its EPP shell extension. Disable to restore it.",
            "封鎖 EPP 個 shell extension，喺右掣 menu 度收埋「用 Microsoft Defender 掃描」。撳返 disable 就會還原。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", "{09A47860-11B0-4DA5-AFA5-26D86198A780}", "", null,
            RegistryValueKind.String, requiresAdmin: false, restart: RestartScope.SignOut, keywords: "scan with microsoft defender antivirus epp context menu blocked shell extension"),

        Tweak.RegToggle("winaero.context-menu.remove-restore-previous-versions", "Remove \"Restore previous versions\" from context menu", "context menu 移除「還原舊版本」",
            "Hides the \"Restore previous versions\" context menu entry and the Previous Versions properties tab by blocking its shell extension. Disable to restore it.",
            "封鎖個 shell extension，收埋「還原舊版本」嘅右掣項目同埋內容頁嘅「之前版本」分頁。撳返 disable 就會還原。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", "{596AB062-B4D2-4215-9F74-E9109B0A8153}", "", null,
            RegistryValueKind.String, requiresAdmin: false, restart: RestartScope.SignOut, keywords: "restore previous versions shadow copy context menu properties tab blocked shell extension"),

        Tweak.RegToggle("winaero.privacy-network.disable-telemetry-autologger", "Disable telemetry autologger (DiagTrack)", "停用遙測自動記錄器 (DiagTrack)",
            "Sets the AutoLogger-Diagtrack-Listener ETW session Start to 0 so the DiagTrack telemetry trace does not run at boot.",
            "將 AutoLogger-Diagtrack-Listener 嘅 Start 設為 0，令 DiagTrack 遙測追蹤開機時唔運行。",
            RegRoot.HKLM, @"SYSTEM\CurrentControlSet\Control\WMI\AutoLogger\AutoLogger-Diagtrack-Listener", "Start", 0, 1,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.Reboot, keywords: "telemetry autologger diagtrack etw trace diagnostic boot"),

        Tweak.RegToggle("winaero.privacy-network.disable-feedback-notifications", "Disable feedback notifications", "停用意見反映通知",
            "Applies the DoNotShowFeedbackNotifications policy so Windows never prompts you for feedback.",
            "套用 DoNotShowFeedbackNotifications 政策，令 Windows 唔會再彈出意見反映要求。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1, null,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.SignOut, keywords: "feedback notification siuf survey prompt diagnostic policy"),

        Tweak.RegToggle("winaero.privacy-network.disable-app-launch-tracking", "Disable app launch tracking", "停用程式啟動追蹤",
            "Turns off Start_TrackProgs so Windows stops tracking which apps you launch to improve Start and search results.",
            "關閉 Start_TrackProgs，令 Windows 唔再追蹤你開過邊啲程式去改善開始選單同搜尋結果。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", 0, 1,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.Explorer, keywords: "app launch tracking start trackprogs most used apps privacy"),

        Tweak.RegToggle("winaero.privacy-network.disable-typing-insights", "Disable typing insights", "停用輸入分析",
            "Sets InsightsEnabled to 0 so Windows stops collecting typing insights from your text suggestions and autocorrect.",
            "將 InsightsEnabled 設為 0，令 Windows 唔再收集你打字嘅文字建議同自動更正分析。",
            RegRoot.HKCU, @"Software\Microsoft\Input\Settings", "InsightsEnabled", 0, 1,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.None, keywords: "typing insights input personalization autocorrect suggestions privacy"),

        Tweak.RegToggle("winaero.privacy-network.deny-apps-location", "Deny apps access to location (policy)", "拒絕 App 存取位置 (政策)",
            "Applies the LetAppsAccessLocation policy set to Force Deny so no Windows apps can access your location.",
            "套用 LetAppsAccessLocation 政策設為強制拒絕，令所有 Windows App 都唔可以存取你嘅位置。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsAccessLocation", 2, null,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.SignOut, keywords: "location apps access privacy letappsaccesslocation policy deny gps"),

        Tweak.RegToggle("winaero.privacy-network.delivery-optimization-no-peering", "Delivery Optimization: no download from other PCs", "傳遞最佳化：唔由其他 PC 下載",
            "Sets DODownloadMode to 0 (HTTP only, no peering) so updates download only from Microsoft, never from or to other PCs.",
            "將 DODownloadMode 設為 0（只用 HTTP、唔做點對點），更新淨係由 Microsoft 下載，唔同其他 PC 互傳。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0, null,
            RegistryValueKind.DWord, requiresAdmin: true, restart: RestartScope.None, keywords: "delivery optimization download mode peering p2p other pcs bandwidth network"),

        Tweak.RegToggle("winaero.privacy-network.disable-tailored-experiences", "Disable tailored experiences with diagnostic data", "停用以診斷資料提供的量身體驗",
            "Sets TailoredExperiencesWithDiagnosticDataEnabled to 0 so Windows does not use your diagnostic data for personalized tips and ads.",
            "將 TailoredExperiencesWithDiagnosticDataEnabled 設為 0，令 Windows 唔用你嘅診斷資料嚟做個人化提示同廣告。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, 1,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.None, keywords: "tailored experiences diagnostic data personalized tips ads privacy"),

        Tweak.RegToggle("winaero.privacy-network.disable-feedback-frequency", "Disable feedback request frequency", "停用意見反映要求頻率",
            "Sets NumberOfSIUFInPeriod to 0 so Windows never asks for feedback through the SIUF survey system.",
            "將 NumberOfSIUFInPeriod 設為 0，令 Windows 唔會再透過 SIUF 問卷系統要求意見反映。",
            RegRoot.HKCU, @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0, 1,
            RegistryValueKind.DWord, requiresAdmin: false, restart: RestartScope.None, keywords: "feedback frequency siuf survey period notifications privacy"),

    };
}
