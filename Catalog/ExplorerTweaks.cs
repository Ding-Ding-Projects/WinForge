using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 檔案總管調校 · File Explorer tweaks.
/// ADV = HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced.
/// </summary>
public static class ExplorerTweaks
{
    private const string ADV = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string EXPLORER = @"Software\Microsoft\Windows\CurrentVersion\Explorer";
    private const string CABINET = @"Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState";

    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.RegToggle("explorer.show-ext", "Show file extensions", "顯示副檔名",
            "Always show file name extensions in File Explorer.", "喺檔案總管永遠顯示副檔名。",
            RegRoot.HKCU, ADV, "HideFileExt",
            onValue: 0, offValue: 1, restart: RestartScope.Explorer, keywords: "extension,副檔名,filetype"),

        Tweak.RegToggle("explorer.show-hidden", "Show hidden files", "顯示隱藏檔案",
            "Display files and folders marked as hidden.", "顯示俾人標記咗做隱藏嘅檔案同資料夾。",
            RegRoot.HKCU, ADV, "Hidden",
            onValue: 1, offValue: 2, restart: RestartScope.Explorer, keywords: "hidden,隱藏"),

        Tweak.RegToggle("explorer.show-super-hidden", "Show protected OS files", "顯示受保護系統檔案",
            "Reveal protected operating-system files (advanced).", "顯示受保護嘅作業系統檔案（進階）。",
            RegRoot.HKCU, ADV, "ShowSuperHidden",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "system,系統,superhidden"),

        Tweak.RegToggle("explorer.full-path", "Full path in title bar", "標題列顯示完整路徑",
            "Show the complete folder path in the address title bar.", "喺標題列顯示資料夾嘅完整路徑。",
            RegRoot.HKCU, CABINET, "FullPath",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "path,路徑,title"),

        // 由 Choice（下拉）升級為 RadioGroup（單選按鈕）＋彩色狀態藥丸 · Upgraded from a Choice (ComboBox)
        // to a RadioGroup so the two mutually-exclusive landing pages are visible at a glance, plus a
        // coloured status pill. Registry behaviour is identical: HKCU ADV "LaunchTo" DWord, This PC=1 / Home=2.
        LaunchToRadio(),

        Tweak.RegToggle("explorer.show-recent", "Show recent files in Quick Access", "快速存取顯示最近檔案",
            "List recently used files under Quick Access / Home.", "喺快速存取／首頁列出最近用過嘅檔案。",
            RegRoot.HKCU, EXPLORER, "ShowRecent",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "recent,最近,quickaccess"),

        Tweak.RegToggle("explorer.show-frequent", "Show frequent folders", "顯示常用資料夾",
            "List frequently used folders under Quick Access / Home.", "喺快速存取／首頁列出常用嘅資料夾。",
            RegRoot.HKCU, EXPLORER, "ShowFrequent",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "frequent,常用,quickaccess"),

        Tweak.RegToggle("explorer.nav-expand", "Expand to current folder", "導覽窗格展開至目前資料夾",
            "Auto-expand the navigation pane to the open folder.", "導覽窗格自動展開到而家開緊嘅資料夾。",
            RegRoot.HKCU, ADV, "NavPaneExpandToCurrentFolder",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "navpane,導覽,expand"),

        Tweak.RegToggle("explorer.checkboxes", "Item check boxes", "項目核取方塊",
            "Show selection check boxes on files and folders.", "喺檔案同資料夾上面顯示選取核取方塊。",
            RegRoot.HKCU, ADV, "AutoCheckSelect",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "checkbox,核取,select"),

        Tweak.RegToggle("explorer.confirm-delete", "Confirm delete dialog", "刪除確認對話框",
            "Ask for confirmation before sending files to the Recycle Bin.", "刪檔案入資源回收筒之前先問你確認。",
            RegRoot.HKCU, ADV, "ConfirmFileDelete",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "delete,刪除,confirm,recycle"),

        Tweak.RegToggle("explorer.separate-process", "Launch folder windows in a separate process", "資料夾視窗用獨立程序開啟",
            "Open each File Explorer window in its own process for stability.", "每個檔案總管視窗用獨立程序開，會穩定啲。",
            RegRoot.HKCU, ADV, "SeparateProcess",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "separate,process,獨立,程序,穩定"),

        Tweak.CustomToggle("explorer.classic-menu", "Classic right-click menu", "經典右鍵選單",
            "Restore the full Windows 10 style context menu.", "還原返 Windows 10 式嘅完整右鍵選單。",
            getIsOn: () => RegistryHelper.KeyExists(RegRoot.HKCU,
                @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32"),
            setIsOn: on =>
            {
                var p = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
                if (on) RegistryHelper.SetDefault(RegRoot.HKCU, p + @"\InprocServer32", "");
                else RegistryHelper.DeleteSubKeyTree(RegRoot.HKCU, p);
            },
            restart: RestartScope.Explorer, keywords: "context,右鍵,classic,menu"),

        Tweak.Cmd("explorer.restart", "Restart File Explorer", "重新啟動檔案總管",
            "Restart the Explorer shell to apply changes.", "重新啟動 Explorer 外殼嚟套用啲變更。",
            "Restart", "重啟", "taskkill /f /im explorer.exe & start explorer.exe",
            restart: RestartScope.Explorer, keywords: "restart,重啟,explorer"),

        Tweak.Cmd("explorer.folder-options", "Open Folder Options", "開啟資料夾選項",
            "Open the classic Folder Options dialog.", "開啟經典嘅資料夾選項對話框。",
            "Open", "開啟", "rundll32.exe shell32.dll,Options_RunDLL 0",
            keywords: "folder,options,資料夾,選項"),
    };

    /// <summary>
    /// 「檔案總管開啟到」單選按鈕 · The "Open File Explorer to" RadioGroup.
    /// 直接讀寫 HKCU\…\Advanced\LaunchTo（DWord），1 = 本機 / This PC，2 = 首頁 / Home，
    /// 與原本嘅 RegChoice 完全一致，只係把下拉換成單選按鈕並加上彩色狀態藥丸。
    /// Reads/writes the same DWord exactly as the original Choice did; only the presentation changes.
    /// </summary>
    private static TweakDefinition LaunchToRadio()
    {
        const string ThisPc = "1";
        const string Home = "2";

        return new TweakDefinition
        {
            Id = "explorer.launch-to",
            Title = new("Open File Explorer to", "檔案總管開啟到"),
            Description = new("Choose what File Explorer opens to by default.", "揀檔案總管預設開啟邊度。"),
            Kind = TweakKind.RadioGroup,
            Restart = RestartScope.Explorer,
            Keywords = new[] { "launchto", "首頁", "thispc" },
            Choices = new List<TweakChoice>
            {
                new(new LocalizedText("This PC", "本機"), ThisPc),
                new(new LocalizedText("Home", "首頁"), Home),
            },
            GetCurrentChoice = () =>
            {
                if (RegistryHelper.ValueEquals(RegRoot.HKCU, ADV, "LaunchTo", 1)) return ThisPc;
                if (RegistryHelper.ValueEquals(RegRoot.HKCU, ADV, "LaunchTo", 2)) return Home;
                return null;
            },
            SetChoice = val =>
            {
                int v = string.Equals(val, Home, StringComparison.Ordinal) ? 2 : 1;
                RegistryHelper.SetValue(RegRoot.HKCU, ADV, "LaunchTo", v, RegistryValueKind.DWord);
            },
            ColoredStatus = () =>
                RegistryHelper.ValueEquals(RegRoot.HKCU, ADV, "LaunchTo", 2)
                    ? ("Home", "首頁", StatusColor.Neutral)
                    : ("This PC", "本機", StatusColor.Good),
        };
    }
}
