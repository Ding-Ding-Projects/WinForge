# Handoff: README + Wiki Screenshots Refresh

| | |
|---|---|
| **Status** | Completed for current docs pass — README/wiki expanded; canonical screenshot inventory promoted to `docs/`; pending module captures added |
| **Source** | Local: `README.md`, `docs/screenshot-*.png`, `docs/features/`, plus the GitHub wiki (`https://github.com/codingmachineedge/WinForge/wiki`) |
| **License** | Project: MIT (see `LICENSE`). Screenshots and docs ship under the same repo license. |
| **Proposed module** | Docs task — no new module. (Touches `Documentation` section only; not a NavigationView page.) |
| **Effort** | M — mechanical but high-volume: ~20+ pages to capture, two surfaces (README + wiki) to keep in sync. |

## What the user asked for
Capture fresh screenshots of every major module by actually running the app and screenshotting each page (via computer-use), save them under `docs/`, and embed many more of them in `README.md` and the GitHub wiki. Also write/refresh prose documenting the newer modules.

## Recommended approach
Pure documentation — no native clone or CLI wrap applies (global strategy is N/A here). The deliverable is images plus markdown. Realistic v1: a complete, current screenshot set under `docs/`, a README that shows far more than the two images it has today (`screenshot-dashboard.png`, `screenshot-git.png`), and a wiki page per major module. Be honest: this requires building and launching the app, so it cannot be done by editing files alone — it needs a Debug x64 build and computer-use to drive the UI and capture each page.

## Features to implement (v1 → later)
- v1: Recapture existing stale shots and ADD missing ones. Current repo state: `docs/` has 147 `screenshot-*.png` files after promoting the tracked wiki image set and adding dedicated captures for SSH Toolset, Battery & Thermal, Nilesoft Shell, Activity Timeline, Audio Editor, qBittorrent, AltSnap, Taskbar Tweaker, LightSwitch, and ViaProxy. `screenshot-cloudflare.png`, `screenshot-aiagents.png`, `screenshot-settingshub.png`, and `screenshot-packages.png` are no longer placeholders.
- v1: Embed a "Suite modules" gallery in `README.md` (one image per major module: Dashboard, Git/GitHub, Package Manager, Cloudflare, AI Agents, Media, Settings hub, Clipboard, SSH/Connections), each with a bilingual one-line caption.
- v1: One wiki page per major module, each embedding its screenshot via the raw GitHub URL (`https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/<file>.png`).
- later: Animated GIFs for flagship flows (chunked uploader in Git/GitHub, batch update in Package Manager). A short "module index" wiki home page linking every page.

## Integration plan (WinForge specifics)
- New files: none in code. New/updated assets: `docs/screenshot-<key>.png` per module (follow existing `screenshot-<key>.png` naming — e.g. `screenshot-cloudflare.png`); edits to `README.md`; new wiki markdown pages.
- Nav wiring: N/A (no module). To know which pages exist, enumerate `Pages/*Module.xaml` and the `module.<key>` Tags in `MainWindow.xaml` / `Services/ModuleRegistry.cs`; each visible nav item = one page to screenshot.
- Capture method: build Debug x64, launch the app, navigate each NavigationView item, and screenshot the content area. Use a consistent window size and the default leading language; let bilingual text show naturally in-shot. Crop to the app window.
- Engine/install: winget id n/a.
- Key APIs/CLIs to call: none. The only "API" is the GitHub wiki (clone `git@github.com:codingmachineedge/WinForge.wiki.git`, add pages, push). README images use repo-relative paths (`docs/...`); wiki images must use absolute `raw.githubusercontent.com` URLs since the wiki is a separate repo.

## Dependencies & risks
- Requires a working Debug x64 build and computer-use to drive the UI — cannot be completed by file edits alone.
- Some modules need elevation or external binaries (gh, winget) to render meaningful content; capture realistic states, not empty/error panes.
- Keep README and wiki in sync — drift is the main long-term risk. Note the canonical screenshot set lives in `docs/`.
- Bilingual: every caption added to README/wiki needs English AND Cantonese (粵語), matching the repo's existing table style.

## Acceptance criteria
- Builds clean (Debug + Release x64) — unchanged, since this is docs-only, but verify the build used for capture is current.
- A current screenshot exists in `docs/` for every major module, including the four confirmed-missing ones.
- `README.md` embeds a multi-image module gallery (not just the two current shots) with bilingual captions.
- The GitHub wiki has one page per major module, each with a working embedded image via a raw URL.
- All new user-facing captions are bilingual (English + 粵語); no broken image links on GitHub.
