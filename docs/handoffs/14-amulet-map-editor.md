# Handoff: Amulet Minecraft World Editor

| | |
|---|---|
| **Status** | Not started |
| **Source** | Local zip: `C:\Users\<USER>\Downloads\amulet_map_editor.zip` · upstream: https://github.com/Amulet-Team/Amulet-Map-Editor |
| **License** | Open source. Amulet-Map-Editor is GPLv3 (core libs `amulet-core`/`PyMCTranslate` are MIT). Bundling the zip and launching it as a separate process is fine; do not statically link GPL code into WinForge. |
| **Proposed module** | "Minecraft World Editor (Amulet)" · Gaming / Emulation group · Tag `module.amulet` |
| **Effort** | L — wrapping + Python bootstrap + launcher/state plumbing is moderate; the native world-tools slice adds risk. |

## What the user asked for
Bring the Amulet Minecraft world editor into WinForge. Amulet is a Python/wxPython desktop app that opens Java/Bedrock worlds for visual editing. The user wants WinForge to bundle/extract the provided zip, install Python if needed, launch/manage Amulet, and surface world picking — plus light native world tools where feasible via the existing `MinecraftService`.

## Recommended approach
**CLI/binary wrap (Hybrid).** Amulet is a large Python + wxPython + OpenGL codebase (3D chunk renderer, format translation tables across every MC version). Per the global strategy this is firmly in the "cannot be cloned in C# in reasonable scope" bucket, so wrap it and build a rich WinUI launcher/manager around it. Realistic v1: extract the bundled zip, ensure a Python runtime, launch Amulet pointed at a world the user picks in WinForge, and track the process (running/stopped, last world, log tail) exactly like `MinecraftService` already tracks the world-downloader proxy. The native slice (read-only world metadata: name, version, last-played, dimensions, size) is achievable in C# by parsing `level.dat` (gzipped NBT) and is a nice value-add; do NOT attempt native block editing.

## Features to implement (v1 → later)
- v1: Locate/extract `amulet_map_editor.zip` to a managed app-data dir; detect Python (`py`/`python` on PATH), offer winget auto-install if absent; "Open World…" folder picker (FileDialogs) → launch Amulet (`python -m amulet_map_editor` or bundled `.exe` if the zip ships a frozen build); tracked Start/Stop with live log tail; show last-launched world and Amulet location.
- v1 native: read `level.dat` via a small NBT reader to show world name / MC version / dimensions / size / last-played in a card before launching.
- later: recent-worlds list with thumbnails; backup-world-before-edit (zip the world folder); per-world "open in Amulet" shortcuts; surface Amulet's CLI/headless ops if any; locate `.minecraft/saves` automatically.

## Integration plan (WinForge specifics)
- New files: `Services/AmuletService.cs` (mirror `MinecraftService`: `FindApp`/`ExpectedPath`, `EnsureExtracted`, `FindPython`, `AutoInstallPython`, tracked `Start`/`Stop`/`IsRunning`, world-folder validation, `level.dat` NBT parse returning bilingual metadata); `Pages/AmuletModule.xaml(.cs)`; optionally `Catalog/AmuletOperations.cs` for list-style ops (extract, open saves folder, backup world) rendered by `Controls/TweakCard`.
- Reuse `MinecraftService` helpers where possible (it already has `FindJava`, process-tracking pattern, `RunOptions`, `TweakResult` bilingual results) — factor shared bits if practical.
- Nav wiring: add `NavigationViewItem Tag="module.amulet"` under the Gaming/Emulation group in `MainWindow.xaml`; add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) for master search; map the Tag in `MainWindow.xaml.cs` (`MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page amulet`).
- Engine/install: Python via `EngineBars.AutoInstallButton("Python.Python.3.12", "Install Python", "安裝 Python", recheck, rescan)`; wxPython/deps via `pip install -r requirements.txt` on first run (or rely on the bundled frozen build if the zip is self-contained — check the zip contents first). winget id for Amulet itself: **n/a** (bundled zip only).
- Key APIs/CLIs to call: `python -m amulet_map_editor` (or bundled exe); `pip`; `level.dat` gzip+NBT parse (System.IO.Compression.GZipStream + a minimal NBT tag reader); FileDialogs folder picker for the world dir.

## Dependencies & risks
- The zip's shape is unknown — it may be source (needs Python + `pip install`) or a PyInstaller frozen build (self-contained). Inspect it before designing the launcher; branch on which.
- wxPython + OpenGL deps can fail to install on some machines; surface clear bilingual errors and a "open install log" affordance.
- GPLv3: keep Amulet as a separate launched process; do not embed its code in the WinForge binary.
- Amulet has no documented headless/CLI editing API, so deep automation is out of scope — WinForge drives it as a GUI app.
- Large worlds / wrong-format folders: validate the selected folder (looks for `level.dat`) before launching.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in the left nav under Gaming/Emulation; deep-link `--page amulet` works.
- Core flow works: extract/locate Amulet, ensure Python, pick a world, launch Amulet on it, Stop kills the process tree, log tail streams.
- Native `level.dat` metadata card renders for a valid world; invalid folder shows a friendly bilingual error.
- Every user-facing string is bilingual (English + 粵語); all file/folder pickers use `FileDialogs` (no WinRT pickers).
