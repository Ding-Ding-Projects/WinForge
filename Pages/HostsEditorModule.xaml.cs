using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內 hosts 編輯器 · In-app hosts editor — edit the hosts file natively (replaces the Notepad
/// redirect), block a domain (0.0.0.0), back up, save (admin) and flush DNS. Bilingual.
/// </summary>
public sealed partial class HostsEditorModule : Page
{
    public HostsEditorModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => { Render(); Reload(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Hosts Editor · hosts 編輯器";
        HeaderBlurb.Text = P("Edit the hosts file in-app to block or redirect domains (use 0.0.0.0). Saving needs admin. Note: browsers using DNS-over-HTTPS ignore the hosts file.",
            "喺 app 內編輯 hosts 檔嚟封鎖或重新導向域名（用 0.0.0.0）。儲存需要管理員。注意：用緊 DNS-over-HTTPS 嘅瀏覽器會無視 hosts 檔。");
        ReloadBtn.Content = P("Reload", "重新載入");
        SaveBtn.Content = P("Save", "儲存");
        BackupBtn.Content = P("Backup", "備份");
        DomainBox.PlaceholderText = P("domain to block (e.g. ads.example.com)", "要封鎖嘅域名（例如 ads.example.com）");
        BlockBtn.Content = P("Block", "封鎖");
        FlushBtn.Content = P("Flush DNS", "清 DNS");
    }

    private void Reload()
    {
        Editor.Text = HostsService.Read();
        Info(InfoBarSeverity.Informational, P("Loaded", "已載入"), $"{HostsService.Path} · {HostsService.EntryCount(Editor.Text)} " + P("active entries", "個有效項目"));
    }

    private void Reload_Click(object sender, RoutedEventArgs e) => Reload();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            HostsService.Save(Editor.Text);
            Info(InfoBarSeverity.Success, P("Saved", "已儲存"), P("Hosts file written. Flush DNS for it to take effect.", "已寫入 hosts 檔。清一清 DNS 就生效。"));
        }
        catch (UnauthorizedAccessException)
        {
            Info(InfoBarSeverity.Error, P("Failed", "失敗"), P("Saving the hosts file needs administrator rights — relaunch WinTune as admin.", "儲存 hosts 檔需要管理員權限 — 請以管理員身分重開 WinTune。"));
        }
        catch (Exception ex) { Info(InfoBarSeverity.Error, P("Failed", "失敗"), ex.Message); }
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dest = HostsService.Backup();
            Info(InfoBarSeverity.Success, P("Backed up", "已備份"), dest);
        }
        catch (Exception ex) { Info(InfoBarSeverity.Error, P("Failed", "失敗"), ex.Message); }
    }

    private void Block_Click(object sender, RoutedEventArgs e)
    {
        var domain = DomainBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(domain)) { Info(InfoBarSeverity.Warning, P("Heads up", "注意"), P("Enter a domain to block.", "請輸入要封鎖嘅域名。")); return; }
        Editor.Text = HostsService.AddBlock(Editor.Text, domain);
        DomainBox.Text = "";
        Info(InfoBarSeverity.Informational, P("Added", "已加入"), P($"0.0.0.0 {domain} added — click Save to apply.", $"已加入 0.0.0.0 {domain} — 撳儲存先生效。"));
    }

    private async void Flush_Click(object sender, RoutedEventArgs e)
    {
        var r = await ShellRunner.RunCmd("ipconfig /flushdns");
        Info(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, r.Success ? P("Done", "完成") : P("Failed", "失敗"),
            P("DNS resolver cache flushed.", "已清除 DNS 解析快取。"));
    }

    private void Info(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev;
        ResultBar.Title = title;
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
