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

    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.Action("lo.version", "LibreOffice version", "LibreOffice 版本",
            "Show the installed LibreOffice version string.", "顯示已安裝嘅 LibreOffice 版本。",
            "Check", "查睇",
            async ct =>
            {
                var v = await LibreOfficeService.Version(ct);
                return string.IsNullOrWhiteSpace(v)
                    ? TweakResult.Fail("LibreOffice not found.", "搵唔到 LibreOffice。")
                    : TweakResult.Ok(v, v, v);
            },
            keywords: "version 版本 soffice about"),

        Launch("lo.start", "Open Start Center", "開啟起始中心",
            "Open the LibreOffice Start Center (pick any app).", "開啟 LibreOffice 起始中心（揀任何模組）。",
            "", keywords: "start center 起始 中心 launch 啟動"),
        Launch("lo.writer", "New Writer document", "新建 Writer 文件",
            "Open a blank Writer (word processor) document.", "開一個空白 Writer（文書處理）文件。",
            "--writer", keywords: "writer word document 文書 文件"),
        Launch("lo.calc", "New Calc spreadsheet", "新建 Calc 試算表",
            "Open a blank Calc (spreadsheet) document.", "開一個空白 Calc（試算表）文件。",
            "--calc", keywords: "calc spreadsheet excel 試算表"),
        Launch("lo.impress", "New Impress presentation", "新建 Impress 簡報",
            "Open a blank Impress (presentation) document.", "開一個空白 Impress（簡報）文件。",
            "--impress", keywords: "impress presentation powerpoint 簡報"),
        Launch("lo.draw", "New Draw drawing", "新建 Draw 繪圖",
            "Open a blank Draw (vector graphics) document.", "開一個空白 Draw（向量繪圖）文件。",
            "--draw", keywords: "draw drawing vector 繪圖"),
        Launch("lo.math", "New Math formula", "新建 Math 公式",
            "Open a blank Math (formula editor) document.", "開一個空白 Math（公式編輯）文件。",
            "--math", keywords: "math formula equation 公式"),
        Launch("lo.base", "New Base database", "新建 Base 資料庫",
            "Open Base (database front-end).", "開啟 Base（資料庫前端）。",
            "--base", keywords: "base database 資料庫"),

        Tweak.Action("lo.kill", "Kill stray soffice", "結束殘留 soffice",
            "Force-quit any stuck soffice / soffice.bin processes (use if a conversion hangs).",
            "強制結束卡住嘅 soffice / soffice.bin 程序（轉檔卡住時用）。",
            "Kill", "結束",
            _ => Task.FromResult(LibreOfficeService.KillStray()),
            destructive: true, keywords: "kill stray hang stuck 結束 卡住 殘留"),
    };
}
