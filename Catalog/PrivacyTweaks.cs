using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 私隱與遙測 · Privacy &amp; telemetry tweaks.
///
/// 全部用真實、已記錄嘅 Windows 11 登錄檔路徑。
/// All paths are real, documented Windows 11 registry locations.
/// </summary>
public static class PrivacyTweaks
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // 1. Advertising ID — Enabled=1 means personalised ads ON; turning the switch OFF (=0) stops ad tracking.
        Tweak.RegToggle("privacy.advertising-id", "Personalised ads (advertising ID)", "個人化廣告（廣告識別碼）",
            "Let apps use your advertising ID to show personalised ads.", "畀啲 App 用你嘅廣告識別碼嚟顯示個人化廣告。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled",
            onValue: 1, offValue: 0, keywords: "advertising,ad,廣告,追蹤"),

        // 2. Let websites access language list — opt-out value; ON(=1) means opted out (more private).
        Tweak.RegToggle("privacy.language-list-optout", "Block websites reading my language list", "阻止網站讀取語言清單",
            "Stop websites from accessing your language list to track you.", "阻止網站讀取你嘅語言清單嚟追蹤你。",
            RegRoot.HKCU, @"Control Panel\International\User Profile", "HttpAcceptLanguageOptOut",
            onValue: 1, offValue: 0, keywords: "language,語言,opt out"),

        // 3. Tailored experiences with diagnostic data.
        Tweak.RegToggle("privacy.tailored-experiences", "Tailored experiences", "量身打造嘅體驗",
            "Let Windows use diagnostic data for tailored tips and ads.", "畀 Windows 用診斷資料嚟提供量身建議同廣告。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled",
            onValue: 1, offValue: 0, keywords: "tailored,diagnostic,診斷,體驗"),

        // 4. Online speech recognition.
        Tweak.RegToggle("privacy.online-speech", "Online speech recognition", "線上語音辨識",
            "Send your voice to Microsoft for online speech recognition.", "將你嘅聲音傳去 Microsoft 做線上語音辨識。",
            RegRoot.HKCU, @"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted",
            onValue: 1, offValue: 0, keywords: "speech,voice,語音,聲音"),

        // 5. Diagnostic data / telemetry level (policy, HKLM).
        Tweak.RegChoice("privacy.telemetry-level", "Diagnostic data level", "診斷資料層級",
            "How much diagnostic and usage data Windows sends to Microsoft.", "Windows 傳幾多診斷同使用資料畀 Microsoft。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry",
            RegistryValueKind.DWord,
            new (string en, string zh, object value)[]
            {
                ("Security (Enterprise)", "安全（企業版）", 0),
                ("Required", "必要", 1),
                ("Optional", "選用", 3),
            },
            requiresAdmin: true, keywords: "telemetry,diagnostic,遙測,診斷"),

        // 6. Activity history — publish user activities (policy, HKLM).
        Tweak.RegToggle("privacy.activity-history", "Activity history", "活動記錄",
            "Let Windows collect and publish your activity history.", "畀 Windows 收集同發佈你嘅活動記錄。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities",
            onValue: 1, offValue: 0, requiresAdmin: true, keywords: "activity,timeline,活動,記錄"),

        // 7. Start / Settings suggested content.
        Tweak.RegToggle("privacy.start-suggestions", "Suggestions in Start", "開始功能表建議",
            "Show suggested content and apps in the Start menu.", "喺開始功能表度顯示建議內容同 App。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "start,suggestion,開始,建議"),

        // 8. Tips & suggestions (Soft Landing / welcome experience).
        Tweak.RegToggle("privacy.softlanding-tips", "Windows welcome tips", "Windows 歡迎提示",
            "Show tips and the welcome experience after updates.", "更新之後顯示提示同歡迎體驗。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled",
            onValue: 1, offValue: 0, keywords: "tips,welcome,提示,歡迎"),

        // 9. Get tips and suggestions when using Windows (notifications).
        Tweak.RegToggle("privacy.usage-tips", "Tips when using Windows", "使用 Windows 時嘅提示",
            "Get tips, tricks and suggestions while you use Windows.", "用 Windows 嗰陣收到提示、技巧同建議。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled",
            onValue: 1, offValue: 0, keywords: "tips,suggestion,提示,建議"),

        // 10. Suggested content in the Settings app.
        Tweak.RegToggle("privacy.settings-suggestions", "Suggested content in Settings", "設定內嘅建議內容",
            "Show suggested content inside the Settings app.", "喺設定 App 入面顯示建議內容。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353694Enabled",
            onValue: 1, offValue: 0, keywords: "settings,suggestion,設定,建議"),

        // 11. Feedback frequency — 0 = never; OFF deletes the cap.
        Tweak.RegToggle("privacy.feedback-frequency", "Never ask for feedback", "永不索取意見",
            "Stop Windows from asking for feedback.", "唔好畀 Windows 問你攞意見。",
            RegRoot.HKCU, @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod",
            onValue: 0, offValue: null, keywords: "feedback,siuf,意見,回饋"),

        // 12. App launch tracking for Start and search results.
        Tweak.RegToggle("privacy.app-launch-tracking", "App launch tracking", "App 啟動追蹤",
            "Let Windows track app launches to improve Start and search.", "畀 Windows 追蹤 App 啟動嚟改善開始同搜尋。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "track,launch,追蹤,啟動"),

        // 13. Location access for this device — String "Allow"/"Deny".
        Tweak.RegChoice("privacy.location-access", "Location access", "位置存取",
            "Whether apps on this device may use your location.", "呢部機嘅 App 可唔可以用你嘅位置。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value",
            RegistryValueKind.String,
            new (string en, string zh, object value)[]
            {
                ("Allow", "允許", "Allow"),
                ("Deny", "拒絕", "Deny"),
            },
            keywords: "location,gps,位置,定位"),

        // 14. Inking & typing personalisation (handwriting/typing dictionary) — multiple values.
        Tweak.CustomToggle("privacy.inking-typing", "Inking & typing personalisation", "手寫與輸入個人化",
            "Build a personal dictionary from your inking and typing.", "由你嘅手寫同輸入嚟建立個人字典。",
            getIsOn: () =>
                RegistryHelper.ValueEquals(RegRoot.HKCU,
                    @"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 0) &&
                RegistryHelper.ValueEquals(RegRoot.HKCU,
                    @"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 0),
            setIsOn: on =>
            {
                // RestrictImplicit* = 1 disables collection, 0 allows it.
                var restrict = on ? 0 : 1;
                var harvest = on ? 1 : 0;
                RegistryHelper.SetValue(RegRoot.HKCU,
                    @"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", restrict, RegistryValueKind.DWord);
                RegistryHelper.SetValue(RegRoot.HKCU,
                    @"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", restrict, RegistryValueKind.DWord);
                RegistryHelper.SetValue(RegRoot.HKCU,
                    @"Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", harvest, RegistryValueKind.DWord);
                RegistryHelper.SetValue(RegRoot.HKCU,
                    @"Software\Microsoft\Personalization\Settings", "AcceptedPrivacyPolicy", harvest, RegistryValueKind.DWord);
            },
            keywords: "inking,typing,handwriting,手寫,輸入,字典"),
    };
}