using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 一個 World Monitor 變體（網址映射） · One World Monitor variant (key → URL mapping).
/// </summary>
public sealed record WmVariant(string Key, string En, string Zh, string Url);

/// <summary>
/// World Monitor 模組嘅服務層 · Service layer for the World Monitor module.
///
/// World Monitor (koala73/worldmonitor) 係一個 AGPL-3.0 嘅實時全球情報儀表板
/// （新聞、地緣政治、金融、能源、不穩定指數）。佢用 TypeScript + WebGL 3D 地球，
/// 重新用 WinUI 寫晒係唔切實際嘅，所以 WinForge 用 WebView2 內嵌官方寄存網頁，
/// 配上原生 WinForge 工具列（變體切換、重載、外開瀏覽器、縮放）。
///
/// World Monitor (koala73/worldmonitor) is an AGPL-3.0 real-time global intelligence
/// dashboard (news, geopolitics, finance, energy, an instability index). It is a
/// TypeScript + WebGL 3D-globe app; re-writing it natively in WinUI is out of scope,
/// so WinForge embeds the official hosted web app in a WebView2 with a native toolbar
/// (variant switch, reload, open-in-browser, zoom).
///
/// AGPL 合規 · AGPL compliance: WinForge embeds the hosted web origin / shells out to
/// the unmodified upstream binary; it never forks, vendors or recompiles WM source.
/// URL 映射可由設定覆寫 · the URL map is config-overridable so upstream URL changes
/// don't break the embed.
/// </summary>
public sealed class WorldMonitorService
{
    private const string KeyVariant = "worldmonitor.variant";
    private const string KeyZoom = "worldmonitor.zoom";
    private const string KeyUrlPrefix = "worldmonitor.url."; // per-variant override
    private const string KeyBinaryPath = "worldmonitor.binarypath";

    /// <summary>內建變體（網址可由設定覆寫）· Built-in variants (URLs override-able via settings).</summary>
    public static readonly IReadOnlyList<WmVariant> DefaultVariants = new[]
    {
        new WmVariant("world",     "World",     "世界",   "https://worldmonitor.app"),
        new WmVariant("tech",      "Tech",      "科技",   "https://tech.worldmonitor.app"),
        new WmVariant("finance",   "Finance",   "金融",   "https://finance.worldmonitor.app"),
        new WmVariant("commodity", "Commodity", "商品",   "https://commodity.worldmonitor.app"),
        new WmVariant("energy",    "Energy",    "能源",   "https://energy.worldmonitor.app"),
        new WmVariant("happy",     "Happy",     "快樂",   "https://happy.worldmonitor.app"),
    };

    /// <summary>上游 Windows 桌面安裝檔下載網址 · Upstream Windows desktop installer download URL.</summary>
    public const string WindowsDownloadUrl = "https://worldmonitor.app/api/download?platform=windows-exe";

    /// <summary>文件網址 · Documentation URL.</summary>
    public const string DocsUrl = "https://www.worldmonitor.app/docs/documentation";

    /// <summary>解析一個變體嘅有效網址（設定覆寫優先）· Effective URL for a variant (setting override wins).</summary>
    public string UrlFor(WmVariant v)
    {
        var ovr = SettingsStore.Get(KeyUrlPrefix + v.Key, "");
        return string.IsNullOrWhiteSpace(ovr) ? v.Url : ovr.Trim();
    }

    /// <summary>覆寫一個變體網址（空字串＝還原內建）· Override a variant URL (empty ⇒ reset to built-in).</summary>
    public void SetUrlOverride(string key, string url)
        => SettingsStore.Set(KeyUrlPrefix + key, (url ?? "").Trim());

    /// <summary>上次選用嘅變體 key（預設 world）· Last used variant key (defaults to "world").</summary>
    public string LastVariantKey
    {
        get => SettingsStore.Get(KeyVariant, "world");
        set => SettingsStore.Set(KeyVariant, value);
    }

    public WmVariant LastVariant =>
        DefaultVariants.FirstOrDefault(x => x.Key == LastVariantKey) ?? DefaultVariants[0];

    /// <summary>記住嘅縮放（0.5–3.0）· Remembered zoom factor (0.5–3.0).</summary>
    public double Zoom
    {
        get
        {
            var s = SettingsStore.Get(KeyZoom, "1.0");
            return double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var z)
                ? Math.Clamp(z, 0.5, 3.0) : 1.0;
        }
        set => SettingsStore.Set(KeyZoom,
            Math.Clamp(value, 0.5, 3.0).ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>記住嘅桌面版安裝路徑 · Remembered path to the wrapped desktop binary.</summary>
    public string BinaryPath
    {
        get => SettingsStore.Get(KeyBinaryPath, "");
        set => SettingsStore.Set(KeyBinaryPath, value ?? "");
    }

    public bool HasBinary => !string.IsNullOrWhiteSpace(BinaryPath) && File.Exists(BinaryPath);

    /// <summary>用系統預設瀏覽器開一條網址 · Open a URL in the system default browser.</summary>
    public static void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch { /* best effort */ }
    }

    /// <summary>啟動已包裝嘅桌面二進位檔 · Launch the wrapped Tauri desktop binary via ShellRunner.</summary>
    public async Task<(bool ok, string message)> LaunchBinaryAsync()
    {
        if (!HasBinary) return (false, "Binary not set or missing · 未設定或搵唔到安裝檔");
        try
        {
            Process.Start(new ProcessStartInfo { FileName = BinaryPath, UseShellExecute = true });
            await Task.CompletedTask;
            return (true, BinaryPath);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    /// <summary>
    /// 下載上游 Windows 安裝檔（簽署版）· Download the upstream signed Windows installer.
    /// 因為 AGPL 唔可以重新編譯，所以淨係下載官方已簽署嘅 release。
    /// AGPL forbids recompiling, so we only fetch the official signed release.
    /// </summary>
    public async Task<(bool ok, string path, string message)> DownloadInstallerAsync(
        string destDir, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(destDir);
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("WinForge-WorldMonitor/1.0");
            using var resp = await http.GetAsync(WindowsDownloadUrl,
                HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            // 由 Content-Disposition 或網址尾段取檔名 · derive a filename.
            var name = resp.Content.Headers.ContentDisposition?.FileNameStar
                       ?? resp.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                       ?? "WorldMonitor-Setup.exe";
            name = name.Trim('"');
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                name = "WorldMonitor-Setup.exe";
            var path = Path.Combine(destDir, name);

            var total = resp.Content.Headers.ContentLength ?? -1L;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(path);
            var buf = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                read += n;
                if (total > 0) progress?.Report(Math.Min(1.0, (double)read / total));
            }
            return (true, path, $"{read / 1024 / 1024} MB");
        }
        catch (OperationCanceledException) { return (false, "", "Cancelled · 已取消"); }
        catch (Exception ex) { return (false, "", ex.Message); }
    }
}
