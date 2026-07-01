using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// JSON Pointer (RFC 6901) · 指標 — paste a JSON document, type a pointer (e.g. /a/b/0), and see the
/// resolved value (pretty), or a clear bilingual "invalid JSON" / "bad pointer" / "not found" message.
/// Also walks the document to list every valid pointer; click a row to copy its pointer. Never throws.
/// </summary>
public sealed partial class JsonPointerModule : Page
{
    private readonly ObservableCollection<JsonPointerService.PointerEntry> _pointers = new();
    private string _lastValue = string.Empty;
    private bool _hasValue;

    public JsonPointerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); PointerList.ItemsSource = _pointers; Resolve(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "JSON Pointer · 指標";
        HeaderBlurb.Text = P("Resolve an RFC 6901 JSON Pointer against a document. Use \"/a/b/0\" for nested members and array indices; \"~1\" means \"/\" and \"~0\" means \"~\"; an empty pointer returns the whole document.",
            "用 RFC 6901 JSON 指標喺文件度定位。\"/a/b/0\" 可以查巢狀成員同陣列索引；\"~1\" 代表 \"/\"，\"~0\" 代表 \"~\"；留空就係成份文件。");
        DocLabel.Text = P("JSON document", "JSON 文件");
        PointerLabel.Text = P("Pointer (RFC 6901)", "指標（RFC 6901）");
        SampleBtn.Content = P("Load sample", "載入範例");
        ListBtn.Content = P("List all pointers", "列出所有指標");
        CopyValueBtn.Content = P("Copy value", "複製值");
        ResultTitle.Text = P("Resolved value", "定位結果");
        ListTitle.Text = P("All pointers", "所有指標");
        ListHint.Text = P("Every valid pointer in the document. Click a row to copy its pointer.", "文件入面每個有效指標。撳一行就複製個指標。");
        Resolve();
    }

    private void Sample_Click(object sender, RoutedEventArgs e)
    {
        DocBox.Text = "{\n  \"name\": \"WinForge\",\n  \"tags\": [\"a\", \"b\", \"c\"],\n  \"nested\": { \"x\": 1, \"y\": [true, null, 3.5] },\n  \"a/b\": \"slash key\",\n  \"m~n\": \"tilde key\"\n}";
        if (string.IsNullOrWhiteSpace(PointerBox.Text)) PointerBox.Text = "/nested/y/2";
        Resolve();
    }

    private void Pointer_Changed(object sender, TextChangedEventArgs e) => Resolve();

    private void Resolve()
    {
        if (ResultBox == null) return; // not loaded yet
        var res = JsonPointerService.Resolve(DocBox?.Text, PointerBox?.Text);

        if (res.InvalidJson)
        {
            SetResult(false, InfoBarSeverity.Error,
                P("Invalid JSON", "JSON 格式錯誤"),
                P("The document above is not valid JSON.", "上面份文件唔係有效嘅 JSON。"),
                res.Detail, "", "");
            return;
        }
        if (res.BadPointer)
        {
            SetResult(false, InfoBarSeverity.Warning,
                P("Invalid pointer", "指標無效"),
                P("The pointer is not a valid RFC 6901 JSON Pointer.", "呢個指標唔係有效嘅 RFC 6901 JSON 指標。"),
                res.Detail, "", "");
            return;
        }
        if (res.NotFound)
        {
            SetResult(false, InfoBarSeverity.Warning,
                P("Not found", "搵唔到"),
                P("No value exists at that pointer.", "呢個指標度冇對應嘅值。"),
                res.Detail, "", "");
            return;
        }

        string typeLabel = P($"Type: {res.ValueType}", $"型別：{res.ValueType}");
        SetResult(true, InfoBarSeverity.Success,
            P("Resolved", "已定位"),
            P("Pointer resolved successfully.", "指標成功定位。"),
            null, res.Pretty ?? "", typeLabel);
    }

    private void SetResult(bool ok, InfoBarSeverity sev, string title, string msg, string? detail, string value, string typeLine)
    {
        try
        {
            ResultBar.Severity = sev;
            ResultBar.Title = title;
            ResultBar.Message = string.IsNullOrEmpty(detail) ? msg : (msg + "  (" + detail + ")");
            ResultBar.IsOpen = true;
            ResultType.Text = typeLine;
            _hasValue = ok;
            _lastValue = ok ? value : string.Empty;
            ResultBox.Text = ok ? value : string.Empty;
            CopyValueBtn.IsEnabled = ok;
        }
        catch { /* never throw from UI update */ }
    }

    private void CopyValue_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasValue || string.IsNullOrEmpty(_lastValue)) return;
        CopyToClipboard(_lastValue);
    }

    private void List_Click(object sender, RoutedEventArgs e)
    {
        var res = JsonPointerService.ListAllPointers(DocBox?.Text);
        _pointers.Clear();
        if (res.InvalidJson)
        {
            ListCard.Visibility = Visibility.Visible;
            ListHint.Text = P("Invalid JSON — nothing to list.", "JSON 格式錯誤 — 冇嘢好列。");
            return;
        }
        foreach (var entry in res.Entries)
        {
            // Show the empty pointer as a readable label.
            if (entry.Pointer.Length == 0) entry.Pointer = "";
            _pointers.Add(entry);
        }
        ListHint.Text = P($"{_pointers.Count} pointer(s). Click a row to copy its pointer.", $"共 {_pointers.Count} 個指標。撳一行就複製個指標。");
        ListCard.Visibility = Visibility.Visible;
    }

    private void PointerList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not JsonPointerService.PointerEntry entry) return;
        CopyToClipboard(entry.Pointer);
        // Also drop it into the pointer box so the user sees it resolve.
        if (PointerBox != null) { PointerBox.Text = entry.Pointer; }
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            var pkg = new DataPackage();
            pkg.SetText(text ?? string.Empty);
            Clipboard.SetContent(pkg);
        }
        catch { /* clipboard can transiently fail; never throw */ }
    }
}
