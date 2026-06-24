using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinForge.Services;

/// <summary>
/// 每個套件嘅安裝選項（UniGetUI 式）· Per-package install options (UniGetUI-style InstallationOptions).
/// 可序列化嘅 POCO，存落 SettingsStore（全域預設 + 每個套件覆寫）。
/// A serializable POCO persisted via SettingsStore: one GLOBAL default plus per-package overrides.
/// 一個冇覆寫嘅套件「跟全域」· A package with no override "follows global".
/// </summary>
public sealed class InstallOptions
{
    // ===== 自訂參數（每個操作各自一條）· Custom CLI args, one per operation =====
    public string CustomArgsInstall { get; set; } = "";
    public string CustomArgsUpdate { get; set; } = "";
    public string CustomArgsUninstall { get; set; } = "";

    // ===== 開關 · Boolean flags =====
    public bool RunAsAdministrator { get; set; }
    public bool Interactive { get; set; }
    public bool SkipHashCheck { get; set; }
    public bool PreRelease { get; set; }
    public bool RemoveDataOnUninstall { get; set; }
    public bool UninstallPreviousOnUpdate { get; set; }
    public bool SkipMinorUpdates { get; set; }
    public bool AutoUpdate { get; set; }

    // ===== 字串選項 · String options =====
    public string Scope { get; set; } = "";              // "", "user", "machine"
    public string Architecture { get; set; } = "";       // "", "x64", "x86", "arm64"
    public string Version { get; set; } = "";            // pinned version, "" = latest
    public string CustomInstallLocation { get; set; } = "";

    // ===== 前置／後置指令鈎 · Pre/post command hooks =====
    public string PreInstallCommand { get; set; } = "";
    public string PostInstallCommand { get; set; } = "";
    public string PreUpdateCommand { get; set; } = "";
    public string PostUpdateCommand { get; set; } = "";
    public string PreUninstallCommand { get; set; } = "";
    public string PostUninstallCommand { get; set; } = "";

    public bool AbortOnPreInstallFail { get; set; }
    public bool AbortOnPreUpdateFail { get; set; }
    public bool AbortOnPreUninstallFail { get; set; }

    // ===== 操作前關閉程序 · Kill processes before the operation =====
    public List<string> KillBeforeOperation { get; set; } = new();
    public bool ForceKill { get; set; }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const string GlobalKey = "pkg.opts.global";

    /// <summary>每個套件嘅儲存鍵 · The settings key for one package's override.</summary>
    private static string PkgKey(string managerKey, string id) => $"pkg.opts.{managerKey}|{id}";

    /// <summary>深層複製（避免共享 List）· Deep clone so callers never share the kill-list reference.</summary>
    public InstallOptions Clone()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, JsonOpts);
            var clone = JsonSerializer.Deserialize<InstallOptions>(json, JsonOpts);
            return clone ?? new InstallOptions();
        }
        catch { return new InstallOptions(); }
    }

    /// <summary>讀全域預設 · Load the GLOBAL defaults (sensible empty defaults on failure).</summary>
    public static InstallOptions LoadGlobal()
    {
        try
        {
            var raw = SettingsStore.Get(GlobalKey, "");
            if (string.IsNullOrWhiteSpace(raw)) return new InstallOptions();
            var o = JsonSerializer.Deserialize<InstallOptions>(raw, JsonOpts);
            return Normalize(o ?? new InstallOptions());
        }
        catch { return new InstallOptions(); }
    }

    /// <summary>寫全域預設 · Save the GLOBAL defaults.</summary>
    public static void SaveGlobal(InstallOptions opts)
    {
        try
        {
            var o = opts ?? new InstallOptions();
            SettingsStore.Set(GlobalKey, JsonSerializer.Serialize(o, JsonOpts));
        }
        catch { /* best effort */ }
    }

    /// <summary>
    /// 讀某套件嘅選項 · Load options for one package: the per-package override if present,
    /// otherwise a clone of the global defaults ("follows global").
    /// </summary>
    public static InstallOptions Load(string managerKey, string id)
    {
        try
        {
            var raw = SettingsStore.Get(PkgKey(managerKey, id), "");
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var o = JsonSerializer.Deserialize<InstallOptions>(raw, JsonOpts);
                if (o is not null) return Normalize(o);
            }
        }
        catch { /* fall through to global */ }
        return LoadGlobal();
    }

    /// <summary>有冇每個套件嘅覆寫 · Does this package have its own override (vs. follow-global)?</summary>
    public static bool HasOverride(string managerKey, string id)
    {
        try { return !string.IsNullOrWhiteSpace(SettingsStore.Get(PkgKey(managerKey, id), "")); }
        catch { return false; }
    }

    /// <summary>寫每個套件嘅覆寫 · Save a per-package override.</summary>
    public static void SaveForPackage(string managerKey, string id, InstallOptions opts)
    {
        try
        {
            var o = opts ?? new InstallOptions();
            SettingsStore.Set(PkgKey(managerKey, id), JsonSerializer.Serialize(o, JsonOpts));
        }
        catch { /* best effort */ }
    }

    /// <summary>刪除覆寫，令套件重新「跟全域」· Delete the override so the package follows global again.</summary>
    public static void ResetForPackage(string managerKey, string id)
    {
        try { SettingsStore.Set(PkgKey(managerKey, id), ""); }
        catch { /* best effort */ }
    }

    /// <summary>保證唔會有 null 集合 · Guarantee no null collections after deserialization.</summary>
    private static InstallOptions Normalize(InstallOptions o)
    {
        o.CustomArgsInstall ??= "";
        o.CustomArgsUpdate ??= "";
        o.CustomArgsUninstall ??= "";
        o.Scope ??= "";
        o.Architecture ??= "";
        o.Version ??= "";
        o.CustomInstallLocation ??= "";
        o.PreInstallCommand ??= "";
        o.PostInstallCommand ??= "";
        o.PreUpdateCommand ??= "";
        o.PostUpdateCommand ??= "";
        o.PreUninstallCommand ??= "";
        o.PostUninstallCommand ??= "";
        o.KillBeforeOperation ??= new List<string>();
        return o;
    }
}
