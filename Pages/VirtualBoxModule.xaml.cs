using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// VirtualBox 管理（包 VBoxManage.exe）· VirtualBox manager wrapping VBoxManage.exe.
/// 列出 VM、控制電源（啟動／無頭／儲存／關機／暫停／繼續）、快照（拍攝／還原／刪除）、
/// 修改 CPU/RAM、建立／複製／刪除 VM、匯入／匯出 OVA、顯示主機資訊。缺安裝時用 winget 一鍵裝。Bilingual.
/// </summary>
public sealed partial class VirtualBoxModule : Page
{
    private VBoxVm? _selected;

    public VirtualBoxModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += async (_, _) => { Render(); await CheckEngine(); await Refresh(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "VirtualBox Manager · VirtualBox 管理";
        HeaderBlurb.Text = P("Manage Oracle VirtualBox virtual machines via VBoxManage — list with live state, start (GUI / headless), pause / resume / save / power off, take and restore snapshots, change CPUs and RAM, create / clone / delete VMs, and import / export OVA appliances. Everything runs in-app.",
            "經 VBoxManage 管理 Oracle VirtualBox 虛擬機 — 列出並顯示即時狀態，啟動（圖形／無頭）、暫停／繼續／儲存／關機，拍攝同還原快照，改 CPU 同記憶體，建立／複製／刪除虛擬機，匯入／匯出 OVA。全部喺 app 內運行。");

        RefreshTxt.Text = P("Refresh", "重新整理");
        CreateTxt.Text = P("New VM", "新增虛擬機");
        ImportBtn.Content = P("Import OVA…", "匯入 OVA…");
        VmSectionTitle.Text = P("Virtual machines", "虛擬機");

        ModifyTitle.Text = P("Modify CPUs & memory (VM must be powered off)", "修改 CPU 同記憶體（虛擬機要先關機）");
        CpuLbl.Text = P("CPUs", "CPU 數");
        RamLbl.Text = P("Memory (MB)", "記憶體（MB）");
        ApplyModifyBtn.Content = P("Apply", "套用");

        SnapTitle.Text = P("Snapshots", "快照");
        SnapTakeBtn.Content = P("Take…", "拍攝…");
        SnapRestoreBtn.Content = P("Restore", "還原");
        SnapDeleteBtn.Content = P("Delete", "刪除");

        HostHeader.Text = P("Host information", "主機資訊");
        HostRefreshBtn.Content = P("Load host info", "載入主機資訊");

        if (_selected is not null) RenderSelected();
    }

    // ── engine detection / install ───────────────────────────────────────────

    private async Task CheckEngine()
    {
        bool ok = VirtualBoxService.IsAvailable();
        EngineBar.IsOpen = !ok;
        CreateBtn.IsEnabled = ok;
        ImportBtn.IsEnabled = ok;
        if (!ok)
        {
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("VirtualBox not found", "搵唔到 VirtualBox");
            EngineBar.Message = P("VBoxManage.exe was not located. Install Oracle VirtualBox below (live progress) to manage virtual machines. Note: VirtualBox can conflict with Hyper-V / WSL2; if VMs fail to start with a VT-x error, disable the Windows hypervisor.",
                "搵唔到 VBoxManage.exe。喺下面安裝 Oracle VirtualBox（即時進度）嚟管理虛擬機。注意：VirtualBox 可能同 Hyper-V／WSL2 衝突；如果開機出現 VT-x 錯誤，請停用 Windows hypervisor。");
            EngineBar.ActionButton = null;
            if (EngineBar.Content is not InstallProgress)
                EngineBar.Content = EngineBars.AutoInstallProgress(
                    "Oracle.VirtualBox", "Install VirtualBox", "安裝 VirtualBox",
                    recheck: async () => { await CheckEngine(); await Refresh(); },
                    rescan: VirtualBoxService.Rescan);
        }
        else
        {
            EngineBar.ActionButton = null;
            EngineBar.Content = null;
            var ver = await VirtualBoxService.GetVersion();
            if (ver is not null)
            {
                EngineBar.IsOpen = true;
                EngineBar.Severity = InfoBarSeverity.Success;
                EngineBar.Title = P("VirtualBox ready", "VirtualBox 已就緒");
                EngineBar.Message = P($"VBoxManage {ver} detected.", $"已偵測到 VBoxManage {ver}。");
            }
        }
    }

    // ── list ──────────────────────────────────────────────────────────────────

    private async Task Refresh()
    {
        if (!VirtualBoxService.IsAvailable()) { VmList.ItemsSource = null; return; }
        Busy.IsActive = true;
        var vms = await VirtualBoxService.ListVms(true);
        Busy.IsActive = false;
        VmList.ItemsSource = vms;
        bool empty = vms.Count == 0;
        VmList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        VmEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (empty) VmEmpty.Text = P("No virtual machines yet. Click \"New VM\" or import an OVA appliance.", "未有任何虛擬機。撳「新增虛擬機」或者匯入 OVA。");
        // re-select previously selected by uuid
        if (_selected is not null)
        {
            var match = vms.FirstOrDefault(v => v.Uuid == _selected.Uuid);
            if (match is not null) VmList.SelectedItem = match;
            else { _selected = null; DetailCard.Visibility = Visibility.Collapsed; }
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await CheckEngine();
        await Refresh();
    }

    private async void VmList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = VmList.SelectedItem as VBoxVm;
        if (_selected is null) { DetailCard.Visibility = Visibility.Collapsed; return; }
        DetailCard.Visibility = Visibility.Visible;
        RenderSelected();
        await RefreshSnapshots();
    }

    private void RenderSelected()
    {
        if (_selected is null) return;
        SelName.Text = _selected.Name;
        SelState.Text = P($"State: {_selected.StateEn} · {_selected.Cpus} CPU · {_selected.MemoryMb} MB · {_selected.OsType}",
            $"狀態：{_selected.StateZh} · {_selected.Cpus} CPU · {_selected.MemoryMb} MB · {_selected.OsType}");
        if (_selected.Cpus > 0) CpuBox.Value = _selected.Cpus;
        if (_selected.MemoryMb > 0) RamBox.Value = _selected.MemoryMb;

        bool off = _selected.IsOff;
        ApplyModifyBtn.IsEnabled = off;
        CpuBox.IsEnabled = off;
        RamBox.IsEnabled = off;
        ModifyHint.IsOpen = !off;
        if (!off)
        {
            ModifyHint.Title = P("VM is not powered off", "虛擬機未關機");
            ModifyHint.Message = P("Save state or power off the VM before changing CPUs or memory.", "改 CPU 或記憶體前，請先儲存狀態或關機。");
        }

        // snapshots: deleting a snapshot of a running VM is allowed but restore is not
        SnapRestoreBtn.IsEnabled = _selected.IsOff;
    }

    // ── power control ──────────────────────────────────────────────────────────

    private static string Tag(object sender) => (sender as FrameworkElement)?.Tag as string ?? "";

    private async Task DoPower(string id, Func<string, Task<Models.TweakResult>> op, string en, string zh)
    {
        if (id.Length == 0) return;
        Busy.IsActive = true;
        var r = await op(id);
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P(en, zh), r.Success ? P("Done.", "完成。") : Msg(r));
        await Refresh();
    }

    private async void StartGui_Click(object s, RoutedEventArgs e) => await DoPower(Tag(s), id => VirtualBoxService.StartGui(id), "Start", "啟動");
    private async void StartHeadless_Click(object s, RoutedEventArgs e) => await DoPower(Tag(s), id => VirtualBoxService.StartHeadless(id), "Start headless", "無頭啟動");
    private async void Pause_Click(object s, RoutedEventArgs e) => await DoPower(Tag(s), id => VirtualBoxService.Pause(id), "Pause", "暫停");
    private async void Resume_Click(object s, RoutedEventArgs e) => await DoPower(Tag(s), id => VirtualBoxService.Resume(id), "Resume", "繼續");
    private async void SaveState_Click(object s, RoutedEventArgs e) => await DoPower(Tag(s), id => VirtualBoxService.SaveState(id), "Save state", "儲存狀態");
    private async void Acpi_Click(object s, RoutedEventArgs e) => await DoPower(Tag(s), id => VirtualBoxService.AcpiPowerButton(id), "ACPI shutdown", "ACPI 關機");

    private async void PowerOff_Click(object s, RoutedEventArgs e)
    {
        var id = Tag(s);
        if (id.Length == 0) return;
        if (!await Confirm(P("Power off VM?", "強制關機？"),
            P("This is a hard power-off — like pulling the plug. Unsaved data in the guest may be lost.",
              "呢個係強制關機 — 等於拔電源。客體機未儲存嘅資料可能會遺失。"),
            P("Power off", "關機"))) return;
        await DoPower(id, x => VirtualBoxService.PowerOff(x), "Power off", "強制關機");
    }

    private async void Reset_Click(object s, RoutedEventArgs e)
    {
        var id = Tag(s);
        if (id.Length == 0) return;
        if (!await Confirm(P("Reset VM?", "重設虛擬機？"),
            P("This is a hard reset — like the reset button. Unsaved guest data may be lost.",
              "呢個係硬重設 — 等於撳重設掣。客體機未儲存嘅資料可能會遺失。"),
            P("Reset", "重設"))) return;
        await DoPower(id, x => VirtualBoxService.Reset(x), "Reset", "重設");
    }

    // ── modify CPUs / RAM ──────────────────────────────────────────────────────

    private async void ApplyModify_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null || !_selected.IsOff) return;
        int cpus = (int)Math.Round(double.IsNaN(CpuBox.Value) ? _selected.Cpus : CpuBox.Value);
        long mem = (long)Math.Round(double.IsNaN(RamBox.Value) ? _selected.MemoryMb : RamBox.Value);
        if (cpus < 1) cpus = 1;
        if (mem < 4) mem = 4;
        Busy.IsActive = true;
        var r = await VirtualBoxService.Modify(_selected.Uuid, cpus, mem);
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Modify VM", "修改虛擬機"),
            r.Success ? P($"{cpus} CPU · {mem} MB", $"{cpus} CPU · {mem} MB") : Msg(r));
        await Refresh();
    }

    // ── snapshots ──────────────────────────────────────────────────────────────

    private async Task RefreshSnapshots()
    {
        if (_selected is null) { SnapList.ItemsSource = null; return; }
        var snaps = await VirtualBoxService.ListSnapshots(_selected.Uuid);
        SnapList.ItemsSource = snaps;
        bool empty = snaps.Count == 0;
        SnapList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        SnapEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (empty) SnapEmpty.Text = P("No snapshots. Take one to capture the current state.", "未有快照。拍攝一個嚟記錄目前狀態。");
    }

    private async void SnapRefresh_Click(object sender, RoutedEventArgs e) => await RefreshSnapshots();

    private async void SnapTake_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var nameBox = new TextBox { PlaceholderText = P("Snapshot name", "快照名稱"), Text = $"Snapshot {DateTime.Now:yyyy-MM-dd HHmm}" };
        var descBox = new TextBox { PlaceholderText = P("Description (optional)", "說明（選填）"), AcceptsReturn = true, Height = 70, Margin = new Thickness(0, 8, 0, 0) };
        var panel = new StackPanel();
        panel.Children.Add(nameBox);
        panel.Children.Add(descBox);
        var dlg = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = P("Take snapshot", "拍攝快照"),
            Content = panel,
            PrimaryButtonText = P("Take", "拍攝"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var name = (nameBox.Text ?? "").Trim();
        if (name.Length == 0) return;
        Busy.IsActive = true;
        var r = await VirtualBoxService.SnapshotTake(_selected.Uuid, name, (descBox.Text ?? "").Trim());
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Take snapshot", "拍攝快照"), r.Success ? name : Msg(r));
        await RefreshSnapshots();
    }

    private async void SnapRestore_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null || SnapList.SelectedItem is not VBoxSnapshot snap) return;
        if (!_selected.IsOff)
        {
            Notify(InfoBarSeverity.Warning, P("Restore snapshot", "還原快照"), P("Power off the VM before restoring a snapshot.", "還原快照前請先關機。"));
            return;
        }
        if (!await Confirm(P("Restore snapshot?", "還原快照？"),
            P($"Restore \"{snap.Name}\". The current state will be discarded unless you snapshot it first.",
              $"還原「{snap.Name}」。除非你先拍攝，否則目前狀態會被捨棄。"),
            P("Restore", "還原"))) return;
        Busy.IsActive = true;
        var id = snap.Uuid.Length > 0 ? snap.Uuid : snap.Name;
        var r = await VirtualBoxService.SnapshotRestore(_selected.Uuid, id);
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Restore snapshot", "還原快照"), r.Success ? snap.Name : Msg(r));
        await Refresh();
        await RefreshSnapshots();
    }

    private async void SnapDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null || SnapList.SelectedItem is not VBoxSnapshot snap) return;
        if (!await Confirm(P("Delete snapshot?", "刪除快照？"),
            P($"Permanently delete snapshot \"{snap.Name}\". This cannot be undone.",
              $"永久刪除快照「{snap.Name}」，無法復原。"),
            P("Delete", "刪除"))) return;
        Busy.IsActive = true;
        var id = snap.Uuid.Length > 0 ? snap.Uuid : snap.Name;
        var r = await VirtualBoxService.SnapshotDelete(_selected.Uuid, id);
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Delete snapshot", "刪除快照"), r.Success ? snap.Name : Msg(r));
        await RefreshSnapshots();
    }

    // ── clone / delete / export ────────────────────────────────────────────────

    private async void Clone_Click(object sender, RoutedEventArgs e)
    {
        var id = Tag(sender);
        if (id.Length == 0) return;
        var src = (VmList.ItemsSource as IEnumerable<VBoxVm>)?.FirstOrDefault(v => v.Uuid == id);
        var nameBox = new TextBox { PlaceholderText = P("New VM name", "新虛擬機名稱"), Text = (src?.Name ?? "VM") + " Clone" };
        var linkedChk = new CheckBox { Content = P("Linked clone (needs a snapshot; smaller, faster)", "連結式複製（需要快照；更細更快）"), Margin = new Thickness(0, 8, 0, 0) };
        var panel = new StackPanel();
        panel.Children.Add(nameBox);
        panel.Children.Add(linkedChk);
        var dlg = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = P("Clone VM", "複製虛擬機"),
            Content = panel,
            PrimaryButtonText = P("Clone", "複製"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var name = (nameBox.Text ?? "").Trim();
        if (name.Length == 0) return;
        Busy.IsActive = true;
        var r = await VirtualBoxService.Clone(id, name, linkedChk.IsChecked == true);
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Clone VM", "複製虛擬機"), r.Success ? name : Msg(r));
        await Refresh();
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var id = Tag(sender);
        if (id.Length == 0) return;
        var src = (VmList.ItemsSource as IEnumerable<VBoxVm>)?.FirstOrDefault(v => v.Uuid == id);
        var path = await FileDialogs.SaveFileAsync((src?.Name ?? "appliance"), ".ova");
        if (path is null) return;
        Busy.IsActive = true;
        var r = await VirtualBoxService.ExportOva(id, path);
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Export OVA", "匯出 OVA"), r.Success ? path : Msg(r));
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var id = Tag(sender);
        if (id.Length == 0) return;
        var src = (VmList.ItemsSource as IEnumerable<VBoxVm>)?.FirstOrDefault(v => v.Uuid == id);
        var name = src?.Name ?? id;

        var deleteFilesChk = new CheckBox { Content = P("Also delete all disk files (permanent)", "同時刪除所有磁碟檔案（永久）"), IsChecked = true };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = P($"Remove \"{name}\" from VirtualBox. If you also delete disk files, this permanently erases the VM's hard disks and cannot be undone.",
                     $"從 VirtualBox 移除「{name}」。如果同時刪除磁碟檔案，會永久抹除虛擬機嘅硬碟，無法復原。"),
        });
        panel.Children.Add(deleteFilesChk);
        var dlg = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = P("Delete VM?", "刪除虛擬機？"),
            Content = panel,
            PrimaryButtonText = P("Delete", "刪除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        Busy.IsActive = true;
        var r = await VirtualBoxService.Unregister(id, deleteFilesChk.IsChecked == true);
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Delete VM", "刪除虛擬機"), r.Success ? name : Msg(r));
        await Refresh();
    }

    // ── create VM wizard ───────────────────────────────────────────────────────

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        Busy.IsActive = true;
        var osTypes = await VirtualBoxService.ListOsTypes();
        Busy.IsActive = false;

        var nameBox = new TextBox { PlaceholderText = P("VM name", "虛擬機名稱"), Text = "New VM" };
        var osBox = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, PlaceholderText = P("Guest OS type", "客體作業系統類型"), ItemsSource = osTypes, DisplayMemberPath = "Display" };
        if (osTypes.Count > 0)
        {
            var def = osTypes.FirstOrDefault(o => o.Id.Contains("Ubuntu", StringComparison.OrdinalIgnoreCase))
                      ?? osTypes.FirstOrDefault(o => o.Id.Contains("Windows10", StringComparison.OrdinalIgnoreCase))
                      ?? osTypes[0];
            osBox.SelectedItem = def;
        }
        var cpuBox = new NumberBox { Header = P("CPUs", "CPU 數"), Value = 2, Minimum = 1, Maximum = 64, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var ramBox = new NumberBox { Header = P("Memory (MB)", "記憶體（MB）"), Value = 2048, Minimum = 4, Maximum = 262144, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var diskBox = new NumberBox { Header = P("Disk size (MB, 0 = no disk)", "磁碟大小（MB，0 = 無磁碟）"), Value = 25600, Minimum = 0, Maximum = 4194304, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };

        var panel = new StackPanel { Spacing = 10, Width = 380 };
        panel.Children.Add(nameBox);
        panel.Children.Add(osBox);
        panel.Children.Add(cpuBox);
        panel.Children.Add(ramBox);
        panel.Children.Add(diskBox);
        var dlg = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = P("Create a new VM", "建立新虛擬機"),
            Content = new ScrollViewer { Content = panel },
            PrimaryButtonText = P("Create", "建立"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        var name = (nameBox.Text ?? "").Trim();
        var os = (osBox.SelectedItem as VBoxOsType)?.Id ?? "Other";
        if (name.Length == 0) { Notify(InfoBarSeverity.Warning, P("Create VM", "建立虛擬機"), P("Enter a name.", "請輸入名稱。")); return; }
        int cpus = (int)Math.Round(double.IsNaN(cpuBox.Value) ? 1 : cpuBox.Value);
        long mem = (long)Math.Round(double.IsNaN(ramBox.Value) ? 2048 : ramBox.Value);
        long disk = (long)Math.Round(double.IsNaN(diskBox.Value) ? 0 : diskBox.Value);

        Busy.IsActive = true;
        var r = await VirtualBoxService.CreateVm(name, os, cpus, mem, disk);
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Create VM", "建立虛擬機"), r.Success ? name : Msg(r));
        await Refresh();
    }

    // ── import OVA ─────────────────────────────────────────────────────────────

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".ova", ".ovf");
        if (path is null) return;

        Busy.IsActive = true;
        var preview = await VirtualBoxService.ImportPreview(path);
        Busy.IsActive = false;

        var nameBox = new TextBox { PlaceholderText = P("New VM name (optional — keep blank for default)", "新虛擬機名稱（選填 — 留空用預設）") };
        var previewBox = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), FontSize = 12,
            Text = string.IsNullOrWhiteSpace(preview) ? P("(no preview available)", "（無預覽）") : preview,
            MaxHeight = 220, Margin = new Thickness(0, 8, 0, 0),
        };
        var panel = new StackPanel { Width = 460 };
        panel.Children.Add(nameBox);
        panel.Children.Add(new TextBlock { Text = P("Dry-run preview:", "乾跑預覽："), Margin = new Thickness(0, 8, 0, 0), Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        panel.Children.Add(previewBox);
        var dlg = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = P("Import OVA appliance", "匯入 OVA"),
            Content = new ScrollViewer { Content = panel },
            PrimaryButtonText = P("Import", "匯入"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        Busy.IsActive = true;
        var r = await VirtualBoxService.ImportOva(path, (nameBox.Text ?? "").Trim());
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Import OVA", "匯入 OVA"), r.Success ? path : Msg(r));
        await Refresh();
    }

    // ── host info ──────────────────────────────────────────────────────────────

    private async void HostRefresh_Click(object sender, RoutedEventArgs e)
    {
        Busy.IsActive = true;
        var info = await VirtualBoxService.HostInfo();
        Busy.IsActive = false;
        HostInfoBox.Text = string.IsNullOrWhiteSpace(info) ? P("(no host info)", "（無主機資訊）") : info;
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private async Task<bool> Confirm(string title, string body, string primary)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = title,
            Content = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primary,
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    private static string Msg(Models.TweakResult r)
    {
        var m = (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";
        var o = r.Output;
        return string.IsNullOrWhiteSpace(o) ? m : $"{m}\n{o}".Trim();
    }

    private void Notify(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev; ResultBar.Title = title; ResultBar.Message = msg; ResultBar.IsOpen = true;
    }
}
