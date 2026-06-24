using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個磁碟機 · One drive/volume.</summary>
public sealed class DriveRow
{
    public string Name { get; init; } = "";
    public string Label { get; init; } = "";
    public string Format { get; init; } = "";
    public string Type { get; init; } = "";
    public long Total { get; init; }
    public long Free { get; init; }
    public bool Ready { get; init; }

    public long Used => Total - Free;
    public double UsedPercent => Total > 0 ? Used * 100.0 / Total : 0;
}

/// <summary>
/// 磁碟機總覽同磁碟映像（純 C# 清單 + 原生 cmdlet）· Drive overview (DriveInfo) plus mount/dismount
/// and VHD creation via native cmdlets. All in-app, no redirect.
/// </summary>
public static class DriveService
{
    public static List<DriveRow> List()
    {
        var rows = new List<DriveRow>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.IsReady)
                {
                    rows.Add(new DriveRow
                    {
                        Name = d.Name,
                        Label = d.VolumeLabel,
                        Format = d.DriveFormat,
                        Type = d.DriveType.ToString(),
                        Total = d.TotalSize,
                        Free = d.AvailableFreeSpace,
                        Ready = true,
                    });
                }
                else
                {
                    rows.Add(new DriveRow { Name = d.Name, Type = d.DriveType.ToString(), Ready = false });
                }
            }
            catch { /* skip */ }
        }
        return rows;
    }

    private static string Esc(string s) => (s ?? "").Replace("'", "''");

    public static Task<TweakResult> MountImage(string path, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"Mount-DiskImage -ImagePath '{Esc(path)}' | Out-Null; 'Mounted {path}'", elevated: false, ct);

    public static Task<TweakResult> DismountImage(string path, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"Dismount-DiskImage -ImagePath '{Esc(path)}' | Out-Null; 'Dismounted {path}'", elevated: false, ct);

    public static Task<TweakResult> CreateVhd(string path, int sizeGb, bool dynamic, CancellationToken ct = default)
        => ShellRunner.RunPowershell(
            $"New-VHD -Path '{Esc(path)}' -SizeBytes {Math.Max(1, sizeGb)}GB {(dynamic ? "-Dynamic" : "-Fixed")} | Out-Null; 'Created {path}'",
            elevated: false, ct);

    public static string HumanSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double s = bytes;
        int i = 0;
        while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
        return $"{Math.Round(s, 1)} {u[i]}";
    }
}
