# Handoff: Rufus USB imager

| | |
|---|---|
| **Status** | Not started |
| **Source** | https://github.com/pbatard/rufus (C) · winget `Rufus.Rufus` |
| **License** | GPLv3 (open source). We do not link or fork Rufus code; we natively re-implement the basic write path in C# and optionally launch the user-installed Rufus binary for advanced boot options. |
| **Proposed module** | Extend existing **Imaging & Game Tools · 燒錄與遊戲工具** · "Imaging" group · Tag `module.imaging` (no new Tag) |
| **Effort** | M — most plumbing already exists (`ImagingService.ListDisks`/`WriteImage`, drive guards, heavy confirm, `AdminHelper`). New work is a write-verify pass, a USB-focused view, and the Rufus launch/install action. |

## What the user asked for
Add Rufus-style USB imaging. Extend the existing Imaging & Game Tools module to write ISO/IMG to removable USB drives natively (list removable drives, write + verify), and offer launching Rufus (`winget Rufus.Rufus`) for advanced boot options (UEFI/MBR scheme, persistence, bootable Windows/Linux media). Requires admin.

## Recommended approach
**Hybrid (native write + wrap).** Per the global strategy we prefer a native C# clone, and the core "raw write an image to a removable disk" path is already implemented in `Services/ImagingService.cs` (used by the Raspberry Pi tab). So v1 reuses that engine and adds a **read-back verify** pass and a USB-oriented tab. The parts Rufus does that are genuinely out of reasonable C# scope — building bootable filesystems, MBR/GPT partition scheme + target-system (BIOS/UEFI) selection, syslinux/GRUB/ms-sys boot records, NTFS/FAT32 formatting with bootloaders, Windows ISO `install.wim` splitting, persistence partitions — are NOT re-implemented; for those we wrap the real Rufus binary (install via winget, then launch). This keeps a fully in-app native flow for the common "flash an ISO to USB and verify" case while still giving users Rufus's full power without leaving WinForge beyond launching the wrapped tool.

## Features to implement (v1 → later)
- v1: A new **"USB imager (Rufus)"** tab inside `ImagingGameModule`. List removable USB disks (`ImagingService.ListDisks()` filtered to `LooksSafeTarget`/removable; never the system/boot disk). Pick an `.iso`/`.img`/`.bin` via `FileDialogs`. Native raw write reusing `ImagingService.WriteImage` with the existing heavy confirmation (type-the-disk-number) + admin guard + image-too-big check. **Add write-verify**: after writing, re-read the disk and compare against the image (hash or streamed byte compare) with a progress bar and pass/fail result. An engine InfoBar showing whether Rufus is installed, with `EngineBars.AutoInstallButton("Rufus.Rufus", …)` to install it, and a "Launch Rufus" button for advanced options.
- later: Pass-through of the selected ISO + drive to Rufus on launch where its CLI/args allow; download-an-ISO helpers; FAT32/NTFS quick-format of a USB without an image; Windows-To-Go / persistence guidance; per-write speed + checksum (SHA-256) display; eject/safely-remove after verify; DD-mode vs ISO-mode hint text.

## Integration plan (WinForge specifics)
- New files: extend `Services/ImagingService.cs` with `VerifyImage(PhysicalDisk, imagePath, progress, ct)` (read-back compare) and a small `RufusService` helper (`IsInstalled()` via PATH/winget, `Launch()` via `ShellRunner`/`Process.Start`). Extend `Pages/ImagingGameModule.xaml(.cs)` with a third `TabViewItem`/`PivotItem` ("USB imager"). No new Page class, no `Catalog/*` needed.
- Nav wiring: **none new** — it lives under existing Tag `module.imaging` (`MainWindow.xaml` line ~126, `MapType` line ~462, `NavView_SelectionChanged` line ~663, `ApplyStartPage`). Update the `ModuleRegistry` entry (`Services/ModuleRegistry.cs` line ~74) keywords to add: `rufus usb bootable iso flash drive uefi mbr verify windows linux installer 開機 USB 手指 啟動碟`.
- Engine/install: winget id `Rufus.Rufus` via `EngineBars.AutoInstallButton(\"Rufus.Rufus\", \"Install Rufus\", \"安裝 Rufus\", recheck, rescan)`. The native write/verify path needs no binary; Rufus is only for advanced/launch.
- Key APIs/CLIs to call: reuse `ImagingService.ListDisks()`, `ImagingService.WriteImage(...)`, `ImagingService.HumanSize(...)`, `AdminHelper.IsElevated`/`RelaunchElevated()`, `FileDialogs.OpenFileAsync(...)`. Launch Rufus via its exe (resolve install path or `rufus` on PATH). Bilingual `TweakResult` for verify outcomes.

## Dependencies & risks
- Raw disk write/read requires **admin elevation** (`\\.\PhysicalDriveN`); reuse the existing `ShowAdminDialog`/`RelaunchElevated` flow.
- Data-safety: writing ERASES the whole disk — keep the existing system/boot-disk refusal and type-the-number confirmation; default the list to removable disks only.
- Verify pass roughly doubles I/O time on large images; run async with cancellation and live progress, never block the UI thread.
- Rufus is GPLv3 — only invoke the user-installed binary; do not bundle, link, or copy Rufus source.
- Rufus's command-line/automation surface is limited; "Launch Rufus" may open it without preselecting the drive/ISO — document this rather than over-promising arg pass-through.

## Acceptance criteria
- Builds clean (Debug + Release x64); the new USB-imager tab appears inside the existing Imaging & Game Tools module (module still reachable from nav + master search); native write of an ISO/IMG to a removable USB succeeds with progress; the post-write verify pass reports pass/fail; system/boot disks are never offered and writes are confirmed via the type-the-number dialog; Rufus installs via winget and launches for advanced options; every user-facing string is bilingual (English + 粵語); no WinRT pickers (FileDialogs only).
