using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>
/// 桌布輔助 · Desktop-wallpaper helpers.
/// 改完 JPEG 壓縮品質之後，要用 SystemParametersInfo 重新套用桌布先會生效。
/// After changing the JPEG import quality, the current wallpaper must be re-applied via
/// SystemParametersInfo (SPI_SETDESKWALLPAPER) for the new quality to take effect.
/// </summary>
public static class WallpaperHelper
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfo(uint action, uint uParam, string pvParam, uint winIni);

    private const uint SPI_SETDESKWALLPAPER = 0x0014;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDWININICHANGE = 0x02;

    /// <summary>
    /// 重新套用目前桌布 · Re-apply the current wallpaper so a new JPEG quality takes effect.
    /// 回傳 true 表示成功 · Returns true on success (false if no wallpaper is set or the call fails).
    /// </summary>
    public static bool ReapplyCurrentWallpaper()
    {
        var path = RegistryHelper.GetValue(RegRoot.HKCU, @"Control Panel\Desktop", "WallPaper") as string;
        if (string.IsNullOrWhiteSpace(path)) return false;
        return SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path,
            SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
    }
}
