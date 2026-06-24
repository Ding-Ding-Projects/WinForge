using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// 批次改名引擎（純 C#）· Pure-C# batch-rename engine (no external tool). Find/replace with optional
/// regex, case sensitivity and "include extension".
/// </summary>
public static class RenameEngine
{
    public static string NewName(string fileName, string find, string replace, bool regex, bool caseSensitive, bool includeExt)
    {
        if (string.IsNullOrEmpty(find)) return fileName;

        string namePart = includeExt ? fileName : Path.GetFileNameWithoutExtension(fileName);
        string ext = includeExt ? "" : Path.GetExtension(fileName);
        replace ??= "";

        string result;
        try
        {
            if (regex)
            {
                var opts = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                result = Regex.Replace(namePart, find, replace, opts);
            }
            else
            {
                var comp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                result = ReplacePlain(namePart, find, replace, comp);
            }
        }
        catch
        {
            return fileName; // invalid regex → leave unchanged
        }

        return result + ext;
    }

    private static string ReplacePlain(string input, string find, string replace, StringComparison comp)
    {
        var sb = new StringBuilder();
        int i = 0;
        while (true)
        {
            int idx = input.IndexOf(find, i, comp);
            if (idx < 0)
            {
                sb.Append(input, i, input.Length - i);
                break;
            }
            sb.Append(input, i, idx - i);
            sb.Append(replace);
            i = idx + find.Length;
        }
        return sb.ToString();
    }

    public static bool IsValidName(string name)
        => !string.IsNullOrWhiteSpace(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
