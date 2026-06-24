using System;
using System.IO;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// 應用程式內 hosts 編輯（純檔案 IO）· In-app hosts file editing (replaces the Notepad redirect).
/// 寫入需要管理員權限。Saving needs administrator rights.
/// </summary>
public static class HostsService
{
    public static string Path =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");

    public static string Read()
    {
        try { return File.Exists(Path) ? File.ReadAllText(Path) : ""; }
        catch (Exception ex) { return $"# {ex.Message}"; }
    }

    /// <summary>寫入（管理員）· Save (admin). Throws UnauthorizedAccessException without elevation.</summary>
    public static void Save(string content) => File.WriteAllText(Path, content);

    public static string Backup()
    {
        var dest = Path + ".wtbak";
        File.Copy(Path, dest, overwrite: true);
        return dest;
    }

    /// <summary>喺內容後面加一行封鎖某域名 · Append a 0.0.0.0 block line for a domain (if not already there).</summary>
    public static string AddBlock(string content, string domain)
    {
        domain = domain.Trim();
        if (string.IsNullOrEmpty(domain)) return content;

        bool already = content.Split('\n').Any(l =>
        {
            var t = l.Trim();
            return !t.StartsWith('#') && t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Skip(1).Any(tok => tok.Equals(domain, StringComparison.OrdinalIgnoreCase));
        });
        if (already) return content;

        if (content.Length > 0 && !content.EndsWith('\n')) content += Environment.NewLine;
        return content + $"0.0.0.0 {domain}" + Environment.NewLine;
    }

    public static int EntryCount(string content) =>
        content.Split('\n').Count(l => { var t = l.Trim(); return t.Length > 0 && !t.StartsWith('#'); });
}
