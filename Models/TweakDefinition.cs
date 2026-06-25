using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

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

    // ======================================================================
    //  Rich interactive members (foundation upgrade) · 進階互動成員（基礎升級）
    //  全部 nullable 兼可選，現有 call-site 唔使改 · all nullable & optional so existing call-sites compile unchanged.
    // ======================================================================

    // ---- Slider / Number shared range · 滑桿／數字共用範圍 ----
    /// <summary>數值下限 · Minimum numeric value (Slider/Number).</summary>
    public double Min { get; init; }
    /// <summary>數值上限 · Maximum numeric value (Slider/Number).</summary>
    public double Max { get; init; } = 100;
    /// <summary>每格步進 · Step increment (Slider/Number).</summary>
    public double Step { get; init; } = 1;
    /// <summary>單位後綴，例如 "ms"、"%"（可選）· Optional unit suffix shown after the value, e.g. "ms", "%".</summary>
    public LocalizedText? Unit { get; init; }
    /// <summary>讀取目前數值 · Reads the current numeric value (Slider/Number).</summary>
    public Func<double>? GetNumber { get; init; }
    /// <summary>寫入新數值 · Writes a new numeric value (Slider/Number).</summary>
    public Action<double>? SetNumber { get; init; }

    // ---- MultiCheck · 多重勾選 ----
    /// <summary>多重勾選子項清單 · The sub-options rendered as a column of CheckBoxes.</summary>
    public IReadOnlyList<TweakToggleItem>? CheckItems { get; init; }

    // ---- Color · 顏色 ----
    /// <summary>讀取顏色（#RRGGBB 十六進位）· Reads the colour as a #RRGGBB hex string.</summary>
    public Func<string>? GetHex { get; init; }
    /// <summary>寫入顏色（#RRGGBB 十六進位）· Writes the colour as a #RRGGBB hex string.</summary>
    public Action<string>? SetHex { get; init; }

    // ---- Date / Time · 日期／時間 ----
    /// <summary>讀取日期（null 表示未設定）· Reads the date (null when unset).</summary>
    public Func<DateTimeOffset?>? GetDate { get; init; }
    /// <summary>寫入日期 · Writes the date.</summary>
    public Action<DateTimeOffset?>? SetDate { get; init; }
    /// <summary>除咗日期亦顯示時間揀選器 · When true, also show a TimePicker beside the DatePicker.</summary>
    public bool IncludeTime { get; init; }

    // ---- Wizard · 精靈 ----
    /// <summary>精靈步驟 · The ordered steps the wizard walks the user through.</summary>
    public IReadOnlyList<WizardStep>? WizardSteps { get; init; }
    /// <summary>
    /// 精靈完成時嘅處理（收到每步收集到嘅值）· Runs when the wizard finishes,
    /// receiving the values collected from each step keyed by <see cref="WizardStep.Key"/>.
    /// </summary>
    public Func<IReadOnlyDictionary<string, string>, CancellationToken, Task<TweakResult>>? WizardFinish { get; init; }

    // ---- Rich display extras (any kind) · 任何種類都可用嘅顯示加料 ----
    /// <summary>
    /// 可選彩色狀態藥丸 · Optional coloured status pill shown on the card,
    /// returning bilingual text plus a colour bucket.
    /// </summary>
    public Func<(string textEn, string textZh, StatusColor color)>? ColoredStatus { get; init; }
    /// <summary>
    /// Action 執行時顯示進度條 · Show a ProgressBar while an Action runs.
    /// 提供 <see cref="ActionProgress"/> 為確定進度，否則為不確定。
    /// Provide <see cref="ActionProgress"/> for a determinate bar; otherwise it is indeterminate.
    /// </summary>
    public bool ShowProgressBar { get; init; }
    /// <summary>確定進度回呼（0–1）· Determinate progress callback returning 0–1; null ⇒ indeterminate.</summary>
    public Func<double>? ActionProgress { get; init; }

    // ======================================================================
    //  Rich visual hook (rich-tweakcards) · 豐富視覺鈎子
    //  令任何卡都可以喺控件旁邊／下面渲染一幅用程式碼畫出嚟嘅圖（色板、量錶、長條圖、走勢線等）。
    //  全部可選兼向後相容；現有 call-site 唔使改。
    //  Lets ANY card render a code-drawn graphic (swatch, gauge, bar chart, sparkline…) alongside its
    //  control. Fully optional and backward compatible — existing call-sites are unaffected.
    // ======================================================================

    /// <summary>
    /// 可選「視覺預覽」工廠：回傳一個用程式碼生成嘅 <see cref="FrameworkElement"/>（無外部圖片）。
    /// 卡片會喺一個橫跨全闊嘅預覽區渲染佢。null（預設）即係照舊唔顯示任何預覽。
    /// Optional "visual preview" factory returning a code-generated <see cref="FrameworkElement"/>
    /// (no external images). The card hosts it in a full-width preview pane. null (default) ⇒ no preview.
    /// </summary>
    public Func<TweakDefinition, FrameworkElement>? VisualBuilder { get; init; }

    /// <summary>
    /// 套用設定之後即時重建預覽（做到「活動預覽」效果）· Rebuild the preview after every apply so it tracks
    /// the live setting (e.g. an accent-colour swatch or a gauge updating as you drag). Default false:
    /// the visual is built once when the card loads.
    /// </summary>
    public bool VisualLiveUpdate { get; init; }

    /// <summary>用嚟做搜尋比對嘅合併文字 · Concatenated haystack used for search.</summary>
    public string SearchHaystack =>
        $"{Title.En} {Title.Zh} {Description.En} {Description.Zh} {string.Join(' ', Keywords)}".ToLowerInvariant();
}
