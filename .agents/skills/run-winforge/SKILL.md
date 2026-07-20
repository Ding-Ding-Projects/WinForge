---
name: run-winforge
description: Build, launch, drive and screenshot the WinForge WinUI 3 desktop app. Use when asked to run, start, launch, build, publish, screenshot, or smoke-test WinForge or its modules (e.g. "run WinForge", "screenshot the reactor page", "open the docker module").
---

# Run WinForge

WinForge is the canonical **.NET 11 WinUI 3 app** in `WinForge.csproj`. The PowerShell driver — **`.agents/skills/run-winforge/driver.ps1`** — publishes it self-contained, deep-links a requested module, and captures the dedicated process window. Treat an output PNG as visual evidence only after inspecting it. All paths below are relative to the repo root.

> Why a self-contained publish + self-capture? A plain `dotnet build` produces a **framework-dependent** exe that, with no matching desktop runtime here, just shows a *"You must install or update .NET"* dialog. And the app is **not a Start-menu app**, so desktop/computer-use screenshot tools can't target its window — the driver's process-owned `CopyFromScreen` is the preferred capture path when the desktop session permits it.

## Prerequisites
- In this workspace, the driver automatically selects USERPROFILE\.dotnet\dotnet.exe when it exposes a .NET 11 SDK. The machine-wide dotnet command can resolve to an older SDK, so direct net11 app build/publish commands must set DOTNET_ROOT to USERPROFILE\.dotnet and prepend that directory to PATH. The ReactorSim focused harness targets net8.0-windows; clear DOTNET_ROOT before running it so its installed net8 runtime remains visible.
- .NET SDK with WinUI/Windows App SDK support (this repo built on .NET 11 SDK; `dotnet --version` → `11.0.100-preview...`). No extra OS packages needed on Windows.

## Build (compile check)
```bash
dotnet build WinForge.sln -c Debug -p:Platform=x64 -v minimal
```
Builds clean (0 errors). This only *compiles* — it does not produce a runnable exe here (see note above).

## Run (agent path) — the driver
One command builds-if-needed, launches a page, and screenshots it:
For a non-visual route smoke check in a capture-blocked desktop session, add -NoCapture. It waits for the dedicated process window without foregrounding it, prints launch-only evidence, and cleans up only that process.
```bash
powershell -ExecutionPolicy Bypass -File .agents/skills/run-winforge/driver.ps1 -Page monitor -Out shot.png
```
- `-Page <alias>` — deep-link alias from `MainWindow.ApplyStartPage` (e.g. `dashboard`, `reactor`, `reactorsettings`, `monitor`, `docker`, `torrent`, `proxmox`, `ocr`, `keepass`, `hexeditor`). Use the registered alias for the target module.
- `-Out <file.png>` — where the screenshot is written (printed as `OK page='…' -> … (WxH)`).
- `-Publish` — force a fresh self-contained publish first.
- `-WaitMs <n>` — render wait before capture (default 12000; raise for heavy pages).
- `-NoCapture` — verify a dedicated managed window launches, then clean it up without taking a screenshot.

First run (no publish yet) does the self-contained publish itself; or do it explicitly:
```bash
dotnet publish WinForge.csproj -c Debug -r win-x64 --self-contained true -p:Platform=x64 -p:WindowsAppSDKSelfContained=true -v quiet
```
The published exe lands at `bin/x64/Debug/net11.0-windows10.0.26100.0/win-x64/publish/WinForge.exe`.

## Direct invocation — reactor engine tests (no GUI)
The reactor physics/services run headless via a console harness (no WinUI):
```bash
Remove-Item Env:DOTNET_ROOT -ErrorAction SilentlyContinue
dotnet run --project tests/ReactorSim.Tests -c Debug
```
Prints a per-scenario PASS/FAIL table (currently **63/63** across reactor physics, accident injection, fuel/waste/water services, reactor-dependent apps, and the cake-factory dependency chain). It includes a sustained high-power thermal-equilibrium regression. Use this for changes that touch reactor internals or reactor-dependent services — far faster than launching the GUI.

## Run (human path)
`WinForge.exe` with no args opens the Dashboard and waits — useless headless, and the plain Debug exe needs the self-contained publish first. Use the driver instead.

## Gotchas
- **Capture must stay process-owned** — the driver launches and cleans up only its own WinForge process; it never terminates or captures another task's instance. If an existing instance intercepts the launch, close only the instance you own or use an isolated desktop session.
- **Capture can be environment-blocked** — if CopyFromScreen reports an invalid handle, record the exact failure as capture-blocked. `PrintWindow(PW_RENDERFULLCONTENT)` can return a blank client surface even when the WinUI visual tree is rendered; the driver rejects blank/near-uniform client frames. Do not reuse a stale image or claim a visual pass; use `-NoCapture` plus the relevant managed tests for launch/behavior evidence.
- **Framework-dependent build won't run** → it pops a *"install .NET"* dialog. Always run/launch the **self-contained publish** exe (the driver does this).
- **App not in the Start menu** → computer-use / desktop screenshot tools mask it. The driver captures via `CopyFromScreen` over the DWM extended-frame bounds (attribute `9`) — accurate and shadow-excluded.
- **`--page` is reliable; bare `--reactor` is not** — with a restored multi-tab session, `--reactor` can land on the Dashboard. Always prefer `--page reactor`.
- **Previously crash-prone pages are fixed** — `audioeditor`, `lightswitch`, and `timelens` now open through `NavigateActive`. If a heavy page captures blank, retry with a longer `-WaitMs` before treating it as a crash.
- **Reactor boots held in MODE 5 cold shutdown** — it is subcritical/idle by design and the operator must start it. Foundational realism P1–P5 is resolved: startup is stable, a fully-rodded fresh core is −1018 pcm subcritical, and the 63/63 harness verifies a sustained high-power equilibrium without emergency cooling, SCRAM, or meltdown.
- **First publish is slow** (~3–4 min); subsequent ones are incremental.

## Troubleshooting
- `no WinForge window appeared for page '<x>'` → raise `-WaitMs`, then check `%LOCALAPPDATA%\WinForge\crash.log` for a genuine load failure.
- Reactor restores a stale/melted autosave → delete `%LOCALAPPDATA%\WinForge\state` (and `…\session\tabs.json`) to boot fresh.
- Test harness fails to compile after engine changes → it links specific engine sources; add any new `Services/Reactor*.cs` it references to `tests/ReactorSim.Tests/ReactorSim.Tests.csproj`.
