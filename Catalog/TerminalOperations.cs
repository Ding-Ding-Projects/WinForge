using System.Collections.Generic;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// Windows Terminal 快捷啟動動作 · Quick-launch wt.exe actions rendered as TweakCards inside the
/// Windows Terminal module. 全部雙語。 All bilingual.
/// </summary>
public static class TerminalOperations
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.Shell("term.launch", "Open Windows Terminal", "開啟 Windows 終端機",
            "Launch a new Windows Terminal window with its default profile.",
            "用預設 profile 開一個新嘅 Windows 終端機視窗。",
            "Open", "開啟", "wt.exe", "",
            keywords: "wt terminal open launch 終端機 開啟"),

        Tweak.Shell("term.new-tab", "New tab", "開新分頁",
            "Open a new tab in the running Windows Terminal (or start it).",
            "喺執行中嘅 Windows 終端機度開新分頁（冇就開啟）。",
            "New tab", "新分頁", "wt.exe", "nt",
            keywords: "wt new tab nt 分頁"),

        Tweak.Shell("term.split-pane", "Split pane", "分割窗格",
            "Open Windows Terminal and split the current tab into a new pane.",
            "開 Windows 終端機並將目前分頁分割成新窗格。",
            "Split", "分割", "wt.exe", "sp",
            keywords: "wt split pane sp 窗格"),

        Tweak.Shell("term.here", "Open Terminal here (Home)", "喺主目錄開終端機",
            "Open Windows Terminal with the working directory set to your user profile folder.",
            "用你個人資料夾做工作目錄開 Windows 終端機。",
            "Open", "開啟", "wt.exe", "-d %USERPROFILE%",
            keywords: "wt directory home 目錄 主目錄"),

        Tweak.Shell("term.pwsh", "Launch PowerShell tab", "開 PowerShell 分頁",
            "Open a Windows Terminal tab running Windows PowerShell.",
            "開一個執行 Windows PowerShell 嘅 Windows 終端機分頁。",
            "Open", "開啟", "wt.exe", "nt -p \"Windows PowerShell\"",
            keywords: "wt powershell pwsh 終端機"),

        Tweak.Shell("term.cmd", "Launch Command Prompt tab", "開命令提示字元分頁",
            "Open a Windows Terminal tab running the Command Prompt.",
            "開一個執行命令提示字元嘅 Windows 終端機分頁。",
            "Open", "開啟", "wt.exe", "nt -p \"Command Prompt\"",
            keywords: "wt cmd command prompt 命令提示字元"),

        Tweak.Shell("term.settings", "Open WT settings UI", "開啟 WT 設定介面",
            "Open Windows Terminal's own settings UI (Ctrl+,).",
            "開 Windows 終端機自己嘅設定介面（Ctrl+,）。",
            "Open", "開啟", "wt.exe", "-w 0 nt -p \"Windows PowerShell\" cmd /c start ms-settings:",
            keywords: "wt settings ui 設定"),
    };
}
