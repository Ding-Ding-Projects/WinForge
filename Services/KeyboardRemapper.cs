using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>一個可重新對應嘅鍵 · One remappable key (name + scancode word).</summary>
public sealed record KeyDef(string En, string Zh, ushort Scancode);

/// <summary>一個對應：source 鍵變成 target（0 = 停用）· One mapping: source key → target (0 = disable).</summary>
public sealed class KeyMap
{
    public ushort Source { get; init; }
    public ushort Target { get; init; }
    public string SourceName { get; set; } = "";
    public string TargetName { get; set; } = "";
}

/// <summary>
/// 鍵盤重新對應（SharpKeys 式，純登錄檔）· In-app keyboard remapper. Builds the HKLM
/// "Scancode Map" REG_BINARY so keys can be remapped or disabled. Needs admin + a reboot to apply.
/// </summary>
public static class KeyboardRemapper
{
    private const string Path = @"SYSTEM\CurrentControlSet\Control\Keyboard Layout";
    private const string ValueName = "Scancode Map";

    /// <summary>常見可對應鍵（含延伸鍵 0xE0xx）· Common remappable keys (incl. extended 0xE0xx).</summary>
    public static readonly KeyDef[] Keys =
    {
        new("Caps Lock", "Caps Lock", 0x003A),
        new("Left Ctrl", "左 Ctrl", 0x001D),
        new("Left Alt", "左 Alt", 0x0038),
        new("Left Shift", "左 Shift", 0x002A),
        new("Left Win", "左 Win", 0xE05B),
        new("Right Win", "右 Win", 0xE05C),
        new("Menu (Apps)", "選單鍵", 0xE05D),
        new("Esc", "Esc", 0x0001),
        new("Tab", "Tab", 0x000F),
        new("Insert", "Insert", 0xE052),
        new("Scroll Lock", "Scroll Lock", 0x0046),
        new("Num Lock", "Num Lock", 0x0045),
        new("Print Screen", "Print Screen", 0xE037),
        new("Enter", "Enter", 0x001C),
        new("Backspace", "Backspace", 0x000E),
    };

    public static string NameOf(ushort scancode)
    {
        if (scancode == 0) return "✕ Disabled · 停用";
        var k = Keys.FirstOrDefault(x => x.Scancode == scancode);
        return k is not null ? $"{k.En} · {k.Zh}" : $"0x{scancode:X4}";
    }

    public static List<KeyMap> GetCurrent()
    {
        var result = new List<KeyMap>();
        if (RegistryHelper.GetValue(RegRoot.HKLM, Path, ValueName) is not byte[] b || b.Length < 16) return result;
        try
        {
            uint count = BitConverter.ToUInt32(b, 8); // includes the null terminator
            int entries = (int)count - 1;
            int o = 12;
            for (int i = 0; i < entries && o + 4 <= b.Length; i++, o += 4)
            {
                ushort target = (ushort)(b[o] | (b[o + 1] << 8));
                ushort source = (ushort)(b[o + 2] | (b[o + 3] << 8));
                result.Add(new KeyMap { Source = source, Target = target, SourceName = NameOf(source), TargetName = NameOf(target) });
            }
        }
        catch { /* corrupt map */ }
        return result;
    }

    public static byte[] Build(IReadOnlyList<KeyMap> maps)
    {
        var bytes = new byte[8 + 4 + maps.Count * 4 + 4];
        BitConverter.GetBytes((uint)(maps.Count + 1)).CopyTo(bytes, 8); // count = mappings + null terminator
        int o = 12;
        foreach (var m in maps)
        {
            bytes[o + 0] = (byte)(m.Target & 0xFF);
            bytes[o + 1] = (byte)(m.Target >> 8);
            bytes[o + 2] = (byte)(m.Source & 0xFF);
            bytes[o + 3] = (byte)(m.Source >> 8);
            o += 4;
        }
        // trailing 4 bytes are the null terminator (already zero)
        return bytes;
    }

    /// <summary>寫入對應（需要管理員 + 重啟）· Write the map (admin; reboot to apply).</summary>
    public static void Apply(IReadOnlyList<KeyMap> maps)
    {
        if (maps.Count == 0) { ClearAll(); return; }
        RegistryHelper.SetValue(RegRoot.HKLM, Path, ValueName, Build(maps), RegistryValueKind.Binary);
    }

    public static void ClearAll() => RegistryHelper.DeleteValue(RegRoot.HKLM, Path, ValueName);
}
