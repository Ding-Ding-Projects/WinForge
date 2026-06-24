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
    // ──────────────────────────────────────────────────────────────────────
    //  彩色狀態藥丸幫手 · Coloured-status-pill helpers
    //
    //  ColoredStatus 係 TweakDefinition 上嘅 init-only 成員，工廠 (RegToggle…)
    //  整完先冇得加；TweakDefinition 又係 sealed class（唔係 record）所以冇 `with`。
    //  所以呢度用一個小幫手：攞工廠整好嘅 Toggle 定義，原封不動咁複製返佢嘅
    //  Id／行為／權限／重啟範圍，淨係 overlay 一個 ColoredStatus。Id、GetIsOn、
    //  SetIsOn 全部保持一模一樣 —— 只係加個顯示用嘅藥丸。
    //
    //  ColoredStatus is init-only and TweakDefinition is a sealed class (no `with`),
    //  so we copy the factory-built toggle's Id/behaviour/admin/restart verbatim and
    //  only overlay a presentation-only status pill. Behaviour is untouched.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>受保護（綠）· "Protected" green pill.</summary>
    private static (string, string, StatusColor) Protected => ("Protected", "受保護", StatusColor.Good);

    /// <summary>外露（紅）· "Exposed" red pill.</summary>
    private static (string, string, StatusColor) Exposed => ("Exposed", "外露", StatusColor.Bad);

    /// <summary>
    /// 為一個 Toggle 定義加上 protected／exposed 藥丸 · Overlay a protected/exposed pill on a toggle.
    /// <paramref name="onMeansPrivate"/> = true 代表「開」即係更私隱（受保護）；false 代表「開」即係外露。
    /// onMeansPrivate=true ⇒ ON is the private/protected state; false ⇒ ON exposes you.
    /// 純粹複製工廠定義並加藥丸，唔改 Id／行為 · Copies the factory toggle and adds the pill only.
    /// </summary>
    private static TweakDefinition WithPrivacyPill(TweakDefinition t, bool onMeansPrivate)
    {
        var getIsOn = t.GetIsOn;
        return new TweakDefinition
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            Kind = t.Kind,
            RequiresAdmin = t.RequiresAdmin,
            Destructive = t.Destructive,
            Restart = t.Restart,
            Keywords = t.Keywords,
            GetIsOn = t.GetIsOn,
            SetIsOn = t.SetIsOn,
            ColoredStatus = () =>
            {
                bool on;
                try { on = getIsOn?.Invoke() ?? false; }
                catch { return ("Unknown", "未知", StatusColor.Neutral); }
                // 私隱狀態 = (開 == onMeansPrivate) · private when ON matches the "private" polarity.
                return on == onMeansPrivate ? Protected : Exposed;
            },
        };
    }

    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // 1. Advertising ID — Enabled=1 means personalised ads ON; turning the switch OFF (=0) stops ad tracking.
        WithPrivacyPill(Tweak.RegToggle("privacy.advertising-id", "Personalised ads (advertising ID)", "個人化廣告（廣告識別碼）",
            "Let apps use your advertising ID to show personalised ads.", "畀啲 App 用你嘅廣告識別碼嚟顯示個人化廣告。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled",
            onValue: 1, offValue: 0, keywords: "advertising,ad,廣告,追蹤"), onMeansPrivate: false),

        // 2. Let websites access language list — opt-out value; ON(=1) means opted out (more private).
        WithPrivacyPill(Tweak.RegToggle("privacy.language-list-optout", "Block websites reading my language list", "阻止網站讀取語言清單",
            "Stop websites from accessing your language list to track you.", "阻止網站讀取你嘅語言清單嚟追蹤你。",
            RegRoot.HKCU, @"Control Panel\International\User Profile", "HttpAcceptLanguageOptOut",
            onValue: 1, offValue: 0, keywords: "language,語言,opt out"), onMeansPrivate: true),

        // 3. Tailored experiences with diagnostic data.
        WithPrivacyPill(Tweak.RegToggle("privacy.tailored-experiences", "Tailored experiences", "量身打造嘅體驗",
            "Let Windows use diagnostic data for tailored tips and ads.", "畀 Windows 用診斷資料嚟提供量身建議同廣告。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled",
            onValue: 1, offValue: 0, keywords: "tailored,diagnostic,診斷,體驗"), onMeansPrivate: false),

        // 4. Online speech recognition.
        WithPrivacyPill(Tweak.RegToggle("privacy.online-speech", "Online speech recognition", "線上語音辨識",
            "Send your voice to Microsoft for online speech recognition.", "將你嘅聲音傳去 Microsoft 做線上語音辨識。",
            RegRoot.HKCU, @"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted",
            onValue: 1, offValue: 0, keywords: "speech,voice,語音,聲音"), onMeansPrivate: false),

        // 5. Diagnostic data / telemetry level (policy, HKLM).
        //    只得三個層級（Security／Required／Optional），用單選按鈕組比下拉選單清晰。
        //    Three levels only → RadioGroup reads better than a ComboBox. Same Id, same registry
        //    value (AllowTelemetry DWord 0/1/3), same admin scope as the previous RegChoice — only
        //    presentation changes. A coloured pill shows how exposed the level is.
        TelemetryRadio(),

        // 6. Activity history — publish user activities (policy, HKLM).
        WithPrivacyPill(Tweak.RegToggle("privacy.activity-history", "Activity history", "活動記錄",
            "Let Windows collect and publish your activity history.", "畀 Windows 收集同發佈你嘅活動記錄。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities",
            onValue: 1, offValue: 0, requiresAdmin: true, keywords: "activity,timeline,活動,記錄"), onMeansPrivate: false),

        // 7. Start / Settings suggested content.
        WithPrivacyPill(Tweak.RegToggle("privacy.start-suggestions", "Suggestions in Start", "開始功能表建議",
            "Show suggested content and apps in the Start menu.", "喺開始功能表度顯示建議內容同 App。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "start,suggestion,開始,建議"), onMeansPrivate: false),

        // 8. Tips & suggestions (Soft Landing / welcome experience).
        WithPrivacyPill(Tweak.RegToggle("privacy.softlanding-tips", "Windows welcome tips", "Windows 歡迎提示",
            "Show tips and the welcome experience after updates.", "更新之後顯示提示同歡迎體驗。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled",
            onValue: 1, offValue: 0, keywords: "tips,welcome,提示,歡迎"), onMeansPrivate: false),

        // 9. Get tips and suggestions when using Windows (notifications).
        WithPrivacyPill(Tweak.RegToggle("privacy.usage-tips", "Tips when using Windows", "使用 Windows 時嘅提示",
            "Get tips, tricks and suggestions while you use Windows.", "用 Windows 嗰陣收到提示、技巧同建議。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled",
            onValue: 1, offValue: 0, keywords: "tips,suggestion,提示,建議"), onMeansPrivate: false),

        // 10. Suggested content in the Settings app.
        WithPrivacyPill(Tweak.RegToggle("privacy.settings-suggestions", "Suggested content in Settings", "設定內嘅建議內容",
            "Show suggested content inside the Settings app.", "喺設定 App 入面顯示建議內容。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353694Enabled",
            onValue: 1, offValue: 0, keywords: "settings,suggestion,設定,建議"), onMeansPrivate: false),

        // 11. Feedback frequency — 0 = never; OFF deletes the cap.
        //     "Never ask" 嘅開關：ON(=0) 代表唔再被問，所以開 = 受保護。
        //     This switch is phrased as "Never ask", so ON is the private state.
        WithPrivacyPill(Tweak.RegToggle("privacy.feedback-frequency", "Never ask for feedback", "永不索取意見",
            "Stop Windows from asking for feedback.", "唔好畀 Windows 問你攞意見。",
            RegRoot.HKCU, @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod",
            onValue: 0, offValue: null, keywords: "feedback,siuf,意見,回饋"), onMeansPrivate: true),

        // 12. App launch tracking for Start and search results.
        WithPrivacyPill(Tweak.RegToggle("privacy.app-launch-tracking", "App launch tracking", "App 啟動追蹤",
            "Let Windows track app launches to improve Start and search.", "畀 Windows 追蹤 App 啟動嚟改善開始同搜尋。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "track,launch,追蹤,啟動"), onMeansPrivate: false),

        // 13. Location access for this device — String "Allow"/"Deny".
        //     Allow／Deny 係清晰嘅二選一，用單選按鈕＋protected／exposed 藥丸更易讀。
        //     A clean binary → RadioGroup + pill. Same Id, same string "Value" (Allow/Deny).
        LocationRadio(),

        // 14. Inking & typing personalisation (handwriting/typing dictionary) — multiple values.
        WithPrivacyPill(Tweak.CustomToggle("privacy.inking-typing", "Inking & typing personalisation", "手寫與輸入個人化",
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
            keywords: "inking,typing,handwriting,手寫,輸入,字典"), onMeansPrivate: false),
    };

    // ──────────────────────────────────────────────────────────────────────
    //  遙測層級單選按鈕組 · Telemetry-level RadioGroup
    //
    //  由原本嘅 RegChoice 升級為 RadioGroup（同一個 Id、同一個 registry 值、同一個
    //  admin scope）。三個層級用單選按鈕比下拉清晰；另加彩色藥丸顯示外露程度。
    //  Upgraded from RegChoice to RadioGroup: identical Id, identical AllowTelemetry DWord
    //  (0/1/3), identical admin requirement — only the control surface differs. A coloured
    //  pill reflects how much data each level exposes.
    // ──────────────────────────────────────────────────────────────────────
    private static TweakDefinition TelemetryRadio()
    {
        const RegRoot Root = RegRoot.HKLM;
        const string Path = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection";
        const string Name = "AllowTelemetry";

        // 值用字串攜帶，寫入時轉返 int 以保持 DWord 型別（同 RegChoice 一致）。
        // Values are carried as strings but written back as int to preserve the DWord kind.
        var t = Tweak.RadioGroup("privacy.telemetry-level", "Diagnostic data level", "診斷資料層級",
            "How much diagnostic and usage data Windows sends to Microsoft.", "Windows 傳幾多診斷同使用資料畀 Microsoft。",
            new (string en, string zh, string value)[]
            {
                ("Security (Enterprise)", "安全（企業版）", "0"),
                ("Required", "必要", "1"),
                ("Optional", "選用", "3"),
            },
            getCurrent: () =>
            {
                foreach (var v in new[] { 0, 1, 3 })
                    if (RegistryHelper.ValueEquals(Root, Path, Name, v))
                        return v.ToString();
                return null;
            },
            setChoice: val =>
            {
                if (int.TryParse(val, out var n))
                    RegistryHelper.SetValue(Root, Path, Name, n, RegistryValueKind.DWord);
            },
            requiresAdmin: true, keywords: "telemetry,diagnostic,遙測,診斷");

        return new TweakDefinition
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            Kind = t.Kind,
            RequiresAdmin = t.RequiresAdmin,
            Destructive = t.Destructive,
            Restart = t.Restart,
            Keywords = t.Keywords,
            Choices = t.Choices,
            GetCurrentChoice = t.GetCurrentChoice,
            SetChoice = t.SetChoice,
            ColoredStatus = () =>
            {
                var cur = t.GetCurrentChoice?.Invoke();
                return cur switch
                {
                    "0" => ("Security only", "僅安全", StatusColor.Good),
                    "1" => ("Required only", "僅必要", StatusColor.Warn),
                    "3" => ("Optional (full)", "選用（完整）", StatusColor.Bad),
                    _ => ("Not set", "未設定", StatusColor.Neutral),
                };
            },
        };
    }

    // ──────────────────────────────────────────────────────────────────────
    //  位置存取單選按鈕組 · Location-access RadioGroup
    //
    //  由 RegChoice 升級為 RadioGroup（同一個 Id、同一個字串 "Value" = Allow/Deny）。
    //  Upgraded from RegChoice to RadioGroup: identical Id, identical string "Value"
    //  (Allow/Deny) at the same ConsentStore path. A pill shows protected vs exposed.
    // ──────────────────────────────────────────────────────────────────────
    private static TweakDefinition LocationRadio()
    {
        const RegRoot Root = RegRoot.HKCU;
        const string Path = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location";
        const string Name = "Value";

        var t = Tweak.RadioGroup("privacy.location-access", "Location access", "位置存取",
            "Whether apps on this device may use your location.", "呢部機嘅 App 可唔可以用你嘅位置。",
            new (string en, string zh, string value)[]
            {
                ("Allow", "允許", "Allow"),
                ("Deny", "拒絕", "Deny"),
            },
            getCurrent: () =>
            {
                foreach (var v in new[] { "Allow", "Deny" })
                    if (RegistryHelper.ValueEquals(Root, Path, Name, v))
                        return v;
                return null;
            },
            setChoice: val =>
            {
                if (val is "Allow" or "Deny")
                    RegistryHelper.SetValue(Root, Path, Name, val, RegistryValueKind.String);
            },
            keywords: "location,gps,位置,定位");

        return new TweakDefinition
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            Kind = t.Kind,
            RequiresAdmin = t.RequiresAdmin,
            Destructive = t.Destructive,
            Restart = t.Restart,
            Keywords = t.Keywords,
            Choices = t.Choices,
            GetCurrentChoice = t.GetCurrentChoice,
            SetChoice = t.SetChoice,
            ColoredStatus = () =>
            {
                var cur = t.GetCurrentChoice?.Invoke();
                return cur switch
                {
                    "Deny" => Protected,
                    "Allow" => Exposed,
                    _ => ("Not set", "未設定", StatusColor.Neutral),
                };
            },
        };
    }
}