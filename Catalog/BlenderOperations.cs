using System.Collections.Generic;
using System.Threading;
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
    // ──────────────────────────────────────────────────────────────────────
    //  彩色狀態藥丸幫手 · Coloured-status-pill helper
    //
    //  ColoredStatus 係 TweakDefinition 上嘅 init-only 成員，工廠 (Tweak.Action…)
    //  整完先冇得加；TweakDefinition 又係 sealed class（唔係 record）所以冇 `with`。
    //  所以涉及藥丸嗰兩張卡片直接用物件初始化器砌定義，原封不動咁保留 Id／按鈕／
    //  RunAsync／keywords，淨係 overlay 一個顯示用嘅「已安裝／搵唔到」藥丸。
    //
    //  ColoredStatus is init-only and TweakDefinition is a sealed class (no `with`),
    //  so the two cards that want a pill are built via an object initializer that keeps
    //  the Id / button / RunAsync / keywords byte-for-byte and only overlays a display-only
    //  "installed / not found" pill. The probe is BlenderService.IsInstalled — a cheap,
    //  synchronous filesystem look-up (no shell), so it is safe in the getter.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Blender 安裝偵測藥丸（綠＝搵到，紅＝搵唔到）· Installed/not-found pill from the cheap sync probe.</summary>
    private static (string en, string zh, StatusColor color) InstalledPill()
        => BlenderService.IsInstalled
            ? ("Installed", "已安裝", StatusColor.Good)
            : ("Not found", "搵唔到", StatusColor.Bad);

    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // Launch the Blender GUI. Same Id / button / behaviour as the factory action;
        // an install-state pill is overlaid so the user sees at a glance whether Blender is found.
        new TweakDefinition
        {
            Id = "blender.open-gui",
            Title = new("Open Blender", "開 Blender"),
            Description = new("Launch the Blender GUI (no file).", "啟動 Blender GUI（唔開檔）。"),
            Kind = TweakKind.Action,
            Keywords = new[] { "open", "launch", "gui", "blender", "開", "啟動" },
            ActionLabel = new("Open", "開"),
            RunAsync = _ => Task.FromResult(BlenderService.OpenGui()),
            ColoredStatus = InstalledPill,
        },

        // blender --version. Same Id / button / behaviour; the install-state pill makes the
        // "Blender not found" failure path obvious before the user even clicks.
        new TweakDefinition
        {
            Id = "blender.version",
            Title = new("Check version", "查睇版本"),
            Description = new("Run blender --version and show the result.", "執行 blender --version 並顯示結果。"),
            Kind = TweakKind.Action,
            Keywords = new[] { "version", "版本" },
            ActionLabel = new("Check", "查睇"),
            RunAsync = async (CancellationToken _) =>
            {
                var v = await BlenderService.GetVersion();
                return v.Length > 0
                    ? TweakResult.Ok(v, v)
                    : TweakResult.Fail("Blender not found.", "搵唔到 Blender。");
            },
            ColoredStatus = InstalledPill,
        },

        // Trivial folder launcher — left as a plain Action (no pill); opening a folder has no
        // meaningful install/health state to surface.
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
