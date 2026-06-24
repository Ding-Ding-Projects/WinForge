# Handoff: TestDisk / PhotoRec data recovery

| | |
|---|---|
| **Status** | Not started |
| **Source** | CLI: `photorec_win.exe` / `testdisk_win.exe` — https://git.cgsecurity.org/cgit/testdisk/ (download builds: https://www.cgsecurity.org/wiki/TestDisk_Download) |
| **License** | GPLv2+ (open source). We only invoke the bundled `.exe` binaries, but the binaries are downloaded at runtime (not committed) to keep the repo clean and respect GPL redistribution. |
| **Proposed module** | TestDisk / PhotoRec Recovery · "Disk & Files" group · Tag `module.testdisk` |
| **Effort** | L — the binaries are interactive ncurses CLIs by default; v1 must drive them in scriptable/non-interactive mode (`/cmd`, `/log`, `/d`) and parse text output/result files. Moderate UI plus careful disk enumeration. |

## What the user asked for
A WinUI front-end wrapping the TestDisk (partition recovery) and PhotoRec (file carving) C CLIs: pick a physical disk or image file, run PhotoRec to carve recoverable files of selected types into an output folder, run a TestDisk partition scan, and show progress and a log. Bundle/download the cgsecurity binaries (no winget).

## Recommended approach
**CLI/binary wrap.** Per the global strategy, TestDisk/PhotoRec is a large, mature C codebase doing low-level filesystem and file-signature carving across hundreds of formats — reimplementing that natively is far out of scope. So wrap the official `photorec_win.exe` / `testdisk_win.exe` and build a rich WinUI front-end. The binaries default to an interactive ncurses TUI; the key is to drive them non-interactively. PhotoRec supports `photorec_win /log /d <outdir> /cmd <device> <options>` where options chain like `partition_none,fileopt,everything,enable,search`; TestDisk supports `testdisk_win /log /list` and `/cmd <device> advanced` for scriptable scans. v1 scope: download binaries, enumerate disks, run a PhotoRec carve with file-type selection to a chosen output folder, run a TestDisk read-only partition scan, stream the log, and show carved-file count/progress. Never launch the TUI; WinForge provides the whole UI (no external redirects).

## Features to implement (v1 → later)
- v1: Detect/download binaries to `%LOCALAPPDATA%\WinForge\testdisk` (cgsecurity zip, extract `*_win.exe`); enumerate physical disks (`\\.\PhysicalDriveN` via WMI `Win32_DiskDrive`) plus an "image file" picker; PhotoRec carve via `/log /d <out> /cmd <dev> partition_none,options,fileopt,<types>,enable,search`; file-type checklist (jpg, png, pdf, docx, zip, mp4, etc. — parse from `photorec /cmd <dev> fileopt`); TestDisk read-only scan via `/log /list` and `/cmd <dev> advanced`; live log pane (tail `photorec.log`/`testdisk.log` + redirected stdout); recovered-file count + open-output-folder button; destructive-action and "recover to a DIFFERENT disk" warnings.
- later: Resume an interrupted PhotoRec session (`photorec.ses`); TestDisk partition rewrite / boot-sector repair (gated behind strong confirmations); preview/thumbnail of carved files; whole-disk image creation; ext/NTFS undelete via TestDisk; saved profiles; per-type recovery stats.

## Integration plan (WinForge specifics)
- New files: `Services/TestDiskService.cs` (locate/download/extract binaries, enumerate drives, build `/cmd` argument strings, run via `ShellRunner.Run`/`Capture`, parse logs and result counts, async `RunPhotoRec`/`RunTestDiskScan` returning bilingual `TweakResult`); `Pages/TestDiskModule.xaml(+.cs)` (engine InfoBar, disk/image picker, file-type `CheckBox` list, output-folder picker, Run buttons, log `ScrollViewer`).
- Nav wiring: add a `NavigationViewItem` Tag `module.testdisk` in `MainWindow.xaml` (Disk & Files group); add a `ModuleRegistry` entry (`Services/ModuleRegistry.cs`) for master search (En "TestDisk / PhotoRec Recovery", Zh "TestDisk / PhotoRec 資料救援", keywords: `testdisk photorec recovery carve undelete partition recover data lost deleted 資料救援 救援 復原 還原 分割區 救回 刪除 檔案`); wire the Tag in `MainWindow.xaml.cs` (`MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page module.testdisk`).
- Engine/install: winget id `n/a`. Download the binaries from cgsecurity at runtime (no `AutoInstallButton`); add a custom "Download recovery tools" InfoBar button that fetches + extracts the zip, then rescans.
- Key CLIs to call: `photorec_win /log /d <out> /cmd <dev> partition_none,fileopt,<types>,enable,search`; `testdisk_win /log /list`; `testdisk_win /log /cmd <dev> advanced`. Verify exact `/cmd` token spellings against the bundled version. Use `FileDialogs` for image-file and output-folder selection (never WinRT pickers).

## Dependencies & risks
- Requires admin/elevation to open `\\.\PhysicalDrive*` raw devices — surface a clear bilingual prompt; WinForge already runs elevated in many flows.
- Data-safety: PhotoRec must write to a DIFFERENT disk than the one being recovered; enforce/validate this and warn loudly. TestDisk write operations can destroy partition tables — keep v1 read-only.
- The `/cmd` scripting syntax and file-type tokens vary across releases; confirm against the downloaded `--help`/`fileopt` listing before hardcoding.
- Long-running scans on large disks; run async with cancellation and live log tailing, never block the UI thread.
- Binary download integrity: verify the cgsecurity zip and pin a known-good version; no winget fallback exists.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav and master search; binaries download/extract on first use; disks and image files enumerate; PhotoRec carves selected file types to a user-chosen folder with live log + recovered count; TestDisk read-only partition scan lists partitions; same-disk recovery is blocked with a warning; every user-facing string is bilingual (English + 粵語); no WinRT pickers (FileDialogs only).
