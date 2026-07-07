using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// HTML 實體編碼／解碼 · HTML entity encoder / decoder. Escape the HTML5 must-escape set (and,
/// optionally, all non-ASCII as numeric refs) or resolve named / numeric entities back to text.
/// Includes a click-to-copy reference list of common entities. Pure managed, never throws. Bilingual.
/// </summary>
public sealed partial class HtmlEntitiesModule : Page
{
    private bool _suppress;

    public HtmlEntitiesModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); Recompute(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) { Render(); Recompute(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private bool IsDecode => ModeBox?.SelectedIndex == 1;

    private void Render()
    {
        try
        {
            Header.Title = "HTML Entities · HTML 實體";
            HeaderBlurb.Text = P("Encode text into HTML entities (or escape every non-ASCII character), or decode named and numeric entities back into plain text. Everything runs locally.",
                "將文字編碼成 HTML 實體（或將所有非 ASCII 字元跳脫），又或者將具名同數字實體解碼返做純文字。全部喺本機運行。");

            ModeLabel.Text = P("Mode", "模式");
            ModeEncodeItem.Content = P("Encode → entities", "編碼 → 實體");
            ModeDecodeItem.Content = P("Decode → text", "解碼 → 文字");
            if (ModeBox.SelectedIndex < 0) { _suppress = true; ModeBox.SelectedIndex = 0; _suppress = false; }

            NonAsciiChk.Content = P("Also escape every non-ASCII character (as &#xHHHH;)", "連所有非 ASCII 字元都跳脫（變成 &#xHHHH;）");
            NonAsciiChk.Visibility = IsDecode ? Visibility.Collapsed : Visibility.Visible;

            InputLabel.Text = P("Input", "輸入");
            OutputLabel.Text = P("Output", "輸出");
            CopyBtn.Content = P("Copy output", "複製輸出");

            RefTitle.Text = P("Common entities", "常用實體");
            RefBlurb.Text = P("Click any row to copy the entity name to the clipboard.", "撳任何一行就會將實體名稱複製去剪貼簿。");

            if (RefList.ItemsSource == null)
                RefList.ItemsSource = HtmlEntitiesService.ReferenceList;
        }
        catch { }
    }

    private void Mode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        NonAsciiChk.Visibility = IsDecode ? Visibility.Collapsed : Visibility.Visible;
        Recompute();
    }

    private void Options_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        Recompute();
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Recompute();
    }

    private void Recompute()
    {
        try
        {
            string input = InputBox?.Text ?? "";
            string output = IsDecode
                ? HtmlEntitiesService.Decode(input)
                : HtmlEntitiesService.Encode(input, NonAsciiChk.IsChecked == true);

            if (OutputBox != null) OutputBox.Text = output;

            if (InputCount != null)
                InputCount.Text = P($"{HtmlEntitiesService.Length(input)} characters in",
                                     $"輸入 {HtmlEntitiesService.Length(input)} 個字元");
            if (OutputCount != null)
                OutputCount.Text = P($"{HtmlEntitiesService.Length(output)} characters out",
                                     $"輸出 {HtmlEntitiesService.Length(output)} 個字元");
        }
        catch { }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox?.Text ?? "";
            if (text.Length == 0)
            {
                ShowInfo(InfoBarSeverity.Informational, P("Nothing to copy yet.", "暫時冇嘢可以複製。"));
                return;
            }
            var dp = new DataPackage();
            dp.SetText(text);
            Clipboard.SetContent(dp);
            ShowInfo(InfoBarSeverity.Success, P("Output copied to clipboard.", "已將輸出複製去剪貼簿。"));
        }
        catch
        {
            ShowInfo(InfoBarSeverity.Error, P("Couldn't access the clipboard.", "無法存取剪貼簿。"));
        }
    }

    private void Ref_ItemClick(object sender, ItemClickEventArgs e)
    {
        try
        {
            if (e.ClickedItem is HtmlEntitiesService.EntityRef row)
            {
                var dp = new DataPackage();
                dp.SetText(row.Name);
                Clipboard.SetContent(dp);
                ShowInfo(InfoBarSeverity.Success, P($"Copied {row.Name} to clipboard.", $"已複製 {row.Name} 去剪貼簿。"));
            }
        }
        catch
        {
            ShowInfo(InfoBarSeverity.Error, P("Couldn't access the clipboard.", "無法存取剪貼簿。"));
        }
    }

    private void ShowInfo(InfoBarSeverity severity, string message)
    {
        try
        {
            Info.Severity = severity;
            Info.Message = message;
            Info.IsOpen = true;
        }
        catch { }
    }
}
