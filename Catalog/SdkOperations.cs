using System.Collections.Generic;

namespace WinForge.Catalog;

/// <summary>
/// Android SDK 套件分類同常用套件目錄 · Catalog metadata for the SDK package manager: bilingual labels
/// and a glyph for each sdkmanager category bucket, plus a small curated list of "quick install" package
/// ids. The package rows themselves are discovered live from sdkmanager --list (see EmulatorService); this
/// file only supplies the human-facing grouping text and a few common one-click suggestions.
/// </summary>
public static class SdkOperations
{
    /// <summary>一個套件分類 · One category bucket's display metadata.</summary>
    public sealed record Category(string Key, string En, string Zh, string Glyph);

    /// <summary>所有分類（顯示順序）· All known categories, in the order the UI should list them.</summary>
    public static readonly IReadOnlyList<Category> Categories = new List<Category>
    {
        new("platform-tools", "Platform Tools (adb, fastboot)", "平台工具（adb、fastboot）", ""),
        new("cmdline-tools",  "Command-line Tools",            "命令列工具",                 ""),
        new("emulator",       "Emulator",                      "模擬器",                     ""),
        new("platforms",      "SDK Platforms (Android APIs)",  "SDK 平台（Android API）",    ""),
        new("build-tools",    "Build Tools",                   "建置工具",                   ""),
        new("system-images",  "System Images (for AVDs)",      "系統映像（AVD 用）",         ""),
        new("ndk",            "NDK (native development)",      "NDK（原生開發）",            ""),
        new("sources",        "Sources",                       "原始碼",                     ""),
        new("extras",         "Extras",                        "額外組件",                   ""),
        new("other",          "Other",                         "其他",                       ""),
    };

    /// <summary>分類顯示名 · Friendly label for a category key (falls back to the raw key).</summary>
    public static (string en, string zh, string glyph) Label(string key)
    {
        foreach (var c in Categories)
            if (c.Key == key) return (c.En, c.Zh, c.Glyph);
        return (key, key, "");
    }

    /// <summary>一個常用套件建議 · One curated quick-install suggestion.</summary>
    public sealed record Suggestion(string Id, string En, string Zh);

    /// <summary>
    /// 常用套件（一鍵安裝）· A handful of commonly-needed packages offered as one-click installs, so a
    /// fresh SDK can be brought up to a working state without typing ids.
    /// </summary>
    public static readonly IReadOnlyList<Suggestion> QuickInstall = new List<Suggestion>
    {
        new("platform-tools", "Platform Tools (adb / fastboot)", "平台工具（adb／fastboot）"),
        new("cmdline-tools;latest", "Command-line Tools (latest)", "命令列工具（最新）"),
        new("emulator", "Android Emulator", "Android 模擬器"),
        new("platforms;android-34", "Android 14 platform (API 34)", "Android 14 平台（API 34）"),
        new("build-tools;34.0.0", "Build Tools 34.0.0", "建置工具 34.0.0"),
        new("system-images;android-34;google_apis;x86_64", "System image · Android 14 · x86_64", "系統映像 · Android 14 · x86_64"),
    };
}
