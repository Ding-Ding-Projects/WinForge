# Handoff: ScreenToGif (GIF Studio)

| | |
|---|---|
| **Status** | Not started |
| **Source** | https://github.com/NickeManarin/ScreenToGif (C#/WPF) · winget `NickeManarin.ScreenToGif` |
| **License** | MS-PL (Microsoft Public License) — open source, permissive; safe to study/port. We write our own code, not copy theirs. |
| **Proposed module** | GIF Studio · 螢幕轉 GIF · Media/Multimedia group · Tag `module.giflab` |
| **Effort** | M — capture + ffmpeg export reuse existing infra; the per-frame editor (delete/reorder/crop preview) is the only net-new UI work. |

## What the user asked for
A ScreenToGif-style tool inside WinForge: record a screen region, see the captured frames, do light editing (delete / reorder / crop frames), then export to GIF, MP4, or APNG.

## Recommended approach
**Native C# WinUI clone.** ScreenToGif is itself C#/WPF, so its core (region recorder → frame list → encode) maps cleanly onto what WinForge already has. Per the global strategy this is exactly the "reimplement natively" case — no need to wrap the WPF binary. WinForge already ships an in-app recorder (`Pages/ScreenRecorderModule.xaml.cs`, ffmpeg `gdigrab`), a Win32 region picker (`Services/RegionSelector.PickRegionAsync()` → `(int x,int y,int w,int h)?`), and an ffmpeg engine bar (`Gyan.FFmpeg`). A v1 records a region to a temp folder of PNG frames (ffmpeg `gdigrab -framerate N`), shows them as a thumbnail strip, lets the user delete/reorder/crop, and re-encodes the surviving frames via ffmpeg. WPF's pixel-level visual editor (drawing overlays, text, free-draw) is out of scope for v1 — be honest: that is a large effort and not what the user emphasized.

## Features to implement (v1 → later)
- v1: Region/window/fullscreen capture via `RegionSelector`; configurable FPS + duration; frames extracted to a temp dir as `frame%05d.png`; horizontal thumbnail strip (`GridView`/`ItemsRepeater`); delete frame(s), reorder (drag or move-left/right), uniform crop applied to all frames; export GIF (palettegen/paletteuse), MP4 (h264), APNG via ffmpeg; per-export FPS/scale/loop; save via `FileDialogs`.
- later: per-frame delay editing; trim range slider; webcam/cursor-highlight overlay; on-canvas annotations (text, arrows, free-draw); paste-from-clipboard frames; recording keyboard hotkey; live preview playback loop.

## Integration plan (WinForge specifics)
- New files: `Pages/GifLabModule.xaml` + `.xaml.cs`; `Services/GifLabService.cs` (capture orchestration, ffmpeg arg building, frame I/O, export). Optionally a small `FrameItem` view-model in the page. No `Catalog/*.cs` needed (this is interactive, not a list-of-ops).
- Nav wiring: add `<NavigationViewItem Content="GIF Studio · 螢幕轉 GIF" Tag="module.giflab">` in `MainWindow.xaml` near `module.recorder`/`module.capture`; add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) for master search; wire the Tag in `MainWindow.xaml.cs` (`MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page giflab`).
- Engine/install: winget `Gyan.FFmpeg` via `EngineBars.AutoInstallButton(...)` shown in an InfoBar when ffmpeg is missing — mirror the pattern already in `ScreenRecorderModule.xaml.cs` (EngineBar + recheck/rescan callbacks).
- Key APIs/CLIs to call: `ffmpeg -f gdigrab -framerate N -offset_x X -offset_y Y -video_size WxH -i desktop frame%05d.png` (capture); GIF export `ffmpeg -i frame%05d.png -vf "fps=N,scale=W:-1:flags=lanczos,palettegen" pal.png` then `paletteuse`; MP4 `-c:v libx264 -pix_fmt yuv420p`; APNG `-f apng -plays 0`. Use `ShellRunner.Capture` to run ffmpeg and surface progress/errors.

## Dependencies & risks
- Requires ffmpeg on PATH (handled by AutoInstallButton); guard every export behind a presence check.
- DPI: `RegionSelector` returns physical pixels — pass coordinates straight to `gdigrab` without WinUI scaling.
- Disk/memory: long recordings produce many PNGs; cap duration, write to a temp dir, and clean up on close.
- Crop/reorder must rewrite or re-index frames consistently before encode; renumber files (or build an explicit input list) so ffmpeg sees a contiguous sequence.
- Large GIFs: warn on huge dimensions/length; default scale-down option.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav under Media and in master search; record → frame strip → delete/reorder/crop → export GIF and MP4 all work end-to-end; ffmpeg-missing shows the AutoInstallButton InfoBar; every user-facing string is bilingual (English + 粵語); all file/folder pickers use `FileDialogs` (no WinRT pickers); temp frames cleaned up.
