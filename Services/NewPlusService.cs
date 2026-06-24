using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// 由使用者範本建立檔案／資料夾（PowerToys New+ 嘅原生複製品）。
/// Native clone of PowerToys "New+": create files and folders from user-defined templates.
///
/// 範本資料夾預設喺 %LOCALAPPDATA%\WinForge\NewPlusTemplates，可以喺設定改。
/// The templates folder defaults to %LOCALAPPDATA%\WinForge\NewPlusTemplates and is configurable.
/// 入面放使用者想喺「新增」選單見到嘅範本檔案同資料夾。
/// It holds the template files and folders the user wants in their "New" menu.
///
/// 全部係純 .NET 檔案操作 — 唔需要套件識別碼，未封裝都用得。
/// All pure .NET file operations — no package identity required, works fully unpackaged.
/// </summary>
public static class NewPlusService
{
    private const string TemplateLocationKey = "newplus.templateLocation";
    private const string HideExtensionKey = "newplus.hideExtension";
    private const string HideStartingDigitsKey = "newplus.hideStartingDigits";
    private const string ReplaceVariablesKey = "newplus.replaceVariables";

    /// <summary>範本種類 · Kind of template.</summary>
    public enum TemplateKind { File, Folder }

    /// <summary>一個範本項目 · One template entry (a file or a folder under the templates root).</summary>
    public sealed class TemplateItem
    {
        public string Path { get; init; } = "";
        public TemplateKind Kind { get; init; }
        public bool IsFolder => Kind == TemplateKind.Folder;

        /// <summary>磁碟上嘅實際名（含副檔名）· The real on-disk name (with extension).</summary>
        public string FileName => System.IO.Path.GetFileName(Path);

        /// <summary>副檔名（細楷，含點）· Extension, lower-cased, with the dot (files only).</summary>
        public string Extension => IsFolder ? "" : System.IO.Path.GetExtension(Path).ToLowerInvariant();

        /// <summary>顯示名（按設定隱藏副檔名／開頭數字後）· Display name after hiding extension / starting digits per settings.</summary>
        public string DisplayName => GetDisplayName(FileName, IsFolder, HideExtension, HideStartingDigits);

        /// <summary>檔案大小（位元組）· File size in bytes; folders report total tree size.</summary>
        public long SizeBytes { get; init; }

        public DateTime Modified { get; init; }
    }

    // ===== Settings =====

    /// <summary>範本資料夾位置 · The templates folder location (default %LOCALAPPDATA%\WinForge\NewPlusTemplates).</summary>
    public static string TemplatesFolder
    {
        get
        {
            var stored = SettingsStore.Get(TemplateLocationKey, "");
            if (!string.IsNullOrWhiteSpace(stored)) return stored;
            return DefaultTemplatesFolder;
        }
        set => SettingsStore.Set(TemplateLocationKey, value ?? "");
    }

    public static string DefaultTemplatesFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinForge", "NewPlusTemplates");

    /// <summary>顯示時隱藏副檔名 · Hide the file extension in display names (default true, like New+).</summary>
    public static bool HideExtension
    {
        get => SettingsStore.Get(HideExtensionKey, "true") == "true";
        set => SettingsStore.Set(HideExtensionKey, value ? "true" : "false");
    }

    /// <summary>隱藏開頭排序數字 · Hide leading sort digits in display names (default true, like New+).</summary>
    public static bool HideStartingDigits
    {
        get => SettingsStore.Get(HideStartingDigitsKey, "true") == "true";
        set => SettingsStore.Set(HideStartingDigitsKey, value ? "true" : "false");
    }

    /// <summary>建立時替換名稱中嘅變數（日期等）· Replace variables (dates etc.) in names when creating (default true).</summary>
    public static bool ReplaceVariables
    {
        get => SettingsStore.Get(ReplaceVariablesKey, "true") == "true";
        set => SettingsStore.Set(ReplaceVariablesKey, value ? "true" : "false");
    }

    // ===== Templates folder management =====

    /// <summary>確保範本資料夾存在 · Ensure the templates folder exists; returns its path.</summary>
    public static string EnsureTemplatesFolder()
    {
        var dir = TemplatesFolder;
        try { Directory.CreateDirectory(dir); } catch { /* best effort */ }
        return dir;
    }

    /// <summary>列出所有範本（資料夾先，然後檔案，順序排序）· List all templates (folders first, then files, ordinal-sorted).</summary>
    public static List<TemplateItem> ListTemplates()
    {
        var root = EnsureTemplatesFolder();
        var items = new List<TemplateItem>();
        if (!Directory.Exists(root)) return items;

        IEnumerable<string> dirs, files;
        try { dirs = Directory.EnumerateDirectories(root); } catch { dirs = Array.Empty<string>(); }
        try { files = Directory.EnumerateFiles(root); } catch { files = Array.Empty<string>(); }

        foreach (var d in dirs.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (IsHiddenOrSystem(d)) continue;
            DateTime modified; long size;
            try { modified = Directory.GetLastWriteTime(d); } catch { modified = DateTime.MinValue; }
            try { size = DirectorySize(d); } catch { size = 0; }
            items.Add(new TemplateItem { Path = d, Kind = TemplateKind.Folder, Modified = modified, SizeBytes = size });
        }

        foreach (var f in files.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (IsHiddenOrSystem(f)) continue;
            DateTime modified; long size;
            try { var fi = new FileInfo(f); modified = fi.LastWriteTime; size = fi.Length; }
            catch { modified = DateTime.MinValue; size = 0; }
            items.Add(new TemplateItem { Path = f, Kind = TemplateKind.File, Modified = modified, SizeBytes = size });
        }

        return items;
    }

    /// <summary>由現有檔案／資料夾複製入範本資料夾 · Copy an existing file/folder into the templates folder as a new template.</summary>
    public static (bool ok, string message) AddTemplateFromPath(string sourcePath)
    {
        var root = EnsureTemplatesFolder();
        try
        {
            if (Directory.Exists(sourcePath))
            {
                var name = new DirectoryInfo(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Name;
                var dest = MakeUniquePath(Path.Combine(root, name));
                CopyDirectory(sourcePath, dest);
                return (true, dest);
            }
            if (File.Exists(sourcePath))
            {
                var dest = MakeUniquePath(Path.Combine(root, Path.GetFileName(sourcePath)));
                File.Copy(sourcePath, dest, overwrite: false);
                return (true, dest);
            }
            return (false, "Source does not exist · 來源唔存在");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>建立空白範本檔案 · Create a new blank template file. Name may include an extension.</summary>
    public static (bool ok, string message) CreateBlankFileTemplate(string name)
    {
        var root = EnsureTemplatesFolder();
        name = SanitizeName(name);
        if (string.IsNullOrWhiteSpace(name)) return (false, "Empty name · 名稱空白");
        try
        {
            var dest = MakeUniquePath(Path.Combine(root, name));
            File.WriteAllText(dest, "");
            return (true, dest);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    /// <summary>建立空白範本資料夾 · Create a new blank template folder.</summary>
    public static (bool ok, string message) CreateBlankFolderTemplate(string name)
    {
        var root = EnsureTemplatesFolder();
        name = SanitizeName(name);
        if (string.IsNullOrWhiteSpace(name)) return (false, "Empty name · 名稱空白");
        try
        {
            var dest = MakeUniquePath(Path.Combine(root, name));
            Directory.CreateDirectory(dest);
            return (true, dest);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    /// <summary>重新命名範本 · Rename a template (file or folder).</summary>
    public static (bool ok, string message) RenameTemplate(string path, string newName)
    {
        newName = SanitizeName(newName);
        if (string.IsNullOrWhiteSpace(newName)) return (false, "Empty name · 名稱空白");
        try
        {
            var parent = Path.GetDirectoryName(path) ?? TemplatesFolder;
            var dest = Path.Combine(parent, newName);
            if (string.Equals(dest, path, StringComparison.OrdinalIgnoreCase)) return (true, path);
            dest = MakeUniquePath(dest);
            if (Directory.Exists(path)) Directory.Move(path, dest);
            else File.Move(path, dest);
            return (true, dest);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    /// <summary>刪除範本 · Delete a template (file or folder).</summary>
    public static (bool ok, string message) DeleteTemplate(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else if (File.Exists(path)) File.Delete(path);
            else return (false, "Not found · 搵唔到");
            return (true, "");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // ===== Create from template =====

    /// <summary>
    /// 由範本喺目標資料夾建立檔案／資料夾 · Create a file/folder from a template into a destination folder.
    /// 可選改名（支援日期／變數替換）· Optional new name (supports date/variable substitution).
    /// 回傳建立出嚟嘅完整路徑 · Returns the full path that was created.
    /// </summary>
    public static (bool ok, string message, string createdPath) CreateFromTemplate(
        TemplateItem template, string destinationFolder, string? newNameRaw, bool replaceVariables)
    {
        if (template is null) return (false, "No template · 冇範本", "");
        if (string.IsNullOrWhiteSpace(destinationFolder) || !Directory.Exists(destinationFolder))
            return (false, "Destination folder does not exist · 目標資料夾唔存在", "");
        if (!File.Exists(template.Path) && !Directory.Exists(template.Path))
            return (false, "Template no longer exists · 範本已經唔存在", "");

        try
        {
            // 決定目標名 · Decide the target name.
            // 無自訂名就用範本實際名（按設定剝走開頭數字）。
            // With no override, use the template's real on-disk name (stripping leading digits per settings).
            string targetName = string.IsNullOrWhiteSpace(newNameRaw)
                ? RemoveStartingDigits(template.FileName, template.IsFolder, HideStartingDigits)
                : newNameRaw!;

            // 替換變數 · Substitute variables in the name.
            if (replaceVariables) targetName = ResolveVariables(targetName, parentFolderName: new DirectoryInfo(destinationFolder).Name);

            targetName = SanitizeName(targetName);
            if (string.IsNullOrWhiteSpace(targetName))
                return (false, "Resolved name is empty · 解析後名稱空白", "");

            var dest = MakeUniquePath(Path.Combine(destinationFolder, targetName));

            if (template.IsFolder)
            {
                CopyDirectory(template.Path, dest);
                if (replaceVariables) ResolveVariablesInTreeNames(dest);
                TouchTreeWriteTime(dest);
            }
            else
            {
                File.Copy(template.Path, dest, overwrite: false);
                try { File.SetLastWriteTime(dest, DateTime.Now); } catch { }
            }

            return (true, "", dest);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, "");
        }
    }

    // ===== Legacy "New" menu (best-effort Explorer integration) =====

    /// <summary>
    /// 舊式 Windows 範本資料夾 · The legacy per-user Windows Templates folder
    /// (%APPDATA%\Microsoft\Windows\Templates). 放喺呢度嘅檔案會出現喺檔案總管嘅「新增」選單。
    /// Files placed here appear in Explorer's "New" submenu after their extension is registered via ShellNew.
    /// </summary>
    public static string LegacyWindowsTemplatesFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft", "Windows", "Templates");

    /// <summary>
    /// 嘗試將一個範本檔案註冊入檔案總管嘅「新增」選單 · Best-effort: register a single template file into
    /// Explorer's classic "New" submenu by (1) copying it into the legacy Windows Templates folder and
    /// (2) writing HKCU\Software\Classes\.&lt;ext&gt;\ShellNew\FileName. Works in an unpackaged app for the
    /// classic / "Show more options" menu. Returns (ok, message).
    /// </summary>
    public static (bool ok, string message) RegisterInExplorerNewMenu(TemplateItem template)
    {
        if (template is null || template.IsFolder)
            return (false, "Explorer New menu supports template files only · 「新增」選單只支援範本檔案");
        var ext = template.Extension;
        if (string.IsNullOrEmpty(ext))
            return (false, "Template has no extension to register · 範本冇副檔名可註冊");

        try
        {
            // 1) Copy the template into the legacy Windows Templates folder (where ShellNew/FileName resolves from).
            var legacy = LegacyWindowsTemplatesFolder;
            Directory.CreateDirectory(legacy);
            var legacyName = template.FileName;
            var legacyPath = Path.Combine(legacy, legacyName);
            File.Copy(template.Path, legacyPath, overwrite: true);

            // 2) Register HKCU\Software\Classes\.<ext>\ShellNew with FileName = legacy template file name.
            using var classes = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{ext}");
            using var shellNew = classes?.CreateSubKey("ShellNew");
            shellNew?.SetValue("FileName", legacyName, Microsoft.Win32.RegistryValueKind.String);

            return (true, $"{ext} → {legacyName}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>移除「新增」選單註冊 · Remove the ShellNew registration for an extension (best effort).</summary>
    public static (bool ok, string message) UnregisterFromExplorerNewMenu(string extension)
    {
        var ext = NormalizeExt(extension);
        if (string.IsNullOrEmpty(ext)) return (false, "No extension · 冇副檔名");
        try
        {
            using var classes = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($"Software\\Classes\\{ext}", writable: true);
            classes?.DeleteSubKeyTree("ShellNew", throwOnMissingSubKey: false);
            return (true, ext);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    private static string NormalizeExt(string ext)
    {
        ext = (ext ?? "").Trim().ToLowerInvariant();
        if (ext.Length == 0) return "";
        if (!ext.StartsWith('.')) ext = "." + ext;
        return ext;
    }

    // ===== Variable / date substitution =====

    /// <summary>
    /// 範本名稱支援嘅變數 · The variable tokens supported in template names. Documents the New+-compatible set.
    /// </summary>
    public static readonly (string Token, string MeaningEn, string MeaningZh)[] SupportedVariables =
    {
        ("$YYYY", "4-digit year", "四位年份"),
        ("$YY", "2-digit year", "兩位年份"),
        ("$Y", "year, last digit", "年份末位"),
        ("$MMMM", "full month name", "月份全名"),
        ("$MMM", "short month name", "月份簡稱"),
        ("$MM", "2-digit month", "兩位月份"),
        ("$M", "month, no leading zero", "月份（無前導零）"),
        ("$DDDD", "full weekday name", "星期全名"),
        ("$DDD", "short weekday name", "星期簡稱"),
        ("$DD", "2-digit day", "兩位日期"),
        ("$D", "day, no leading zero", "日期（無前導零）"),
        ("$hh", "2-digit hour (24h)", "兩位時（24 小時）"),
        ("$h", "hour (24h)", "時（24 小時）"),
        ("$HH", "2-digit hour (12h)", "兩位時（12 小時）"),
        ("$H", "hour (12h)", "時（12 小時）"),
        ("$mm", "2-digit minute", "兩位分鐘"),
        ("$m", "minute, no leading zero", "分鐘（無前導零）"),
        ("$ss", "2-digit second", "兩位秒"),
        ("$s", "second, no leading zero", "秒（無前導零）"),
        ("$TT", "AM/PM (upper)", "上午／下午（大楷）"),
        ("$tt", "am/pm (lower)", "上午／下午（細楷）"),
        ("$PARENT_FOLDER_NAME", "destination folder name", "目標資料夾名"),
        ("%VAR%", "environment variable", "環境變數"),
    };

    /// <summary>
    /// 解析名稱中嘅變數 · Resolve date/time tokens, environment variables and $PARENT_FOLDER_NAME in a name.
    /// 用 $$ 輸出一個字面 $ · Use $$ to emit a literal $. Tokens are case-sensitive (matching New+).
    /// </summary>
    public static string ResolveVariables(string input, string? parentFolderName = null)
    {
        if (string.IsNullOrEmpty(input)) return input ?? "";

        var now = DateTime.Now;
        var ci = CultureInfo.CurrentCulture;
        int hour12 = now.Hour % 12; if (hour12 == 0) hour12 = 12;

        // Protect literal "$$" first so it survives token replacement, then restore at the end.
        const string dollarPlaceholder = "DOLLAR";
        var s = input.Replace("$$", dollarPlaceholder);

        // Order matters: longest tokens first within each unit so prefixes aren't half-eaten.
        var map = new (string token, string value)[]
        {
            ("$YYYY", now.ToString("yyyy", ci)),
            ("$YY",   now.ToString("yy", ci)),
            ("$Y",    (now.Year % 10).ToString(ci)),
            ("$MMMM", ci.DateTimeFormat.GetMonthName(now.Month)),
            ("$MMM",  ci.DateTimeFormat.GetAbbreviatedMonthName(now.Month)),
            ("$MM",   now.ToString("MM", ci)),
            ("$M",    now.Month.ToString(ci)),
            ("$DDDD", ci.DateTimeFormat.GetDayName(now.DayOfWeek)),
            ("$DDD",  ci.DateTimeFormat.GetAbbreviatedDayName(now.DayOfWeek)),
            ("$DD",   now.ToString("dd", ci)),
            ("$D",    now.Day.ToString(ci)),
            ("$hh",   now.ToString("HH", ci)),   // 24-hour, 2 digit
            ("$h",    now.Hour.ToString(ci)),    // 24-hour
            ("$HH",   hour12.ToString("00", ci)),// 12-hour, 2 digit
            ("$H",    hour12.ToString(ci)),      // 12-hour
            ("$mm",   now.ToString("mm", ci)),
            ("$m",    now.Minute.ToString(ci)),
            ("$ss",   now.ToString("ss", ci)),
            ("$s",    now.Second.ToString(ci)),
            ("$TT",   now.Hour < 12 ? "AM" : "PM"),
            ("$tt",   now.Hour < 12 ? "am" : "pm"),
        };
        foreach (var (token, value) in map)
            s = s.Replace(token, value, StringComparison.Ordinal);

        if (!string.IsNullOrEmpty(parentFolderName))
            s = s.Replace("$PARENT_FOLDER_NAME", parentFolderName, StringComparison.Ordinal);

        // Environment variables: %NAME% (case-insensitive lookup).
        s = Regex.Replace(s, "%([^%]+)%", m =>
        {
            var val = Environment.GetEnvironmentVariable(m.Groups[1].Value);
            return val ?? m.Value;
        });

        s = s.Replace(dollarPlaceholder, "$");
        return s;
    }

    /// <summary>遞迴替換已複製樹入面每個項目名嘅變數 · Recursively rename entries in a copied tree, resolving variables (leaves first).</summary>
    private static void ResolveVariablesInTreeNames(string root)
    {
        // Rename deepest entries first so parent renames don't invalidate child paths.
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .OrderByDescending(p => p.Length).ToList())
            RenameEntryWithVariables(dir, isDir: true);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToList())
            RenameEntryWithVariables(file, isDir: false);
    }

    private static void RenameEntryWithVariables(string path, bool isDir)
    {
        try
        {
            var parent = Path.GetDirectoryName(path);
            if (parent is null) return;
            var name = Path.GetFileName(path);
            var resolved = SanitizeName(ResolveVariables(name, parentFolderName: new DirectoryInfo(parent).Name));
            if (string.IsNullOrWhiteSpace(resolved) || resolved == name) return;
            var dest = MakeUniquePath(Path.Combine(parent, resolved));
            if (isDir) Directory.Move(path, dest);
            else File.Move(path, dest);
        }
        catch { /* best effort per entry */ }
    }

    // ===== Display-name helpers (extension + leading-digit handling) =====

    /// <summary>顯示名 · Build a display name, hiding extension and/or leading sort digits per flags.</summary>
    public static string GetDisplayName(string fileName, bool isFolder, bool hideExtension, bool hideStartingDigits)
    {
        var name = fileName;
        if (hideStartingDigits) name = RemoveStartingDigits(name, isFolder, true);
        if (hideExtension && !isFolder)
        {
            var ext = Path.GetExtension(name);
            if (!string.IsNullOrEmpty(ext)) name = name.Substring(0, name.Length - ext.Length);
        }
        return name;
    }

    /// <summary>
    /// 剝走開頭排序數字 · Strip leading sort digits (and one separator) from a name, mirroring New+:
    /// "01. First.txt" → "First.txt", "03 Third" → "Third", "05.Fifth.txt" → "Fifth.txt",
    /// but a name that is all digits ("001231.txt") is left unchanged.
    /// </summary>
    public static string RemoveStartingDigits(string fileName, bool isFolder, bool hideStartingDigits)
    {
        if (!hideStartingDigits || string.IsNullOrEmpty(fileName)) return fileName;

        var ext = isFolder ? "" : Path.GetExtension(fileName);
        var stem = isFolder ? fileName : fileName.Substring(0, fileName.Length - ext.Length);

        // Count leading digits.
        int i = 0;
        while (i < stem.Length && char.IsDigit(stem[i])) i++;
        if (i == 0) return fileName;            // no leading digits

        // If the whole stem is digits, keep it (it IS the name, e.g. "001231").
        if (i == stem.Length) return fileName;

        // Strip one optional separator after the digit run: ". ", " .", ".", or " ".
        int j = i;
        if (j < stem.Length && (stem[j] == '.' || stem[j] == ' '))
        {
            char sep = stem[j];
            j++;
            // allow the paired separator too (". " or " .")
            if (j < stem.Length && ((sep == '.' && stem[j] == ' ') || (sep == ' ' && stem[j] == '.')))
                j++;
        }
        var newStem = stem.Substring(j);
        if (string.IsNullOrEmpty(newStem)) return fileName; // would empty the name → keep original
        return newStem + ext;
    }

    // ===== Filesystem helpers =====

    /// <summary>移除非法檔名字元 · Replace invalid filename characters with a space.</summary>
    public static string SanitizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var invalid = new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };
        var sb = new StringBuilder(name.Length);
        foreach (var c in name) sb.Append(invalid.Contains(c) ? ' ' : c);
        return sb.ToString().Trim();
    }

    /// <summary>令路徑唯一（已存在就加 (1)、(2)…）· Make a path unique by appending " (1)", " (2)"… before the extension.</summary>
    public static string MakeUniquePath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path) ?? "";
        var ext = Path.GetExtension(path);
        var stem = Path.GetFileNameWithoutExtension(path);
        // For folders, GetExtension may grab a trailing ".x"; treat folders as having no extension.
        if (Directory.Exists(path)) { stem = Path.GetFileName(path); ext = ""; }
        for (int n = 1; n < 10000; n++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({n}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
        return path;
    }

    /// <summary>遞迴複製資料夾樹 · Recursively copy a directory tree.</summary>
    public static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, dest));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(source, dest), overwrite: true);
    }

    private static long DirectorySize(string dir)
    {
        long total = 0;
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(f).Length; } catch { }
            }
        }
        catch { }
        return total;
    }

    private static void TouchTreeWriteTime(string root)
    {
        var now = DateTime.Now;
        try { Directory.SetLastWriteTime(root, now); } catch { }
        try
        {
            foreach (var d in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
                try { Directory.SetLastWriteTime(d, now); } catch { }
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                try { File.SetLastWriteTime(f, now); } catch { }
        }
        catch { }
    }

    private static bool IsHiddenOrSystem(string path)
    {
        try
        {
            var attr = File.GetAttributes(path);
            return attr.HasFlag(FileAttributes.Hidden) || attr.HasFlag(FileAttributes.System);
        }
        catch { return false; }
    }

    /// <summary>易讀大小 · Human-readable size.</summary>
    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes; int u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return u == 0 ? $"{bytes} {units[u]}" : $"{v:0.#} {units[u]}";
    }
}
