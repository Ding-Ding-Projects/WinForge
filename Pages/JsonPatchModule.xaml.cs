using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// JSON Patch（RFC 6902）· JSON 修補 — diff two JSON docs into a standard RFC 6902 patch array, or apply
/// a patch array to a document. Pure managed (System.Text.Json.Nodes). Bilingual, never throws. No redirect.
/// </summary>
public sealed partial class JsonPatchModule : Page
{
    private bool _ready;

    public JsonPatchModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ready = true;
        Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private bool IsApply => ModeBox?.SelectedIndex == 1;

    private void Render()
    {
        Header.Title = "JSON Patch · JSON 修補 (RFC 6902)";
        HeaderBlurb.Text = P("Produce or apply a standard RFC 6902 JSON Patch — the machine-readable array of add/remove/replace operations, not a human diff. Everything runs locally.",
            "產生或者套用標準 RFC 6902 JSON Patch — 即係機器讀嘅 add / remove / replace 運算陣列，而唔係俾人睇嘅 diff。全部喺本機運行。");

        ModeLabel.Text = P("Mode", "模式");
        ModeDiffItem.Content = P("Diff — source + target → patch", "比較 — 來源 + 目標 → 修補");
        ModeApplyItem.Content = P("Apply — document + patch → result", "套用 — 文件 + 修補 → 結果");

        RunBtn.Content = IsApply ? P("Apply patch", "套用修補") : P("Generate patch", "產生修補");
        CopyBtn.Content = P("Copy output", "複製輸出");
        OutLabel.Text = P("Output", "輸出");

        if (IsApply)
        {
            LeftLabel.Text = P("Document (JSON to patch)", "文件（要修補嘅 JSON）");
            RightLabel.Text = P("Patch (RFC 6902 array)", "修補（RFC 6902 陣列）");
        }
        else
        {
            LeftLabel.Text = P("Source JSON", "來源 JSON");
            RightLabel.Text = P("Target JSON", "目標 JSON");
        }

        if (StatusText.Text.Length == 0)
            StatusText.Text = P("Paste JSON above, then run.", "喺上面貼入 JSON，跟住執行。");
    }

    private void Mode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        StatusText.Text = "";
        Render();
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        JsonPatchService.Result r;
        try
        {
            r = IsApply
                ? JsonPatchService.Apply(LeftBox.Text ?? "", RightBox.Text ?? "", P)
                : JsonPatchService.Diff(LeftBox.Text ?? "", RightBox.Text ?? "", P);
        }
        catch (Exception ex)
        {
            OutBox.Text = "";
            StatusText.Text = P("Unexpected error: ", "非預期錯誤：") + ex.Message;
            return;
        }

        if (r.Ok)
        {
            OutBox.Text = r.Output;
            StatusText.Text = IsApply
                ? P("Patch applied. Result below.", "已套用修補，結果喺下面。")
                : P("Patch generated. Copy it below.", "已產生修補，喺下面複製。");
        }
        else
        {
            OutBox.Text = "";
            StatusText.Text = r.Error ?? P("Something went wrong.", "出咗啲問題。");
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutBox.Text ?? "";
            if (text.Length == 0)
            {
                StatusText.Text = P("Nothing to copy yet.", "暫時冇嘢可以複製。");
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Output copied to the clipboard.", "已將輸出複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Could not copy: ", "無法複製：") + ex.Message;
        }
    }
}
