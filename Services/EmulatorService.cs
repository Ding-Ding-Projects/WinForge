using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個 AVD（Android 虛擬裝置）· One Android Virtual Device.</summary>
public sealed class Avd
{
    public string Name { get; set; } = "";
    public string Device { get; set; } = "";
    public string Target { get; set; } = "";
    public string Display => string.IsNullOrEmpty(Device) ? Name : $"{Name} · {Device}";
}

/// <summary>一個系統映像套件（emulator 用）· One installed system-image package id.</summary>
public sealed class AvdImage
{
    public string Package { get; set; } = "";  // e.g. system-images;android-34;google_apis;x86_64
    public string Display => Package;
}

/// <summary>
/// 應用程式內 Android 模擬器控制 · In-app Android emulator control wrapping the SDK's emulator + avdmanager +
/// sdkmanager. Lists/creates/launches/stops/wipes AVDs. Locates the SDK from ANDROID_SDK_ROOT /
/// ANDROID_HOME / the default %LOCALAPPDATA%\Android\Sdk. No redirect — drives the real SDK tools.
/// </summary>
public static class EmulatorService
{
    /// <summary>The Android SDK root, or "" if not found.</summary>
    public static string SdkRoot()
    {
        foreach (var v in new[] { "ANDROID_SDK_ROOT", "ANDROID_HOME" })
        {
            var p = Environment.GetEnvironmentVariable(v);
            if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) return p;
        }
        var def = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk");
        if (Directory.Exists(def)) return def;
        return "";
    }

    private static string ToolPath(string relativeUnderSdk, string exe)
    {
        var root = SdkRoot();
        if (root.Length == 0) return "";
        var p = Path.Combine(root, relativeUnderSdk, exe);
        return File.Exists(p) ? p : "";
    }

    private static string FindFirst(string subdir, string exe)
    {
        var root = SdkRoot();
        if (root.Length == 0) return "";
        var baseDir = Path.Combine(root, subdir);
        if (!Directory.Exists(baseDir)) return "";
        // Direct hit (e.g. emulator/emulator.exe)
        var direct = Path.Combine(baseDir, exe);
        if (File.Exists(direct)) return direct;
        // Versioned tools live under cmdline-tools/<ver>/bin
        try
        {
            foreach (var dir in Directory.GetDirectories(baseDir))
            {
                var cand = Path.Combine(dir, "bin", exe);
                if (File.Exists(cand)) return cand;
            }
        }
        catch { }
        return "";
    }

    public static string EmulatorExe => ToolPath("emulator", "emulator.exe");
    public static string AvdManager => FindFirst("cmdline-tools", "avdmanager.bat");
    public static string SdkManager => FindFirst("cmdline-tools", "sdkmanager.bat");

    public static bool IsAvailable() => EmulatorExe.Length > 0 && AvdManager.Length > 0;

    /// <summary>A human-readable note on what's missing, for the engine bar.</summary>
    public static (bool ok, string en, string zh) Health()
    {
        var root = SdkRoot();
        if (root.Length == 0)
            return (false, "Android SDK not found. Set ANDROID_SDK_ROOT or install the SDK command-line tools.",
                "搵唔到 Android SDK。請設定 ANDROID_SDK_ROOT，或者安裝 SDK 命令列工具。");
        if (EmulatorExe.Length == 0)
            return (false, $"SDK at {root} has no emulator. Install the 'emulator' package.",
                $"SDK（{root}）冇 emulator。請安裝「emulator」套件。");
        if (AvdManager.Length == 0)
            return (false, $"SDK at {root} has no cmdline-tools (avdmanager). Install 'cmdline-tools;latest'.",
                $"SDK（{root}）冇 cmdline-tools（avdmanager）。請安裝「cmdline-tools;latest」。");
        return (true, $"Android SDK: {root}", $"Android SDK：{root}");
    }

    public static async Task<List<Avd>> ListAvds(CancellationToken ct = default)
    {
        var res = new List<Avd>();
        if (AvdManager.Length == 0) return res;
        var r = await ShellRunner.Run(AvdManager, "list avd", false, ct);
        var outp = r.Output ?? "";
        Avd? cur = null;
        foreach (var raw in outp.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
            {
                if (cur is not null) res.Add(cur);
                cur = new Avd { Name = line.Substring("Name:".Length).Trim() };
            }
            else if (cur is not null && line.StartsWith("Device:", StringComparison.OrdinalIgnoreCase))
                cur.Device = line.Substring("Device:".Length).Trim();
            else if (cur is not null && line.StartsWith("Target:", StringComparison.OrdinalIgnoreCase))
                cur.Target = line.Substring("Target:".Length).Trim();
            else if (cur is not null && line.StartsWith("---------")) { res.Add(cur); cur = null; }
        }
        if (cur is not null) res.Add(cur);
        return res;
    }

    /// <summary>List installed system images that an AVD can be created against.</summary>
    public static async Task<List<AvdImage>> ListSystemImages(CancellationToken ct = default)
    {
        var res = new List<AvdImage>();
        if (SdkManager.Length == 0) return res;
        var r = await ShellRunner.Run(SdkManager, "--list_installed", false, ct);
        foreach (var raw in (r.Output ?? "").Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("system-images;", StringComparison.OrdinalIgnoreCase))
            {
                var pkg = line.Split(new[] { ' ', '\t', '|' }, StringSplitOptions.RemoveEmptyEntries)[0];
                res.Add(new AvdImage { Package = pkg });
            }
        }
        return res;
    }

    /// <summary>Create a new AVD (avdmanager create avd). Requires an installed system image package.</summary>
    public static Task<TweakResult> CreateAvd(string name, string systemImage, string device, CancellationToken ct = default)
    {
        if (AvdManager.Length == 0) return Task.FromResult(TweakResult.Fail("avdmanager not found.", "搵唔到 avdmanager。"));
        var dev = string.IsNullOrEmpty(device) ? "" : $" --device \"{device}\"";
        // echo "no" answers the "create a custom hardware profile?" prompt.
        var args = $"create avd --name \"{name}\" --package \"{systemImage}\"{dev} --force";
        return ShellRunner.RunCmd($"echo no | \"{AvdManager}\" {args}", false, ct);
    }

    private static Process? _emuProc;
    public static bool IsRunning => _emuProc is { HasExited: false };

    /// <summary>Launch an AVD (optionally cold-boot / wipe data). Tracked so it can be stopped.</summary>
    public static TweakResult Launch(string avdName, bool wipeData, bool coldBoot)
    {
        if (EmulatorExe.Length == 0) return TweakResult.Fail("emulator not found.", "搵唔到 emulator。");
        try
        {
            var extra = (wipeData ? " -wipe-data" : "") + (coldBoot ? " -no-snapshot-load" : "");
            var psi = new ProcessStartInfo
            {
                FileName = EmulatorExe,
                Arguments = $"-avd \"{avdName}\"{extra}",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            _emuProc = Process.Start(psi);
            if (_emuProc is null) return TweakResult.Fail("Failed to start the emulator.", "無法啟動模擬器。");
            return TweakResult.Ok("Emulator launching…", "模擬器啟動緊…");
        }
        catch (Exception ex)
        {
            _emuProc = null;
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>Stop a running emulator gracefully via adb emu kill (falls back to killing the tracked process).</summary>
    public static async Task<TweakResult> Stop(CancellationToken ct = default)
    {
        // adb emu kill stops the most recently started emulator console.
        var r = await ShellRunner.RunCmd("adb emu kill", false, ct);
        var p = _emuProc;
        _emuProc = null;
        try { if (p is { HasExited: false }) p.Kill(true); } catch { }
        return r.Success ? r : TweakResult.Ok("Stopped the emulator.", "已停止模擬器。");
    }

    /// <summary>Wipe an AVD's user data (cold next boot) by launching with -wipe-data then killing it,
    /// or, when offline, deleting the AVD's data files. Here we use the documented -wipe-data launch.</summary>
    public static TweakResult Wipe(string avdName) => Launch(avdName, wipeData: true, coldBoot: true);

    public static Task<TweakResult> DeleteAvd(string avdName, CancellationToken ct = default)
    {
        if (AvdManager.Length == 0) return Task.FromResult(TweakResult.Fail("avdmanager not found.", "搵唔到 avdmanager。"));
        return ShellRunner.RunCmd($"\"{AvdManager}\" delete avd --name \"{avdName}\"", false, ct);
    }
}
