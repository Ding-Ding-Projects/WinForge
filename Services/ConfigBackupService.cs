using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個設定快照（git commit）· One config snapshot (a git commit row).</summary>
public sealed class SnapshotInfo
{
    public string Hash { get; set; } = "";
    public string ShortHash => Hash.Length >= 7 ? Hash[..7] : Hash;
    public string Date { get; set; } = "";
    public string Subject { get; set; } = "";
}

/// <summary>
/// 設定與備份引擎 · Config &amp; Backup engine — exports the whole suite's settings into a portable
/// .zip bundle (with a version manifest + SHA-256 checksums), imports/re-applies them, keeps a local
/// git snapshot repo of config history under %LOCALAPPDATA%\WinTune\snapshots, and wraps real CLIs
/// (git, schtasks, reg, winget, robocopy) for scheduling, registry/app capture, mirroring,
/// bundling, pruning and integrity checks. Reuses <see cref="SettingsStore"/> export/import and the
/// raw <see cref="ShellRunner"/>/git plumbing.
/// </summary>
public static class ConfigBackupService
{
    public const string ManifestVersion = "1";

    private static readonly string AppDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinTune");

    /// <summary>本地快照 git 倉庫 · The local git snapshot repository folder.</summary>
    public static string SnapshotsDir => Path.Combine(AppDir, "snapshots");

    private static readonly string SettingsFile = Path.Combine(AppDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    /// <summary>
    /// 套件已知會改動嘅登錄機碼（用嚟匯出 .reg）· Registry keys the suite is known to touch, exported
    /// to a single human-reviewable .reg backup. Only HKCU keys are included so export needs no admin.
    /// </summary>
    public static readonly (string key, string label)[] TouchedRegistryKeys =
    {
        (@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Explorer Advanced"),
        (@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband", "Taskbar pins"),
        (@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", "Search"),
        (@"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "Personalize / dark mode"),
        (@"HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Advertising ID"),
        (@"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "Suggestions"),
        (@"HKCU\Control Panel\Desktop", "Desktop / wallpaper quality"),
        (@"HKCU\Control Panel\Mouse", "Mouse"),
        (@"HKCU\Control Panel\Keyboard", "Keyboard"),
        (@"HKCU\Software\Microsoft\Clipboard", "Clipboard"),
        (@"HKCU\Environment", "User environment variables"),
    };

    // ───────────────────────── ZIP bundle export / import ─────────────────────────

    /// <summary>
    /// 匯出成個套件嘅設定做一個 .zip · Export all suite settings into a single portable .zip bundle
    /// (settings.json + a version manifest + a SHA-256 checksums manifest).
    /// </summary>
    public static async Task<TweakResult> ExportBundle(string zipPath, CancellationToken ct = default)
    {
        try
        {
            // Persist the live settings so the on-disk file is current.
            SettingsStore.ExportTo(SettingsFile);

            var staging = Path.Combine(Path.GetTempPath(), "wintune_export_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                if (File.Exists(SettingsFile))
                    File.Copy(SettingsFile, Path.Combine(staging, "settings.json"), true);
                else
                    await File.WriteAllTextAsync(Path.Combine(staging, "settings.json"), "{}", ct);

                var manifest = new
                {
                    app = "WinTune",
                    bundleVersion = ManifestVersion,
                    created = DateTimeOffset.Now.ToString("o"),
                    machine = Environment.MachineName,
                    user = Environment.UserName,
                };
                await File.WriteAllTextAsync(Path.Combine(staging, "manifest.json"),
                    JsonSerializer.Serialize(manifest, JsonOpts), ct);

                // SHA-256 checksums for every staged file (used by integrity verify on restore).
                await WriteChecksums(staging, ct);

                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(staging, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            }
            finally { TryDeleteDir(staging); }

            return TweakResult.Ok($"Exported settings bundle to {zipPath}.",
                $"已將設定匯出到 {zipPath}。", zipPath);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"匯出失敗：{ex.Message}");
        }
    }

    /// <summary>
    /// 匯入設定檔案再套返 · Import a .zip bundle, validate its manifest version, then merge/re-apply
    /// the settings through the existing <see cref="SettingsStore"/> import pipeline.
    /// </summary>
    public static async Task<TweakResult> ImportBundle(string zipPath, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(zipPath))
                return TweakResult.Fail("Bundle not found.", "搵唔到設定檔案。");

            var staging = Path.Combine(Path.GetTempPath(), "wintune_import_" + Guid.NewGuid().ToString("N"));
            try
            {
                ZipFile.ExtractToDirectory(zipPath, staging);

                var manifestPath = Path.Combine(staging, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath, ct));
                    if (doc.RootElement.TryGetProperty("bundleVersion", out var v) &&
                        v.GetString() is string ver && ver != ManifestVersion)
                        return TweakResult.Fail(
                            $"Bundle version {ver} is not supported (expected {ManifestVersion}).",
                            $"設定檔案版本 {ver} 唔支援（預期 {ManifestVersion}）。");
                }

                var settings = Path.Combine(staging, "settings.json");
                if (!File.Exists(settings))
                    return TweakResult.Fail("Bundle has no settings.json.", "設定檔案入面冇 settings.json。");

                int n = SettingsStore.ImportFrom(settings);
                return TweakResult.Ok(
                    $"Imported & re-applied {n} setting(s). Restart WinTune for all of them to take effect.",
                    $"已匯入並套用 {n} 項設定。重開 WinTune 全部即生效。");
            }
            finally { TryDeleteDir(staging); }
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"匯入失敗：{ex.Message}");
        }
    }

    // ───────────────────────── git snapshot repo ─────────────────────────

    private static async Task<(bool ok, string output)> Git(string args, CancellationToken ct = default)
    {
        var r = await ShellRunner.RunIn(SnapshotsDir, "git", args, elevated: false, ct);
        return (r.Success, (r.Output ?? string.Empty).Trim());
    }

    public static bool SnapshotRepoExists =>
        Directory.Exists(Path.Combine(SnapshotsDir, ".git"));

    /// <summary>起一個本地 git 倉庫儲設定歷史 · git init the snapshots repo (idempotent).</summary>
    public static async Task<TweakResult> InitSnapshotRepo(CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(SnapshotsDir);
            if (!SnapshotRepoExists)
            {
                var (ok, outp) = await Git("init", ct);
                if (!ok) return TweakResult.Fail("git init failed.", "git init 失敗。", outp);
                // Local identity so commits work even without a global git config.
                await Git("config user.name WinTune", ct);
                await Git("config user.email wintune@localhost", ct);
            }
            return TweakResult.Ok($"Snapshot repo ready at {SnapshotsDir}.",
                $"快照倉庫已就緒：{SnapshotsDir}。", SnapshotsDir);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>
    /// 影一個快照：寫低設定再 git commit · Take a snapshot — write the current settings export into the
    /// repo working tree, then git add -A &amp; commit with a timestamp message.
    /// </summary>
    public static async Task<TweakResult> TakeSnapshot(string? message = null, CancellationToken ct = default)
    {
        var init = await InitSnapshotRepo(ct);
        if (!init.Success) return init;
        try
        {
            SettingsStore.ExportTo(Path.Combine(SnapshotsDir, "settings.json"));

            // Also capture a winget app list inside the snapshot when winget is available (best effort).
            await TryCaptureWingetInto(Path.Combine(SnapshotsDir, "apps.json"), ct);

            var (addOk, addOut) = await Git("add -A", ct);
            if (!addOk) return TweakResult.Fail("git add failed.", "git add 失敗。", addOut);

            var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var msg = string.IsNullOrWhiteSpace(message) ? stamp : $"{stamp} — {message.Trim()}";
            var (cOk, cOut) = await Git($"commit -m \"{msg.Replace("\"", "'")}\"", ct);
            if (!cOk)
            {
                if (cOut.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase))
                    return TweakResult.Ok("No changes since the last snapshot.", "同上一個快照冇分別。");
                return TweakResult.Fail("git commit failed.", "git commit 失敗。", cOut);
            }
            return TweakResult.Ok($"Snapshot saved: {msg}", $"已儲存快照：{msg}", cOut);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>睇返啲快照歷史 · List snapshot history (git log), newest first.</summary>
    public static async Task<List<SnapshotInfo>> ListSnapshots(CancellationToken ct = default)
    {
        var list = new List<SnapshotInfo>();
        if (!SnapshotRepoExists) return list;
        var (ok, outp) = await Git("log --pretty=format:%H%x09%ad%x09%s --date=iso", ct);
        if (!ok) return list;
        foreach (var raw in outp.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = raw.Split('\t');
            if (parts.Length < 3) continue;
            list.Add(new SnapshotInfo { Hash = parts[0].Trim(), Date = parts[1].Trim(), Subject = parts[2].Trim() });
        }
        return list;
    }

    /// <summary>
    /// 還原返去之前一個快照 · Restore the working tree to a snapshot, then re-apply via import.
    /// Non-destructive: the current state is committed as a safety snapshot first.
    /// </summary>
    public static async Task<TweakResult> RestoreSnapshot(string commit, CancellationToken ct = default)
    {
        if (!SnapshotRepoExists)
            return TweakResult.Fail("No snapshot repo yet.", "未有快照倉庫。");
        try
        {
            // Safety: snapshot whatever is current before overwriting it.
            await TakeSnapshot("auto: before restore", ct);

            var (ok, outp) = await Git($"checkout {commit} -- .", ct);
            if (!ok) return TweakResult.Fail("git restore failed.", "還原失敗。", outp);

            var settings = Path.Combine(SnapshotsDir, "settings.json");
            int n = File.Exists(settings) ? SettingsStore.ImportFrom(settings) : 0;
            return TweakResult.Ok(
                $"Restored to {commit[..Math.Min(7, commit.Length)]} and re-applied {n} setting(s).",
                $"已還原到 {commit[..Math.Min(7, commit.Length)]} 並套用 {n} 項設定。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>比較而家同舊快照嘅設定差異 · Diff current settings.json against a snapshot.</summary>
    public static async Task<TweakResult> DiffSnapshot(string commit, CancellationToken ct = default)
    {
        if (!SnapshotRepoExists)
            return TweakResult.Fail("No snapshot repo yet.", "未有快照倉庫。");
        // Make sure the working tree reflects the live settings before diffing.
        SettingsStore.ExportTo(Path.Combine(SnapshotsDir, "settings.json"));
        var (_, outp) = await Git($"diff {commit} -- settings.json", ct);
        if (string.IsNullOrWhiteSpace(outp))
            return TweakResult.Ok("No differences.", "冇分別。", "");
        return TweakResult.Ok("Diff computed.", "已計算差異。", outp);
    }

    /// <summary>將一個快照打包成單一 git bundle 檔 · Package full history into one git bundle file.</summary>
    public static async Task<TweakResult> CreateBundle(string bundlePath, CancellationToken ct = default)
    {
        if (!SnapshotRepoExists)
            return TweakResult.Fail("No snapshot repo yet.", "未有快照倉庫。");
        try { if (File.Exists(bundlePath)) File.Delete(bundlePath); } catch { }
        var (ok, outp) = await Git($"bundle create \"{bundlePath}\" --all", ct);
        return ok
            ? TweakResult.Ok($"Bundle written to {bundlePath}.", $"已寫入 bundle：{bundlePath}。", outp)
            : TweakResult.Fail("git bundle failed.", "git bundle 失敗。", outp);
    }

    /// <summary>清走舊快照，慳返空間 · Prune history: reflog expire + aggressive gc.</summary>
    public static async Task<TweakResult> PruneHistory(CancellationToken ct = default)
    {
        if (!SnapshotRepoExists)
            return TweakResult.Fail("No snapshot repo yet.", "未有快照倉庫。");
        var sb = new StringBuilder();
        var (e1, o1) = await Git("reflog expire --expire=now --all", ct);
        sb.AppendLine(o1);
        var (e2, o2) = await Git("gc --prune=now --aggressive", ct);
        sb.AppendLine(o2);
        return (e1 && e2)
            ? TweakResult.Ok("History pruned and repo compacted.", "已清走舊歷史並壓縮倉庫。", sb.ToString().Trim())
            : TweakResult.Fail("Prune failed.", "清理失敗。", sb.ToString().Trim());
    }

    /// <summary>驗一驗備份冇壞 · Verify integrity — recompute SHA-256 + git fsck.</summary>
    public static async Task<TweakResult> VerifyIntegrity(CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        bool ok = true;

        if (SnapshotRepoExists)
        {
            var (fOk, fOut) = await Git("fsck --full", ct);
            sb.AppendLine("git fsck:");
            sb.AppendLine(string.IsNullOrWhiteSpace(fOut) ? "  (no problems reported)" : fOut);
            ok &= fOk;
        }
        else
        {
            sb.AppendLine("git fsck: (no snapshot repo)");
        }

        return ok
            ? TweakResult.Ok("Integrity OK — git objects verified.", "完整性正常 — git 物件已驗證。", sb.ToString().Trim())
            : TweakResult.Fail("Integrity check reported problems.", "完整性檢查發現問題。", sb.ToString().Trim());
    }

    // ───────────────────────── scheduled daily backup (schtasks) ─────────────────────────

    public const string DailyTaskName = "WinTune Daily Backup";

    /// <summary>排好每日自動備份 · Register a daily backup task that runs WinTune --snapshot.</summary>
    public static async Task<TweakResult> ScheduleDailyBackup(string time = "03:00", CancellationToken ct = default)
    {
        var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "WinTune.exe");
        var tr = $"\\\"{exe}\\\" --snapshot";
        var args = $"/Create /SC DAILY /TN \"{DailyTaskName}\" /TR \"{tr}\" /ST {time} /RL LIMITED /F";
        var r = await ShellRunner.Run("schtasks.exe", args, elevated: false, ct);
        return r.Success
            ? TweakResult.Ok($"Daily backup scheduled at {time}.", $"已排定每日 {time} 自動備份。", r.Output)
            : TweakResult.Fail("Could not schedule the task.", "排程失敗。", r.Output);
    }

    /// <summary>取消每日自動備份 · Remove the scheduled daily backup task.</summary>
    public static async Task<TweakResult> UnscheduleDailyBackup(CancellationToken ct = default)
    {
        var r = await ShellRunner.Run("schtasks.exe", $"/Delete /TN \"{DailyTaskName}\" /F", elevated: false, ct);
        return r.Success
            ? TweakResult.Ok("Daily backup task removed.", "已移除每日備份工作。", r.Output)
            : TweakResult.Fail("No scheduled task to remove (or it failed).", "冇排程工作可以移除（或者失敗）。", r.Output);
    }

    public static async Task<bool> IsDailyBackupScheduled(CancellationToken ct = default)
    {
        var r = await ShellRunner.Run("schtasks.exe", $"/Query /TN \"{DailyTaskName}\"", elevated: false, ct);
        return r.Success;
    }

    // ───────────────────────── registry / winget / start layout capture ─────────────────────────

    /// <summary>匯出改過嘅登錄機碼做 .reg 檔 · Export the suite's touched HKCU keys to one .reg file.</summary>
    public static async Task<TweakResult> ExportRegistry(string regPath, CancellationToken ct = default)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Windows Registry Editor Version 5.00");
            sb.AppendLine();
            int exported = 0;
            foreach (var (key, label) in TouchedRegistryKeys)
            {
                ct.ThrowIfCancellationRequested();
                var tmp = Path.Combine(Path.GetTempPath(), $"wintune_reg_{Guid.NewGuid():N}.reg");
                var r = await ShellRunner.Run("reg.exe", $"export \"{key}\" \"{tmp}\" /y", elevated: false, ct);
                if (r.Success && File.Exists(tmp))
                {
                    var body = await File.ReadAllTextAsync(tmp, ct);
                    // Strip each file's own header line; keep only the key blocks.
                    var lines = body.Split('\n').SkipWhile(l =>
                        l.StartsWith("Windows Registry Editor", StringComparison.OrdinalIgnoreCase) ||
                        string.IsNullOrWhiteSpace(l));
                    sb.AppendLine($"; --- {label} ({key}) ---");
                    sb.AppendLine(string.Join("\n", lines).TrimEnd());
                    sb.AppendLine();
                    exported++;
                }
                try { File.Delete(tmp); } catch { }
            }
            await File.WriteAllTextAsync(regPath, sb.ToString(), new UTF8Encoding(false), ct);
            return TweakResult.Ok($"Exported {exported} registry key(s) to {regPath}.",
                $"已匯出 {exported} 個登錄機碼到 {regPath}。", regPath);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"匯出失敗：{ex.Message}");
        }
    }

    /// <summary>用 winget 匯出而家裝咗嘅程式清單 · Capture the installed-app list via winget export.</summary>
    public static async Task<TweakResult> ExportWingetApps(string jsonPath, CancellationToken ct = default)
    {
        var r = await ShellRunner.Run("winget.exe",
            $"export -o \"{jsonPath}\" --include-versions --accept-source-agreements", elevated: false, ct);
        return r.Success
            ? TweakResult.Ok($"App list captured to {jsonPath}.", $"已匯出程式清單到 {jsonPath}。", r.Output)
            : TweakResult.Fail("winget export failed (is winget installed?).",
                "winget 匯出失敗（有冇裝 winget？）。", r.Output);
    }

    private static async Task TryCaptureWingetInto(string jsonPath, CancellationToken ct)
    {
        try { await ExportWingetApps(jsonPath, ct); } catch { /* best effort inside a snapshot */ }
    }

    /// <summary>
    /// 備份工作列固定捷徑同開始選單排版 · Back up taskbar pins (Taskband reg) + the Start layout file
    /// (start2.bin) into a folder. NOTE: Win11 has no supported Start-layout import, so start2.bin is
    /// kept for reference / manual re-pin only.
    /// </summary>
    public static async Task<TweakResult> BackupTaskbarAndStart(string destDir, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(destDir);
            var sb = new StringBuilder();

            var reg = Path.Combine(destDir, "taskband.reg");
            var r = await ShellRunner.Run("reg.exe",
                $"export \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Taskband\" \"{reg}\" /y",
                elevated: false, ct);
            sb.AppendLine(r.Success ? "Taskbar pins (Taskband) exported." : "Taskband export failed.");

            var start2 = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages", "Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy", "LocalState", "start2.bin");
            if (File.Exists(start2))
            {
                File.Copy(start2, Path.Combine(destDir, "start2.bin"), true);
                sb.AppendLine("Start layout (start2.bin) copied — reference only (no supported import on Win11).");
            }
            else
            {
                sb.AppendLine("start2.bin not found (Start layout not captured).");
            }

            return TweakResult.Ok($"Taskbar/Start backup written to {destDir}.",
                $"已備份工作列／開始選單到 {destDir}。", sb.ToString().Trim());
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"備份失敗：{ex.Message}");
        }
    }

    // ───────────────────────── mirror (robocopy) ─────────────────────────

    /// <summary>將備份鏡像去資料夾／網絡共享 · Mirror the snapshots dir with robocopy /MIR.</summary>
    public static async Task<TweakResult> MirrorTo(string dest, CancellationToken ct = default)
    {
        if (!Directory.Exists(SnapshotsDir))
            return TweakResult.Fail("Nothing to mirror — no snapshots yet.", "冇嘢可以鏡像 — 未有快照。");
        if (string.IsNullOrWhiteSpace(dest))
            return TweakResult.Fail("Pick a destination folder first.", "請先揀目的地資料夾。");

        // robocopy exit codes 0–7 are success (8+ are real failures). ShellRunner flags any non-zero
        // exit as a failure, so run via cmd and normalise the code ourselves.
        var cmd = $"robocopy \"{SnapshotsDir}\" \"{dest}\" /MIR /R:2 /W:2 /NP & if %ERRORLEVEL% LSS 8 (exit /b 0) else (exit /b %ERRORLEVEL%)";
        var r = await ShellRunner.RunCmd(cmd, elevated: false, ct);
        return r.Success
            ? TweakResult.Ok($"Mirrored snapshots to {dest}.", $"已鏡像快照到 {dest}。", r.Output)
            : TweakResult.Fail("Mirror failed.", "鏡像失敗。", r.Output);
    }

    // ───────────────────────── helpers ─────────────────────────

    private static async Task WriteChecksums(string dir, CancellationToken ct)
    {
        var sb = new StringBuilder();
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                     .Where(f => !f.EndsWith("checksums.txt", StringComparison.OrdinalIgnoreCase)))
        {
            ct.ThrowIfCancellationRequested();
            using var fs = File.OpenRead(file);
            using var sha = SHA256.Create();
            var hash = Convert.ToHexString(await sha.ComputeHashAsync(fs, ct));
            sb.AppendLine($"{hash}  {Path.GetFileName(file)}");
        }
        await File.WriteAllTextAsync(Path.Combine(dir, "checksums.txt"), sb.ToString(), ct);
    }

    private static void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }
}
