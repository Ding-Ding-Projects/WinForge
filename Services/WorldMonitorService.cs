using System;
using System.Collections.Generic;
using System.Linq;

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
/// 配上原生 WinForge 工具列（變體切換、重載、複製網址、縮放）。
///
/// World Monitor (koala73/worldmonitor) is an AGPL-3.0 real-time global intelligence
/// dashboard (news, geopolitics, finance, energy, an instability index). It is a
/// TypeScript + WebGL 3D-globe app; re-writing it natively in WinUI is out of scope,
/// so WinForge embeds the official hosted web app in a WebView2 with a native toolbar
/// (variant switch, reload, copy URL, zoom).
///
/// AGPL 合規 · AGPL compliance: WinForge embeds the hosted web origin; it never forks,
/// vendors, recompiles or launches the upstream binary.
/// URL 映射可由設定覆寫 · the URL map is config-overridable so upstream URL changes
/// don't break the embed.
/// </summary>
public sealed class WorldMonitorService
{
    private const string KeyVariant = "worldmonitor.variant";
    private const string KeyZoom = "worldmonitor.zoom";
    private const string KeyUrlPrefix = "worldmonitor.url."; // per-variant override

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

}
