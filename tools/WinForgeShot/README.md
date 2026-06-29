# winforge-shot · WinForge 驅動與截圖工具

A C# console tool that **drives the WinForge app (via UI Automation) and captures
screenshots** — reliably, even when another window (e.g. a Unity editor) is in the
foreground. This is "computer use" through a C# tool: discover elements, click /
select / toggle / type into them, and screenshot the result.

## Driving (UI Automation)

WinForge sets `AutomationId`s (e.g. `ShellNavItem_dashboard`, `ShellNavItem_module_reactor`)
and `Name`s, so the tool can find and operate elements without the window being focused.

```
# Discover what's on screen (AutomationId | Name | ControlType):
winforge-shot --attach --list-ui 6

# Navigate to the reactor page, wait, screenshot:
winforge-shot --page dashboard --wait 15000 \
    --invoke ShellNavItem_module_reactor --sleep 2000 --out reactor.png

# Type into a box and toggle a switch (by AutomationId or Name substring):
winforge-shot --attach --settext "SyncNoteBox=nightly" --toggle "PushOnSyncCheck" --out x.png
```

Actions run **in the order given**. `--invoke` tries Invoke → Select → Toggle → a real
mouse click. Collapsed nav groups realize their children only once expanded (invoke the
group first, e.g. `--invoke "System"`, then the child item).

## Screenshots

## Why not the PowerShell driver?

`.claude/skills/run-winforge/driver.ps1` captures with `CopyFromScreen`, which
copies whatever pixels are physically on screen at the window's rectangle. If
another app overlaps WinForge, you screenshot *that* app instead. This tool uses
**`PrintWindow` with `PW_RENDERFULLCONTENT`**, which asks the WinForge window to
render *its own* pixels into a bitmap — so the capture is correct regardless of
z-order or occlusion (required for WinUI / DirectComposition windows).

## Build

```
dotnet build tools/WinForgeShot/WinForgeShot.csproj -c Release
```

## Run

```
# Open the dashboard and screenshot it (auto-finds the published WinForge.exe):
dotnet run --project tools/WinForgeShot/WinForgeShot.csproj -c Release -- \
    --page dashboard --out shot.png

# Capture an already-running instance without launching a new one:
winforge-shot --attach --out shot.png

# List candidate WinForge windows:
winforge-shot --list-windows
```

> A framework-dependent WinForge Debug build only shows an "install .NET" dialog,
> so the tool looks for a **self-contained publish** under
> `bin/**/win-x64/publish/WinForge.exe`. Produce one with:
> `dotnet publish WinForge.csproj -c Debug -r win-x64 --self-contained true -p:Platform=x64 -p:WindowsAppSDKSelfContained=true`

## Options

| Flag | Meaning |
|------|---------|
| `--page <alias>` | Deep-link page to open (`dashboard`, `reactor`, `camoufox`, `fileserver`, …). Omit to capture the current page. |
| `--out <file>` | Output PNG path (default `winforge-shot.png`). |
| `--wait <ms>` | Settle time before capture (default `12000`). |
| `--exe <path>` | Explicit `WinForge.exe` (else auto-detected, then any running instance). |
| `--attach` | Capture a running instance; do not launch. |
| `--keep-open` | Leave a launched instance running afterwards. |
| `--list-windows` | Print candidate WinForge top-level windows and exit. |

Exit code `0` on success, `2` on error (message on stderr).

The project is intentionally **not** part of `WinForge.sln` so it never affects the
main app build; build/run it via its own `.csproj`.
