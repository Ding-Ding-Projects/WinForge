using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>一個右鍵選單項目 · One custom right-click verb.</summary>
public sealed class MenuVerb
{
    public int Scope { get; init; }
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string Command { get; init; } = "";
    public string Icon { get; init; } = "";
    public bool Extended { get; init; }
}

/// <summary>
/// 應用程式內右鍵選單編輯（純 HKCU，安全）· In-app context-menu editor. Adds/lists/removes custom verbs
/// under HKCU\Software\Classes\&lt;scope&gt;\shell only (per-user; never touches system HKLM/HKCR defaults).
/// </summary>
public static class ContextMenuService
{
    // Scope index → (EN label, ZH label, registry sub-path under HKCU\Software\Classes, command placeholder)
    private static readonly (string en, string zh, string path, string ph)[] Scopes =
    {
        ("All files", "所有檔案", @"Software\Classes\*\shell", "%1"),
        ("Folders", "資料夾", @"Software\Classes\Directory\shell", "%1"),
        ("Folder background", "資料夾空白處", @"Software\Classes\Directory\Background\shell", "%V"),
        ("Drives", "磁碟機", @"Software\Classes\Drive\shell", "%1"),
    };

    public static int ScopeCount => Scopes.Length;
    public static string ScopeLabel(int i) => Loc.I.Pick(Scopes[i].en, Scopes[i].zh);
    public static string ScopePlaceholder(int i) => Scopes[i].ph;
    private static string ScopePath(int i) => Scopes[i].path;

    /// <summary>List all user-defined verbs across every scope.</summary>
    public static List<MenuVerb> List()
    {
        var verbs = new List<MenuVerb>();
        for (int s = 0; s < Scopes.Length; s++)
        {
            string root = ScopePath(s);
            foreach (var key in RegistryHelper.GetSubKeyNames(RegRoot.HKCU, root))
            {
                string vp = $@"{root}\{key}";
                string label = AsString(RegistryHelper.GetValue(RegRoot.HKCU, vp, ""));
                if (string.IsNullOrEmpty(label)) label = AsString(RegistryHelper.GetValue(RegRoot.HKCU, vp, "MUIVerb"));
                if (string.IsNullOrEmpty(label)) label = key;
                string cmd = AsString(RegistryHelper.GetValue(RegRoot.HKCU, $@"{vp}\command", ""));
                string icon = AsString(RegistryHelper.GetValue(RegRoot.HKCU, vp, "Icon"));
                bool ext = RegistryHelper.GetValue(RegRoot.HKCU, vp, "Extended") is not null;
                verbs.Add(new MenuVerb { Scope = s, Key = key, Label = label, Command = cmd, Icon = icon, Extended = ext });
            }
        }
        return verbs;
    }

    /// <summary>Create (or overwrite) a custom verb. Returns the key name used.</summary>
    public static string Add(int scope, string label, string command, string? icon, bool extended)
    {
        scope = Math.Clamp(scope, 0, Scopes.Length - 1);
        string key = SanitizeKey(label);
        string vp = $@"{ScopePath(scope)}\{key}";

        RegistryHelper.SetDefault(RegRoot.HKCU, vp, label);
        if (!string.IsNullOrWhiteSpace(icon))
            RegistryHelper.SetValue(RegRoot.HKCU, vp, "Icon", icon!, RegistryValueKind.String);
        if (extended)
            RegistryHelper.SetValue(RegRoot.HKCU, vp, "Extended", "", RegistryValueKind.String);
        RegistryHelper.SetDefault(RegRoot.HKCU, $@"{vp}\command", command);
        return key;
    }

    public static void Remove(int scope, string key)
    {
        scope = Math.Clamp(scope, 0, Scopes.Length - 1);
        RegistryHelper.DeleteSubKeyTree(RegRoot.HKCU, $@"{ScopePath(scope)}\{key}");
    }

    private static string AsString(object? o) => o?.ToString() ?? "";

    private static string SanitizeKey(string label)
    {
        var chars = new List<char>();
        foreach (char c in label)
            if (char.IsLetterOrDigit(c) || c is '_' or '-' or ' ') chars.Add(c);
        string k = new string(chars.ToArray()).Trim().Replace(' ', '_');
        if (string.IsNullOrEmpty(k)) k = "WinForgeVerb";
        return "WT_" + k;
    }
}
