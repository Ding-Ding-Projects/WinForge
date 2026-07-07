using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// JSON 鍵排序 / 正規化 · JSON key sorter / normaliser. Paste JSON → recursively sort every
/// object's keys (A→Z or Z→A, optional case-insensitive), optionally sort primitive arrays,
/// then pretty-print (2/4/tab indent) or minify. Pure managed <see cref="JsonSortService"/>.
/// Robust: bad input never throws — a bilingual error shows in the InfoBar. Bilingual (粵語).
/// </summary>
public sealed partial class JsonSortModule : Page
{
    public JsonSortModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = P("JSON Key Sort", "JSON 鍵排序");
        HeaderBlurb.Text = P(
            "Paste JSON and get it back with every object's keys sorted and the whole thing re-formatted — a canonical, diff-friendly order. Nothing leaves this PC.",
            "貼入 JSON，幫你將每個物件嘅鍵排好序、成段重新排版 — 標準又易 diff 嘅次序。啲嘢唔會離開呢部電腦。");

        InputLabel.Text = P("Input JSON", "輸入 JSON");
        OptionsTitle.Text = P("Options", "選項");

        OrderLabel.Text = P("Key order", "鍵次序");
        OrderAsc.Content = P("A → Z (ascending)", "A → Z（升序）");
        OrderDesc.Content = P("Z → A (descending)", "Z → A（降序）");

        CaseChk.Content = P("Case-insensitive sort", "唔分大細楷排序");

        MinifyTitle.Text = P("Minify", "壓縮");
        MinifyHint.Text = P("One line, no whitespace (overrides indent).", "一行、冇空白（會蓋過縮排）。");

        IndentLabel.Text = P("Indent", "縮排");
        Indent2.Content = P("2 spaces", "2 個空格");
        Indent4.Content = P("4 spaces", "4 個空格");
        IndentTab.Content = P("Tab", "定位字元 Tab");

        SortArraysChk.Content = P("Also sort array elements (primitives only)", "連陣列元素都排（淨係基本值）");

        SortBtn.Content = P("Sort & format", "排序並排版");
        CopyBtn.Content = P("Copy output", "複製結果");
        OutputLabel.Text = P("Output", "輸出");

        UpdateIndentEnabled();
    }

    private void Minify_Toggled(object sender, RoutedEventArgs e) => UpdateIndentEnabled();

    private void UpdateIndentEnabled()
    {
        if (IndentPanel != null)
            IndentPanel.Opacity = (MinifySwitch?.IsOn == true) ? 0.4 : 1.0;
        if (IndentCombo != null)
            IndentCombo.IsEnabled = !(MinifySwitch?.IsOn == true);
    }

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var opts = new JsonSortService.JsonSortOptions
            {
                Descending = OrderCombo?.SelectedIndex == 1,
                CaseInsensitive = CaseChk?.IsChecked == true,
                Minify = MinifySwitch?.IsOn == true,
                SortArrays = SortArraysChk?.IsChecked == true,
                Indent = IndentCombo?.SelectedIndex switch
                {
                    1 => JsonSortService.IndentKind.FourSpaces,
                    2 => JsonSortService.IndentKind.Tab,
                    _ => JsonSortService.IndentKind.TwoSpaces,
                },
            };

            var result = JsonSortService.Sort(InputBox?.Text, opts);

            if (!result.Ok)
            {
                OutputBox.Text = string.Empty;
                CopyBtn.IsEnabled = false;
                Info.Severity = InfoBarSeverity.Error;
                Info.Title = P("Couldn't sort", "排唔到");
                Info.Message = P(result.ErrorEn ?? "Invalid JSON.", result.ErrorZh ?? "JSON 格式錯誤。");
                Info.IsOpen = true;
                return;
            }

            OutputBox.Text = result.Output;
            CopyBtn.IsEnabled = result.Output.Length > 0;

            if (result.HadDuplicateKeys)
            {
                Info.Severity = InfoBarSeverity.Warning;
                Info.Title = P("Sorted — with a note", "已排好 — 但有提示");
                Info.Message = P(
                    "The input had duplicate keys in one or more objects; only the last value for each was kept.",
                    "輸入有物件出現重複嘅鍵；每個只保留最後嗰個值。");
            }
            else
            {
                Info.Severity = InfoBarSeverity.Success;
                Info.Title = P("Done", "完成");
                Info.Message = P("Keys sorted and JSON re-formatted.", "鍵已排好、JSON 已重新排版。");
            }
            Info.IsOpen = true;
        }
        catch (Exception ex)
        {
            OutputBox.Text = string.Empty;
            CopyBtn.IsEnabled = false;
            Info.Severity = InfoBarSeverity.Error;
            Info.Title = P("Something went wrong", "出咗啲問題");
            Info.Message = P(ex.Message, ex.Message);
            Info.IsOpen = true;
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = OutputBox?.Text ?? string.Empty;
            if (text.Length == 0) return;
            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            Info.Severity = InfoBarSeverity.Success;
            Info.Title = P("Copied", "已複製");
            Info.Message = P("Output copied to the clipboard.", "結果已複製到剪貼簿。");
            Info.IsOpen = true;
        }
        catch (Exception ex)
        {
            Info.Severity = InfoBarSeverity.Error;
            Info.Title = P("Copy failed", "複製失敗");
            Info.Message = P(ex.Message, ex.Message);
            Info.IsOpen = true;
        }
    }
}
