# Handoff: VLC Media Player (Embedded)

| | |
|---|---|
| **Status** | Not started |
| **Source** | LibVLCSharp (github.com/videolan/libvlcsharp) + libVLC (github.com/videolan/vlc); NuGet `LibVLCSharp`, `LibVLCSharp.WinUI`, `VideoLAN.LibVLC.Windows` |
| **License** | libVLC engine: LGPL-2.1+; LibVLCSharp: LGPL-2.1+ (open source). Note: bundled codecs/plugins may pull GPL components — ship the LGPL libVLC build to stay LGPL. |
| **Proposed module** | Media Player · Media / Tools group · Tag `module.mediaplayer` |
| **Effort** | L — embedding native libVLC + WinUI video surface, playlist, track/subtitle UI, and transcode pipeline is substantial but well-trodden. |

## What the user asked for
Embed a real media player inside WinForge using the actual VLC engine (libVLC via LibVLCSharp), not a redirect to the VLC app. Open files/URLs/streams, play/pause/seek, manage a playlist, switch audio/subtitle tracks, take snapshots, and convert/transcode media — effectively VLC living inside the WinForge GUI.

## Recommended approach
**Hybrid (embed libVLC).** Per the global strategy, VLC's core is a large C/C++ codebase that cannot be reimplemented natively in reasonable scope, but it ships as an embeddable native library with first-class .NET bindings. So we embed the *same engine VLC uses* rather than wrapping the standalone app — giving a fully in-GUI experience with no external redirect. Use `LibVLCSharp.WinUI`'s `VideoView` (SwapChainPanel-backed) as the render surface and the `VideoLAN.LibVLC.Windows` NuGet to bundle the native `libvlc.dll`/plugins (no separate install needed). Keep `winget VideoLAN.VLC` only as an optional fallback for extra codecs/users who want the full desktop app.

Realistic v1: a working playback module (open file/URL, transport controls, seek, volume, fullscreen, playlist, track selection, snapshot). Transcode is a libVLC `--sout` chain — ship a small preset-based v1, expand later.

## Features to implement (v1 → later)
- v1: Open local file (FileDialogs) / paste URL or stream; play, pause, stop, seek bar with time display, volume/mute; basic playlist (add/remove/next/prev); audio + subtitle track dropdowns; load external subtitle file; PNG snapshot of current frame; fullscreen toggle.
- later: Transcode/convert with presets (MP4/H.264, MP3, GIF) via libVLC `--sout` or wrapped ffmpeg; equalizer/audio filters; playback speed; A-B loop; thumbnail scrubbing; network stream recording; remember position; subtitle delay/sync; chapter/title menus for DVDs/Blu-ray.

## Integration plan (WinForge specifics)
- New files: `Services/MediaPlayerService.cs` (owns `LibVLC` + `MediaPlayer`, exposes play/seek/track/snapshot/transcode), `Pages/MediaPlayerModule.xaml` + `.cs` (hosts `VideoView`, transport bar, playlist pane), optionally `Catalog/MediaOperations.cs` (transcode presets as TweakCard-style ops).
- Nav wiring: add `NavigationViewItem Tag="module.mediaplayer"` in `MainWindow.xaml` under the Media/Tools group; add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) for master search; wire the Tag in `MainWindow.xaml.cs` `MapType` and `NavView_SelectionChanged`, plus `ApplyStartPage` for `--page mediaplayer` deep-link.
- Engine/install: native engine ships via the `VideoLAN.LibVLC.Windows` NuGet, so no runtime install is required. Add an optional `EngineBars.AutoInstallButton("VideoLAN.VLC", "Install full VLC (extra codecs)", "安裝完整版 VLC（額外編解碼器）", recheck, rescan)` as a fallback InfoBar.
- Key APIs: `Core.Initialize()`, `new LibVLC()`, `new MediaPlayer(libVLC)`, `mediaPlayer.Hwnd`/`VideoView.MediaPlayer`, `new Media(libVLC, uri, FromType.FromLocation/FromPath)`, `Play/Pause/Stop`, `Time`/`Position`/`Length`, `AudioTrack`/`SpuTrack` + `Tracks`, `AddSlave` (subtitles), `TakeSnapshot`, and `:sout=#transcode{...}:std{...}` for conversion.

## Dependencies & risks
- NuGet: `LibVLCSharp`, `LibVLCSharp.WinUI`, `VideoLAN.LibVLC.Windows` (native binaries, large ~40MB+ — inflates installer size).
- Must call `Core.Initialize()` before any libVLC use; WinUI rendering uses `SwapChainPanel` and is sensitive to thread/DPI — test windowed + fullscreen.
- Native lib pinned to x64; ensure plugins folder is deployed alongside `libvlc.dll`.
- License: stay on LGPL libVLC build; document third-party notices.
- Dispose `MediaPlayer`/`LibVLC` on page unload to avoid native leaks.
- Every user-facing string needs English + Hong Kong Cantonese (粵語).

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav under Media/Tools.
- Core flow works: open a local file and a URL, play/pause/seek, adjust volume, build a playlist, switch audio/subtitle tracks, take a snapshot.
- Native libVLC loads from the bundled NuGet with no separate install.
- All UI strings bilingual (English + Cantonese); file/folder selection uses `FileDialogs` (Win32 COM), never WinRT pickers.
