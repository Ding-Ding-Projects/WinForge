using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 去煩擾／除臃腫切換 · De-bloat / annoyance toggles for the most-complained-about Win11 nags.
/// 由 ultracode workflow 產生並對抗式覆核。
/// </summary>
public static class AnnoyanceTweaks
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // ===== copilot-search (16) =====
        Tweak.RegToggle("annoy.copilot-off", "Turn off Windows Copilot", "熄 Windows Copilot",
            "Disable the Copilot integration many users find intrusive.", "停用好多人覺得阻住嘅 Copilot 整合呀。",
            RegRoot.HKCU, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "copilot,ai,煩擾,助手"),
        
        Tweak.RegToggle("annoy.recall-off", "Turn off Recall snapshots", "熄 Recall 截圖記錄",
            "Stop Windows Recall from continuously saving snapshots of your screen.", "唔好俾 Recall 一直影低你個畫面嘅截圖呀。",
            RegRoot.HKCU, @"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "recall,ai,snapshot,私隱,截圖"),
        
        Tweak.RegToggle("annoy.search-box-suggestions-off", "Turn off search box web suggestions", "熄搜尋框嘅網上建議",
            "Disable web-powered suggestions in the Start menu search box.", "停用開始選單搜尋框入面靠網上嚟嘅建議呀。",
            RegRoot.HKCU, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "search,web,suggestions,搜尋,建議"),
        
        Tweak.RegToggle("annoy.bing-search-off", "Turn off Bing in Start search", "熄開始搜尋嘅 Bing",
            "Remove Bing and web results from the Start menu search.", "喺開始選單搜尋度攞走 Bing 同網上結果呀。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled",
            onValue: 0, offValue: 1, restart: RestartScope.Explorer, keywords: "bing,web,search,搜尋,網頁"),
        
        Tweak.RegToggle("annoy.cortana-consent-off", "Turn off Cortana consent in search", "熄搜尋度嘅 Cortana 同意",
            "Disable the Cortana consent flag tied to web search results.", "停用同網上搜尋結果掛鈎嘅 Cortana 同意設定呀。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent",
            onValue: 0, offValue: 1, restart: RestartScope.Explorer, keywords: "cortana,search,consent,搜尋,同意"),
        
        Tweak.RegToggle("annoy.search-highlights-off", "Turn off Search Highlights", "熄搜尋焦點 (Search Highlights)",
            "Disable the rotating illustrations and trivia in the search box.", "停用搜尋框入面轉嚟轉去嘅插圖同冷知識呀。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDynamicSearchBoxEnabled",
            onValue: 0, offValue: 1, restart: RestartScope.Explorer, keywords: "highlights,search,焦點,搜尋,插圖"),
        
        Tweak.RegToggle("annoy.cortana-off", "Turn off Cortana", "熄 Cortana",
            "Block Cortana entirely via policy (applies to all users).", "用原則完全封鎖 Cortana（套用到所有用戶）呀。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana",
            onValue: 0, offValue: 1, requiresAdmin: true, restart: RestartScope.Reboot, keywords: "cortana,assistant,助手"),
        
        Tweak.RegToggle("annoy.search-web-off", "Turn off web search in Search", "熄搜尋度嘅網上搜尋",
            "Stop Windows Search from connecting to the web for results.", "唔好俾 Windows 搜尋連去網上攞結果呀。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "ConnectedSearchUseWeb",
            onValue: 0, offValue: 1, requiresAdmin: true, restart: RestartScope.Reboot, keywords: "web,search,connected,搜尋,網上"),
        
        Tweak.RegToggle("annoy.disable-web-search-policy-off", "Turn off web results in Search (policy)", "用原則熄搜尋嘅網上結果",
            "Use the DisableWebSearch policy to keep Search strictly local.", "用 DisableWebSearch 原則令搜尋淨係搵本機嘢呀。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "DisableWebSearch",
            onValue: 1, offValue: 0, requiresAdmin: true, restart: RestartScope.Reboot, keywords: "web,search,disable,搜尋,網上,原則"),
        
        Tweak.RegToggle("annoy.cortana-above-lock-off", "Turn off Cortana above lock screen", "熄鎖屏上面嘅 Cortana",
            "Prevent Cortana from being usable above the lock screen.", "唔好俾 Cortana 喺鎖屏上面用到呀。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortanaAboveLock",
            onValue: 0, offValue: 1, requiresAdmin: true, restart: RestartScope.Reboot, keywords: "cortana,lock,鎖屏,助手"),
        
        Tweak.RegToggle("annoy.edge-copilot-sidebar-off", "Turn off Edge Copilot sidebar", "熄 Edge Copilot 側欄",
            "Hide the Copilot / hubs sidebar button in Microsoft Edge.", "喺 Microsoft Edge 度收埋 Copilot／hubs 側欄掣呀。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Edge", "HubsSidebarEnabled",
            onValue: 0, offValue: 1, requiresAdmin: true, restart: RestartScope.None, keywords: "edge,copilot,sidebar,側欄"),
        
        Tweak.RegToggle("annoy.edge-shopping-off", "Turn off Edge shopping assistant", "熄 Edge 購物助手",
            "Disable Edge's shopping / discover assistant and price comparisons.", "停用 Edge 嘅購物／discover 助手同比價功能呀。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Edge", "EdgeShoppingAssistantEnabled",
            onValue: 0, offValue: 1, requiresAdmin: true, restart: RestartScope.None, keywords: "edge,shopping,discover,購物,助手"),
        
        Tweak.RegToggle("annoy.edge-collections-off", "Turn off Edge Collections", "熄 Edge Collections",
            "Hide the Collections feature button in Microsoft Edge.", "喺 Microsoft Edge 度收埋 Collections 功能掣呀。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Edge", "EdgeCollectionsEnabled",
            onValue: 0, offValue: 1, requiresAdmin: true, restart: RestartScope.None, keywords: "edge,collections,集藏"),
        
        Tweak.RegToggle("annoy.edge-copilot-page-context-off", "Turn off Edge Copilot page context", "熄 Edge Copilot 讀取頁面內容",
            "Stop Edge Copilot from reading the content of pages you visit.", "唔好俾 Edge Copilot 讀你睇緊嘅網頁內容呀。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Edge", "CopilotPageContext",
            onValue: 0, offValue: 1, requiresAdmin: true, restart: RestartScope.None, keywords: "edge,copilot,context,頁面,私隱"),
        
        Tweak.Cmd("annoy.copilot-key-remap", "Remap the Copilot key to do nothing", "將 Copilot 鍵改成乜都唔做",
            "Disable the dedicated Copilot key by clearing its launch mapping (re-login to apply).", "清走 Copilot 專用鍵嘅啟動設定嚟停用佢（重新登入先生效）呀。",
            "Disable Copilot key", "停用 Copilot 鍵",
            @"reg add ""HKCU\Software\Classes\CLSID\{2781761E-28E0-4109-99FE-B9D127C57AFE}\InprocServer32"" /ve /d """" /f",
            requiresAdmin: false, destructive: false, restart: RestartScope.Explorer, keywords: "copilot,key,鍵,鍵盤"),
        
        Tweak.Cmd("annoy.click-to-do-off", "Turn off Click to Do", "熄 Click to Do",
            "Disable the AI 'Click to Do' overlay via policy.", "用原則停用 AI 嘅「Click to Do」浮層呀。",
            "Disable Click to Do", "停用 Click to Do",
            @"reg add ""HKCU\Software\Policies\Microsoft\Windows\WindowsAI"" /v DisableClickToDo /t REG_DWORD /d 1 /f",
            requiresAdmin: false, destructive: false, restart: RestartScope.Explorer, keywords: "click to do,ai,overlay,浮層"),

        Tweak.RegToggle("annoy.remove-copilot-app", "Remove the Copilot app", "移除 Copilot app",
            "Use policy to remove the standalone Microsoft Copilot app for the current user.", "用原則為目前用戶移除獨立嘅 Microsoft Copilot app 呀。",
            RegRoot.HKCU, @"Software\Policies\Microsoft\Windows\WindowsAI", "RemoveMicrosoftCopilotApp",
            onValue: 1, offValue: 0, restart: RestartScope.SignOut, keywords: "copilot,app,remove,ai,移除,助手"),

        // ===== ads-nags (16) =====
        Tweak.RegToggle("annoy.lockscreen-spotlight-tips-off", "Turn off lock-screen Spotlight tips", "熄鎖機畫面 Spotlight 提示",
            "Stop the rotating lock-screen overlay that shows tips and promos.", "唔再喺鎖機畫面碌出嗰啲提示同推廣呀。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "RotatingLockScreenOverlayEnabled",
            onValue: 0, offValue: 1, restart: RestartScope.None, keywords: "lock screen,spotlight,提示,鎖機"),
        
        Tweak.RegToggle("annoy.lockscreen-funfacts-off", "Turn off lock-screen fun facts & tips", "熄鎖機畫面趣聞同貼士",
            "Hide the \"fun facts, tips and tricks\" lock-screen content from Spotlight.", "收埋鎖機畫面嗰啲所謂趣聞同貼士啦。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338387Enabled",
            onValue: 0, offValue: 1, restart: RestartScope.None, keywords: "lock screen,fun facts,tips,趣聞,貼士"),
        
        Tweak.RegToggle("annoy.settings-suggested-393-off", "Hide suggested content in Settings (1)", "收埋設定入面嘅建議內容 (1)",
            "Stop the Settings app from showing suggested/promo content cards.", "唔好喺設定 App 度彈嗰啲建議同推廣卡呀。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338393Enabled",
            onValue: 0, offValue: 1, restart: RestartScope.None, keywords: "settings,suggested,建議,設定"),
        
        Tweak.RegToggle("annoy.settings-suggested-694-off", "Hide suggested content in Settings (2)", "收埋設定入面嘅建議內容 (2)",
            "Remove another set of suggested content shown inside Settings.", "再清埋設定入面另一批建議內容啦。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353694Enabled",
            onValue: 0, offValue: 1, restart: RestartScope.None, keywords: "settings,suggested,建議,設定"),
        
        Tweak.RegToggle("annoy.settings-suggested-696-off", "Hide suggested content in Settings (3)", "收埋設定入面嘅建議內容 (3)",
            "Remove the remaining suggested content cards inside Settings.", "清埋設定入面剩低嗰啲建議內容卡呀。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353696Enabled",
            onValue: 0, offValue: 1, restart: RestartScope.None, keywords: "settings,suggested,建議,設定"),
        
        Tweak.RegToggle("annoy.scoobe-off", "Turn off \"Get even more out of Windows\" nag", "熄「再進一步善用 Windows」嘅纏擾",
            "Stop the post-update finish-setup nag screen (SCOOBE).", "唔再彈嗰個更新後叫你完成設定嘅畫面呀。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled",
            onValue: 0, offValue: 1, restart: RestartScope.None, keywords: "scoobe,finish setup,nag,纏擾,設定"),
        
        Tweak.RegToggle("annoy.welcome-experience-off", "Skip welcome experience after updates", "更新後跳過歡迎畫面",
            "Stop the \"welcome experience\" highlights page that appears after updates and sign-in.", "唔再喺更新同登入之後彈嗰個歡迎介紹畫面呀。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-310093Enabled",
            onValue: 0, offValue: 1, restart: RestartScope.None, keywords: "welcome experience,after update,歡迎,更新"),
        
        Tweak.RegToggle("annoy.softlanding-off", "Turn off tips & suggestions (soft landing)", "熄提示同建議（soft landing）",
            "Disable the \"tips, tricks and suggestions\" pop-ups Windows shows as you use it.", "停用 Windows 一路用一路彈嗰啲提示同建議呀。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled",
            onValue: 0, offValue: 1, restart: RestartScope.None, keywords: "tips,suggestions,soft landing,提示,建議"),
        
        Tweak.RegToggle("annoy.windows-tips-338389-off", "Turn off Windows tips notifications", "熄 Windows 貼士通知",
            "Stop the \"get tips, tricks, and suggestions as you use Windows\" notifications.", "唔再彈嗰啲用 Windows 期間嘅貼士通知呀。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled",
            onValue: 0, offValue: 1, restart: RestartScope.None, keywords: "tips,notifications,貼士,通知"),
        
        Tweak.RegToggle("annoy.explorer-sync-ads-off", "Hide File Explorer sync-provider ads", "收埋檔案總管嘅 sync 推廣",
            "Disable the OneDrive/sync-provider promo banners that appear in File Explorer.", "停用檔案總管入面嗰啲 OneDrive／同步推廣橫額呀。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSyncProviderNotifications",
            onValue: 0, offValue: 1, restart: RestartScope.Explorer, keywords: "explorer,sync,onedrive,ads,檔案總管,廣告"),
        
        Tweak.RegToggle("annoy.start-account-notifications-off", "Turn off account notifications in Start", "熄開始選單嘅帳戶通知",
            "Stop Start menu from nagging about your account (backup, sign-in, etc.).", "唔再喺開始選單度提你帳戶嗰啲嘢（備份、登入之類）呀。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_AccountNotifications",
            onValue: 0, offValue: 1, restart: RestartScope.Explorer, keywords: "start,account,notifications,開始,帳戶,通知"),
        
        Tweak.RegToggle("annoy.start-iris-recommendations-off", "Reduce Start \"recommended\" suggestions", "減少開始選單「建議」推介",
            "Turn off the Iris-driven recommended app/website suggestions in the Start menu.", "熄開始選單嗰啲 Iris 推介嘅 App 同網站建議呀。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_IrisRecommendations",
            onValue: 0, offValue: 1, restart: RestartScope.Explorer, keywords: "start,recommended,iris,開始,建議,推介"),
        
        Tweak.RegToggle("annoy.start-suggestions-off", "Turn off Start menu app suggestions", "熄開始選單嘅 App 建議",
            "Stop occasionally showing suggested apps in the Start menu.", "唔好喺開始選單度間中彈建議 App 呀。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled",
            onValue: 0, offValue: 1, restart: RestartScope.Explorer, keywords: "start,suggestions,開始,建議"),
        
        Tweak.RegToggle("annoy.preinstalled-apps-off", "Stop silently installing suggested apps", "唔好靜雞雞裝建議 App",
            "Disable auto-installing of promoted/suggested apps from the Microsoft Store.", "停用自動裝商店推廣嗰啲建議 App 呀。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled",
            onValue: 0, offValue: 1, restart: RestartScope.None, keywords: "preinstalled,suggested apps,silent install,自動安裝,建議"),
        
        Tweak.RegToggle("annoy.contentdelivery-master-off", "Turn off general content suggestions", "熄一般內容建議",
            "Master switch that stops Content Delivery Manager from fetching suggested content.", "總開關，唔再俾 Content Delivery Manager 攞建議內容呀。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "ContentDeliveryAllowed",
            onValue: 0, offValue: 1, restart: RestartScope.None, keywords: "content delivery,suggestions,建議,內容"),
        
        Tweak.Cmd("annoy.widgets-board-off", "Disable the Widgets board", "停用小工具面板",
            "Turn off the news & interests Widgets board for all users on this PC.", "為呢部機所有用戶熄埋新聞同興趣嘅小工具面板呀。",
            "Disable Widgets", "停用小工具",
            "reg add \"HKLM\\Software\\Policies\\Microsoft\\Dsh\" /v AllowNewsAndInterests /t REG_DWORD /d 0 /f",
            requiresAdmin: true, restart: RestartScope.Explorer, keywords: "widgets,news and interests,小工具,新聞"),

        Tweak.RegToggle("annoy.consumer-features-off", "Disable Windows consumer features", "停用 Windows 消費者功能",
            "Stop Windows from auto-installing promoted apps and showing consumer-oriented suggestions.",
            "唔再俾 Windows 自動裝推廣 App 同顯示消費者導向嘅建議呀。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures",
            onValue: 1, offValue: 0, requiresAdmin: true, restart: RestartScope.SignOut,
            keywords: "consumer features,cloud content,debloat,suggested apps,消費者,推廣"),

        Tweak.RegToggle("annoy.settings-hide-home", "Hide the Settings homepage", "隱藏設定首頁",
            "Hide the ad-laden Home page in the Settings app so it opens straight to System.",
            "收埋設定 App 嗰個滿是廣告嘅首頁，開就直接去「系統」呀。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "SettingsPageVisibility",
            onValue: "hide:home", offValue: null, kind: RegistryValueKind.String,
            requiresAdmin: true, restart: RestartScope.None,
            keywords: "settings,home,homepage,hide,設定,首頁,隱藏"),
    };
}
