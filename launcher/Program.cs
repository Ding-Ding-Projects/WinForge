using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
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
    private const long MaxInstallerBytes = 512L * 1024 * 1024;

    [STAThread]
    private static int Main(string[] args)
    {
        if (Array.Exists(args, a => string.Equals(a, "--apply-update", StringComparison.OrdinalIgnoreCase)))
            return ApplyUpdate(args);

        string dir = AppContext.BaseDirectory;
        string app = Path.Combine(dir, "WinForge.exe");
        if (!File.Exists(app)) app = "WinForge.exe";

        bool reactor = false;
        bool updated = Array.Exists(args, a => string.Equals(a, "--updated", StringComparison.OrdinalIgnoreCase));
        if (updated && IsElevated()) return 1;
        if (updated && SameInstallAlreadyRunning()) return 0;
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
        if (rc == UserQuit || UserQuitFlagPresent() || UpdatePendingFlagPresent()) return 0; // never fight a quit/update

        // ---- reactor keep-alive supervised loop ----
        var restarts = new Queue<DateTime>();
        while (true)
        {
            if (!KeepAliveEnabled()) return 0;   // disabled in-app → stop
            if (UserQuitFlagPresent()) return 0;
            if (UpdatePendingFlagPresent()) return 0;

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

            if (!KeepAliveEnabled() || UserQuitFlagPresent() || UpdatePendingFlagPresent()) return 0;

            restarts.Enqueue(DateTime.UtcNow);
            int childRc = LaunchAndWait(app, dir, passthrough);
            if (childRc == UserQuit || UserQuitFlagPresent()) return 0;
        }
    }

    private static bool SameInstallAlreadyRunning()
    {
        string dir = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var process in Process.GetProcessesByName("WinForge"))
        {
            try
            {
                if (process.Id == Environment.ProcessId) continue;
                string? path = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(path) && path.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { }
            finally { process.Dispose(); }
        }
        return false;
    }

    /// <summary>
    /// Apply an update from a copy of this single-file launcher staged outside the installation folder.
    /// The visual updater exits before this path runs the installer, so every file under the target folder
    /// is replaceable. The installer is per-user and deliberately runs at normal integrity.
    /// </summary>
    private static int ApplyUpdate(string[] args)
    {
        var values = ParseNamedArguments(args);
        string installer = values.GetValueOrDefault("installer", "");
        string installDir = values.GetValueOrDefault("install-dir", "");
        string targetLauncher = values.GetValueOrDefault("launcher", "");
        string targetExe = values.GetValueOrDefault("exe", "");
        string expectedSha256 = NormalizeSha256(values.GetValueOrDefault("sha256", ""));
        string logPath = values.GetValueOrDefault("log", "");
        _ = int.TryParse(values.GetValueOrDefault("wait-pid", ""), out int waitPid);

        if (string.IsNullOrWhiteSpace(logPath))
            logPath = Path.Combine(LocalAppData(), "WinForge", "updates", "update-helper.log");

        var options = new UpdateOptions(installer, installDir, targetLauncher, targetExe, expectedSha256, logPath);
        using var updateMutex = new Mutex(false, "Local\\WinForge.Update");
        bool ownsUpdateMutex;
        try { ownsUpdateMutex = updateMutex.WaitOne(0); }
        catch (AbandonedMutexException) { ownsUpdateMutex = true; }
        catch { ownsUpdateMutex = false; }
        if (!ownsUpdateMutex)
        {
            AppendUpdateLog(SafeHelperLogPath(), "Another update helper owns the per-user update mutex; exiting.");
            return 0;
        }
        try
        {
            if (IsElevated())
            {
                string safeLog = SafeHelperLogPath();
                AppendUpdateLog(safeLog,
                    $"ERROR: update helper refused elevated execution. pid={Environment.ProcessId}; waitPid={waitPid}");
                try { MessageBox(IntPtr.Zero,
                    "The update helper cannot run as administrator. Restart WinForge normally.\n\n更新助手唔可以用系統管理員身分執行，請以一般權限重開 WinForge。",
                    "WinForge Update · WinForge 更新", 0x00000010); }
                catch { }
                return 1;
            }
            AppendUpdateLog(logPath, $"Helper started. pid={Environment.ProcessId}; waitPid={waitPid}");
            if (!File.Exists(installer) || string.IsNullOrWhiteSpace(installDir) ||
                string.IsNullOrWhiteSpace(targetLauncher) || string.IsNullOrWhiteSpace(targetExe) ||
                expectedSha256.Length != 64)
                return FailUpdate(options,
                    "The update handoff is incomplete or the installer digest is missing.",
                    "更新交接資料唔完整，或者欠缺安裝程式雜湊值。");
            long installerBytes = new FileInfo(installer).Length;
            if (installerBytes <= 0 || installerBytes > MaxInstallerBytes)
                return FailUpdate(options,
                    "The installer size is invalid or exceeds the 512 MB safety limit.",
                    "安裝程式大小無效，或者超過 512 MB 安全上限。");

            if (waitPid > 0 && waitPid != Environment.ProcessId && !WaitForProcessExit(waitPid, TimeSpan.FromMinutes(2)))
                return FailUpdate(options,
                    $"The visual updater (pid {waitPid}) did not close in time.",
                    $"更新視窗（pid {waitPid}）未能及時關閉。");
            if (!WaitForInstallProcessesToExit(installDir, TimeSpan.FromSeconds(45)))
                return FailUpdate(options,
                    "A WinForge process is still using the installation folder.",
                    "仍然有 WinForge 程序使用緊安裝資料夾。");

            // Keep the verified file open with write/delete sharing denied until Setup exits. This closes
            // the final hash-to-execute replacement window in the user-writable updates directory.
            using var verifiedInstaller = new FileStream(
                installer, FileMode.Open, FileAccess.Read, FileShare.Read);
            string actualSha256 = ComputeSha256(verifiedInstaller);
            AppendUpdateLog(logPath, $"Installer SHA-256: {actualSha256}");
            if (!FixedTimeHexEquals(expectedSha256, actualSha256))
                return FailUpdate(options,
                    "The downloaded installer failed SHA-256 verification and was not run.",
                    "下載嘅安裝程式未通過 SHA-256 驗證，所以冇執行。");

            string innoLog = Path.ChangeExtension(logPath, ".inno.log");
            var psi = new ProcessStartInfo
            {
                FileName = installer,
                WorkingDirectory = Path.GetDirectoryName(installer) ?? LocalAppData(),
                UseShellExecute = false,
            };
            foreach (var argument in new[]
                     {
                         "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CLOSEAPPLICATIONS",
                         "/NORESTARTAPPLICATIONS", "/LOGCLOSEAPPLICATIONS", $"/DIR={installDir}", $"/LOG={innoLog}"
                     })
                psi.ArgumentList.Add(argument);

            AppendUpdateLog(logPath, $"Starting installer at normal integrity. Inno log: {innoLog}");
            using var installerProcess = Process.Start(psi);
            if (installerProcess is null)
                return FailUpdate(options, "The installer process could not be started.",
                    "無法啟動安裝程式程序。");
            installerProcess.WaitForExit();
            AppendUpdateLog(logPath, $"Installer exit code: {installerProcess.ExitCode}");
            if (installerProcess.ExitCode != 0)
                return FailUpdate(options,
                    $"The installer exited with code {installerProcess.ExitCode}. Diagnostic log: {innoLog}",
                    $"安裝程式以代碼 {installerProcess.ExitCode} 結束。診斷記錄：{innoLog}",
                    installerProcess.ExitCode);

            AppendUpdateLog(logPath, "Update installed successfully; installer bootstrap owns relaunch.");
            ClearUpdatePendingFlag();
            return 0;
        }
        catch (Exception ex)
        {
            AppendUpdateLog(logPath, "Unhandled helper error: " + ex);
            return FailUpdate(options, $"The update helper failed: {ex.Message}",
                $"更新助手失敗：{ex.Message}");
        }
        finally
        {
            try { updateMutex.ReleaseMutex(); } catch { }
        }
    }

    private sealed record UpdateOptions(
        string Installer,
        string InstallDir,
        string Launcher,
        string Exe,
        string ExpectedSha256,
        string LogPath);

    private static Dictionary<string, string> ParseNamedArguments(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) ||
                string.Equals(args[i], "--apply-update", StringComparison.OrdinalIgnoreCase))
                continue;
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                values[args[i][2..]] = args[++i];
        }
        return values;
    }

    private static string NormalizeSha256(string value)
    {
        value = value.Trim();
        if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) value = value[7..];
        return value.ToUpperInvariant();
    }

    private static string ComputeSha256(Stream input)
    {
        input.Position = 0;
        return Convert.ToHexString(SHA256.HashData(input));
    }

    private static bool FixedTimeHexEquals(string expected, string actual)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(actual));
        }
        catch { return false; }
    }

    private static bool WaitForProcessExit(int pid, TimeSpan timeout)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.WaitForExit((int)Math.Min(int.MaxValue, timeout.TotalMilliseconds));
        }
        catch (ArgumentException) { return true; }
        catch { return false; }
    }

    private static bool WaitForInstallProcessesToExit(string installDir, TimeSpan timeout)
    {
        string root;
        try { root = Path.GetFullPath(installDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; }
        catch { return false; }
        var deadline = DateTime.UtcNow + timeout;
        do
        {
            bool found = false;
            foreach (var name in new[] { "WinForge", "WinForgeLauncher", "WinForgeUpdater" })
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    using (process)
                    {
                        if (process.Id == Environment.ProcessId) continue;
                        try
                        {
                            string? path = process.MainModule?.FileName;
                            if (!string.IsNullOrWhiteSpace(path) && Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase))
                            { found = true; break; }
                        }
                        catch
                        {
                            // The helper refuses elevated execution, so a higher-integrity process whose path
                            // cannot be inspected cannot be one of this normal-integrity update's children.
                            // Inno Setup still records any unexpected file lock in its persistent log.
                            continue;
                        }
                    }
                    if (found) break;
                }
                if (found) break;
            }
            if (!found) return true;
            Thread.Sleep(250);
        } while (DateTime.UtcNow < deadline);
        return false;
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return true; }
    }

    private static int FailUpdate(UpdateOptions options, string en, string zh, int exitCode = 1)
    {
        AppendUpdateLog(options.LogPath, "ERROR: " + en);
        ClearUpdatePendingFlag();
        try { MessageBox(IntPtr.Zero, $"{en}\n\n{zh}\n\nLog · 記錄：{options.LogPath}",
            "WinForge Update · WinForge 更新", 0x00000010); }
        catch { }
        RelaunchUpdatedApp(options);
        return exitCode == 0 ? 1 : exitCode;
    }

    private static void RelaunchUpdatedApp(UpdateOptions options)
    {
        string? target = File.Exists(options.Launcher) ? options.Launcher : File.Exists(options.Exe) ? options.Exe : null;
        if (target is null) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                WorkingDirectory = Path.GetDirectoryName(target) ?? options.InstallDir,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) { AppendUpdateLog(options.LogPath, "Relaunch failed: " + ex.Message); }
    }

    private static void AppendUpdateLog(string path, string message)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    private static string SafeHelperLogPath() => Path.Combine(
        LocalAppData(), "WinForge", "updates", "update-helper.log");

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBox(IntPtr owner, string text, string caption, uint type);

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
    private static string UpdatePendingFlagPath() => Path.Combine(LocalAppData(), "WinForge", "update.pending");

    private static bool UserQuitFlagPresent()
    {
        try { return File.Exists(UserQuitFlagPath()); } catch { return false; }
    }

    private static bool UpdatePendingFlagPresent()
    {
        try
        {
            string path = UpdatePendingFlagPath();
            if (!File.Exists(path)) return false;
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > TimeSpan.FromMinutes(10))
            {
                File.Delete(path);
                return false;
            }
            return true;
        }
        catch { return true; }
    }

    private static void ClearUpdatePendingFlag()
    {
        try { File.Delete(UpdatePendingFlagPath()); } catch { }
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
