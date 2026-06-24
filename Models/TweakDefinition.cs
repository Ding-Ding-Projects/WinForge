using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Models;

/// <summary>
/// 一個調校項目嘅完整定義 · The full, self-contained definition of one tweak.
///
/// 採用「資料驅動」設計：每個項目都帶住自己嘅讀／寫行為（registry、shell 等），
/// UI 只負責顯示。Data-driven: each tweak carries its own read/write behaviour so the
/// UI is a thin renderer over the catalog.
/// </summary>
public sealed class TweakDefinition
{
    public required string Id { get; init; }
    public required LocalizedText Title { get; init; }
    public required LocalizedText Description { get; init; }
    public required TweakKind Kind { get; init; }

    /// <summary>由目錄登記時填上 · Assigned by the catalog when the tweak is registered.</summary>
    public AppCategory Category { get; set; } = default!;

    /// <summary>需要管理員權限 · Requires elevation (HKLM, services, powercfg, etc.).</summary>
    public bool RequiresAdmin { get; init; }

    /// <summary>具破壞性／不可逆，UI 會要求確認 · Destructive/irreversible; UI confirms first.</summary>
    public bool Destructive { get; init; }

    /// <summary>套用後生效所需嘅重啟 · Restart needed for the change to apply.</summary>
    public RestartScope Restart { get; init; } = RestartScope.None;

    /// <summary>搜尋關鍵字 · Extra search keywords (both languages welcome).</summary>
    public string[] Keywords { get; init; } = Array.Empty<string>();

    // ---- Toggle behaviour ----
    public Func<bool>? GetIsOn { get; init; }
    public Action<bool>? SetIsOn { get; init; }

    // ---- Action behaviour ----
    public LocalizedText? ActionLabel { get; init; }
    public Func<CancellationToken, Task<TweakResult>>? RunAsync { get; init; }

    /// <summary>動作輸出係 CSV 表格，UI 用網格顯示 · Action output is CSV; the UI renders it as a sortable grid.</summary>
    public bool TabularOutput { get; init; }

    // ---- Choice behaviour ----
    public IReadOnlyList<TweakChoice>? Choices { get; init; }
    public Func<string?>? GetCurrentChoice { get; init; }
    public Action<string>? SetChoice { get; init; }

    // ---- Info behaviour ----
    public Func<string>? GetInfo { get; init; }

    /// <summary>用嚟做搜尋比對嘅合併文字 · Concatenated haystack used for search.</summary>
    public string SearchHaystack =>
        $"{Title.En} {Title.Zh} {Description.En} {Description.Zh} {string.Join(' ', Keywords)}".ToLowerInvariant();
}
