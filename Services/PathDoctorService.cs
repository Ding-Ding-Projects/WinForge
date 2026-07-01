using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// PATH 醫生 · PATH Doctor — pure-managed helpers for reading, cleaning and writing the User and
/// Machine <c>Path</c> environment variables. Splits on ';', drops empties, and never throws:
/// every operation returns a plain result so the UI can show a friendly bilingual status.
/// Machine writes need administrator rights — <see cref="Apply"/> reports that instead of crashing.
/// </summary>
public static class PathDoctorService
{
    /// <summary>Which PATH we operate on.</summary>
    public enum Scope { User, Machine }

    private static EnvironmentVariableTarget TargetOf(Scope s)
        => s == Scope.Machine ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;

    /// <summary>讀取並拆分 PATH · Read a PATH and split it into non-empty, trimmed entries.</summary>
    public static List<string> Read(Scope scope)
    {
        try
        {
            var raw = Environment.GetEnvironmentVariable("Path", TargetOf(scope)) ?? string.Empty;
            return Split(raw);
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>拆分「;」分隔字串，去除空白同重複空項 · Split a ';'-separated string, dropping empties.</summary>
    public static List<string> Split(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return new List<string>();
        try
        {
            return raw.Split(';')
                      .Select(e => e.Trim())
                      .Where(e => e.Length > 0)
                      .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>組回「;」字串 · Join entries back into a ';'-separated string.</summary>
    public static string Join(IEnumerable<string> entries)
    {
        try { return string.Join(";", entries.Where(e => !string.IsNullOrWhiteSpace(e))); }
        catch { return string.Empty; }
    }

    /// <summary>資料夾係咪存在 · Whether the entry resolves to an existing directory.</summary>
    public static bool Exists(string entry)
    {
        try { return !string.IsNullOrWhiteSpace(entry) && Directory.Exists(Environment.ExpandEnvironmentVariables(entry)); }
        catch { return false; }
    }

    /// <summary>移除大小寫不敏感嘅重複（保留第一個）· Remove case-insensitive duplicates, keeping the first.</summary>
    public static List<string> Dedupe(IEnumerable<string> entries)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        try
        {
            foreach (var e in entries)
            {
                var key = e?.Trim().TrimEnd('\\', '/') ?? string.Empty;
                if (key.Length == 0) continue;
                if (seen.Add(key)) result.Add(e!.Trim());
            }
        }
        catch { }
        return result;
    }

    /// <summary>移除唔存在嘅資料夾 · Drop entries whose directory does not exist.</summary>
    public static List<string> RemoveDead(IEnumerable<string> entries)
    {
        try { return entries.Where(Exists).ToList(); }
        catch { return entries?.ToList() ?? new List<string>(); }
    }

    /// <summary>排序（大小寫不敏感）· Sort entries case-insensitively.</summary>
    public static List<string> Sort(IEnumerable<string> entries)
    {
        try { return entries.OrderBy(e => e, StringComparer.OrdinalIgnoreCase).ToList(); }
        catch { return entries?.ToList() ?? new List<string>(); }
    }

    /// <summary>寫入結果 · Result of an <see cref="Apply"/> attempt.</summary>
    public readonly record struct ApplyResult(bool Ok, bool NeedsAdmin, string? Error);

    /// <summary>
    /// 寫返去環境變數 · Write entries back to the target PATH. User target works unelevated;
    /// Machine target needs administrator — a failure there is reported via <see cref="ApplyResult.NeedsAdmin"/>
    /// rather than thrown.
    /// </summary>
    public static ApplyResult Apply(Scope scope, IEnumerable<string> entries)
    {
        try
        {
            Environment.SetEnvironmentVariable("Path", Join(entries), TargetOf(scope));
            return new ApplyResult(true, false, null);
        }
        catch (System.Security.SecurityException ex)
        {
            return new ApplyResult(false, scope == Scope.Machine, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ApplyResult(false, scope == Scope.Machine, ex.Message);
        }
        catch (Exception ex)
        {
            return new ApplyResult(false, scope == Scope.Machine, ex.Message);
        }
    }
}
