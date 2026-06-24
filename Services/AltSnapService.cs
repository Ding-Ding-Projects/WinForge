using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// AltSnap 管理服務 · Manager service that wraps the real AltSnap.exe binary (RamonUnch.AltSnap).
/// 偵測安裝、啟動／結束／重啟、開機自啟動、同埋讀寫 AltSnap.ini 設定。
/// Detects the install, controls lifecycle (launch / quit / restart / reload), toggles run-at-startup
/// (HKCU\…\Run), and reads/writes the AltSnap.ini config so a WinForge front-end can edit the high-value
/// keys. AltSnap itself is GPL — we never relink its code; we only install and drive the official binary.
/// Bilingual throughout.
/// </summary>
public static class AltSnapService
{
    public const string WingetId = "RamonUnch.AltSnap";
    public const string ProcessName = "AltSnap"; // image name without extension
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "AltSnap";

    private static string? _cachedExe;

    // ===================== detection =====================

    /// <summary>
    /// 搵 AltSnap.exe · Locate AltSnap.exe. Order: cached → registry uninstall key →
    /// %ProgramFiles%\AltSnap → winget package folders / LocalAppData → PATH. Returns null when absent.
    /// </summary>
    public static async Task<string?> LocateAsync(CancellationToken ct = default)
    {
        if (_cachedExe is not null && File.Exists(_cachedExe)) return _cachedExe;
        _cachedExe = null;

        // 1) Registry uninstall keys (InstallLocation / DisplayIcon).
        foreach (var hit in FromUninstallKeys())
            if (File.Exists(hit)) return _cachedExe = hit;

        // 2) Well-known install dirs.
        var direct = new[]
        {
            Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files", "AltSnap", "AltSnap.exe"),
            Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)", "AltSnap", "AltSnap.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AltSnap", "AltSnap.exe"),
        };
        foreach (var p in direct)
            if (File.Exists(p)) return _cachedExe = p;

        // 3) On PATH?
        try
        {
            var where = await ShellRunner.Capture("where.exe", "AltSnap.exe", ct);
            var first = where.Replace("\r", "").Split('\n')
                .FirstOrDefault(l => l.Trim().EndsWith("AltSnap.exe", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(first) && File.Exists(first.Trim()))
                return _cachedExe = first.Trim();
        }
        catch { /* ignore */ }

        // 4) Bounded search of winget package roots (winget drops a versioned folder).
        var roots = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetEnvironmentVariable("ProgramData") ?? @"C:\ProgramData",
        };
        foreach (var root in roots.Distinct())
        {
            try
            {
                if (!Directory.Exists(root)) continue;
                foreach (var hit in SafeEnumerate(root, "AltSnap.exe", 6))
                    if (File.Exists(hit)) return _cachedExe = hit;
            }
            catch { /* ignore */ }
        }
        return null;
    }

    public static void InvalidateCache() => _cachedExe = null;

    public static async Task<bool> IsInstalled(CancellationToken ct = default)
        => await LocateAsync(ct) is not null;

    private static IEnumerable<string> FromUninstallKeys()
    {
        string[] keys =
        {
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AltSnap",
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AltSnap_is1",
            @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\AltSnap",
            @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\AltSnap_is1",
        };
        foreach (var root in new[] { RegRoot.HKLM, RegRoot.HKCU })
        {
            foreach (var key in keys)
            {
                var loc = RegistryHelper.GetValue(root, key, "InstallLocation") as string;
                if (!string.IsNullOrWhiteSpace(loc))
                {
                    var exe = Path.Combine(loc.Trim().Trim('"'), "AltSnap.exe");
                    if (File.Exists(exe)) yield return exe;
                }
                var icon = RegistryHelper.GetValue(root, key, "DisplayIcon") as string;
                if (!string.IsNullOrWhiteSpace(icon))
                {
                    var exe = icon.Split(',')[0].Trim().Trim('"');
                    if (exe.EndsWith("AltSnap.exe", StringComparison.OrdinalIgnoreCase) && File.Exists(exe))
                        yield return exe;
                }
            }
        }
    }

    private static IEnumerable<string> SafeEnumerate(string root, string pattern, int maxDepth)
    {
        var stack = new Stack<(string dir, int depth)>();
        stack.Push((root, 0));
        while (stack.Count > 0)
        {
            var (dir, depth) = stack.Pop();
            string[] files = Array.Empty<string>();
            try { files = Directory.GetFiles(dir, pattern); } catch { }
            foreach (var f in files) yield return f;
            if (depth >= maxDepth) continue;
            string[] subs = Array.Empty<string>();
            try { subs = Directory.GetDirectories(dir); } catch { }
            foreach (var s in subs) stack.Push((s, depth + 1));
        }
    }

    /// <summary>讀取版本（檔案版本資訊）· Read the file version of AltSnap.exe (or empty).</summary>
    public static async Task<string> VersionAsync(CancellationToken ct = default)
    {
        var exe = await LocateAsync(ct);
        if (exe is null) return "";
        try
        {
            var fvi = FileVersionInfo.GetVersionInfo(exe);
            return fvi.ProductVersion ?? fvi.FileVersion ?? "";
        }
        catch { return ""; }
    }

    // ===================== install =====================

    /// <summary>用 winget 安裝 AltSnap · Install AltSnap via winget (RamonUnch.AltSnap).</summary>
    public static async Task<bool> InstallViaWinget(CancellationToken ct = default)
    {
        var ok = await PackageService.AutoInstall(WingetId, ct);
        InvalidateCache();
        return ok;
    }

    // ===================== lifecycle =====================

    /// <summary>AltSnap 而家係咪行緊 · Whether an AltSnap process is currently running.</summary>
    public static bool IsRunning()
    {
        try { return Process.GetProcessesByName(ProcessName).Length > 0; }
        catch { return false; }
    }

    /// <summary>啟動 AltSnap · Launch AltSnap. elevated=true 經 UAC 以管理員身分啟動（可控制管理員視窗）。</summary>
    public static async Task<TweakResult> Launch(bool elevated, CancellationToken ct = default)
    {
        var exe = await LocateAsync(ct);
        if (exe is null)
            return TweakResult.Fail("AltSnap.exe not found — install it first.", "搵唔到 AltSnap.exe — 請先安裝。");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
                UseShellExecute = true,
            };
            if (elevated && !AdminHelper.IsElevated) psi.Verb = "runas";
            Process.Start(psi);
            return TweakResult.Ok(
                elevated ? "AltSnap launched (elevated)." : "AltSnap launched.",
                elevated ? "AltSnap 已啟動（管理員）。" : "AltSnap 已啟動。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not launch AltSnap: {ex.Message}", $"無法啟動 AltSnap：{ex.Message}");
        }
    }

    /// <summary>結束 AltSnap · Quit every AltSnap instance (taskkill — AltSnap has no clean quit flag).</summary>
    public static async Task<TweakResult> Quit(CancellationToken ct = default)
    {
        if (!IsRunning())
            return TweakResult.Ok("AltSnap was not running.", "AltSnap 本來就冇行。");
        try
        {
            int killed = 0;
            foreach (var p in Process.GetProcessesByName(ProcessName))
            {
                try { p.Kill(entireProcessTree: false); killed++; }
                catch { /* likely elevated — fall through to taskkill */ }
            }
            // If some instances were elevated, the in-proc Kill above fails; try an elevated taskkill.
            if (IsRunning())
                await ShellRunner.RunCmd($"taskkill /f /im {ProcessName}.exe", elevated: true, ct);

            return TweakResult.Ok($"AltSnap quit ({Math.Max(killed, 1)} instance(s)).",
                $"AltSnap 已結束（{Math.Max(killed, 1)} 個）。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not quit AltSnap: {ex.Message}", $"無法結束 AltSnap：{ex.Message}");
        }
    }

    /// <summary>重啟 AltSnap（結束再啟動）· Restart AltSnap (quit, then relaunch).</summary>
    public static async Task<TweakResult> Restart(bool elevated, CancellationToken ct = default)
    {
        await Quit(ct);
        // Give the old hook time to release.
        await Task.Delay(400, ct);
        return await Launch(elevated, ct);
    }

    /// <summary>重新載入設定（-r）· Ask the running AltSnap to reload its settings (CLI -r), restart if absent.</summary>
    public static async Task<TweakResult> ReloadSettings(bool elevated, CancellationToken ct = default)
    {
        var exe = await LocateAsync(ct);
        if (exe is null)
            return TweakResult.Fail("AltSnap.exe not found.", "搵唔到 AltSnap.exe。");
        if (!IsRunning())
            return await Launch(elevated, ct);
        try
        {
            // AltSnap -r reloads the ini in the already-running instance.
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "-r",
                WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
                UseShellExecute = true,
            };
            if (elevated && !AdminHelper.IsElevated) psi.Verb = "runas";
            Process.Start(psi);
            return TweakResult.Ok("Reloaded AltSnap settings.", "已重新載入 AltSnap 設定。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Reload failed: {ex.Message}", $"重新載入失敗：{ex.Message}");
        }
    }

    /// <summary>開啟 AltSnap 內建設定對話框（-c）· Open AltSnap's own settings dialog (CLI -c).</summary>
    public static async Task<TweakResult> OpenAdvancedSettings(CancellationToken ct = default)
    {
        var exe = await LocateAsync(ct);
        if (exe is null)
            return TweakResult.Fail("AltSnap.exe not found.", "搵唔到 AltSnap.exe。");
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "-c",
                WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
                UseShellExecute = true,
            });
            return TweakResult.Ok("Opened AltSnap settings.", "已開啟 AltSnap 設定。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not open settings: {ex.Message}", $"無法開啟設定：{ex.Message}");
        }
    }

    // ===================== run at startup =====================

    /// <summary>開機自啟動係咪開咗 · Whether AltSnap is set to launch at login (HKCU\…\Run).</summary>
    public static bool IsRunAtStartupEnabled()
        => RegistryHelper.GetValue(RegRoot.HKCU, RunKey, RunValueName) is string s && !string.IsNullOrWhiteSpace(s);

    /// <summary>
    /// 設定開機自啟動 · Enable/disable run-at-startup via HKCU\…\Run. Launches hidden-to-tray (-h)
    /// so it sits quietly in the notification area on login.
    /// </summary>
    public static async Task<TweakResult> SetRunAtStartup(bool enabled, CancellationToken ct = default)
    {
        if (!enabled)
        {
            RegistryHelper.DeleteValue(RegRoot.HKCU, RunKey, RunValueName);
            return TweakResult.Ok("Run at startup disabled.", "已關閉開機自啟動。");
        }
        var exe = await LocateAsync(ct);
        if (exe is null)
            return TweakResult.Fail("AltSnap.exe not found — install it first.", "搵唔到 AltSnap.exe — 請先安裝。");
        try
        {
            RegistryHelper.SetValue(RegRoot.HKCU, RunKey, RunValueName, $"\"{exe}\"", RegistryValueKind.String);
            return TweakResult.Ok("Run at startup enabled.", "已啟用開機自啟動。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not set startup entry: {ex.Message}", $"無法設定開機項目：{ex.Message}");
        }
    }

    // ===================== ini location =====================

    /// <summary>
    /// 搵 AltSnap.ini · Locate the active AltSnap.ini. AltSnap reads it next to AltSnap.exe by default,
    /// and falls back to %APPDATA%\AltSnap\AltSnap.ini. Returns the path it would write to even if the
    /// file does not yet exist (so we can create it on first save).
    /// </summary>
    public static async Task<string?> IniPathAsync(bool forWrite = false, CancellationToken ct = default)
    {
        var exe = await LocateAsync(ct);
        var candidates = new List<string>();
        if (exe is not null)
        {
            var dir = Path.GetDirectoryName(exe);
            if (!string.IsNullOrEmpty(dir)) candidates.Add(Path.Combine(dir, "AltSnap.ini"));
        }
        candidates.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AltSnap", "AltSnap.ini"));

        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        // None exists yet — return the preferred write location (next to the exe if we can, else APPDATA).
        if (forWrite) return candidates.FirstOrDefault();
        return null;
    }

    // ===================== ini read / write =====================

    /// <summary>讀一個 INI 值 · Read a key from a section, or returns <paramref name="fallback"/>.</summary>
    public static async Task<string> ReadIni(string section, string key, string fallback = "", CancellationToken ct = default)
    {
        var ini = await IniPathAsync(forWrite: false, ct);
        if (ini is null || !File.Exists(ini)) return fallback;
        try
        {
            var map = ParseIni(await File.ReadAllTextAsync(ini, ct));
            if (map.TryGetValue(section, out var sect) && sect.TryGetValue(key, out var val))
                return val;
        }
        catch { /* ignore */ }
        return fallback;
    }

    /// <summary>寫一個 INI 值（保留其餘內容同註解）· Write a key, preserving the rest of the file and comments.</summary>
    public static async Task<TweakResult> WriteIni(string section, string key, string value, CancellationToken ct = default)
    {
        var ini = await IniPathAsync(forWrite: true, ct);
        if (ini is null)
            return TweakResult.Fail("Could not determine an AltSnap.ini location.", "無法決定 AltSnap.ini 位置。");
        try
        {
            var dir = Path.GetDirectoryName(ini);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string text = File.Exists(ini) ? await File.ReadAllTextAsync(ini, ct) : "";
            var updated = UpsertIniValue(text, section, key, value);
            await File.WriteAllTextAsync(ini, updated, ct);
            return TweakResult.Ok($"Saved [{section}] {key}={value}.", $"已儲存 [{section}] {key}={value}。", ini);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not write AltSnap.ini: {ex.Message}", $"無法寫入 AltSnap.ini：{ex.Message}");
        }
    }

    /// <summary>讀取整個 ini 原始內容（畀 raw-edit fallback 用）· Read the raw ini text for the raw-edit fallback.</summary>
    public static async Task<string> ReadRaw(CancellationToken ct = default)
    {
        var ini = await IniPathAsync(forWrite: false, ct);
        if (ini is null || !File.Exists(ini)) return "";
        try { return await File.ReadAllTextAsync(ini, ct); } catch { return ""; }
    }

    /// <summary>寫入整個 ini 原始內容 · Overwrite the raw ini text (raw-edit fallback save).</summary>
    public static async Task<TweakResult> WriteRaw(string text, CancellationToken ct = default)
    {
        var ini = await IniPathAsync(forWrite: true, ct);
        if (ini is null)
            return TweakResult.Fail("Could not determine an AltSnap.ini location.", "無法決定 AltSnap.ini 位置。");
        try
        {
            var dir = Path.GetDirectoryName(ini);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(ini, text, ct);
            return TweakResult.Ok("Saved AltSnap.ini.", "已儲存 AltSnap.ini。", ini);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not write AltSnap.ini: {ex.Message}", $"無法寫入 AltSnap.ini：{ex.Message}");
        }
    }

    /// <summary>匯入設定（複製到 ini 位置）· Import a config file by copying it over the active AltSnap.ini.</summary>
    public static async Task<TweakResult> ImportIni(string sourcePath, CancellationToken ct = default)
    {
        var ini = await IniPathAsync(forWrite: true, ct);
        if (ini is null)
            return TweakResult.Fail("Could not determine an AltSnap.ini location.", "無法決定 AltSnap.ini 位置。");
        try
        {
            var dir = Path.GetDirectoryName(ini);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Copy(sourcePath, ini, overwrite: true);
            return TweakResult.Ok("Imported configuration.", "已匯入設定。", ini);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Import failed: {ex.Message}", $"匯入失敗：{ex.Message}");
        }
    }

    /// <summary>匯出設定（複製 ini 到指定路徑）· Export the active AltSnap.ini to a chosen path.</summary>
    public static async Task<TweakResult> ExportIni(string destPath, CancellationToken ct = default)
    {
        var ini = await IniPathAsync(forWrite: false, ct);
        if (ini is null || !File.Exists(ini))
            return TweakResult.Fail("No AltSnap.ini to export yet.", "暫時冇 AltSnap.ini 可匯出。");
        try
        {
            File.Copy(ini, destPath, overwrite: true);
            return TweakResult.Ok("Exported configuration.", "已匯出設定。", destPath);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Export failed: {ex.Message}", $"匯出失敗：{ex.Message}");
        }
    }

    // ===================== conflict detection =====================

    /// <summary>偵測會搶 hook 嘅衝突工具（例如舊版 AltDrag）· Detect a conflicting hook owner (e.g. legacy AltDrag).</summary>
    public static string? DetectConflict()
    {
        try
        {
            foreach (var name in new[] { "AltDrag", "WindowsGrep", "easydrag" })
                if (Process.GetProcessesByName(name).Length > 0)
                    return name + ".exe";
        }
        catch { /* ignore */ }
        return null;
    }

    // ===================== ini helpers (case-insensitive, comment-preserving) =====================

    private static Dictionary<string, Dictionary<string, string>> ParseIni(string text)
    {
        var map = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        map[""] = current;
        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                var name = line.Substring(1, line.Length - 2).Trim();
                if (!map.TryGetValue(name, out current!))
                {
                    current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    map[name] = current;
                }
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line.Substring(0, eq).Trim();
            var val = line.Substring(eq + 1).Trim();
            current[key] = val;
        }
        return map;
    }

    /// <summary>
    /// 喺保留註解嘅前提下，新增／更新一個 section/key=value · Insert or update a key under a section while
    /// preserving every other line and comment. Creates the section (and a trailing one) when missing.
    /// </summary>
    private static string UpsertIniValue(string text, string section, string key, string value)
    {
        var newline = text.Contains("\r\n") || text.Length == 0 ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();

        int sectionStart = -1, sectionEnd = lines.Count;
        bool inTarget = false;
        for (int i = 0; i < lines.Count; i++)
        {
            var t = lines[i].Trim();
            if (t.StartsWith("[") && t.EndsWith("]"))
            {
                var name = t.Substring(1, t.Length - 2).Trim();
                if (inTarget) { sectionEnd = i; break; }
                if (string.Equals(name, section, StringComparison.OrdinalIgnoreCase))
                {
                    inTarget = true;
                    sectionStart = i;
                }
            }
        }

        if (sectionStart < 0)
        {
            // Section absent — append it.
            var sb = new StringBuilder(text);
            if (text.Length > 0 && !text.EndsWith("\n")) sb.Append(newline);
            if (text.Length > 0) sb.Append(newline);
            sb.Append('[').Append(section).Append(']').Append(newline);
            sb.Append(key).Append('=').Append(value).Append(newline);
            return sb.ToString();
        }

        // Section present — look for the key inside it.
        for (int i = sectionStart + 1; i < sectionEnd; i++)
        {
            var t = lines[i].Trim();
            if (t.Length == 0 || t.StartsWith(";") || t.StartsWith("#")) continue;
            int eq = t.IndexOf('=');
            if (eq <= 0) continue;
            var k = t.Substring(0, eq).Trim();
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"{key}={value}";
                return string.Join(newline, lines);
            }
        }

        // Key absent in section — insert just after the section header.
        lines.Insert(sectionStart + 1, $"{key}={value}");
        return string.Join(newline, lines);
    }
}
