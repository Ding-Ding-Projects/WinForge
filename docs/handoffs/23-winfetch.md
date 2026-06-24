# Handoff: Winfetch (System Info)

| | |
|---|---|
| **Status** | Not started |
| **Source** | https://github.com/lptstr/winfetch (PowerShell) |
| **License** | MIT (open source) |
| **Proposed module** | System Info (Winfetch) · "System" / "Info" nav group · Tag `module.winfetch` |
| **Effort** | M — mostly data plumbing from existing SystemInfo service into a styled panel; ASCII export is the only novel bit. |

## What the user asked for
A native clone of winfetch: a system-info panel that shows OS, host, kernel, uptime, packages, shell, resolution, desktop environment, CPU, GPU, memory, and disk next to a Windows logo. An optional ASCII/console output mode that mimics winfetch's classic look.

## Recommended approach
**Native C# clone.** Per the global strategy this is the ideal case — winfetch is a single PowerShell script that just queries WMI/CIM/registry and prints them. No reason to wrap PowerShell. WinForge already has a `SystemInfo` service; reuse it and add any missing fields. v1 scope: a WinUI panel with the colored Windows logo on the left and a key/value info column on the right, refreshed on load. The ASCII/console mode (renders the same data as monospace text with an ASCII Windows logo for copy/paste) is a stretch goal but cheap to add once the data layer exists.

## Features to implement (v1 → later)
- v1: Info rows — OS (edition + build), Host (manufacturer/model), Kernel (NT version), Uptime, Packages (winget/scoop/choco counts), Shell (PowerShell version), Resolution (per-monitor), DE/WM (Windows shell + theme), CPU (name + cores/clock), GPU(s), Memory (used/total), Disk (per-volume used/total).
- v1: Colored Windows logo (XAML/SVG) beside the info column; "Copy to clipboard" button (plain text).
- later: ASCII/console output mode (monospace + ASCII logo); custom field toggles; accent-color picker matching logo; export to PNG; battery/locale/terminal-font rows.

## Integration plan (WinForge specifics)
- New files: `Pages/WinfetchModule.xaml(.cs)`; extend or add `Services/SystemInfoService.cs` (reuse existing SystemInfo service; add missing fields like package counts and uptime).
- Data sources: `System.Management` (Win32_OperatingSystem, Win32_Processor, Win32_VideoController, Win32_LogicalDisk, Win32_ComputerSystem), `Microsoft.Win32` registry (build/edition), `GetTickCount64`/`Environment` for uptime, and `ShellRunner.Capture` for `winget list`/`scoop list`/`choco list` counts.
- Nav wiring: add NavigationViewItem `Tag="module.winfetch"` in MainWindow.xaml; add a ModuleRegistry entry (English + Cantonese title/keywords) for master search; map the Tag in MainWindow.xaml.cs `MapType` and `NavView_SelectionChanged`; add to `ApplyStartPage` for `--page winfetch`.
- Engine/install: winget id `n/a` (no external binary). Package counts degrade gracefully if a manager is absent — no AutoInstallButton needed.
- Bilingual labels: e.g. "Operating System / 作業系統", "Uptime / 開機時間", "Resolution / 解像度", "Memory / 記憶體", "Disk / 磁碟", "Copy / 複製".

## Dependencies & risks
- WMI/CIM queries can be slow or throw on locked-down machines — query off the UI thread and wrap each field in try/catch so one failure does not blank the panel.
- Package-manager counts depend on optional CLIs; show a dash when missing.
- Multi-GPU / multi-monitor / multi-disk must render as repeated rows, not just the first.
- ASCII mode column alignment with CJK characters needs monospace handling.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in the nav and master search; panel loads all info rows with the Windows logo; copy-to-clipboard works; every label is bilingual (English + Cantonese); uses FileDialogs (not WinRT pickers) if any file export is added; missing data degrades gracefully instead of crashing.
