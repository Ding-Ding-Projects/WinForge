using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>
/// 一個 WinForge 右鍵選單動作嘅定義 · One curated WinForge shell-verb action.
/// </summary>
public sealed class ShellAction
{
    /// <summary>穩定識別碼（設定鍵 + 登錄子鍵名）· Stable id (used as the settings key and the registry sub-key suffix).</summary>
    public required string Id { get; init; }
    public required string En { get; init; }
    public required string Zh { get; init; }
    /// <summary>路由去邊個模組嘅 --page 別名 · The --page alias this verb routes to.</summary>
    public required string PageAlias { get; init; }
    /// <summary>登錄範圍 · The HKCU\Software\Classes scope this verb lives under.</summary>
    public required ShellScope Scope { get; init; }
    /// <summary>Segoe Fluent 圖示字元（用嚟做 app 內顯示）· Glyph for in-app display.</summary>
    public string Glyph { get; init; } = "";
    /// <summary>選用：DLL 圖示資源（shell32 等）· Optional icon resource for the menu entry.</summary>
    public string Icon { get; init; } = "";
}

/// <summary>右鍵選單範圍 · The shell scope a verb applies to.</summary>
public enum ShellScope
{
    AllFiles,            // *  — every file
    Directory,           // Directory — folders
    DirectoryBackground, // Directory\Background — empty space inside a folder
}

/// <summary>
/// WinForge 原生檔案總管右鍵選單整合 · Native Explorer right-click integration for WinForge.
///
/// 喺 HKCU\Software\Classes 登記「經典」shell verb（純使用者、免管理員、即時生效）。
/// Registers classic shell verbs under HKCU\Software\Classes (per-user, no elevation, applied live).
/// 每個 verb 嘅指令都係叫返 WinForge 自己嘅 exe，帶 deep-link 參數
///   "&lt;WinForgeExe&gt;" --page &lt;alias&gt; --path "%1"
/// 路由去相關模組並帶住右鍵揀中嘅檔案／資料夾。
/// Each verb's command launches WinForge's OWN exe with a deep-link that routes to the right module
/// and carries the right-clicked path. Win11 shows these under "Show more options"; Win10 shows them
/// directly. A true top-level Win11 entry would need an IExplorerCommand sparse-package handler.
/// </summary>
public static class ShellContextMenuService
{
    // 我哋自己嘅 verb 一律用呢個前綴，避免撞到 ContextMenuService（"WT_"）或者其他人嘅 verb。
    // All WinForge verbs use this prefix so we never collide with the in-app editor ("WT_") or third parties.
    private const string KeyPrefix = "WinForge.";

    // 分組 flyout 嘅母項鍵名（每個 scope 一個）· Parent submenu key (one per scope) used to group our verbs.
    private const string GroupKey = "WinForge.Menu";

    /// <summary>curated 動作清單 · The curated set of WinForge shell actions.</summary>
    public static readonly IReadOnlyList<ShellAction> Actions = new List<ShellAction>
    {
        // -------- On any file (*) --------
        new() { Id = "hash",       En = "Hash with WinForge",        Zh = "用 WinForge 計雜湊值",   PageAlias = "duplicates", Scope = ShellScope.AllFiles, Glyph = "", Icon = "imageres.dll,-5301" },
        new() { Id = "ocr",        En = "OCR image text",            Zh = "圖片文字辨識（OCR）",    PageAlias = "ocr",       Scope = ShellScope.AllFiles, Glyph = "", Icon = "imageres.dll,-1019" },
        new() { Id = "resize",     En = "Resize image",              Zh = "縮放圖片",               PageAlias = "imageresizer", Scope = ShellScope.AllFiles, Glyph = "", Icon = "imageres.dll,-1019" },
        new() { Id = "locksmith",  En = "What's locking this?",      Zh = "邊個程序鎖住佢？",       PageAlias = "filelocksmith", Scope = ShellScope.AllFiles, Glyph = "", Icon = "imageres.dll,-100" },
        new() { Id = "copypath",   En = "Copy as path",              Zh = "複製路徑",               PageAlias = "copypath",  Scope = ShellScope.AllFiles, Glyph = "", Icon = "imageres.dll,-5302" },

        // -------- On folders (Directory) --------
        new() { Id = "openfolder", En = "Open folder in WinForge",   Zh = "喺 WinForge 開資料夾",   PageAlias = "disk",      Scope = ShellScope.Directory, Glyph = "", Icon = "imageres.dll,-3" },
        new() { Id = "diskusage",  En = "Analyse disk usage",        Zh = "分析磁碟用量",           PageAlias = "disk",      Scope = ShellScope.Directory, Glyph = "", Icon = "imageres.dll,-30" },
        new() { Id = "lockfolder", En = "What's locking this folder?",Zh = "邊個程序鎖住此資料夾？", PageAlias = "filelocksmith", Scope = ShellScope.Directory, Glyph = "", Icon = "imageres.dll,-100" },

        // -------- On folder background (Directory\Background) --------
        new() { Id = "openhere",   En = "Open WinForge here",        Zh = "喺呢度開 WinForge",      PageAlias = "disk",      Scope = ShellScope.DirectoryBackground, Glyph = ", ".TrimEnd(',', ' '), Icon = "imageres.dll,-3" },
    };

    /// <summary>scope → HKCU\Software\Classes 子路徑 · scope → registry sub-path.</summary>
    private static string ScopePath(ShellScope scope) => scope switch
    {
        ShellScope.AllFiles => @"Software\Classes\*\shell",
        ShellScope.Directory => @"Software\Classes\Directory\shell",
        ShellScope.DirectoryBackground => @"Software\Classes\Directory\Background\shell",
        _ => @"Software\Classes\*\shell",
    };

    /// <summary>scope → 命令裡面代表揀中項目嘅佔位符 · scope → the placeholder for the selected item.</summary>
    private static string ScopePlaceholder(ShellScope scope)
        => scope == ShellScope.DirectoryBackground ? "%V" : "%1";

    public static ShellAction? Find(string id) => Actions.FirstOrDefault(a => a.Id == id);

    /// <summary>WinForge 自己嘅 exe 完整路徑 · Full path to WinForge's own executable.</summary>
    public static string ExePath
    {
        get
        {
            var p = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
            // Fallbacks: main module, then argv[0].
            try { var mm = Process.GetCurrentProcess().MainModule?.FileName; if (!string.IsNullOrEmpty(mm)) return mm!; } catch { }
            return Environment.GetCommandLineArgs()[0];
        }
    }

    /// <summary>砌一條 verb 嘅完整命令字串 · Build the full command string for a verb.</summary>
    private static string BuildCommand(ShellAction a)
    {
        string ph = ScopePlaceholder(a.Scope);
        // "<exe>" --page <alias> --path "<%1 | %V>"
        return $"\"{ExePath}\" --page {a.PageAlias} --path \"{ph}\"";
    }

    /// <summary>一個動作嘅 verb 喺登錄入面嘅完整路徑 · The full registry path of an action's verb key.</summary>
    private static string VerbPath(ShellAction a) => $@"{ScopePath(a.Scope)}\{KeyPrefix}{a.Id}";

    /// <summary>呢個動作而家係咪已登記？· Is this action currently registered?</summary>
    public static bool IsRegistered(ShellAction a)
    {
        // Either it lives directly under shell, or it's a sub-command under the grouped flyout.
        return RegistryHelper.KeyExists(RegRoot.HKCU, $@"{VerbPath(a)}\command")
            || RegistryHelper.KeyExists(RegRoot.HKCU, $@"{GroupCommandStorePath(a.Scope)}\{KeyPrefix}{a.Id}\command");
    }

    /// <summary>登記單一動作 · Register one action (idempotent).</summary>
    public static void Register(ShellAction a)
    {
        string vp = VerbPath(a);
        RegistryHelper.SetValue(RegRoot.HKCU, vp, "MUIVerb", DisplayLabel(a), RegistryValueKind.String);
        RegistryHelper.SetDefault(RegRoot.HKCU, vp, DisplayLabel(a));
        if (!string.IsNullOrWhiteSpace(a.Icon))
            RegistryHelper.SetValue(RegRoot.HKCU, vp, "Icon", a.Icon, RegistryValueKind.String);
        RegistryHelper.SetDefault(RegRoot.HKCU, $@"{vp}\command", BuildCommand(a));
    }

    /// <summary>移除單一動作 · Unregister one action.</summary>
    public static void Unregister(ShellAction a)
    {
        RegistryHelper.DeleteSubKeyTree(RegRoot.HKCU, VerbPath(a));
        // Also drop any grouped variant.
        RegistryHelper.DeleteSubKeyTree(RegRoot.HKCU, $@"{GroupCommandStorePath(a.Scope)}\{KeyPrefix}{a.Id}");
    }

    public static void SetRegistered(ShellAction a, bool on)
    {
        if (on) Register(a); else Unregister(a);
    }

    /// <summary>全部登記 · Register every curated action.</summary>
    public static void RegisterAll()
    {
        foreach (var a in Actions) Register(a);
    }

    /// <summary>全部移除（連分組母項）· Unregister everything, including the grouped parents.</summary>
    public static void UnregisterAll()
    {
        foreach (var a in Actions) Unregister(a);
        foreach (ShellScope s in Enum.GetValues(typeof(ShellScope)))
        {
            RegistryHelper.DeleteSubKeyTree(RegRoot.HKCU, $@"{ScopePath(s)}\{GroupKey}");
            RegistryHelper.DeleteSubKeyTree(RegRoot.HKCU, GroupCommandStorePath(s));
        }
    }

    /// <summary>登記咗幾多個動作 · How many curated actions are currently registered.</summary>
    public static int RegisteredCount() => Actions.Count(IsRegistered);

    private static string DisplayLabel(ShellAction a) => $"{a.En} · {a.Zh}";

    // ---------------------------------------------------------------------
    //  Grouped "WinForge" flyout (a plus, not required) · 分組「WinForge」flyout
    //  Uses SubCommands via a per-scope CommandStore so the verbs nest under
    //  one "WinForge" parent. If anything looks off the flat verbs above are
    //  the supported path.
    // ---------------------------------------------------------------------

    private static string GroupCommandStorePath(ShellScope scope)
        => $@"Software\Classes\WinForge.CommandStore\{scope}\shell";

    /// <summary>
    /// 用一個「WinForge」母項把某 scope 嘅所有已選動作收埋做 flyout。
    /// Group all enabled actions for a scope under one "WinForge" parent flyout.
    /// 會先清走該 scope 嘅平面 verb，避免重複。Clears the flat verbs for that scope first to avoid duplicates.
    /// </summary>
    public static void RegisterGrouped(ShellScope scope)
    {
        var inScope = Actions.Where(a => a.Scope == scope).ToList();

        // Remove flat verbs for this scope so they don't show twice.
        foreach (var a in inScope) RegistryHelper.DeleteSubKeyTree(RegRoot.HKCU, VerbPath(a));

        string parent = $@"{ScopePath(scope)}\{GroupKey}";
        RegistryHelper.SetValue(RegRoot.HKCU, parent, "MUIVerb", "WinForge", RegistryValueKind.String);
        RegistryHelper.SetValue(RegRoot.HKCU, parent, "Icon", "imageres.dll,-3", RegistryValueKind.String);
        // Pull the children from our command store, ordered, semicolon-separated.
        string subList = string.Join(";", inScope.Select(a => $"{KeyPrefix}{a.Id}"));
        RegistryHelper.SetValue(RegRoot.HKCU, parent, "SubCommands", subList, RegistryValueKind.String);

        // Define each child in the command store.
        foreach (var a in inScope)
        {
            string cp = $@"{GroupCommandStorePath(scope)}\{KeyPrefix}{a.Id}";
            RegistryHelper.SetValue(RegRoot.HKCU, cp, "MUIVerb", DisplayLabel(a), RegistryValueKind.String);
            if (!string.IsNullOrWhiteSpace(a.Icon))
                RegistryHelper.SetValue(RegRoot.HKCU, cp, "Icon", a.Icon, RegistryValueKind.String);
            RegistryHelper.SetDefault(RegRoot.HKCU, $@"{cp}\command", BuildCommand(a));
        }
    }
}
