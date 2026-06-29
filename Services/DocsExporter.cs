using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WinForge.Catalog;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 為每一個功能寫一個 Markdown 檔 · Writes one Markdown file per feature.
/// 透過 `WinForge.exe --export-docs &lt;dir&gt;` 觸發（無視窗，寫完即退出）。
/// Triggered by `WinForge.exe --export-docs &lt;dir&gt;` (headless; exits when done).
/// </summary>
public static class DocsExporter
{
    private static readonly UTF8Encoding Utf8 = new(false);

    public static int Export(string dir)
    {
        Directory.CreateDirectory(dir);

        var all = new List<(TweakDefinition t, string module)>();
        foreach (var t in TweakCatalog.All)
            all.Add((t, $"{t.Category.Name.En} · {t.Category.Name.Zh}"));
        foreach (var t in GitOperations.All())
            all.Add((t, "Git & GitHub · Git 與 GitHub"));
        foreach (var t in ArchiveOperations.All())
            all.Add((t, "Archives · 壓縮檔"));
        foreach (var t in MediaOperations.All())
            all.Add((t, "Media · 媒體"));

        foreach (var (t, module) in all)
        {
            var sub = Path.Combine(dir, Folder(module));
            Directory.CreateDirectory(sub);
            File.WriteAllText(Path.Combine(sub, Sanitize(t.Id) + ".md"), BuildMd(t, module), Utf8);
        }

        WriteIndex(dir, all);
        return all.Count;
    }

    /// <summary>
    /// 由真實 app 目錄匯出網站資料 · Export the documentation site's data as JSON straight from the
    /// LIVE app catalogs — real module list (<see cref="ModuleRegistry"/>), real categories +
    /// feature counts (<see cref="Categories"/> / <see cref="TweakCatalog"/>) — so the GitHub Pages
    /// site shows the app's actual capabilities instead of hand-written/stale numbers.
    /// Writes {meta, categories, modules}; the wiki content is merged in by the publish step.
    /// </summary>
    public static void ExportSiteData(string path)
    {
        var categories = Categories.All.Select(cat => new
        {
            en = cat.Name.En,
            zh = cat.Name.Zh,
            count = TweakCatalog.CountFor(cat),
            features = TweakCatalog.ByCategory(cat).Select(t => new
            {
                en = t.Title.En,
                zh = t.Title.Zh,
                id = t.Id,
                folder = Folder($"{cat.Name.En} · {cat.Name.Zh}"),
            }).ToList(),
        }).ToList();

        var modules = ModuleRegistry.All.Select(m => new
        {
            en = m.En,
            zh = m.Zh,
            page = m.Tag.StartsWith("module.", StringComparison.Ordinal) ? m.Tag["module.".Length..] : m.Tag,
            tag = m.Tag,
            status = "✅",
        }).ToList();

        var payload = new
        {
            meta = new
            {
                totalFeatures = TweakCatalog.Count,
                categoryCount = Categories.All.Length,
                moduleCount = ModuleRegistry.All.Count,
                generatedUtc = DateTime.UtcNow.ToString("o"),
            },
            categories,
            modules,
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, json, Utf8);
    }

    /// <summary>Folder slug from a "English · 粵語" module label, e.g. "Git &amp; GitHub · …" -&gt; "git-github".</summary>
    private static string Folder(string module)
    {
        var en = module.Split('·')[0].Trim().ToLowerInvariant();
        var chars = en.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        slug = slug.Trim('-');
        return slug.Length == 0 ? "misc" : slug;
    }

    private static string BuildMd(TweakDefinition t, string module)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {t.Title.En} · {t.Title.Zh}");
        sb.AppendLine();
        sb.AppendLine("| Field · 欄位 | Value · 值 |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| **ID** | `{t.Id}` |");
        sb.AppendLine($"| **Module · 模組** | {module} |");
        sb.AppendLine($"| **Type · 種類** | {t.Kind} |");
        sb.AppendLine($"| **Administrator · 管理員** | {(t.RequiresAdmin ? "Yes · 需要" : "No · 唔使")} |");
        sb.AppendLine($"| **Destructive · 具破壞性** | {(t.Destructive ? "Yes · 係" : "No · 唔係")} |");
        sb.AppendLine($"| **Restart · 重啟** | {t.Restart} |");
        if (t.ActionLabel is not null)
            sb.AppendLine($"| **Action · 動作** | {t.ActionLabel.En} · {t.ActionLabel.Zh} |");
        sb.AppendLine();
        sb.AppendLine("## English");
        sb.AppendLine(t.Description.En);
        sb.AppendLine();
        sb.AppendLine("## 粵語");
        sb.AppendLine(t.Description.Zh);
        if (t.Keywords.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"_Keywords · 關鍵字: {string.Join(", ", t.Keywords)}_");
        }
        sb.AppendLine();
        sb.AppendLine("_Part of WinForge · WinForge 套件嘅一部分_");
        return sb.ToString();
    }

    private static void WriteIndex(string dir, List<(TweakDefinition t, string module)> all)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# WinForge — Feature Reference · 功能總覽");
        sb.AppendLine();
        sb.AppendLine($"**{all.Count}** features, each with its own page. · **{all.Count}** 項功能，每項一頁。");
        sb.AppendLine();
        foreach (var grp in all.GroupBy(x => x.module).OrderBy(g => g.Key))
        {
            sb.AppendLine($"## {grp.Key} ({grp.Count()})");
            var folder = Folder(grp.Key);
            foreach (var (t, _) in grp.OrderBy(x => x.t.Id))
                sb.AppendLine($"- [{t.Title.En} · {t.Title.Zh}]({folder}/{Sanitize(t.Id)}.md)");
            sb.AppendLine();
        }
        File.WriteAllText(Path.Combine(dir, "README.md"), sb.ToString(), Utf8);
    }

    private static string Sanitize(string id)
    {
        var chars = id.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '-').ToArray();
        return new string(chars);
    }
}
