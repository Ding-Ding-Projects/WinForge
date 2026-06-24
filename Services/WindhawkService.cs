using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// Windhawk 模組管理器嘅應用程式內前端（偵測／安裝／啟動／深層連結）·
/// In-app front-end for the Windhawk mod platform: detect the official install, launch its UI,
/// open the mods/settings folders, and deep-link to each mod's page on windhawk.net.
///
/// Windhawk 本身係一個 C++ 注入平台（編譯 mod、注入 DLL 入 explorer.exe／工作列等），需要提權同安裝服務，
/// 無法亦唔應該複製。所以 WinForge 透過 winget 安裝官方版本、開佢嘅 UI 做真正嘅 mod 設定，
/// 再加上一個精選、雙語嘅 mod 目錄同深層連結。Defensive throughout — never throws.
/// Windhawk is a C++ injection platform (compiles mods, injects DLLs into explorer.exe/taskbar via a
/// system-wide hook engine and elevated service); cloning it is out of scope and unsafe. WinForge installs
/// the official binary via winget, launches its UI for the real authoring/config experience, and adds a
/// curated bilingual mod gallery with deep-links.
/// </summary>
public static class WindhawkService
{
    /// <summary>winget 套件 ID · The winget package ID for installs.</summary>
    public const string WingetId = "RamenSoftware.Windhawk";

    /// <summary>mod 頁面網址前綴 · windhawk.net mod-page URL root (append a mod id).</summary>
    public const string ModPageRoot = "https://windhawk.net/mods/";

    /// <summary>Windhawk 主頁／mod 目錄 · Windhawk homepage / full mod catalog.</summary>
    public const string Homepage = "https://windhawk.net/";

    private static string? _cachedExe;

    /// <summary>
    /// 偵測已安裝嘅 windhawk.exe 路徑（先查標準路徑，再查解除安裝登錄檔）·
    /// Locate an installed windhawk.exe by probing the standard install dirs, then the uninstall registry.
    /// Returns null if not found.
    /// </summary>
    public static string? FindExe()
    {
        if (_cachedExe is not null && File.Exists(_cachedExe)) return _cachedExe;
        _cachedExe = null;

        foreach (var dir in CandidateDirs())
        {
            try
            {
                var exe = Path.Combine(dir, "windhawk.exe");
                if (File.Exists(exe)) { _cachedExe = exe; return exe; }
            }
            catch { /* ignore bad path */ }
        }

        // 解除安裝登錄檔（InstallLocation / DisplayIcon）· Uninstall registry fallback.
        foreach (var root in new[] { RegRoot.HKLM, RegRoot.HKCU })
        {
            var loc = RegistryHelper.GetValue(root,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Windhawk_is1", "InstallLocation") as string;
            if (!string.IsNullOrWhiteSpace(loc))
            {
                try
                {
                    var exe = Path.Combine(loc, "windhawk.exe");
                    if (File.Exists(exe)) { _cachedExe = exe; return exe; }
                }
                catch { /* ignore */ }
            }
        }
        return null;
    }

    private static System.Collections.Generic.IEnumerable<string> CandidateDirs()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(pf)) yield return Path.Combine(pf, "Windhawk");
        if (!string.IsNullOrEmpty(pfx86)) yield return Path.Combine(pfx86, "Windhawk");
    }

    /// <summary>裝咗未 · True if windhawk.exe was found.</summary>
    public static bool IsInstalled() => FindExe() is not null;

    /// <summary>清快取，等重新偵測（安裝完之後叫）· Clear the cached path so detection re-runs after an install.</summary>
    public static void Rescan() => _cachedExe = null;

    /// <summary>
    /// 由解除安裝登錄檔讀版本（DisplayVersion）· Read the installed version from the uninstall registry, if present.
    /// </summary>
    public static string? Version()
    {
        foreach (var root in new[] { RegRoot.HKLM, RegRoot.HKCU })
        {
            var v = RegistryHelper.GetValue(root,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Windhawk_is1", "DisplayVersion") as string;
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        return null;
    }

    /// <summary>
    /// Windhawk 引擎資料夾（mod 設定／engine.ini 喺度）· The Windhawk engine data folder
    /// (mod configs / engine state live here). Usually %ProgramData%\Windhawk\Engine.
    /// </summary>
    public static string? EngineFolder()
    {
        try
        {
            var pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var f = Path.Combine(pd, "Windhawk", "Engine");
            return Directory.Exists(f) ? f : null;
        }
        catch { return null; }
    }

    /// <summary>啟動 Windhawk UI · Launch the Windhawk app window.</summary>
    public static TweakResult Launch()
    {
        var exe = FindExe();
        if (exe is null)
            return TweakResult.Fail("Windhawk is not installed.", "未安裝 Windhawk。");
        try
        {
            Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
            return TweakResult.Ok("Launched Windhawk.", "已啟動 Windhawk。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not launch Windhawk: {ex.Message}",
                $"無法啟動 Windhawk：{ex.Message}");
        }
    }

    /// <summary>用系統預設方式開一個 URL（mod 頁／主頁）· Open a URL with the system handler (mod page / homepage).</summary>
    public static TweakResult OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return TweakResult.Ok($"Opened {url}", $"已開啟 {url}");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not open link: {ex.Message}", $"無法開啟連結：{ex.Message}");
        }
    }

    /// <summary>開某個 mod 嘅 windhawk.net 頁面 · Open a mod's page on windhawk.net.</summary>
    public static TweakResult OpenModPage(string modId) => OpenUrl(ModPageRoot + modId);

    /// <summary>喺檔案總管開一個資料夾 · Reveal a folder in Explorer.</summary>
    public static TweakResult OpenFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return TweakResult.Fail("Folder not found.", "搵唔到資料夾。");
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{folder}\"", UseShellExecute = true });
            return TweakResult.Ok($"Opened {folder}", $"已開啟 {folder}");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not open folder: {ex.Message}", $"無法開啟資料夾：{ex.Message}");
        }
    }

    /// <summary>異步偵測（畀 EngineBar 用，避免阻塞 UI thread）· Async detection wrapper for the engine bar.</summary>
    public static Task<bool> IsInstalledAsync(CancellationToken ct = default)
        => Task.Run(() => IsInstalled(), ct);
}
