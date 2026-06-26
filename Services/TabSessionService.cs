using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// Tab session state mirrored to a local git repo under %LOCALAPPDATA%\WinForge\session.
/// The JSON parser accepts the old string-array schema and the current grouped/styled schema.
/// </summary>
public static class TabSessionService
{
    public sealed class TabFontData
    {
        public string Family { get; set; } = string.Empty;
        public double Size { get; set; }
        public string Weight { get; set; } = string.Empty;
        public string Style { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }

    public sealed class TabData
    {
        public string Key { get; set; } = "dashboard";
        public string Name { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public TabFontData Font { get; set; } = new();
    }

    public sealed class TabGroupData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string RepoPath { get; set; } = string.Empty;
        public TabFontData Font { get; set; } = new();
    }

    public sealed class LocalGitData
    {
        public string CurrentRepoPath { get; set; } = string.Empty;
        public List<string> SavedRepos { get; set; } = new();
    }

    public sealed class SessionData
    {
        public int Version { get; set; } = 2;
        public List<TabData> Tabs { get; set; } = new();
        public int Active { get; set; }
        public List<TabGroupData> Groups { get; set; } = new();
        public LocalGitData LocalGit { get; set; } = new();
    }

    public sealed class GroupExportData
    {
        public int Version { get; set; } = 1;
        public TabGroupData Group { get; set; } = new();
        public List<TabData> Tabs { get; set; } = new();
        public LocalGitData LocalGit { get; set; } = new();
    }

    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge", "session");
    private static string FilePath => Path.Combine(Dir, "tabs.json");
    private static readonly object CommitGate = new();

    /// <summary>The session folder, also initialized as a local git repo.</summary>
    public static string Folder => Dir;

    /// <summary>Load the last saved tab session, or null if none exists.</summary>
    public static SessionData? Load() => ParseSessionFile(FilePath);

    /// <summary>Compatibility save for the old key-only callers.</summary>
    public static void Save(IEnumerable<string> tabs, int active)
    {
        Save(new SessionData
        {
            Tabs = tabs.Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => new TabData { Key = t })
                .ToList(),
            Active = active,
        });
    }

    /// <summary>Save and commit the full grouped/styled session in the background.</summary>
    public static void Save(SessionData data)
    {
        try
        {
            var normalized = CloneSession(data);
            Normalize(normalized);
            if (normalized.Tabs.Count == 0) return;

            Directory.CreateDirectory(Dir);
            var json = ToJson(normalized);
            if (File.Exists(FilePath) && File.ReadAllText(FilePath) == json) return;

            File.WriteAllText(FilePath, json);
            ScheduleCommit("tab session update");
        }
        catch { }
    }

    /// <summary>Export the latest saved session to a JSON file.</summary>
    public static void ExportTo(string path)
    {
        var data = Load() ?? new SessionData { Tabs = { new TabData { Key = "dashboard" } } };
        ExportTo(path, data);
    }

    /// <summary>Export the supplied in-memory session to a JSON file.</summary>
    public static void ExportTo(string path, SessionData data)
    {
        EnsureParentFolder(path);
        File.WriteAllText(path, ToJson(data));
    }

    /// <summary>Import a full tab session from JSON, save it, and commit it.</summary>
    public static SessionData? ImportFrom(string path)
    {
        var data = ParseSessionFile(path);
        if (data is null) return null;
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, ToJson(data));
            ScheduleCommit("import tab session");
        }
        catch { }
        return data;
    }

    /// <summary>Export one tab group plus all tabs assigned to that group.</summary>
    public static void ExportGroupTo(string path, SessionData session, string groupId)
    {
        var group = session.Groups.FirstOrDefault(g => Same(g.Id, groupId));
        if (group is null) return;
        var data = new GroupExportData
        {
            Group = CloneGroup(group),
            Tabs = session.Tabs.Where(t => Same(t.GroupId, groupId)).Select(CloneTab).ToList(),
            LocalGit = CloneLocalGit(session.LocalGit),
        };
        EnsureParentFolder(path);
        File.WriteAllText(path, ToGroupJson(data));
    }

    /// <summary>Import a group export. If a full session is picked, the first group is imported.</summary>
    public static GroupExportData? ImportGroupFrom(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            if (TryGet(root, "Group", out var g) && g.ValueKind == JsonValueKind.Object)
            {
                var data = new GroupExportData { Group = ParseGroup(g) };
                if (TryGet(root, "Tabs", out var tabs) && tabs.ValueKind == JsonValueKind.Array)
                    foreach (var item in tabs.EnumerateArray())
                        data.Tabs.Add(ParseTab(item));
                if (TryGet(root, "LocalGit", out var localGit) && localGit.ValueKind == JsonValueKind.Object)
                    data.LocalGit = ParseLocalGit(localGit);
                NormalizeGroupExport(data);
                return string.IsNullOrWhiteSpace(data.Group.Id) ? null : data;
            }

            var session = ParseSession(root);
            var group = session?.Groups.FirstOrDefault();
            if (session is null || group is null) return null;
            return new GroupExportData
            {
                Group = CloneGroup(group),
                Tabs = session.Tabs.Where(t => Same(t.GroupId, group.Id)).Select(CloneTab).ToList(),
                LocalGit = CloneLocalGit(session.LocalGit),
            };
        }
        catch { return null; }
    }

    public static SessionData CloneSession(SessionData source)
    {
        return new SessionData
        {
            Version = source.Version <= 0 ? 2 : source.Version,
            Active = source.Active,
            Tabs = source.Tabs.Select(CloneTab).ToList(),
            Groups = source.Groups.Select(CloneGroup).ToList(),
            LocalGit = CloneLocalGit(source.LocalGit),
        };
    }

    public static TabData CloneTab(TabData source)
    {
        return new TabData
        {
            Key = source.Key,
            Name = source.Name,
            GroupId = source.GroupId,
            Color = source.Color,
            Font = CloneFont(source.Font),
        };
    }

    public static TabGroupData CloneGroup(TabGroupData source)
    {
        return new TabGroupData
        {
            Id = source.Id,
            Name = source.Name,
            Color = source.Color,
            RepoPath = source.RepoPath,
            Font = CloneFont(source.Font),
        };
    }

    public static TabFontData CloneFont(TabFontData? source)
    {
        if (source is null) return new TabFontData();
        return new TabFontData
        {
            Family = source.Family,
            Size = source.Size,
            Weight = source.Weight,
            Style = source.Style,
            Color = source.Color,
        };
    }

    public static LocalGitData CloneLocalGit(LocalGitData? source)
    {
        if (source is null) return new LocalGitData();
        return new LocalGitData
        {
            CurrentRepoPath = source.CurrentRepoPath,
            SavedRepos = source.SavedRepos.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    // ----- JSON -------------------------------------------------------------

    private static SessionData? ParseSessionFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return ParseSession(doc.RootElement);
        }
        catch { return null; }
    }

    private static SessionData? ParseSession(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;

        var data = new SessionData();
        if (TryGet(root, "Version", out var version) && version.ValueKind == JsonValueKind.Number)
            data.Version = version.GetInt32();

        if (TryGet(root, "Tabs", out var tabs) && tabs.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in tabs.EnumerateArray())
            {
                var tab = ParseTab(item);
                if (!string.IsNullOrWhiteSpace(tab.Key)) data.Tabs.Add(tab);
            }
        }

        if (TryGet(root, "Groups", out var groups) && groups.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in groups.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var group = ParseGroup(item);
                if (!string.IsNullOrWhiteSpace(group.Id)) data.Groups.Add(group);
            }
        }

        if (TryGet(root, "Active", out var active) && active.ValueKind == JsonValueKind.Number)
            data.Active = active.GetInt32();

        if (TryGet(root, "LocalGit", out var localGit) && localGit.ValueKind == JsonValueKind.Object)
            data.LocalGit = ParseLocalGit(localGit);

        Normalize(data);
        return data.Tabs.Count == 0 ? null : data;
    }

    private static TabData ParseTab(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.String)
            return new TabData { Key = item.GetString() ?? "dashboard" };

        var tab = new TabData();
        if (item.ValueKind != JsonValueKind.Object) return tab;

        tab.Key = ReadString(item, "Key", "dashboard");
        tab.Name = ReadString(item, "Name", string.Empty);
        tab.GroupId = ReadString(item, "GroupId", string.Empty);
        tab.Color = ReadString(item, "Color", string.Empty);
        if (TryGet(item, "Font", out var font) && font.ValueKind == JsonValueKind.Object)
            tab.Font = ParseFont(font);
        return tab;
    }

    private static TabGroupData ParseGroup(JsonElement item)
    {
        var group = new TabGroupData
        {
            Id = ReadString(item, "Id", string.Empty),
            Name = ReadString(item, "Name", string.Empty),
            Color = ReadString(item, "Color", string.Empty),
            RepoPath = ReadString(item, "RepoPath", string.Empty),
        };
        if (TryGet(item, "Font", out var font) && font.ValueKind == JsonValueKind.Object)
            group.Font = ParseFont(font);
        return group;
    }

    private static TabFontData ParseFont(JsonElement item)
    {
        return new TabFontData
        {
            Family = ReadString(item, "Family", string.Empty),
            Size = ReadDouble(item, "Size", 0),
            Weight = ReadString(item, "Weight", string.Empty),
            Style = ReadString(item, "Style", string.Empty),
            Color = ReadString(item, "Color", string.Empty),
        };
    }

    private static LocalGitData ParseLocalGit(JsonElement item)
    {
        var data = new LocalGitData
        {
            CurrentRepoPath = ReadString(item, "CurrentRepoPath", string.Empty),
        };
        if (TryGet(item, "SavedRepos", out var repos) && repos.ValueKind == JsonValueKind.Array)
        {
            foreach (var repo in repos.EnumerateArray())
            {
                var s = repo.GetString();
                if (!string.IsNullOrWhiteSpace(s)) data.SavedRepos.Add(s);
            }
        }
        return data;
    }

    private static string ToJson(SessionData source)
    {
        var data = CloneSession(source);
        Normalize(data);

        var sb = new StringBuilder();
        sb.Append("{\n");
        AppendNumber(sb, "  ", "Version", 2, comma: true);
        AppendNumber(sb, "  ", "Active", data.Active, comma: true);
        AppendTabs(sb, "  ", data.Tabs, comma: true);
        AppendGroups(sb, "  ", data.Groups, comma: true);
        AppendLocalGit(sb, "  ", data.LocalGit, comma: false);
        sb.Append("}\n");
        return sb.ToString();
    }

    private static string ToGroupJson(GroupExportData source)
    {
        var data = new GroupExportData
        {
            Version = 1,
            Group = CloneGroup(source.Group),
            Tabs = source.Tabs.Select(CloneTab).ToList(),
            LocalGit = CloneLocalGit(source.LocalGit),
        };
        NormalizeGroupExport(data);

        var sb = new StringBuilder();
        sb.Append("{\n");
        AppendNumber(sb, "  ", "Version", 1, comma: true);
        AppendString(sb, "  ", "Kind", "WinForgeTabGroup", comma: true);
        sb.Append("  \"Group\": ");
        AppendGroup(sb, data.Group, "  ");
        sb.Append(",\n");
        AppendTabs(sb, "  ", data.Tabs, comma: true);
        AppendLocalGit(sb, "  ", data.LocalGit, comma: false);
        sb.Append("}\n");
        return sb.ToString();
    }

    private static void AppendTabs(StringBuilder sb, string indent, List<TabData> tabs, bool comma)
    {
        sb.Append(indent).Append("\"Tabs\": [\n");
        for (int i = 0; i < tabs.Count; i++)
        {
            sb.Append(indent).Append("  ");
            AppendTab(sb, tabs[i], indent + "  ");
            if (i < tabs.Count - 1) sb.Append(',');
            sb.Append('\n');
        }
        sb.Append(indent).Append(']');
        if (comma) sb.Append(',');
        sb.Append('\n');
    }

    private static void AppendGroups(StringBuilder sb, string indent, List<TabGroupData> groups, bool comma)
    {
        sb.Append(indent).Append("\"Groups\": [\n");
        for (int i = 0; i < groups.Count; i++)
        {
            sb.Append(indent).Append("  ");
            AppendGroup(sb, groups[i], indent + "  ");
            if (i < groups.Count - 1) sb.Append(',');
            sb.Append('\n');
        }
        sb.Append(indent).Append(']');
        if (comma) sb.Append(',');
        sb.Append('\n');
    }

    private static void AppendTab(StringBuilder sb, TabData tab, string indent)
    {
        sb.Append("{\n");
        AppendString(sb, indent + "  ", "Key", tab.Key, comma: true);
        AppendString(sb, indent + "  ", "Name", tab.Name, comma: true);
        AppendString(sb, indent + "  ", "GroupId", tab.GroupId, comma: true);
        AppendString(sb, indent + "  ", "Color", tab.Color, comma: true);
        AppendFont(sb, indent + "  ", tab.Font, comma: false);
        sb.Append(indent).Append('}');
    }

    private static void AppendGroup(StringBuilder sb, TabGroupData group, string indent)
    {
        sb.Append("{\n");
        AppendString(sb, indent + "  ", "Id", group.Id, comma: true);
        AppendString(sb, indent + "  ", "Name", group.Name, comma: true);
        AppendString(sb, indent + "  ", "Color", group.Color, comma: true);
        AppendString(sb, indent + "  ", "RepoPath", group.RepoPath, comma: true);
        AppendFont(sb, indent + "  ", group.Font, comma: false);
        sb.Append(indent).Append('}');
    }

    private static void AppendFont(StringBuilder sb, string indent, TabFontData font, bool comma)
    {
        sb.Append(indent).Append("\"Font\": {\n");
        AppendString(sb, indent + "  ", "Family", font.Family, comma: true);
        AppendNumber(sb, indent + "  ", "Size", font.Size, comma: true);
        AppendString(sb, indent + "  ", "Weight", font.Weight, comma: true);
        AppendString(sb, indent + "  ", "Style", font.Style, comma: true);
        AppendString(sb, indent + "  ", "Color", font.Color, comma: false);
        sb.Append(indent).Append('}');
        if (comma) sb.Append(',');
        sb.Append('\n');
    }

    private static void AppendLocalGit(StringBuilder sb, string indent, LocalGitData localGit, bool comma)
    {
        sb.Append(indent).Append("\"LocalGit\": {\n");
        AppendString(sb, indent + "  ", "CurrentRepoPath", localGit.CurrentRepoPath, comma: true);
        sb.Append(indent).Append("  \"SavedRepos\": [");
        for (int i = 0; i < localGit.SavedRepos.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('"').Append(Escape(localGit.SavedRepos[i])).Append('"');
        }
        sb.Append("]\n");
        sb.Append(indent).Append('}');
        if (comma) sb.Append(',');
        sb.Append('\n');
    }

    private static void AppendString(StringBuilder sb, string indent, string name, string? value, bool comma)
    {
        sb.Append(indent).Append('"').Append(name).Append("\": \"").Append(Escape(value ?? string.Empty)).Append('"');
        if (comma) sb.Append(',');
        sb.Append('\n');
    }

    private static void AppendNumber(StringBuilder sb, string indent, string name, int value, bool comma)
    {
        sb.Append(indent).Append('"').Append(name).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
        if (comma) sb.Append(',');
        sb.Append('\n');
    }

    private static void AppendNumber(StringBuilder sb, string indent, string name, double value, bool comma)
    {
        sb.Append(indent).Append('"').Append(name).Append("\": ").Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        if (comma) sb.Append(',');
        sb.Append('\n');
    }

    private static string Escape(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

    private static void EnsureParentFolder(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
    }

    // ----- normalization ----------------------------------------------------

    private static void Normalize(SessionData data)
    {
        data.Version = 2;
        data.Tabs = data.Tabs.Where(t => t is not null).Select(CloneTab).ToList();
        data.Groups = data.Groups.Where(g => g is not null).Select(CloneGroup).ToList();
        data.LocalGit = CloneLocalGit(data.LocalGit);

        foreach (var tab in data.Tabs)
        {
            tab.Key = string.IsNullOrWhiteSpace(tab.Key) ? "dashboard" : tab.Key.Trim();
            tab.Name = tab.Name?.Trim() ?? string.Empty;
            tab.GroupId = tab.GroupId?.Trim() ?? string.Empty;
            tab.Color = tab.Color?.Trim() ?? string.Empty;
            tab.Font ??= new TabFontData();
        }

        var seenGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in data.Groups)
        {
            group.Id = string.IsNullOrWhiteSpace(group.Id) ? Guid.NewGuid().ToString("N") : group.Id.Trim();
            while (!seenGroups.Add(group.Id)) group.Id = Guid.NewGuid().ToString("N");
            group.Name = string.IsNullOrWhiteSpace(group.Name) ? "Tab group" : group.Name.Trim();
            group.Color = group.Color?.Trim() ?? string.Empty;
            group.RepoPath = group.RepoPath?.Trim() ?? string.Empty;
            group.Font ??= new TabFontData();
        }

        var groupIds = data.Groups.Select(g => g.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var tab in data.Tabs)
            if (!string.IsNullOrWhiteSpace(tab.GroupId) && !groupIds.Contains(tab.GroupId))
                tab.GroupId = string.Empty;

        if (data.Active < 0 || data.Active >= data.Tabs.Count) data.Active = 0;
    }

    private static void NormalizeGroupExport(GroupExportData data)
    {
        data.Version = 1;
        data.Group = CloneGroup(data.Group);
        data.Group.Id = string.IsNullOrWhiteSpace(data.Group.Id) ? Guid.NewGuid().ToString("N") : data.Group.Id.Trim();
        data.Group.Name = string.IsNullOrWhiteSpace(data.Group.Name) ? "Imported group" : data.Group.Name.Trim();
        data.Group.Font ??= new TabFontData();
        data.Tabs = data.Tabs.Where(t => t is not null).Select(CloneTab).ToList();
        foreach (var tab in data.Tabs)
        {
            tab.Key = string.IsNullOrWhiteSpace(tab.Key) ? "dashboard" : tab.Key.Trim();
            tab.GroupId = data.Group.Id;
            tab.Font ??= new TabFontData();
        }
        data.LocalGit = CloneLocalGit(data.LocalGit);
    }

    // ----- DOM helpers ------------------------------------------------------

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static string ReadString(JsonElement element, string name, string fallback)
    {
        if (TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? fallback;
        return fallback;
    }

    private static double ReadDouble(JsonElement element, string name, double fallback)
    {
        if (TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d))
            return d;
        return fallback;
    }

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // ----- local git mirror -------------------------------------------------

    private static void ScheduleCommit(string msg) => _ = Task.Run(() => Commit(msg));

    private static void EnsureRepo()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            if (!Directory.Exists(Path.Combine(Dir, ".git")))
                Git("init", "-q");

            var ignore = Path.Combine(Dir, ".gitignore");
            if (!File.Exists(ignore))
                File.WriteAllText(ignore, "*.tmp\n*.bak\n");

            var readme = Path.Combine(Dir, "README.md");
            if (!File.Exists(readme))
                File.WriteAllText(readme, "# WinForge tab session\n\nThis local repository stores WinForge tab, group, style, and repo-state history.\n");

            Git("config", "user.name", "WinForge");
            Git("config", "user.email", "session@winforge.local");
        }
        catch { }
    }

    private static void Commit(string msg)
    {
        try
        {
            lock (CommitGate)
            {
                EnsureRepo();
                Git("add", "-A");
                var status = Git("status", "--porcelain");
                if (string.IsNullOrWhiteSpace(status)) return;
                Git("commit", "-q", "-m", msg.Replace("\"", "'"));
            }
        }
        catch { }
    }

    private static string Git(params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("git")
            {
                WorkingDirectory = Dir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);
            using var p = Process.Start(psi);
            if (p is null) return string.Empty;
            if (!p.WaitForExit(8000))
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                return string.Empty;
            }
            return (p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd()).Trim();
        }
        catch { return string.Empty; }
    }
}
