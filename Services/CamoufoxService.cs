using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 一個 Camoufox 設定檔（指紋瀏覽器）· One Camoufox anti-detect browser profile. Persisted as
/// <c>profiles/&lt;id&gt;/profile.json</c> next to a <c>userdata/</c> Firefox profile directory.
/// Every field is plain JSON so the whole store is git-trackable.
/// </summary>
public sealed class CamoufoxProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Tags { get; set; } = "";
    public string CreatedUtc { get; set; } = "";
    public string UpdatedUtc { get; set; } = "";

    // ── First-class fingerprint fields (the common ones; everything else lives in ConfigJson) ──
    public string UserAgent { get; set; } = "";
    public string Locale { get; set; } = "";       // e.g. en-US
    public string Timezone { get; set; } = "";     // e.g. America/New_York
    public string OsName { get; set; } = "";       // windows | macos | linux
    public string ScreenWidth { get; set; } = "";
    public string ScreenHeight { get; set; } = "";
    public string Proxy { get; set; } = "";        // http://user:pass@host:port | socks5://host:port
    /// <summary>額外 Camoufox 設定（JSON 物件）· Extra raw Camoufox config (a JSON object); overrides the fields above.</summary>
    public string ConfigJson { get; set; } = "";

    [JsonIgnore]
    public string ShortId => Id.Length >= 8 ? Id[..8] : Id;

    [JsonIgnore]
    public string FingerprintSummary
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(OsName)) parts.Add(OsName);
            if (!string.IsNullOrWhiteSpace(Locale)) parts.Add(Locale);
            if (!string.IsNullOrWhiteSpace(Timezone)) parts.Add(Timezone);
            if (!string.IsNullOrWhiteSpace(ScreenWidth) && !string.IsNullOrWhiteSpace(ScreenHeight))
                parts.Add($"{ScreenWidth}×{ScreenHeight}");
            if (!string.IsNullOrWhiteSpace(Proxy)) parts.Add("proxy");
            return parts.Count == 0 ? "—" : string.Join(" · ", parts);
        }
    }
}

/// <summary>一個 git commit 行（畀歷史 UI 用）· One git commit row for the history UI.</summary>
public sealed class CamoufoxCommit
{
    public string Hash { get; set; } = "";
    public string Date { get; set; } = "";
    public string Subject { get; set; } = "";
    public string ShortHash => Hash.Length >= 7 ? Hash[..7] : Hash;
}

/// <summary>
/// Camoufox 設定檔管理引擎 · Camoufox profile-manager engine. Stores each profile as files
/// (manifest + cookies/fingerprint userdata) under a <b>local git repo</b> that auto-commits on every
/// change, can export one / selected / all profiles, launches a profile through the Camoufox executable
/// (cloning + building it from source when absent), and exposes the full git history for management.
/// Fully managed C#; the only external process launched is Camoufox itself (plus git/python during setup).
/// </summary>
public static class CamoufoxService
{
    private static readonly string AppDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge");

    /// <summary>本地 git 倉庫（設定檔儲存）· The local git-backed profile store.</summary>
    public static string StoreDir => Path.Combine(AppDir, "camoufox");
    public static string ProfilesDir => Path.Combine(StoreDir, "profiles");
    /// <summary>Camoufox 原始碼 clone 位置 · Where the Camoufox source is cloned/built.</summary>
    public static string SourceDir => Path.Combine(AppDir, "camoufox-src");
    /// <summary>Camoufox 執行檔安裝位置 · Where a built/fetched Camoufox binary lives.</summary>
    public static string BinDir => Path.Combine(AppDir, "camoufox-bin");

    public const string RepoUrl = "https://github.com/daijro/camoufox";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    // ───────────────────────── git plumbing (shell out, same pattern as ConfigBackupService) ─────────────────────────

    public static bool RepoExists => Directory.Exists(Path.Combine(StoreDir, ".git"));

    private static async Task<(bool ok, string output)> Git(string args, CancellationToken ct = default)
    {
        var r = await ShellRunner.RunIn(StoreDir, "git", args, elevated: false, ct);
        return (r.Success, (r.Output ?? string.Empty).Trim());
    }

    /// <summary>確保倉庫存在 · Ensure the store directory + git repo exist (idempotent).</summary>
    public static async Task EnsureRepoAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(ProfilesDir);
        if (!RepoExists)
        {
            await Git("init", ct);
            await Git("config user.name WinForge", ct);
            await Git("config user.email winforge@localhost", ct);
            // Keep the profiles folder tracked even when empty, and document the store.
            var readme = Path.Combine(StoreDir, "README.md");
            if (!File.Exists(readme))
                File.WriteAllText(readme,
                    "# WinForge Camoufox profile store\n\n" +
                    "Every Camoufox profile (manifest + cookies/fingerprint userdata) is committed here.\n" +
                    "Each profile lives under `profiles/<id>/`. Do not edit by hand while WinForge is open.\n");
            await CommitAsync("Initialise Camoufox profile store", ct);
        }
    }

    /// <summary>影低改動：git add -A 再 commit · Stage everything and commit; skips when nothing changed.</summary>
    public static async Task<TweakResult> CommitAsync(string message, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(ProfilesDir);
            var (addOk, addOut) = await Git("add -A", ct);
            if (!addOk) return TweakResult.Fail("git add failed.", "git add 失敗。", addOut);

            var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var msg = $"{stamp} — {message}";
            var (cOk, cOut) = await Git($"commit -m \"{msg.Replace("\"", "'")}\"", ct);
            if (!cOk)
            {
                if (cOut.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase))
                    return TweakResult.Ok("No changes to commit.", "冇改動需要 commit。", cOut);
                return TweakResult.Fail("git commit failed.", "git commit 失敗。", cOut);
            }
            return TweakResult.Ok($"Committed: {message}", $"已 commit：{message}", cOut);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>有幾多項未 commit 嘅改動 · Number of uncommitted changes in the store.</summary>
    public static async Task<int> PendingChangesAsync(CancellationToken ct = default)
    {
        if (!RepoExists) return 0;
        var (ok, outp) = await Git("status --porcelain", ct);
        if (!ok || string.IsNullOrWhiteSpace(outp)) return 0;
        return outp.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    // ───────────────────────── remote (push on sync) ─────────────────────────

    public const string KeyRemote = "camoufox.remote.url";
    public const string KeyPushOnSync = "camoufox.remote.pushonsync";

    /// <summary>遠端倉庫網址（可含權杖）· Remote URL to push the profile store to (may embed a token).</summary>
    public static string RemoteUrl
    {
        get => SettingsStore.Get(KeyRemote, "");
        set => SettingsStore.Set(KeyRemote, value ?? "");
    }

    /// <summary>每次同步後自動 push · Whether every Sync also pushes to the remote.</summary>
    public static bool PushOnSync
    {
        get => SettingsStore.Get(KeyPushOnSync, "false") == "true";
        set => SettingsStore.Set(KeyPushOnSync, value ? "true" : "false");
    }

    public static bool HasRemote => !string.IsNullOrWhiteSpace(RemoteUrl);

    /// <summary>設定 origin 遠端（idempotent）· Point the 'origin' remote at RemoteUrl (add or update).</summary>
    private static async Task EnsureRemoteAsync(CancellationToken ct = default)
    {
        if (!HasRemote) return;
        var (ok, _) = await Git("remote get-url origin", ct);
        if (ok) await Git($"remote set-url origin \"{RemoteUrl}\"", ct);
        else await Git($"remote add origin \"{RemoteUrl}\"", ct);
    }

    /// <summary>將設定檔倉庫 push 上遠端 · Push the profile store to the configured remote.</summary>
    public static async Task<TweakResult> PushAsync(CancellationToken ct = default)
    {
        if (!HasRemote)
            return TweakResult.Fail("No remote configured — set a remote URL first.", "未設定遠端 — 請先填遠端網址。");
        try
        {
            await EnsureRepoAsync(ct);
            await EnsureRemoteAsync(ct);
            // Push the current branch and set upstream; works for a fresh master/main alike.
            var (ok, outp) = await Git("push -u origin HEAD", ct);
            return ok
                ? TweakResult.Ok("Pushed the profile store to the remote.", "已將設定檔倉庫 push 上遠端。", outp)
                : TweakResult.Fail("git push failed (check the URL / credentials).", "git push 失敗（請檢查網址／憑證）。", outp);
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>「同步」= 將所有待處理改動 commit（並按設定 push）· "Sync" — commit every pending change, then push if enabled.</summary>
    public static async Task<TweakResult> SyncAsync(string? note = null, CancellationToken ct = default)
    {
        await EnsureRepoAsync(ct);
        int pending = await PendingChangesAsync(ct);
        TweakResult commitResult = pending == 0
            ? TweakResult.Ok("Already in sync — nothing to commit.", "已經同步 — 冇嘢需要 commit。")
            : await CommitAsync(string.IsNullOrWhiteSpace(note) ? $"Sync ({pending} change(s))" : $"Sync — {note}", ct);

        // Push on every sync when a remote is configured and the option is on.
        if (HasRemote && PushOnSync)
        {
            var push = await PushAsync(ct);
            var combined = (commitResult.Output ?? "") + "\n" + (push.Output ?? "");
            return push.Success
                ? TweakResult.Ok("Synced and pushed to the remote.", "已同步並 push 上遠端。", combined)
                : TweakResult.Fail("Committed, but the push failed (check URL / credentials).",
                                   "已 commit，但 push 失敗（請檢查網址／憑證）。", combined);
        }
        return commitResult;
    }

    public static async Task<List<CamoufoxCommit>> ListCommitsAsync(CancellationToken ct = default)
    {
        var list = new List<CamoufoxCommit>();
        if (!RepoExists) return list;
        var (ok, outp) = await Git("log --pretty=format:%H%x09%ad%x09%s --date=iso -n 500", ct);
        if (!ok) return list;
        foreach (var raw in outp.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = raw.Split('\t');
            if (parts.Length < 3) continue;
            list.Add(new CamoufoxCommit { Hash = parts[0].Trim(), Date = parts[1].Trim(), Subject = parts[2].Trim() });
        }
        return list;
    }

    public static async Task<TweakResult> DiffCommitAsync(string commit, CancellationToken ct = default)
    {
        if (!RepoExists) return TweakResult.Fail("No repo yet.", "未有倉庫。");
        var (_, outp) = await Git($"show --stat --pretty=format:%H%n%an%n%ad%n%s%n {commit}", ct);
        return string.IsNullOrWhiteSpace(outp)
            ? TweakResult.Ok("No details.", "冇詳情。", "")
            : TweakResult.Ok("Diff computed.", "已計算差異。", outp);
    }

    /// <summary>還原到某個 commit（事先自動影一個安全 commit）· Restore the store to a commit (auto-commits current state first).</summary>
    public static async Task<TweakResult> RestoreCommitAsync(string commit, CancellationToken ct = default)
    {
        if (!RepoExists) return TweakResult.Fail("No repo yet.", "未有倉庫。");
        try
        {
            await CommitAsync("auto: before restore", ct);
            var (ok, outp) = await Git($"checkout {commit} -- .", ct);
            if (!ok) return TweakResult.Fail("Restore failed.", "還原失敗。", outp);
            await CommitAsync($"Restore to {(commit.Length >= 7 ? commit[..7] : commit)}", ct);
            return TweakResult.Ok($"Restored profiles to {(commit.Length >= 7 ? commit[..7] : commit)}.",
                $"已還原設定檔到 {(commit.Length >= 7 ? commit[..7] : commit)}。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    // ───────────────────────── profile CRUD (every mutation commits) ─────────────────────────

    private static string ProfileDir(string id) => Path.Combine(ProfilesDir, id);
    private static string ManifestPath(string id) => Path.Combine(ProfileDir(id), "profile.json");
    public static string UserDataDir(string id) => Path.Combine(ProfileDir(id), "userdata");

    public static async Task<List<CamoufoxProfile>> ListProfilesAsync(CancellationToken ct = default)
    {
        await EnsureRepoAsync(ct);
        var list = new List<CamoufoxProfile>();
        if (!Directory.Exists(ProfilesDir)) return list;
        foreach (var dir in Directory.EnumerateDirectories(ProfilesDir))
        {
            var manifest = Path.Combine(dir, "profile.json");
            if (!File.Exists(manifest)) continue;
            try
            {
                var p = JsonSerializer.Deserialize<CamoufoxProfile>(File.ReadAllText(manifest), JsonOpts);
                if (p is not null && !string.IsNullOrEmpty(p.Id)) list.Add(p);
            }
            catch { /* skip a corrupt manifest, fail open */ }
        }
        return list.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static CamoufoxProfile NewProfile(string name) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = string.IsNullOrWhiteSpace(name) ? "New profile" : name.Trim(),
        CreatedUtc = DateTime.UtcNow.ToString("o"),
        UpdatedUtc = DateTime.UtcNow.ToString("o"),
        OsName = "windows",
        Locale = "en-US",
    };

    /// <summary>新增或更新一個設定檔，然後 commit · Create/update a profile, then commit the change.</summary>
    public static async Task<TweakResult> SaveProfileAsync(CamoufoxProfile profile, CancellationToken ct = default)
    {
        try
        {
            await EnsureRepoAsync(ct);
            if (string.IsNullOrWhiteSpace(profile.Id)) profile.Id = Guid.NewGuid().ToString("N");
            bool isNew = !File.Exists(ManifestPath(profile.Id));
            if (isNew && string.IsNullOrWhiteSpace(profile.CreatedUtc)) profile.CreatedUtc = DateTime.UtcNow.ToString("o");
            profile.UpdatedUtc = DateTime.UtcNow.ToString("o");

            Directory.CreateDirectory(ProfileDir(profile.Id));
            Directory.CreateDirectory(UserDataDir(profile.Id));
            File.WriteAllText(ManifestPath(profile.Id), JsonSerializer.Serialize(profile, JsonOpts));

            var verb = isNew ? "Create" : "Edit";
            var r = await CommitAsync($"{verb} profile \"{profile.Name}\" ({profile.ShortId})", ct);
            return r.Success
                ? TweakResult.Ok($"Saved profile \"{profile.Name}\".", $"已儲存設定檔「{profile.Name}」。", r.Output)
                : r;
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    public static async Task<TweakResult> DeleteProfileAsync(CamoufoxProfile profile, CancellationToken ct = default)
    {
        try
        {
            var dir = ProfileDir(profile.Id);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            var r = await CommitAsync($"Delete profile \"{profile.Name}\" ({profile.ShortId})", ct);
            return r.Success
                ? TweakResult.Ok($"Deleted profile \"{profile.Name}\".", $"已刪除設定檔「{profile.Name}」。", r.Output)
                : r;
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    // ───────────────────────── export / import (zip) ─────────────────────────

    /// <summary>匯出一個設定檔做 .zip · Export one profile folder to a .zip.</summary>
    public static Task<TweakResult> ExportProfileAsync(CamoufoxProfile profile, string zipPath, CancellationToken ct = default)
        => ExportProfilesAsync(new[] { profile }, zipPath, ct);

    /// <summary>匯出選定／全部設定檔做一個 .zip · Export selected (or all) profiles into a single .zip.</summary>
    public static async Task<TweakResult> ExportProfilesAsync(IEnumerable<CamoufoxProfile> profiles, string zipPath, CancellationToken ct = default)
    {
        try
        {
            var list = profiles.ToList();
            if (list.Count == 0) return TweakResult.Fail("No profiles selected.", "未揀任何設定檔。");
            await Task.Run(() =>
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
                using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                foreach (var p in list)
                {
                    var dir = ProfileDir(p.Id);
                    if (!Directory.Exists(dir)) continue;
                    foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        var rel = Path.Combine(p.Id, Path.GetRelativePath(dir, file)).Replace('\\', '/');
                        zip.CreateEntryFromFile(file, rel);
                    }
                }
            }, ct);
            return TweakResult.Ok($"Exported {list.Count} profile(s) to {Path.GetFileName(zipPath)}.",
                $"已匯出 {list.Count} 個設定檔到 {Path.GetFileName(zipPath)}。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    public static async Task<TweakResult> ExportAllAsync(string zipPath, CancellationToken ct = default)
        => await ExportProfilesAsync(await ListProfilesAsync(ct), zipPath, ct);

    /// <summary>由 .zip 匯入設定檔，然後 commit · Import profiles from a .zip, then commit.</summary>
    public static async Task<TweakResult> ImportAsync(string zipPath, CancellationToken ct = default)
    {
        try
        {
            await EnsureRepoAsync(ct);
            int imported = await Task.Run(() =>
            {
                using var zip = ZipFile.OpenRead(zipPath);
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var root = Path.GetFullPath(ProfilesDir);
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry
                    var dest = Path.GetFullPath(Path.Combine(ProfilesDir, entry.FullName));
                    if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue; // zip-slip guard
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    entry.ExtractToFile(dest, overwrite: true);
                    var top = entry.FullName.Replace('\\', '/').Split('/')[0];
                    if (!string.IsNullOrWhiteSpace(top)) ids.Add(top);
                }
                return ids.Count;
            }, ct);
            var r = await CommitAsync($"Import {imported} profile(s) from {Path.GetFileName(zipPath)}", ct);
            return TweakResult.Ok($"Imported {imported} profile(s).", $"已匯入 {imported} 個設定檔。", r.Output);
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    // ───────────────────────── Camoufox engine: detect / clone+build / launch ─────────────────────────

    /// <summary>搵 Camoufox 執行檔 · Locate a Camoufox executable, or null if it isn't installed yet.</summary>
    public static string? LocateExecutable()
    {
        var candidates = new List<string>
        {
            Path.Combine(BinDir, "camoufox.exe"),
            Path.Combine(BinDir, "camoufox", "camoufox.exe"),
            Path.Combine(SourceDir, "dist", "camoufox.exe"),
            Environment.ExpandEnvironmentVariables(@"%LocalAppData%\camoufox\camoufox.exe"),
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Camoufox\camoufox.exe"),
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        // Python package install: %LocalAppData%\camoufox or the pip cache.
        try
        {
            var pyCache = Environment.ExpandEnvironmentVariables(@"%LocalAppData%\camoufox");
            if (Directory.Exists(pyCache))
            {
                var hit = Directory.EnumerateFiles(pyCache, "camoufox.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (hit is not null) return hit;
            }
        }
        catch { }
        return null;
    }

    public static bool IsInstalled => LocateExecutable() is not null;

    private static string? FindPython()
    {
        foreach (var name in new[] { "python", "python3", "py" })
        {
            try
            {
                var psi = new ProcessStartInfo { FileName = name, Arguments = "--version", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                using var p = Process.Start(psi);
                if (p is null) continue;
                p.WaitForExit(4000);
                if (p.ExitCode == 0) return name;
            }
            catch { }
        }
        return null;
    }

    /// <summary>
    /// 自動 clone 並由原始碼建置 Camoufox · Clone the Camoufox repo and build it from source, streaming
    /// progress. A full Firefox-patch build is heavy, so after cloning we bootstrap a runnable engine via
    /// the project's Python fetcher (<c>python -m camoufox fetch</c>) as a fallback. All steps are logged.
    /// </summary>
    public static async Task<TweakResult> CloneAndBuildAsync(Action<string> onLog, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(BinDir);
            void Log(string s) => onLog(s);

            // 1. git clone (shallow) — the only external setup tool besides python.
            if (!Directory.Exists(Path.Combine(SourceDir, ".git")))
            {
                Log($"$ git clone --depth 1 {RepoUrl}");
                var clone = await ShellRunner.Run("git", $"clone --depth 1 {RepoUrl} \"{SourceDir}\"", false, ct);
                Log(clone.Output ?? "");
                if (!clone.Success) return TweakResult.Fail("git clone failed.", "git clone 失敗。", clone.Output);
            }
            else
            {
                Log("$ git -C <src> pull (already cloned)");
                var pull = await ShellRunner.RunIn(SourceDir, "git", "pull --ff-only", false, ct);
                Log(pull.Output ?? "");
            }

            // 2. Build from source. The repo's source build needs Python + a Mozilla build env; we drive it
            //    through Python and fall back to fetching a prebuilt engine so the manager is always usable.
            var py = FindPython();
            if (py is null)
                return TweakResult.Fail(
                    "Cloned, but Python was not found — install Python to build/fetch Camoufox.",
                    "已 clone，但搵唔到 Python — 請先安裝 Python 先可以建置／取得 Camoufox。");

            Log($"$ {py} -m pip install --upgrade camoufox[geoip]");
            var pip = await ShellRunner.RunIn(SourceDir, py, "-m pip install --upgrade camoufox[geoip]", false, ct);
            Log(pip.Output ?? "");

            Log($"$ {py} -m camoufox fetch   (download/build the Camoufox engine)");
            var fetch = await ShellRunner.RunIn(SourceDir, py, "-m camoufox fetch", false, ct);
            Log(fetch.Output ?? "");

            if (LocateExecutable() is { } exe)
            {
                Log($"Camoufox ready: {exe}");
                return TweakResult.Ok($"Camoufox is ready ({exe}).", $"Camoufox 已就緒（{exe}）。");
            }
            return TweakResult.Fail(
                "Build/fetch finished but no camoufox.exe was found. Check the log.",
                "建置／取得完成，但搵唔到 camoufox.exe。請睇記錄。",
                (pip.Output ?? "") + "\n" + (fetch.Output ?? ""));
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>合併設定檔嘅指紋成 Camoufox 設定 JSON · Build the CAMOU_CONFIG JSON from a profile.</summary>
    public static string BuildCamouConfig(CamoufoxProfile p)
    {
        var dict = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(p.UserAgent)) dict["navigator.userAgent"] = p.UserAgent;
        if (!string.IsNullOrWhiteSpace(p.Locale)) { dict["locale:language"] = p.Locale; dict["navigator.language"] = p.Locale; }
        if (!string.IsNullOrWhiteSpace(p.Timezone)) dict["timezone"] = p.Timezone;
        if (int.TryParse(p.ScreenWidth, out var w)) dict["screen.width"] = w;
        if (int.TryParse(p.ScreenHeight, out var h)) dict["screen.height"] = h;
        if (!string.IsNullOrWhiteSpace(p.OsName)) dict["os"] = p.OsName;

        // Raw extra config overrides the convenience fields.
        if (!string.IsNullOrWhiteSpace(p.ConfigJson))
        {
            try
            {
                var extra = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(p.ConfigJson);
                if (extra is not null)
                    foreach (var kv in extra) dict[kv.Key] = kv.Value;
            }
            catch { /* ignore invalid raw JSON, keep the structured fields */ }
        }
        return JsonSerializer.Serialize(dict);
    }

    /// <summary>
    /// 用某個設定檔啟動 Camoufox · Launch Camoufox with a profile's userdata dir + fingerprint config.
    /// This is the ONLY app WinForge launches. A windowed browser must not have its stdio redirected
    /// (the frozen-exe lesson), so we start it detached with env vars only.
    /// </summary>
    public static async Task<TweakResult> LaunchProfileAsync(CamoufoxProfile profile, CancellationToken ct = default)
    {
        var exe = LocateExecutable();
        if (exe is null)
            return TweakResult.Fail(
                "Camoufox isn't installed yet — clone & build it from the Engine tab first.",
                "Camoufox 仲未安裝 — 請先喺「引擎」分頁 clone 並建置。");
        try
        {
            // Make sure the userdata dir exists and the profile is committed before launch.
            Directory.CreateDirectory(UserDataDir(profile.Id));
            await CommitAsync($"Launch profile \"{profile.Name}\" ({profile.ShortId})", ct);

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,            // needed to pass env vars
                RedirectStandardOutput = false,     // windowed app: never redirect stdio
                RedirectStandardError = false,
                CreateNoWindow = false,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? BinDir,
            };
            psi.ArgumentList.Add("-no-remote");
            psi.ArgumentList.Add("-new-instance");
            psi.ArgumentList.Add("-profile");
            psi.ArgumentList.Add(UserDataDir(profile.Id));

            var config = BuildCamouConfig(profile);
            psi.EnvironmentVariables["CAMOU_CONFIG"] = config;
            psi.EnvironmentVariables["CAMOU_CONFIG_1"] = config; // Camoufox reads chunked CAMOU_CONFIG_N
            if (!string.IsNullOrWhiteSpace(profile.Proxy))
                psi.EnvironmentVariables["CAMOU_PROXY"] = profile.Proxy;

            using var p = new Process { StartInfo = psi };
            if (!p.Start())
                return TweakResult.Fail("Failed to start Camoufox.", "啟動 Camoufox 失敗。");

            return TweakResult.Ok($"Launched \"{profile.Name}\" in Camoufox.", $"已用 Camoufox 開啟「{profile.Name}」。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }
}
