using System;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 主機檔編輯器 · HOSTS file editor — SAFE and non-elevated. Loads / parses the system hosts file,
/// lets you enable/disable (toggles the leading '#'), add and remove entries, apply curated ad/tracker
/// block-list presets (→ 0.0.0.0), rebuild canonical hosts text, copy it, Save As, or write back to the
/// real system path (needs admin — on denial it just reports, never crashes). Bilingual (粵語 / English).
/// </summary>
public sealed partial class HostsEditModule : Page
{
    private readonly HostsEditService _svc = new();

    public HostsEditModule()
    {
        InitializeComponent();
        EntriesList.ItemsSource = _svc.Entries;
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Hosts File Editor · 主機檔編輯器";
        HeaderBlurb.Text = P("View and edit the Windows hosts file safely. Load it, add or block domains, toggle entries on/off, then copy, save a copy, or write it back (writing the system file needs administrator rights).",
            "安全咁睇同改 Windows hosts 檔。載入之後可以加或者封鎖網域、逐條開關，再複製、另存或者寫返去（寫系統檔要管理員權限）。");

        LoadTitle.Text = P("System hosts file", "系統 hosts 檔");
        LoadBtn.Content = P("Load system hosts", "載入系統 hosts");
        ClearBtn.Content = P("Clear all", "全部清除");
        PresetLabel.Text = P("Block-list preset", "封鎖清單預設");
        ApplyPresetBtn.Content = P("Add preset", "加入預設");

        AddTitle.Text = P("Add an entry", "加一條記錄");
        IpLabel.Text = P("IP address", "IP 位址");
        HostLabel.Text = P("Host name(s)", "主機名稱");
        CommentLabel.Text = P("Comment (optional)", "備註（可選）");
        AddBtn.Content = P("Add entry", "加入記錄");

        ListTitle.Text = P("Entries — toggle to enable/disable", "記錄 — 撳掣開關");
        RemoveBtn.Content = P("Remove selected", "移除所選");
        RebuildBtn.Content = P("Rebuild output", "重建輸出");

        OutputTitle.Text = P("Rebuilt hosts file", "重建嘅 hosts 檔");
        CopyBtn.Content = P("Copy output", "複製輸出");
        SaveAsBtn.Content = P("Save as…", "另存新檔…");
        WriteBackBtn.Content = P("Write back to system hosts", "寫返系統 hosts");

        BuildPresetBox();
        if (Status.Message.Length == 0)
            Show(P("Not loaded yet. Click “Load system hosts” to begin.", "未載入。撳「載入系統 hosts」開始。"), InfoBarSeverity.Informational);
    }

    private void BuildPresetBox()
    {
        int sel = PresetBox.SelectedIndex;
        PresetBox.Items.Clear();
        foreach (var p in HostsEditService.Presets)
            PresetBox.Items.Add(P(p.En, p.Zh));
        PresetBox.SelectedIndex = (sel >= 0 && sel < PresetBox.Items.Count) ? sel : (PresetBox.Items.Count > 0 ? 0 : -1);
    }

    private void Show(string msg, InfoBarSeverity severity = InfoBarSeverity.Success)
    {
        Status.Severity = severity;
        Status.Message = msg;
        Status.IsOpen = true;
    }

    private async void Load_Click(object sender, RoutedEventArgs e)
    {
        LoadBtn.IsEnabled = false;
        try
        {
            var msg = await _svc.LoadSystemAsync();
            RefreshOutput();
            Show(msg, msg.Contains("Loaded") || msg.Contains("載入") ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
        }
        catch (Exception ex) { Show(P($"Load failed: {ex.Message}", $"載入失敗：{ex.Message}"), InfoBarSeverity.Error); }
        finally { LoadBtn.IsEnabled = true; }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _svc.Entries.Clear();
        RefreshOutput();
        Show(P("Cleared all entries.", "已清除全部記錄。"), InfoBarSeverity.Informational);
    }

    private void ApplyPreset_Click(object sender, RoutedEventArgs e)
    {
        int i = PresetBox.SelectedIndex;
        if (i < 0 || i >= HostsEditService.Presets.Count)
        {
            Show(P("Select a preset first.", "先揀一個預設。"), InfoBarSeverity.Warning);
            return;
        }
        var msg = _svc.ApplyPreset(HostsEditService.Presets[i]);
        RefreshOutput();
        Show(msg, InfoBarSeverity.Success);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var msg = _svc.Add(IpBox.Text, HostBox.Text, CommentBox.Text);
        bool ok = msg.StartsWith("Added") || msg.StartsWith("已加入");
        if (ok) { IpBox.Text = ""; HostBox.Text = ""; CommentBox.Text = ""; RefreshOutput(); }
        Show(msg, ok ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (EntriesList.SelectedItem is HostsEditService.HostEntry entry)
        {
            _svc.Entries.Remove(entry);
            RefreshOutput();
            Show(P("Removed the selected entry.", "已移除所選記錄。"), InfoBarSeverity.Success);
        }
        else Show(P("Select an entry to remove.", "揀一條要移除嘅記錄。"), InfoBarSeverity.Warning);
    }

    private void Rebuild_Click(object sender, RoutedEventArgs e)
    {
        RefreshOutput();
        Show(P("Rebuilt the hosts text.", "已重建 hosts 文字。"), InfoBarSeverity.Success);
    }

    private void RefreshOutput()
    {
        try { OutputBox.Text = _svc.BuildText(); }
        catch { /* never throws in practice; guard anyway */ }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RefreshOutput();
            var pkg = new DataPackage();
            pkg.SetText(OutputBox.Text ?? "");
            Clipboard.SetContent(pkg);
            Show(P("Copied the rebuilt hosts file to the clipboard.", "已複製重建嘅 hosts 檔到剪貼簿。"), InfoBarSeverity.Success);
        }
        catch (Exception ex) { Show(P($"Copy failed: {ex.Message}", $"複製失敗：{ex.Message}"), InfoBarSeverity.Error); }
    }

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await FileDialogs.SaveFileAsync("hosts", ".txt");
            if (string.IsNullOrEmpty(path)) return;
            var msg = await _svc.SaveAsAsync(path);
            Show(msg, msg.StartsWith("Saved") || msg.StartsWith("已儲存") ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
        }
        catch (Exception ex) { Show(P($"Save failed: {ex.Message}", $"儲存失敗：{ex.Message}"), InfoBarSeverity.Error); }
    }

    private async void WriteBack_Click(object sender, RoutedEventArgs e)
    {
        WriteBackBtn.IsEnabled = false;
        try
        {
            var msg = await _svc.WriteBackAsync();
            bool ok = msg.StartsWith("Wrote") || msg.StartsWith("已寫入");
            bool denied = msg.Contains("administrator") || msg.Contains("管理員");
            Show(msg, ok ? InfoBarSeverity.Success : (denied ? InfoBarSeverity.Warning : InfoBarSeverity.Error));
        }
        catch (Exception ex) { Show(P($"Write failed: {ex.Message}", $"寫入失敗：{ex.Message}"), InfoBarSeverity.Error); }
        finally { WriteBackBtn.IsEnabled = true; }
    }
}
