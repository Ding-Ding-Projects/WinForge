using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個已安裝嘅 WSL 發行版 · One installed WSL distribution row.</summary>
public sealed class WslDistro
{
    public string Name { get; set; } = "";
    public string State { get; set; } = "";
    public int Version { get; set; }
    public bool IsDefault { get; set; }

    public string Display => $"{(IsDefault ? "★ " : "")}{Name} · {State} · WSL{Version}";
}

/// <summary>一個可供安裝嘅線上發行版 · One online (installable) distribution row.</summary>
public sealed class WslOnlineDistro
{
    public string Name { get; set; } = "";
    public string FriendlyName { get; set; } = "";

    public string Display => string.IsNullOrEmpty(FriendlyName) || FriendlyName == Name
        ? Name : $"{FriendlyName} ({Name})";
}

/// <summary>WSL GUI 狀態 · WSL GUI capability/settings status.</summary>
public sealed record WslGuiStatus(
    bool WslAvailable,
    bool WslgLikelyAvailable,
    bool SystemdEnabled,
    bool WaylandSocketPresent,
    bool X11SocketPresent,
    string Distro,
    string Kernel,
    string MessageEn,
    string MessageZh);

/// <summary>Linux .desktop 應用程式 · One GUI app discovered from Linux .desktop files.</summary>
public sealed record WslGuiApp(
    string Distro,
    string Id,
    string Name,
    string Comment,
    string Exec,
    string Icon,
    string DesktopFile,
    bool Terminal);

/// <summary>WSL 檔案項目 · One file/folder row from a WSL path.</summary>
public sealed record WslFileEntry(
    string Distro,
    string LinuxPath,
    string WindowsPath,
    string Name,
    bool IsDirectory,
    long Size,
    DateTimeOffset Modified);

/// <summary>WSL GUI 設定 · Small set of safe ~/.wslconfig and /etc/wsl.conf settings.</summary>
public sealed record WslGuiSettings(
    bool EnableGuiApplications = true,
    bool EnableSystemd = true,
    bool EnableNetworking = true,
    bool EnableInterop = true,
    int MemoryGb = 0,
    int Processors = 0);

/// <summary>
/// 應用程式內 WSL 與 Windows 沙盒啟動器（包真實 wsl.exe / WindowsSandbox.exe 引擎）·
/// In-app WSL distro manager + Windows Sandbox launcher wrapping wsl.exe and WindowsSandbox.exe — list /
/// install / export / import / set-default / shutdown WSL distros, and emit a .wsb config to start
/// Windows Sandbox with mapped folders and networking. No redirect. Bilingual.
/// </summary>
public static class WslVmService
{
    // ── WSL ────────────────────────────────────────────────────────────────

    private static Task<string> CaptureWsl(string args, CancellationToken ct)
        // wsl.exe emits UTF-16LE; force UTF-8 so PowerShell capture is clean.
        => ShellRunner.CapturePowershell(
            "$env:WSL_UTF8=1; wsl.exe " + args + " 2>&1 | Out-String -Width 400", ct);

    public static async Task<bool> IsWslAvailable(CancellationToken ct = default)
    {
        try
        {
            var outp = await CaptureWsl("--status", ct);
            // --status fails on very old builds; fall back to --version.
            if (outp.Contains("not recognized", StringComparison.OrdinalIgnoreCase) ||
                outp.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                var v = await CaptureWsl("--version", ct);
                return !v.Contains("not recognized", StringComparison.OrdinalIgnoreCase)
                    && !v.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    && v.Trim().Length > 0;
            }
            return true;
        }
        catch { return false; }
    }

    /// <summary>列出已安裝發行版 · Parse `wsl --list --verbose` into rows.</summary>
    public static async Task<List<WslDistro>> ListDistros(CancellationToken ct = default)
    {
        var list = new List<WslDistro>();
        string outp;
        try { outp = await CaptureWsl("--list --verbose", ct); } catch { return list; }

        foreach (var raw in outp.Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Trim().Length == 0) continue;
            // Header row: "  NAME   STATE   VERSION"
            if (line.Contains("NAME", StringComparison.OrdinalIgnoreCase)
                && line.Contains("STATE", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.Contains("Windows Subsystem for Linux", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.TrimStart().StartsWith("wsl.exe", StringComparison.OrdinalIgnoreCase)) continue;

            bool isDefault = line.TrimStart().StartsWith("*");
            var body = line.Replace("*", " ").Trim();
            var parts = body.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;

            var d = new WslDistro { Name = parts[0], State = parts[1], IsDefault = isDefault };
            if (int.TryParse(parts[2], out var ver)) d.Version = ver;
            list.Add(d);
        }
        return list;
    }

    /// <summary>列出線上可裝發行版 · Parse `wsl --list --online`.</summary>
    public static async Task<List<WslOnlineDistro>> ListOnline(CancellationToken ct = default)
    {
        var list = new List<WslOnlineDistro>();
        string outp;
        try { outp = await CaptureWsl("--list --online", ct); } catch { return list; }

        bool started = false;
        foreach (var raw in outp.Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Trim().Length == 0) continue;
            // The table header is "NAME   FRIENDLY NAME"; rows start after it.
            if (line.Contains("NAME", StringComparison.OrdinalIgnoreCase)
                && line.Contains("FRIENDLY", StringComparison.OrdinalIgnoreCase)) { started = true; continue; }
            if (!started) continue;
            if (line.TrimStart().StartsWith("*")) continue;

            var parts = line.Trim().Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            list.Add(new WslOnlineDistro { Name = parts[0], FriendlyName = parts.Length > 1 ? parts[1].Trim() : parts[0] });
        }
        return list;
    }

    public static Task<TweakResult> InstallDistro(string distro, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"$env:WSL_UTF8=1; wsl.exe --install -d {Quote(distro)} --no-launch", false, ct);

    public static Task<TweakResult> SetDefault(string distro, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"$env:WSL_UTF8=1; wsl.exe --set-default {Quote(distro)}", false, ct);

    public static Task<TweakResult> Terminate(string distro, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"$env:WSL_UTF8=1; wsl.exe --terminate {Quote(distro)}", false, ct);

    public static Task<TweakResult> Unregister(string distro, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"$env:WSL_UTF8=1; wsl.exe --unregister {Quote(distro)}", false, ct);

    public static Task<TweakResult> Shutdown(CancellationToken ct = default)
        => ShellRunner.RunPowershell("$env:WSL_UTF8=1; wsl.exe --shutdown", false, ct);

    public static Task<TweakResult> Export(string distro, string tarPath, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"$env:WSL_UTF8=1; wsl.exe --export {Quote(distro)} {Quote(tarPath)}", false, ct);

    public static Task<TweakResult> Import(string name, string installDir, string tarPath, CancellationToken ct = default)
        => ShellRunner.RunPowershell(
            $"$env:WSL_UTF8=1; wsl.exe --import {Quote(name)} {Quote(installDir)} {Quote(tarPath)}", false, ct);

    /// <summary>喺指定 distro 執行 Linux shell 指令並擷取輸出 · Run a shell command inside a distro and capture output.</summary>
    public static Task<string> CaptureDistro(string distro, string command, CancellationToken ct = default)
        => CaptureWsl($"-d {Quote(distro)} -- sh -lc {Quote(command)}", ct);

    /// <summary>喺指定 distro 執行 Linux shell 指令 · Run a shell command inside a distro.</summary>
    public static Task<TweakResult> RunDistro(string distro, string command, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"$env:WSL_UTF8=1; wsl.exe -d {Quote(distro)} -- sh -lc {Quote(command)}", false, ct);

    /// <summary>讀取 WSL GUI / WSLg 狀態 · Inspect WSL GUI / WSLg readiness for one distro.</summary>
    public static async Task<WslGuiStatus> GetGuiStatus(string distro, CancellationToken ct = default)
    {
        var available = await IsWslAvailable(ct);
        if (!available)
            return new(false, false, false, false, false, distro, "",
                "WSL is not available.", "WSL 未可用。");

        string outp;
        try
        {
            outp = await CaptureDistro(distro,
                "printf 'KERNEL=%s\\n' \"$(uname -r 2>/dev/null)\"; " +
                "printf 'WAYLAND=%s\\n' \"${WAYLAND_DISPLAY:-}\"; " +
                "printf 'DISPLAY=%s\\n' \"${DISPLAY:-}\"; " +
                "test -S \"${XDG_RUNTIME_DIR:-/mnt/wslg/runtime-dir}/wayland-0\" && echo WAYLAND_SOCKET=1 || echo WAYLAND_SOCKET=0; " +
                "test -S /tmp/.X11-unix/X0 && echo X11_SOCKET=1 || echo X11_SOCKET=0; " +
                "grep -qi '^systemd *= *true' /etc/wsl.conf 2>/dev/null && echo SYSTEMD=1 || echo SYSTEMD=0", ct);
        }
        catch
        {
            return new(true, false, false, false, false, distro, "",
                "Could not query the distro.", "無法查詢呢個發行版。");
        }

        var kernel = ReadKey(outp, "KERNEL");
        var wayland = ReadKey(outp, "WAYLAND");
        var display = ReadKey(outp, "DISPLAY");
        var waylandSocket = ReadKey(outp, "WAYLAND_SOCKET") == "1";
        var x11Socket = ReadKey(outp, "X11_SOCKET") == "1";
        var systemd = ReadKey(outp, "SYSTEMD") == "1";
        var wslg = wayland.Length > 0 || display.Length > 0 || waylandSocket || x11Socket || kernel.Contains("microsoft", StringComparison.OrdinalIgnoreCase);

        return new(true, wslg, systemd, waylandSocket, x11Socket, distro, kernel,
            wslg ? "WSL GUI support looks available." : "WSL GUI support was not detected.",
            wslg ? "WSL GUI 支援睇落可用。" : "偵測唔到 WSL GUI 支援。");
    }

    /// <summary>寫出基本 WSL GUI 設定 · Write safe WSL GUI settings to ~/.wslconfig and /etc/wsl.conf.</summary>
    public static async Task<TweakResult> ApplyGuiSettings(string distro, WslGuiSettings settings, CancellationToken ct = default)
    {
        try
        {
            var wslConfig = BuildUserWslConfig(settings);
            var userPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wslconfig");
            await File.WriteAllTextAsync(userPath, wslConfig, new UTF8Encoding(false), ct);

            var wslConf = BuildDistroWslConf(settings);
            var escaped = ShellSingleQuote(wslConf);
            var r = await RunDistro(distro, $"printf %s {escaped} | sudo tee /etc/wsl.conf >/dev/null", ct);
            if (!r.Success) return r;
            return TweakResult.Ok("WSL GUI settings saved. Run WSL shutdown/restart to apply all VM settings.",
                "已儲存 WSL GUI 設定。請關閉再重開 WSL，令 VM 設定全部生效。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    public static string BuildUserWslConfig(WslGuiSettings settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[wsl2]");
        sb.AppendLine($"guiApplications={(settings.EnableGuiApplications ? "true" : "false")}");
        if (settings.MemoryGb > 0) sb.AppendLine($"memory={settings.MemoryGb}GB");
        if (settings.Processors > 0) sb.AppendLine($"processors={settings.Processors}");
        return sb.ToString();
    }

    public static string BuildDistroWslConf(WslGuiSettings settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[boot]");
        sb.AppendLine($"systemd={(settings.EnableSystemd ? "true" : "false")}");
        sb.AppendLine();
        sb.AppendLine("[network]");
        sb.AppendLine($"generateResolvConf={(settings.EnableNetworking ? "true" : "false")}");
        sb.AppendLine();
        sb.AppendLine("[interop]");
        sb.AppendLine($"enabled={(settings.EnableInterop ? "true" : "false")}");
        sb.AppendLine("appendWindowsPath=true");
        return sb.ToString();
    }

    /// <summary>列出 Linux GUI app · List installed Linux GUI apps by reading .desktop files.</summary>
    public static async Task<List<WslGuiApp>> ListGuiApps(string distro, CancellationToken ct = default)
    {
        var list = new List<WslGuiApp>();
        string outp;
        try
        {
            outp = await CaptureDistro(distro,
                "for d in /usr/share/applications ~/.local/share/applications; do " +
                "  [ -d \"$d\" ] || continue; " +
                "  find \"$d\" -maxdepth 1 -type f -name '*.desktop' -print; " +
                "done | sort | while IFS= read -r f; do " +
                "  n=$(grep -m1 '^Name=' \"$f\" | cut -d= -f2-); " +
                "  e=$(grep -m1 '^Exec=' \"$f\" | cut -d= -f2-); " +
                "  h=$(grep -m1 '^NoDisplay=' \"$f\" | cut -d= -f2-); " +
                "  [ -n \"$n\" ] && [ -n \"$e\" ] && [ \"$h\" != \"true\" ] || continue; " +
                "  c=$(grep -m1 '^Comment=' \"$f\" | cut -d= -f2-); " +
                "  i=$(grep -m1 '^Icon=' \"$f\" | cut -d= -f2-); " +
                "  t=$(grep -m1 '^Terminal=' \"$f\" | cut -d= -f2-); " +
                "  printf '%s\\t%s\\t%s\\t%s\\t%s\\t%s\\n' \"$f\" \"$n\" \"$c\" \"$e\" \"$i\" \"$t\"; " +
                "done", ct);
        }
        catch { return list; }

        foreach (var raw in outp.Replace("\r", "").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var p = raw.Split('\t');
            if (p.Length < 6) continue;
            var file = p[0].Trim();
            var id = Path.GetFileNameWithoutExtension(file);
            list.Add(new WslGuiApp(distro, id, p[1].Trim(), p[2].Trim(), p[3].Trim(), p[4].Trim(), file,
                p[5].Trim().Equals("true", StringComparison.OrdinalIgnoreCase)));
        }
        return list;
    }

    /// <summary>啟動一個 Linux GUI app · Launch a Linux GUI app from its Exec line.</summary>
    public static Task<TweakResult> LaunchGuiApp(WslGuiApp app, CancellationToken ct = default)
    {
        var exec = StripDesktopExecTokens(app.Exec);
        return RunDistro(app.Distro, $"{exec} >/dev/null 2>&1 &", ct);
    }

    /// <summary>將 Linux GUI app 匯出成 Windows .cmd launcher · Export a GUI app launcher to any folder.</summary>
    public static async Task<TweakResult> ExportGuiAppLauncher(WslGuiApp app, string targetDirectory, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(targetDirectory);
            var safe = SafeFileName(string.IsNullOrWhiteSpace(app.Name) ? app.Id : app.Name);
            var path = Path.Combine(targetDirectory, $"{safe}.cmd");
            var body = "@echo off\r\n" +
                       $"wsl.exe -d {QuoteForCmd(app.Distro)} -- sh -lc {QuoteForCmd(StripDesktopExecTokens(app.Exec) + " >/dev/null 2>&1 &")}\r\n";
            await File.WriteAllTextAsync(path, body, new UTF8Encoding(false), ct);
            return TweakResult.Ok($"Exported launcher: {path}", $"已匯出啟動器：{path}");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>為 Linux GUI app 建立 Windows 捷徑 · Create a Windows shortcut for a WSL GUI app.</summary>
    public static Task<TweakResult> CreateGuiAppShortcut(WslGuiApp app, string shortcutPath, CancellationToken ct = default)
    {
        var exec = StripDesktopExecTokens(app.Exec) + " >/dev/null 2>&1 &";
        var ps = "$ws = New-Object -ComObject WScript.Shell; " +
                 "$s = $ws.CreateShortcut(" + PsQuote(shortcutPath) + "); " +
                 "$s.TargetPath = 'wsl.exe'; " +
                 "$s.Arguments = " + PsQuote($"-d {Quote(app.Distro)} -- sh -lc {Quote(exec)}") + "; " +
                 "$s.WorkingDirectory = " + PsQuote(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) + "; " +
                 "$s.Description = " + PsQuote($"WinForge WSL GUI app: {app.Name}") + "; " +
                 "$s.Save()";
        return ShellRunner.RunPowershell(ps, false, ct);
    }

    public static string DesktopShortcutPath(string fileName)
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), SafeFileName(fileName) + ".lnk");

    /// <summary>Linux path → \\wsl.localhost UNC path · Convert a Linux path to a Windows Explorer path.</summary>
    public static string ToWindowsPath(string distro, string linuxPath)
    {
        linuxPath = string.IsNullOrWhiteSpace(linuxPath) ? "/" : linuxPath.Trim();
        linuxPath = linuxPath.Replace('\\', '/');
        if (linuxPath.StartsWith("/mnt/", StringComparison.OrdinalIgnoreCase) && linuxPath.Length > 6)
        {
            var drive = linuxPath[5];
            var rest = linuxPath.Length > 7 ? linuxPath.Substring(7).Replace('/', '\\') : "";
            return char.ToUpperInvariant(drive) + @":\" + rest;
        }
        return @"\\wsl.localhost\" + distro + linuxPath.Replace('/', '\\');
    }

    /// <summary>Windows path → WSL path · Convert a Windows path to a WSL path for one distro.</summary>
    public static async Task<string> ToLinuxPath(string distro, string windowsPath, CancellationToken ct = default)
    {
        try
        {
            var outp = await CaptureDistro(distro, $"wslpath -a {ShellSingleQuote(windowsPath)}", ct);
            return outp.Trim();
        }
        catch { return ""; }
    }

    /// <summary>列出 WSL 資料夾內容 · List a WSL folder using its Explorer-accessible UNC path.</summary>
    public static List<WslFileEntry> ListFiles(string distro, string linuxPath)
    {
        var list = new List<WslFileEntry>();
        var win = ToWindowsPath(distro, linuxPath);
        try
        {
            if (!Directory.Exists(win)) return list;
            foreach (var dir in Directory.GetDirectories(win))
            {
                var di = new DirectoryInfo(dir);
                list.Add(new WslFileEntry(distro, CombineLinux(linuxPath, di.Name), dir, di.Name, true, 0, di.LastWriteTime));
            }
            foreach (var file in Directory.GetFiles(win))
            {
                var fi = new FileInfo(file);
                list.Add(new WslFileEntry(distro, CombineLinux(linuxPath, fi.Name), file, fi.Name, false, fi.Length, fi.LastWriteTime));
            }
        }
        catch { }
        return list;
    }

    /// <summary>喺 Windows Explorer 開 WSL 路徑 · Open a WSL path in Windows Explorer.</summary>
    public static TweakResult OpenExplorer(string distro, string linuxPath)
    {
        try
        {
            var path = ToWindowsPath(distro, linuxPath);
            Process.Start(new ProcessStartInfo("explorer.exe", Quote(path)) { UseShellExecute = true });
            return TweakResult.Ok("Opened WSL folder.", "已開啟 WSL 資料夾。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>複製 Windows 檔案入 WSL · Copy a Windows file into a WSL folder.</summary>
    public static TweakResult CopyWindowsFileToWsl(string distro, string windowsFile, string linuxDirectory, bool overwrite = true)
    {
        try
        {
            var target = Path.Combine(ToWindowsPath(distro, linuxDirectory), Path.GetFileName(windowsFile));
            File.Copy(windowsFile, target, overwrite);
            return TweakResult.Ok("Copied file into WSL.", "已複製檔案入 WSL。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>由 WSL 複製檔案去 Windows · Copy a WSL file to a Windows folder.</summary>
    public static TweakResult CopyWslFileToWindows(string distro, string linuxFile, string windowsDirectory, bool overwrite = true)
    {
        try
        {
            Directory.CreateDirectory(windowsDirectory);
            var source = ToWindowsPath(distro, linuxFile);
            var target = Path.Combine(windowsDirectory, Path.GetFileName(source));
            File.Copy(source, target, overwrite);
            return TweakResult.Ok("Copied file from WSL.", "已由 WSL 複製檔案。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>開一個發行版嘅互動式終端機（喺新視窗）· Launch an interactive terminal for a distro in a new window.</summary>
    public static Task<TweakResult> LaunchTerminal(string distro, CancellationToken ct = default)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = $"-d {Quote(distro)}",
                UseShellExecute = true, // opens its own console window
            };
            System.Diagnostics.Process.Start(psi);
            return Task.FromResult(TweakResult.Ok("Opened a terminal.", "已開終端機。"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"));
        }
    }

    private static string Quote(string s) => "\"" + (s ?? "").Replace("\"", "") + "\"";

    private static string ReadKey(string text, string key)
    {
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (!line.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)) continue;
            return line.Substring(key.Length + 1).Trim();
        }
        return "";
    }

    private static string ShellSingleQuote(string s) => "'" + (s ?? "").Replace("'", "'\"'\"'") + "'";

    private static string PsQuote(string s) => "'" + (s ?? "").Replace("'", "''") + "'";

    private static string QuoteForCmd(string s) => "\"" + (s ?? "").Replace("\"", "\\\"") + "\"";

    private static string StripDesktopExecTokens(string exec)
    {
        if (string.IsNullOrWhiteSpace(exec)) return "";
        var tokens = new[] { "%f", "%F", "%u", "%U", "%i", "%c", "%k", "%v", "%m", "%d", "%D", "%n", "%N" };
        foreach (var t in tokens) exec = exec.Replace(t, "", StringComparison.OrdinalIgnoreCase);
        return exec.Trim();
    }

    private static string SafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "WSL app" : name.Trim();
    }

    private static string CombineLinux(string dir, string name)
    {
        if (string.IsNullOrWhiteSpace(dir) || dir == "/") return "/" + name;
        return dir.TrimEnd('/') + "/" + name;
    }

    // ── Windows Sandbox ─────────────────────────────────────────────────────

    public static bool IsSandboxAvailable()
    {
        foreach (var dir in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.System),
                     Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                 })
        {
            if (string.IsNullOrEmpty(dir)) continue;
            if (File.Exists(Path.Combine(dir, "WindowsSandbox.exe"))) return true;
        }
        return false;
    }

    /// <summary>
    /// 砌一個 .wsb 設定檔 · Build a Windows Sandbox configuration XML.
    /// </summary>
    public static string BuildWsbXml(IEnumerable<(string Host, bool ReadOnly)> mappedFolders,
        bool networking, bool vGpu, bool clipboard, string? logonCommand)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Configuration>");
        sb.AppendLine($"  <Networking>{(networking ? "Default" : "Disable")}</Networking>");
        sb.AppendLine($"  <vGPU>{(vGpu ? "Enable" : "Disable")}</vGPU>");
        sb.AppendLine($"  <ClipboardRedirection>{(clipboard ? "Default" : "Disable")}</ClipboardRedirection>");

        var folders = new List<(string Host, bool ReadOnly)>();
        foreach (var f in mappedFolders)
            if (!string.IsNullOrWhiteSpace(f.Host)) folders.Add(f);

        if (folders.Count > 0)
        {
            sb.AppendLine("  <MappedFolders>");
            foreach (var f in folders)
            {
                sb.AppendLine("    <MappedFolder>");
                sb.AppendLine($"      <HostFolder>{Esc(f.Host.Trim())}</HostFolder>");
                sb.AppendLine($"      <ReadOnly>{(f.ReadOnly ? "true" : "false")}</ReadOnly>");
                sb.AppendLine("    </MappedFolder>");
            }
            sb.AppendLine("  </MappedFolders>");
        }

        if (!string.IsNullOrWhiteSpace(logonCommand))
        {
            sb.AppendLine("  <LogonCommand>");
            sb.AppendLine($"    <Command>{Esc(logonCommand.Trim())}</Command>");
            sb.AppendLine("  </LogonCommand>");
        }

        sb.AppendLine("</Configuration>");
        return sb.ToString();
    }

    private static string Esc(string s) => (s ?? "")
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
        .Replace("\"", "&quot;");

    /// <summary>寫出一個 .wsb 檔再用 WindowsSandbox.exe 起動 · Write a .wsb file and launch it.</summary>
    public static async Task<TweakResult> LaunchSandbox(string wsbXml, CancellationToken ct = default)
    {
        if (!IsSandboxAvailable())
            return TweakResult.Fail("Windows Sandbox is not enabled.", "未啟用 Windows 沙盒。");
        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"winforge-{DateTime.Now:yyyyMMdd-HHmmss}.wsb");
            await File.WriteAllTextAsync(path, wsbXml, ct);
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "WindowsSandbox.exe",
                Arguments = Quote(path),
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(psi);
            return TweakResult.Ok($"Started Windows Sandbox ({path}).", $"已啟動 Windows 沙盒（{path}）。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>啟用 Windows 沙盒功能（要管理員 + 重啟）· Enable the Windows Sandbox feature via DISM (admin, reboot).</summary>
    public static Task<TweakResult> EnableSandboxFeature(CancellationToken ct = default)
        => ShellRunner.Run("dism.exe",
            "/Online /Enable-Feature /FeatureName:Containers-DisposableClientVM /All /NoRestart",
            elevated: true, ct);
}
