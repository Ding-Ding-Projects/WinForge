using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// UniGetUI 式更新忽略／釘版資料庫 · UniGetUI-style granular update pinning database.
/// 每個釘版（Pin）可以係：釘住某個版本（之後更新版又會再提示）、釘住所有版本、
/// 或者一個會自動到期嘅暫停（PauseUntil）。背後用 <see cref="SettingsStore"/> 嘅 JSON 字串存喺
/// 新 key 'pkg.ignored.v2'；第一次載入會將舊 'pkg.ignored' 嘅項目搬入嚟做「所有版本」釘版。
/// Each pin is one of: skip-this-version (a newer version resumes updates), pin-all-versions,
/// or a time-limited snooze (PauseUntil) that auto-expires. Backed by a JSON string in
/// <see cref="SettingsStore"/> under the new key 'pkg.ignored.v2'; legacy 'pkg.ignored' newline
/// entries are migrated once into all-version pins. NEVER throws.
/// </summary>
public static class IgnoredUpdates
{
    /// <summary>
    /// 一個釘版項目 · One pin.
    /// <para><see cref="Version"/> == "*" 代表所有版本；具體版本字串代表只跳過嗰個版本。</para>
    /// <para><see cref="PauseUntil"/>（yyyy-MM-dd，可為 null）代表一個會自動到期嘅暫停。</para>
    /// Version=="*" means all versions; a concrete version string means skip only that version.
    /// PauseUntil (yyyy-MM-dd, nullable) is a time-limited snooze that auto-expires.
    /// </summary>
    public sealed record Pin(string Manager, string Id, string Version, string? PauseUntil);

    private const string KeyV2 = "pkg.ignored.v2";
    private const string KeyLegacy = "pkg.ignored";
    private const string DateFormat = "yyyy-MM-dd";

    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
    private static List<Pin>? _cache;

    /// <summary>確保已載入並做一次性遷移 · Ensure loaded + run one-time legacy migration.</summary>
    private static List<Pin> Ensure()
    {
        if (_cache is not null) return _cache;
        lock (Gate)
        {
            if (_cache is not null) return _cache;
            var list = new List<Pin>();
            try
            {
                var raw = SettingsStore.Get(KeyV2, "");
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    var parsed = JsonSerializer.Deserialize<List<Pin>>(raw);
                    if (parsed is not null)
                        foreach (var p in parsed)
                            if (p is not null && !string.IsNullOrEmpty(p.Id))
                                list.Add(NormalizePin(p));
                }
                else
                {
                    // 一次性遷移舊 'pkg.ignored'（每行 "manager|id"）做「所有版本」釘版。
                    // One-time migration of legacy 'pkg.ignored' newline "manager|id" entries.
                    var legacy = SettingsStore.Get(KeyLegacy, "");
                    foreach (var line in legacy.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = line.Split('|', 2);
                        var manager = parts.Length > 0 ? parts[0].Trim() : "";
                        var id = parts.Length > 1 ? parts[1].Trim() : line.Trim();
                        if (id.Length == 0) continue;
                        if (!list.Any(x => SameKey(x, manager, id, "*")))
                            list.Add(new Pin(manager, id, "*", null));
                    }
                    _cache = list;
                    SaveLocked(); // 持久化遷移結果（即使係空）· persist the migrated database.
                    return _cache;
                }
            }
            catch { list = new List<Pin>(); }
            _cache = list;
            return _cache;
        }
    }

    private static Pin NormalizePin(Pin p)
    {
        var manager = p.Manager ?? "";
        var id = p.Id ?? "";
        var version = string.IsNullOrEmpty(p.Version) ? "*" : p.Version;
        var pause = string.IsNullOrWhiteSpace(p.PauseUntil) ? null : p.PauseUntil.Trim();
        return new Pin(manager, id, version, pause);
    }

    private static void SaveLocked()
    {
        try { SettingsStore.Set(KeyV2, JsonSerializer.Serialize(_cache ?? new List<Pin>(), JsonOpts)); }
        catch { /* best effort */ }
    }

    private static bool SameKey(Pin p, string manager, string id, string version)
        => string.Equals(p.Manager, manager, StringComparison.OrdinalIgnoreCase)
        && string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)
        && string.Equals(p.Version, version, StringComparison.OrdinalIgnoreCase);

    /// <summary>釘版是否已到期（過咗 PauseUntil）· True if this snooze has expired.</summary>
    private static bool IsExpired(Pin p, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(p.PauseUntil)) return false;
        if (DateTime.TryParseExact(p.PauseUntil, DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var until))
            return until.Date < now.Date; // 到期日當日仍然有效 · still active on the expiry day itself.
        return false; // 解析唔到就當佢未到期，唔好誤刪 · unparseable -> keep it.
    }

    /// <summary>剷走已到期暫停並持久化 · Prune expired snoozes and persist if anything changed.</summary>
    private static void Prune()
    {
        lock (Gate)
        {
            var list = Ensure();
            var now = DateTime.Now;
            int before = list.Count;
            list.RemoveAll(p => IsExpired(p, now));
            if (list.Count != before) SaveLocked();
        }
    }

    private static (string manager, string id, string version) Effective(PackageItem item)
    {
        var version = !string.IsNullOrWhiteSpace(item.AvailableVersion)
            ? item.AvailableVersion.Trim()
            : (item.Version ?? "").Trim();
        return (item.ManagerKey ?? "", item.Id ?? "", version);
    }

    // ===== Public API =====

    /// <summary>
    /// 該套件嘅更新是否被忽略 · True if any matching pin covers this update.
    /// 條件：Manager+Id 相符，且 Pin.Version 係 "*" 或等於 item.AvailableVersion（提供緊嘅版本），
    /// 而且 PauseUntil 係 null 或仲未到期。讀取時順手剷走過期暫停。
    /// </summary>
    public static bool IsIgnored(PackageItem item)
    {
        try
        {
            Prune();
            var (manager, id, offered) = Effective(item);
            lock (Gate)
            {
                var list = Ensure();
                var now = DateTime.Now;
                foreach (var p in list)
                {
                    if (!string.Equals(p.Manager, manager, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) continue;
                    if (IsExpired(p, now)) continue;
                    bool versionMatch = p.Version == "*"
                        || string.Equals(p.Version, offered, StringComparison.OrdinalIgnoreCase);
                    if (versionMatch) return true;
                }
            }
        }
        catch { /* never throw */ }
        return false;
    }

    /// <summary>釘住所有版本 · Pin every version (permanent ignore).</summary>
    public static void PinAllVersions(PackageItem item)
        => Upsert(item.ManagerKey ?? "", item.Id ?? "", "*", null);

    /// <summary>只跳過提供緊嘅版本（後續更版會再提示）· Skip only this offered version.</summary>
    public static void PinThisVersion(PackageItem item)
    {
        var version = !string.IsNullOrWhiteSpace(item.AvailableVersion)
            ? item.AvailableVersion.Trim()
            : (item.Version ?? "").Trim();
        if (string.IsNullOrEmpty(version)) version = "*"; // 冇版本可釘就退而求其次釘全部 · fall back to all.
        Upsert(item.ManagerKey ?? "", item.Id ?? "", version, null);
    }

    /// <summary>暫停一段時間後自動恢復 · Snooze updates; auto-resumes after the duration.</summary>
    public static void Snooze(PackageItem item, TimeSpan duration)
    {
        var until = DateTime.Now.Add(duration).ToString(DateFormat, CultureInfo.InvariantCulture);
        Upsert(item.ManagerKey ?? "", item.Id ?? "", "*", until);
    }

    /// <summary>移除呢個套件嘅所有釘版 · Remove all pins for this package (any version).</summary>
    public static void Remove(PackageItem item)
    {
        try
        {
            lock (Gate)
            {
                var list = Ensure();
                int before = list.Count;
                list.RemoveAll(p =>
                    string.Equals(p.Manager, item.ManagerKey ?? "", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.Id, item.Id ?? "", StringComparison.OrdinalIgnoreCase));
                if (list.Count != before) SaveLocked();
            }
        }
        catch { /* never throw */ }
    }

    /// <summary>按 manager|id|version 精確移除一個釘版 · Remove one exact pin.</summary>
    public static void RemoveKey(string manager, string id, string version)
    {
        try
        {
            lock (Gate)
            {
                var list = Ensure();
                int before = list.Count;
                list.RemoveAll(p => SameKey(p, manager ?? "", id ?? "", version ?? "*"));
                if (list.Count != before) SaveLocked();
            }
        }
        catch { /* never throw */ }
    }

    /// <summary>清空全部釘版 · Clear every pin.</summary>
    public static void ResetAll()
    {
        try
        {
            lock (Gate)
            {
                _cache = new List<Pin>();
                SaveLocked();
            }
        }
        catch { /* never throw */ }
    }

    /// <summary>所有目前生效嘅釘版（讀取前順手剷走過期暫停）· All current pins (prunes expired first).</summary>
    public static List<Pin> All()
    {
        try
        {
            Prune();
            lock (Gate) { return new List<Pin>(Ensure()); }
        }
        catch { return new List<Pin>(); }
    }

    /// <summary>新增或就地更新一個釘版（同一 manager|id|version 視為同一項）· Insert or replace a pin.</summary>
    private static void Upsert(string manager, string id, string version, string? pauseUntil)
    {
        try
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (Gate)
            {
                var list = Ensure();
                // 同一套件改用新釘版時，先清走舊釘版避免重複／矛盾。
                // Replacing a pin for the same package: drop prior pins for that manager|id.
                list.RemoveAll(p =>
                    string.Equals(p.Manager, manager, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
                list.Add(new Pin(manager, id, string.IsNullOrEmpty(version) ? "*" : version, pauseUntil));
                SaveLocked();
            }
        }
        catch { /* never throw */ }
    }
}
