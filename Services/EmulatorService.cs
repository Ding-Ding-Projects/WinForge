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

/// <summary>一個 SDK 套件（sdkmanager）· One Android SDK package row from sdkmanager --list / --list_installed.</summary>
public sealed class SdkPackage
{
    public string Path { get; set; } = "";       // e.g. platforms;android-34
    public string Version { get; set; } = "";     // e.g. 34
    public string Description { get; set; } = ""; // human-readable
    public bool Installed { get; set; }
    public string Category { get; set; } = "";    // grouping bucket key (platforms, build-tools, …)

    /// <summary>套件分類嘅顯示字串 · Friendly group label, derived from the first path segment.</summary>
    public string Group => Category;
    public string Status => Installed ? "✓" : "";
    public string Display => Path + (Description.Length > 0 ? $"  —  {Description}" : "");
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

    /// <summary>更新頻道（0=stable 1=beta 2=dev 3=canary）· Release channel passed to sdkmanager via --channel.</summary>
    public static int Channel { get; set; } = 0;

    /// <summary>JDK 偵測（sdkmanager.bat 需要 Java）· true if a JDK/JRE is reachable (JAVA_HOME or java on PATH).</summary>
    public static bool HasJava()
    {
        var jh = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(jh) && File.Exists(Path.Combine(jh, "bin", "java.exe"))) return true;
        // The Android SDK ships its own JBR under jbr/ or jre/ for some tool versions.
        var root = SdkRoot();
        if (root.Length > 0)
        {
            foreach (var sub in new[] { "jbr", "jre" })
                if (File.Exists(Path.Combine(root, sub, "bin", "java.exe"))) return true;
        }
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try { if (File.Exists(Path.Combine(dir.Trim(), "java.exe"))) return true; } catch { }
        }
        return false;
    }

    /// <summary>開啟 SDK 資料夾（Explorer）· Open the SDK root folder in Explorer. Returns false if no SDK.</summary>
    public static bool OpenSdkFolder()
    {
        var root = SdkRoot();
        if (root.Length == 0) return false;
        try { Process.Start(new ProcessStartInfo { FileName = root, UseShellExecute = true }); return true; }
        catch { return false; }
    }

    private static string ChannelArg => Channel is >= 0 and <= 3 ? $" --channel={Channel}" : "";

    /// <summary>由套件 id 推斷分類 · Map a package path to its category bucket (first segment, normalised).</summary>
    private static string CategoryOf(string path)
    {
        var head = path.Split(';')[0].Trim();
        return head switch
        {
            "platforms" => "platforms",
            "build-tools" => "build-tools",
            "platform-tools" => "platform-tools",
            "cmdline-tools" => "cmdline-tools",
            "ndk" or "ndk-bundle" => "ndk",
            "system-images" => "system-images",
            "emulator" => "emulator",
            "sources" => "sources",
            "extras" => "extras",
            _ => head.Length > 0 ? head : "other",
        };
    }

    /// <summary>解析 sdkmanager 表格輸出（以 | 分隔）· Parse a sdkmanager --list / --list_installed table block.</summary>
    private static List<SdkPackage> ParseList(string output, bool installed)
    {
        var res = new List<SdkPackage>();
        if (string.IsNullOrEmpty(output)) return res;
        bool inTable = false;
        foreach (var raw in output.Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            // Section headers / separators we skip.
            if (trimmed.StartsWith("Installed packages:", StringComparison.OrdinalIgnoreCase)) { inTable = true; continue; }
            if (trimmed.StartsWith("Available Packages:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Available Updates:", StringComparison.OrdinalIgnoreCase)) { inTable = false; continue; }
            if (trimmed.StartsWith("---") || trimmed.StartsWith("===")) continue;
            if (trimmed.StartsWith("Path", StringComparison.OrdinalIgnoreCase) && trimmed.Contains("Version")) { inTable = true; continue; }
            if (trimmed.StartsWith("ID", StringComparison.OrdinalIgnoreCase) && trimmed.Contains("Installed")) { inTable = true; continue; }
            // Pipe-delimited data rows.
            if (!line.Contains('|')) continue;
            if (!inTable && installed) inTable = true; // --list_installed has no "Installed packages:" preamble in some versions
            var cols = line.Split('|');
            var path = cols[0].Trim();
            if (path.Length == 0 || path.StartsWith("Path", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("ID", StringComparison.OrdinalIgnoreCase)) continue;
            // A valid package id has no spaces in its first token (e.g. "platforms;android-34").
            if (path.Contains(' ')) continue;
            var pkg = new SdkPackage
            {
                Path = path,
                Version = cols.Length > 1 ? cols[1].Trim() : "",
                Description = cols.Length > 2 ? cols[2].Trim() : "",
                Installed = installed,
                Category = CategoryOf(path),
            };
            res.Add(pkg);
        }
        return res;
    }

    /// <summary>列出已安裝套件 · List installed SDK packages (sdkmanager --list_installed).</summary>
    public static async Task<List<SdkPackage>> ListInstalledPackages(CancellationToken ct = default)
    {
        if (SdkManager.Length == 0) return new();
        var r = await ShellRunner.Run(SdkManager, "--list_installed", false, ct);
        return ParseList(r.Output ?? "", installed: true);
    }

    /// <summary>列出所有可用套件（含已安裝）· List all available SDK packages (sdkmanager --list).
    /// Merges in the installed set so the UI can show a ✓ on installed rows.</summary>
    public static async Task<List<SdkPackage>> ListAvailablePackages(CancellationToken ct = default)
    {
        if (SdkManager.Length == 0) return new();
        var installed = await ListInstalledPackages(ct);
        var installedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in installed) installedIds.Add(p.Path);

        var r = await ShellRunner.Run(SdkManager, "--list" + ChannelArg, false, ct);
        var avail = ParseList(r.Output ?? "", installed: false);
        // De-dup by path, prefer marking installed.
        var byPath = new Dictionary<string, SdkPackage>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in avail)
        {
            p.Installed = installedIds.Contains(p.Path);
            byPath[p.Path] = p;
        }
        // Ensure every installed package is present even if --list omitted it.
        foreach (var p in installed)
            if (!byPath.ContainsKey(p.Path)) byPath[p.Path] = p;
        return new List<SdkPackage>(byPath.Values);
    }

    /// <summary>安裝一個套件 · Install a package by id (sdkmanager "&lt;pkg&gt;"), auto-accepting prompts.</summary>
    public static Task<TweakResult> InstallPackage(string id, CancellationToken ct = default)
    {
        if (SdkManager.Length == 0) return Task.FromResult(TweakResult.Fail("sdkmanager not found.", "搵唔到 sdkmanager。"));
        if (string.IsNullOrWhiteSpace(id)) return Task.FromResult(TweakResult.Fail("No package id.", "冇套件 id。"));
        // yes | feeds the license/confirm prompt; quote the id (it contains ; and is cmd-safe inside quotes).
        return ShellRunner.RunCmd($"(echo y & echo y & echo y) | \"{SdkManager}\"{ChannelArg} \"{id}\"", false, ct);
    }

    /// <summary>更新所有套件 · Update all installed packages (sdkmanager --update).</summary>
    public static Task<TweakResult> UpdatePackages(CancellationToken ct = default)
    {
        if (SdkManager.Length == 0) return Task.FromResult(TweakResult.Fail("sdkmanager not found.", "搵唔到 sdkmanager。"));
        return ShellRunner.RunCmd($"(echo y & echo y & echo y) | \"{SdkManager}\"{ChannelArg} --update", false, ct);
    }

    /// <summary>移除一個套件 · Uninstall a package by id (sdkmanager --uninstall "&lt;pkg&gt;").</summary>
    public static Task<TweakResult> Uninstall(string id, CancellationToken ct = default)
    {
        if (SdkManager.Length == 0) return Task.FromResult(TweakResult.Fail("sdkmanager not found.", "搵唔到 sdkmanager。"));
        if (string.IsNullOrWhiteSpace(id)) return Task.FromResult(TweakResult.Fail("No package id.", "冇套件 id。"));
        return ShellRunner.RunCmd($"\"{SdkManager}\"{ChannelArg} --uninstall \"{id}\"", false, ct);
    }

    /// <summary>接受所有 SDK 授權 · Accept all SDK licenses (sdkmanager --licenses, auto-answering y).</summary>
    public static Task<TweakResult> AcceptLicenses(CancellationToken ct = default)
    {
        if (SdkManager.Length == 0) return Task.FromResult(TweakResult.Fail("sdkmanager not found.", "搵唔到 sdkmanager。"));
        // The licenses flow asks several y/n questions; pipe a stream of y's.
        return ShellRunner.RunCmd(
            $"(for /L %i in (1,1,50) do @echo y) | \"{SdkManager}\" --licenses", false, ct);
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
