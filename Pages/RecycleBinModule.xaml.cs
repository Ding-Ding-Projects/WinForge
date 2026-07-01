using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 回收筒管理 · Recycle Bin manager — reports total item count and size across all drives
/// (SHQueryRecycleBin) and empties the bin silently (SHEmptyRecycleBin) after a bilingual confirm.
/// Pure managed P/Invoke; every shell call is guarded and surfaced as status — never throws. Bilingual.
/// </summary>
public sealed partial class RecycleBinModule : Page
{
    public RecycleBinModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Recycle Bin · 回收筒管理";
        HeaderBlurb.Text = P("See how much space your Recycle Bin is using across every drive, then empty it in one click. Emptying is permanent — the files can't be restored.",
            "睇下你所有磁碟嘅回收筒用咗幾多空間，一撳就清晒。清咗係永久刪除，還原唔返㗎。");
        RefreshButton.Content = P("Refresh", "重新整理");
        EmptyButton.Content = P("Empty Recycle Bin", "清空回收筒");
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        var info = RecycleBinService.Query();
        if (!info.Ok)
        {
            SizeTitle.Text = "—";
            ItemsText.Text = string.Empty;
            EmptyButton.IsEnabled = false;
            StatusText.Text = P("Couldn't read the Recycle Bin right now. Try Refresh again.",
                "而家讀唔到回收筒嘅資料，撳多次「重新整理」試下。");
            return;
        }

        SizeTitle.Text = RecycleBinService.FormatBytes(info.Bytes);
        if (info.Items <= 0)
        {
            ItemsText.Text = P("Nothing in the bin", "回收筒係空嘅");
            EmptyButton.IsEnabled = false;
            StatusText.Text = P("Your Recycle Bin is already empty — nothing to clean up.",
                "你嘅回收筒已經空咗，冇嘢好清。");
        }
        else
        {
            ItemsText.Text = info.Items == 1
                ? P("1 item across all drives", "所有磁碟合共 1 個項目")
                : P($"{info.Items:N0} items across all drives", $"所有磁碟合共 {info.Items:N0} 個項目");
            EmptyButton.IsEnabled = true;
            StatusText.Text = P("Ready. Emptying permanently deletes these files.",
                "準備好喇。清空會永久刪除呢啲檔案。");
        }
    }

    private async void Empty_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = P("Empty the Recycle Bin?", "確定清空回收筒？"),
                Content = P("This permanently deletes everything in the Recycle Bin on all drives. This can't be undone.",
                    "呢個動作會永久刪除所有磁碟回收筒入面嘅嘢，還原唔返㗎。"),
                PrimaryButtonText = P("Empty", "清空"),
                CloseButtonText = P("Cancel", "取消"),
                DefaultButton = ContentDialogButton.Close
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var outcome = RecycleBinService.Empty();
            if (!outcome.Ok)
            {
                StatusText.Text = P("Couldn't empty the Recycle Bin. Some files may be in use.",
                    "清唔到回收筒，可能有啲檔案畀人用緊。");
            }
            else if (outcome.FreedItems <= 0 && outcome.FreedBytes <= 0)
            {
                StatusText.Text = P("The Recycle Bin was already empty.", "回收筒本身已經係空嘅。");
            }
            else
            {
                string freed = RecycleBinService.FormatBytes(outcome.FreedBytes);
                StatusText.Text = P($"Emptied — freed {freed} from {outcome.FreedItems:N0} item(s).",
                    $"清空咗 — 釋放咗 {freed}，共 {outcome.FreedItems:N0} 個項目。");
            }
        }
        catch
        {
            StatusText.Text = P("Something went wrong while emptying the Recycle Bin.",
                "清空回收筒嘅時候出咗啲問題。");
        }
        finally
        {
            Refresh();
        }
    }
}
