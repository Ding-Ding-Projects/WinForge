using System.Collections.Generic;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// LibreOffice 操作目錄 · TweakCard-style operations for the LibreOffice module: launch the
/// individual apps (Writer / Calc / Impress / Draw / Math / Base / Start Center), probe the
/// version, and kill stray soffice processes if a conversion hangs.
/// </summary>
public static class LibreOfficeOperations
{
    private static TweakDefinition Launch(string id, string enT, string zhT, string enD, string zhD,
        string switchArg, string? keywords = null)
        => Tweak.Action(id, enT, zhT, enD, zhD, "Launch", "啟動",
            _ => Task.FromResult(LibreOfficeService.LaunchApp(switchArg)), keywords: keywords);

    // ======================================================================
    //  顯示輔助（只改顯示同接線，行為完全不變）
    //  Presentation helpers (PRESENTATION + WIRING ONLY — behaviour unchanged)
    //
    //  ColoredStatus / ShowProgressBar 係 TweakDefinition 上嘅 init-only 成員，工廠
    //  (Tweak.Action…) 整完先冇得加；TweakDefinition 又係 sealed class（唔係 record）所以
    //  冇 `with`。所以呢度用幫手：攞工廠整好嘅定義，原封不動咁複製返佢嘅
    //  Id／行為／權限／破壞性／重啟範圍／按鈕／RunAsync，淨係 overlay 一個顯示用嘅藥丸
    //  （同可選嘅進度條）。每張卡嘅 Id 同行為都保持一模一樣。
    //
    //  ColoredStatus and ShowProgressBar are init-only and TweakDefinition is a sealed class
    //  (no `with`), so we copy the factory-built definition's Id / behaviour / admin / destructive
    //  / restart / button / RunAsync verbatim and only overlay a presentation-only "installed /
    //  not found" pill (and an optional progress bar). Every card's Id and behaviour are untouched.
    // ======================================================================

    /// <summary>
    /// 為一個動作定義加上「已安裝／搵唔到」彩色狀態藥丸（可選進度條）· Overlay an
    /// "installed / not found" status pill on an action, optionally with a progress bar.
    /// 藥丸由平價同步探測 <see cref="LibreOfficeService.IsInstalled"/> 驅動（純讀登錄檔／檢查檔案存在，
    /// 唔行任何 shell），所以喺 getter 入面叫都唔會卡 UI。
    /// The pill is driven by the cheap synchronous <see cref="LibreOfficeService.IsInstalled"/> probe
    /// (registry reads + File.Exists only, no shell), so it is safe to call from the card getter.
    /// 只係複製工廠定義並加藥丸，唔改 Id／行為 · Copies the factory definition and adds the pill only.
    /// </summary>
    private static TweakDefinition WithStatusPill(TweakDefinition t, bool showProgress = false)
        => new()
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            Kind = t.Kind,
            RequiresAdmin = t.RequiresAdmin,
            Destructive = t.Destructive,
            Restart = t.Restart,
            Keywords = t.Keywords,
            ActionLabel = t.ActionLabel,
            RunAsync = t.RunAsync,
            ShowProgressBar = showProgress,
            ColoredStatus = () => LibreOfficeService.IsInstalled
                ? ("LibreOffice installed", "已安裝 LibreOffice", StatusColor.Good)
                : ("LibreOffice not found", "搵唔到 LibreOffice", StatusColor.Bad),
        };

    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // 版本探測會啟動 soffice --version（首次會建立暫時 profile，可能慢幾秒），所以加進度條。
        // The version probe launches soffice --version (first run builds a throwaway profile and can
        // take a few seconds), so it gets a progress bar in addition to the installed/not-found pill.
        WithStatusPill(Tweak.Action("lo.version", "LibreOffice version", "LibreOffice 版本",
            "Show the installed LibreOffice version string.", "顯示已安裝嘅 LibreOffice 版本。",
            "Check", "查睇",
            async ct =>
            {
                var v = await LibreOfficeService.Version(ct);
                return string.IsNullOrWhiteSpace(v)
                    ? TweakResult.Fail("LibreOffice not found.", "搵唔到 LibreOffice。")
                    : TweakResult.Ok(v, v, v);
            },
            keywords: "version 版本 soffice about"), showProgress: true),

        WithStatusPill(Launch("lo.start", "Open Start Center", "開啟起始中心",
            "Open the LibreOffice Start Center (pick any app).", "開啟 LibreOffice 起始中心（揀任何模組）。",
            "", keywords: "start center 起始 中心 launch 啟動")),
        WithStatusPill(Launch("lo.writer", "New Writer document", "新建 Writer 文件",
            "Open a blank Writer (word processor) document.", "開一個空白 Writer（文書處理）文件。",
            "--writer", keywords: "writer word document 文書 文件")),
        WithStatusPill(Launch("lo.calc", "New Calc spreadsheet", "新建 Calc 試算表",
            "Open a blank Calc (spreadsheet) document.", "開一個空白 Calc（試算表）文件。",
            "--calc", keywords: "calc spreadsheet excel 試算表")),
        WithStatusPill(Launch("lo.impress", "New Impress presentation", "新建 Impress 簡報",
            "Open a blank Impress (presentation) document.", "開一個空白 Impress（簡報）文件。",
            "--impress", keywords: "impress presentation powerpoint 簡報")),
        WithStatusPill(Launch("lo.draw", "New Draw drawing", "新建 Draw 繪圖",
            "Open a blank Draw (vector graphics) document.", "開一個空白 Draw（向量繪圖）文件。",
            "--draw", keywords: "draw drawing vector 繪圖")),
        WithStatusPill(Launch("lo.math", "New Math formula", "新建 Math 公式",
            "Open a blank Math (formula editor) document.", "開一個空白 Math（公式編輯）文件。",
            "--math", keywords: "math formula equation 公式")),
        WithStatusPill(Launch("lo.base", "New Base database", "新建 Base 資料庫",
            "Open Base (database front-end).", "開啟 Base（資料庫前端）。",
            "--base", keywords: "base database 資料庫")),

        WithStatusPill(Tweak.Action("lo.kill", "Kill stray soffice", "結束殘留 soffice",
            "Force-quit any stuck soffice / soffice.bin processes (use if a conversion hangs).",
            "強制結束卡住嘅 soffice / soffice.bin 程序（轉檔卡住時用）。",
            "Kill", "結束",
            _ => Task.FromResult(LibreOfficeService.KillStray()),
            destructive: true, keywords: "kill stray hang stuck 結束 卡住 殘留")),
    };
}
