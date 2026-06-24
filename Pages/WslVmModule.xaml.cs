using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內 WSL 與 Windows 沙盒啟動器 · In-app WSL distro manager + Windows Sandbox launcher
/// (wraps wsl.exe / WindowsSandbox.exe). List / install / export / import / set-default / shutdown WSL
/// distros; emit a .wsb config and start Windows Sandbox with mapped folders. No redirect. Bilingual.
/// </summary>
public sealed partial class WslVmModule : Page
{
    public WslVmModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) => { Render(); await CheckEngines(); await RefreshDistros(); await RefreshOnline(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "WSL & VM Launcher · WSL 與 VM 啟動器";
        HeaderBlurb.Text = P("Manage Windows Subsystem for Linux distros (list, install, export/import, set default, shut down) and launch Windows Sandbox from a generated .wsb config with mapped folders. Everything runs in-app via wsl.exe and WindowsSandbox.exe.",
            "管理 Windows Subsystem for Linux 發行版（列出、安裝、匯出／匯入、設預設、關閉），並用自動產生嘅 .wsb 設定（含對應資料夾）啟動 Windows 沙盒。全部喺 app 內經 wsl.exe 同 WindowsSandbox.exe 運行。");

        WslSectionTitle.Text = P("WSL distributions", "WSL 發行版");
        WslRefreshTxt.Text = P("Refresh", "重新整理");
        WslShutdownBtn.Content = P("Shut down WSL (free RAM)", "關閉 WSL（釋放記憶體）");
        WslImportBtn.Content = P("Import from .tar…", "從 .tar 匯入…");
        InstallTitle.Text = P("Install a new distribution", "安裝新發行版");
        InstallBtn.Content = P("Install", "安裝");

        SandboxSectionTitle.Text = P("Windows Sandbox", "Windows 沙盒");
        MapTitle.Text = P("Mapped folder (shared into the sandbox)", "對應資料夾（分享入沙盒）");
        MapReadOnly.Content = P("Read-only", "唯讀");
        NetworkingChk.Content = P("Networking", "網絡");
        VGpuChk.Content = P("vGPU", "vGPU");
        ClipboardChk.Content = P("Clipboard", "剪貼簿");
        LogonTitle.Text = P("Logon command (optional)", "登入指令（選填）");
        LaunchSandboxBtn.Content = P("Launch Sandbox", "啟動沙盒");
        PreviewWsbBtn.Content = P("Preview .wsb", "預覽 .wsb");
        SaveWsbBtn.Content = P("Save .wsb…", "儲存 .wsb…");
    }

    // ── engine detection ────────────────────────────────────────────────────

    private async Task CheckEngines()
    {
        bool wsl = await WslVmService.IsWslAvailable();
        WslEngineBar.IsOpen = !wsl;
        if (!wsl)
        {
            WslEngineBar.Severity = InfoBarSeverity.Warning;
            WslEngineBar.Title = P("WSL not found", "搵唔到 WSL");
            WslEngineBar.Message = P("Windows Subsystem for Linux is not installed. Click to install it (wsl --install). A restart may be required. You can also enable it from the Package Manager.",
                "未安裝 Windows Subsystem for Linux。撳一下安裝（wsl --install），可能要重啟。你亦可以喺套件管理啟用。");
            var btn = new Button { Content = P("Install WSL", "安裝 WSL") };
            btn.Click += async (_, _) =>
            {
                btn.IsEnabled = false;
                btn.Content = P("Installing…", "安裝緊…");
                var r = await ShellRunner.RunPowershell("wsl.exe --install --no-distribution", false);
                Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                    P("Install WSL", "安裝 WSL"), Msg(r));
                await CheckEngines();
                if (await WslVmService.IsWslAvailable()) { await RefreshDistros(); await RefreshOnline(); }
            };
            WslEngineBar.ActionButton = btn;
        }
        else WslEngineBar.ActionButton = null;

        bool sandbox = WslVmService.IsSandboxAvailable();
        SandboxEngineBar.IsOpen = !sandbox;
        LaunchSandboxBtn.IsEnabled = sandbox;
        if (!sandbox)
        {
            SandboxEngineBar.Severity = InfoBarSeverity.Warning;
            SandboxEngineBar.Title = P("Windows Sandbox not enabled", "未啟用 Windows 沙盒");
            SandboxEngineBar.Message = P("WindowsSandbox.exe was not found. Click to enable the optional feature (DISM, requires admin + restart). Available on Windows Pro/Enterprise/Education.",
                "搵唔到 WindowsSandbox.exe。撳一下啟用呢個選用功能（DISM，需要管理員 + 重啟）。只限 Windows 專業版／企業版／教育版。");
            var btn = new Button { Content = P("Enable Windows Sandbox", "啟用 Windows 沙盒") };
            btn.Click += async (_, _) =>
            {
                btn.IsEnabled = false;
                btn.Content = P("Enabling…", "啟用緊…");
                var r = await WslVmService.EnableSandboxFeature();
                Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
                    P("Enable Windows Sandbox", "啟用 Windows 沙盒"),
                    r.Success ? P("Enabled. Restart Windows to finish.", "已啟用。重啟 Windows 完成。") : Msg(r));
                await CheckEngines();
            };
            SandboxEngineBar.ActionButton = btn;
        }
        else SandboxEngineBar.ActionButton = null;
    }

    // ── WSL: installed distros ───────────────────────────────────────────────

    private async Task RefreshDistros()
    {
        WslBusy.IsActive = true;
        var distros = await WslVmService.ListDistros();
        WslBusy.IsActive = false;
        DistroList.ItemsSource = distros;
        bool empty = distros.Count == 0;
        DistroList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        DistroEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (empty && !WslEngineBar.IsOpen)
            DistroEmpty.Text = P("No distributions installed yet. Pick one below and install it.",
                "未安裝任何發行版。喺下面揀一個嚟裝。");
    }

    private async void WslRefresh_Click(object sender, RoutedEventArgs e)
    {
        await CheckEngines();
        await RefreshDistros();
        await RefreshOnline();
    }

    private async void WslShutdown_Click(object sender, RoutedEventArgs e)
    {
        WslBusy.IsActive = true;
        var r = await WslVmService.Shutdown();
        WslBusy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Shut down WSL", "關閉 WSL"), r.Success ? P("All distributions stopped.", "已停止所有發行版。") : Msg(r));
        await RefreshDistros();
    }

    private void DistroActions_Click(object sender, RoutedEventArgs e) { /* opens flyout */ }

    private static string Tag(object sender) => (sender as FrameworkElement)?.Tag as string ?? "";

    private async void DistroLaunch_Click(object sender, RoutedEventArgs e)
    {
        var name = Tag(sender);
        if (name.Length == 0) return;
        var r = await WslVmService.LaunchTerminal(name);
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Launch terminal", "開終端機"), Msg(r));
    }

    private async void DistroSetDefault_Click(object sender, RoutedEventArgs e)
    {
        var name = Tag(sender);
        if (name.Length == 0) return;
        WslBusy.IsActive = true;
        var r = await WslVmService.SetDefault(name);
        WslBusy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Set default", "設為預設"), r.Success ? $"{name}" : Msg(r));
        await RefreshDistros();
    }

    private async void DistroTerminate_Click(object sender, RoutedEventArgs e)
    {
        var name = Tag(sender);
        if (name.Length == 0) return;
        WslBusy.IsActive = true;
        var r = await WslVmService.Terminate(name);
        WslBusy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Terminate", "終止"), r.Success ? $"{name}" : Msg(r));
        await RefreshDistros();
    }

    private async void DistroExport_Click(object sender, RoutedEventArgs e)
    {
        var name = Tag(sender);
        if (name.Length == 0) return;
        var path = await FileDialogs.SaveFileAsync($"{name}-backup", ".tar");
        if (path is null) return;
        WslBusy.IsActive = true;
        var r = await WslVmService.Export(name, path);
        WslBusy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Export", "匯出"), r.Success ? path : Msg(r));
    }

    private async void DistroUnregister_Click(object sender, RoutedEventArgs e)
    {
        var name = Tag(sender);
        if (name.Length == 0) return;
        var dlg = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = P("Unregister distribution?", "移除發行版？"),
            Content = P($"This permanently deletes \"{name}\" and all its files. This cannot be undone. Export a backup first if you need it.",
                $"呢個會永久刪除「{name}」同佢所有檔案，無法復原。如有需要請先匯出備份。"),
            PrimaryButtonText = P("Unregister", "移除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        WslBusy.IsActive = true;
        var r = await WslVmService.Unregister(name);
        WslBusy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Unregister", "移除"), r.Success ? $"{name}" : Msg(r));
        await RefreshDistros();
    }

    // ── WSL: install / import ────────────────────────────────────────────────

    private async Task RefreshOnline()
    {
        var online = await WslVmService.ListOnline();
        OnlineBox.ItemsSource = online;
        if (OnlineBox.Items.Count > 0) OnlineBox.SelectedIndex = 0;
        OnlineBox.DisplayMemberPath = "Display";
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (OnlineBox.SelectedItem is not WslOnlineDistro d) return;
        WslBusy.IsActive = true;
        var r = await WslVmService.InstallDistro(d.Name);
        WslBusy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Install", "安裝"), r.Success ? d.Name : Msg(r));
        await RefreshDistros();
    }

    private async void WslImport_Click(object sender, RoutedEventArgs e)
    {
        // pick the .tar backup
        var tar = await FileDialogs.OpenFileAsync(".tar");
        if (tar is null) return;

        // pick the install dir
        var dir = await FileDialogs.OpenFolderAsync();
        if (dir is null) return;

        // ask for a name
        var nameBox = new TextBox { PlaceholderText = "Ubuntu-restored" };
        var dlg = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = P("Name the imported distribution", "為匯入嘅發行版命名"),
            Content = nameBox,
            PrimaryButtonText = P("Import", "匯入"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var name = (nameBox.Text ?? "").Trim();
        if (name.Length == 0) return;

        WslBusy.IsActive = true;
        var r = await WslVmService.Import(name, dir, tar);
        WslBusy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Import", "匯入"), r.Success ? name : Msg(r));
        await RefreshDistros();
    }

    // ── Windows Sandbox ──────────────────────────────────────────────────────

    private async void MapBrowse_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync();
        if (folder is not null) MapFolderBox.Text = folder;
    }

    private string BuildWsb()
    {
        var folders = new List<(string, bool)>();
        var host = (MapFolderBox.Text ?? "").Trim();
        if (host.Length > 0) folders.Add((host, MapReadOnly.IsChecked == true));
        return WslVmService.BuildWsbXml(
            folders,
            NetworkingChk.IsChecked == true,
            VGpuChk.IsChecked == true,
            ClipboardChk.IsChecked == true,
            (LogonBox.Text ?? "").Trim());
    }

    private void PreviewWsb_Click(object sender, RoutedEventArgs e)
    {
        WsbPreview.Text = BuildWsb();
        WsbPreviewCard.Visibility = Visibility.Visible;
    }

    private async void SaveWsb_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync("winforge-sandbox", ".wsb");
        if (path is null) return;
        await File.WriteAllTextAsync(path, BuildWsb());
        Notify(InfoBarSeverity.Success, P("Saved .wsb", "已儲存 .wsb"), path);
    }

    private async void LaunchSandbox_Click(object sender, RoutedEventArgs e)
    {
        SbBusy.IsActive = true;
        var r = await WslVmService.LaunchSandbox(BuildWsb());
        SbBusy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Launch Sandbox", "啟動沙盒"), Msg(r));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string Msg(WinForge.Models.TweakResult r)
        => (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";

    private void Notify(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev; ResultBar.Title = title; ResultBar.Message = msg; ResultBar.IsOpen = true;
    }
}
