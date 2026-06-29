using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內批次檔案操作 · In-app bulk file operations — match by wildcard/regex/extension, then
/// copy/move/recycle/flatten/organise. Pure C#, no redirect. Bilingual.
/// </summary>
public sealed partial class BulkOpsModule : Page
{
    private string _source = "";
    private string _target = "";
    private List<string> _matches = new();
    private bool _busy;

    public BulkOpsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) =>
        {
            Render();
            Recompute();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Bulk File Ops · 批次檔案操作";
        HeaderBlurb.Text = P("Match files by wildcard, regex or extension, preview them, then copy, move, recycle, flatten or organise by type.",
            "用萬用字元、正則或副檔名比對檔案，預覽之後可以複製、移動、放入回收筒、攤平或者按類型整理。");
        SrcCap.Text = P("Source", "來源");
        TgtCap.Text = P("Target", "目標");
        SrcBtn.Content = P("Browse…", "瀏覽…");
        TgtBtn.Content = P("Browse…", "瀏覽…");
        PatternBox.PlaceholderText = P("Pattern (e.g. *.jpg or ^IMG.*)…", "樣式（例如 *.jpg 或 ^IMG.*）…");
        RecurseCheck.Content = P("Subfolders", "包括子資料夾");
        CopyBtn.Content = P("Copy → target", "複製 → 目標");
        MoveBtn.Content = P("Move → target", "移動 → 目標");
        RecycleBtn.Content = P("Recycle", "放入回收筒");
        FlattenBtn.Content = P("Flatten", "攤平");
        OrganizeBtn.Content = P("Organise by type", "按類型整理");

        var sel = ModeBox.SelectedIndex < 0 ? 0 : ModeBox.SelectedIndex;
        ModeBox.Items.Clear();
        ModeBox.Items.Add(new ComboBoxItem { Content = P("Wildcard", "萬用字元"), Tag = MatchMode.Wildcard });
        ModeBox.Items.Add(new ComboBoxItem { Content = P("Regex", "正則"), Tag = MatchMode.Regex });
        ModeBox.Items.Add(new ComboBoxItem { Content = P("Extension", "副檔名"), Tag = MatchMode.Extension });
        ModeBox.SelectedIndex = sel;
    }

    private MatchMode Mode => (ModeBox.SelectedItem as ComboBoxItem)?.Tag is MatchMode m ? m : MatchMode.Wildcard;

    private void Recompute()
    {
        if (string.IsNullOrEmpty(_source))
        {
            _matches = new();
            List.ItemsSource = new[] { P("Choose a source folder to preview matching files.", "揀來源資料夾後會預覽符合嘅檔案。") };
            CountText.Text = P("No source selected", "未揀來源");
            return;
        }
        _matches = BulkFileOps.Match(_source, PatternBox.Text ?? "", Mode, RecurseCheck.IsChecked == true);
        List.ItemsSource = _matches;
        CountText.Text = P($"{_matches.Count} matched", $"配對到 {_matches.Count} 個");
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) => Recompute();
    private void Mode_Changed(object sender, SelectionChangedEventArgs e) => Recompute();
    private void Recurse_Changed(object sender, RoutedEventArgs e) => Recompute();

    private async void BrowseSrc_Click(object sender, RoutedEventArgs e)
    {
        var f = await PickFolder();
        if (f is not null) { _source = f; SourceBox.Text = f; Recompute(); }
    }

    private async void BrowseTgt_Click(object sender, RoutedEventArgs e)
    {
        var f = await PickFolder();
        if (f is not null) { _target = f; TargetBox.Text = f; }
    }

    private static Task<string?> PickFolder() => FileDialogs.OpenFolderAsync();

    private void Done(int ok, int fail, string verb)
    {
        ResultBar.Severity = fail == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        ResultBar.Title = P("Done", "完成");
        ResultBar.Message = P($"{verb}: {ok} ok, {fail} failed.", $"{verb}：成功 {ok}，失敗 {fail}。");
        ResultBar.IsOpen = true;
        Recompute();
    }

    private async Task<bool> Confirm(string verb)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Are you sure?", "確定嗎？"),
            Content = P($"{verb} {_matches.Count} file(s)?", $"{verb} {_matches.Count} 個檔案？"),
            PrimaryButtonText = P("Proceed", "繼續"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _matches.Count == 0) return;
        if (string.IsNullOrEmpty(_target)) { Warn(P("Pick a target folder first.", "請先揀目標資料夾。")); return; }
        _busy = true;
        var (ok, fail) = BulkFileOps.Copy(_matches, _target);
        _busy = false;
        Done(ok, fail, P("Copied", "已複製"));
    }

    private async void Move_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _matches.Count == 0) return;
        if (string.IsNullOrEmpty(_target)) { Warn(P("Pick a target folder first.", "請先揀目標資料夾。")); return; }
        if (!await Confirm(P("Move", "移動"))) return;
        _busy = true;
        var (ok, fail) = BulkFileOps.Move(_matches, _target);
        _busy = false;
        Done(ok, fail, P("Moved", "已移動"));
    }

    private async void Recycle_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _matches.Count == 0) return;
        if (!await Confirm(P("Recycle", "放入回收筒"))) return;
        _busy = true;
        var (ok, fail) = BulkFileOps.Recycle(_matches);
        _busy = false;
        Done(ok, fail, P("Recycled", "已放入回收筒"));
    }

    private async void Flatten_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _matches.Count == 0) return;
        if (!await Confirm(P("Flatten into source root", "攤平到來源根目錄"))) return;
        _busy = true;
        var (ok, fail) = BulkFileOps.Flatten(_source, _matches);
        _busy = false;
        Done(ok, fail, P("Flattened", "已攤平"));
    }

    private async void Organize_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _matches.Count == 0) return;
        if (!await Confirm(P("Organise by extension", "按副檔名整理"))) return;
        _busy = true;
        var (ok, fail) = BulkFileOps.OrganizeByExtension(_source, _matches);
        _busy = false;
        Done(ok, fail, P("Organised", "已整理"));
    }

    private void Warn(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Title = P("Heads up", "注意");
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
