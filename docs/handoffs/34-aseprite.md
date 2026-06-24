# Handoff: Aseprite-style Pixel Editor

| | |
|---|---|
| **Status** | Not started |
| **Source** | Reference only: https://github.com/aseprite/aseprite (C++). Native WinUI re-implementation of a lightweight subset; no Aseprite code copied. |
| **License** | Aseprite source is under the EULA-ish "Aseprite source" license (free to read/compile for personal use, NOT redistributable/commercial); the binary is paid. So we do NOT reuse, link, or bundle any Aseprite code/assets. Our native editor is original WinForge code (MIT, like the rest of the suite). Optional: detect & launch a user-installed Aseprite. |
| **Proposed module** | "Pixel Editor" · Creative / Media group (alongside Audacity/VLC) · Tag `module.pixeleditor` |
| **Effort** | L — a native canvas + tools + layers/frames + PNG/GIF export is a substantial but self-contained WinUI feature; no external runtime needed for the core. |

## What the user asked for
Bring an Aseprite-style pixel-art editor into WinForge: a native, lightweight WinUI canvas with palette, pencil/eraser/fill/select tools, layers, animation frames, and PNG/GIF export. Be explicit that this is a small subset, not full Aseprite, and offer to launch an installed Aseprite if present.

## Recommended approach
**Hybrid (native v1 + optional wrap).** Per the global strategy we prefer a native C# clone, and pixel-editing is one of the few graphics tools that *is* clonable: the data model is a small indexed bitmap and rendering is just drawing scaled rectangles. So build a genuine native editor in WinUI. Full Aseprite (tilemaps, scripting via Lua, advanced blending, slices, onion-skinning UI, brush engine) is out of scope — the C++ codebase is huge and paid, so we re-create only the core. As a courtesy, if Aseprite is installed (registry/`%LOCALAPPDATA%`/PATH), surface a "Launch Aseprite" button — never redirect elsewhere. Realistic v1 = open/create a small canvas (e.g. up to 256×256), draw with pencil/eraser/bucket-fill, manage a palette, a couple of layers and frames, and export PNG (and animated GIF).

## Features to implement (v1 → later)
- v1: New/Open canvas (configurable W×H, zoom with pixel grid); tools = Pencil, Eraser, Bucket Fill (flood), Eyedropper, Rectangular Select + move/delete selection; editable color palette + recent colors; undo/redo stack; Layers (add/delete/reorder/toggle visibility/opacity); Frames (add/duplicate/delete, simple timeline); Export PNG (single frame) and animated GIF (all frames, per-frame delay).
- v1 detect/launch: if Aseprite is installed, show "Launch Aseprite" + "Open current file in Aseprite".
- later: import PNG/GIF for editing; line/ellipse/dither/select-by-color tools; onion skinning; tile/symmetry mode; spritesheet export; .ase/.aseprite read-only import if format is reverse-engineerable; configurable shortcuts.

## Integration plan (WinForge specifics)
- New files: `Services/PixelEditorService.cs` (the document model: indexed/ARGB frame+layer buffers, flood-fill, undo/redo, PNG encode via `BitmapEncoder`/`Windows.Graphics.Imaging`, GIF encode via `GifBitmapEncoder` or ffmpeg fallback, Aseprite detect/launch); `Pages/PixelEditorModule.xaml(.cs)` (canvas host, tool/palette/layers/frames panels); optionally `Catalog/PixelEditorOperations.cs` for list-style actions (New, Export PNG, Export GIF, Launch Aseprite) rendered by `Controls/TweakCard`.
- Rendering: draw onto a `Win2D CanvasControl` if available, else a `WriteableBitmap` scaled into an `Image` (simplest, dependency-free) with pointer-pressed/moved handlers mapping screen→pixel coords.
- Nav wiring: add `NavigationViewItem Tag="module.pixeleditor"` under the Creative/Media group in `MainWindow.xaml`; add a `ModuleRegistry` entry (Services/ModuleRegistry.cs); map the Tag in `MainWindow.xaml.cs` (`MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page pixeleditor`).
- Engine/install: winget id **n/a** for the native editor (no binary needed). GIF export may use ffmpeg as a fallback — if so, offer `EngineBars.AutoInstallButton("Gyan.FFmpeg", "Install ffmpeg", "安裝 ffmpeg", recheck, rescan)`. Prefer native `GifBitmapEncoder` first to avoid the dependency.
- File pickers: ALWAYS use `FileDialogs` (Services/FileDialogs.cs) for open/save — never WinRT pickers (must work elevated).
- Key APIs: `WriteableBitmap`/`Win2D`, `BitmapEncoder`/`GifBitmapEncoder`, `System.IO`, pointer events for drawing, `ShellRunner` only for the optional ffmpeg/Aseprite launch.

## Dependencies & risks
- Scope creep: pixel editors invite endless tooling — keep v1 to the listed tools and a small max canvas size to bound memory/perf.
- GIF encoding: native `GifBitmapEncoder` lacks per-frame delay/loop metadata control in some stacks; verify, else fall back to ffmpeg (palette-gen for clean output).
- Performance: redraw the whole `WriteableBitmap` per stroke can lag at high zoom — batch updates / dirty-rect if needed; Win2D removes most of this risk.
- Licensing: do NOT copy Aseprite code, default palette, UI assets, or its `.ase` format spec wholesale; native editor must be original. Launch-installed-Aseprite is fine.
- Undo/redo memory: store compact per-stroke deltas, not full-frame snapshots, for larger canvases.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in the left nav under Creative/Media; deep-link `--page pixeleditor` works.
- Core flow works: create canvas, draw with pencil/eraser/fill/eyedropper, select+move, edit palette, add/reorder layers, add/duplicate frames, undo/redo, export a valid PNG and an animated GIF.
- If Aseprite is installed it is detected and launchable; if not, the button is hidden/disabled with a friendly note.
- Every user-facing string is bilingual (English + 粵語); all file pickers use `FileDialogs` (no WinRT pickers); UI clearly states this is a lightweight subset, not full Aseprite.
