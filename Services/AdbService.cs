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
    private const string InvalidDeviceMessage = "Invalid Android device identifier · Android 裝置識別碼無效。";

    private static Task<string> Capture(IReadOnlyList<string> args, CancellationToken ct)
        => ShellRunner.CaptureArguments("adb", args, ct);

    private static bool IsSafeDeviceToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 255) return false;
        foreach (char c in value)
        {
            bool allowed = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') || c is '.' or '-' or '_' or ':' or '[' or ']' or '%';
            if (!allowed) return false;
        }
        return true;
    }

    private static string[]? DeviceArgs(string serial, params string[] args)
    {
        serial = (serial ?? "").Trim();
        if (!IsSafeDeviceToken(serial)) return null;
        var result = new string[args.Length + 2];
        result[0] = "-s";
        result[1] = serial;
        Array.Copy(args, 0, result, 2, args.Length);
        return result;
    }

    private static Task<TweakResult> InvalidDevice() => Task.FromResult(
        TweakResult.Fail("Invalid Android device identifier.", "Android 裝置識別碼無效。"));

    private static Task<TweakResult> RunDevice(string serial, CancellationToken ct, params string[] args)
    {
        var fullArgs = DeviceArgs(serial, args);
        return fullArgs is null ? InvalidDevice() : ShellRunner.RunArguments("adb", fullArgs, false, ct);
    }

    private static Task<string> CaptureDevice(string serial, CancellationToken ct, params string[] args)
    {
        var fullArgs = DeviceArgs(serial, args);
        return fullArgs is null ? Task.FromResult(InvalidDeviceMessage) : Capture(fullArgs, ct);
    }

    // adb shell forwards a command string to the Android shell. Keep its quoting separate from Windows
    // process construction: the resulting string is one argument in ProcessStartInfo.ArgumentList, so it
    // cannot be interpreted by a local Windows shell.
    private static string RemoteCommand(params string[] args)
    {
        var command = new StringBuilder();
        foreach (var arg in args)
        {
            if (command.Length > 0) command.Append(' ');
            command.Append('\'').Append((arg ?? "").Replace("'", "'\"'\"'")).Append('\'');
        }
        return command.ToString();
    }

    public static async Task<bool> IsAvailable(CancellationToken ct = default)
    {
        try { return (await Capture(new[] { "version" }, ct)).Contains("Android Debug Bridge", StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    public static async Task<List<AdbDevice>> Devices(CancellationToken ct = default)
    {
        var list = new List<AdbDevice>();
        string outp;
        try { outp = await Capture(new[] { "devices", "-l" }, ct); } catch { return list; }

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
        => RunDevice(serial, ct, "install", "-r", apkPath ?? "");

    public static Task<TweakResult> Uninstall(string serial, string package, CancellationToken ct = default)
        => RunDevice(serial, ct, "uninstall", package ?? "");

    /// <param name="mode">"" (system), "bootloader" or "recovery".</param>
    public static Task<TweakResult> Reboot(string serial, string mode, CancellationToken ct = default)
        => string.IsNullOrWhiteSpace(mode)
            ? RunDevice(serial, ct, "reboot")
            : RunDevice(serial, ct, "reboot", mode.Trim());

    public static Task<TweakResult> Connect(string ipPort, CancellationToken ct = default)
    {
        var endpoint = (ipPort ?? "").Trim();
        return !IsSafeDeviceToken(endpoint)
            ? Task.FromResult(TweakResult.Fail(
                "Enter an ADB host or IP address with an optional port.",
                "請輸入 ADB 主機或 IP 位址（可加連接埠）。"))
            : ShellRunner.RunArguments("adb", new[] { "connect", endpoint }, false, ct);
    }

    public static Task<TweakResult> Disconnect(string ipPort, CancellationToken ct = default)
    {
        var endpoint = (ipPort ?? "").Trim();
        return !IsSafeDeviceToken(endpoint)
            ? Task.FromResult(TweakResult.Fail(
                "Enter an ADB host or IP address with an optional port.",
                "請輸入 ADB 主機或 IP 位址（可加連接埠）。"))
            : ShellRunner.RunArguments("adb", new[] { "disconnect", endpoint }, false, ct);
    }

    public static Task<TweakResult> KillServer(CancellationToken ct = default)
        => ShellRunner.RunArguments("adb", new[] { "kill-server" }, false, ct);

    public static Task<string> Shell(string serial, string command, CancellationToken ct = default)
        // The remote command intentionally remains one ADB argument. It is interpreted only by the
        // Android shell after adb connects; it is never parsed by local cmd.exe or PowerShell.
        => CaptureDevice(serial, ct, "shell", command ?? "");

    public static Task<string> Logcat(string serial, int lines, CancellationToken ct = default)
        => CaptureDevice(serial, ct, "logcat", "-d", "-t", Math.Max(1, lines).ToString());

    public static Task<string> Packages(string serial, CancellationToken ct = default)
        => CaptureDevice(serial, ct, "shell", RemoteCommand("pm", "list", "packages"));

    /// <summary>Capture a device screenshot to a local PNG (screencap on the device, then pull).</summary>
    public static async Task<TweakResult> Screenshot(string serial, string localPath, CancellationToken ct = default)
    {
        const string remote = "/sdcard/winforge_screen.png";
        var cap = await RunDevice(serial, ct, "shell", RemoteCommand("screencap", "-p", remote));
        if (!cap.Success) return cap;
        return await RunDevice(serial, ct, "pull", remote, localPath ?? "");
    }

    // ── File browser (push / pull) · 檔案瀏覽（推送／拉取） ───────────────────────────────

    /// <summary>列出裝置上一個資料夾 · List a directory on the device (folders first, then files).</summary>
    public static async Task<List<AdbFileEntry>> ListDir(string serial, string path, CancellationToken ct = default)
    {
        var list = new List<AdbFileEntry>();
        // -F appends a type indicator: '/' dir, '*' exec, '@' symlink, '|' fifo, '=' socket.
        string outp;
        try { outp = await CaptureDevice(serial, ct, "shell", RemoteCommand("ls", "-1aF", path ?? "")); }
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
        => RunDevice(serial, ct, "pull", remotePath ?? "", localPath ?? "");

    /// <summary>Push a local file/folder to the device.</summary>
    public static Task<TweakResult> Push(string serial, string localPath, string remotePath, CancellationToken ct = default)
        => RunDevice(serial, ct, "push", localPath ?? "", remotePath ?? "");

    /// <summary>Delete a file/folder on the device (rm -rf). Caller must confirm.</summary>
    public static Task<TweakResult> Delete(string serial, string remotePath, CancellationToken ct = default)
        => RunDevice(serial, ct, "shell", RemoteCommand("rm", "-rf", remotePath ?? ""));

    // ── APK backup (pm path + pull) · 備份已裝 APK ──────────────────────────────────────

    /// <summary>List installed third-party packages mapped to their on-device base APK path.</summary>
    public static async Task<List<AdbPackage>> InstalledApks(string serial, bool includeSystem, CancellationToken ct = default)
    {
        var res = new List<AdbPackage>();
        var args = includeSystem
            ? DeviceArgs(serial, "shell", RemoteCommand("pm", "list", "packages"))
            : DeviceArgs(serial, "shell", RemoteCommand("pm", "list", "packages", "-3")); // -3 = third-party only
        if (args is null) return res;
        string list;
        try { list = await Capture(args, ct); }
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
        var outp = await CaptureDevice(serial, ct, "shell", RemoteCommand("pm", "path", package ?? ""));
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
            var psi = CreateLogcatStartInfo(serial, filter);
            if (psi is null) return false;
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

    /// <summary>Builds the tracked logcat process without passing user text through a command shell.</summary>
    internal static ProcessStartInfo? CreateLogcatStartInfo(string serial, string filter)
    {
        var args = DeviceArgs(serial, "logcat");
        if (args is null) return null;
        var psi = new ProcessStartInfo
        {
            FileName = "adb",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        foreach (var part in (filter ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            psi.ArgumentList.Add(part);
        return psi;
    }

    public static void StopLogcatStream()
    {
        var p = _logcatProc;
        _logcatProc = null;
        try { if (p is { HasExited: false }) p.Kill(true); } catch { }
        try { p?.Dispose(); } catch { }
    }
}
