# Handoff: WinForge Vault (SecureVault / VeraCrypt-derived disk encryption)

| | |
|---|---|
| **Status** | Implemented in WinForge; branch verification in progress |
| **Source** | Local fork: `C:\Users\<USER>\Documents\GitHub\SecureVault` (VeraCrypt → TrueCrypt 7.1a derived, C/C++) |
| **License** | Apache License 2.0 (VeraCrypt portions) + TrueCrypt License 3.0 (legacy parts). Source-available; **derived works must NOT use the "TrueCrypt" / "VeraCrypt" names or logos** — de-brand mandatory. |
| **Implemented module** | "WinForge Vault" (粵語: WinForge 保險庫) · left-nav group **Security & Privacy** · Tag `module.vault-volumes` |
| **Effort** | **L** — no native crypto to write, but a rich create/mount/dismount front-end over a CLI, plus elevation, progress parsing and bundling the de-branded binary. |

## What the user asked for
Bring SecureVault (a VeraCrypt/TrueCrypt fork) into WinForge as a disk-encryption module: create encrypted volumes/containers, mount/dismount, change password, and benchmark — with a polished bilingual WinUI front-end, bundling the SecureVault-built binary and stripping all "SecureVault"/"VeraCrypt"/"TrueCrypt" branding to "WinForge Vault".

## Recommended approach
**Hybrid (wrap the CLI + rich WinUI front-end).** Per the global strategy, a native C# reimplementation is the goal *only* when feasible — here it is not. The codebase is large C/C++ implementing on-the-fly encryption with a **kernel-mode filesystem/disk driver** (`veracrypt.sys`). Reimplementing the cryptography, volume format, and driver in C# would be infeasible and dangerously insecure. So we wrap the binary's command-line interface and build the GUI around it.

Realistic v1: a WinUI page that drives a **de-branded** `VeraCrypt.exe` / `VeraCrypt Format.exe` (renamed, e.g. `WinForgeVault.exe`) via its documented switches for mount, dismount, and silent operations, plus a guided "Create container" wizard. **Note:** `Catalog/VaultTweaks.cs` already ships ~20 VeraCrypt ops (`vault.veracrypt.*`) that shell out to `%ProgramFiles%\VeraCrypt\...`. **Extend that, do not duplicate** — the new module should host a real UI flow; keep the catalog ops as quick-actions but repoint paths to the bundled de-branded binary.

## Implemented features
- **v1:** Create encrypted file container (size, filesystem, AES/Serpent/Twofish, password, optional keyfile/PIM) via `Format.exe`; mount to a chosen drive letter (`/v /l /p /pim /k /q`); dismount one / dismount-all / force-dismount (`/d /f`); change volume password; list mounted volumes; run algorithm benchmark; open a mounted volume in Explorer.
- **Extended surface:** Hidden-volume wizard entry, partition/device encryption entry, system-encryption entry, favourites & auto-mount, keyfile generator, traveler-disk, volume header backup/restore, read-only and removable-media mount options.

## Implemented integration (WinForge specifics)
- `Services/VaultVolumeService.cs` builds CLI argument strings, calls `ShellRunner.Run` with elevation where needed, and re-lists drives to confirm state.
- `Pages/VaultVolumesModule.xaml` + `.cs` host create, mount, mounted-volume, browse, dismount, password-change, cache-wipe and benchmark flows.
- `Catalog/VaultTweaks.cs` keeps the quick actions in the existing Security & Vault catalog and routes them through the same service where a direct flow exists.
- Navigation is wired through `MainWindow.xaml`, `MainWindow.xaml.cs`, and `Services/ModuleRegistry.cs`.
- File and folder selection uses `Services/FileDialogs.cs`; no WinRT picker is used by the module.

## Dependencies & risks
- Mount/dismount and the kernel driver require **elevation** — route through `ShellRunner.Run(..., elevated:true)`; captured output is unavailable under UAC, so confirm state by re-listing drives.
- The `.sys` driver must be **signed** and installed; an unsigned re-branded build may be blocked by Windows. Driver signing is a hard prerequisite — flag early.
- **Never pass plaintext passwords on the command line in shipping builds** (visible to other processes); prefer stdin/interactive prompt where the binary supports it.
- Branding removal is a license obligation: rename exe/driver/strings and the bundled User Guide; do not surface "VeraCrypt"/"TrueCrypt" in any user-facing string.
- Volume operations are destructive (format overwrites the target) — gate with `destructive:true` confirmations like existing catalog ops.

## Acceptance criteria
- Builds clean (Debug + Release **x64**); "WinForge Vault" appears in the Security & Privacy nav group; create → mount → browse → dismount round-trips against a test container; change-password and benchmark work; every user-facing string is English + Cantonese; all file/folder selection uses `FileDialogs` (no WinRT pickers); no "SecureVault/VeraCrypt/TrueCrypt" branding visible.
