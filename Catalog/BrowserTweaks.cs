using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

public static class BrowserTweaks
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // --- chrome (20) ---
        Tweak.Shell("br.chrome.launch", "Launch Chrome", "啟動 Chrome",
            "Open Google Chrome normally.", "正常打開 Google Chrome。",
            "Launch", "啟動", "chrome", "",
            keywords: "chrome,launch,啟動,瀏覽器"),
        
        Tweak.Shell("br.chrome.incognito", "Open Incognito window", "開無痕視窗",
            "Launch Chrome in a private Incognito window.", "用無痕模式打開 Chrome 視窗。",
            "Incognito", "無痕", "chrome", "--incognito",
            keywords: "chrome,incognito,private,無痕,私隱"),
        
        Tweak.Shell("br.chrome.app-mode", "Open in App mode", "用應用程式模式開",
            "Launch a site as a standalone app window with no tabs or toolbar.", "將網站當成獨立應用程式視窗開，冇分頁同工具列。",
            "App mode", "應用程式", "chrome", "--app=https://www.google.com",
            keywords: "chrome,app,kiosk,應用程式,視窗"),
        
        Tweak.Shell("br.chrome.kiosk", "Open in Kiosk mode", "用 Kiosk 全螢幕模式",
            "Launch Chrome full-screen in kiosk mode for displays or signage.", "用 Kiosk 全螢幕模式打開 Chrome，啱做展示或者告示牌。",
            "Kiosk", "Kiosk", "chrome", "--kiosk https://www.google.com",
            keywords: "chrome,kiosk,fullscreen,全螢幕,展示"),
        
        Tweak.Shell("br.chrome.new-window", "Open a new window", "開新視窗",
            "Force Chrome to open a brand new browser window.", "強制 Chrome 開一個全新嘅瀏覽器視窗。",
            "New window", "新視窗", "chrome", "--new-window",
            keywords: "chrome,window,new,新視窗"),
        
        Tweak.Shell("br.chrome.guest", "Open Guest window", "開訪客視窗",
            "Launch Chrome in Guest mode with no profile data saved.", "用訪客模式打開 Chrome，唔會儲存任何設定檔資料。",
            "Guest", "訪客", "chrome", "--guest",
            keywords: "chrome,guest,訪客,profile"),
        
        Tweak.Shell("br.chrome.profile", "Open specific profile", "開指定設定檔",
            "Launch Chrome using the Default profile directory.", "用 Default 設定檔資料夾打開 Chrome。",
            "Open profile", "開設定檔", "chrome", "--profile-directory=Default",
            keywords: "chrome,profile,設定檔,profile-directory"),
        
        Tweak.Shell("br.chrome.no-extensions", "Launch without extensions", "停用擴充功能啟動",
            "Start Chrome with all extensions disabled to troubleshoot issues.", "停用所有擴充功能嚟啟動 Chrome，用嚟排查問題。",
            "No extensions", "停擴充", "chrome", "--disable-extensions",
            keywords: "chrome,extensions,disable,擴充,排查"),
        
        Tweak.Shell("br.chrome.flags", "Open experimental flags", "開實驗性功能",
            "Open chrome://flags to toggle experimental browser features.", "打開 chrome://flags 嚟切換實驗性瀏覽器功能。",
            "Open flags", "開 flags", "chrome", "chrome://flags",
            keywords: "chrome,flags,experimental,實驗,功能"),
        
        Tweak.Shell("br.chrome.settings", "Open Settings page", "開設定頁",
            "Open the chrome://settings configuration page.", "打開 chrome://settings 設定頁面。",
            "Open settings", "開設定", "chrome", "chrome://settings",
            keywords: "chrome,settings,設定,configuration"),
        
        Tweak.Shell("br.chrome.version", "Open Version info", "開版本資料",
            "Open chrome://version to view build, profile path and command line.", "打開 chrome://version 睇版本、設定檔路徑同命令列資料。",
            "Open version", "開版本", "chrome", "chrome://version",
            keywords: "chrome,version,build,版本,資料"),
        
        Tweak.Shell("br.chrome.flush-dns", "Open DNS cache page", "開 DNS 快取頁",
            "Open chrome://net-internals/#dns where you can clear Chrome's host cache.", "打開 chrome://net-internals/#dns，喺度可以清除 Chrome 嘅主機快取。",
            "Open DNS", "開 DNS", "chrome", "chrome://net-internals/#dns",
            keywords: "chrome,dns,net-internals,flush,快取,清除"),
        
        Tweak.Shell("br.chrome.extensions", "Open Extensions page", "開擴充功能頁",
            "Open chrome://extensions to manage installed extensions.", "打開 chrome://extensions 嚟管理已安裝嘅擴充功能。",
            "Extensions", "擴充功能", "chrome", "chrome://extensions",
            keywords: "chrome,extensions,manage,擴充,管理"),
        
        Tweak.Shell("br.chrome.downloads", "Open Downloads page", "開下載頁",
            "Open chrome://downloads to view download history.", "打開 chrome://downloads 嚟睇下載紀錄。",
            "Downloads", "下載", "chrome", "chrome://downloads",
            keywords: "chrome,downloads,history,下載,紀錄"),
        
        Tweak.Shell("br.chrome.history", "Open History page", "開瀏覽紀錄頁",
            "Open chrome://history to review and clear browsing history.", "打開 chrome://history 嚟睇同清除瀏覽紀錄。",
            "History", "紀錄", "chrome", "chrome://history",
            keywords: "chrome,history,clear,瀏覽,紀錄"),
        
        Tweak.Shell("br.chrome.clear-cache", "Open Clear data dialog", "開清除資料對話框",
            "Open chrome://settings/clearBrowserData to clear cache and cookies.", "打開 chrome://settings/clearBrowserData 嚟清除快取同 cookie。",
            "Clear data", "清除資料", "chrome", "chrome://settings/clearBrowserData",
            keywords: "chrome,clear,cache,cookies,快取,清除"),
        
        Tweak.Shell("br.chrome.open-url", "Open URL in new window", "喺新視窗開網址",
            "Open Google in a fresh Chrome window.", "喺一個新 Chrome 視窗打開 Google。",
            "Open URL", "開網址", "chrome", "--new-window https://www.google.com",
            keywords: "chrome,url,window,網址,新視窗"),
        
        Tweak.Shell("br.chrome.proxy", "Launch with proxy server", "用代理伺服器啟動",
            "Start Chrome routing traffic through a local proxy on port 8080 (edit as needed).", "用本機 8080 埠嘅代理伺服器路由流量嚟啟動 Chrome（按需要修改）。",
            "Use proxy", "用代理", "chrome", "--proxy-server=127.0.0.1:8080",
            keywords: "chrome,proxy,proxy-server,代理,伺服器"),
        
        Tweak.Cmd("br.chrome.safe-mode", "Launch in safe diagnostic mode", "用安全診斷模式啟動",
            "Start Chrome with extensions disabled and a clean temporary profile for diagnosis.", "停用擴充功能再用一個乾淨臨時設定檔嚟啟動 Chrome，方便診斷。",
            "Safe mode", "安全模式", "start \"\" chrome --disable-extensions \"--user-data-dir=%TEMP%\\chrome-safe\"",
            keywords: "chrome,safe,diagnostic,安全,診斷,排查"),
        
        Tweak.Shell("br.chrome.disable-gpu", "Launch with GPU disabled", "停用 GPU 啟動",
            "Start Chrome with hardware GPU acceleration turned off to fix rendering glitches.", "關閉硬件 GPU 加速嚟啟動 Chrome，用嚟修正畫面顯示問題。",
            "Disable GPU", "停 GPU", "chrome", "--disable-gpu",
            keywords: "chrome,gpu,acceleration,render,顯示,加速"),

        // --- edge (20) ---
        Tweak.Shell("br.edge.launch", "Launch Microsoft Edge", "開啟 Microsoft Edge",
            "Start a normal Microsoft Edge browser window.", "開一個正常嘅 Microsoft Edge 視窗。",
            "Launch", "開啟", "msedge.exe", "",
            keywords: "edge,browser,launch,瀏覽器,開啟"),
        
        Tweak.Shell("br.edge.inprivate", "Open InPrivate window", "開無痕視窗",
            "Open a new Edge InPrivate browsing window that keeps no history.", "開一個唔留紀錄嘅 Edge 無痕瀏覽視窗。",
            "Open", "開啟", "msedge.exe", "--inprivate",
            keywords: "edge,inprivate,private,無痕,私隱"),
        
        Tweak.Shell("br.edge.app-mode", "Launch site in app mode", "用應用程式模式開網站",
            "Open a website in Edge app mode (no tabs or toolbar), here example.com.", "用 Edge 應用程式模式開網站（冇分頁同工具列），呢度用 example.com。",
            "Launch", "開啟", "msedge.exe", "--app=https://example.com",
            keywords: "edge,app,mode,kiosk,應用程式,模式"),
        
        Tweak.Shell("br.edge.kiosk", "Launch in kiosk mode", "用自助服務模式開啟",
            "Start Edge in full-screen kiosk mode showing example.com.", "用全螢幕自助服務（kiosk）模式開啟 Edge，顯示 example.com。",
            "Launch", "開啟", "msedge.exe", "--kiosk https://example.com --edge-kiosk-type=fullscreen",
            keywords: "edge,kiosk,fullscreen,自助,全螢幕"),
        
        Tweak.Shell("br.edge.flags", "Open Edge experiments", "開實驗功能頁",
            "Open edge://flags to toggle experimental Edge features.", "開 edge://flags 去切換 Edge 嘅實驗功能。",
            "Open", "開啟", "msedge.exe", "edge://flags",
            keywords: "edge,flags,experiments,實驗,功能"),
        
        Tweak.Shell("br.edge.settings", "Open Edge settings", "開 Edge 設定",
            "Open the main Edge settings page.", "開 Edge 嘅主設定頁面。",
            "Open", "開啟", "msedge.exe", "edge://settings",
            keywords: "edge,settings,設定"),
        
        Tweak.Shell("br.edge.version", "Show Edge version", "睇 Edge 版本",
            "Open edge://version to view the build, channel and profile path.", "開 edge://version 去睇版本、頻道同設定檔路徑。",
            "Open", "開啟", "msedge.exe", "edge://version",
            keywords: "edge,version,about,版本,關於"),
        
        Tweak.Shell("br.edge.clear-data", "Clear browsing data", "清除瀏覽資料",
            "Open the Clear Browsing Data dialog in Edge settings.", "開 Edge 設定入面嘅清除瀏覽資料對話框。",
            "Open", "開啟", "msedge.exe", "edge://settings/clearBrowserData",
            keywords: "edge,clear,cache,history,清除,快取,紀錄"),
        
        Tweak.Shell("br.edge.extensions", "Open extensions page", "開擴充功能頁",
            "Open edge://extensions to manage installed Edge extensions.", "開 edge://extensions 去管理已安裝嘅 Edge 擴充功能。",
            "Open", "開啟", "msedge.exe", "edge://extensions",
            keywords: "edge,extensions,addons,擴充,外掛"),
        
        Tweak.Shell("br.edge.favorites", "Open favorites manager", "開我的最愛管理",
            "Open edge://favorites to manage your saved bookmarks.", "開 edge://favorites 去管理你儲低嘅書籤。",
            "Open", "開啟", "msedge.exe", "edge://favorites",
            keywords: "edge,favorites,bookmarks,我的最愛,書籤"),
        
        Tweak.Shell("br.edge.downloads", "Open downloads", "開下載項目",
            "Open edge://downloads to view your download history.", "開 edge://downloads 去睇下載紀錄。",
            "Open", "開啟", "msedge.exe", "edge://downloads",
            keywords: "edge,downloads,下載"),
        
        Tweak.Shell("br.edge.profiles", "Open profile settings", "開設定檔設定",
            "Open edge://settings/profiles to manage Edge profiles and sync.", "開 edge://settings/profiles 去管理 Edge 設定檔同同步。",
            "Open", "開啟", "msedge.exe", "edge://settings/profiles",
            keywords: "edge,profiles,sync,設定檔,同步"),
        
        Tweak.Shell("br.edge.ie-mode", "Open IE mode settings", "開 IE 模式設定",
            "Open the default browser page where Internet Explorer (IE) mode is configured.", "開預設瀏覽器頁面，喺嗰度設定 Internet Explorer（IE）模式。",
            "Open", "開啟", "msedge.exe", "edge://settings/defaultBrowser",
            keywords: "edge,ie,mode,internet explorer,相容"),
        
        Tweak.Shell("br.edge.new-window-url", "Open URL in new window", "喺新視窗開網址",
            "Open Microsoft's homepage in a brand-new Edge window.", "喺一個全新嘅 Edge 視窗開 Microsoft 首頁。",
            "Open", "開啟", "msedge.exe", "--new-window https://www.microsoft.com",
            keywords: "edge,new window,url,新視窗,網址"),
        
        Tweak.Shell("br.edge.privacy", "Open privacy settings", "開私隱設定",
            "Open edge://settings/privacy to adjust tracking prevention and privacy options.", "開 edge://settings/privacy 去調整追蹤防護同私隱選項。",
            "Open", "開啟", "msedge.exe", "edge://settings/privacy",
            keywords: "edge,privacy,tracking,私隱,追蹤"),
        
        Tweak.Shell("br.edge.passwords", "Open password settings", "開密碼設定",
            "Open edge://settings/passwords to manage the saved-password options (no credentials are entered).", "開 edge://settings/passwords 去管理儲存密碼嘅選項（唔會輸入任何帳密）。",
            "Open", "開啟", "msedge.exe", "edge://settings/passwords",
            keywords: "edge,passwords,密碼,管理"),
        
        Tweak.Shell("br.edge.gpu", "Open GPU diagnostics", "開 GPU 診斷",
            "Open edge://gpu to view graphics and hardware-acceleration status.", "開 edge://gpu 去睇繪圖同硬件加速嘅狀態。",
            "Open", "開啟", "msedge.exe", "edge://gpu",
            keywords: "edge,gpu,graphics,acceleration,繪圖,加速"),
        
        Tweak.Shell("br.edge.net-export", "Open network logger", "開網路紀錄工具",
            "Open edge://net-export to capture a network log for troubleshooting.", "開 edge://net-export 去擷取網路紀錄做疑難排解。",
            "Open", "開啟", "msedge.exe", "edge://net-export",
            keywords: "edge,net-export,network,log,網路,紀錄"),
        
        Tweak.Shell("br.edge.startup-boost", "Open startup boost setting", "開啟動加速設定",
            "Open edge://settings/system where Startup boost can be turned on or off.", "開 edge://settings/system，喺嗰度開或關啟動加速。",
            "Open", "開啟", "msedge.exe", "edge://settings/system",
            keywords: "edge,startup boost,system,啟動加速,系統"),
        
        Tweak.Shell("br.edge.set-default", "Open default apps settings", "開預設應用程式設定",
            "Open the Windows Default apps settings page to set Microsoft Edge as your default browser.", "開 Windows 預設應用程式設定頁，將 Microsoft Edge 設做預設瀏覽器。",
            "Open", "開啟", "ms-settings:defaultapps", "",
            keywords: "edge,default,browser,預設,瀏覽器,defaultapps"),

        // --- policies (20) ---
        Tweak.RegToggle("br.policies.chrome-homepage", "Chrome: set homepage policy", "Chrome：設定主頁政策",
            "Force the Chrome homepage to a fixed URL via the HomepageLocation policy.", "用 HomepageLocation 政策強制 Chrome 主頁去一個固定網址。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Google\\Chrome", "HomepageLocation", "https://www.google.com", null,
            RegistryValueKind.String, requiresAdmin: true, keywords: "chrome,homepage,policy,主頁,政策"),
        
        Tweak.RegToggle("br.policies.chrome-newtab", "Chrome: set new tab page", "Chrome：設定新分頁版面",
            "Force the Chrome new tab page to a fixed URL via NewTabPageLocation.", "用 NewTabPageLocation 政策強制 Chrome 新分頁去一個固定網址。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Google\\Chrome", "NewTabPageLocation", "https://www.google.com", null,
            RegistryValueKind.String, requiresAdmin: true, keywords: "chrome,newtab,policy,新分頁,政策"),
        
        Tweak.RegToggle("br.policies.chrome-bookmarkbar", "Chrome: bookmark bar", "Chrome：書籤列",
            "Force the Chrome bookmark bar on (1) or remove the policy to leave it user-controlled.", "用政策強制開啟 Chrome 書籤列（1），或移除政策由用戶自己控制。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Google\\Chrome", "BookmarkBarEnabled", 1, null,
            RegistryValueKind.DWord, requiresAdmin: true, keywords: "chrome,bookmark,bar,書籤,政策"),
        
        IncognitoPolicy("br.policies.chrome-incognito", "Chrome: incognito availability", "Chrome：無痕模式可用性",
            "Control whether Chrome incognito mode is available (0 enabled, 1 disabled, 2 forced).", "控制 Chrome 無痕模式可唔可以用（0 啟用、1 停用、2 強制）。",
            "SOFTWARE\\Policies\\Google\\Chrome", "IncognitoModeAvailability",
            keywords: "chrome,incognito,無痕,政策"),
        
        Tweak.RegToggle("br.policies.chrome-password-manager", "Chrome: password manager", "Chrome：密碼管理員",
            "Enable (1) or disable (0) the Chrome built-in password manager via policy.", "用政策啟用（1）或停用（0）Chrome 內置密碼管理員。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Google\\Chrome", "PasswordManagerEnabled", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: true, keywords: "chrome,password,manager,密碼,政策"),
        
        Tweak.RegToggle("br.policies.chrome-default-browser", "Chrome: default browser prompt", "Chrome：預設瀏覽器提示",
            "Control whether Chrome checks if it is the default browser (1 on, 0 off) via policy.", "用政策控制 Chrome 會唔會檢查自己係咪預設瀏覽器（1 開、0 關）。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Google\\Chrome", "DefaultBrowserSettingEnabled", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: true, keywords: "chrome,default,browser,預設,政策"),
        
        Tweak.RegToggle("br.policies.chrome-metrics", "Chrome: usage metrics reporting", "Chrome：使用統計報告",
            "Enable (1) or disable (0) Chrome usage and crash metrics reporting via policy.", "用政策啟用（1）或停用（0）Chrome 使用同當機統計報告。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Google\\Chrome", "MetricsReportingEnabled", 0, 1,
            RegistryValueKind.DWord, requiresAdmin: true, keywords: "chrome,metrics,telemetry,統計,政策"),
        
        Tweak.RegToggle("br.policies.chrome-background-mode", "Chrome: background mode", "Chrome：背景執行模式",
            "Disable (0) Chrome continuing to run in the background after windows close, or enable (1).", "用政策停用（0）Chrome 喺所有視窗關咗之後仲繼續喺背景跑，或者啟用（1）。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Google\\Chrome", "BackgroundModeEnabled", 0, 1,
            RegistryValueKind.DWord, requiresAdmin: true, keywords: "chrome,background,mode,背景,政策"),
        
        Tweak.RegToggle("br.policies.chrome-safebrowsing", "Chrome: Safe Browsing", "Chrome：安全瀏覽",
            "Enable (1) or disable (0) Chrome Safe Browsing protection via the SafeBrowsingEnabled policy.", "用 SafeBrowsingEnabled 政策啟用（1）或停用（0）Chrome 安全瀏覽保護。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Google\\Chrome", "SafeBrowsingEnabled", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: true, keywords: "chrome,safebrowsing,safety,安全,政策"),
        
        Tweak.RegToggle("br.policies.chrome-autofill-addresses", "Chrome: address autofill", "Chrome：地址自動填入",
            "Enable (1) or disable (0) Chrome autofill for addresses via the AutofillAddressEnabled policy.", "用 AutofillAddressEnabled 政策啟用（1）或停用（0）Chrome 地址自動填入。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Google\\Chrome", "AutofillAddressEnabled", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: true, keywords: "chrome,autofill,address,自動填入,政策"),
        
        Tweak.RegToggle("br.policies.edge-homepage", "Edge: set homepage policy", "Edge：設定主頁政策",
            "Force the Edge homepage to a fixed URL via the HomepageLocation policy.", "用 HomepageLocation 政策強制 Edge 主頁去一個固定網址。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Microsoft\\Edge", "HomepageLocation", "https://www.bing.com", null,
            RegistryValueKind.String, requiresAdmin: true, keywords: "edge,homepage,policy,主頁,政策"),
        
        IncognitoPolicy("br.policies.edge-inprivate", "Edge: InPrivate availability", "Edge：InPrivate 可用性",
            "Control whether Edge InPrivate mode is available (0 enabled, 1 disabled, 2 forced).", "控制 Edge InPrivate 模式可唔可以用（0 啟用、1 停用、2 強制）。",
            "SOFTWARE\\Policies\\Microsoft\\Edge", "InPrivateModeAvailability",
            keywords: "edge,inprivate,private,私密,政策"),
        
        Tweak.RegToggle("br.policies.edge-background-mode", "Edge: background mode", "Edge：背景執行模式",
            "Disable (0) Edge continuing to run in the background after windows close, or enable (1).", "用政策停用（0）Edge 喺所有視窗關咗之後仲繼續喺背景跑，或者啟用（1）。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Microsoft\\Edge", "BackgroundModeEnabled", 0, 1,
            RegistryValueKind.DWord, requiresAdmin: true, keywords: "edge,background,mode,背景,政策"),
        
        Tweak.RegToggle("br.policies.edge-startup-boost", "Edge: startup boost", "Edge：啟動加速",
            "Disable (0) or enable (1) Edge startup boost (pre-launching at sign-in) via policy.", "用政策停用（0）或啟用（1）Edge 啟動加速（登入時預先載入）。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Microsoft\\Edge", "StartupBoostEnabled", 0, 1,
            RegistryValueKind.DWord, requiresAdmin: true, keywords: "edge,startup,boost,啟動,政策"),
        
        Tweak.RegToggle("br.policies.edge-smartscreen", "Edge: SmartScreen", "Edge：SmartScreen",
            "Enable (1) or disable (0) Microsoft Defender SmartScreen in Edge via the SmartScreenEnabled policy.", "用 SmartScreenEnabled 政策啟用（1）或停用（0）Edge 入面嘅 SmartScreen 保護。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Microsoft\\Edge", "SmartScreenEnabled", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: true, keywords: "edge,smartscreen,safety,安全,政策"),
        
        Tweak.RegToggle("br.policies.edge-hubs-sidebar", "Edge: hubs sidebar", "Edge：側邊欄",
            "Show (1) or hide (0) the Edge hubs sidebar via the HubsSidebarEnabled policy.", "用 HubsSidebarEnabled 政策顯示（1）或隱藏（0）Edge 側邊欄。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Microsoft\\Edge", "HubsSidebarEnabled", 0, 1,
            RegistryValueKind.DWord, requiresAdmin: true, keywords: "edge,hubs,sidebar,側邊欄,政策"),
        
        Tweak.RegToggle("br.policies.edge-shopping", "Edge: shopping assistant", "Edge：購物助手",
            "Disable (0) or enable (1) the Edge shopping assistant via the EdgeShoppingAssistantEnabled policy.", "用 EdgeShoppingAssistantEnabled 政策停用（0）或啟用（1）Edge 購物助手。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Microsoft\\Edge", "EdgeShoppingAssistantEnabled", 0, 1,
            RegistryValueKind.DWord, requiresAdmin: true, keywords: "edge,shopping,assistant,購物,政策"),
        
        Tweak.RegToggle("br.policies.edge-password-manager", "Edge: password manager", "Edge：密碼管理員",
            "Enable (1) or disable (0) the Edge built-in password manager via the PasswordManagerEnabled policy.", "用 PasswordManagerEnabled 政策啟用（1）或停用（0）Edge 內置密碼管理員。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Microsoft\\Edge", "PasswordManagerEnabled", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: true, keywords: "edge,password,manager,密碼,政策"),
        
        Tweak.RegToggle("br.policies.edge-personalization", "Edge: web data personalization", "Edge：個人化內容",
            "Disable (0) or enable (1) Edge personalization of ads, search and news via PersonalizationReportingEnabled.", "用 PersonalizationReportingEnabled 政策停用（0）或啟用（1）Edge 對廣告、搜尋同新聞嘅個人化。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Microsoft\\Edge", "PersonalizationReportingEnabled", 0, 1,
            RegistryValueKind.DWord, requiresAdmin: true, keywords: "edge,personalization,ads,個人化,政策"),
        
        Tweak.RegToggle("br.policies.edge-default-browser", "Edge: default browser prompt", "Edge：預設瀏覽器提示",
            "Control whether Edge checks if it is the default browser (1 on, 0 off) via the DefaultBrowserSettingEnabled policy.", "用 DefaultBrowserSettingEnabled 政策控制 Edge 會唔會檢查自己係咪預設瀏覽器（1 開、0 關）。",
            RegRoot.HKLM, "SOFTWARE\\Policies\\Microsoft\\Edge", "DefaultBrowserSettingEnabled", 1, 0,
            RegistryValueKind.DWord, requiresAdmin: true, keywords: "edge,default,browser,預設,政策"),

        // --- profiles (20) ---
        Tweak.Powershell("br.profiles.list-chrome-profiles", "List Chrome profiles", "列出 Chrome 設定檔",
            "List all Chrome profile folders under your local app data.", "列出本機應用程式資料夾入面所有 Chrome 設定檔資料夾。",
            "List", "列出", "Get-ChildItem -Path \"$env:LOCALAPPDATA\\Google\\Chrome\\User Data\" -Directory -ErrorAction SilentlyContinue | Select-Object Name, LastWriteTime | Format-Table -AutoSize",
            keywords: "chrome,profiles,設定檔,profile"),
        
        Tweak.Cmd("br.profiles.open-chrome-userdata", "Open Chrome user data folder", "開啟 Chrome 用戶資料夾",
            "Open the Chrome User Data folder in File Explorer.", "喺檔案總管度開啟 Chrome 用戶資料夾。",
            "Open", "開啟", "explorer \"%LOCALAPPDATA%\\Google\\Chrome\\User Data\"",
            keywords: "chrome,userdata,folder,資料夾"),
        
        Tweak.Powershell("br.profiles.clear-chrome-cache", "Clear Chrome cache", "清除 Chrome 快取",
            "Delete the Chrome Default profile cache folder to free space (close Chrome first).", "刪除 Chrome 預設設定檔嘅快取資料夾以釋放空間（請先關閉 Chrome）。",
            "Clear", "清除", "Remove-Item -Path \"$env:LOCALAPPDATA\\Google\\Chrome\\User Data\\Default\\Cache\\*\" -Recurse -Force -ErrorAction SilentlyContinue; Write-Output 'Chrome cache cleared.'",
            destructive: true, keywords: "chrome,cache,clear,快取,清除"),
        
        Tweak.Cmd("br.profiles.open-edge-userdata", "Open Edge user data folder", "開啟 Edge 用戶資料夾",
            "Open the Microsoft Edge User Data folder in File Explorer.", "喺檔案總管度開啟 Microsoft Edge 用戶資料夾。",
            "Open", "開啟", "explorer \"%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\"",
            keywords: "edge,userdata,folder,資料夾"),
        
        Tweak.Powershell("br.profiles.clear-edge-cache", "Clear Edge cache", "清除 Edge 快取",
            "Delete the Edge Default profile cache folder to free space (close Edge first).", "刪除 Edge 預設設定檔嘅快取資料夾以釋放空間（請先關閉 Edge）。",
            "Clear", "清除", "Remove-Item -Path \"$env:LOCALAPPDATA\\Microsoft\\Edge\\User Data\\Default\\Cache\\*\" -Recurse -Force -ErrorAction SilentlyContinue; Write-Output 'Edge cache cleared.'",
            destructive: true, keywords: "edge,cache,clear,快取,清除"),
        
        Tweak.Powershell("br.profiles.reset-chrome-default", "Reset Chrome Default profile", "重設 Chrome 預設設定檔",
            "Rename the Chrome Default profile to a backup so Chrome recreates a fresh one (close Chrome first).", "將 Chrome 預設設定檔改名做備份，等 Chrome 重新建立一個全新嘅（請先關閉 Chrome）。",
            "Reset", "重設", "Rename-Item -Path \"$env:LOCALAPPDATA\\Google\\Chrome\\User Data\\Default\" -NewName (\"Default.bak.\" + (Get-Date -Format 'yyyyMMddHHmmss')) -ErrorAction SilentlyContinue; Write-Output 'Default profile renamed to backup.'",
            destructive: true, keywords: "chrome,reset,profile,重設,設定檔"),
        
        Tweak.Powershell("br.profiles.backup-chrome-bookmarks", "Backup Chrome bookmarks", "備份 Chrome 書籤",
            "Copy the Chrome Default Bookmarks file to your Desktop.", "將 Chrome 預設設定檔嘅書籤檔案複製到你嘅桌面。",
            "Backup", "備份", "Copy-Item -Path \"$env:LOCALAPPDATA\\Google\\Chrome\\User Data\\Default\\Bookmarks\" -Destination \"$env:USERPROFILE\\Desktop\\Chrome-Bookmarks-$(Get-Date -Format 'yyyyMMdd').json\" -Force -ErrorAction SilentlyContinue; Write-Output 'Chrome bookmarks backed up to Desktop (if present).'",
            keywords: "chrome,bookmarks,backup,書籤,備份"),
        
        Tweak.Powershell("br.profiles.backup-edge-bookmarks", "Backup Edge bookmarks", "備份 Edge 書籤",
            "Copy the Edge Default Bookmarks file to your Desktop.", "將 Edge 預設設定檔嘅書籤檔案複製到你嘅桌面。",
            "Backup", "備份", "Copy-Item -Path \"$env:LOCALAPPDATA\\Microsoft\\Edge\\User Data\\Default\\Bookmarks\" -Destination \"$env:USERPROFILE\\Desktop\\Edge-Bookmarks-$(Get-Date -Format 'yyyyMMdd').json\" -Force -ErrorAction SilentlyContinue; Write-Output 'Edge bookmarks backed up to Desktop (if present).'",
            keywords: "edge,bookmarks,backup,書籤,備份"),
        
        Tweak.Shell("br.profiles.set-default-browser", "Set default browser", "設定預設瀏覽器",
            "Open the Default apps settings page to choose your default browser.", "開啟預設應用程式設定頁面，揀你嘅預設瀏覽器。",
            "Open", "開啟", "ms-settings:defaultapps", "",
            keywords: "default,browser,預設,瀏覽器"),
        
        Tweak.Powershell("br.profiles.list-installed-browsers", "List installed browsers", "列出已安裝瀏覽器",
            "List browsers registered under StartMenuInternet in the registry.", "列出登錄檔 StartMenuInternet 入面已註冊嘅瀏覽器。",
            "List", "列出", "@('HKLM:\\SOFTWARE\\Clients\\StartMenuInternet','HKCU:\\SOFTWARE\\Clients\\StartMenuInternet') | ForEach-Object { Get-ChildItem $_ -ErrorAction SilentlyContinue | ForEach-Object { $_.GetValue('') } } | Where-Object { $_ } | Sort-Object -Unique",
            keywords: "browsers,installed,registry,瀏覽器,已安裝"),
        
        Tweak.Shell("br.profiles.open-default-apps", "Open Default apps", "開啟預設應用程式",
            "Open the Windows Default apps settings page.", "開啟 Windows 預設應用程式設定頁面。",
            "Open", "開啟", "ms-settings:defaultapps", "",
            keywords: "default,apps,settings,預設,應用程式"),
        
        Tweak.Powershell("br.profiles.clear-chrome-cookies", "Clear Chrome cookies", "清除 Chrome Cookies",
            "Delete the Chrome Default profile Cookies database — you will be signed out of sites (close Chrome first).", "刪除 Chrome 預設設定檔嘅 Cookies 資料庫 — 你會喺各網站登出（請先關閉 Chrome）。",
            "Clear", "清除", "Remove-Item -Path \"$env:LOCALAPPDATA\\Google\\Chrome\\User Data\\Default\\Network\\Cookies\" -Force -ErrorAction SilentlyContinue; Write-Output 'Chrome cookies cleared.'",
            destructive: true, keywords: "chrome,cookies,clear,清除"),
        
        Tweak.Powershell("br.profiles.show-chrome-version", "Show Chrome version", "顯示 Chrome 版本",
            "Read the installed Chrome version from the registry.", "由登錄檔讀取已安裝嘅 Chrome 版本。",
            "Show", "顯示", "(Get-ItemProperty 'HKLM:\\SOFTWARE\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Google Chrome' -ErrorAction SilentlyContinue).DisplayVersion; (Get-ItemProperty 'HKCU:\\SOFTWARE\\Google\\Chrome\\BLBeacon' -ErrorAction SilentlyContinue).version",
            keywords: "chrome,version,版本"),
        
        Tweak.Cmd("br.profiles.kill-chrome", "Kill all Chrome processes", "結束所有 Chrome 程序",
            "Force-terminate every running chrome.exe process.", "強制結束所有正在運行嘅 chrome.exe 程序。",
            "Kill", "結束", "taskkill /im chrome.exe /f /t",
            destructive: true, keywords: "chrome,kill,taskkill,結束,程序"),
        
        Tweak.Cmd("br.profiles.kill-edge", "Kill all Edge processes", "結束所有 Edge 程序",
            "Force-terminate every running msedge.exe process.", "強制結束所有正在運行嘅 msedge.exe 程序。",
            "Kill", "結束", "taskkill /im msedge.exe /f /t",
            destructive: true, keywords: "edge,kill,taskkill,結束,程序"),
        
        Tweak.Cmd("br.profiles.open-downloads", "Open downloads folder", "開啟下載資料夾",
            "Open your Downloads folder in File Explorer.", "喺檔案總管度開啟你嘅下載資料夾。",
            "Open", "開啟", "explorer \"%USERPROFILE%\\Downloads\"",
            keywords: "downloads,folder,下載,資料夾"),
        
        Tweak.Powershell("br.profiles.chrome-profile-disk-usage", "Chrome profile disk usage", "Chrome 設定檔磁碟用量",
            "Show the total size of the Chrome User Data folder in MB.", "顯示 Chrome 用戶資料夾嘅總大小（MB）。",
            "Measure", "計算", "$s = (Get-ChildItem \"$env:LOCALAPPDATA\\Google\\Chrome\\User Data\" -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum; Write-Output (\"Chrome User Data: {0:N1} MB\" -f ($s/1MB))",
            keywords: "chrome,disk,usage,size,磁碟,用量"),
        
        Tweak.Powershell("br.profiles.edge-profile-disk-usage", "Edge profile disk usage", "Edge 設定檔磁碟用量",
            "Show the total size of the Edge User Data folder in MB.", "顯示 Edge 用戶資料夾嘅總大小（MB）。",
            "Measure", "計算", "$s = (Get-ChildItem \"$env:LOCALAPPDATA\\Microsoft\\Edge\\User Data\" -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum; Write-Output (\"Edge User Data: {0:N1} MB\" -f ($s/1MB))",
            keywords: "edge,disk,usage,size,磁碟,用量"),
        
        Tweak.Powershell("br.profiles.list-edge-profiles", "List Edge profiles", "列出 Edge 設定檔",
            "List all Edge profile folders under your local app data.", "列出本機應用程式資料夾入面所有 Edge 設定檔資料夾。",
            "List", "列出", "Get-ChildItem -Path \"$env:LOCALAPPDATA\\Microsoft\\Edge\\User Data\" -Directory -ErrorAction SilentlyContinue | Select-Object Name, LastWriteTime | Format-Table -AutoSize",
            keywords: "edge,profiles,設定檔,profile"),
        
        Tweak.Powershell("br.profiles.open-edge-downloads", "Open Edge download folder", "開啟 Edge 下載資料夾",
            "Open the download folder configured for Edge (defaults to your Downloads folder).", "開啟 Edge 設定嘅下載資料夾（預設係你嘅下載資料夾）。",
            "Open", "開啟", "$p = (Get-ItemProperty 'HKCU:\\SOFTWARE\\Policies\\Microsoft\\Edge' -Name 'DownloadDirectory' -ErrorAction SilentlyContinue).DownloadDirectory; if (-not $p) { $p = \"$env:USERPROFILE\\Downloads\" }; explorer $p",
            keywords: "edge,downloads,folder,下載,資料夾"),

        // --- webtools (20) ---
        Tweak.Cmd("br.webtools.open-url-default", "Open URL in default browser", "喺預設瀏覽器開網址",
            "Launch a web page in your default browser via the shell start verb.", "用 shell 嘅 start 指令喺你預設瀏覽器開個網頁。",
            "Open", "開啟", "start \"\" https://www.example.com",
            keywords: "browser,url,start,open,瀏覽器,網址"),
        
        Tweak.Cmd("br.webtools.open-about-blank", "Open a blank page", "開空白頁",
            "Open about:blank in your default browser for a clean starting tab.", "喺預設瀏覽器開 about:blank，畀你一個乾淨嘅起始分頁。",
            "Open", "開啟", "start \"\" about:blank",
            keywords: "browser,blank,空白,about,瀏覽器"),
        
        Tweak.Cmd("br.webtools.open-chrome-webstore", "Open Chrome Web Store", "開 Chrome 網上商店",
            "Open the Chrome Web Store extensions page in your default browser.", "喺預設瀏覽器開 Chrome 網上商店嘅擴充功能頁面。",
            "Open", "開啟", "start \"\" https://chromewebstore.google.com/",
            keywords: "chrome,webstore,extensions,擴充,商店,瀏覽器"),
        
        Tweak.Cmd("br.webtools.flush-dns", "Flush DNS cache", "清除 DNS 快取",
            "Clear the system DNS resolver cache so browsers re-resolve hostnames.", "清除系統 DNS 解析快取，等瀏覽器重新解析網域名稱。",
            "Flush", "清除", "ipconfig /flushdns",
            keywords: "dns,flush,ipconfig,cache,快取,清除"),
        
        Tweak.Cmd("br.webtools.open-internet-options", "Open Internet Options", "開網際網路選項",
            "Open the classic Internet Options control panel (inetcpl.cpl).", "開傳統嘅網際網路選項控制台 (inetcpl.cpl)。",
            "Open", "開啟", "control inetcpl.cpl",
            keywords: "internet,options,inetcpl,選項,網際網路"),
        
        Tweak.Cmd("br.webtools.open-proxy-settings", "Open proxy settings", "開 Proxy 設定",
            "Open the Windows network proxy settings page in Settings.", "喺設定開 Windows 嘅網絡 Proxy 設定頁面。",
            "Open", "開啟", "start ms-settings:network-proxy",
            keywords: "proxy,settings,network,設定,網絡"),
        
        Tweak.Cmd("br.webtools.reset-ie-settings", "Reset IE / WinINET settings", "重設 IE / WinINET 設定",
            "Open the Internet Options Advanced tab where you can reset IE / WinINET settings.", "開網際網路選項嘅進階分頁，喺嗰度可以重設 IE / WinINET 設定。",
            "Open", "開啟", "control inetcpl.cpl,,6",
            keywords: "internet explorer,reset,inetcpl,advanced,重設,ie"),
        
        Tweak.Cmd("br.webtools.open-credential-manager", "Open Credential Manager", "開認證管理員",
            "Open the Windows Credential Manager to review stored web credentials.", "開 Windows 認證管理員，睇返儲存咗嘅網頁認證。",
            "Open", "開啟", "control keymgr.dll",
            keywords: "credential,manager,keymgr,認證,管理員"),
        
        Tweak.Powershell("br.webtools.list-saved-wifi", "List saved Wi-Fi profiles", "列出已儲存 Wi-Fi",
            "List all saved Wi-Fi profile names stored on this PC via netsh.", "用 netsh 列出呢部電腦上面所有已儲存嘅 Wi-Fi 設定檔名。",
            "List", "列出", "netsh wlan show profiles",
            keywords: "wifi,wlan,profiles,netsh,已儲存"),
        
        Tweak.Powershell("br.webtools.list-pwa-apps", "List installed PWAs", "列出已安裝 PWA",
            "List Start menu apps whose AppIDs match common installed web apps (PWAs).", "列出開始功能表入面 AppID 配對到常見已安裝網頁應用 (PWA) 嘅項目。",
            "List", "列出", "Get-StartApps | Where-Object { $_.AppID -match 'Edge|Chrome|_crx_|MSEdgeApp' } | Format-Table Name, AppID -AutoSize",
            keywords: "pwa,startapps,web app,網頁應用"),
        
        Tweak.Cmd("br.webtools.open-edge-collections", "Open Edge Collections", "開 Edge 收藏集",
            "Open the Microsoft Edge Collections pane to organise saved web content.", "開 Microsoft Edge 收藏集窗格，整理你儲低嘅網頁內容。",
            "Open", "開啟", "start microsoft-edge:--show-collections",
            keywords: "edge,collections,收藏集"),
        
        Tweak.Cmd("br.webtools.open-edge-favorites", "Open Edge favorites", "開 Edge 我的最愛",
            "Open the Edge favorites management page (in lieu of a reading list).", "開 Edge 我的最愛管理頁面（代替閱讀清單）。",
            "Open", "開啟", "msedge edge://favorites/",
            keywords: "edge,favorites,reading list,我的最愛,閱讀清單"),
        
        Tweak.Cmd("br.webtools.open-edge-site-permissions", "Open Edge site permissions", "開 Edge 網站權限",
            "Open the Edge site permissions (content settings) page.", "開 Edge 嘅網站權限 (內容設定) 頁面。",
            "Open", "開啟", "msedge edge://settings/content",
            keywords: "edge,permissions,content,site,權限,網站"),
        
        Tweak.Cmd("br.webtools.open-edge-startup-pages", "Open Edge startup pages", "開 Edge 啟動頁設定",
            "Open the Edge on-startup settings to configure which pages open at launch.", "開 Edge 啟動時設定，決定開瀏覽器時開邊啲頁面。",
            "Open", "開啟", "msedge edge://settings/onStartup",
            keywords: "edge,startup,onstartup,啟動頁"),
        
        Tweak.Cmd("br.webtools.open-edge-settings", "Open Edge settings", "開 Edge 設定",
            "Open the main Microsoft Edge settings page.", "開 Microsoft Edge 嘅主設定頁面。",
            "Open", "開啟", "msedge edge://settings/",
            keywords: "edge,settings,設定"),
        
        Tweak.Cmd("br.webtools.note-devtools-shortcut", "DevTools shortcut note", "開發人員工具捷徑提示",
            "Print a reminder that F12 (or Ctrl+Shift+I) opens browser DevTools.", "顯示提示：F12（或 Ctrl+Shift+I）可以開瀏覽器嘅開發人員工具。",
            "Show", "顯示", "echo Press F12 or Ctrl+Shift+I in your browser to open DevTools.",
            keywords: "devtools,f12,shortcut,開發人員工具,捷徑"),
        
        Tweak.Cmd("br.webtools.note-browser-task-manager", "Browser task manager note", "瀏覽器工作管理員提示",
            "Print a reminder that Shift+Esc opens the Edge/Chrome browser task manager.", "顯示提示：Shift+Esc 可以開 Edge/Chrome 嘅瀏覽器工作管理員。",
            "Show", "顯示", "echo Press Shift+Esc in Edge or Chrome to open the browser task manager.",
            keywords: "task manager,shift esc,browser,工作管理員,瀏覽器"),
        
        Tweak.Cmd("br.webtools.note-screenshot-webpage", "Capture webpage note", "擷取網頁提示",
            "Print a reminder that Ctrl+Shift+S in Edge captures a web page.", "顯示提示：喺 Edge 撳 Ctrl+Shift+S 可以擷取網頁。",
            "Show", "顯示", "echo Press Ctrl+Shift+S in Edge to capture a web page screenshot.",
            keywords: "screenshot,capture,web,擷取,網頁"),
        
        Tweak.RegToggle("br.webtools.edge-efficiency-mode", "Edge efficiency mode policy", "Edge 效率模式政策",
            "Enable the Microsoft Edge efficiency mode policy to reduce resource usage. Off clears the policy.", "開啟 Microsoft Edge 效率模式政策減少資源用量。關閉會清除呢個政策值。",
            RegRoot.HKCU, @"Software\Policies\Microsoft\Edge", "EfficiencyMode", 1, null,
            keywords: "edge,efficiency,performance,效率模式,省電"),
        
        EdgeStartupBehavior("br.webtools.edge-startup-behavior", "Edge startup behavior policy", "Edge 啟動行為政策",
            "Choose the Edge RestoreOnStartup policy: new tab page, restore last session, or open set pages.", "揀 Edge 嘅 RestoreOnStartup 政策：開新分頁頁面、還原上次工作階段，或者開指定頁面。",
            keywords: "edge,startup,restore,啟動,頁面"),
    };

    // ======================================================================
    //  Local builders · 本地建構器
    //  Presentation-only helpers: same Id / registry value / RequiresAdmin /
    //  restart scope as the original RegChoice, rendered as a RadioGroup with a
    //  coloured state pill. 只改顯示，行為、登錄檔值、權限同重啟範圍完全一樣。
    // ======================================================================

    /// <summary>
    /// 無痕／InPrivate 可用性政策 · Incognito/InPrivate availability policy (0 enabled, 1 disabled, 2 forced)
    /// under HKLM, requires admin — rendered as a RadioGroup with a coloured status pill.
    /// </summary>
    private static TweakDefinition IncognitoPolicy(
        string id, string enT, string zhT, string enD, string zhD,
        string path, string name, string keywords)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.RadioGroup,
            RequiresAdmin = true,
            Keywords = keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Choices = new List<TweakChoice>
            {
                new(new LocalizedText("Enabled", "啟用"), "0"),
                new(new LocalizedText("Disabled", "停用"), "1"),
                new(new LocalizedText("Forced", "強制"), "2"),
            },
            GetCurrentChoice = () =>
            {
                if (RegistryHelper.ValueEquals(RegRoot.HKLM, path, name, 0)) return "0";
                if (RegistryHelper.ValueEquals(RegRoot.HKLM, path, name, 1)) return "1";
                if (RegistryHelper.ValueEquals(RegRoot.HKLM, path, name, 2)) return "2";
                return null;
            },
            SetChoice = val =>
            {
                if (int.TryParse(val, out var v))
                    RegistryHelper.SetValue(RegRoot.HKLM, path, name, v, RegistryValueKind.DWord);
            },
            ColoredStatus = () => RegistryHelper.ValueEquals(RegRoot.HKLM, path, name, 1)
                    ? ("Disabled", "停用", StatusColor.Bad)
                : RegistryHelper.ValueEquals(RegRoot.HKLM, path, name, 2)
                    ? ("Forced", "強制", StatusColor.Warn)
                : RegistryHelper.ValueEquals(RegRoot.HKLM, path, name, 0)
                    ? ("Enabled", "啟用", StatusColor.Good)
                    : ("Not set", "未設定", StatusColor.Neutral),
        };

    /// <summary>
    /// Edge RestoreOnStartup 政策 · Edge startup behaviour (5 new tab, 1 restore session, 4 set pages)
    /// under HKCU, no admin — rendered as a RadioGroup with a coloured status pill.
    /// </summary>
    private static TweakDefinition EdgeStartupBehavior(
        string id, string enT, string zhT, string enD, string zhD, string keywords)
    {
        const string path = @"Software\Policies\Microsoft\Edge";
        const string name = "RestoreOnStartup";
        return new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.RadioGroup,
            Keywords = keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Choices = new List<TweakChoice>
            {
                new(new LocalizedText("Open new tab page", "開新分頁頁面"), "5"),
                new(new LocalizedText("Restore last session", "還原上次工作階段"), "1"),
                new(new LocalizedText("Open a set of pages", "開一組指定頁面"), "4"),
            },
            GetCurrentChoice = () =>
            {
                if (RegistryHelper.ValueEquals(RegRoot.HKCU, path, name, 5)) return "5";
                if (RegistryHelper.ValueEquals(RegRoot.HKCU, path, name, 1)) return "1";
                if (RegistryHelper.ValueEquals(RegRoot.HKCU, path, name, 4)) return "4";
                return null;
            },
            SetChoice = val =>
            {
                if (int.TryParse(val, out var v))
                    RegistryHelper.SetValue(RegRoot.HKCU, path, name, v, RegistryValueKind.DWord);
            },
            ColoredStatus = () => RegistryHelper.ValueEquals(RegRoot.HKCU, path, name, 1)
                    ? ("Restore session", "還原工作階段", StatusColor.Good)
                : RegistryHelper.ValueEquals(RegRoot.HKCU, path, name, 4)
                    ? ("Set pages", "指定頁面", StatusColor.Neutral)
                : RegistryHelper.ValueEquals(RegRoot.HKCU, path, name, 5)
                    ? ("New tab page", "新分頁頁面", StatusColor.Neutral)
                    : ("Not set", "未設定", StatusColor.Neutral),
        };
    }
}
