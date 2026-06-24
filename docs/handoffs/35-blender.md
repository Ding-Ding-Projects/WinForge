# Handoff: Blender Integration

| | |
|---|---|
| **Status** | Not started |
| **Source** | CLI: `blender` (the desktop binary, run headless/`-b`) · upstream: https://developer.blender.org · https://github.com/blender/blender |
| **License** | Blender is GPLv2-or-later (open source). WinForge launches `blender.exe` as a separate process and never links its code in, so GPL is not a concern. Python scripts authored by WinForge that run inside Blender are user content. |
| **Proposed module** | "Blender (3D / Render)" · Media / Tools group · Tag `module.blender` |
| **Effort** | M — no engine to embed; effort is a solid CLI wrapper + a rich job/queue front-end and live render-progress parsing. |

## What the user asked for
Bring Blender into WinForge. Blender is a massive 3D suite (C/C++/Python) — reimplementing it is infeasible. Instead wrap the `blender` CLI: launch the GUI, open `.blend` files, run headless renders, run Python scripts, and batch-render — with a WinUI front-end for render jobs and asset browsing. Install via winget `BlenderFoundation.Blender`.

## Recommended approach
**CLI/binary wrap.** Per the global strategy, a 3D DCC suite (viewport, modeling, sculpting, Cycles/EEVEE renderers, Python API) is squarely in the "cannot be cloned in C# in reasonable scope" bucket. So wrap the installed `blender.exe` and build a rich WinUI front-end around its already-excellent command line. Realistic v1: detect/install Blender, open `.blend` files in the GUI, and a headless **render job** form (pick file, output dir, frame or frame-range, engine, format) that runs `blender -b` and streams progress. Nothing about the 3D suite itself is reimplemented natively — WinForge is the launcher, job builder, and progress dashboard.

## Features to implement (v1 → later)
- v1: Detect Blender (`FindApp`/`winget` path / PATH); `EngineBars.AutoInstallButton` if absent. "Open in Blender" (FileDialogs `.blend` picker → launch GUI). Headless **single render**: `blender -b <file> -o <out>/frame_#### -f <frame>` (or `-s start -e end -a` for animation). Form fields: input `.blend`, output folder, frame/range, engine (`-E CYCLES`/`BLENDER_EEVEE`), format (`-F PNG/JPEG/FFMPEG`). Stream stdout via `ShellRunner.Capture`, parse `Fra:`/`Saved:` lines into a progress bar + log tail. Cancel button kills the process tree.
- v1: **Run Python script** against a file: `blender -b <file> --python <script.py>`; ship 1–2 starter scripts (e.g. export to glTF/FBX).
- later: **Batch render queue** (multiple files/ranges, sequential, persistable). Asset browser: scan a folder for `.blend` and show thumbnails (Blender embeds a preview, or render one quickly with `-f` to a temp PNG). Output presets; tile/`--render-output` templating; samples override via `--python-expr`; open output folder when done; estimated time from per-frame timing.

## Integration plan (WinForge specifics)
- New files: `Services/BlenderService.cs` (`FindBlender`/`ExpectedPath`, `IsInstalled`, build arg lists, tracked `StartRender`/`StartScript`/`OpenGui`/`Cancel`/`IsRunning`, stdout parser → bilingual `TweakResult`/progress events); `Pages/BlenderModule.xaml(.cs)` (render-job form, progress bar, log tail, queue list); optionally `Catalog/BlenderOperations.cs` for list-style ops (open saves folder, run starter script, render still) rendered by `Controls/TweakCard`.
- Nav wiring: add `NavigationViewItem Tag="module.blender"` under the Media/Tools group in `MainWindow.xaml`; add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) for master search; map the Tag in `MainWindow.xaml.cs` (`MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page blender`).
- Engine/install: `EngineBars.AutoInstallButton("BlenderFoundation.Blender", "Install Blender", "安裝 Blender", recheck, rescan)`. Mirror existing CLI-wrap services for process tracking + bilingual status.
- Key CLIs to call: `blender -b <file> -o <out> -F <fmt> -E <engine> -f <frame>` / `-s -e -a` (animation); `blender --python <script>` / `--python-expr "<code>"`; `blender --version` (detect); GUI launch `blender <file>`. Use `ShellRunner.Run`/`Capture`; FileDialogs for all `.blend`/output pickers.

## Dependencies & risks
- Argument ORDER matters in Blender's CLI: `-f`/`-a` must come AFTER `-o`/`-F`/`-E` or they use defaults. Build args carefully and unit-test the order.
- Renders are long and CPU/GPU-heavy; run off the UI thread, stream progress, and make Cancel reliably kill the whole process tree.
- Output paths use `#` for frame-number padding (`frame_####`); validate user paths and escape spaces.
- Blender version drift can change CLI/Python API; gate features on `--version` and surface clear bilingual errors.
- GPU vs CPU device selection isn't a simple flag (needs `--python-expr` to set Cycles device) — keep advanced device control in "later".
- Every user-facing string needs English + Hong Kong Cantonese (粵語).

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in the left nav under Media/Tools; deep-link `--page blender` works.
- Core flow works: detect/install Blender, open a `.blend` in the GUI, run a headless render of a frame to a chosen folder with live progress, Cancel kills the process.
- Run-Python-script flow executes and reports success/failure.
- Every user-facing string is bilingual (English + 粵語); all file/folder pickers use `FileDialogs` (no WinRT pickers).
