using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 分頁工作階段：將開住嘅分頁存落本地 git 倉，自動同步，可匯出／匯入／隨時還原。
/// Tab session — persists the open browser-style tabs to a local git repo under
/// %LOCALAPPDATA%\WinForge\session, auto-commits on every change (so the user can
/// restore any earlier state), and supports export / import to a single .json file.
/// JSON is hand-written / read via JsonDocument so it survives IL trimming.
/// </summary>
public static class TabSessionService
{
    public sealed class SessionData
    {
        public List<string> Tabs { get; set; } = new();
        public int Active { get; set; }
    }

    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge", "session");
    private static string FilePath => Path.Combine(Dir, "tabs.json");

    /// <summary>工作階段資料夾（本地 git 倉）· The session folder (a local git repo).</summary>
    public static string Folder => Dir;

    /// <summary>讀取上次嘅分頁 · Load the last saved tab session (null if none).</summary>
    public static SessionData? Load() => ParseFile(FilePath);

    /// <summary>存檔並喺背景 git commit（自動同步）· Save and auto-commit in the background.</summary>
    public static void Save(IEnumerable<string> tabs, int active)
    {
        try
        {
            var data = new SessionData { Tabs = tabs.Where(t => !string.IsNullOrWhiteSpace(t)).ToList(), Active = active };
            if (data.Tabs.Count == 0) return;
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, ToJson(data));
            _ = Task.Run(() => Commit("session update"));
        }
        catch { }
    }

    /// <summary>匯出工作階段做一個 .json · Export the session to a single .json file.</summary>
    public static void ExportTo(string path)
    {
        Directory.CreateDirectory(Dir);
        if (File.Exists(FilePath)) File.Copy(FilePath, path, overwrite: true);
        else File.WriteAllText(path, ToJson(new SessionData { Tabs = { "dashboard" } }));
    }

    /// <summary>由一個 .json 匯入工作階段 · Import a session from a .json file.</summary>
    public static SessionData? ImportFrom(string path)
    {
        var d = ParseFile(path);
        if (d is null) return null;
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, ToJson(d));
            _ = Task.Run(() => Commit("import session"));
        }
        catch { }
        return d;
    }

    // ----- trimming-safe JSON ----------------------------------------------
    private static SessionData? ParseFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            var data = new SessionData();
            if (root.TryGetProperty("Tabs", out var tabs) && tabs.ValueKind == JsonValueKind.Array)
                foreach (var t in tabs.EnumerateArray())
                {
                    var s = t.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) data.Tabs.Add(s);
                }
            if (root.TryGetProperty("Active", out var a) && a.ValueKind == JsonValueKind.Number) data.Active = a.GetInt32();
            return data.Tabs.Count == 0 ? null : data;
        }
        catch { return null; }
    }

    private static string ToJson(SessionData d)
    {
        var sb = new StringBuilder();
        sb.Append("{\n  \"Tabs\": [");
        for (int i = 0; i < d.Tabs.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('"').Append(d.Tabs[i].Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
        }
        sb.Append("],\n  \"Active\": ").Append(d.Active).Append("\n}\n");
        return sb.ToString();
    }

    // ----- local git mirror -------------------------------------------------
    private static void EnsureRepo()
    {
        try
        {
            if (Directory.Exists(Path.Combine(Dir, ".git"))) return;
            Git("init -q");
        }
        catch { }
    }

    private static void Commit(string msg)
    {
        try
        {
            EnsureRepo();
            Git("add -A");
            Git($"-c user.name=WinForge -c user.email=session@winforge.local commit -q --allow-empty -m \"{msg.Replace("\"", "'")}\"");
        }
        catch { }
    }

    private static void Git(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = Dir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(8000);
        }
        catch { }
    }
}
