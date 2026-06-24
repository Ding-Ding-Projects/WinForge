using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Management.Deployment;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個已安裝嘅商店／UWP 應用程式 · One installed Store/UWP app (with icon + size).</summary>
public sealed class AppInfo : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string PackageFullName { get; set; } = "";
    public string PackageFamilyName { get; set; } = "";
    public string Publisher { get; set; } = "";
    public string Version { get; set; } = "";
    public string InstallLocation { get; set; } = "";

    private string _display = "";
    /// <summary>友善名稱，無就退回 short name · Friendly name; falls back to the short name when blank/unresolved.</summary>
    public string DisplayName
    {
        get => (string.IsNullOrWhiteSpace(_display) || _display.StartsWith("ms-resource", StringComparison.OrdinalIgnoreCase))
            ? ShortName : _display;
        set => _display = value;
    }

    public string ShortName
    {
        get
        {
            var dot = Name.LastIndexOf('.');
            return dot >= 0 && dot < Name.Length - 1 ? Name[(dot + 1)..] : Name;
        }
    }

    private string? _logoUri;
    public string? LogoUri { get => _logoUri; set { _logoUri = value; OnChanged(nameof(LogoUri)); OnChanged(nameof(HasLogo)); } }
    public bool HasLogo => !string.IsNullOrEmpty(_logoUri);

    private string _sizeText = "…";
    /// <summary>磁碟用量（安裝 + 使用者資料）· On-disk size (install + per-user data); streams in after listing.</summary>
    public string SizeText { get => _sizeText; set { _sizeText = value; OnChanged(nameof(SizeText)); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>
/// 應用程式內解除安裝（商店／UWP）· In-app uninstaller for Store/UWP apps via the PackageManager API
/// (rich metadata: logo, display name, family name) + Remove-AppxPackage. Frameworks/resource packages are
/// excluded so shared runtimes can't be removed. Deep uninstall also clears per-user leftovers. No Settings redirect.
/// </summary>
public static class UninstallManager
{
    public static Task<List<AppInfo>> ListAsync(CancellationToken ct = default) => Task.Run(() =>
    {
        var result = new List<AppInfo>();
        try
        {
            var pm = new PackageManager();
            foreach (var p in pm.FindPackagesForUser(string.Empty))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (p.IsFramework || p.IsResourcePackage) continue;
                }
                catch { continue; }

                string install = "", logo = "", disp = "", pub = "";
                try { install = p.InstalledPath ?? ""; } catch { }
                try { logo = p.Logo?.ToString() ?? ""; } catch { }
                try { disp = p.DisplayName ?? ""; } catch { }
                try { pub = p.PublisherDisplayName ?? ""; } catch { }

                string ver = "";
                try { var v = p.Id.Version; ver = $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}"; } catch { }

                result.Add(new AppInfo
                {
                    Name = SafeId(() => p.Id.Name),
                    PackageFullName = SafeId(() => p.Id.FullName),
                    PackageFamilyName = SafeId(() => p.Id.FamilyName),
                    Publisher = pub,
                    Version = ver,
                    InstallLocation = install,
                    DisplayName = disp,
                    LogoUri = logo,
                });
            }
        }
        catch { /* PackageManager unavailable — return what we have */ }
        return result.OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }, ct);

    private static string SafeId(Func<string> f) { try { return f() ?? ""; } catch { return ""; } }

    /// <summary>量度應用程式磁碟用量（安裝資料夾 + 使用者套件資料夾）· Best-effort on-disk size (install + per-user data).</summary>
    public static long MeasureSize(AppInfo app)
    {
        long total = DirSize(app.InstallLocation);
        if (!string.IsNullOrEmpty(app.PackageFamilyName))
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            total += DirSize(Path.Combine(local, "Packages", app.PackageFamilyName));
        }
        return total;
    }

    private static long DirSize(string dir)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return 0;
        long sum = 0;
        try
        {
            // Manual walk so a single access-denied subfolder doesn't abort the whole measurement.
            var stack = new Stack<string>();
            stack.Push(dir);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                try
                {
                    foreach (var f in Directory.EnumerateFiles(cur))
                        try { sum += new FileInfo(f).Length; } catch { }
                    foreach (var d in Directory.EnumerateDirectories(cur))
                        stack.Push(d);
                }
                catch { /* skip inaccessible folder */ }
            }
        }
        catch { }
        return sum;
    }

    public static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "—";
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double s = bytes;
        int i = 0;
        while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
        return $"{s:0.#} {u[i]}";
    }

    /// <summary>解除安裝（目前使用者）· Uninstall for the current user.</summary>
    public static Task<TweakResult> Uninstall(AppInfo app, CancellationToken ct = default)
        => ShellRunner.RunPowershell(
            $"Remove-AppxPackage -Package '{app.PackageFullName.Replace("'", "''")}' -ErrorAction Stop; 'Removed {app.ShortName}'",
            elevated: false, ct);

    /// <summary>
    /// 徹底解除安裝：移除套件後再清埋 %LocalAppData%\Packages\&lt;family&gt; 嘅殘留資料 ·
    /// Deep uninstall: remove the package, then clear the per-user leftover data folder
    /// (%LocalAppData%\Packages\&lt;PackageFamilyName&gt;). Returns a short summary of what was cleared.
    /// </summary>
    public static async Task<TweakResult> DeepUninstall(AppInfo app, CancellationToken ct = default)
    {
        var r = await Uninstall(app, ct);
        if (!r.Success) return r;

        long cleared = 0;
        string note = "";
        try
        {
            if (!string.IsNullOrEmpty(app.PackageFamilyName))
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var leftover = Path.Combine(local, "Packages", app.PackageFamilyName);
                if (Directory.Exists(leftover))
                {
                    cleared = DirSize(leftover);
                    try { Directory.Delete(leftover, recursive: true); note = "leftover data"; }
                    catch { note = "leftover data (partly locked)"; }
                }
            }
        }
        catch { }

        var freed = cleared > 0 ? $" · freed {FormatSize(cleared)} {note}" : "";
        return TweakResult.Ok($"Removed {app.ShortName}{freed}.", $"已徹底移除 {app.ShortName}{(cleared > 0 ? $"，清咗 {FormatSize(cleared)} 殘留資料" : "")}。");
    }
}
