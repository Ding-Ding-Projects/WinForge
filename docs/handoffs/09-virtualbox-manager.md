# Handoff: VirtualBox Manager

| | |
|---|---|
| **Status** | Not started |
| **Source** | CLI: `VBoxManage` (Oracle VirtualBox command-line) — https://www.virtualbox.org |
| **License** | VirtualBox base package is GPLv3 (open source); the Extension Pack is PUEL (proprietary, optional, not required for this module). We only shell out to the CLI, so no licensing impact on WinForge. |
| **Proposed module** | VirtualBox Manager · "WSL & VM Launcher" group (sits beside `module.wslvm`) · Tag `module.virtualbox` |
| **Effort** | M — VBoxManage output parsing + a rich list UI is moderate; no native hypervisor work, all heavy lifting is delegated to the CLI. |

## What the user asked for
A VirtualBox module that wraps `VBoxManage`: list VMs with running state, control power (start / headless start / save / poweroff / pause / resume), create / clone / delete VMs, manage snapshots (take / list / restore / delete), modify CPU and memory, import/export OVA appliances, and show host info — presented as a rich WinUI list with per-VM actions, installing VirtualBox via winget if it is missing.

## Recommended approach
**CLI/binary wrap.** Per the global strategy, VirtualBox is a large C/C++ hypervisor with kernel drivers — reimplementing it natively is out of scope. We wrap the bundled `VBoxManage.exe` and build a rich WinUI front-end. A v1 should cover the full inventory + lifecycle loop (list, power control, snapshots, basic modify) plus install detection. OVA import/export and create/clone are realistic in v1 too since they are single CLI calls; advanced device editing (NICs, storage controllers, shared folders) is later.

## Features to implement (v1 → later)
- **v1:** Detect VirtualBox (locate `VBoxManage.exe` via `%VBOX_MSI_INSTALL_PATH%` / `C:\Program Files\Oracle\VirtualBox\` / PATH); AutoInstallButton if missing. List all VMs (`list vms`) joined with running VMs (`list runningvms`) and per-VM detail (`showvminfo <uuid> --machinereadable`) for state, CPUs, RAM. Per-VM actions: start (`startvm <id> --type gui`), headless start (`--type headless`), save state (`controlvm <id> savestate`), power off (`controlvm <id> poweroff`), pause/resume (`controlvm <id> pause|resume`). Snapshots: take (`snapshot <id> take <name>`), list (`snapshot <id> list --machinereadable`), restore (`snapshot <id> restore <name>`), delete (`snapshot <id> delete <name>`). Modify CPUs/RAM (`modifyvm <id> --cpus N --memory MB`, VM must be powered off). Delete VM (`unregistervm <id> --delete`). Host info (`list hostinfo`, `list ostypes`). Confirmation dialogs for destructive actions.
- **later:** Create new VM wizard (`createvm` + `modifyvm` + `createmedium` + `storagectl`/`storageattach`). Clone (`clonevm <id> --name <new> --register`, full/linked). Import OVA (`import <file.ova>` with a dry-run preview) and export (`export <id> -o <file.ova>`). NIC/network mode editing, shared folders, USB filters, live snapshot tree view, guest control, screenshot of VM (`controlvm <id> screenshotpng`).

## Integration plan (WinForge specifics)
- **New files:** `Services/VirtualBoxService.cs` (locate VBoxManage, run commands via `ShellRunner.Run`, parse `--machinereadable` key="value" output into model objects, async list/control methods), `Pages/VirtualBoxModule.xaml` + `.xaml.cs` (engine InfoBar at top, ListView/`ItemsRepeater` of VMs with state badge + action buttons, snapshot sub-panel, host-info expander).
- **Nav wiring:** add `NavigationViewItem` with `Tag="module.virtualbox"` in `MainWindow.xaml` near the `module.wslvm` item; add a `ModuleRegistry.All` entry (`Tag="module.virtualbox"`, En="VirtualBox Manager", Zh="VirtualBox 管理", Glyph `0xEC7A`, Keywords: `virtualbox vbox vboxmanage vm virtual machine snapshot clone ova headless oracle 虛擬機 虛擬機器 快照 複製 匯入 匯出`); map the Tag in `MainWindow.xaml.cs` (`MapType`, `NavView_SelectionChanged`) and add to `ApplyStartPage` for `--page module.virtualbox` deep-links.
- **Engine/install:** winget id `Oracle.VirtualBox` via `EngineBars.AutoInstallButton("Oracle.VirtualBox", "Install VirtualBox", "安裝 VirtualBox", recheck, rescan)`; `rescan` clears the cached VBoxManage path so the UI lights up without a restart.
- **Key CLIs to call:** `VBoxManage list vms|runningvms|hostinfo|ostypes`, `showvminfo --machinereadable`, `startvm`, `controlvm`, `modifyvm`, `snapshot`, `clonevm`, `import`, `export`, `unregistervm`, `createvm`. Use `FileDialogs` (never WinRT pickers) for OVA open/save paths.

## Dependencies & risks
- VirtualBox needs admin to install and is mutually exclusive with running Hyper-V/WSL2 hypervisor in some configs — surface a clear bilingual warning if VMs fail to start with VT-x/Hyper-V errors.
- `modifyvm` only works on powered-off VMs; gate the modify UI on state and message the user.
- Output parsing: prefer `--machinereadable` (stable `key="value"` lines) over human output. Handle non-ASCII VM names (UTF-8 is already set in ShellRunner).
- Destructive ops (`poweroff`, `unregistervm --delete`, snapshot delete) need confirmation dialogs; `--delete` removes disk files permanently.
- VBoxManage path varies by version/install location — do not hard-code; probe env var + common paths + PATH.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in the left nav and master search; install InfoBar appears when VirtualBox is absent and AutoInstall works; listing shows VMs with correct running/paused/off state; start / headless / save / poweroff / pause / resume / snapshot take-list-restore-delete / modify CPUs+RAM all function; OVA import/export and delete use `FileDialogs` and confirmation; every user-facing string is bilingual (English + 粵語); no WinRT pickers used.
