using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// JSON Merge Patch (RFC 7386) · JSON 合併修補（RFC 7386）.
/// Generate a merge patch from a source→target diff, or apply a merge patch to a document.
/// The simple "shallow-merge, null-deletes" patch — contrast with RFC 6902 JSON Patch (op arrays).
/// Pure managed System.Text.Json. Bilingual, never-throws, status via InfoBar.
/// </summary>
public sealed partial class JsonMergePatchModule : Page
{
    private bool _ready;

    public JsonMergePatchModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { _ready = true; Render(); };
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private bool IsGenerate => ModeBox.SelectedIndex != 1; // 0 = Generate (default), 1 = Apply

    private void Render()
    {
        Header.Title = "JSON Merge Patch · JSON 合併修補";
        HeaderBlurb.Text = P(
            "Build or apply an RFC 7386 JSON Merge Patch — a plain JSON object that shallow-merges into a document, where a null value deletes a key.",
            "產生或套用 RFC 7386 JSON 合併修補 — 一個普通 JSON 物件，淺層合併入文件，而 null 就代表刪除嗰個鍵。");

        ModeLabel.Text = P("Mode", "模式");
        int keep = ModeBox.SelectedIndex < 0 ? 0 : ModeBox.SelectedIndex;
        ModeBox.Items.Clear();
        ModeBox.Items.Add(P("Generate patch (source → target)", "產生修補（來源 → 目標）"));
        ModeBox.Items.Add(P("Apply patch (document + patch)", "套用修補（文件 + 修補）"));
        ModeBox.SelectedIndex = keep;

        NoteBlock.Text = P(
            "RFC 7386 is the simple \"shallow-merge, null-deletes\" patch: objects merge recursively, a null deletes that key, and any array or scalar replaces the whole value. Unlike RFC 6902 JSON Patch (an explicit array of add/remove/replace/move ops), a merge patch cannot delete array elements or target array indexes.",
            "RFC 7386 係最簡單嗰種「淺層合併、null 刪除」修補：物件會遞迴合併，null 會刪除嗰個鍵，而任何陣列或者純量都係成個取代。同 RFC 6902 JSON Patch（一個明確嘅 add/remove/replace/move 操作陣列）唔同，合併修補冇辦法刪除陣列元素或者針對陣列索引。");

        UpdateModeLabels();

        RunButton.Content = IsGenerate ? P("Generate patch", "產生修補") : P("Apply patch", "套用修補");
        SampleButton.Content = P("Load sample", "載入範例");
        CopyButton.Content = P("Copy result", "複製結果");
        OutLabel.Text = P("Result", "結果");
    }

    private void UpdateModeLabels()
    {
        if (IsGenerate)
        {
            LeftLabel.Text = P("Source JSON", "來源 JSON");
            RightLabel.Text = P("Target JSON", "目標 JSON");
        }
        else
        {
            LeftLabel.Text = P("Document JSON", "文件 JSON");
            RightLabel.Text = P("Merge patch JSON", "合併修補 JSON");
        }
    }

    private void Mode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        UpdateModeLabels();
        RunButton.Content = IsGenerate ? P("Generate patch", "產生修補") : P("Apply patch", "套用修補");
        Info.IsOpen = false;
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        var result = IsGenerate
            ? JsonMergePatchService.Generate(LeftBox.Text, RightBox.Text)
            : JsonMergePatchService.Apply(LeftBox.Text, RightBox.Text);

        if (result.Ok)
        {
            OutBox.Text = result.Json;
            CopyButton.IsEnabled = result.Json.Length > 0;
            Info.Severity = InfoBarSeverity.Success;
            Info.Message = IsGenerate
                ? P("Merge patch generated.", "已產生合併修補。")
                : P("Patch applied.", "已套用修補。");
            Info.IsOpen = true;
        }
        else
        {
            OutBox.Text = string.Empty;
            CopyButton.IsEnabled = false;
            Info.Severity = InfoBarSeverity.Error;
            Info.Message = result.Error;
            Info.IsOpen = true;
        }
    }

    private void Sample_Click(object sender, RoutedEventArgs e)
    {
        if (IsGenerate)
        {
            LeftBox.Text = "{\n  \"title\": \"Goodbye!\",\n  \"author\": { \"givenName\": \"John\", \"familyName\": \"Doe\" },\n  \"tags\": [ \"example\", \"sample\" ],\n  \"content\": \"This will be unchanged\"\n}";
            RightBox.Text = "{\n  \"title\": \"Hello!\",\n  \"author\": { \"givenName\": \"John\" },\n  \"tags\": [ \"example\" ],\n  \"content\": \"This will be unchanged\",\n  \"phoneNumber\": \"+01-123-456-7890\"\n}";
        }
        else
        {
            LeftBox.Text = "{\n  \"title\": \"Goodbye!\",\n  \"author\": { \"givenName\": \"John\", \"familyName\": \"Doe\" },\n  \"tags\": [ \"example\", \"sample\" ],\n  \"content\": \"This will be unchanged\"\n}";
            RightBox.Text = "{\n  \"title\": \"Hello!\",\n  \"author\": { \"familyName\": null },\n  \"phoneNumber\": \"+01-123-456-7890\",\n  \"tags\": [ \"example\" ]\n}";
        }
        Info.IsOpen = false;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(OutBox.Text)) return;
            var pkg = new DataPackage();
            pkg.SetText(OutBox.Text);
            Clipboard.SetContent(pkg);
            Info.Severity = InfoBarSeverity.Success;
            Info.Message = P("Copied to clipboard.", "已複製到剪貼簿。");
            Info.IsOpen = true;
        }
        catch (Exception ex)
        {
            Info.Severity = InfoBarSeverity.Error;
            Info.Message = P($"Copy failed — {ex.Message}", $"複製失敗 — {ex.Message}");
            Info.IsOpen = true;
        }
    }
}
