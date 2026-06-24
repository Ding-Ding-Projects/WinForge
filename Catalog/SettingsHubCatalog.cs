using System.Collections.Generic;
using System.Linq;

namespace WinForge.Catalog;

/// <summary>
/// 設定／控制台啟動器嘅一個項目 · One launchable entry in the Settings &amp; Control Panel hub.
/// 每個項目都有雙語標題、關鍵字同一句真實嘅啟動指令。
/// Each entry carries a bilingual label, keywords and a real launch command.
/// </summary>
public sealed class SettingsHubEntry
{
    /// <summary>英文標題 · English label.</summary>
    public string En { get; init; } = "";

    /// <summary>粵語標題 · Cantonese label.</summary>
    public string Zh { get; init; } = "";

    /// <summary>分類（用嚟分組同上色）· Kind, used for grouping &amp; the chip colour.</summary>
    public SettingsHubKind Kind { get; init; }

    /// <summary>
    /// 真實啟動目標 · The real launch target.
    /// ms-settings: → a URI; ControlApplet → a canonical name for <c>control /name …</c>;
    /// Cpl → a <c>*.cpl</c> file name run via <c>control …</c>.
    /// </summary>
    public string Target { get; init; } = "";

    /// <summary>額外搜尋關鍵字（英／粵／別名）· Extra search keywords (EN / 粵 / aliases).</summary>
    public string Keywords { get; init; } = "";

    /// <summary>畀使用者睇嘅技術指令文字 · The technical command string shown to the user.</summary>
    public string CommandText => Kind switch
    {
        SettingsHubKind.Settings => Target,
        SettingsHubKind.ControlApplet => $"control /name {Target}",
        SettingsHubKind.Cpl => $"control {Target}",
        _ => Target,
    };

    public string Haystack => $"{En} {Zh} {Keywords} {Target} {CommandText}".ToLowerInvariant();
}

/// <summary>項目種類 · The kind of launch target.</summary>
public enum SettingsHubKind
{
    /// <summary>現代「設定」頁（ms-settings:）· A modern Settings page (ms-settings:).</summary>
    Settings,

    /// <summary>傳統控制台 applet（control /name CanonicalName）· A classic Control Panel applet.</summary>
    ControlApplet,

    /// <summary>控制台 *.cpl 檔（control name.cpl）· A Control Panel *.cpl file.</summary>
    Cpl,
}

/// <summary>
/// 設定與控制台總匯 · The curated catalog for the Settings &amp; Control Panel hub.
/// 唯一允許嘅「啟動器」式集線器：列出每個常用 ms-settings: 頁面同每個控制台 applet，
/// 用 Process.Start 直接打開（呢啲嘢冇 app 內等價物）。
/// The one allowed launcher-style hub: every common ms-settings: page and every Control Panel
/// applet, opened directly via Process.Start (there is no in-app replacement for each applet).
/// </summary>
public static class SettingsHubCatalog
{
    // ── ms-settings: pages · 現代「設定」頁 ───────────────────────────────────────
    private static readonly SettingsHubEntry[] SettingsPages =
    {
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:", En = "Settings (home)", Zh = "設定（主頁）", Keywords = "settings home main 設定 主頁" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:system", En = "System", Zh = "系統", Keywords = "system 系統" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:display", En = "Display", Zh = "顯示", Keywords = "display screen resolution scaling monitor 顯示 螢幕 解析度 縮放" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:nightlight", En = "Night light", Zh = "夜燈", Keywords = "night light blue 夜燈 藍光" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:display-advanced", En = "Advanced display (refresh rate)", Zh = "進階顯示（更新率）", Keywords = "refresh rate hz advanced display 更新率 進階顯示" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:display-advancedgraphics", En = "Graphics (GPU preference)", Zh = "圖形（GPU 偏好）", Keywords = "graphics gpu preference 圖形 顯示卡" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:sound", En = "Sound", Zh = "音效", Keywords = "sound audio output input volume 音效 聲音 音量" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:apps-volume", En = "Volume mixer (app sound)", Zh = "音量混合器（程式音效）", Keywords = "volume mixer app sound 音量 混合器" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:notifications", En = "Notifications", Zh = "通知", Keywords = "notifications focus 通知 專注" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:quiethours", En = "Focus / Do not disturb", Zh = "專注／勿擾", Keywords = "focus assist do not disturb quiet hours 專注 勿擾" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:powersleep", En = "Power & sleep", Zh = "電源與睡眠", Keywords = "power sleep battery 電源 睡眠 電池" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:batterysaver", En = "Battery saver", Zh = "省電模式", Keywords = "battery saver power 省電 電池" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:storagesense", En = "Storage", Zh = "儲存體", Keywords = "storage sense disk cleanup space 儲存 磁碟 清理" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:multitasking", En = "Multitasking", Zh = "多工", Keywords = "multitasking snap alt-tab 多工 貼齊" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:project", En = "Projecting to this PC", Zh = "投影到此電腦", Keywords = "project miracast wireless display 投影 無線顯示" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:clipboard", En = "Clipboard", Zh = "剪貼簿", Keywords = "clipboard history sync 剪貼簿 歷史" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:remotedesktop", En = "Remote Desktop", Zh = "遠端桌面", Keywords = "remote desktop rdp 遠端 桌面" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:about", En = "About this PC", Zh = "關於此電腦", Keywords = "about device specs system info 關於 規格 系統資訊" },

        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:bluetooth", En = "Bluetooth & devices", Zh = "藍牙與裝置", Keywords = "bluetooth devices pair 藍牙 裝置 配對" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:connecteddevices", En = "Devices", Zh = "裝置", Keywords = "devices connected 裝置" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:printers", En = "Printers & scanners", Zh = "印表機與掃描器", Keywords = "printers scanners print 印表機 掃描器 列印" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:mousetouchpad", En = "Mouse", Zh = "滑鼠", Keywords = "mouse pointer 滑鼠 指標" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:devices-touchpad", En = "Touchpad", Zh = "觸控板", Keywords = "touchpad gestures 觸控板 手勢" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:typing", En = "Typing", Zh = "輸入", Keywords = "typing autocorrect suggestions 輸入 自動更正" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:pen", En = "Pen & Windows Ink", Zh = "手寫筆與 Windows Ink", Keywords = "pen ink stylus 手寫筆" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:autoplay", En = "AutoPlay", Zh = "自動播放", Keywords = "autoplay removable media 自動播放" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:usb", En = "USB", Zh = "USB", Keywords = "usb connection 連線" },

        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:network", En = "Network & internet", Zh = "網絡與互聯網", Keywords = "network internet 網絡 互聯網" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:network-status", En = "Network status", Zh = "網絡狀態", Keywords = "network status 網絡 狀態" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:network-wifi", En = "Wi-Fi", Zh = "Wi-Fi", Keywords = "wifi wireless 無線 網絡" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:network-ethernet", En = "Ethernet", Zh = "乙太網路", Keywords = "ethernet lan wired 乙太 有線" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:network-vpn", En = "VPN", Zh = "VPN", Keywords = "vpn 虛擬私人網絡" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:network-mobilehotspot", En = "Mobile hotspot", Zh = "行動熱點", Keywords = "hotspot tethering 熱點 分享" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:network-airplanemode", En = "Airplane mode", Zh = "飛航模式", Keywords = "airplane flight mode 飛航 飛行" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:network-proxy", En = "Proxy", Zh = "Proxy 代理", Keywords = "proxy 代理 伺服器" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:network-dialup", En = "Dial-up", Zh = "撥號", Keywords = "dialup 撥號" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:network-advancedsettings", En = "Advanced network settings", Zh = "進階網絡設定", Keywords = "advanced network adapter 進階 網絡 介面卡" },

        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:personalization", En = "Personalization", Zh = "個人化", Keywords = "personalization theme 個人化 主題" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:personalization-background", En = "Background (wallpaper)", Zh = "背景（桌布）", Keywords = "background wallpaper desktop 背景 桌布" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:personalization-colors", En = "Colors (accent / dark mode)", Zh = "色彩（強調色／深色模式）", Keywords = "colors accent dark light mode 色彩 強調色 深色 淺色" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:themes", En = "Themes", Zh = "主題", Keywords = "themes 主題" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:lockscreen", En = "Lock screen", Zh = "鎖定畫面", Keywords = "lock screen 鎖定 畫面" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:personalization-start", En = "Start", Zh = "開始功能表", Keywords = "start menu 開始 功能表" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:taskbar", En = "Taskbar", Zh = "工作列", Keywords = "taskbar tray 工作列 系統匣" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:fonts", En = "Fonts", Zh = "字型", Keywords = "fonts typeface 字型 字體" },

        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:appsfeatures", En = "Installed apps", Zh = "已安裝的應用程式", Keywords = "apps features installed uninstall 應用程式 解除安裝" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:optionalfeatures", En = "Optional features", Zh = "選用功能", Keywords = "optional features add 選用 功能" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:defaultapps", En = "Default apps", Zh = "預設應用程式", Keywords = "default apps browser 預設 瀏覽器" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:startupapps", En = "Startup apps", Zh = "啟動應用程式", Keywords = "startup boot logon 啟動 開機" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:maps", En = "Offline maps", Zh = "離線地圖", Keywords = "maps offline 地圖 離線" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:appsforwebsites", En = "Apps for websites", Zh = "網站適用的應用程式", Keywords = "apps websites 網站 應用程式" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:videoplayback", En = "Video playback", Zh = "影片播放", Keywords = "video playback hdr 影片 播放" },

        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:yourinfo", En = "Your info (account)", Zh = "您的資訊（帳戶）", Keywords = "account info microsoft 帳戶 資訊" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:emailandaccounts", En = "Email & accounts", Zh = "電子郵件與帳戶", Keywords = "email accounts 郵件 帳戶" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:signinoptions", En = "Sign-in options", Zh = "登入選項", Keywords = "sign-in pin password hello 登入 密碼" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:windowshello", En = "Windows Hello (face/finger)", Zh = "Windows Hello（臉部／指紋）", Keywords = "hello face fingerprint biometric 臉部 指紋 生物辨識" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:otherusers", En = "Other users", Zh = "其他使用者", Keywords = "users family accounts 使用者 家庭" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:workplace", En = "Access work or school", Zh = "存取公司或學校資源", Keywords = "work school azure ad 公司 學校" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:sync", En = "Windows backup / sync", Zh = "Windows 備份／同步", Keywords = "sync backup settings 備份 同步" },

        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:dateandtime", En = "Date & time", Zh = "日期與時間", Keywords = "date time clock timezone 日期 時間 時區" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:regionlanguage", En = "Language & region", Zh = "語言與地區", Keywords = "language region locale 語言 地區" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:regionformatting", En = "Region (formats)", Zh = "地區（格式）", Keywords = "region format 地區 格式" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:keyboard", En = "Typing / keyboard (region)", Zh = "輸入／鍵盤（地區）", Keywords = "keyboard input language 鍵盤 輸入法" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:speech", En = "Speech", Zh = "語音", Keywords = "speech voice recognition 語音 辨識" },

        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:gaming-gamebar", En = "Game Bar", Zh = "遊戲列", Keywords = "game bar overlay 遊戲列" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:gaming-gamemode", En = "Game Mode", Zh = "遊戲模式", Keywords = "game mode 遊戲模式" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:gaming-gamedvr", En = "Captures (Game DVR)", Zh = "擷取（Game DVR）", Keywords = "captures game dvr record 擷取 錄影" },

        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:easeofaccess", En = "Accessibility", Zh = "協助工具", Keywords = "accessibility ease of access 協助工具 無障礙" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:easeofaccess-display", En = "Text size", Zh = "文字大小", Keywords = "text size scaling accessibility 文字 大小" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:easeofaccess-magnifier", En = "Magnifier", Zh = "放大鏡", Keywords = "magnifier zoom 放大鏡" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:easeofaccess-highcontrast", En = "Contrast themes", Zh = "對比佈景主題", Keywords = "high contrast 高對比" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:easeofaccess-narrator", En = "Narrator", Zh = "朗讀程式", Keywords = "narrator screen reader 朗讀 螢幕閱讀" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:easeofaccess-mouse", En = "Mouse pointer & touch", Zh = "滑鼠指標與觸控", Keywords = "mouse pointer accessibility 滑鼠 指標" },

        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:privacy", En = "Privacy & security", Zh = "隱私權與安全性", Keywords = "privacy security 隱私 安全" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:privacy-general", En = "Privacy: general (ad ID)", Zh = "隱私：一般（廣告 ID）", Keywords = "privacy advertising id general 廣告 ID 隱私" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:privacy-location", En = "Location", Zh = "位置", Keywords = "location gps privacy 位置 定位" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:privacy-webcam", En = "Camera", Zh = "相機", Keywords = "camera webcam privacy 相機 攝影機" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:privacy-microphone", En = "Microphone", Zh = "麥克風", Keywords = "microphone privacy 麥克風" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:privacy-feedback", En = "Diagnostics & feedback", Zh = "診斷與意見反應", Keywords = "diagnostics telemetry feedback 診斷 遙測" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:privacy-activityhistory", En = "Activity history", Zh = "活動記錄", Keywords = "activity history timeline 活動 記錄" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:privacy-appdiagnostics", En = "App diagnostics", Zh = "應用程式診斷", Keywords = "app diagnostics privacy 應用程式 診斷" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:windowsdefender", En = "Windows Security", Zh = "Windows 安全性", Keywords = "windows security defender antivirus 防毒 安全" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:findmydevice", En = "Find my device", Zh = "尋找我的裝置", Keywords = "find my device 尋找 裝置" },

        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:windowsupdate", En = "Windows Update", Zh = "Windows Update", Keywords = "update windows update 更新" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:windowsupdate-history", En = "Update history", Zh = "更新記錄", Keywords = "update history 更新 記錄" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:windowsupdate-options", En = "Update advanced options", Zh = "更新進階選項", Keywords = "update advanced options 更新 進階" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:windowsupdate-restartoptions", En = "Update active hours", Zh = "更新使用時段", Keywords = "update active hours restart 更新 重新啟動 使用時段" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:recovery", En = "Recovery (reset this PC)", Zh = "復原（重設此電腦）", Keywords = "recovery reset reinstall 復原 重設 還原" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:troubleshoot", En = "Troubleshoot", Zh = "疑難排解", Keywords = "troubleshoot fix 疑難排解 修復" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:activation", En = "Activation", Zh = "啟用", Keywords = "activation license key 啟用 授權 金鑰" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:developers", En = "For developers", Zh = "供開發人員使用", Keywords = "developers developer mode sudo 開發人員 開發者模式" },
        new() { Kind = SettingsHubKind.Settings, Target = "ms-settings:crossdevice", En = "Phone Link / cross-device", Zh = "手機連結／跨裝置", Keywords = "phone link cross device 手機 連結 跨裝置" },
    };

    // ── Control Panel applets · 傳統控制台 applet（control /name CanonicalName）─────
    private static readonly SettingsHubEntry[] ControlApplets =
    {
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.AdministrativeTools", En = "Administrative Tools", Zh = "系統管理工具", Keywords = "administrative tools 系統管理 工具" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.AutoPlay", En = "AutoPlay", Zh = "自動播放", Keywords = "autoplay 自動播放" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.BackupAndRestore", En = "Backup and Restore (Win7)", Zh = "備份與還原（Windows 7）", Keywords = "backup restore 備份 還原" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.BitLockerDriveEncryption", En = "BitLocker Drive Encryption", Zh = "BitLocker 磁碟機加密", Keywords = "bitlocker encryption 加密 磁碟" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.ColorManagement", En = "Color Management", Zh = "色彩管理", Keywords = "color management icc profile 色彩 管理" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.CredentialManager", En = "Credential Manager", Zh = "認證管理員", Keywords = "credential manager passwords vault 認證 密碼" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.DateAndTime", En = "Date and Time", Zh = "日期和時間", Keywords = "date time clock 日期 時間" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.DefaultPrograms", En = "Default Programs", Zh = "預設程式", Keywords = "default programs associations 預設 程式" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.DeviceManager", En = "Device Manager", Zh = "裝置管理員", Keywords = "device manager drivers hardware 裝置 驅動程式" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.DevicesAndPrinters", En = "Devices and Printers", Zh = "裝置和印表機", Keywords = "devices printers 裝置 印表機" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.EaseOfAccessCenter", En = "Ease of Access Center", Zh = "輕鬆存取中心", Keywords = "ease of access accessibility 輕鬆存取 協助工具" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.FileExplorerOptions", En = "File Explorer Options", Zh = "檔案總管選項", Keywords = "folder options file explorer 資料夾 選項 檔案總管" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.FileHistory", En = "File History", Zh = "檔案歷程記錄", Keywords = "file history backup 檔案 歷程 備份" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.Fonts", En = "Fonts", Zh = "字型", Keywords = "fonts 字型" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.IndexingOptions", En = "Indexing Options", Zh = "索引選項", Keywords = "indexing search options 索引 搜尋" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.InternetOptions", En = "Internet Options", Zh = "網際網路選項", Keywords = "internet options inetcpl proxy 網際網路 選項" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.Keyboard", En = "Keyboard", Zh = "鍵盤", Keywords = "keyboard repeat 鍵盤" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.Mouse", En = "Mouse", Zh = "滑鼠", Keywords = "mouse buttons pointer 滑鼠 指標" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.NetworkAndSharingCenter", En = "Network and Sharing Center", Zh = "網路和共用中心", Keywords = "network sharing center adapters 網路 共用 介面卡" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.PenAndTouch", En = "Pen and Touch", Zh = "手寫筆與觸控", Keywords = "pen touch 手寫筆 觸控" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.PhoneAndModem", En = "Phone and Modem", Zh = "電話和數據機", Keywords = "phone modem 電話 數據機" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.PowerOptions", En = "Power Options", Zh = "電源選項", Keywords = "power options plans 電源 計劃" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.ProgramsAndFeatures", En = "Programs and Features", Zh = "程式和功能", Keywords = "programs features uninstall appwiz 程式 功能 解除安裝" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.RegionAndLanguage", En = "Region", Zh = "地區", Keywords = "region locale format 地區 格式" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.RemoteAppAndDesktopConnections", En = "RemoteApp and Desktop Connections", Zh = "RemoteApp 和桌面連線", Keywords = "remoteapp desktop connections 遠端 連線" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.Sound", En = "Sound", Zh = "聲音", Keywords = "sound audio playback recording 聲音 音效" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.SpeechRecognition", En = "Speech Recognition", Zh = "語音辨識", Keywords = "speech recognition 語音 辨識" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.StorageSpaces", En = "Storage Spaces", Zh = "儲存空間", Keywords = "storage spaces pool 儲存 空間" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.System", En = "System", Zh = "系統", Keywords = "system about specs 系統 規格" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.Taskbar", En = "Taskbar and Navigation", Zh = "工作列和導覽", Keywords = "taskbar 工作列" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.Troubleshooting", En = "Troubleshooting", Zh = "疑難排解", Keywords = "troubleshooting 疑難排解" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.UserAccounts", En = "User Accounts", Zh = "使用者帳戶", Keywords = "user accounts 使用者 帳戶" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.WindowsDefender", En = "Windows Security", Zh = "Windows 安全性", Keywords = "defender security antivirus 防毒 安全" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.WindowsFirewall", En = "Windows Defender Firewall", Zh = "Windows Defender 防火牆", Keywords = "firewall 防火牆" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.WindowsMobilityCenter", En = "Windows Mobility Center", Zh = "Windows 行動中心", Keywords = "mobility center laptop 行動中心" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.WorkFolders", En = "Work Folders", Zh = "工作資料夾", Keywords = "work folders 工作 資料夾" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.RecoveryDrive", En = "Recovery", Zh = "復原", Keywords = "recovery drive restore 復原 還原" },
        new() { Kind = SettingsHubKind.ControlApplet, Target = "Microsoft.AutoComplete", En = "Web Browser AutoComplete", Zh = "網頁瀏覽器自動完成", Keywords = "autocomplete 自動完成" },
    };

    // ── *.cpl files · 控制台 *.cpl 檔（control name.cpl）──────────────────────────
    private static readonly SettingsHubEntry[] CplFiles =
    {
        new() { Kind = SettingsHubKind.Cpl, Target = "appwiz.cpl", En = "Programs and Features", Zh = "程式和功能", Keywords = "appwiz uninstall programs 解除安裝 程式" },
        new() { Kind = SettingsHubKind.Cpl, Target = "desk.cpl", En = "Display Settings", Zh = "顯示設定", Keywords = "desk display screen 顯示 螢幕" },
        new() { Kind = SettingsHubKind.Cpl, Target = "firewall.cpl", En = "Windows Firewall", Zh = "Windows 防火牆", Keywords = "firewall 防火牆" },
        new() { Kind = SettingsHubKind.Cpl, Target = "hdwwiz.cpl", En = "Device Manager", Zh = "裝置管理員", Keywords = "hdwwiz device manager 裝置 管理員" },
        new() { Kind = SettingsHubKind.Cpl, Target = "inetcpl.cpl", En = "Internet Options", Zh = "網際網路選項", Keywords = "inetcpl internet options proxy 網際網路" },
        new() { Kind = SettingsHubKind.Cpl, Target = "intl.cpl", En = "Region", Zh = "地區", Keywords = "intl region locale 地區" },
        new() { Kind = SettingsHubKind.Cpl, Target = "joy.cpl", En = "Game Controllers", Zh = "遊戲控制器", Keywords = "joy game controllers joystick 遊戲 控制器 搖桿" },
        new() { Kind = SettingsHubKind.Cpl, Target = "main.cpl", En = "Mouse Properties", Zh = "滑鼠內容", Keywords = "main mouse 滑鼠" },
        new() { Kind = SettingsHubKind.Cpl, Target = "mmsys.cpl", En = "Sound", Zh = "聲音", Keywords = "mmsys sound audio 聲音 音效" },
        new() { Kind = SettingsHubKind.Cpl, Target = "ncpa.cpl", En = "Network Connections", Zh = "網路連線", Keywords = "ncpa network connections adapters 網路 連線 介面卡" },
        new() { Kind = SettingsHubKind.Cpl, Target = "powercfg.cpl", En = "Power Options", Zh = "電源選項", Keywords = "powercfg power options 電源" },
        new() { Kind = SettingsHubKind.Cpl, Target = "sysdm.cpl", En = "System Properties", Zh = "系統內容", Keywords = "sysdm system properties advanced environment 系統 內容 進階 環境變數" },
        new() { Kind = SettingsHubKind.Cpl, Target = "timedate.cpl", En = "Date and Time", Zh = "日期和時間", Keywords = "timedate date time 日期 時間" },
        new() { Kind = SettingsHubKind.Cpl, Target = "wscui.cpl", En = "Security and Maintenance", Zh = "安全性與維護", Keywords = "wscui security maintenance action center 安全性 維護" },
        new() { Kind = SettingsHubKind.Cpl, Target = "control.exe", En = "Control Panel (all items)", Zh = "控制台（所有項目）", Keywords = "control panel all items 控制台 所有項目" },
        new() { Kind = SettingsHubKind.Cpl, Target = "telephon.cpl", En = "Location Information", Zh = "位置資訊", Keywords = "telephon location dialing 位置 撥號" },
    };

    /// <summary>全部項目 · Every entry (Settings → Applets → CPLs).</summary>
    public static readonly List<SettingsHubEntry> All =
        SettingsPages
            .Concat(ControlApplets)
            .Concat(CplFiles)
            .ToList();

    /// <summary>搜尋（雙語＋關鍵字＋指令）· Filter by query across both languages, keywords and the command.</summary>
    public static IEnumerable<SettingsHubEntry> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return All;
        var q = query.Trim().ToLowerInvariant();
        return All.Where(e => e.Haystack.Contains(q));
    }
}
