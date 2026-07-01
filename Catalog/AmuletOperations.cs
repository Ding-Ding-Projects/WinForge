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
/// control rows: extract the bundled zip, install Python deps, open the
/// managed app-data dir, and open the default .minecraft\saves folder. Each action drives
/// <see cref="AmuletService"/> directly and returns a bilingual result. No redirect.
/// </summary>
public static class AmuletOperations
{
    public static IEnumerable<TweakDefinition> All => new List<TweakDefinition>
    {
        // 行為／Id／按鈕完全不變；只加即時彩色狀態藥丸 + 解壓進度條 ·
        // Identical Id/behaviour/button; only adds a live coloured status pill and an extraction progress bar.
        new TweakDefinition
        {
            Id = "amulet.extract",
            Title = new("Extract / re-extract Amulet", "解壓／重新解壓 Amulet"),
            Description = new(
                "Extract the bundled amulet_map_editor.zip into the managed app-data folder. Idempotent — safe to re-run.",
                "將打包嘅 amulet_map_editor.zip 解壓到管理用嘅 app-data 資料夾。可重複執行，安全。"),
            Kind = TweakKind.Action,
            Keywords = Keys("extract unzip setup install 解壓 安裝 設定"),
            ActionLabel = new("Extract", "解壓"),
            RunAsync = async _ => await AmuletService.EnsureExtracted(),
            // 純本地檔案系統探測（FindEntryPoint），冇 shell／WMI · cheap local-disk probe; no shell/WMI.
            ColoredStatus = () => AmuletService.IsExtracted()
                ? ("Extracted", "已解壓", StatusColor.Good)
                : ("Not extracted", "未解壓", StatusColor.Bad),
            // 解壓大型 zip 係真正長時間 I/O · extracting a large zip is genuinely long-running I/O.
            ShowProgressBar = true,
        },

        // 行為／Id／按鈕完全不變；只加即時彩色狀態藥丸 + pip 安裝進度條 ·
        // Identical Id/behaviour/button; only adds a live coloured status pill and a pip-install progress bar.
        new TweakDefinition
        {
            Id = "amulet.deps",
            Title = new("Install Python dependencies", "安裝 Python 相依"),
            Description = new(
                "Install Amulet's pip dependencies (amulet-core, wxPython, OpenGL…) into the user site. Skipped for a self-contained frozen build.",
                "將 Amulet 嘅 pip 相依（amulet-core、wxPython、OpenGL…）安裝到使用者 site。凍結版會跳過。"),
            Kind = TweakKind.Action,
            Keywords = Keys("pip dependencies requirements wxpython opengl 相依 安裝"),
            ActionLabel = new("Install", "安裝"),
            RunAsync = async _ => await AmuletService.EnsureDeps(),
            // 反映 EnsureDeps 嘅前置條件，全部係本地探測（入口／PATH 掃描）· mirrors EnsureDeps' preconditions; all local probes.
            ColoredStatus = () =>
            {
                var entry = AmuletService.FindEntryPoint();
                if (entry is null)
                    return ("Extract first", "請先解壓", StatusColor.Bad);
                if (entry.Mode == AmuletService.LaunchMode.FrozenExe)
                    return ("Self-contained", "自帶相依", StatusColor.Good);
                return AmuletService.HasPython()
                    ? ("Python ready", "Python 就緒", StatusColor.Good)
                    : ("Python not found", "搵唔到 Python", StatusColor.Bad);
            },
            // pip install 串流下載／編譯，係真正長時間操作 · pip downloads/compiles wheels; genuinely long-running.
            ShowProgressBar = true,
        },

        // 行為／Id／按鈕完全不變；只加即時彩色狀態藥丸 ·
        // Identical Id/behaviour/button; only adds a live coloured status pill.
        new TweakDefinition
        {
            Id = "amulet.opensaves",
            Title = new("Open Minecraft saves folder", "開啟 Minecraft 存檔資料夾"),
            Description = new(
                "Open %AppData%\\.minecraft\\saves in Explorer so you can pick a world to edit.",
                "喺檔案總管開啟 %AppData%\\.minecraft\\saves，方便揀世界嚟編輯。"),
            Kind = TweakKind.Action,
            Keywords = Keys("saves minecraft worlds folder 存檔 世界 資料夾"),
            ActionLabel = new("Open", "開啟"),
            RunAsync = _ => Task.FromResult(OpenFolder(AmuletService.FindSavesFolder(),
                "Opened the saves folder.", "已開啟存檔資料夾。",
                "Couldn't find %AppData%\\.minecraft\\saves.", "搵唔到 %AppData%\\.minecraft\\saves。")),
            // 單一 Directory.Exists 探測 · a single Directory.Exists probe.
            ColoredStatus = () => AmuletService.FindSavesFolder() is not null
                ? ("Saves found", "搵到存檔", StatusColor.Good)
                : ("No saves folder", "冇存檔資料夾", StatusColor.Bad),
        },

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

    /// <summary>
    /// 同 <see cref="Tweak"/> 工廠一致嘅關鍵字切分（淨係用 ,／; 分隔）· Keyword split matching the
    /// <see cref="Tweak"/> factory exactly (splits on ',' / ';' only), so converting these cards from the
    /// factory to object initializers preserves their search keywords byte-for-byte.
    /// </summary>
    private static string[] Keys(string? kw) => string.IsNullOrWhiteSpace(kw)
        ? Array.Empty<string>()
        : kw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
