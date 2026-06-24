# Handoff: yt-dlp Downloader

| | |
|---|---|
| **Status** | Not started |
| **Source** | CLI: yt-dlp (github.com/yt-dlp/yt-dlp) |
| **License** | The Unlicense (public domain; open source). Bundled/used ffmpeg is LGPL/GPL — install separately, do not redistribute. |
| **Proposed module** | Media Downloader · Media / Tools group · Tag `module.ytdlp` |
| **Effort** | M — no native reimplementation; effort is in a rich WinUI front-end, robust progress parsing, and format selection UX. |

## What the user asked for
A rich WinUI front-end over the yt-dlp CLI: paste one or more URLs, list available formats (`yt-dlp -F`), pick quality/format, download audio or video, handle playlists and subtitles, set an output template, parse live progress, and embed thumbnail/metadata. Reuse the existing ffmpeg integration. Install yt-dlp via `winget yt-dlp.yt-dlp`.

## Recommended approach
**CLI/binary wrap.** yt-dlp is a large, fast-moving Python project whose value is its constantly-updated extractors for 1000+ sites — cloning that in C# is infeasible and would rot immediately, so per the global strategy this is a legitimate wrap. WinForge already wraps CLIs via `ShellRunner` and already integrates ffmpeg, so we build a self-contained WinUI module that shells out to `yt-dlp.exe` and never redirects the user elsewhere. Realistic v1: paste URL → probe formats → pick → download with live progress to a chosen folder, plus audio-only and subtitle toggles.

## Features to implement (v1 → later)
- v1: URL input (multi-line, one per line); "List formats" runs `yt-dlp -F <url>` and shows results in a selectable list/grid; quality preset dropdown (Best video+audio, 1080p, 720p, Audio only MP3/M4A); output folder picker (FileDialogs) + output template box (default `%(title)s [%(id)s].%(ext)s`); Download button with live progress bar + %/speed/ETA parsed from stdout; cancel; subtitle checkbox (`--write-subs --sub-langs en,zh`); embed thumbnail + metadata toggles (`--embed-thumbnail --embed-metadata`).
- later: Full playlist UI (items list, range `--playlist-items`); concurrent/queue downloads; download archive (`--download-archive`); cookies-from-browser; sponsorblock; clipboard auto-detect; per-site presets; remember last settings; format filtering by codec/res; auto-update yt-dlp (`-U`).

## Integration plan (WinForge specifics)
- New files: `Services/YtDlpService.cs` (build arg lists, run via `ShellRunner.Capture`, async streaming for progress, parse `-F` table and `[download] xx.x%` lines, locate ffmpeg, expose events); `Pages/YtDlpModule.xaml` + `.cs` (URL box, format list, presets, folder/template, progress, log pane). Optionally `Catalog/YtDlpOperations.cs` for TweakCard-style maintenance ops (update yt-dlp, clear cache).
- Nav wiring: add `NavigationViewItem Tag="module.ytdlp"` in `MainWindow.xaml` (Media / Tools group); add a `ModuleRegistry` entry (`Services/ModuleRegistry.cs`) for master search; wire the Tag in `MainWindow.xaml.cs` `MapType` and `NavView_SelectionChanged`, plus `ApplyStartPage` for `--page ytdlp`.
- Engine/install: `EngineBars.AutoInstallButton("yt-dlp.yt-dlp", "Install yt-dlp", "安裝 yt-dlp", recheck, rescan)`; also offer ffmpeg install (`Gyan.FFmpeg`) if not already present, and pass `--ffmpeg-location` to yt-dlp.
- Key CLIs to call: `yt-dlp -F <url>`, `yt-dlp -f <id> -o <template> -P <dir> [--write-subs --sub-langs ...] [-x --audio-format mp3] [--embed-thumbnail --embed-metadata] [--ffmpeg-location <path>] <url>`, `yt-dlp -U`, `yt-dlp --version`. Use `--newline` and `--progress` for parseable progress; `--no-color` to keep stdout clean.

## Dependencies & risks
- Requires `yt-dlp.exe` and `ffmpeg.exe` on disk; detect both before enabling Download and surface install bars when missing.
- Progress/format output format can shift between yt-dlp versions — parse defensively, keep a raw log pane as fallback.
- Long-running processes: stream output off the UI thread; support clean cancel/kill and dispose the process.
- Some sites need cookies/auth or are geo-blocked — out of v1 scope; show yt-dlp's stderr clearly.
- Every user-facing string needs English + Hong Kong Cantonese (粵語).

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav under Media / Tools.
- Core flow works: paste a URL, list formats, pick a quality, choose an output folder, download with live progress, and produce the file; audio-only and subtitle options work.
- Install bars appear when yt-dlp/ffmpeg are missing and `AutoInstallButton` installs them via winget.
- All UI strings bilingual (English + Cantonese); folder selection uses `FileDialogs` (Win32 COM), never WinRT pickers.
