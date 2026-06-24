using System;
using System.Collections.Generic;
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
            var path = Path.Combine(Path.GetTempPath(), $"wintune-{DateTime.Now:yyyyMMdd-HHmmss}.wsb");
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
