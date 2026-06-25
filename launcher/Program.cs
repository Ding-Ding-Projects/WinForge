using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace WinForgeLauncher;

/// <summary>
/// WinForge 可靠啟動器 · Reliable WinForge launcher.
/// 啟動 WinForge.exe；若佢喺開機頭幾秒以 0xC000027B（WinUI 框架偶發閃退）退出，就重試（最多 5 次）。
/// Launches WinForge.exe; if it exits with 0xC000027B (the intermittent WinUI startup fail-fast) within
/// the first few seconds, it relaunches — up to 5 attempts. ~10% per-launch failure → ~0.001% after 5.
///
/// 常駐反應堆（可選、預設關）· Always-on reactor (opt-in, default OFF): when started with "--reactor",
/// after a successful launch the launcher supervises the reactor with a keep-alive loop. It honours
/// user-quit (sentinel exit code OR a flag file) and re-reads reactor.keepalive each iteration so
/// turning it OFF in-app stops respawns. Crash respawns use exponential backoff + a circuit breaker.
/// </summary>
internal static class Program
{
    private const int StowedException = unchecked((int)0xC000027B);
    private const int UserQuit = 0x5151;     // sentinel: the user explicitly quit
    private const int MaxAttempts = 5;
    private const int EarlyWindowMs = 8000;

    [STAThread]
    private static int Main(string[] args)
    {
        string dir = AppContext.BaseDirectory;
        string app = Path.Combine(dir, "WinForge.exe");
        if (!File.Exists(app)) app = "WinForge.exe";

        bool reactor = false;
        var passthrough = new List<string>();
        foreach (var a in args)
        {
            if (string.Equals(a, "--reactor", StringComparison.OrdinalIgnoreCase)) { reactor = true; passthrough.Add(a); }
            else passthrough.Add(a);
        }
        // The reactor keep-alive always opens the flagship reactor page.
        if (reactor && !passthrough.Exists(s => string.Equals(s, "--page", StringComparison.OrdinalIgnoreCase)))
        {
            passthrough.Add("--page");
            passthrough.Add("reactor");
        }

        // ---- initial launch with the existing early-crash retry ----
        int rc = LaunchWithRetry(app, dir, passthrough);
        if (!reactor) return rc;
        if (rc == UserQuit || UserQuitFlagPresent()) return 0; // never fight a quit

        // ---- reactor keep-alive supervised loop ----
        var restarts = new Queue<DateTime>();
        while (true)
        {
            if (!KeepAliveEnabled()) return 0;   // disabled in-app → stop
            if (UserQuitFlagPresent()) return 0;

            // circuit breaker: stop after 5 restarts in 60 s.
            var now = DateTime.UtcNow;
            while (restarts.Count > 0 && (now - restarts.Peek()).TotalSeconds > 60) restarts.Dequeue();
            if (restarts.Count >= 5)
            {
                LogCrash("reactor keep-alive: circuit breaker tripped (5 restarts in 60 s) — giving up");
                return 1;
            }

            // exponential backoff based on recent restart count.
            int backoffMs = Math.Min(30000, 1000 * (1 << Math.Min(restarts.Count, 5)));
            Thread.Sleep(backoffMs);

            if (!KeepAliveEnabled() || UserQuitFlagPresent()) return 0;

            restarts.Enqueue(DateTime.UtcNow);
            int childRc = LaunchAndWait(app, dir, passthrough);
            if (childRc == UserQuit || UserQuitFlagPresent()) return 0;
        }
    }

    private static int LaunchWithRetry(string app, string dir, List<string> args)
    {
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            Process child;
            try
            {
                var psi = new ProcessStartInfo { FileName = app, UseShellExecute = false, WorkingDirectory = dir };
                foreach (var a in args) psi.ArgumentList.Add(a);
                child = Process.Start(psi)!;
            }
            catch { return 1; }

            if (child.WaitForExit(EarlyWindowMs))
            {
                if (child.ExitCode == StowedException && attempt < MaxAttempts)
                {
                    Thread.Sleep(400);
                    continue;
                }
                return child.ExitCode;
            }

            child.WaitForExit();
            return child.ExitCode;
        }
        return StowedException;
    }

    private static int LaunchAndWait(string app, string dir, List<string> args)
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = app, UseShellExecute = false, WorkingDirectory = dir };
            foreach (var a in args) psi.ArgumentList.Add(a);
            var child = Process.Start(psi)!;
            child.WaitForExit();
            return child.ExitCode;
        }
        catch { return 1; }
    }

    private static string LocalAppData() =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static string SettingsPath() => Path.Combine(LocalAppData(), "WinForge", "settings.json");
    private static string UserQuitFlagPath() => Path.Combine(LocalAppData(), "WinForge", "reactor.userquit");

    private static bool UserQuitFlagPresent()
    {
        try { return File.Exists(UserQuitFlagPath()); } catch { return false; }
    }

    /// <summary>Read reactor.keepalive from the shared settings.json (single source of truth).</summary>
    private static bool KeepAliveEnabled()
    {
        try
        {
            var p = SettingsPath();
            if (!File.Exists(p)) return false;
            using var doc = JsonDocument.Parse(File.ReadAllText(p));
            if (doc.RootElement.TryGetProperty("reactor.keepalive", out var v))
                return string.Equals(v.GetString(), "True", StringComparison.OrdinalIgnoreCase);
        }
        catch { /* unreadable → treat as disabled to be safe */ }
        return false;
    }

    private static void LogCrash(string msg)
    {
        try
        {
            var path = Path.Combine(LocalAppData(), "WinForge", "crash.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] launcher: {msg}{Environment.NewLine}");
        }
        catch { }
    }
}
