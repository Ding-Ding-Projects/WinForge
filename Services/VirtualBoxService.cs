using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一部 VirtualBox 虛擬機 · One VirtualBox virtual machine row.</summary>
public sealed class VBoxVm
{
    public string Name { get; set; } = "";
    public string Uuid { get; set; } = "";
    /// <summary>VBoxManage 狀態字串（running / paused / poweroff / saved / aborted…）· Raw VBoxManage state.</summary>
    public string State { get; set; } = "poweroff";
    public string OsType { get; set; } = "";
    public int Cpus { get; set; }
    public long MemoryMb { get; set; }
    public bool DetailLoaded { get; set; }

    public bool IsRunning => State.Equals("running", StringComparison.OrdinalIgnoreCase);
    public bool IsPaused => State.Equals("paused", StringComparison.OrdinalIgnoreCase);
    public bool IsSaved => State.Equals("saved", StringComparison.OrdinalIgnoreCase);
    public bool IsOff => !IsRunning && !IsPaused;

    public string StateEn => State.ToLowerInvariant() switch
    {
        "running" => "Running",
        "paused" => "Paused",
        "saved" => "Saved",
        "poweroff" => "Powered off",
        "aborted" => "Aborted",
        "stuck" => "Stuck",
        "starting" => "Starting",
        "stopping" => "Stopping",
        _ => State,
    };

    public string StateZh => State.ToLowerInvariant() switch
    {
        "running" => "運行中",
        "paused" => "已暫停",
        "saved" => "已儲存",
        "poweroff" => "已關機",
        "aborted" => "已中止",
        "stuck" => "卡住",
        "starting" => "啟動中",
        "stopping" => "停止中",
        _ => State,
    };
}

/// <summary>一個快照 · One snapshot node.</summary>
public sealed class VBoxSnapshot
{
    public string Name { get; set; } = "";
    public string Uuid { get; set; } = "";
    public int Depth { get; set; }
    public bool IsCurrent { get; set; }

    public string Display => $"{new string(' ', Depth * 2)}{(IsCurrent ? "● " : "○ ")}{Name}";
}

/// <summary>一個 OS 類型（畀建立 VM 用）· One guest OS type for the create-VM wizard.</summary>
public sealed class VBoxOsType
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public string Display => string.IsNullOrEmpty(Description) || Description == Id ? Id : $"{Description} ({Id})";
}

/// <summary>
/// VirtualBox 管理（包 VBoxManage.exe 命令列）· VirtualBox manager wrapping the bundled VBoxManage.exe.
/// 偵測安裝路徑、列出／控制 VM、快照、修改 CPU/RAM、建立／複製／刪除、匯入／匯出 OVA、主機資訊。
/// Locates VBoxManage, parses --machinereadable key="value" output, and exposes async list/control
/// methods for the full inventory + lifecycle loop. No redirect; all heavy lifting is the CLI's. Bilingual.
/// </summary>
public static class VirtualBoxService
{
    private static string? _cachedPath;
    private static bool _probed;

    /// <summary>清除快取嘅 VBoxManage 路徑（安裝完之後重掃）· Clear the cached path after an install so the UI re-lights.</summary>
    public static void Rescan()
    {
        _cachedPath = null;
        _probed = false;
    }

    /// <summary>搵 VBoxManage.exe（環境變數 → 常見路徑 → PATH）· Locate VBoxManage.exe.</summary>
    public static string? FindVBoxManage()
    {
        if (_probed) return _cachedPath;
        _probed = true;

        var candidates = new List<string>();

        // 1) %VBOX_MSI_INSTALL_PATH% / %VBOX_INSTALL_PATH%
        foreach (var ev in new[] { "VBOX_MSI_INSTALL_PATH", "VBOX_INSTALL_PATH" })
        {
            var dir = Environment.GetEnvironmentVariable(ev);
            if (!string.IsNullOrWhiteSpace(dir))
                candidates.Add(Path.Combine(dir, "VBoxManage.exe"));
        }

        // 2) common install locations
        foreach (var root in new[]
                 {
                     Environment.GetEnvironmentVariable("ProgramFiles"),
                     Environment.GetEnvironmentVariable("ProgramW6432"),
                     @"C:\Program Files",
                 })
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            candidates.Add(Path.Combine(root, "Oracle", "VirtualBox", "VBoxManage.exe"));
        }

        foreach (var c in candidates)
        {
            try { if (File.Exists(c)) { _cachedPath = c; return _cachedPath; } } catch { }
        }

        // 3) bare name on PATH
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var p = Path.Combine(dir.Trim(), "VBoxManage.exe");
                if (File.Exists(p)) { _cachedPath = p; return _cachedPath; }
            }
            catch { }
        }

        _cachedPath = null;
        return null;
    }

    public static bool IsAvailable() => FindVBoxManage() is not null;

    /// <summary>取得 VirtualBox 版本字串 · Get the VirtualBox version string, or null.</summary>
    public static async Task<string?> GetVersion(CancellationToken ct = default)
    {
        var exe = FindVBoxManage();
        if (exe is null) return null;
        var r = await ShellRunner.Run(exe, "--version", false, ct);
        var v = (r.Output ?? "").Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    private static async Task<(bool ok, string output)> Capture(string args, CancellationToken ct)
    {
        var exe = FindVBoxManage();
        if (exe is null) return (false, "");
        var r = await ShellRunner.Run(exe, args, false, ct);
        return (r.Success, r.Output ?? "");
    }

    /// <summary>執行一個會改變狀態嘅 VBoxManage 指令 · Run a state-changing VBoxManage command, returning a TweakResult.</summary>
    private static async Task<TweakResult> Run(string args, CancellationToken ct)
    {
        var exe = FindVBoxManage();
        if (exe is null) return TweakResult.Fail("VBoxManage was not found.", "搵唔到 VBoxManage。");
        return await ShellRunner.Run(exe, args, false, ct);
    }

    // ── inventory ────────────────────────────────────────────────────────────

    /// <summary>列出所有 VM（合併運行中清單 + 每部詳情）· List all VMs joined with running set + per-VM detail.</summary>
    public static async Task<List<VBoxVm>> ListVms(bool withDetail = true, CancellationToken ct = default)
    {
        var vms = new List<VBoxVm>();
        var (ok, outp) = await Capture("list vms", ct);
        if (!ok && outp.Length == 0) return vms;

        foreach (var (name, uuid) in ParseNameUuidList(outp))
            vms.Add(new VBoxVm { Name = name, Uuid = uuid });

        // mark running ones
        var (rok, rout) = await Capture("list runningvms", ct);
        if (rok)
        {
            var running = ParseNameUuidList(rout).Select(t => t.uuid).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var vm in vms.Where(v => running.Contains(v.Uuid)))
                vm.State = "running";
        }

        if (withDetail)
        {
            foreach (var vm in vms)
            {
                try { await LoadDetail(vm, ct); } catch { }
            }
        }
        return vms.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>讀取一部 VM 嘅詳細狀態（--machinereadable）· Load state, CPUs and RAM for one VM.</summary>
    public static async Task LoadDetail(VBoxVm vm, CancellationToken ct = default)
    {
        var id = vm.Uuid.Length > 0 ? vm.Uuid : vm.Name;
        var (ok, outp) = await Capture($"showvminfo {Quote(id)} --machinereadable", ct);
        if (!ok && outp.Length == 0) return;
        var map = ParseMachineReadable(outp);

        if (map.TryGetValue("VMState", out var st) && st.Length > 0) vm.State = st;
        if (map.TryGetValue("cpus", out var cpus) && int.TryParse(cpus, out var c)) vm.Cpus = c;
        if (map.TryGetValue("memory", out var mem) && long.TryParse(mem, out var m)) vm.MemoryMb = m;
        if (map.TryGetValue("ostype", out var os) && os.Length > 0) vm.OsType = os;
        if (map.TryGetValue("name", out var nm) && nm.Length > 0) vm.Name = nm;
        vm.DetailLoaded = true;
    }

    // ── power control ────────────────────────────────────────────────────────

    public static Task<TweakResult> StartGui(string id, CancellationToken ct = default)
        => Run($"startvm {Quote(id)} --type gui", ct);

    public static Task<TweakResult> StartHeadless(string id, CancellationToken ct = default)
        => Run($"startvm {Quote(id)} --type headless", ct);

    public static Task<TweakResult> SaveState(string id, CancellationToken ct = default)
        => Run($"controlvm {Quote(id)} savestate", ct);

    public static Task<TweakResult> PowerOff(string id, CancellationToken ct = default)
        => Run($"controlvm {Quote(id)} poweroff", ct);

    public static Task<TweakResult> Pause(string id, CancellationToken ct = default)
        => Run($"controlvm {Quote(id)} pause", ct);

    public static Task<TweakResult> Resume(string id, CancellationToken ct = default)
        => Run($"controlvm {Quote(id)} resume", ct);

    public static Task<TweakResult> AcpiPowerButton(string id, CancellationToken ct = default)
        => Run($"controlvm {Quote(id)} acpipowerbutton", ct);

    public static Task<TweakResult> Reset(string id, CancellationToken ct = default)
        => Run($"controlvm {Quote(id)} reset", ct);

    // ── modify CPUs / RAM (powered-off only) ───────────────────────────────────

    public static Task<TweakResult> Modify(string id, int cpus, long memoryMb, CancellationToken ct = default)
        => Run($"modifyvm {Quote(id)} --cpus {cpus} --memory {memoryMb}", ct);

    // ── delete / unregister ────────────────────────────────────────────────────

    public static Task<TweakResult> Unregister(string id, bool deleteFiles, CancellationToken ct = default)
        => Run($"unregistervm {Quote(id)}{(deleteFiles ? " --delete" : "")}", ct);

    // ── snapshots ──────────────────────────────────────────────────────────────

    public static Task<TweakResult> SnapshotTake(string id, string name, string? description, CancellationToken ct = default)
    {
        var desc = string.IsNullOrWhiteSpace(description) ? "" : $" --description {Quote(description!)}";
        return Run($"snapshot {Quote(id)} take {Quote(name)}{desc}", ct);
    }

    public static Task<TweakResult> SnapshotRestore(string id, string nameOrUuid, CancellationToken ct = default)
        => Run($"snapshot {Quote(id)} restore {Quote(nameOrUuid)}", ct);

    public static Task<TweakResult> SnapshotRestoreCurrent(string id, CancellationToken ct = default)
        => Run($"snapshot {Quote(id)} restorecurrent", ct);

    public static Task<TweakResult> SnapshotDelete(string id, string nameOrUuid, CancellationToken ct = default)
        => Run($"snapshot {Quote(id)} delete {Quote(nameOrUuid)}", ct);

    /// <summary>列出一部 VM 嘅快照樹 · Parse the snapshot tree for one VM.</summary>
    public static async Task<List<VBoxSnapshot>> ListSnapshots(string id, CancellationToken ct = default)
    {
        var list = new List<VBoxSnapshot>();
        var (ok, outp) = await Capture($"snapshot {Quote(id)} list --machinereadable", ct);
        if (!ok || outp.Trim().Length == 0) return list; // "does not have any snapshots"
        var map = ParseMachineReadable(outp);

        // keys look like: SnapshotName="root", SnapshotName-1="child", SnapshotName-1-1="grandchild"
        // plus SnapshotUUID with matching suffix, and CurrentSnapshotUUID / CurrentSnapshotName.
        var currentUuid = map.TryGetValue("CurrentSnapshotUUID", out var cu) ? cu : "";
        foreach (var kvp in map)
        {
            if (!kvp.Key.StartsWith("SnapshotName", StringComparison.Ordinal)) continue;
            var suffix = kvp.Key.Substring("SnapshotName".Length); // "" or "-1-2-3"
            int depth = suffix.Length == 0 ? 0 : suffix.Split('-', StringSplitOptions.RemoveEmptyEntries).Length;
            var uuid = map.TryGetValue("SnapshotUUID" + suffix, out var u) ? u : "";
            list.Add(new VBoxSnapshot
            {
                Name = kvp.Value,
                Uuid = uuid,
                Depth = depth,
                IsCurrent = uuid.Length > 0 && string.Equals(uuid, currentUuid, StringComparison.OrdinalIgnoreCase),
            });
        }
        // order by suffix order (roughly tree order) — keep insertion order which is already tree order
        return list;
    }

    // ── clone ────────────────────────────────────────────────────────────────

    public static Task<TweakResult> Clone(string id, string newName, bool linked, CancellationToken ct = default)
    {
        // linked clones require a snapshot; --options link only works off a snapshot. Fall back to full.
        var mode = linked ? " --options link" : "";
        return Run($"clonevm {Quote(id)} --name {Quote(newName)} --register{mode}", ct);
    }

    // ── import / export OVA ────────────────────────────────────────────────────

    public static Task<TweakResult> ImportOva(string ovaPath, string? vmName, CancellationToken ct = default)
    {
        var name = string.IsNullOrWhiteSpace(vmName) ? "" : $" --vsys 0 --vmname {Quote(vmName!)}";
        return Run($"import {Quote(ovaPath)}{name}", ct);
    }

    /// <summary>OVA 匯入乾跑預覽（唔會真係匯入）· Dry-run preview of an OVA import.</summary>
    public static async Task<string> ImportPreview(string ovaPath, CancellationToken ct = default)
    {
        var (_, outp) = await Capture($"import {Quote(ovaPath)} --dry-run", ct);
        return outp;
    }

    public static Task<TweakResult> ExportOva(string id, string ovaPath, CancellationToken ct = default)
        => Run($"export {Quote(id)} -o {Quote(ovaPath)}", ct);

    // ── create VM ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 建立一部新 VM（createvm + modifyvm + 可選硬碟 + 控制器）· Create a new VM end-to-end:
    /// register, set OS type / CPU / RAM, optionally create a VDI disk and attach it on a SATA controller.
    /// </summary>
    public static async Task<TweakResult> CreateVm(string name, string osType, int cpus, long memoryMb,
        long diskSizeMb, CancellationToken ct = default)
    {
        var exe = FindVBoxManage();
        if (exe is null) return TweakResult.Fail("VBoxManage was not found.", "搵唔到 VBoxManage。");

        var create = await Run($"createvm --name {Quote(name)} --ostype {Quote(osType)} --register", ct);
        if (!create.Success) return create;

        var mod = await Run($"modifyvm {Quote(name)} --cpus {cpus} --memory {memoryMb} --boot1 disk --boot2 dvd " +
                            "--nic1 nat --graphicscontroller vmsvga", ct);
        if (!mod.Success) return mod;

        if (diskSizeMb > 0)
        {
            // figure out the VM folder for the .vdi
            string vdiPath;
            var (ok, info) = await Capture($"showvminfo {Quote(name)} --machinereadable", ct);
            var map = ParseMachineReadable(info);
            if (ok && map.TryGetValue("CfgFile", out var cfg) && cfg.Length > 0)
                vdiPath = Path.Combine(Path.GetDirectoryName(cfg) ?? "", $"{name}.vdi");
            else
                vdiPath = Path.Combine(Path.GetTempPath(), $"{name}.vdi");

            var disk = await Run($"createmedium disk --filename {Quote(vdiPath)} --size {diskSizeMb} --format VDI", ct);
            if (!disk.Success) return disk;

            var ctl = await Run($"storagectl {Quote(name)} --name SATA --add sata --controller IntelAhci", ct);
            if (!ctl.Success) return ctl;

            var attach = await Run($"storageattach {Quote(name)} --storagectl SATA --port 0 --device 0 " +
                                   $"--type hdd --medium {Quote(vdiPath)}", ct);
            if (!attach.Success) return attach;
        }

        return TweakResult.Ok($"Created VM \"{name}\".", $"已建立虛擬機「{name}」。");
    }

    /// <summary>列出可用嘅 OS 類型 · Parse `list ostypes`.</summary>
    public static async Task<List<VBoxOsType>> ListOsTypes(CancellationToken ct = default)
    {
        var list = new List<VBoxOsType>();
        var (ok, outp) = await Capture("list ostypes", ct);
        if (!ok) return list;

        string id = "", desc = "";
        foreach (var raw in outp.Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
                id = line.Substring(3).Trim();
            else if (line.StartsWith("Description:", StringComparison.OrdinalIgnoreCase))
            {
                desc = line.Substring("Description:".Length).Trim();
                if (id.Length > 0) { list.Add(new VBoxOsType { Id = id, Description = desc }); id = ""; desc = ""; }
            }
        }
        return list;
    }

    // ── host info ──────────────────────────────────────────────────────────────

    /// <summary>主機資訊（原始文字）· Host info as raw text from `list hostinfo`.</summary>
    public static async Task<string> HostInfo(CancellationToken ct = default)
    {
        var (_, outp) = await Capture("list hostinfo", ct);
        return outp.Trim();
    }

    // ── parsing helpers ────────────────────────────────────────────────────────

    /// <summary>解析 `key="value"` 行 · Parse VBoxManage --machinereadable key="value" lines.</summary>
    private static Dictionary<string, string> ParseMachineReadable(string text)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line.Substring(0, eq).Trim().Trim('"');
            var val = line.Substring(eq + 1).Trim();
            if (val.Length >= 2 && val.StartsWith("\"") && val.EndsWith("\""))
                val = val.Substring(1, val.Length - 2);
            map[key] = val;
        }
        return map;
    }

    /// <summary>解析 `"Name" {uuid}` 清單 · Parse `"VM name" {uuid}` lines from list vms/runningvms.</summary>
    private static List<(string name, string uuid)> ParseNameUuidList(string text)
    {
        var list = new List<(string, string)>();
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            // format: "Ubuntu 22.04" {a1b2c3...}
            int brace = line.LastIndexOf('{');
            string name, uuid = "";
            if (brace >= 0)
            {
                var namePart = line.Substring(0, brace).Trim().Trim('"');
                var uuidPart = line.Substring(brace).Trim().TrimStart('{').TrimEnd('}');
                name = namePart;
                uuid = uuidPart;
            }
            else name = line.Trim('"');
            if (name.Length > 0) list.Add((name, uuid));
        }
        return list;
    }

    private static string Quote(string s) => "\"" + (s ?? "").Replace("\"", "") + "\"";
}
