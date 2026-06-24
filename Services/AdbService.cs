using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一部 ADB 裝置 · One adb device row.</summary>
public sealed class AdbDevice
{
    public string Serial { get; set; } = "";
    public string State { get; set; } = "";
    public string Model { get; set; } = "";

    public string Display => string.IsNullOrEmpty(Model) ? $"{Serial} ({State})" : $"{Model} · {Serial} ({State})";
}

/// <summary>裝置上嘅一個檔案／資料夾 · One file/folder entry from `adb shell ls`.</summary>
public sealed class AdbFileEntry
{
    public string Name { get; set; } = "";
    public bool IsDirectory { get; set; }
    public string DisplayName => IsDirectory ? Name + "/" : Name;
    public string Glyph => IsDirectory ? "" : "";
}

/// <summary>裝置上一個已安裝 APK · One installed package mapped to its on-device APK path.</summary>
public sealed class AdbPackage
{
    public string Package { get; set; } = "";
    public string ApkPath { get; set; } = "";
}

/// <summary>
/// 應用程式內 Android ADB 主控台（包真實 adb 引擎）· In-app Android ADB console wrapping adb.exe — list
/// devices, install/uninstall APKs, shell, logcat, screenshot, reboot and wireless connect. No redirect.
/// adb comes from Google.PlatformTools (install it from the Package Manager).
/// </summary>
public static class AdbService
{
    private static Task<string> Capture(string args, CancellationToken ct)
        => ShellRunner.CapturePowershell($"adb {args} 2>&1 | Out-String -Width 400", ct);

    public static async Task<bool> IsAvailable(CancellationToken ct = default)
    {
        try { return (await Capture("version", ct)).Contains("Android Debug Bridge", StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    public static async Task<List<AdbDevice>> Devices(CancellationToken ct = default)
    {
        var list = new List<AdbDevice>();
        string outp;
        try { outp = await Capture("devices -l", ct); } catch { return list; }

        bool started = false;
        foreach (var raw in outp.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase)) { started = true; continue; }
            if (!started || line.Length == 0 || line.StartsWith("*")) continue;

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            var dev = new AdbDevice { Serial = parts[0], State = parts[1] };
            foreach (var p in parts)
                if (p.StartsWith("model:", StringComparison.OrdinalIgnoreCase))
                    dev.Model = p.Substring("model:".Length).Replace('_', ' ');
            list.Add(dev);
        }
        return list;
    }

    public static Task<TweakResult> Install(string serial, string apkPath, CancellationToken ct = default)
        => ShellRunner.RunCmd($"adb -s {serial} install -r \"{apkPath}\"", false, ct);

    public static Task<TweakResult> Uninstall(string serial, string package, CancellationToken ct = default)
        => ShellRunner.RunCmd($"adb -s {serial} uninstall {package}", false, ct);

    /// <param name="mode">"" (system), "bootloader" or "recovery".</param>
    public static Task<TweakResult> Reboot(string serial, string mode, CancellationToken ct = default)
        => ShellRunner.RunCmd($"adb -s {serial} reboot {mode}".TrimEnd(), false, ct);

    public static Task<TweakResult> Connect(string ipPort, CancellationToken ct = default)
        => ShellRunner.RunCmd($"adb connect {ipPort}", false, ct);

    public static Task<TweakResult> Disconnect(string ipPort, CancellationToken ct = default)
        => ShellRunner.RunCmd($"adb disconnect {ipPort}", false, ct);

    public static Task<TweakResult> KillServer(CancellationToken ct = default)
        => ShellRunner.RunCmd("adb kill-server", false, ct);

    public static Task<string> Shell(string serial, string command, CancellationToken ct = default)
        => Capture($"-s {serial} shell {command}", ct);

    public static Task<string> Logcat(string serial, int lines, CancellationToken ct = default)
        => Capture($"-s {serial} logcat -d -t {lines}", ct);

    public static Task<string> Packages(string serial, CancellationToken ct = default)
        => Capture($"-s {serial} shell pm list packages", ct);

    /// <summary>Capture a device screenshot to a local PNG (screencap on the device, then pull).</summary>
    public static async Task<TweakResult> Screenshot(string serial, string localPath, CancellationToken ct = default)
    {
        const string remote = "/sdcard/wintune_screen.png";
        var cap = await ShellRunner.RunCmd($"adb -s {serial} shell screencap -p {remote}", false, ct);
        if (!cap.Success) return cap;
        return await ShellRunner.RunCmd($"adb -s {serial} pull {remote} \"{localPath}\"", false, ct);
    }

    // ── File browser (push / pull) · 檔案瀏覽（推送／拉取） ───────────────────────────────

    /// <summary>列出裝置上一個資料夾 · List a directory on the device (folders first, then files).</summary>
    public static async Task<List<AdbFileEntry>> ListDir(string serial, string path, CancellationToken ct = default)
    {
        var list = new List<AdbFileEntry>();
        // -F appends a type indicator: '/' dir, '*' exec, '@' symlink, '|' fifo, '=' socket.
        string outp;
        try { outp = await Capture($"-s {serial} shell ls -1aF \"{ShellEscape(path)}\"", ct); }
        catch { return list; }

        foreach (var raw in outp.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.Contains("Permission denied") || line.Contains("No such file") || line.Contains("Not a directory"))
                continue;
            bool dir = line.EndsWith("/");
            var name = line.TrimEnd('/', '*', '@', '|', '=');
            if (name is "." or "..") continue;
            // symlinks may render as "name@" — treat unknowns as files; toolbox ls -F marks dirs with '/'.
            list.Add(new AdbFileEntry { Name = name, IsDirectory = dir });
        }
        list.Sort((a, b) =>
        {
            if (a.IsDirectory != b.IsDirectory) return a.IsDirectory ? -1 : 1;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return list;
    }

    /// <summary>Pull a file/folder from the device to a local path.</summary>
    public static Task<TweakResult> Pull(string serial, string remotePath, string localPath, CancellationToken ct = default)
        => ShellRunner.RunCmd($"adb -s {serial} pull \"{remotePath}\" \"{localPath}\"", false, ct);

    /// <summary>Push a local file/folder to the device.</summary>
    public static Task<TweakResult> Push(string serial, string localPath, string remotePath, CancellationToken ct = default)
        => ShellRunner.RunCmd($"adb -s {serial} push \"{localPath}\" \"{remotePath}\"", false, ct);

    /// <summary>Delete a file/folder on the device (rm -rf). Caller must confirm.</summary>
    public static Task<TweakResult> Delete(string serial, string remotePath, CancellationToken ct = default)
        => ShellRunner.RunCmd($"adb -s {serial} shell rm -rf \"{ShellEscape(remotePath)}\"", false, ct);

    private static string ShellEscape(string p) => p.Replace("\"", "\\\"");

    // ── APK backup (pm path + pull) · 備份已裝 APK ──────────────────────────────────────

    /// <summary>List installed third-party packages mapped to their on-device base APK path.</summary>
    public static async Task<List<AdbPackage>> InstalledApks(string serial, bool includeSystem, CancellationToken ct = default)
    {
        var res = new List<AdbPackage>();
        var flag = includeSystem ? "" : "-3"; // -3 = third-party only
        string list;
        try { list = await Capture($"-s {serial} shell pm list packages {flag}", ct); }
        catch { return res; }

        foreach (var raw in list.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("package:")) continue;
            var pkg = line.Substring("package:".Length).Trim();
            if (pkg.Length == 0) continue;
            res.Add(new AdbPackage { Package = pkg });
        }
        res.Sort((a, b) => string.Compare(a.Package, b.Package, StringComparison.OrdinalIgnoreCase));
        return res;
    }

    /// <summary>Resolve the base APK path for a package via `pm path`.</summary>
    public static async Task<string> ApkPath(string serial, string package, CancellationToken ct = default)
    {
        var outp = await Capture($"-s {serial} shell pm path {package}", ct);
        foreach (var raw in outp.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("package:")) return line.Substring("package:".Length).Trim();
        }
        return "";
    }

    /// <summary>Back up a package's APK: resolve its path with `pm path`, then pull it locally.</summary>
    public static async Task<TweakResult> BackupApk(string serial, string package, string localPath, CancellationToken ct = default)
    {
        var remote = await ApkPath(serial, package, ct);
        if (string.IsNullOrEmpty(remote))
            return TweakResult.Fail($"Could not resolve APK path for {package}.", $"搵唔到 {package} 嘅 APK 路徑。");
        return await Pull(serial, remote, localPath, ct);
    }

    // ── Streaming logcat (tracked process) · 即時 logcat（追蹤程序） ─────────────────────

    private static Process? _logcatProc;

    public static bool IsStreamingLogcat => _logcatProc is { HasExited: false };

    /// <summary>Start a live logcat stream; each output line is delivered via <paramref name="onLine"/>
    /// on a background thread (the caller marshals to the UI). Returns false if it can't start.</summary>
    public static bool StartLogcatStream(string serial, string filter, Action<string> onLine)
    {
        if (IsStreamingLogcat) return true;
        try
        {
            // clear the buffer first so we stream fresh lines, then follow.
            var args = $"-s {serial} logcat {filter}".Trim();
            var psi = new ProcessStartInfo
            {
                FileName = "adb",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            _logcatProc = Process.Start(psi);
            if (_logcatProc is null) return false;
            _logcatProc.OutputDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data); };
            _logcatProc.ErrorDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data); };
            _logcatProc.BeginOutputReadLine();
            _logcatProc.BeginErrorReadLine();
            return true;
        }
        catch { _logcatProc = null; return false; }
    }

    public static void StopLogcatStream()
    {
        var p = _logcatProc;
        _logcatProc = null;
        try { if (p is { HasExited: false }) p.Kill(true); } catch { }
        try { p?.Dispose(); } catch { }
    }
}
