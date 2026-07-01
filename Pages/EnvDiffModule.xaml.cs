using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 環境變數快照同差異 · Environment snapshot &amp; diff. Capture Process/User/Machine environment variables
/// into named, timestamped JSON snapshots; list/delete them; diff two snapshots (or one vs the live
/// environment) into Added / Removed / Changed groups; export a snapshot or the diff as plain text to
/// the clipboard. Pure managed; all IO off the UI thread and guarded — a corrupt file never crashes.
/// </summary>
public sealed partial class EnvDiffModule : Page
{
    /// <summary>Row shown in the saved-snapshot ListView.</summary>
    public sealed class SnapRow
    {
        public string Name { get; set; } = "";
        public string Target { get; set; } = "";
        public string LocalTime { get; set; } = "";
        public int Count { get; set; }
        public string FileName { get; set; } = "";
        public EnvDiffService.Snapshot Model { get; set; } = new();
    }

    /// <summary>Entry in the diff pickers — either a saved snapshot or the special "live environment".</summary>
    public sealed class PickItem
    {
        public string Label { get; set; } = "";
        public bool IsLive { get; set; }
        public EnvDiffService.Snapshot? Snapshot { get; set; }
    }

    private readonly ObservableCollection<SnapRow> _rows = new();
    private readonly List<EnvDiffService.Snapshot> _snapshots = new();
    private EnvDiffService.DiffResult? _lastDiff;
    private string _lastOldLabel = "", _lastNewLabel = "";

    public EnvDiffModule()
    {
        InitializeComponent();
        SnapshotsList.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLang;
        Unloaded += OnUnloaded;
        Loaded += (_, _) => { Render(); LoadSnapshots(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLang(object? sender, EventArgs e) => Render();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Unloaded -= OnUnloaded;
    }

    private void Render()
    {
        Header.Title = P("Env Snapshot & Diff · 環境變數快照", "環境變數快照同差異 · Env Snapshot & Diff");
        HeaderBlurb.Text = P("Capture Process, User or Machine environment variables into named, timestamped snapshots, then diff two of them (or one against the live environment) to see exactly what changed.",
            "將 Process、User 或者 Machine 嘅環境變數影低做有名有時間嘅快照，再攞兩個嚟比較（或者其中一個同而家嘅實時環境比），睇清楚邊啲變咗。");
        CaptureTitle.Text = P("Capture a snapshot", "影一個快照");
        TargetLabel.Text = P("Target", "目標");
        NameBox.PlaceholderText = P("optional name", "自訂名（可留空）");
        CaptureBtn.Content = P("Capture snapshot", "影快照");
        SavedTitle.Text = P("Saved snapshots", "已儲存嘅快照");
        EmptyText.Text = P("No snapshots yet — capture one above.", "仲未有快照 — 喺上面影一個。");
        DiffTitle.Text = P("Compare", "比較");
        OldLabel.Text = P("Old", "舊");
        NewLabel.Text = P("New", "新");
        DiffBtn.Content = P("Diff", "比較");
        AddedHdr.Text = P("Added", "新增");
        RemovedHdr.Text = P("Removed", "移除");
        ChangedHdr.Text = P("Changed", "改動");
        ToolTipService.SetToolTip(DiffExportBtn, P("Copy diff to clipboard", "複製差異到剪貼簿"));
        RebuildPickers();
    }

    private EnvironmentVariableTarget SelectedTarget()
    {
        int i = TargetBox.SelectedIndex;
        return i switch
        {
            1 => EnvironmentVariableTarget.User,
            2 => EnvironmentVariableTarget.Machine,
            _ => EnvironmentVariableTarget.Process,
        };
    }

    private async void LoadSnapshots()
    {
        try
        {
            var list = await EnvDiffService.LoadAllAsync();
            _snapshots.Clear();
            _snapshots.AddRange(list);
            _rows.Clear();
            foreach (var s in list)
                _rows.Add(new SnapRow
                {
                    Name = s.Name,
                    Target = s.Target,
                    LocalTime = s.CapturedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    Count = s.Vars.Count,
                    FileName = s.FileName,
                    Model = s,
                });
            EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RebuildPickers();
        }
        catch (Exception ex)
        {
            SetStatus(P("Could not load snapshots: ", "載入快照失敗：") + ex.Message);
        }
    }

    private void RebuildPickers()
    {
        var items = new List<PickItem>
        {
            new() { Label = P("[ live environment ]", "［ 實時環境 ］"), IsLive = true },
        };
        foreach (var s in _snapshots)
            items.Add(new PickItem
            {
                Label = $"{s.Name} · {s.Target} · {s.CapturedUtc.ToLocalTime():MM-dd HH:mm}",
                Snapshot = s,
            });

        int oldIdx = OldBox.SelectedIndex, newIdx = NewBox.SelectedIndex;
        OldBox.ItemsSource = items;
        NewBox.ItemsSource = items.ToList();
        OldBox.SelectedIndex = oldIdx >= 0 && oldIdx < items.Count ? oldIdx : 0;
        NewBox.SelectedIndex = newIdx >= 0 && newIdx < items.Count ? newIdx : 0;
    }

    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CaptureBtn.IsEnabled = false;
            var target = SelectedTarget();
            string name = NameBox.Text ?? "";
            var snap = await EnvDiffService.CaptureAsync(name, target);
            NameBox.Text = "";
            SetStatus(P($"Captured '{snap.Name}' ({snap.Vars.Count} vars, {snap.Target}).",
                $"已影低「{snap.Name}」（{snap.Vars.Count} 個變數，{snap.Target}）。"));
            LoadSnapshots();
        }
        catch (Exception ex)
        {
            SetStatus(P("Capture failed: ", "影快照失敗：") + ex.Message);
        }
        finally { CaptureBtn.IsEnabled = true; }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button b || b.Tag is not string file) return;
            bool ok = await EnvDiffService.DeleteAsync(file);
            SetStatus(ok ? P("Snapshot deleted.", "已刪除快照。") : P("Nothing deleted.", "冇嘢刪除到。"));
            LoadSnapshots();
        }
        catch (Exception ex)
        {
            SetStatus(P("Delete failed: ", "刪除失敗：") + ex.Message);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button b || b.Tag is not string file) return;
            var snap = _snapshots.FirstOrDefault(s => s.FileName == file);
            if (snap == null) { SetStatus(P("Snapshot not found.", "搵唔到快照。")); return; }
            CopyToClipboard(EnvDiffService.ToPlainText(snap));
            SetStatus(P($"Copied '{snap.Name}' to clipboard.", $"已複製「{snap.Name}」到剪貼簿。"));
        }
        catch (Exception ex)
        {
            SetStatus(P("Export failed: ", "匯出失敗：") + ex.Message);
        }
    }

    private async void Diff_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (OldBox.SelectedItem is not PickItem oldItem || NewBox.SelectedItem is not PickItem newItem)
            { SetStatus(P("Pick two items to compare.", "揀兩樣嘢嚟比較。")); return; }

            var oldVars = await ResolveVars(oldItem);
            var newVars = await ResolveVars(newItem);
            _lastOldLabel = oldItem.Label;
            _lastNewLabel = newItem.Label;

            var diff = EnvDiffService.Diff(oldVars, newVars);
            _lastDiff = diff;

            AddedList.ItemsSource = diff.Added;
            RemovedList.ItemsSource = diff.Removed;
            ChangedList.ItemsSource = diff.Changed;
            AddedHdr.Text = P("Added", "新增") + $" ({diff.Added.Count})";
            RemovedHdr.Text = P("Removed", "移除") + $" ({diff.Removed.Count})";
            ChangedHdr.Text = P("Changed", "改動") + $" ({diff.Changed.Count})";
            SetStatus(P($"Diff: +{diff.Added.Count} / -{diff.Removed.Count} / ~{diff.Changed.Count}.",
                $"差異：+{diff.Added.Count} / -{diff.Removed.Count} / ~{diff.Changed.Count}。"));
        }
        catch (Exception ex)
        {
            SetStatus(P("Diff failed: ", "比較失敗：") + ex.Message);
        }
    }

    private void DiffExport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_lastDiff == null) { SetStatus(P("Run a diff first.", "先做一次比較。")); return; }
            CopyToClipboard(EnvDiffService.ToPlainText(_lastDiff, _lastOldLabel, _lastNewLabel));
            SetStatus(P("Diff copied to clipboard.", "已複製差異到剪貼簿。"));
        }
        catch (Exception ex)
        {
            SetStatus(P("Export failed: ", "匯出失敗：") + ex.Message);
        }
    }

    private async System.Threading.Tasks.Task<Dictionary<string, string>> ResolveVars(PickItem item)
    {
        if (item.IsLive)
        {
            var target = SelectedTarget();
            return await System.Threading.Tasks.Task.Run(() => EnvDiffService.ReadLive(target));
        }
        return item.Snapshot?.Vars ?? new();
    }

    private static void CopyToClipboard(string text)
    {
        var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dp.SetText(text ?? "");
        Clipboard.SetContent(dp);
    }

    private void SetStatus(string text) => StatusText.Text = text;
}
