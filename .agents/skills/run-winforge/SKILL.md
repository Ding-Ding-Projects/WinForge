---
name: run-winforge
description: Build, launch, drive and screenshot the WinForge WinUI 3 desktop app on an owned off-screen desktop. Use when asked to run, start, launch, build, publish, screenshot, or smoke-test WinForge or its modules (e.g. "run WinForge", "screenshot the reactor page", "open the docker module").
---

# Run WinForge

WinForge is the canonical **.NET 11 WinUI 3 app** in `WinForge.csproj`. The PowerShell driver — **`.agents/skills/run-winforge/driver.ps1`** — publishes it self-contained, creates a unique non-input Win32 desktop, deep-links a requested module there, and captures only the dedicated process window. It launches with native `CreateProcess(lpDesktop=…)` and `CREATE_NO_WINDOW`, never calls `SwitchDesktop`, and never foregrounds WinForge, a terminal, or a helper. Treat an output PNG as visual evidence only after inspecting it. All paths below are relative to the repo root.

> Why a self-contained publish + off-screen self-capture? A plain `dotnet build` produces a **framework-dependent** exe that, with no matching desktop runtime here, just shows a *"You must install or update .NET"* dialog. And the app is **not a Start-menu app**, so ordinary desktop screenshot tools cannot safely target it. The driver keeps the app on its own desktop, enumerates the owned HWND from that desktop, prefers WinForge's DEBUG-only live visual-tree capture, and uses only HWND-targeted `PrintWindow` as a validated fallback. It never samples raw desktop pixels, so an overlapping window cannot leak into the result.

## Prerequisites
- In this workspace, the driver automatically selects USERPROFILE\.dotnet\dotnet.exe when it exposes a .NET 11 SDK. The machine-wide dotnet command can resolve to an older SDK, so direct net11 app build/publish commands must set DOTNET_ROOT to USERPROFILE\.dotnet and prepend that directory to PATH. The ReactorSim focused harness targets net8.0-windows; clear DOTNET_ROOT before running it so its installed net8 runtime remains visible.
- .NET SDK with WinUI/Windows App SDK support (this repo built on .NET 11 SDK; `dotnet --version` → `11.0.100-preview...`). No extra OS packages needed on Windows.

## Build (compile check)
```bash
dotnet build WinForge.sln -c Debug -p:Platform=x64 -v minimal
```
Builds clean (0 errors). This only *compiles* — it does not produce a runnable exe here (see note above).

## Run (agent path) — off-screen driver only
One command builds-if-needed, creates an owned off-screen desktop, launches a page, and screenshots it. Agent automation must not replace this with `Start-Process`, direct executable launch, or a visible computer-use fallback.

For a non-visual route smoke check, add `-NoCapture`. It verifies the dedicated window by enumerating the owned off-screen desktop, prints launch-only evidence, and cleans up only that process and desktop.
```bash
powershell -ExecutionPolicy Bypass -File .agents/skills/run-winforge/driver.ps1 -Page monitor -Out shot.png
```
- `-Page <alias>` — deep-link alias from `MainWindow.ApplyStartPage` (e.g. `dashboard`, `reactor`, `reactorsettings`, `monitor`, `docker`, `torrent`, `proxmox`, `ocr`, `keepass`, `hexeditor`). Use the registered alias for the target module.
- `-Out <file.png>` — where the screenshot is written (printed as `OK off-screen page='…' -> … (WxH)`). It must resolve to a `.png` path of at most 1,024 characters on a fixed or removable local drive; UNC, mapped-network, and other drive types fail closed.
- `-Publish` — force a fresh self-contained publish first.
- `-WaitMs <n>` — render wait before capture (1,000–120,000 ms; default 12000; raise for heavy pages).
- `-NoCapture` — verify a dedicated managed window launches on the owned off-screen desktop, then clean it up without taking a screenshot.

First run (no publish yet) does the self-contained publish itself; or do it explicitly:
```bash
dotnet publish WinForge.csproj -c Debug -r win-x64 --self-contained true -p:Platform=x64 -p:WindowsAppSDKSelfContained=true -v quiet
```
The driver publishes its executable to the ignored, driver-owned
`artifacts/run-winforge/publish/WinForge.exe` path. This deliberately avoids the SDK's default
publish folder, which may be locked by a human-owned WinForge instance that automation must not
close or disturb.

## Direct invocation — reactor engine tests (no GUI)
The reactor physics/services run headless via a console harness (no WinUI):
```bash
Remove-Item Env:DOTNET_ROOT -ErrorAction SilentlyContinue
dotnet run --project tests/ReactorSim.Tests -c Debug
```
Prints a per-scenario PASS/FAIL table (currently **67/67** across reactor physics, accident injection, fuel/waste/water services, reactor-dependent apps, the optional feature-bus EDG, and the cake-factory dependency chain). It includes a sustained high-power thermal-equilibrium regression. Use this for changes that touch reactor internals or reactor-dependent services — far faster than launching the GUI.

## No visible launch path
Every launch described or performed by this skill uses the owned off-screen desktop. Do not document, suggest, or execute a visible `WinForge.exe`, terminal, helper, or interactive-desktop fallback from this workflow.

## Gotchas
- **Off-screen is mandatory** — the driver creates one uniquely named desktop with `CreateDesktop`, launches WinForge there with `SW_SHOWNOACTIVATE`, finds the largest owned top-level HWND with `EnumDesktopWindows`, and closes the desktop after terminating the exact owned process. It never switches or attaches the caller thread to that desktop. Any child/helper inherits the non-input desktop and cannot steal focus from the user's session.
- **Capture must stay process-owned** — the driver retains its original process handle and matches the HWND by that PID on its own desktop; it never terminates or captures another task's WinForge instance. Existing interactive instances remain untouched.
- **Automation data is isolated and temporary** — the child alone inherits a unique `WINFORGE_AUTOMATION_DATA_ROOT` beneath the current user's temp directory. DEBUG app data-path code must validate and honor that override instead of KnownFolder LocalAppData; Release builds ignore it. The driver immediately restores the parent's environment and, after process cleanup, recursively removes only the exact normal directory whose parent and driver-owned name both validate. It never deletes or writes the user's regular WinForge LocalAppData.
- **Capture has two process-owned paths** — the driver first requests a DEBUG-only in-process `RenderTargetBitmap` of the live WinUI tree, then may use `PrintWindow(PW_RENDERFULLCONTENT)` against the fresh owned off-screen HWND. The in-process service composites premultiplied pixels against the root's `ActualTheme`, flushes a unique same-directory partial PNG, and atomically promotes it; failures never log the requested path. The driver removes a stale destination when a new attempt begins, validates either source, and uses same-directory `MoveFileEx(REPLACE_EXISTING | WRITE_THROUGH)` for every final promotion; neither live-tree copy nor `PrintWindow` writes directly to the requested filename. It intentionally never calls `CopyFromScreen`, which can capture another app covering the same desktop rectangle. Temporary images are uniquely named, validated as non-uniform, retried during cleanup, and reported if they survive. If both paths fail, record the exact error as capture-blocked, do not reuse a stale image, and use `-NoCapture` plus the relevant managed tests for launch/behavior evidence. Never fall back to a visible launch.
- **Framework-dependent build won't run** → it pops a *"install .NET"* dialog. Always run/launch the **self-contained publish** exe (the driver does this).
- **App not in the Start menu** → use the driver or LowLevel headless workflow. The driver captures the app-owned visual tree on a non-input desktop and never reads unrelated desktop pixels.
- **`--page` is reliable; bare `--reactor` is not** — with a restored multi-tab session, `--reactor` can land on the Dashboard. Always prefer `--page reactor`.
- **Previously crash-prone pages are fixed** — `audioeditor`, `lightswitch`, and `timelens` now open through `NavigateActive`. If a heavy page captures blank, retry with a longer `-WaitMs` before treating it as a crash.
- **Reactor boots held in MODE 5 cold shutdown** — it is subcritical/idle by design and the operator must start it. Foundational realism P1–P5 is resolved: startup is stable, a fully-rodded fresh core is −1018 pcm subcritical, and the 67/67 harness verifies a sustained high-power equilibrium without emergency cooling, SCRAM, or meltdown.
- **First publish is slow** (~3–4 min); subsequent ones are incremental.

## Troubleshooting
- `no owned WinForge window appeared on the isolated desktop for page '<x>'` → raise `-WaitMs`, verify the self-contained publish, and use the relevant managed smoke test. Do not retry on the interactive desktop.
- A driver run must not restore or delete the user's regular reactor/session state. If automation sees stale state, the DEBUG data-path hook is not honoring `WINFORGE_AUTOMATION_DATA_ROOT`; fix that hook rather than touching `%LOCALAPPDATA%\WinForge`.
- Test harness fails to compile after engine changes → it links specific engine sources; add any new `Services/Reactor*.cs` it references to `tests/ReactorSim.Tests/ReactorSim.Tests.csproj`.
