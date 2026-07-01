using System;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// JSON 查詢工具 · JSON Query — paste JSON, run a JSONPath-lite query ($.a.b, $.arr[0], $.arr[*],
/// wildcards, recursive ..key), or list every leaf path / flatten the document. Pure managed
/// (System.Text.Json). Never throws; all feedback goes to a status line. Bilingual (粵語).
/// </summary>
public sealed partial class JsonPathModule : Page
{
    private const string SampleJson =
        "{\n  \"name\": \"WinForge\",\n  \"version\": 11,\n  \"tags\": [\"winui\", \"reactor\", \"tools\"],\n  \"authors\": [\n    { \"id\": 1, \"name\": \"Ada\" },\n    { \"id\": 2, \"name\": \"Alan\" }\n  ]\n}";

    public JsonPathModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? s, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "JSON Query · JSON 查詢";
        HeaderBlurb.Text = P("Paste JSON, then pull values out with a JSONPath-lite query — or list every leaf path and flatten the whole document. Everything runs locally.",
            "貼上 JSON，再用簡易 JSONPath 抽出值 — 又或者列出全部葉子路徑同攤平成扁平結構。全部喺本機執行。");
        JsonLabel.Text = P("JSON", "JSON");
        QueryLabel.Text = P("Query", "查詢式");
        RunBtn.Content = P("Run", "執行");
        LeafBtn.Content = P("List all leaf paths", "列出所有葉子路徑");
        FlattenBtn.Content = P("Flatten", "攤平");
        CopyBtn.Content = P("Copy results", "複製結果");
        Hint.Text = P("Supports $.a.b, $.arr[0], $.arr[*], .* wildcards and ..key recursive descent. The leading $ is optional.",
            "支援 $.a.b、$.arr[0]、$.arr[*]、.* 萬用字元同 ..key 遞迴搜尋。開頭嘅 $ 可省略。");

        if (string.IsNullOrEmpty(JsonBox.Text)) JsonBox.Text = SampleJson;
        if (string.IsNullOrEmpty(QueryBox.Text)) QueryBox.Text = "$..name";

        SetStatus(P("Ready.", "準備就緒。"), false);
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var r = JsonPathService.Query(JsonBox.Text ?? "", QueryBox.Text ?? "");
            Bind(r,
                P($"{r.Matches.Count} match(es).", $"{r.Matches.Count} 個結果。"));
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Leaf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var r = JsonPathService.LeafPaths(JsonBox.Text ?? "");
            Bind(r,
                P($"{r.Matches.Count} leaf path(s).", $"{r.Matches.Count} 條葉子路徑。"));
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Flatten_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var r = JsonPathService.Flatten(JsonBox.Text ?? "");
            Bind(r,
                P($"Flattened into {r.Matches.Count} entries.", $"攤平成 {r.Matches.Count} 條記錄。"));
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Bind(JsonPathService.QueryResult r, string okMsg)
    {
        if (!r.Ok)
        {
            ResultsList.ItemsSource = null;
            ShowError(r.Error);
            return;
        }
        ResultsList.ItemsSource = r.Matches;
        if (r.Matches.Count == 0)
            SetStatus(P("No matches.", "冇符合嘅結果。"), false);
        else
            SetStatus(okMsg, false);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ResultsList.ItemsSource is not System.Collections.Generic.IEnumerable<JsonPathService.Match> items)
            {
                SetStatus(P("Nothing to copy yet.", "暫時冇嘢可以複製。"), false);
                return;
            }
            var sb = new StringBuilder();
            int n = 0;
            foreach (var m in items) { sb.Append(m.Path).Append(" = ").AppendLine(m.Value); n++; }
            if (n == 0) { SetStatus(P("Nothing to copy yet.", "暫時冇嘢可以複製。"), false); return; }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(sb.ToString());
            Clipboard.SetContent(dp);
            SetStatus(P($"Copied {n} line(s) to the clipboard.", $"已複製 {n} 行到剪貼簿。"), false);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void ShowError(string? code)
    {
        string kind = code ?? "";
        string detail = "";
        int colon = kind.IndexOf(':');
        if (colon >= 0) { detail = kind.Substring(colon + 1); kind = kind.Substring(0, colon); }

        string msg = kind switch
        {
            "json" => P("Invalid JSON — check for missing quotes, commas or braces.", "JSON 無效 — 檢查下有冇漏引號、逗號或者括號。"),
            "query" => P("Invalid query — expected something like $.a.b, $.arr[0] or ..key.", "查詢式無效 — 應該似 $.a.b、$.arr[0] 或者 ..key。"),
            _ => P("Could not evaluate the query.", "無法評估查詢式。"),
        };
        if (!string.IsNullOrWhiteSpace(detail)) msg += "  (" + detail.Trim() + ")";
        SetStatus(msg, true);
    }

    private void Fail(Exception ex) => SetStatus(P("Something went wrong: ", "出咗啲問題：") + ex.Message, true);

    private void SetStatus(string text, bool error)
    {
        StatusText.Text = text;
        StatusText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
            error ? "SystemFillColorCriticalBrush" : "TextFillColorSecondaryBrush"];
    }
}
