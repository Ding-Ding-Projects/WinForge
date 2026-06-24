using System.Collections.Generic;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// Blender 快速操作目錄 · Catalog of quick Blender operations rendered as TweakCards on the module page:
/// open Blender's GUI, version check, and open the scripts / config folders. The heavy render-job and
/// Python-script flows live in the page's rich form; these are the one-tap shortcuts. Bilingual.
/// </summary>
public static class BlenderOperations
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.Action("blender.open-gui", "Open Blender", "開 Blender",
            "Launch the Blender GUI (no file).", "啟動 Blender GUI（唔開檔）。",
            "Open", "開", _ => Task.FromResult(BlenderService.OpenGui()),
            keywords: "open launch gui blender 開 啟動"),

        Tweak.Action("blender.version", "Check version", "查睇版本",
            "Run blender --version and show the result.", "執行 blender --version 並顯示結果。",
            "Check", "查睇", async _ =>
            {
                var v = await BlenderService.GetVersion();
                return v.Length > 0
                    ? TweakResult.Ok(v, v)
                    : TweakResult.Fail("Blender not found.", "搵唔到 Blender。");
            },
            keywords: "version 版本"),

        Tweak.Action("blender.open-scripts", "Open scripts folder", "開 script 資料夾",
            "Open the folder where WinForge keeps the starter Python scripts.",
            "開 WinForge 擺起步 Python script 嘅資料夾。",
            "Open", "開", _ =>
            {
                BlenderService.OpenFolder(BlenderService.ScriptsDir);
                return Task.FromResult(TweakResult.Ok("Opened the scripts folder.", "已開 script 資料夾。"));
            },
            keywords: "scripts python folder 資料夾"),
    };
}
