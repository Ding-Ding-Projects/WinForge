using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 「原生 app 彈窗啟動器」引擎 · The engine behind the native-app popup launcher. For apps that cannot be
/// reimplemented in-app it (1) resolves the real executable across PATH / App Paths registry / known install
/// dirs / winget shims, (2) installs the WHOLE dependency chain touchlessly via <see cref="PackageService"/>
/// (dependencies first, the app last) with live streaming progress, and (3) launches the app in its own
/// native window. Pure managed C#; never links the upstream program's code. Bilingual, never throws.
/// </summary>
public static class ExternalAppService
{
    // Resolved-path cache (successful resolutions only). Cleared per-spec on Rescan after an install.
    private static readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _gate = new();

    private static string SettingsKey(ExternalAppSpec spec) => $"applauncher.{spec.Id}.exe";

    // ── Detection ────────────────────────────────────────────────────────────

    /// <summary>
    /// 解析真正嘅執行檔 · Resolve the app's executable: a user override, then the App Paths registry, then
    /// PATH stems, then absolute candidates (glob-aware), then the winget Links shim. Returns null if the
    /// app is not installed.
    /// </summary>
    public static string? ResolveExe(ExternalAppSpec spec)
    {
        lock (_gate)
            if (_cache.TryGetValue(spec.Id, out var cached) && File.Exists(cached))
                return cached;

        var found = ResolveUncached(spec);
        if (found is not null)
            lock (_gate) _cache[spec.Id] = found;
        return found;
    }

    private static string? ResolveUncached(ExternalAppSpec spec)
    {
        // 1. explicit user override
        try
        {
            var saved = SettingsStore.Get(SettingsKey(spec), "");
            if (!string.IsNullOrWhiteSpace(saved) && File.Exists(saved)) return saved;
        }
        catch { }

        // 2. App Paths registry (HKLM + HKCU) — the most reliable place a GUI installer registers its exe.
        foreach (var name in spec.AppPathsExe)
        {
            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                try
                {
                    using var k = root.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + name);
                    var p = k?.GetValue(null) as string;
                    p = p?.Trim().Trim('"');
                    if (!string.IsNullOrWhiteSpace(p) && File.Exists(p)) return p;
                }
                catch { }
            }
        }

        // 3. PATH stems (probe .exe/.cmd/.bat) + the per-user winget Links shim.
        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var stem in spec.PathStems)
        {
            foreach (var dir in pathDirs)
            {
                foreach (var ext in new[] { ".exe", ".cmd", ".bat" })
                {
                    try
                    {
                        var c = Path.Combine(dir.Trim(), stem + ext);
                        if (File.Exists(c)) return c;
                    }
                    catch { }
                }
            }
            try
            {
                var links = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "WinGet", "Links", stem + ".exe");
                if (File.Exists(links)) return links;
            }
            catch { }
        }

        // 4. Absolute candidates (env vars expanded, single-* wildcard supported), newest match first.
        foreach (var pattern in spec.Candidates)
        {
            var hit = ExpandGlob(pattern).FirstOrDefault();
            if (hit is not null) return hit;
        }

        return null;
    }

    /// <summary>已安裝？（解析到執行檔）· Installed? (the executable resolves).</summary>
    public static bool IsInstalled(ExternalAppSpec spec) => ResolveExe(spec) is not null;

    /// <summary>清快取 + 刷新本程序 PATH（啱啱裝完之後叫）· Clear the cache + refresh this process's PATH
    /// (call right after an install so the new exe resolves without an app restart).</summary>
    public static void Rescan(ExternalAppSpec? spec = null)
    {
        lock (_gate)
        {
            if (spec is null) _cache.Clear();
            else _cache.Remove(spec.Id);
        }
        PackageService.RefreshProcessPath();
    }

    /// <summary>記住使用者手動揀嘅路徑 · Persist a user-picked executable path for this app.</summary>
    public static void SetPathOverride(ExternalAppSpec spec, string exe)
    {
        try { SettingsStore.Set(SettingsKey(spec), exe); } catch { }
        lock (_gate)
        {
            if (File.Exists(exe)) _cache[spec.Id] = exe;
            else _cache.Remove(spec.Id);
        }
    }

    // ── Install the whole dependency chain ───────────────────────────────────

    /// <summary>
    /// 全自動安裝成條相依鏈 · Touchlessly install the whole chain (dependencies first, the app last) via
    /// winget, reporting per-step percent + streamed output. A required step failing aborts the chain and
    /// surfaces winget's real error; an optional step failing is logged and skipped. Refreshes PATH + the
    /// resolve cache at the end. Never throws (except on cancellation).
    /// </summary>
    public static async Task<TweakResult> InstallAllAsync(ExternalAppSpec spec,
        IProgress<InstallProgressReport>? progress, CancellationToken ct = default)
    {
        var deps = spec.Dependencies;
        if (deps.Count == 0)
            return TweakResult.Fail($"No installer is configured for {spec.NameEn}.",
                $"未為 {spec.NameZh} 設定安裝程式。");

        var log = new StringBuilder();
        int total = deps.Count;
        int done = 0;
        int optionalSkipped = 0;

        foreach (var dep in deps)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(InstallProgressReport.Progress(
                done * 100.0 / total, $"Installing {dep.En}…", $"安裝緊 {dep.Zh}…"));

            // Stream winget's live output into the status line (keeps the current percent).
            var onLine = new Progress<string>(l => progress?.Report(InstallProgressReport.FromLine(l)));

            TweakResult r;
            try { r = await PackageService.AutoInstallDetailed(dep.WingetId, onLine, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { r = TweakResult.Fail(ex.Message, $"出錯：{ex.Message}", ex.Message); }

            log.AppendLine($"[{dep.WingetId}] {(r.Success ? "OK" : "FAILED")}");
            if (!string.IsNullOrWhiteSpace(r.Output)) log.AppendLine(r.Output!.Trim());

            if (!r.Success)
            {
                if (dep.Optional)
                {
                    optionalSkipped++;
                    progress?.Report(InstallProgressReport.Status(
                        $"Skipped optional {dep.En}.", $"已略過可選項 {dep.Zh}。"));
                }
                else
                {
                    Rescan(spec);
                    return TweakResult.Fail(
                        $"Couldn't install {dep.En}. {r.Message?.En}".Trim(),
                        $"安裝 {dep.Zh} 失敗。{r.Message?.Zh}".Trim(),
                        log.ToString());
                }
            }

            done++;
            progress?.Report(InstallProgressReport.Progress(
                done * 100.0 / total, $"{dep.En} ready.", $"{dep.Zh} 已就緒。"));
        }

        Rescan(spec);
        bool installed = IsInstalled(spec);
        progress?.Report(InstallProgressReport.Progress(100,
            installed ? $"{spec.NameEn} is ready to launch." : $"{spec.NameEn} installed.",
            installed ? $"{spec.NameZh} 已可啟動。" : $"{spec.NameZh} 已安裝。"));

        string note = optionalSkipped > 0 ? $" ({optionalSkipped} optional skipped)" : "";
        string noteZh = optionalSkipped > 0 ? $"（略過 {optionalSkipped} 個可選項）" : "";
        return TweakResult.Ok(
            $"{spec.NameEn} and its dependencies are installed.{note}",
            $"{spec.NameZh} 同其相依項已安裝。{noteZh}",
            log.ToString());
    }

    // ── Launch the native app as a popup ─────────────────────────────────────

    /// <summary>
    /// 以原生視窗啟動 app · Launch the app in its own native window (a "popup" outside the WinForge shell).
    /// Uses ShellExecute and sets the working directory to the exe folder (required by apps like OBS).
    /// </summary>
    public static TweakResult Launch(ExternalAppSpec spec, string? extraArgs = null)
    {
        var exe = ResolveExe(spec);
        if (exe is null)
            return TweakResult.Fail($"{spec.NameEn} is not installed yet.",
                $"仲未安裝 {spec.NameZh}。");
        try
        {
            var args = string.Join(" ",
                new[] { spec.LaunchArgs, extraArgs }.Where(a => !string.IsNullOrWhiteSpace(a)));
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
            });
            return TweakResult.Ok($"Launched {spec.NameEn}.", $"已啟動 {spec.NameZh}。", exe);
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}", ex.Message); }
    }

    // ── glob helper ──────────────────────────────────────────────────────────

    /// <summary>
    /// 展開一個可含單個 <c>*</c> 段嘅路徑樣式 · Expand a path pattern whose segments may each contain a
    /// <c>*</c> wildcard (e.g. <c>%ProgramFiles%\Blender Foundation\*\blender.exe</c>). Returns existing
    /// files, newest write-time first. Non-wildcard patterns just test <see cref="File.Exists"/>.
    /// </summary>
    private static IEnumerable<string> ExpandGlob(string pattern)
    {
        string expanded;
        try { expanded = Environment.ExpandEnvironmentVariables(pattern); } catch { yield break; }

        if (!expanded.Contains('*'))
        {
            if (SafeFileExists(expanded)) yield return expanded;
            yield break;
        }

        var parts = expanded.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) yield break;

        // Seed with the drive/root (e.g. "C:\").
        var roots = new List<string> { parts[0].EndsWith(':') ? parts[0] + Path.DirectorySeparatorChar : parts[0] };
        for (int i = 1; i < parts.Length; i++)
        {
            var part = parts[i];
            bool last = i == parts.Length - 1;
            var next = new List<string>();
            foreach (var root in roots)
            {
                try
                {
                    if (part.Contains('*'))
                    {
                        if (last) next.AddRange(Directory.EnumerateFiles(root, part));
                        else next.AddRange(Directory.EnumerateDirectories(root, part));
                    }
                    else
                    {
                        var combined = Path.Combine(root, part);
                        if (last) { if (File.Exists(combined)) next.Add(combined); }
                        else { if (Directory.Exists(combined)) next.Add(combined); }
                    }
                }
                catch { /* unreadable dir — skip */ }
            }
            roots = next;
            if (roots.Count == 0) yield break;
        }

        foreach (var f in roots.OrderByDescending(SafeWriteTime))
            yield return f;
    }

    private static bool SafeFileExists(string p) { try { return File.Exists(p); } catch { return false; } }
    private static DateTime SafeWriteTime(string p) { try { return File.GetLastWriteTimeUtc(p); } catch { return DateTime.MinValue; } }
}
