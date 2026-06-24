# Handoff: Audio Editor (Audacity-style)

| | |
|---|---|
| **Status** | Not started |
| **Source** | github.com/audacity/audacity (C++/wxWidgets); native via NAudio + ffmpeg; winget fallback |
| **License** | Audacity is GPL-2.0-or-later. Do NOT port/copy its source. Our native code is original; ffmpeg is LGPL/GPL (ship/install, don't statically link GPL). NAudio is MIT. |
| **Proposed module** | Audio Editor · Media (or Multimedia) group · Tag `module.audioeditor` |
| **Effort** | L — waveform rendering + NAudio record/play is real work; ffmpeg-backed effects are individually small but numerous. A true full Audacity clone would be XL/infeasible. |

## What the user asked for
The user wants "entire Audacity features cloned" as an audio-editor tab inside WinForge — multitrack waveform editing, recording, playback, and a broad set of effects.

## Recommended approach
**Hybrid.** A faithful full clone of Audacity (its mixer, nondestructive editing engine, plug-in host, 30+ effects, label tracks, spectrogram) is XL and not realistic in C# in reasonable scope. Per the global strategy we ship the *core* natively and wrap a proven binary for the heavy lifting:
- **Native C# (NAudio):** device enumeration, record to WAV, transport (play/pause/stop/seek), and a custom waveform renderer (Win2D or a `CanvasControl`/Image drawing peak data) with selection ranges.
- **ffmpeg-backed effects:** destructive, file-to-file operations driven through `ShellRunner` — fast to add and cover most user intent.
- **Fallback:** an `EngineBars.AutoInstallButton` to install/launch real Audacity (`Audacity.Audacity`) for advanced editing we don't clone. Be explicit in-UI that this is a focused editor, not a 1:1 Audacity clone.

v1 scope: open/record one clip, view+select waveform, apply effects, export. Multitrack is "later".

## Features to implement (v1 → later)
- **v1:** Open audio (WAV/MP3/FLAC/M4A via ffmpeg decode to WAV scratch); record from mic (NAudio `WaveInEvent`); play/seek; waveform draw with selection; effects: **trim** (`-ss/-to`), **fade in/out** (`afade`), **normalize/loudnorm** (`loudnorm`), **gain** (`volume`), **speed** (`atempo`), **pitch shift** (`asetrate`+`atempo`), **noise reduction** (`afftdn`), **format convert**, **concat**, simple **mix** (`amix`); export to chosen format. "Launch/Install Audacity" fallback bar.
- **later:** true multitrack timeline + per-track mute/solo/gain, undo/redo history, spectrogram view, label/marker tracks, EQ (`firequalizer`/`equalizer`), compressor, reverb (`aecho`/`afir`), batch/macro processing, non-destructive editing.

## Integration plan (WinForge specifics)
- **New files:** `Services/AudioEngineService.cs` (NAudio record/play/device list, peak extraction), `Services/FfmpegAudioService.cs` (wraps `ShellRunner.Capture` for each effect, returns output path), `Pages/AudioEditorModule.xaml(.cs)` (waveform canvas, transport, effect panel), optionally `Catalog/AudioEffectsOperations.cs` (effects as `Tweak.Action` items rendered via `Controls/TweakCard` for the "list of ops" UX).
- **Nav wiring:** add `NavigationViewItem Tag="module.audioeditor"` under the Media group in `MainWindow.xaml`; add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) for master search; wire the Tag in `MainWindow.xaml.cs` (`MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page audioeditor`).
- **Engine/install:** ffmpeg via `EngineBars.AutoInstallButton("Gyan.FFmpeg", "Install FFmpeg", "安裝 FFmpeg", recheck, rescan)`; Audacity fallback via `EngineBars.AutoInstallButton("Audacity.Audacity", "Install Audacity", "安裝 Audacity", ...)`. NAudio is a NuGet package.
- **Key APIs/CLIs:** NAudio `WaveInEvent`/`WaveOutEvent`/`AudioFileReader`; `ffmpeg -i in -af "<filter>" out`; `ffprobe` for duration/peaks. Always use `Services/FileDialogs.cs` for open/save (never WinRT pickers). Write scratch files to a temp working dir; clean up on close.

## Dependencies & risks
- Add NAudio NuGet; ffmpeg/ffprobe must be on PATH or resolved via the install bar (gate effects behind a presence check + InfoBar).
- **GPL:** never copy Audacity source; keep ffmpeg as an external invoked binary, not linked.
- Waveform peak extraction on large files must be off the UI thread (async + downsampled peaks) to avoid freezes.
- Destructive ffmpeg pipeline means no true undo in v1 — keep originals and operate on copies; surface this clearly.
- Recording needs mic permission/device selection; handle "no input device" gracefully.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in the Media nav group and in master search.
- Core flow works: open or record → see waveform → select → apply at least trim/fade/normalize/gain → export a playable file.
- ffmpeg/Audacity install bars function; effects disabled with a clear message when ffmpeg is missing.
- All user-facing strings bilingual (English + 粵語); file dialogs use `FileDialogs`, no WinRT pickers; no UI-thread blocking on large audio.
