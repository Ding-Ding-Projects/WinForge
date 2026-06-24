using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 保安相關調校 · Security-related tweaks (UAC, SmartScreen, sign-in, firewall, Defender).
/// 唔會關閉 Defender 即時保護（Tamper Protection 會阻止，會係假調校）。
/// Never disables Defender real-time protection — Tamper Protection blocks it.
/// </summary>
public static class SecurityTweaks
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // UAC 提示行為改用 RadioGroup：三個嚴格度層級一眼睇晒，並加上保護狀態藥丸。
        // UAC behaviour as a RadioGroup so all three strictness levels are visible at once,
        // with a protected/at-risk status pill. Same HKLM DWord, same Id/values/admin scope.
        UacPromptRadio(),

        UacSecureDesktopToggle(),

        // SmartScreen（應用程式同檔案）係保護強度階梯，改用 RadioGroup 連狀態藥丸。
        // The apps/files SmartScreen level is a protection-strength ladder — RadioGroup + status pill.
        SmartScreenAppsRadio(),

        ProtectedToggle("security.smartscreen-edge", "SmartScreen for Store & web content", "商店同網頁內容 SmartScreen",
            "Toggle SmartScreen evaluation of Microsoft Store and web content.",
            "開關 SmartScreen 對 Microsoft Store 同網頁內容嘅檢查。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\AppHost",
            "EnableWebContentEvaluation", onValue: 1, offValue: 0,
            onProtects: true, keywords: "smartscreen,web content,網頁"),

        ProtectedToggle("security.require-cad", "Require Ctrl+Alt+Del at sign-in", "登入要按 Ctrl+Alt+Del",
            "Force the secure attention sequence before the sign-in screen.",
            "登入畫面之前強制要先撳安全序列鍵。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
            "DisableCAD", onValue: 0, offValue: 1,
            onProtects: true, requiresAdmin: true, restart: RestartScope.SignOut,
            keywords: "ctrl alt del,登入,sign-in"),

        ProtectedToggle("security.hide-last-user", "Hide last signed-in user name", "隱藏上次登入嘅使用者名稱",
            "Do not display the last account name on the sign-in screen.",
            "唔喺登入畫面顯示上次登入嗰個帳戶名。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
            "DontDisplayLastUserName", onValue: 1, offValue: 0,
            onProtects: true, requiresAdmin: true, restart: RestartScope.SignOut,
            keywords: "last user,登入,privacy"),

        // 開啟 RDP 會擴大攻擊面，所以「開」係風險狀態（黃），「熄」係中性。
        // Enabling RDP widens the attack surface, so "on" is the at-risk state (amber), "off" is neutral.
        RemoteDesktopToggle(),

        Tweak.Cmd("security.firewall-off", "Turn Windows Firewall off (all profiles)", "熄咗 Windows 防火牆（所有設定檔）",
            "Disables the firewall on every profile — leaves the PC exposed.",
            "喺所有設定檔熄咗防火牆，會令部機冇咗保護。",
            "Turn off", "熄咗佢", "netsh advfirewall set allprofiles state off",
            requiresAdmin: true, destructive: true, keywords: "firewall,防火牆,netsh"),

        Tweak.Cmd("security.firewall-on", "Turn Windows Firewall on (all profiles)", "開返 Windows 防火牆（所有設定檔）",
            "Re-enables the firewall on every profile.",
            "喺所有設定檔重新開返防火牆。",
            "Turn on", "開返佢", "netsh advfirewall set allprofiles state on",
            requiresAdmin: true, keywords: "firewall,防火牆,netsh"),

        Tweak.Cmd("security.open-security-app", "Open Windows Security", "開啟 Windows 安全性",
            "Launches the Windows Security app to review protection status.",
            "開啟 Windows 安全性 App 睇下保護狀態。",
            "Open", "開啟", "start windowsdefender:",
            keywords: "windows security,defender,安全性"),

        Tweak.Cmd("security.bitlocker-status", "Show BitLocker status", "顯示 BitLocker 狀態",
            "Reports the encryption status of every drive.",
            "報告每個磁碟機嘅加密狀態。",
            "Check", "查詢", "manage-bde -status",
            requiresAdmin: true, keywords: "bitlocker,encryption,加密"),

        Tweak.Powershell("security.defender-exclude-downloads", "Exclude Downloads from Defender scans", "將 Downloads 排除喺 Defender 掃描之外",
            "Adds the Downloads folder to Microsoft Defender's exclusion list.",
            "將 Downloads 資料夾加入 Microsoft Defender 嘅排除清單。",
            "Add", "新增",
            "Add-MpPreference -ExclusionPath \"$env:USERPROFILE\\Downloads\"",
            requiresAdmin: true, keywords: "defender,exclusion,排除,downloads"),

        Tweak.Powershell("security.defender-quick-scan", "Run a quick Defender scan", "行一次 Defender 快速掃描",
            "Starts a Microsoft Defender quick antivirus scan.",
            "開始一次 Microsoft Defender 快速防毒掃描。",
            "Scan", "掃描",
            "Start-MpScan -ScanType QuickScan",
            requiresAdmin: true, keywords: "defender,scan,掃描,antivirus"),

        Tweak.Powershell("security.defender-update", "Update Defender definitions", "更新 Defender 病毒定義",
            "Downloads the latest Microsoft Defender security intelligence updates.",
            "下載最新嘅 Microsoft Defender 安全情報更新。",
            "Update", "更新",
            "Update-MpSignature",
            requiresAdmin: true, keywords: "defender,signature,定義,update"),
    };

    // ======================================================================
    //  進階呈現工廠 · Richer-presentation factories.
    //  全部保持原本 Id／登錄檔路徑／值／admin／restart 範圍完全一致，只係換控件 + 加狀態藥丸。
    //  Each keeps the original Id / registry path / values / admin / restart scope EXACTLY;
    //  only the control surface changes and a coloured status pill is added.
    // ======================================================================

    private const string PoliciesSystem = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";

    /// <summary>UAC 提示行為（RadioGroup）· UAC prompt behaviour as a RadioButtons group + status pill.</summary>
    private static TweakDefinition UacPromptRadio()
    {
        const string Name = "ConsentPromptBehaviorAdmin";
        // 值同原本 RegChoice 一字不變：0 = 唔通知、5 = 預設、2 = 次次都通知。
        var options = new (string en, string zh, int value)[]
        {
            ("Never notify", "唔通知", 0),
            ("Default", "預設", 5),
            ("Always notify", "次次都通知", 2),
        };
        return new TweakDefinition
        {
            Id = "security.uac-prompt",
            Title = new("UAC prompt behaviour", "UAC 提示行為"),
            Description = new("Choose how strict the User Account Control elevation prompt is.",
                "揀 UAC 提權提示有幾嚴格。"),
            Kind = TweakKind.RadioGroup,
            RequiresAdmin = true,
            Keywords = new[] { "uac", "使用者帳戶控制", "consent" },
            Choices = ChoicesFromInts(options),
            GetCurrentChoice = () => CurrentIntChoice(RegRoot.HKLM, PoliciesSystem, Name, options),
            SetChoice = val => SetIntChoice(RegRoot.HKLM, PoliciesSystem, Name, options, val),
            ColoredStatus = () => (int.TryParse(
                    CurrentIntChoice(RegRoot.HKLM, PoliciesSystem, Name, options), out var v) ? v : 5) switch
            {
                0 => ("At risk · never notifies", "有風險 · 從不通知", StatusColor.Bad),
                2 => ("Strictest", "最嚴格", StatusColor.Good),
                _ => ("Default", "預設", StatusColor.Neutral),
            },
        };
    }

    /// <summary>UAC 安全桌面（Toggle + 狀態藥丸）· Secure-desktop dimming toggle with a protected/at-risk pill.</summary>
    private static TweakDefinition UacSecureDesktopToggle()
        => ProtectedToggle("security.uac-secure-desktop", "Dim the desktop for UAC", "UAC 時將桌面變暗",
            "Show the UAC prompt on the secure desktop (dims the screen).",
            "喺安全桌面顯示 UAC 提示，會將成個畫面變暗。",
            RegRoot.HKLM, PoliciesSystem,
            "PromptOnSecureDesktop", onValue: 1, offValue: 0,
            onProtects: true, requiresAdmin: true, keywords: "uac,secure desktop,安全桌面");

    /// <summary>應用程式／檔案 SmartScreen（RadioGroup）· Apps &amp; files SmartScreen as a RadioButtons group + pill.</summary>
    private static TweakDefinition SmartScreenAppsRadio()
    {
        const string Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer";
        const string Name = "SmartScreenEnabled";
        // 字串值同原本 RegChoice 一字不變。 String values identical to the original RegChoice.
        var options = new (string en, string zh, string value)[]
        {
            ("Warn", "警告", "Warn"),
            ("Prompt (admin)", "提示", "Prompt"),
            ("Off", "熄", "Off"),
        };
        return new TweakDefinition
        {
            Id = "security.smartscreen-apps",
            Title = new("SmartScreen for apps & files", "應用程式同檔案 SmartScreen"),
            Description = new("Control SmartScreen checking of downloaded apps and files.",
                "控制 SmartScreen 點樣檢查下載返嚟嘅應用程式同檔案。"),
            Kind = TweakKind.RadioGroup,
            RequiresAdmin = true,
            Keywords = new[] { "smartscreen", "智慧型畫面" },
            Choices = ChoicesFromStrings(options),
            GetCurrentChoice = () => CurrentStringChoice(RegRoot.HKLM, Path, Name, options),
            SetChoice = val => SetStringChoice(RegRoot.HKLM, Path, Name, options, val),
            ColoredStatus = () => CurrentStringChoice(RegRoot.HKLM, Path, Name, options) switch
            {
                "Off" => ("At risk · off", "有風險 · 已關", StatusColor.Bad),
                "Warn" => ("Protected", "受保護", StatusColor.Good),
                "Prompt" => ("Protected", "受保護", StatusColor.Good),
                _ => ("Unknown", "未知", StatusColor.Neutral),
            },
        };
    }

    /// <summary>開啟遠端桌面（Toggle + 風險藥丸）· Remote Desktop toggle; "on" widens attack surface ⇒ amber.</summary>
    private static TweakDefinition RemoteDesktopToggle()
    {
        const string Path = @"SYSTEM\CurrentControlSet\Control\Terminal Server";
        const string Name = "fDenyTSConnections";
        // 行為同原本 RegToggle 一字不變：on ⇒ 0（允許）、off ⇒ 1（拒絕）。
        // Behaviour identical to the original RegToggle: on ⇒ 0 (allow), off ⇒ 1 (deny), DWord.
        bool IsOn() => RegistryHelper.ValueEquals(RegRoot.HKLM, Path, Name, 0);
        return new TweakDefinition
        {
            Id = "security.remote-desktop",
            Title = new("Enable Remote Desktop", "開啟遠端桌面"),
            Description = new("Allow incoming Remote Desktop (RDP) connections to this PC.",
                "允許其他電腦透過遠端桌面（RDP）連入嚟呢部機。"),
            Kind = TweakKind.Toggle,
            RequiresAdmin = true,
            Restart = RestartScope.Reboot,
            Keywords = new[] { "rdp", "remote desktop", "遠端" },
            GetIsOn = IsOn,
            SetIsOn = on => RegistryHelper.SetValue(RegRoot.HKLM, Path, Name,
                on ? 0 : 1, RegistryValueKind.DWord),
            ColoredStatus = () => IsOn()
                ? ("Exposed · RDP open", "暴露 · RDP 已開", StatusColor.Warn)
                : ("Closed", "已關閉", StatusColor.Neutral),
        };
    }

    /// <summary>
    /// 帶保護狀態藥丸嘅開關 · A registry-backed toggle (identical semantics to <see cref="Tweak.RegToggle"/>)
    /// 加埋「受保護／有風險」彩色狀態。with an added protected/at-risk coloured pill.
    /// <paramref name="onProtects"/> 表示「開」係安全狀態 · true ⇒ "on" is the protected state.
    /// </summary>
    private static TweakDefinition ProtectedToggle(
        string id, string enT, string zhT, string enD, string zhD,
        RegRoot root, string path, string name, object onValue, object? offValue,
        bool onProtects, bool requiresAdmin = false, RestartScope restart = RestartScope.None,
        RegistryValueKind kind = RegistryValueKind.DWord, string? keywords = null)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.Toggle,
            RequiresAdmin = requiresAdmin,
            Restart = restart,
            Keywords = string.IsNullOrWhiteSpace(keywords)
                ? Array.Empty<string>()
                : keywords.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            // get/set 行為同 Tweak.RegToggle 完全一致 · behaviour mirrors Tweak.RegToggle exactly
            // (offValue is null ⇒ delete the value when turned off).
            GetIsOn = () => RegistryHelper.ValueEquals(root, path, name, onValue),
            SetIsOn = on =>
            {
                if (on) RegistryHelper.SetValue(root, path, name, onValue, kind);
                else if (offValue is null) RegistryHelper.DeleteValue(root, path, name);
                else RegistryHelper.SetValue(root, path, name, offValue, kind);
            },
            ColoredStatus = () =>
            {
                bool on = RegistryHelper.ValueEquals(root, path, name, onValue);
                bool safe = on == onProtects;
                return safe
                    ? ("Protected", "受保護", StatusColor.Good)
                    : ("At risk", "有風險", StatusColor.Warn);
            },
        };

    // ---- 共用揀選輔助 · shared choice helpers (mirror Tweak.RegChoice read/write exactly) ----

    private static List<TweakChoice> ChoicesFromInts((string en, string zh, int value)[] options)
    {
        var list = new List<TweakChoice>();
        foreach (var o in options) list.Add(new TweakChoice(new LocalizedText(o.en, o.zh), o.value.ToString()));
        return list;
    }

    private static List<TweakChoice> ChoicesFromStrings((string en, string zh, string value)[] options)
    {
        var list = new List<TweakChoice>();
        foreach (var o in options) list.Add(new TweakChoice(new LocalizedText(o.en, o.zh), o.value));
        return list;
    }

    private static string? CurrentIntChoice(RegRoot root, string path, string name, (string en, string zh, int value)[] options)
    {
        foreach (var o in options)
            if (RegistryHelper.ValueEquals(root, path, name, o.value)) return o.value.ToString();
        return null;
    }

    private static void SetIntChoice(RegRoot root, string path, string name, (string en, string zh, int value)[] options, string val)
    {
        foreach (var o in options)
            if (string.Equals(o.value.ToString(), val, StringComparison.OrdinalIgnoreCase))
            {
                RegistryHelper.SetValue(root, path, name, o.value, RegistryValueKind.DWord);
                return;
            }
    }

    private static string? CurrentStringChoice(RegRoot root, string path, string name, (string en, string zh, string value)[] options)
    {
        foreach (var o in options)
            if (RegistryHelper.ValueEquals(root, path, name, o.value)) return o.value;
        return null;
    }

    private static void SetStringChoice(RegRoot root, string path, string name, (string en, string zh, string value)[] options, string val)
    {
        foreach (var o in options)
            if (string.Equals(o.value, val, StringComparison.OrdinalIgnoreCase))
            {
                RegistryHelper.SetValue(root, path, name, o.value, RegistryValueKind.String);
                return;
            }
    }
}