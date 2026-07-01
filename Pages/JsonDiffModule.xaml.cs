using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// JSON Diff · JSON 比對 — paste two JSON documents (A / B); WinForge parses both with
/// System.Text.Json and recursively lists every difference (added / removed / changed /
/// type-changed) by JSON path, colour-coded, with counts. Optional "ignore array order"
/// compares arrays as multisets. Live compare on edit; never throws. Bilingual. No redirect.
/// </summary>
public sealed partial class JsonDiffModule : Page
{
    /// <summary>Row view-model with a per-status Foreground brush for the ListView.</summary>
    public sealed class Row
    {
        public string Path { get; init; } = "";
        public string Status { get; init; } = "";
        public string Detail { get; init; } = "";
        public Brush Brush { get; init; } = new SolidColorBrush();
    }

    private readonly ObservableCollection<Row> _rows = new();

    private static readonly Windows.UI.Color CAdded = Windows.UI.Color.FromArgb(0xFF, 0x3F, 0xB9, 0x50);
    private static readonly Windows.UI.Color CRemoved = Windows.UI.Color.FromArgb(0xFF, 0xE0, 0x48, 0x3B);
    private static readonly Windows.UI.Color CChanged = Windows.UI.Color.FromArgb(0xFF, 0xE8, 0x8B, 0x1A);

    public JsonDiffModule()
    {
        InitializeComponent();
        DiffList.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); Recompute(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) { Render(); Recompute(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "JSON Diff · JSON 比對";
        HeaderBlurb.Text = P("Paste two JSON documents and see every difference by path — what B added, removed or changed versus A. All local, nothing leaves your PC.",
            "貼兩份 JSON 落去，逐條路徑睇清楚有咩分別 — B 相對 A 多咗、少咗定改咗。全部喺本機處理，唔會傳出去。");
        LabelA.Text = P("A (original)", "A（原始）");
        LabelB.Text = P("B (compared)", "B（比對）");
        ToggleTitle.Text = P("Ignore array order", "唔理陣列次序");
        ToggleSub.Text = P("Compare arrays as multisets — order does not matter.", "陣列當作集合比對 — 次序唔緊要。");
        CopyButton.Content = P("Copy result", "複製結果");
        UpdateSummary(null);
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e) => Recompute();

    private void IgnoreOrder_Toggled(object sender, RoutedEventArgs e) => Recompute();

    private void Recompute()
    {
        _rows.Clear();
        JsonDiffService.DiffOutcome outcome;
        try
        {
            outcome = JsonDiffService.Compare(InputA?.Text, InputB?.Text, IgnoreOrderSwitch?.IsOn == true);
        }
        catch
        {
            UpdateSummary(null);
            return;
        }

        ShowError(ErrorA, outcome.ParsedA, outcome.ErrorA, side: "A");
        ShowError(ErrorB, outcome.ParsedB, outcome.ErrorB, side: "B");

        if (!outcome.Ok)
        {
            UpdateSummary(null);
            EmptyHint.Text = P("Fix the invalid JSON above to compare.", "修正上面唔正確嘅 JSON 先可以比對。");
            return;
        }

        foreach (var r in outcome.Rows)
        {
            _rows.Add(new Row
            {
                Path = r.Path,
                Status = r.Status,
                Detail = r.Detail,
                Brush = new SolidColorBrush(BrushFor(r.Kind)),
            });
        }

        UpdateSummary(outcome);
        EmptyHint.Text = _rows.Count == 0
            ? P("No differences — A and B are equivalent.", "冇分別 — A 同 B 一樣。")
            : "";
    }

    private Windows.UI.Color BrushFor(JsonDiffService.DiffKind kind) => kind switch
    {
        JsonDiffService.DiffKind.Added => CAdded,
        JsonDiffService.DiffKind.Removed => CRemoved,
        _ => CChanged,
    };

    private void ShowError(TextBlock target, bool parsed, string? message, string side)
    {
        if (parsed || string.IsNullOrWhiteSpace(InputForSide(side)))
        {
            target.Visibility = Visibility.Collapsed;
            target.Text = "";
            return;
        }
        target.Visibility = Visibility.Visible;
        target.Text = side == "A"
            ? P($"A is not valid JSON: {message}", $"A 唔係正確嘅 JSON：{message}")
            : P($"B is not valid JSON: {message}", $"B 唔係正確嘅 JSON：{message}");
    }

    private string InputForSide(string side) => (side == "A" ? InputA?.Text : InputB?.Text) ?? "";

    private void UpdateSummary(JsonDiffService.DiffOutcome? outcome)
    {
        if (outcome is null || !outcome.Ok)
        {
            SummaryText.Text = P("Waiting for two valid JSON documents…", "等緊兩份正確嘅 JSON…");
            return;
        }
        SummaryText.Text = P(
            $"+{outcome.Added} added · −{outcome.Removed} removed · ~{outcome.Changed} changed",
            $"+{outcome.Added} 新增 · −{outcome.Removed} 缺少 · ~{outcome.Changed} 改變");
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            foreach (var r in _rows)
                sb.AppendLine($"{r.Path}\t{r.Status}\t{r.Detail}");
            var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
            pkg.SetText(sb.Length == 0 ? P("(no differences)", "（冇分別）") : sb.ToString());
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
        }
        catch { /* never throw from clipboard */ }
    }
}
