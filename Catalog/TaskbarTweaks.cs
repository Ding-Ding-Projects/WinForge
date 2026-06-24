using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 工作列同開始功能表 · Taskbar &amp; Start tweaks (all real Windows 11 registry paths/commands).
/// </summary>
public static class TaskbarTweaks
{
    private const string ADV = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string SEARCH = @"Software\Microsoft\Windows\CurrentVersion\Search";

    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // 工作列對齊：靠左／置中係互斥選項，用單選按鈕直接攤開 + 彩色狀態藥丸。
        // Alignment is two mutually-exclusive options → RadioButtons, with a status pill.
        // Same HKCU\…\Advanced\TaskbarAl DWord (0=Left, 1=Center) as before; presentation only.
        RegRadio("taskbar.align", "Taskbar alignment", "工作列對齊",
            "Place taskbar icons on the left or centred.", "工作列圖示靠左定置中。",
            RegRoot.HKCU, ADV, "TaskbarAl",
            new (string en, string zh, int value)[] { ("Left", "靠左", 0), ("Center", "置中", 1) },
            cur => cur switch
            {
                "0" => ("Left-aligned", "靠左", StatusColor.Neutral),
                "1" => ("Centred", "置中", StatusColor.Neutral),
                _ => ("Not set", "未設定", StatusColor.Neutral),
            },
            restart: RestartScope.Explorer, keywords: "align,對齊,左,中"),

        // 工作列搜尋：四個互斥顯示模式，用單選按鈕一眼睇晒；隱藏時用警告色提示。
        // Four mutually-exclusive search-box modes → RadioButtons; pill flags the Hidden state.
        // Same HKCU\…\Search\SearchboxTaskbarMode DWord (0..3) as before; presentation only.
        RegRadio("taskbar.search-mode", "Search on taskbar", "工作列搜尋",
            "Choose how the taskbar search appears.", "揀工作列搜尋點樣顯示。",
            RegRoot.HKCU, SEARCH, "SearchboxTaskbarMode",
            new (string en, string zh, int value)[]
            {
                ("Hidden", "隱藏", 0),
                ("Icon only", "只顯示圖示", 1),
                ("Search box", "搜尋框", 2),
                ("Icon and label", "圖示同標籤", 3),
            },
            cur => cur switch
            {
                "0" => ("Hidden", "已隱藏", StatusColor.Warn),
                "1" => ("Icon only", "只顯示圖示", StatusColor.Neutral),
                "2" => ("Search box", "搜尋框", StatusColor.Good),
                "3" => ("Icon and label", "圖示同標籤", StatusColor.Neutral),
                _ => ("Not set", "未設定", StatusColor.Neutral),
            },
            restart: RestartScope.Explorer, keywords: "search,搜尋"),

        Tweak.RegToggle("taskbar.task-view", "Show Task View button", "顯示工作檢視按鈕",
            "Show the Task View button on the taskbar.", "喺工作列顯示工作檢視按鈕。",
            RegRoot.HKCU, ADV, "ShowTaskViewButton",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "task view,工作檢視"),

        Tweak.RegToggle("taskbar.widgets", "Show Widgets button", "顯示小工具按鈕",
            "Show the Widgets (news and interests) button.", "顯示小工具（新聞同興趣）按鈕。",
            RegRoot.HKCU, ADV, "TaskbarDa",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "widgets,小工具"),

        Tweak.RegToggle("taskbar.chat", "Show Chat/Copilot button", "顯示聊天/Copilot 按鈕",
            "Show the Chat (Copilot) button on the taskbar.", "喺工作列顯示聊天（Copilot）按鈕。",
            RegRoot.HKCU, ADV, "TaskbarMn",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "chat,copilot,聊天"),

        // 合併工作列按鈕：三個互斥規則，用單選按鈕睇得清楚啲。
        // Three mutually-exclusive grouping rules → RadioButtons.
        // Same HKCU\…\Advanced\TaskbarGlomLevel DWord (0/1/2) as before; presentation only.
        RegRadio("taskbar.combine", "Combine taskbar buttons", "合併工作列按鈕",
            "Control when taskbar buttons group together.", "控制工作列按鈕幾時合併。",
            RegRoot.HKCU, ADV, "TaskbarGlomLevel",
            new (string en, string zh, int value)[]
            {
                ("Always combine", "永遠合併", 0),
                ("When taskbar is full", "工作列滿先合併", 1),
                ("Never", "永不合併", 2),
            },
            cur => cur switch
            {
                "0" => ("Always", "永遠合併", StatusColor.Neutral),
                "1" => ("When full", "滿先合併", StatusColor.Neutral),
                "2" => ("Never", "永不合併", StatusColor.Good),
                _ => ("Not set", "未設定", StatusColor.Neutral),
            },
            restart: RestartScope.Explorer, keywords: "combine,合併,group"),

        Tweak.RegToggle("taskbar.end-task", "Show \"End Task\" on right-click", "右鍵顯示「結束工作」",
            "Add End Task to the taskbar right-click menu.", "喺工作列右鍵選單加入結束工作。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\TaskbarDeveloperSettings", "TaskbarEndTask",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "end task,結束工作"),

        Tweak.RegToggle("taskbar.start-most-used", "Show most used apps in Start", "開始功能表顯示最常用程式",
            "List your most-used apps in the Start menu.", "喺開始功能表列出你最常用嘅程式。",
            RegRoot.HKCU, ADV, "Start_TrackProgs",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "start,most used,常用"),

        Tweak.RegToggle("taskbar.start-recently-added", "Show recently added apps in Start", "開始功能表顯示最近新增程式",
            "Show newly installed apps in the Start menu.", "喺開始功能表顯示啱啱裝嘅程式。",
            RegRoot.HKCU, ADV, "Start_TrackDocs",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "start,recently added,最近"),

        Tweak.RegToggle("taskbar.start-recommendations", "Show tips and recommendations in Start", "開始功能表顯示提示同建議",
            "Show tips, shortcuts and app recommendations in Start.", "喺開始功能表顯示提示、捷徑同程式建議。",
            RegRoot.HKCU, ADV, "Start_IrisRecommendations",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "recommendations,建議,tips"),

        Tweak.RegToggle("taskbar.show-seconds-clock", "Show seconds in system clock", "系統時鐘顯示秒數",
            "Display seconds on the taskbar clock (uses more power).", "喺工作列時鐘顯示秒數（會用多啲電）。",
            RegRoot.HKCU, ADV, "ShowSecondsInSystemClock",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "seconds,秒,clock,時鐘"),

        Tweak.RegToggle("taskbar.show-all-tray-icons", "Show all system tray icons", "顯示所有系統匣圖示",
            "Show every icon in the notification overflow area.", "喺系統匣顯示所有通知圖示。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer", "EnableAutoTray",
            onValue: 0, offValue: 1, restart: RestartScope.Explorer, keywords: "tray,系統匣,icons"),

        Tweak.RegToggle("taskbar.multi-monitor-all", "Show taskbar on all displays", "所有螢幕顯示工作列",
            "Show the taskbar on every connected monitor.", "喺每個接咗嘅螢幕都顯示工作列。",
            RegRoot.HKCU, ADV, "MMTaskbarEnabled",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer, keywords: "monitor,螢幕,multi,工作列"),

        Tweak.Cmd("taskbar.open-settings", "Open Taskbar settings", "開啟工作列設定",
            "Open the Windows taskbar settings page.", "開啟 Windows 工作列設定頁面。",
            "Open", "開啟", "start ms-settings:taskbar", keywords: "settings,設定"),
    };

    /// <summary>
    /// 由單一登錄檔 DWord 支援嘅單選按鈕組（含彩色狀態藥丸）·
    /// A RadioButtons group backed by one registry DWord, with a coloured status pill.
    ///
    /// 行為同 <see cref="Tweak.RegChoice"/> 完全一致：讀取時揀第一個同現值相等嘅選項，
    /// 寫入時用 DWord 寫返去。只係換咗呈現方式（combo → radio）並加咗狀態藥丸。
    /// Read/write semantics are identical to RegChoice (read = first option whose value matches,
    /// write = SetValue as DWord); only the presentation changes (combo → radio) plus a status pill.
    /// </summary>
    private static TweakDefinition RegRadio(
        string id, string enT, string zhT, string enD, string zhD,
        RegRoot root, string path, string name,
        (string en, string zh, int value)[] options,
        Func<string?, (string en, string zh, StatusColor color)> status,
        RestartScope restart = RestartScope.None, string? keywords = null)
    {
        string? GetCurrent()
        {
            foreach (var o in options)
                if (RegistryHelper.ValueEquals(root, path, name, o.value))
                    return o.value.ToString();
            return null;
        }

        void SetChoice(string val)
        {
            foreach (var o in options)
                if (string.Equals(o.value.ToString(), val, StringComparison.OrdinalIgnoreCase))
                {
                    RegistryHelper.SetValue(root, path, name, o.value, RegistryValueKind.DWord);
                    return;
                }
        }

        var radioOptions = new (string en, string zh, string value)[options.Length];
        for (int i = 0; i < options.Length; i++)
            radioOptions[i] = (options[i].en, options[i].zh, options[i].value.ToString());

        var def = Tweak.RadioGroup(id, enT, zhT, enD, zhD, radioOptions,
            GetCurrent, SetChoice, restart: restart, keywords: keywords);

        return new TweakDefinition
        {
            Id = def.Id,
            Title = def.Title,
            Description = def.Description,
            Kind = def.Kind,
            RequiresAdmin = def.RequiresAdmin,
            Restart = def.Restart,
            Keywords = def.Keywords,
            Choices = def.Choices,
            GetCurrentChoice = def.GetCurrentChoice,
            SetChoice = def.SetChoice,
            ColoredStatus = () => status(GetCurrent()),
        };
    }
}