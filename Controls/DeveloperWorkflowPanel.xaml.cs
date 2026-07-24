using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Controls;

/// <summary>Reviewed, parameterized workflows that close the Developer &amp; Terminal roadmap gaps.</summary>
public sealed partial class DeveloperWorkflowPanel : UserControl
{
    private IReadOnlyList<PortListener> _listeners = Array.Empty<PortListener>();
    private int _inspectedPort;
    private IReadOnlyList<DeveloperCacheSnapshot> _cacheSnapshots = Array.Empty<DeveloperCacheSnapshot>();
    private CancellationTokenSource? _operation;
    private bool _languageSubscribed;
    private bool _busy;

    public DeveloperWorkflowPanel()
    {
        InitializeComponent();
        PortBox.Minimum = DeveloperWorkflowCore.MinimumPort;
        PortBox.Maximum = DeveloperWorkflowCore.MaximumPort;
        PortBox.Value = 8080;
        DynamicStartBox.Minimum = DeveloperWorkflowCore.MinimumDynamicPortStart;
        DynamicStartBox.Maximum = DeveloperWorkflowCore.MaximumPort;
        DynamicStartBox.Value = 10000;
        DynamicCountBox.Minimum = 1;
        DynamicCountBox.Maximum = DeveloperWorkflowCore.MaximumPort;
        DynamicCountBox.Value = 55000;
        TimedWaitBox.Minimum = DeveloperWorkflowCore.MinimumTimedWaitSeconds;
        TimedWaitBox.Maximum = DeveloperWorkflowCore.MaximumTimedWaitSeconds;
        TimedWaitBox.Value = 60;
        NodeVersionBox.Text = "lts";
        CorepackChannelBox.Text = "latest";
        foreach (var control in new Control[]
        {
            PortBox, NodeManagerBox, NodeVersionBox, CorepackChannelBox, DefenderFolderBox,
            DefenderExclusionBox, DynamicStartBox, DynamicCountBox, TimedWaitBox,
        }) control.MinHeight = 48;
        RenderText();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Panel_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_languageSubscribed)
        {
            Loc.I.LanguageChanged += OnLanguageChanged;
            _languageSubscribed = true;
        }
        RenderText();
        ReloadNodeManagers();
    }

    private void Panel_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_languageSubscribed)
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            _languageSubscribed = false;
        }
        _operation?.Cancel();
        _operation?.Dispose();
        _operation = null;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RenderText();

    private void RenderText()
    {
        TitleText.Text = P("Developer workflow workbench", "開發工作流程工作台");
        DescriptionText.Text = P(
            "Inspect first, then run a reviewed action. User values stay in discrete argument-list items; privileged and destructive actions require a decision.",
            "先檢視，再執行已審閱動作。使用者輸入會保持獨立參數；提權同破壞性動作一定要確認。");
        PortExpander.Header = P("Resolve and terminate the process listening on a port", "搵出並終止監聽連接埠嘅程序");
        PortBox.Header = P("Listening TCP port", "監聽 TCP 連接埠");
        SetButton(InspectPortBtn, P("Inspect listener", "檢視監聽程序"));
        SetButton(TerminatePortBtn, P("Terminate reviewed listener(s)", "終止已檢視監聽程序"));

        NodeExpander.Header = P("Node version per shell", "每個 shell 獨立 Node 版本");
        NodeManagerBox.Header = P("Detected version manager", "偵測到嘅版本管理器");
        NodeVersionBox.Header = P("Node version", "Node 版本");
        SetButton(RefreshNodeBtn, P("Detect managers", "偵測管理器"));
        SetButton(ListNodeBtn, P("List versions", "列出版本"));
        SetButton(InstallNodeBtn, P("Install version", "安裝版本"));
        SetButton(OpenNodeShellBtn, P("Open version-scoped shell", "開指定版本 shell"));
        NodeSafetyText.Text = P(
            "fnm and Volta open an isolated version-scoped PowerShell. nvm-windows is detected for list/install only because its symlink switch is machine-wide.",
            "fnm 同 Volta 會開隔離版本 PowerShell。nvm-windows 只供列出／安裝，因為佢個 symlink 切換係全機共用。");

        CorepackExpander.Header = P("Corepack for pnpm and yarn", "用 Corepack 管理 pnpm 同 yarn");
        CorepackChannelBox.Header = P("Channel or version", "頻道或版本");
        SetButton(CorepackStatusBtn, P("Show Corepack version", "顯示 Corepack 版本"));
        SetButton(CorepackEnableBtn, P("Enable Corepack shims", "啟用 Corepack shims"));
        SetButton(CorepackPnpmBtn, P("Prepare pnpm", "準備 pnpm"));
        SetButton(CorepackYarnBtn, P("Prepare yarn", "準備 yarn"));

        DefenderExpander.Header = P("Reviewed Defender developer-folder exclusions", "已審閱 Defender 開發資料夾例外");
        DefenderFolderBox.Header = P("Developer folder", "開發資料夾");
        SetButton(PickDefenderFolderBtn, P("Choose…", "揀選…"));
        DefenderExclusionBox.Header = P("Existing exclusion", "現有例外");
        SetButton(ListDefenderBtn, P("Refresh exclusions", "重新整理例外"));
        SetButton(AddDefenderBtn, P("Add selected folder", "加入揀選資料夾"));
        SetButton(RemoveDefenderBtn, P("Remove selected exclusion", "移除揀選例外"));
        DefenderSafetyText.Text = P(
            "Drive roots, Windows, and Program Files are rejected. Add/remove uses Microsoft Defender PowerShell and requests elevation.",
            "會拒絕磁碟根目錄、Windows 同 Program Files。加入／移除會用 Defender PowerShell 並要求提權。");

        TcpExpander.Header = P("Ephemeral TCP range and TIME_WAIT", "Ephemeral TCP 範圍同 TIME_WAIT");
        DynamicStartBox.Header = P("Range start", "範圍起點");
        DynamicCountBox.Header = P("Port count", "連接埠數量");
        TimedWaitBox.Header = P("TIME_WAIT seconds", "TIME_WAIT 秒數");
        SetButton(InspectTcpBtn, P("Inspect current values", "檢視目前數值"));
        SetButton(ApplyTcpBtn, P("Apply reviewed values", "套用已審閱數值"));

        CacheExpander.Header = P("Measure and clean developer caches", "量度並清理開發快取");
        CacheSafetyText.Text = P(
            "Inspection is read-only and bounded. Cleanup stays disabled until reclaimable locations/tool output have been shown.",
            "檢視係唯讀同有界。顯示可回收位置／工具輸出之前，清理會保持停用。");
        NpmCacheCheck.Content = "npm";
        PnpmCacheCheck.Content = "pnpm";
        PipCacheCheck.Content = "pip";
        DockerCacheCheck.Content = "Docker builder";
        SetButton(InspectCachesBtn, P("Inspect reclaimable sizes", "檢視可回收大小"));
        SetButton(CleanCachesBtn, P("Clean selected reviewed caches", "清理揀選已檢視快取"));
        SetButton(CancelBtn, P("Cancel", "取消"));

        AutomationProperties.SetName(PortBox, P("Listening TCP port", "監聽 TCP 連接埠"));
        AutomationProperties.SetName(NodeManagerBox, P("Node version manager", "Node 版本管理器"));
        AutomationProperties.SetName(NodeVersionBox, P("Node version", "Node 版本"));
        AutomationProperties.SetName(DefenderFolderBox, P("Developer folder", "開發資料夾"));
        AutomationProperties.SetName(DynamicStartBox, P("Dynamic port range start", "動態連接埠範圍起點"));
        AutomationProperties.SetName(DynamicCountBox, P("Dynamic port count", "動態連接埠數量"));
        AutomationProperties.SetName(TimedWaitBox, P("TIME WAIT seconds", "TIME WAIT 秒數"));
    }

    private static void SetButton(Button button, string label)
    {
        button.MinHeight = 48;
        button.Padding = new Thickness(12, 8, 12, 8);
        button.Content = new TextBlock { Text = label, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetName(button, label);
    }

    private void ReloadNodeManagers()
    {
        var managers = DeveloperWorkflowService.DetectNodeManagers();
        NodeManagerBox.ItemsSource = managers;
        NodeManagerBox.SelectedIndex = managers.Count > 0 ? 0 : -1;
        if (managers.Count == 0) ShowStatus(TweakResult.Fail(
            "No fnm, Volta, or nvm-windows executable was found.",
            "搵唔到 fnm、Volta 或 nvm-windows。"));
    }

    private int ReadInt(NumberBox box, string name)
    {
        if (double.IsNaN(box.Value) || box.Value != Math.Truncate(box.Value))
            throw new ArgumentException(P($"Enter a whole number for {name}.", $"請為{name}輸入整數。"));
        return checked((int)box.Value);
    }

    private async void InspectPort_Click(object sender, RoutedEventArgs e)
    {
        await RunCustom(async ct =>
        {
            var port = ReadInt(PortBox, P("port", "連接埠"));
            _listeners = await DeveloperWorkflowService.InspectPortAsync(port, ct);
            _inspectedPort = port;
            PortOutput.Text = _listeners.Count == 0
                ? P("No TCP listener found.", "搵唔到 TCP 監聽程序。")
                : string.Join("\n", _listeners.Select(item => item.DisplayLabel));
            TerminatePortBtn.IsEnabled = _listeners.Count > 0;
            return TweakResult.Ok("Listener inspection completed.", "監聽程序檢視完成。");
        });
    }

    private async void TerminatePort_Click(object sender, RoutedEventArgs e)
    {
        if (_listeners.Count == 0) return;
        if (!await Confirm(P("Terminate listener processes?", "終止監聽程序？"),
                string.Join("\n", _listeners.Select(item => item.DisplayLabel)), P("Terminate", "終止"))) return;
        await RunCustom(ct => DeveloperWorkflowService.TerminateReviewedListenersAsync(_inspectedPort, _listeners, ct));
        _listeners = Array.Empty<PortListener>();
        TerminatePortBtn.IsEnabled = false;
    }

    private void RefreshNode_Click(object sender, RoutedEventArgs e) => ReloadNodeManagers();

    private async void ListNode_Click(object sender, RoutedEventArgs e)
    {
        if (NodeManagerBox.SelectedItem is not DetectedNodeManager manager) return;
        await RunCustom(ct => DeveloperWorkflowService.ListNodeVersionsAsync(manager, ct));
    }

    private async void InstallNode_Click(object sender, RoutedEventArgs e)
    {
        if (NodeManagerBox.SelectedItem is not DetectedNodeManager manager) return;
        string version;
        try { version = DeveloperWorkflowCore.ValidateNodeVersion(NodeVersionBox.Text); }
        catch (Exception ex) { ShowInputError(ex); return; }
        if (!await Confirm(P("Install this Node version?", "安裝呢個 Node 版本？"),
                $"{manager.DisplayName} · {version}", P("Install", "安裝"))) return;
        await RunCustom(ct => DeveloperWorkflowService.InstallNodeVersionAsync(manager, version, ct));
    }

    private void OpenNodeShell_Click(object sender, RoutedEventArgs e)
    {
        if (NodeManagerBox.SelectedItem is not DetectedNodeManager manager) return;
        string version;
        try { version = DeveloperWorkflowCore.ValidateNodeVersion(NodeVersionBox.Text); }
        catch (Exception ex) { ShowInputError(ex); return; }
        ShowStatus(DeveloperWorkflowService.OpenNodeVersionShell(manager, version));
    }

    private async void CorepackStatus_Click(object sender, RoutedEventArgs e)
        => await RunCustom(DeveloperWorkflowService.CorepackStatusAsync);

    private async void CorepackEnable_Click(object sender, RoutedEventArgs e)
    {
        if (await Confirm(P("Enable Corepack shims?", "啟用 Corepack shims？"),
                P("This changes the active Node installation's package-manager shims.", "呢個會修改目前 Node 安裝嘅套件管理器 shims。"), P("Enable", "啟用")))
            await RunCustom(DeveloperWorkflowService.EnableCorepackAsync);
    }

    private async void CorepackPnpm_Click(object sender, RoutedEventArgs e) => await PrepareCorepack("pnpm");
    private async void CorepackYarn_Click(object sender, RoutedEventArgs e) => await PrepareCorepack("yarn");

    private async Task PrepareCorepack(string manager)
    {
        try { _ = DeveloperWorkflowCore.BuildCorepackPreparePlan(manager, CorepackChannelBox.Text); }
        catch (Exception ex) { ShowInputError(ex); return; }
        if (await Confirm(P($"Activate {manager}?", $"啟用 {manager}？"),
                $"{manager}@{CorepackChannelBox.Text}", P("Prepare", "準備")))
            await RunCustom(ct => DeveloperWorkflowService.PrepareCorepackAsync(manager, CorepackChannelBox.Text, ct));
    }

    private async void PickDefenderFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Choose developer folder", "揀開發資料夾"));
        if (folder is not null) DefenderFolderBox.Text = folder;
    }

    private async void ListDefender_Click(object sender, RoutedEventArgs e)
    {
        await RunCustom(async ct =>
        {
            var paths = await DeveloperWorkflowService.DefenderExclusionsAsync(ct);
            DefenderExclusionBox.ItemsSource = paths;
            DefenderExclusionBox.SelectedIndex = paths.Count > 0 ? 0 : -1;
            return TweakResult.Ok($"Found {paths.Count} Defender path exclusion(s).", $"搵到 {paths.Count} 個 Defender 路徑例外。");
        });
    }

    private async void AddDefender_Click(object sender, RoutedEventArgs e)
    {
        string folder;
        try { folder = DeveloperWorkflowCore.ValidateDeveloperFolder(DefenderFolderBox.Text); }
        catch (Exception ex) { ShowInputError(ex); return; }
        if (!await Confirm(P("Add Defender exclusion?", "加入 Defender 例外？"), folder, P("Add exclusion", "加入例外"))) return;
        await RunCustom(ct => DeveloperWorkflowService.SetDefenderExclusionAsync(folder, add: true, ct));
    }

    private async void RemoveDefender_Click(object sender, RoutedEventArgs e)
    {
        if (DefenderExclusionBox.SelectedItem is not string path) return;
        if (!await Confirm(P("Remove Defender exclusion?", "移除 Defender 例外？"), path, P("Remove exclusion", "移除例外"))) return;
        await RunCustom(ct => DeveloperWorkflowService.SetDefenderExclusionAsync(path, add: false, ct));
    }

    private async void InspectTcp_Click(object sender, RoutedEventArgs e)
    {
        await RunCustom(async ct =>
        {
            TcpOutput.Text = await DeveloperWorkflowService.InspectTcpTuningAsync(ct);
            return TweakResult.Ok("TCP tuning values were inspected.", "已檢視 TCP 調校數值。");
        });
    }

    private async void ApplyTcp_Click(object sender, RoutedEventArgs e)
    {
        int start;
        int count;
        int wait;
        try
        {
            start = ReadInt(DynamicStartBox, P("range start", "範圍起點"));
            count = ReadInt(DynamicCountBox, P("port count", "連接埠數量"));
            wait = ReadInt(TimedWaitBox, "TIME_WAIT");
            DeveloperWorkflowCore.ValidateTcpTuning(start, count, wait);
        }
        catch (Exception ex) { ShowInputError(ex); return; }
        if (!await Confirm(P("Apply TCP tuning?", "套用 TCP 調校？"),
                P($"Dynamic ports {start}-{start + count - 1}; TIME_WAIT {wait}s. A restart may be required.",
                    $"動態連接埠 {start}-{start + count - 1}；TIME_WAIT {wait} 秒。可能要重新開機。"), P("Apply", "套用"))) return;
        await RunCustom(ct => DeveloperWorkflowService.ApplyTcpTuningAsync(start, count, wait, ct));
    }

    private async void InspectCaches_Click(object sender, RoutedEventArgs e)
    {
        await RunCustom(async ct =>
        {
            _cacheSnapshots = await DeveloperWorkflowService.InspectCachesAsync(ct);
            CacheOutput.Text = string.Join("\n\n", _cacheSnapshots.Select(item => item.Summary));
            ConfigureCacheCheck(NpmCacheCheck, DeveloperCacheKind.Npm);
            ConfigureCacheCheck(PnpmCacheCheck, DeveloperCacheKind.Pnpm);
            ConfigureCacheCheck(PipCacheCheck, DeveloperCacheKind.Pip);
            ConfigureCacheCheck(DockerCacheCheck, DeveloperCacheKind.Docker);
            CleanCachesBtn.IsEnabled = _cacheSnapshots.Any(item => item.ToolAvailable);
            return TweakResult.Ok("Reclaimable cache locations were inspected.", "已檢視可回收快取位置。");
        });
    }

    private void ConfigureCacheCheck(CheckBox check, DeveloperCacheKind kind)
    {
        var snapshot = _cacheSnapshots.First(item => item.Kind == kind);
        check.IsEnabled = snapshot.ToolAvailable;
        check.IsChecked = snapshot.ToolAvailable;
    }

    private async void CleanCaches_Click(object sender, RoutedEventArgs e)
    {
        var kinds = new List<DeveloperCacheKind>();
        if (NpmCacheCheck.IsChecked == true) kinds.Add(DeveloperCacheKind.Npm);
        if (PnpmCacheCheck.IsChecked == true) kinds.Add(DeveloperCacheKind.Pnpm);
        if (PipCacheCheck.IsChecked == true) kinds.Add(DeveloperCacheKind.Pip);
        if (DockerCacheCheck.IsChecked == true) kinds.Add(DeveloperCacheKind.Docker);
        var summary = string.Join("\n", _cacheSnapshots.Where(item => kinds.Contains(item.Kind)).Select(item => item.Summary));
        if (!await Confirm(P("Clean selected caches?", "清理揀選快取？"), summary, P("Clean", "清理"))) return;
        await RunCustom(ct => DeveloperWorkflowService.CleanCachesAsync(kinds, ct));
        CleanCachesBtn.IsEnabled = false;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _operation?.Cancel();

    private async Task RunCustom(Func<CancellationToken, Task<TweakResult>> action)
    {
        if (_busy) return;
        SetBusy(true);
        _operation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { ShowStatus(await action(_operation.Token)); }
        catch (OperationCanceledException) { ShowStatus(TweakResult.Fail("Cancelled or timed out.", "已取消或者逾時。")); }
        catch (Exception ex) { ShowInputError(ex); }
        finally
        {
            _operation.Dispose();
            _operation = null;
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        BusyRing.IsActive = busy;
        BusyRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        CancelBtn.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task<bool> Confirm(string title, string message, string primary)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primary,
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        try { return await dialog.ShowAsync() == ContentDialogResult.Primary; }
        catch { return false; }
    }

    private void ShowStatus(TweakResult result)
    {
        StatusBar.Severity = result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        StatusBar.Title = result.Success ? P("Done", "完成") : P("Failed", "失敗");
        StatusBar.Message = result.Message?.Get(Loc.I.Language) ?? string.Empty;
        StatusBar.IsOpen = true;
        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            OutputText.Text = result.Output.Length <= 4000 ? result.Output : result.Output[..4000] + "…";
            OutputBorder.Visibility = Visibility.Visible;
        }
        else OutputBorder.Visibility = Visibility.Collapsed;
    }

    private void ShowInputError(Exception ex)
        => ShowStatus(TweakResult.Fail(
            ex.Message,
            "輸入無效或者所需工具不可用；請檢查欄位同安裝狀態。",
            ex.Message));
}
