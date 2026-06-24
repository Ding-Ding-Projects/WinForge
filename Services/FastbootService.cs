using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一部 fastboot/bootloader 裝置 · One device seen in fastboot/bootloader mode.</summary>
public sealed class FastbootDevice
{
    public string Serial { get; set; } = "";
    public string Mode { get; set; } = "fastboot";
    public string Display => $"{Serial} ({Mode})";
}

/// <summary>
/// 應用程式內 fastboot 面板（包真實 fastboot 引擎）· In-app fastboot panel wrapping fastboot.exe (ships with
/// Google.PlatformTools alongside adb). Reads bootloader state, flashes partitions and sideloads OTAs.
///
/// ⚠ DANGEROUS · 危險：flashing/unlocking can wipe data or brick the device. Every mutating call accepts a
/// <c>dryRun</c> flag that returns the exact command WITHOUT running it, and the UI guards each op behind an
/// explicit, typed confirmation. No redirect — WinForge drives the real binary.
/// </summary>
public static class FastbootService
{
    private static Task<string> Capture(string args, CancellationToken ct)
        => ShellRunner.CapturePowershell($"fastboot {args} 2>&1 | Out-String -Width 400", ct);

    public static async Task<bool> IsAvailable(CancellationToken ct = default)
    {
        try
        {
            var outp = await Capture("--version", ct);
            return outp.Contains("fastboot", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>List devices currently in fastboot/fastbootd mode.</summary>
    public static async Task<List<FastbootDevice>> Devices(CancellationToken ct = default)
    {
        var list = new List<FastbootDevice>();
        string outp;
        try { outp = await Capture("devices", ct); } catch { return list; }
        foreach (var raw in outp.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("<")) continue;
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            list.Add(new FastbootDevice { Serial = parts[0], Mode = parts[1] });
        }
        return list;
    }

    /// <summary>Read a bootloader variable (e.g. "unlocked", "product", "current-slot").</summary>
    public static async Task<string> GetVar(string serial, string name, CancellationToken ct = default)
    {
        var sel = string.IsNullOrEmpty(serial) ? "" : $"-s {serial} ";
        var outp = await Capture($"{sel}getvar {name}", ct);
        foreach (var raw in outp.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            int i = line.IndexOf(':');
            if (i > 0 && line.Substring(0, i).Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                return line.Substring(i + 1).Trim();
        }
        return "";
    }

    /// <summary>True/false/unknown for the bootloader unlock state (getvar unlocked).</summary>
    public static async Task<bool?> IsUnlocked(string serial, CancellationToken ct = default)
    {
        var v = (await GetVar(serial, "unlocked", ct)).ToLowerInvariant();
        if (v.Contains("yes") || v == "true") return true;
        if (v.Contains("no") || v == "false") return false;
        return null;
    }

    /// <summary>A quick bootloader summary: product, current slot, unlock state.</summary>
    public static async Task<string> Summary(string serial, CancellationToken ct = default)
    {
        var product = await GetVar(serial, "product", ct);
        var slot = await GetVar(serial, "current-slot", ct);
        var unlocked = await IsUnlocked(serial, ct);
        var u = unlocked is null ? "unknown · 未知" : (unlocked.Value ? "unlocked · 已解鎖" : "locked · 已鎖");
        return $"product: {product}\ncurrent-slot: {slot}\nbootloader: {u}";
    }

    private static string Sel(string serial) => string.IsNullOrEmpty(serial) ? "" : $"-s {serial} ";

    /// <summary>Run (or, with dryRun, just preview) a raw fastboot command line.</summary>
    private static Task<TweakResult> Exec(string serial, string subcommand, bool dryRun, CancellationToken ct)
    {
        var cmd = $"fastboot {Sel(serial)}{subcommand}".Trim();
        if (dryRun) return Task.FromResult(TweakResult.Ok($"[dry-run] {cmd}", $"[試行] {cmd}", cmd));
        return ShellRunner.RunCmd(cmd, false, ct);
    }

    // ── Mutating, DANGEROUS operations (all support dryRun) ─────────────────────────────

    /// <summary>Unlock the bootloader (WIPES ALL DATA). Tries `flashing unlock`, the modern verb.</summary>
    public static Task<TweakResult> Unlock(string serial, bool dryRun, CancellationToken ct = default)
        => Exec(serial, "flashing unlock", dryRun, ct);

    /// <summary>Re-lock the bootloader (also wipes data on most devices).</summary>
    public static Task<TweakResult> Lock(string serial, bool dryRun, CancellationToken ct = default)
        => Exec(serial, "flashing lock", dryRun, ct);

    /// <summary>Flash a single partition (e.g. "boot", "recovery") with an image file.</summary>
    public static Task<TweakResult> Flash(string serial, string partition, string imagePath, bool dryRun, CancellationToken ct = default)
        => Exec(serial, $"flash {partition} \"{imagePath}\"", dryRun, ct);

    /// <summary>Boot a kernel/recovery image once without flashing (safe way to test a patched boot.img).</summary>
    public static Task<TweakResult> BootImage(string serial, string imagePath, bool dryRun, CancellationToken ct = default)
        => Exec(serial, $"boot \"{imagePath}\"", dryRun, ct);

    /// <summary>Sideload an OTA zip from recovery's `adb sideload` (device must be in sideload mode via adb).</summary>
    public static Task<TweakResult> SideloadOta(string serial, string zipPath, bool dryRun, CancellationToken ct = default)
    {
        // Sideload is an ADB-recovery operation, not fastboot.
        var sel = string.IsNullOrEmpty(serial) ? "" : $"-s {serial} ";
        var cmd = $"adb {sel}sideload \"{zipPath}\"".Trim();
        if (dryRun) return Task.FromResult(TweakResult.Ok($"[dry-run] {cmd}", $"[試行] {cmd}", cmd));
        return ShellRunner.RunCmd(cmd, false, ct);
    }

    /// <summary>Flash an entire factory image via fastboot update package (zip of partitions).</summary>
    public static Task<TweakResult> FlashFactoryZip(string serial, string zipPath, bool wipe, bool dryRun, CancellationToken ct = default)
        => Exec(serial, $"update {(wipe ? "-w " : "")}\"{zipPath}\"", dryRun, ct);

    /// <summary>Reboot out of the bootloader back into the OS.</summary>
    public static Task<TweakResult> Reboot(string serial, bool dryRun, CancellationToken ct = default)
        => Exec(serial, "reboot", dryRun, ct);
}
