using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace WinForge.Services;

/// <summary>一個 compose 服務 · One service from a docker-compose.yml.</summary>
public sealed class ComposeService
{
    public string Name { get; init; } = "";
    public string Image { get; init; } = "";
    public List<string> Ports { get; } = new();
    public Dictionary<string, string> Env { get; } = new();
    public List<string> Volumes { get; } = new();
    public List<string> DependsOn { get; } = new();
}

/// <summary>
/// 一個已解析嘅 compose 專案 · A parsed compose project (services + depends_on ordering).
/// Warnings 收集咗無法支援嘅欄位，畀 UI 顯示而唔係靜靜失敗。
/// Warnings collects fields we don't support so the UI can surface them rather than fail silently.
/// </summary>
public sealed class ComposeProject
{
    public string Name { get; init; } = "app";
    public List<ComposeService> Services { get; } = new();
    public List<string> Warnings { get; } = new();

    /// <summary>按 depends_on 做拓撲排序（有環就退回宣告次序）· Topological sort by depends_on.</summary>
    public List<ComposeService> OrderedServices()
    {
        var byName = Services.ToDictionary(s => s.Name, s => s, StringComparer.Ordinal);
        var ordered = new List<ComposeService>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var inStack = new HashSet<string>(StringComparer.Ordinal);

        void Visit(ComposeService s)
        {
            if (visited.Contains(s.Name)) return;
            if (!inStack.Add(s.Name)) return; // cycle guard
            foreach (var dep in s.DependsOn)
                if (byName.TryGetValue(dep, out var d)) Visit(d);
            inStack.Remove(s.Name);
            if (visited.Add(s.Name)) ordered.Add(s);
        }

        foreach (var s in Services) Visit(s);
        return ordered;
    }
}

/// <summary>
/// 純 C# 嘅 docker-compose.yml 解析器（用 managed YamlDotNet）· Managed C# compose parser.
/// 支援：services 嘅 image / ports / environment / volumes / depends_on。其餘欄位記入 Warnings。
/// Supports image / ports / environment / volumes / depends_on per service; everything else → Warnings.
/// 完全唔會 shell out 去 <c>docker compose</c>。 No shelling out to the compose CLI.
/// </summary>
public static class ComposeParser
{
    private static readonly HashSet<string> Supported = new(StringComparer.Ordinal)
        { "image", "ports", "environment", "volumes", "depends_on", "container_name" };

    public static ComposeProject Parse(string yamlText, string projectName)
    {
        var project = new ComposeProject { Name = SanitizeName(projectName) };

        var yaml = new YamlStream();
        using (var reader = new StringReader(yamlText)) yaml.Load(reader);
        if (yaml.Documents.Count == 0) return project;
        if (yaml.Documents[0].RootNode is not YamlMappingNode root)
            { project.Warnings.Add("Top-level YAML is not a mapping · 頂層 YAML 唔係對應結構"); return project; }

        if (!TryGet(root, "services", out var servicesNode) || servicesNode is not YamlMappingNode services)
            { project.Warnings.Add("No 'services:' block found · 搵唔到 services 區塊"); return project; }

        foreach (var entry in services.Children)
        {
            string svcName = Scalar(entry.Key);
            if (entry.Value is not YamlMappingNode svcMap) continue;

            string image = "";
            if (TryGet(svcMap, "image", out var imgN)) image = Scalar(imgN);

            if (string.IsNullOrWhiteSpace(image))
            {
                if (TryGet(svcMap, "build", out _))
                    project.Warnings.Add($"Service '{svcName}': 'build:' is not supported (managed API has no image builder) — provide a prebuilt 'image:'. · 服務「{svcName}」唔支援 build，請改用預建 image。");
                else
                    project.Warnings.Add($"Service '{svcName}': no image — skipped. · 服務「{svcName}」冇 image，已略過。");
                continue;
            }

            var svc = new ComposeService { Name = svcName, Image = image };

            if (TryGet(svcMap, "ports", out var portsN)) ParsePorts(portsN, svc, svcName, project);
            if (TryGet(svcMap, "environment", out var envN)) ParseEnv(envN, svc);
            if (TryGet(svcMap, "volumes", out var volN)) ParseVolumes(volN, svc);
            if (TryGet(svcMap, "depends_on", out var depN)) ParseDependsOn(depN, svc);

            // 記低未支援欄位。
            foreach (var k in svcMap.Children.Keys)
            {
                var key = Scalar(k);
                if (!Supported.Contains(key))
                    project.Warnings.Add($"Service '{svcName}': field '{key}' is not applied. · 服務「{svcName}」嘅「{key}」未套用。");
            }

            project.Services.Add(svc);
        }

        if (root.Children.Keys.Any(k => Scalar(k) == "volumes"))
            project.Warnings.Add("Top-level named 'volumes:' are created implicitly when first bound. · 頂層具名 volumes 會喺首次掛載時自動建立。");
        if (root.Children.Keys.Any(k => Scalar(k) == "networks"))
            project.Warnings.Add("Custom top-level 'networks:' are ignored; all services join one default bridge. · 自訂 networks 會被忽略，所有服務加入同一個 default bridge。");

        return project;
    }

    private static void ParsePorts(YamlNode node, ComposeService svc, string svcName, ComposeProject project)
    {
        if (node is YamlSequenceNode seq)
        {
            foreach (var item in seq)
            {
                if (item is YamlScalarNode sc) svc.Ports.Add(sc.Value ?? "");
                else if (item is YamlMappingNode m) // long syntax: {target, published}
                {
                    string target = TryGet(m, "target", out var t) ? Scalar(t) : "";
                    string published = TryGet(m, "published", out var p) ? Scalar(p) : "";
                    if (target.Length > 0)
                        svc.Ports.Add(published.Length > 0 ? $"{published}:{target}" : target);
                }
            }
        }
    }

    private static void ParseEnv(YamlNode node, ComposeService svc)
    {
        if (node is YamlMappingNode map) // environment: {KEY: val}
        {
            foreach (var kv in map.Children)
                svc.Env[Scalar(kv.Key)] = Scalar(kv.Value);
        }
        else if (node is YamlSequenceNode seq) // environment: ["KEY=val"]
        {
            foreach (var item in seq)
            {
                var s = Scalar(item);
                int eq = s.IndexOf('=');
                if (eq > 0) svc.Env[s[..eq]] = s[(eq + 1)..];
                else if (s.Length > 0) svc.Env[s] = "";
            }
        }
    }

    private static void ParseVolumes(YamlNode node, ComposeService svc)
    {
        if (node is YamlSequenceNode seq)
        {
            foreach (var item in seq)
            {
                if (item is YamlScalarNode sc && !string.IsNullOrWhiteSpace(sc.Value)) svc.Volumes.Add(sc.Value!);
                else if (item is YamlMappingNode m) // long syntax: {source, target}
                {
                    string source = TryGet(m, "source", out var s) ? Scalar(s) : "";
                    string target = TryGet(m, "target", out var t) ? Scalar(t) : "";
                    if (target.Length > 0) svc.Volumes.Add(source.Length > 0 ? $"{source}:{target}" : target);
                }
            }
        }
    }

    private static void ParseDependsOn(YamlNode node, ComposeService svc)
    {
        if (node is YamlSequenceNode seq)
            foreach (var item in seq) { var s = Scalar(item); if (s.Length > 0) svc.DependsOn.Add(s); }
        else if (node is YamlMappingNode map) // depends_on: {svc: {condition: ...}}
            foreach (var kv in map.Children) svc.DependsOn.Add(Scalar(kv.Key));
    }

    private static bool TryGet(YamlMappingNode map, string key, out YamlNode value)
    {
        foreach (var kv in map.Children)
            if (Scalar(kv.Key) == key) { value = kv.Value; return true; }
        value = null!;
        return false;
    }

    private static string Scalar(YamlNode node) => node is YamlScalarNode s ? s.Value ?? "" : "";

    private static string SanitizeName(string s)
    {
        var name = new string((s ?? "app").ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
        return string.IsNullOrEmpty(name) ? "app" : name;
    }
}
