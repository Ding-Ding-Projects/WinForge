using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// Packer 操作目錄 · Catalog of plugin / housekeeping operations for the Packer module, rendered as
/// TweakCards. 呢度收嘅係短跑、擷取式嘅指令（plugins、version、help、fmt -check）；
/// 主要嘅 init / validate / build 由 PackerModule 嘅串流主控台處理。
/// These are short, capture-style commands (plugins, version, help, fmt -check); the long-running
/// init / validate / build live in the module's streaming console. All run in the picked working dir.
/// </summary>
public static class PackerOperations
{
    private static TweakDefinition InDir(string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string args, string? keywords = null)
        => Tweak.Action(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            ct => PackerService.RunRaw(args, PackerService.WorkingDir, ct), keywords: keywords);

    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // ===== basics · 基本 =====
        InDir("pk.version", "Packer version", "Packer 版本",
            "Show the installed Packer version.", "顯示已安裝嘅 Packer 版本。",
            "Check", "查睇", "version", "version 版本"),
        InDir("pk.help", "Packer help", "Packer 說明",
            "Show the top-level Packer help.", "顯示 Packer 頂層說明。",
            "Show", "顯示", "--help", "help 說明"),

        // ===== formatting · 格式化 =====
        InDir("pk.fmt-check", "Check formatting", "檢查格式",
            "Check (without changing) whether templates in the working folder are canonically formatted.",
            "檢查（唔會改動）工作資料夾入面嘅範本格式是否規範。",
            "Check", "檢查", "fmt -check -diff .", "fmt format check 格式 檢查"),
        InDir("pk.fmt-write", "Format templates", "格式化範本",
            "Rewrite all *.pkr.hcl templates in the working folder to the canonical format.",
            "將工作資料夾內所有 *.pkr.hcl 範本改寫成規範格式。",
            "Format", "格式化", "fmt .", "fmt format write 格式化"),

        // ===== plugins · 插件 =====
        InDir("pk.plugins-installed", "List installed plugins", "列出已安裝插件",
            "List the Packer plugins currently installed for this user.",
            "列出目前為此用戶安裝嘅 Packer 插件。",
            "List", "列出", "plugins installed", "plugin plugins installed 插件 列出"),
        InDir("pk.plugins-required", "Required plugins (from templates)", "範本所需插件",
            "Show which plugins the templates in the working folder require (parsed from required_plugins).",
            "顯示工作資料夾範本所需嘅插件（由 required_plugins 解析）。",
            "Show", "顯示", "plugins required .", "plugin plugins required 插件 所需"),

        // ===== inspect · 檢視 =====
        InDir("pk.inspect", "Inspect working folder", "檢視工作資料夾",
            "Show the variables, builders and provisioners declared in the working folder's templates.",
            "顯示工作資料夾範本入面宣告嘅變數、builder 同 provisioner。",
            "Inspect", "檢視", "inspect .", "inspect variables builders 檢視 變數"),
    };
}
