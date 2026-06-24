# Handoff: Android SDK Tools Manager

| | |
|---|---|
| **Status** | Not started |
| **Source** | CLI: `sdkmanager` + `avdmanager` (Android SDK command-line tools) · https://developer.android.com/tools/sdkmanager · https://developer.android.com/tools/avdmanager |
| **License** | Android SDK command-line tools: Apache 2.0 (binaries ship under the Android SDK Terms / SDK License Agreement; licenses must be accepted before download). |
| **Proposed module** | Android Emulator (extended) · "Mobile & Devices" nav group · Tag `module.emulator` (EXTEND existing) |
| **Effort** | M — the wrapping service (`EmulatorService`) and module already exist and call `sdkmanager`/`avdmanager`; this adds package-management UI and license handling, not a new module. |

## What the user asked for
Wrap the Android SDK's `sdkmanager`/`avdmanager` CLIs so users can list/install/update SDK packages (platforms, build-tools, NDK, platform-tools, system images), accept SDK licenses, and create/manage AVDs from inside WinForge. Extend the existing Android Emulator module rather than add a new one; it should tie into the existing ADB (`module.adb`) and Fastboot (`module.fastboot`) modules.

## Recommended approach
**CLI/binary wrap** — per global strategy step 2, the SDK package manager is a Java-based tool resolving against Google's remote repository XML and an evolving licensing model; reimplementing the resolver natively is out of scope and brittle. Wrap it with a rich WinUI front end. This is the correct call because `Services/EmulatorService.cs` already locates the SDK (`SdkRoot()` via `ANDROID_SDK_ROOT`/`ANDROID_HOME`/`%LOCALAPPDATA%\Android\Sdk`), exposes `SdkManager`/`AvdManager` paths, and has `ListSystemImages()` + AVD CRUD. v1 scope: a "SDK Packages" section in `EmulatorModule` that lists available/installed packages, installs/updates/removes them, and accepts licenses — leaving the existing AVD UI intact.

## Features to implement (v1 → later)
- v1: List installed packages (`sdkmanager --list_installed`) and available packages (`sdkmanager --list`), grouped by category (platforms, build-tools, platform-tools, cmdline-tools, ndk, system-images).
- v1: Install / update / uninstall a package by id (`sdkmanager "<pkg>"`, `sdkmanager --update`, `sdkmanager --uninstall "<pkg>"`); stream progress into an output pane.
- v1: Accept licenses (`sdkmanager --licenses`, auto-answering `y`) — surface as a one-click "Accept all SDK licenses" button.
- v1: SDK root indicator + "Open SDK folder" (via `FileDialogs` / ShellRunner) and a channel selector (`--channel=0..3`).
- later: NDK side-by-side version management; bundle/export of an installed-package manifest into Config & Backup; deep-link "Install system image" from the AVD create dialog when none exist; proxy support (`--proxy`).

## Integration plan (WinForge specifics)
- New files: none required; extend `Services/EmulatorService.cs` (add `ListAvailablePackages()`, `InstallPackage(id)`, `UpdatePackage`, `Uninstall(id)`, `AcceptLicenses()`) and `Pages/EmulatorModule.xaml(.cs)` (add a "SDK Packages" expander/pivot). Optionally add `Catalog/SdkOperations.cs` if package actions are rendered as `TweakCard`s.
- Nav wiring: already done — `module.emulator` exists in `MainWindow.xaml`, `ModuleRegistry.cs` (extend its `Keywords` with `sdk sdkmanager packages platform-tools build-tools ndk license 套件 平台 授權`), `MapType`, `NavView_SelectionChanged`. No new entry needed.
- Engine/install: SDK has no single winget id; `Google.AndroidStudio` (winget) bundles the SDK + cmdline-tools. Use `EngineBars.AutoInstallButton("Google.AndroidStudio", "Install Android Studio / SDK automatically", "自動安裝 Android Studio／SDK", recheck, rescan)` on the existing `EngineBar` when `EmulatorService.Health()` reports the SDK missing.
- Key APIs/CLIs to call: `sdkmanager --list` / `--list_installed` / `--licenses` / `--update` / `--uninstall` / `--channel`; `avdmanager list avd` / `create avd` / `delete avd` (existing). Drive all via `ShellRunner.Run`/`RunCmd` (license accept needs piped `y`, mirror the existing `echo no |` pattern in `CreateAvd`).

## Dependencies & risks
- `sdkmanager`/`avdmanager` are `.bat` wrappers needing a JDK on PATH (`JAVA_HOME`); detect and warn bilingually if missing.
- License prompts block forever without piped input — always feed `y`/`yes`; long downloads need a cancel path (use the `CancellationToken` overloads already in `EmulatorService`).
- `--list` output format differs across cmdline-tools versions; parse defensively (split on `|`, trim) like `ListSystemImages()` does.

## Acceptance criteria
- Builds clean (Debug + Release x64); the Android Emulator module appears in nav and shows the new SDK Packages section; list/install/update/uninstall/accept-licenses all work against a real SDK; AutoInstallButton installs the SDK when absent; all strings bilingual (English + 粵語); no WinRT pickers (use `FileDialogs`).
