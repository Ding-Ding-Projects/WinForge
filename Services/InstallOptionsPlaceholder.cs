// ============================================================================
//  InstallOptions 佔位類別 · InstallOptions PLACEHOLDER
// ----------------------------------------------------------------------------
//  呢個檔案只係喺「安裝選項核心」(Services/InstallOptions.cs，由 install-options-core
//  套件交付) 仲未出現時，提供一個最小可用嘅 `InstallOptions`，等 BundleService 同
//  套件清單工作區可以照樣編譯。一旦真正嘅 InstallOptions 出現，喺專案定義
//  WINFORGE_HAS_INSTALLOPTIONS（或者真檔案自己定義），呢個佔位就會自動讓位，
//  唔會撞名。永遠優先用真嘅 InstallOptions。
//
//  This file provides a MINIMAL stand-in `InstallOptions` ONLY while the real
//  install-options-core type (Services/InstallOptions.cs) is not yet present in
//  this worktree, so BundleService and the bundle workspace still compile. When
//  the real InstallOptions lands, define the WINFORGE_HAS_INSTALLOPTIONS symbol
//  (the real file is expected to define it) and this placeholder steps aside so
//  there is never a duplicate-type clash. Always prefer the real type.
//
//  The shape mirrors the dangerous-field surface the bundle security inspector
//  needs: custom CLI parameters (install/update/uninstall), pre/post commands,
//  and the kill-before-operation list. Everything is optional and defensive.
// ============================================================================
#if !WINFORGE_HAS_INSTALLOPTIONS

using System.Collections.Generic;

namespace WinForge.Services;

/// <summary>
/// 安裝選項（佔位）· Install options (placeholder). Carries the per-package overrides a
/// bundle may attach. Only the security-relevant fields are modelled in full; the rest
/// are simple optional values that round-trip through JSON/YAML/XML untouched.
/// </summary>
public sealed class InstallOptions
{
    // ---- 簡單純量選項 · Simple scalar options ----
    public bool SkipHashCheck { get; set; }
    public bool InteractiveInstallation { get; set; }
    public bool RunAsAdministrator { get; set; }
    public bool PreRelease { get; set; }
    public string Architecture { get; set; } = "";
    public string InstallationScope { get; set; } = "";
    public string CustomInstallLocation { get; set; } = "";
    public string Version { get; set; } = "";

    // ---- 危險欄位 · Dangerous fields (the security inspector flags these) ----

    /// <summary>自訂安裝 CLI 參數 · Custom install-time CLI arguments.</summary>
    public List<string> CustomParameters_Install { get; set; } = new();

    /// <summary>自訂更新 CLI 參數 · Custom update-time CLI arguments.</summary>
    public List<string> CustomParameters_Update { get; set; } = new();

    /// <summary>自訂解除安裝 CLI 參數 · Custom uninstall-time CLI arguments.</summary>
    public List<string> CustomParameters_Uninstall { get; set; } = new();

    /// <summary>安裝前指令 · Command run before install.</summary>
    public string PreInstallCommand { get; set; } = "";

    /// <summary>安裝後指令 · Command run after install.</summary>
    public string PostInstallCommand { get; set; } = "";

    /// <summary>更新前指令 · Command run before update.</summary>
    public string PreUpdateCommand { get; set; } = "";

    /// <summary>更新後指令 · Command run after update.</summary>
    public string PostUpdateCommand { get; set; } = "";

    /// <summary>解除安裝前指令 · Command run before uninstall.</summary>
    public string PreUninstallCommand { get; set; } = "";

    /// <summary>解除安裝後指令 · Command run after uninstall.</summary>
    public string PostUninstallCommand { get; set; } = "";

    /// <summary>操作前要終止嘅程序清單 · Processes to kill before the operation.</summary>
    public List<string> KillBeforeOperation { get; set; } = new();

    /// <summary>深複製 · Deep copy.</summary>
    public InstallOptions Copy() => new()
    {
        SkipHashCheck = SkipHashCheck,
        InteractiveInstallation = InteractiveInstallation,
        RunAsAdministrator = RunAsAdministrator,
        PreRelease = PreRelease,
        Architecture = Architecture,
        InstallationScope = InstallationScope,
        CustomInstallLocation = CustomInstallLocation,
        Version = Version,
        CustomParameters_Install = new List<string>(CustomParameters_Install),
        CustomParameters_Update = new List<string>(CustomParameters_Update),
        CustomParameters_Uninstall = new List<string>(CustomParameters_Uninstall),
        PreInstallCommand = PreInstallCommand,
        PostInstallCommand = PostInstallCommand,
        PreUpdateCommand = PreUpdateCommand,
        PostUpdateCommand = PostUpdateCommand,
        PreUninstallCommand = PreUninstallCommand,
        PostUninstallCommand = PostUninstallCommand,
        KillBeforeOperation = new List<string>(KillBeforeOperation),
    };
}

#endif
