using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// Rufus 啟動與偵測小助手 · Small helper for the user-installed Rufus binary. We never bundle, link or
/// copy Rufus' GPLv3 source — the native write/verify path in <see cref="ImagingService"/> needs no
/// binary. Rufus is only resolved and launched for its advanced boot options (UEFI/MBR scheme,
/// persistence, Windows-To-Go, bootable installer media). Install via winget id <c>Rufus.Rufus</c>.
/// </summary>
public static class RufusService
{
    /// <summary>winget package id · 用嚟自動安裝 Rufus。</summary>
    public const string WingetId = "Rufus.Rufus";

    /// <summary>
    /// 搵 Rufus 執行檔 · Resolve a Rufus executable path: PATH, then the App Paths registry key, then the
    /// common winget / portable install locations. Returns null if not found.
    /// </summary>
    public static string? FindExe()
    {
        // 1. On PATH (some installs add a "rufus" shim).
        foreach (var name in new[] { "rufus.exe", "rufus" })
        {
            var onPath = OnPath(name);
            if (onPath is not null) return onPath;
        }

        // 2. App Paths registry (Rufus registers itself here when installed).
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var k = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\rufus.exe");
                var p = k?.GetValue(null) as string;
                if (!string.IsNullOrWhiteSpace(p) && File.Exists(p)) return p;
            }
            catch { }
        }

        // 3. Common winget / portable locations + any rufus*.exe in the user profile.
        foreach (var dir in CandidateDirs())
        {
            try
            {
                if (!Directory.Exists(dir)) continue;
                var hit = Directory.EnumerateFiles(dir, "rufus*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (hit is not null) return hit;
            }
            catch { }
        }
        return null;
    }

    /// <summary>係咪已安裝 · True if a Rufus binary can be resolved on this machine.</summary>
    public static bool IsInstalled() => FindExe() is not null;

    private static System.Collections.Generic.IEnumerable<string> CandidateDirs()
    {
        var lad = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var prog = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(lad, @"Microsoft\WinGet\Links");
        yield return Path.Combine(lad, @"Microsoft\WinGet\Packages\Rufus.Rufus_Microsoft.Winget.Source_8wekyb3d8bbwe");
        yield return Path.Combine(prog, "Rufus");
        yield return Path.Combine(profile, "Downloads");
    }

    private static string? OnPath(string exe)
    {
        try
        {
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
            foreach (var p in paths)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                var full = Path.Combine(p.Trim(), exe);
                if (File.Exists(full)) return full;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 啟動 Rufus（進階開機選項）· Launch the user-installed Rufus for its advanced boot options. Rufus is
    /// a GUI tool with essentially no automation/CLI surface, so we cannot pre-select the drive or ISO —
    /// the user picks them inside Rufus. Returns a bilingual result. Rufus self-elevates (UAC) on launch.
    /// </summary>
    public static async Task<TweakResult> Launch(CancellationToken ct = default)
    {
        await Task.Yield();
        var exe = FindExe();
        if (exe is null)
            return TweakResult.Fail("Rufus is not installed. Use the Install button first.",
                "未安裝 Rufus。請先撳安裝掣。");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true, // let Rufus raise its own UAC prompt
                WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
            };
            Process.Start(psi);
            return TweakResult.Ok($"Launched Rufus ({Path.GetFileName(exe)}). Pick the drive and ISO inside Rufus.",
                $"已啟動 Rufus（{Path.GetFileName(exe)}）。請喺 Rufus 入面揀磁碟同 ISO。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }
}
