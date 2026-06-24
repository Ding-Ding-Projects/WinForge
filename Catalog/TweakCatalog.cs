using System.Collections.Generic;
using System.Linq;
using WinForge.Models;

namespace WinForge.Catalog;

/// <summary>
/// 全部調校項目嘅總目錄 · The master catalog aggregating every category's tweaks.
/// 每個分類各自貢獻一批項目，喺度登記分類資訊。
/// Each category file contributes its tweaks; this stamps the category onto each.
/// </summary>
public static class TweakCatalog
{
    private static List<TweakDefinition>? _all;

    public static IReadOnlyList<TweakDefinition> All => _all ??= Build();

    private static List<TweakDefinition> Build()
    {
        var list = new List<TweakDefinition>();
        Add(list, Categories.Appearance, AppearanceTweaks.All());
        Add(list, Categories.Explorer, ExplorerTweaks.All());
        Add(list, Categories.Taskbar, TaskbarTweaks.All());
        Add(list, Categories.Privacy, PrivacyTweaks.All());
        Add(list, Categories.Performance, PerformanceTweaks.All());
        Add(list, Categories.Network, NetworkTweaks.All());
        Add(list, Categories.Cleanup, CleanupTweaks.All());
        Add(list, Categories.Security, SecurityTweaks.All());
        Add(list, Categories.System, SystemTweaks.All());
        Add(list, Categories.Apps, AppsTweaks.All());
        Add(list, Categories.PowerTools, PowerToolsTweaks.All());
        Add(list, Categories.Launcher, LauncherTweaks.All());
        Add(list, Categories.Maintenance, MaintenanceTweaks.All());
        Add(list, Categories.Win11Pro, Win11ProTweaks.All());
        Add(list, Categories.Annoyances, AnnoyanceTweaks.All());
        Add(list, Categories.Winaero, WinaeroTweaks.All());
        Add(list, Categories.Info, InfoTweaks.All());
        Add(list, Categories.RecipesCat, Recipes.All());
        Add(list, Categories.DevTerminal, DevTerminalTweaks.All());
        Add(list, Categories.Browser, BrowserTweaks.All());
        Add(list, Categories.Vault, VaultTweaks.All());
        Add(list, Categories.NetPro, NetProTweaks.All());
        return list;
    }

    private static void Add(List<TweakDefinition> list, AppCategory category, IEnumerable<TweakDefinition> tweaks)
    {
        foreach (var t in tweaks)
        {
            t.Category = category;
            list.Add(t);
        }
    }

    public static IEnumerable<TweakDefinition> ByCategory(AppCategory category)
        => All.Where(t => t.Category == category);

    public static int CountFor(AppCategory category) => ByCategory(category).Count();

    public static int Count => All.Count;

    /// <summary>跨語言搜尋 · Search across both languages and keywords.</summary>
    public static IEnumerable<TweakDefinition> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return All;
        var q = query.Trim().ToLowerInvariant();
        return All.Where(t => t.SearchHaystack.Contains(q));
    }
}
