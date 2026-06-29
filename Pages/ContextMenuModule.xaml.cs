using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內右鍵選單編輯器 · In-app context-menu editor — add/list/remove custom right-click verbs
/// (per-user HKCU shell keys; never touches system defaults). Bilingual. No redirect.
/// </summary>
public sealed partial class ContextMenuModule : Page
{
    public sealed class VerbRow
    {
        public int Scope { get; init; }
        public string Key { get; init; } = "";
        public string ScopeLabel { get; init; } = "";
        public string Label { get; init; } = "";
        public string Command { get; init; } = "";
    }

    private readonly ObservableCollection<VerbRow> _rows = new();

    public ContextMenuModule()
    {
        InitializeComponent();
        List.ItemsSource = _rows;
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => { Render(); RefreshList(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Context Menu Editor · 右鍵選單編輯器";
        HeaderBlurb.Text = P("Add your own right-click commands and remove ones you created — per-user, applied instantly. Use %1 for the file/folder, %V for folder-background.",
            "加自己嘅右鍵指令，又可以移除自己整嘅 — 只限本使用者、即時生效。用 %1 代表檔案／資料夾，%V 代表資料夾空白處。");
        LabelBox.PlaceholderText = P("Menu label, e.g. Open with Notepad", "選單文字，例如 用記事本開");
        CommandBox.PlaceholderText = P("Command, e.g. notepad.exe \"%1\"", "指令，例如 notepad.exe \"%1\"");
        IconBox.PlaceholderText = P("Icon path (optional), e.g. notepad.exe", "圖示路徑（可選），例如 notepad.exe");
        BrowseBtn.Content = P("Browse…", "瀏覽…");
        ExtendedChk.Content = P("Shift only", "淨係 Shift");
        AddBtn.Content = P("Add", "新增");
        PresetsLabel.Text = P("Quick presets:", "快速範本：");
        PresetPs.Content = P("PowerShell here", "喺呢度開 PowerShell");
        PresetCmd.Content = P("Command Prompt here", "喺呢度開命令提示字元");
        ColScope.Text = P("Scope", "範圍");
        ColLabel.Text = P("Label", "文字");
        ColCommand.Text = P("Command", "指令");

        int sel = ScopeBox.SelectedIndex < 0 ? 2 : ScopeBox.SelectedIndex;
        ScopeBox.Items.Clear();
        for (int i = 0; i < ContextMenuService.ScopeCount; i++) ScopeBox.Items.Add(ContextMenuService.ScopeLabel(i));
        ScopeBox.SelectedIndex = sel;
    }

    private void RefreshList()
    {
        _rows.Clear();
        foreach (var v in ContextMenuService.List())
            _rows.Add(new VerbRow { Scope = v.Scope, Key = v.Key, ScopeLabel = ContextMenuService.ScopeLabel(v.Scope), Label = v.Label, Command = v.Command });
        ColCommand.Text = P($"Command — {_rows.Count} custom", $"指令 — {_rows.Count} 個自訂");
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".exe");
        if (path is not null)
        {
            string ph = ContextMenuService.ScopePlaceholder(Math.Max(0, ScopeBox.SelectedIndex));
            CommandBox.Text = $"\"{path}\" \"{ph}\"";
            if (string.IsNullOrWhiteSpace(IconBox.Text)) IconBox.Text = path;
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        string label = (LabelBox.Text ?? "").Trim();
        string cmd = (CommandBox.Text ?? "").Trim();
        if (label.Length == 0 || cmd.Length == 0)
        {
            Show(InfoBarSeverity.Warning, P("Fill in a label and a command", "請填寫文字同指令"), "");
            return;
        }
        try
        {
            ContextMenuService.Add(ScopeBox.SelectedIndex, label, cmd, IconBox.Text?.Trim(), ExtendedChk.IsChecked == true);
            Show(InfoBarSeverity.Success, P("Added to the right-click menu", "已加入右鍵選單"), $"{ContextMenuService.ScopeLabel(ScopeBox.SelectedIndex)} · {label}");
            LabelBox.Text = ""; CommandBox.Text = ""; IconBox.Text = ""; ExtendedChk.IsChecked = false;
            RefreshList();
        }
        catch (Exception ex) { Show(InfoBarSeverity.Error, P("Failed", "失敗"), ex.Message); }
    }

    private void PresetPs_Click(object sender, RoutedEventArgs e)
        => AddPreset(2, P("Open PowerShell here", "喺呢度開 PowerShell"),
            "powershell.exe -NoExit -Command \"Set-Location -LiteralPath '%V'\"", "powershell.exe");

    private void PresetCmd_Click(object sender, RoutedEventArgs e)
        => AddPreset(2, P("Open Command Prompt here", "喺呢度開命令提示字元"),
            "cmd.exe /s /k pushd \"%V\"", "cmd.exe");

    private void AddPreset(int scope, string label, string cmd, string icon)
    {
        try
        {
            ContextMenuService.Add(scope, label, cmd, icon, false);
            Show(InfoBarSeverity.Success, P("Preset added", "已加範本"), label);
            RefreshList();
        }
        catch (Exception ex) { Show(InfoBarSeverity.Error, P("Failed", "失敗"), ex.Message); }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not VerbRow row) return;
        try { ContextMenuService.Remove(row.Scope, row.Key); RefreshList(); }
        catch (Exception ex) { Show(InfoBarSeverity.Error, P("Failed", "失敗"), ex.Message); }
    }

    private void Show(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev;
        ResultBar.Title = title;
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
