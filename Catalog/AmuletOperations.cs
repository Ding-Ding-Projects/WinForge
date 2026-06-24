using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// Amulet 設定／維護操作目錄 · Catalog of Amulet setup / maintenance operations rendered as self-contained
/// <see cref="Controls.TweakCard"/> action rows: extract the bundled zip, install Python deps, open the
/// managed app-data dir, and open the default .minecraft\saves folder. Each action drives
/// <see cref="AmuletService"/> directly and returns a bilingual result. No redirect.
/// </summary>
public static class AmuletOperations
{
    public static IEnumerable<TweakDefinition> All => new List<TweakDefinition>
    {
        Tweak.Action("amulet.extract",
            "Extract / re-extract Amulet", "解壓／重新解壓 Amulet",
            "Extract the bundled amulet_map_editor.zip into the managed app-data folder. Idempotent — safe to re-run.",
            "將打包嘅 amulet_map_editor.zip 解壓到管理用嘅 app-data 資料夾。可重複執行，安全。",
            "Extract", "解壓",
            async _ => await AmuletService.EnsureExtracted(),
            keywords: "extract unzip setup install 解壓 安裝 設定"),

        Tweak.Action("amulet.deps",
            "Install Python dependencies", "安裝 Python 相依",
            "Install Amulet's pip dependencies (amulet-core, wxPython, OpenGL…) into the user site. Skipped for a self-contained frozen build.",
            "將 Amulet 嘅 pip 相依（amulet-core、wxPython、OpenGL…）安裝到使用者 site。凍結版會跳過。",
            "Install", "安裝",
            async _ => await AmuletService.EnsureDeps(),
            keywords: "pip dependencies requirements wxpython opengl 相依 安裝"),

        Tweak.Action("amulet.opensaves",
            "Open Minecraft saves folder", "開啟 Minecraft 存檔資料夾",
            "Open %AppData%\\.minecraft\\saves in Explorer so you can pick a world to edit.",
            "喺檔案總管開啟 %AppData%\\.minecraft\\saves，方便揀世界嚟編輯。",
            "Open", "開啟",
            _ => Task.FromResult(OpenFolder(AmuletService.FindSavesFolder(),
                "Opened the saves folder.", "已開啟存檔資料夾。",
                "Couldn't find %AppData%\\.minecraft\\saves.", "搵唔到 %AppData%\\.minecraft\\saves。")),
            keywords: "saves minecraft worlds folder 存檔 世界 資料夾"),

        Tweak.Action("amulet.openappdir",
            "Open Amulet app folder", "開啟 Amulet 應用資料夾",
            "Open WinForge's managed Amulet folder (where the zip is extracted) in Explorer.",
            "喺檔案總管開啟 WinForge 管理嘅 Amulet 資料夾（解壓位置）。",
            "Open", "開啟",
            _ => { try { Directory.CreateDirectory(AmuletService.AppDir); } catch { }
                   return Task.FromResult(OpenFolder(AmuletService.AppDir,
                       "Opened the Amulet folder.", "已開啟 Amulet 資料夾。",
                       "Couldn't open the folder.", "開唔到資料夾。")); },
            keywords: "appdata folder location amulet 資料夾 位置"),
    };

    private static TweakResult OpenFolder(string? path, string okEn, string okZh, string failEn, string failZh)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return TweakResult.Fail(failEn, failZh);
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            return TweakResult.Ok(okEn, okZh);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"{failEn} {ex.Message}", $"{failZh}{ex.Message}");
        }
    }
}
