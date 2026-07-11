using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 背景套件更新排程器 · Background package-update scheduler (UniGetUI-style).
/// 一條 <see cref="PeriodicTimer"/> 背景任務，按 <see cref="PackageManagerSettings.CheckIntervalMinutes"/>
/// 定期檢查更新（當 AutoCheckEnabled 開咗）。每次 tick：
///   1) 跨管理器收集更新（<see cref="PackageManagerRegistry.AllUpdatesAsync"/>）
///   2) 套用「最少更新年齡」過濾（盡力而為；唔知日期就唔扣起）
///   3) winget 安裝程式來源主機改變檢查（盡力而為，標記警告旗標）
///   4) 過濾已忽略更新，再更新系統匣數量 + 發「有更新」通知
///   5) 若全域自動更新或逐套件 AutoUpdate 開咗，而且冇被流量／電池／慳電閘住，交畀全域操作隊列安裝
///   6) 每日寫一次本地備份（已安裝清單）
/// 所有嘢都包住，永遠唔會擲返去 UI 執行緒。
///
/// A single <see cref="PeriodicTimer"/>-driven background task that honours
/// <see cref="PackageManagerSettings.CheckIntervalMinutes"/> when AutoCheckEnabled. Each tick collects
/// updates, applies the minimum-update-age filter and the winget installer-host-change check (both
/// best-effort), filters ignored updates before updating the tray count, fires the "updates available"
/// toast, optionally auto-installs globally enabled or per-package enabled updates through the shared
/// operation coordinator (respecting metered/battery/battery-saver gates), and runs the optional daily
/// local backup. Everything is wrapped — it never throws onto the UI thread.
/// </summary>
public static class PackageUpdateScheduler
{
    private static readonly object Gate = new();
    private static CancellationTokenSource? _cts;
    private static Task? _loop;
    private static DateTime _lastBackupUtc = DateTime.MinValue;

    /// <summary>排程器有冇喺度跑 · Is the scheduler running?</summary>
    public static bool IsRunning { get { lock (Gate) return _loop is { IsCompleted: false }; } }

    /// <summary>
    /// 啟動排程器 · Start the scheduler (idempotent). 安全重入；會先停咗舊嘅再起新嘅，
    /// 令設定（例如間隔）改變後可以即時生效。Safe to call repeatedly — restarts cleanly so an
    /// interval change takes effect. Returns immediately; all work runs on a background task.
    /// </summary>
    public static void Start()
    {
        try
        {
            Stop();
            lock (Gate)
            {
                _cts = new CancellationTokenSource();
                _loop = Task.Run(() => RunLoopAsync(_cts.Token));
            }
        }
        catch { /* never block the caller */ }
    }

    /// <summary>停止排程器 · Stop the scheduler (safe to call when not running).</summary>
    public static void Stop()
    {
        CancellationTokenSource? cts;
        lock (Gate)
        {
            cts = _cts;
            _cts = null;
            _loop = null;
        }
        try { cts?.Cancel(); } catch { }
        try { cts?.Dispose(); } catch { }
    }

    // ===== main loop =====

    private static async Task RunLoopAsync(CancellationToken ct)
    {
        try
        {
            // 一開波先等一個短延遲，避免同 app 啟動爭資源 · small initial delay so we don't fight startup.
            try { await Task.Delay(TimeSpan.FromSeconds(20), ct); } catch (OperationCanceledException) { return; }

            while (!ct.IsCancellationRequested)
            {
                // 只喺 AutoCheck 開咗先做工作；無論如何都會喺間隔之後再 tick。
                // Only do work when AutoCheck is on; we still loop so a later toggle is picked up.
                if (PackageManagerSettings.AutoCheckEnabled)
                {
                    try { await TickAsync(ct); }
                    catch (OperationCanceledException) { return; }
                    catch { /* swallow — never let a tick crash the loop */ }
                }

                // 用設定嘅間隔（夾喺最少 1 分鐘）· wait the configured interval (min 1 minute).
                int minutes = Math.Max(1, PackageManagerSettings.CheckIntervalMinutes);
                using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
                try
                {
                    if (!await timer.WaitForNextTickAsync(ct)) return;
                }
                catch (OperationCanceledException) { return; }
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch { /* defensive — the loop must never throw */ }
    }

    /// <summary>一個 update result 連 winget 安裝來源警告旗標 · One update plus its best-effort host-change warning.</summary>
    private sealed class ScannedUpdate
    {
        public required PackageItem Item { get; init; }
        public bool InstallerHostChanged { get; init; }
    }

    private static async Task TickAsync(CancellationToken ct)
    {
        // 1) 收集更新 · collect updates across all managers.
        List<PackageItem> raw;
        try { raw = await PackageManagerRegistry.AllUpdatesAsync(null, ct); }
        catch { raw = new(); }
        if (ct.IsCancellationRequested) return;

        // 2) 最少更新年齡過濾（盡力而為）· minimum-update-age filter (best-effort).
        int minAge = PackageManagerSettings.MinimumUpdateAgeDays;
        var aged = minAge <= 0 ? raw : await FilterByAgeAsync(raw, minAge, ct);

        // 已忽略／暫停嘅更新唔應該入系統匣數量，亦唔應該觸發主機警告或自動更新。
        // Ignored/snoozed updates do not count in the tray and must not trigger host warnings or automation.
        var visible = aged
            .Where(item => PackageOperationCoordinator.IsSafePackageId(item.Id))
            .Where(item => !IgnoredUpdates.IsIgnored(item))
            .Where(item => !PackageOperationCoordinator.IsMinorUpdateSuppressed(item))
            .ToList();

        // 3) winget 安裝來源主機改變檢查（盡力而為）· installer-host-change check for winget (best-effort).
        var scanned = new List<ScannedUpdate>(visible.Count);
        bool checkHost = PackageManagerSettings.WarnInstallerHostChange;
        foreach (var item in visible)
        {
            if (ct.IsCancellationRequested) return;
            bool changed = false;
            if (checkHost && string.Equals(item.ManagerKey, "winget", StringComparison.OrdinalIgnoreCase))
            {
                try { changed = await InstallerHostChangedAsync(item, ct); } catch { changed = false; }
                if (ct.IsCancellationRequested) return;
                if (changed)
                {
                    // PackageNotifier 冇獨立 warning 類型；用可見嘅一般通知清楚標示安全警告。
                    // PackageNotifier has no warning channel, so surface an explicit security warning
                    // through its visible generic notification path.
                    PackageNotifier.ShowProgress(
                        Loc.I.Pick(
                            $"Security warning: {item.Name}'s installer host changed. Automatic update was blocked.",
                            $"安全警告：{item.Name} 嘅安裝程式主機有變，自動更新已被封鎖。"),
                        item.ManagerKey);
                }
            }
            scanned.Add(new ScannedUpdate { Item = item, InstallerHostChanged = changed });
        }

        if (ct.IsCancellationRequested) return;

        // 4) 更新系統匣 + 通知 · update the tray count + fire "updates available".
        int count = scanned.Count;
        MarshalToUi(() => TrayService.SetUpdateCount(count));
        if (count > 0) PackageNotifier.ShowUpdatesAvailable(count);

        // 5) 自動安裝（全域或逐套件開咗，而且冇被閘住）· auto-install globally or per-package when not gated.
        if (count > 0 && !AutoOperationsGated())
        {
            if (ct.IsCancellationRequested) return;
            // 跳過會改安裝來源主機嘅 winget 套件（安全閘）· skip winget packages whose installer host changed.
            bool autoInstallAll = PackageManagerSettings.AutoInstallUpdates;
            var installable = scanned
                .Where(s => !s.InstallerHostChanged)
                .Select(s => s.Item)
                .Where(item => autoInstallAll || IsPerPackageAutoUpdateEnabled(item))
                .ToList();
            if (installable.Count > 0)
                await InstallUpdatesAsync(installable, ct);
        }

        // 6) 每日本地備份 · run the optional local backup on a daily cadence.
        if (PackageManagerSettings.LocalBackupEnabled && (DateTime.UtcNow - _lastBackupUtc).TotalHours >= 24)
        {
            if (ct.IsCancellationRequested) return;
            try { await RunLocalBackupAsync(ct); _lastBackupUtc = DateTime.UtcNow; } catch { }
        }
    }

    // ===== minimum update age =====

    /// <summary>
    /// 按發行／修改日期過濾更新（只保留夠舊嘅）· Keep only updates whose available release/modified date is at
    /// least <paramref name="minDays"/> days old. 盡力而為：攞唔到日期就放行（唔扣起）。
    /// Best-effort: if the date is unknown, the update is NOT held back.
    /// </summary>
    private static async Task<List<PackageItem>> FilterByAgeAsync(List<PackageItem> items, int minDays, CancellationToken ct)
    {
        var threshold = DateTime.UtcNow.AddDays(-minDays);
        var kept = new List<PackageItem>(items.Count);
        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;
            DateTime? released = null;
            try { released = await ReleaseDateAsync(item, ct); } catch { released = null; }
            // 攞唔到日期 → 放行；攞到就只保留夠舊嘅 · unknown date passes; known date must be old enough.
            if (released is null || released.Value.ToUniversalTime() <= threshold)
                kept.Add(item);
        }
        return kept;
    }

    /// <summary>
    /// 盡力攞一個套件可用版本嘅發行日期 · Best-effort release/modified date for a package's available version.
    /// 目前只有 winget show 可靠提供少量日期資訊；其餘回 null（即放行）。
    /// Only winget exposes anything date-like via "winget show"; other managers return null (=> passes).
    /// </summary>
    private static async Task<DateTime?> ReleaseDateAsync(PackageItem item, CancellationToken ct)
    {
        if (!string.Equals(item.ManagerKey, "winget", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            var text = await ShellRunner.CapturePowershell(
                $"winget show --id \"{PkgQuote(item.Id)}\" -e --accept-source-agreements --disable-interactivity | Out-String -Width 300", ct);
            if (string.IsNullOrWhiteSpace(text)) return null;
            // 搵類似 "Released: 2024-01-02" / "Release Date" / "Last Updated" 嘅行 · scan for a date-ish line.
            foreach (var line in text.Replace("\r", "").Split('\n'))
            {
                var l = line.Trim();
                if (l.Length == 0) continue;
                int colon = l.IndexOf(':');
                if (colon <= 0) continue;
                var label = l.Substring(0, colon).Trim().ToLowerInvariant();
                if (!(label.Contains("released") || label.Contains("release date") || label.Contains("updated")
                      || label.Contains("date"))) continue;
                var val = l.Substring(colon + 1).Trim();
                if (DateTime.TryParse(val, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                        out var dt))
                    return dt;
            }
        }
        catch { }
        return null;
    }

    // ===== installer host change (winget security check) =====

    /// <summary>
    /// 盡力比較 winget 已安裝版本同可用版本嘅安裝程式 URL 主機 · Best-effort compare the installer URL host of
    /// the installed vs available winget version. 主機唔同就回 true（值得警告）。
    /// Returns true when the host differs (worth warning about); any uncertainty returns false (no warning).
    /// </summary>
    private static async Task<bool> InstallerHostChangedAsync(PackageItem item, CancellationToken ct)
    {
        try
        {
            // 可用版本嘅 manifest（最新）· available version's manifest (latest).
            var availText = await ShellRunner.CapturePowershell(
                $"winget show --id \"{PkgQuote(item.Id)}\" -e --accept-source-agreements --disable-interactivity | Out-String -Width 400", ct);
            var availHost = FirstInstallerHost(availText);
            if (string.IsNullOrEmpty(availHost)) return false; // 唔肯定就唔警告 · unknown => no warning.

            // 已安裝版本嘅 manifest（指定版本）· installed version's manifest (pinned to the installed version).
            string installedHost = "";
            if (!string.IsNullOrWhiteSpace(item.Version))
            {
                var instText = await ShellRunner.CapturePowershell(
                    $"winget show --id \"{PkgQuote(item.Id)}\" -e --version \"{PkgQuote(item.Version)}\" --accept-source-agreements --disable-interactivity | Out-String -Width 400", ct);
                installedHost = FirstInstallerHost(instText);
            }
            if (string.IsNullOrEmpty(installedHost)) return false; // 冇得比就唔警告 · nothing to compare => no warning.

            return !string.Equals(availHost, installedHost, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>由 winget show 輸出抽第一個安裝程式 URL 主機 · Extract the first installer-URL host from "winget show".</summary>
    private static string FirstInstallerHost(string? showOutput)
    {
        if (string.IsNullOrWhiteSpace(showOutput)) return "";
        foreach (var line in showOutput.Replace("\r", "").Split('\n'))
        {
            var l = line.Trim();
            // winget show 一般有 "Installer Url: https://…" 一行 · the manifest prints "Installer Url: https://…".
            int idx = l.IndexOf("http", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            var label = l.Length > 0 ? l.ToLowerInvariant() : "";
            if (!(label.Contains("installer") && label.Contains("url")) && !label.Contains("download")) continue;
            var url = l.Substring(idx).Trim();
            try { return new Uri(url).Host; } catch { }
        }
        return "";
    }

    // ===== auto-install through the shared global coordinator =====

    /// <summary>讀逐套件自動更新偏好（失敗就安全地當關閉）· Read a per-package auto-update preference; fail closed.</summary>
    private static bool IsPerPackageAutoUpdateEnabled(PackageItem item)
    {
        try { return InstallOptions.Load(item.ManagerKey, item.Id).AutoUpdate; }
        catch { return false; }
    }

    private static async Task InstallUpdatesAsync(List<PackageItem> items, CancellationToken ct)
    {
        // The coordinator loads each package's saved options, enforces the configured global concurrency,
        // de-duplicates operations, re-checks ignore/minor policies, and owns progress/success/error notices.
        try
        {
            await PackageOperationCoordinator.RunManyAsync(
                items, PackageOperations.Op.Update, options: null, ct: ct);
        }
        catch (OperationCanceledException) { }
        catch { /* per-item failures are represented by coordinator snapshots */ }

        // 安裝完重新整理系統匣計數 · refresh the tray count after installing.
        try
        {
            var remaining = await PackageManagerRegistry.AllUpdatesAsync(null, ct);
            int minAge = PackageManagerSettings.MinimumUpdateAgeDays;
            var aged = minAge <= 0 ? remaining : await FilterByAgeAsync(remaining, minAge, ct);
            int visibleCount = aged.Count(item => PackageOperationCoordinator.IsSafePackageId(item.Id)
                && !IgnoredUpdates.IsIgnored(item)
                && !PackageOperationCoordinator.IsMinorUpdateSuppressed(item));
            MarshalToUi(() => TrayService.SetUpdateCount(visibleCount));
        }
        catch { }
    }

    // ===== local backup =====

    /// <summary>
    /// 將已安裝套件清單寫做 JSON 到備份資料夾 · Write the installed-package list to the backup directory as JSON,
    /// 用 FileDialogs-free 嘅直接 File IO（背景排程冇 UI 可彈對話框），啟用時加時間戳檔名。
    /// Uses direct File IO (no pickers — the scheduler has no UI), with a timestamped filename when enabled.
    /// </summary>
    private static async Task RunLocalBackupAsync(CancellationToken ct)
    {
        var dir = PackageManagerSettings.LocalBackupDir;
        if (string.IsNullOrWhiteSpace(dir)) return;

        List<PackageItem> installed;
        try { installed = await PackageManagerRegistry.AllInstalledAsync(null, ct); }
        catch { installed = new(); }

        try
        {
            Directory.CreateDirectory(dir);
            var entries = installed.Select(i => new BackupEntry
            {
                Manager = i.ManagerKey,
                Id = i.Id,
                Name = i.Name,
                Version = i.Version,
                Source = i.Source,
            }).ToList();

            var baseName = PackageManagerSettings.LocalBackupFileName;
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "winforge-packages";
            // 去掉任何路徑成分／非法字元 · strip any path/illegal chars.
            baseName = Path.GetFileNameWithoutExtension(baseName);
            foreach (var c in Path.GetInvalidFileNameChars()) baseName = baseName.Replace(c, '_');

            var fileName = PackageManagerSettings.BackupTimestamping
                ? $"{baseName}-{DateTime.Now:yyyyMMdd-HHmmss}.json"
                : $"{baseName}.json";

            var payload = new BackupFile
            {
                CreatedUtc = DateTime.UtcNow,
                Count = entries.Count,
                Packages = entries,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(dir, fileName), json, ct);
        }
        catch { /* best effort — backup must never crash the scheduler */ }
    }

    private sealed class BackupFile
    {
        public DateTime CreatedUtc { get; set; }
        public int Count { get; set; }
        public List<BackupEntry> Packages { get; set; } = new();
    }

    private sealed class BackupEntry
    {
        public string Manager { get; set; } = "";
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Source { get; set; } = "";
    }

    // ===== power / network gating =====

    /// <summary>
    /// 自動操作有冇被流量／電池／慳電閘住 · Are auto-operations currently gated by metered / battery /
    /// battery-saver, per settings? 任何探測失敗都當「冇閘住」（放行），唔好因為偵測唔到而卡死自動更新。
    /// Any probe failure is treated as "not gated" so detection issues never permanently block auto-updates.
    /// </summary>
    private static bool AutoOperationsGated()
    {
        try
        {
            if (PackageManagerSettings.DisableOnMetered && IsMetered()) return true;
            if (PackageManagerSettings.DisableOnBattery && IsOnBattery()) return true;
            if (PackageManagerSettings.DisableOnBatterySaver && IsBatterySaverOn()) return true;
        }
        catch { /* fall through — not gated */ }
        return false;
    }

    private static bool IsMetered()
    {
        try
        {
            var profile = Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();
            if (profile is null) return false;
            var cost = profile.GetConnectionCost();
            if (cost is null) return false;
            // 用量計費 或 超流量 · metered cost types, or over the data limit / roaming.
            var t = cost.NetworkCostType;
            return t == Windows.Networking.Connectivity.NetworkCostType.Fixed
                || t == Windows.Networking.Connectivity.NetworkCostType.Variable
                || cost.OverDataLimit
                || cost.Roaming;
        }
        catch { return false; }
    }

    private static bool IsOnBattery()
    {
        try
        {
            // 冇接電源（即係靠電池）· power supply is disconnected => running on battery.
            return Windows.System.Power.PowerManager.PowerSupplyStatus
                   == Windows.System.Power.PowerSupplyStatus.NotPresent
                && Windows.System.Power.PowerManager.BatteryStatus
                   != Windows.System.Power.BatteryStatus.NotPresent;
        }
        catch { return false; }
    }

    private static bool IsBatterySaverOn()
    {
        try
        {
            return Windows.System.Power.PowerManager.EnergySaverStatus
                   == Windows.System.Power.EnergySaverStatus.On;
        }
        catch { return false; }
    }

    // ===== helpers =====

    private static string PkgQuote(string s) => (s ?? "").Replace("\"", "").Replace("`", "").Trim();

    /// <summary>喺 UI 執行緒上跑（攞到 dispatcher 就用，攞唔到就直接跑）· Marshal an action to the UI thread when possible.</summary>
    private static void MarshalToUi(Action action)
    {
        try
        {
            var dq = App.Shell?.DispatcherQueue;
            if (dq is not null) dq.TryEnqueue(() => { try { action(); } catch { } });
            else action();
        }
        catch
        {
            try { action(); } catch { }
        }
    }
}
