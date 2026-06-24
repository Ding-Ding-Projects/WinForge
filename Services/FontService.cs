using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>一個已安裝（或待裝）嘅字型 · A font file resolved to a face name + path.</summary>
public sealed class FontEntry
{
    /// <summary>顯示嘅字款名（例如 "Arial"）· The face/family name (e.g. "Arial").</summary>
    public string Face { get; init; } = "";
    /// <summary>登錄檔值名稱（例如 "Arial (TrueType)"）· The registry value name.</summary>
    public string RegName { get; init; } = "";
    /// <summary>字型檔完整路徑 · Full path to the .ttf/.otf/.ttc file.</summary>
    public string Path { get; init; } = "";
    /// <summary>true = 全機安裝（HKLM）· machine-wide (HKLM); false = per-user (HKCU).</summary>
    public bool MachineWide { get; init; }
    /// <summary>檔案類型標籤 · "TrueType" or "OpenType".</summary>
    public string Kind { get; init; } = "TrueType";
}

/// <summary>
/// 應用程式內字型管理（裝／睇／移除）· In-app font manager.
/// Per-user install (NO UAC): copy the file to %LOCALAPPDATA%\Microsoft\Windows\Fonts and add a value
/// under HKCU\...\Fonts named "&lt;Face&gt; (TrueType)" = file path, then broadcast WM_FONTCHANGE.
/// Machine-wide variant copies to %WINDIR%\Fonts + HKLM (needs admin). Replaces the Settings redirect.
/// </summary>
public static class FontService
{
    // ---- Win32 broadcast so running apps pick up the new/removed font ----
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern int AddFontResource(string path);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern bool RemoveFontResource(string path);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    private static readonly IntPtr HWND_BROADCAST = new(0xffff);
    private const uint WM_FONTCHANGE = 0x001D;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    private const string HkcuFontsKey = @"Software\Microsoft\Windows NT\CurrentVersion\Fonts";
    private const string HklmFontsKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";

    private static readonly string[] FontExtensions = { ".ttf", ".otf", ".ttc", ".otc", ".fon" };

    public static string UserFontDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Fonts");

    public static string MachineFontDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");

    public static bool IsFontFile(string path) =>
        FontExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>由副檔名估字型類型 · "TrueType" for .ttf/.ttc/.fon, "OpenType" for .otf/.otc.</summary>
    public static string KindFor(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".otf" or ".otc" ? "OpenType" : "TrueType";
    }

    // ------------------------------------------------------------------
    // Install
    // ------------------------------------------------------------------

    /// <summary>
    /// 裝一個字型檔 · Install one font file. Returns the installed entry.
    /// Per-user (no UAC) by default; machine-wide needs admin and will throw without it.
    /// </summary>
    public static FontEntry Install(string sourcePath, bool machineWide)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException(sourcePath);
        if (!IsFontFile(sourcePath)) throw new InvalidOperationException($"Not a font file: {sourcePath}");

        var face = ReadFaceName(sourcePath);
        var kind = KindFor(sourcePath);
        var dir = machineWide ? MachineFontDir : UserFontDir;
        Directory.CreateDirectory(dir);

        var fileName = Path.GetFileName(sourcePath);
        var dest = Path.Combine(dir, fileName);

        // Avoid clobbering a different file with the same name.
        if (File.Exists(dest) && !PathsEqual(sourcePath, dest))
        {
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            int i = 1;
            do { dest = Path.Combine(dir, $"{baseName}_{i++}{ext}"); } while (File.Exists(dest));
        }

        if (!PathsEqual(sourcePath, dest))
            File.Copy(sourcePath, dest, overwrite: true);

        var regName = $"{face} ({kind})";
        // Per-user values store the full path; machine-wide historically stores the bare file name
        // (the file lives in %WINDIR%\Fonts which GDI scans), but a full path also works.
        var regData = machineWide ? Path.GetFileName(dest) : dest;

        RegistryHelper.SetValue(machineWide ? RegRoot.HKLM : RegRoot.HKCU,
            machineWide ? HklmFontsKey : HkcuFontsKey, regName, regData, RegistryValueKind.String);

        AddFontResource(dest);
        Broadcast();

        return new FontEntry { Face = face, RegName = regName, Path = dest, MachineWide = machineWide, Kind = kind };
    }

    /// <summary>批次安裝 · Install many files; returns (installed, errors-by-path).</summary>
    public static (List<FontEntry> installed, List<(string path, string error)> errors) InstallMany(
        IEnumerable<string> paths, bool machineWide)
    {
        var ok = new List<FontEntry>();
        var bad = new List<(string, string)>();
        foreach (var p in paths)
        {
            try { ok.Add(Install(p, machineWide)); }
            catch (Exception ex) { bad.Add((p, ex.Message)); }
        }
        return (ok, bad);
    }

    // ------------------------------------------------------------------
    // Uninstall
    // ------------------------------------------------------------------

    /// <summary>移除一個已安裝字型 · Uninstall a font (deletes file + registry value).</summary>
    public static void Uninstall(FontEntry entry)
    {
        var root = entry.MachineWide ? RegRoot.HKLM : RegRoot.HKCU;
        var key = entry.MachineWide ? HklmFontsKey : HkcuFontsKey;

        if (!string.IsNullOrEmpty(entry.RegName))
            RegistryHelper.DeleteValue(root, key, entry.RegName);

        if (!string.IsNullOrEmpty(entry.Path) && File.Exists(entry.Path))
        {
            RemoveFontResource(entry.Path);
            try { File.Delete(entry.Path); }
            catch (IOException) { /* in use — registry value already gone, file clears on next reboot */ }
            catch (UnauthorizedAccessException) { throw; }
        }

        Broadcast();
    }

    // ------------------------------------------------------------------
    // List installed
    // ------------------------------------------------------------------

    /// <summary>列出已安裝嘅使用者字型 · List per-user installed fonts (HKCU).</summary>
    public static List<FontEntry> ListUserFonts() => ListFrom(RegRoot.HKCU, HkcuFontsKey, machineWide: false);

    /// <summary>列出全機字型 · List machine-wide fonts (HKLM).</summary>
    public static List<FontEntry> ListMachineFonts() => ListFrom(RegRoot.HKLM, HklmFontsKey, machineWide: true);

    private static List<FontEntry> ListFrom(RegRoot root, string key, bool machineWide)
    {
        var list = new List<FontEntry>();
        var baseDir = machineWide ? MachineFontDir : UserFontDir;
        foreach (var (name, _, data) in RegistryHelper.GetValues(root, key))
        {
            var raw = data?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(raw)) continue;

            // Resolve to a full path; machine-wide values are often just a filename under %WINDIR%\Fonts.
            var path = Path.IsPathRooted(raw) ? raw : Path.Combine(baseDir, raw);

            // Strip the " (TrueType)" / " (OpenType)" suffix to get a friendly face name.
            var face = name;
            var kind = "TrueType";
            int paren = name.LastIndexOf(" (", StringComparison.Ordinal);
            if (paren > 0 && name.EndsWith(")", StringComparison.Ordinal))
            {
                face = name[..paren];
                kind = name[(paren + 2)..^1];
            }

            list.Add(new FontEntry { Face = face, RegName = name, Path = path, MachineWide = machineWide, Kind = kind });
        }
        return list.OrderBy(f => f.Face, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ------------------------------------------------------------------
    // Face-name extraction (read the OpenType/TrueType 'name' table)
    // ------------------------------------------------------------------

    /// <summary>
    /// 由字型檔讀出字款名 · Read the typographic family name from a .ttf/.otf 'name' table.
    /// Falls back to the file name if the table cannot be parsed.
    /// </summary>
    public static string ReadFaceName(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var r = new BinaryReader(fs);

            uint sfnt = ReadU32(r);
            // .ttc / .otc collections start with 'ttcf' — read the first font's offset table.
            if (sfnt == 0x74746366) // 'ttcf'
            {
                r.BaseStream.Seek(8, SeekOrigin.Begin);   // skip tag + version
                ReadU32(r);                                // numFonts
                uint firstOffset = ReadU32(r);
                r.BaseStream.Seek(firstOffset, SeekOrigin.Begin);
                sfnt = ReadU32(r);
            }

            if (sfnt != 0x00010000 && sfnt != 0x4F54544F && sfnt != 0x74727565) // ttf / 'OTTO' / 'true'
                return Path.GetFileNameWithoutExtension(path);

            ushort numTables = ReadU16(r);
            r.BaseStream.Seek(6, SeekOrigin.Current); // searchRange, entrySelector, rangeShift

            uint nameOffset = 0;
            for (int i = 0; i < numTables; i++)
            {
                uint tag = ReadU32(r);
                ReadU32(r);                 // checksum
                uint off = ReadU32(r);
                ReadU32(r);                 // length
                if (tag == 0x6E616D65)      // 'name'
                {
                    nameOffset = off;
                    break;
                }
            }
            if (nameOffset == 0) return Path.GetFileNameWithoutExtension(path);

            r.BaseStream.Seek(nameOffset, SeekOrigin.Begin);
            ReadU16(r);                       // format
            ushort count = ReadU16(r);
            ushort stringOffset = ReadU16(r);

            string? family = null, typoFamily = null, fullName = null;
            for (int i = 0; i < count; i++)
            {
                ushort platformId = ReadU16(r);
                ReadU16(r);                   // encodingId
                ReadU16(r);                   // languageId
                ushort nameId = ReadU16(r);
                ushort length = ReadU16(r);
                ushort offset = ReadU16(r);

                if (nameId is not (1 or 4 or 16)) continue;

                long save = r.BaseStream.Position;
                r.BaseStream.Seek(nameOffset + stringOffset + offset, SeekOrigin.Begin);
                var bytes = r.ReadBytes(length);
                r.BaseStream.Seek(save, SeekOrigin.Begin);

                // platformId 0 = Unicode, 3 = Windows (UTF-16BE); 1 = Mac (ASCII-ish).
                bool unicode = platformId is 0 or 3;
                string value = unicode
                    ? Encoding.BigEndianUnicode.GetString(bytes)
                    : Encoding.ASCII.GetString(bytes);
                value = value.Trim('\0', ' ');
                if (value.Length == 0) continue;

                switch (nameId)
                {
                    case 16: typoFamily ??= value; break;   // typographic family (preferred)
                    case 1: family ??= value; break;        // legacy family
                    case 4: fullName ??= value; break;      // full font name
                }
            }

            var resolved = typoFamily ?? family ?? fullName;
            return string.IsNullOrWhiteSpace(resolved) ? Path.GetFileNameWithoutExtension(path) : resolved;
        }
        catch
        {
            return Path.GetFileNameWithoutExtension(path);
        }
    }

    private static uint ReadU32(BinaryReader r)
    {
        var b = r.ReadBytes(4);
        return (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
    }

    private static ushort ReadU16(BinaryReader r)
    {
        var b = r.ReadBytes(2);
        return (ushort)((b[0] << 8) | b[1]);
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private static void Broadcast() =>
        SendMessageTimeout(HWND_BROADCAST, WM_FONTCHANGE, IntPtr.Zero, IntPtr.Zero,
            SMTO_ABORTIFHUNG, 1000, out _);
}
