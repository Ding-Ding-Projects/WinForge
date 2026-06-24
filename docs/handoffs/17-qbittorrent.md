# Handoff: qBittorrent (Torrent Client)

| | |
|---|---|
| **Status** | Not started |
| **Source** | github.com/codingmachineedge/qBittorrent (upstream: github.com/qbittorrent/qBittorrent) · Qt/C++ |
| **License** | GPL-2.0-or-later (open source). WinForge wrapper code is original C#, so no license conflict. |
| **Proposed module** | qBittorrent · Apps / Downloads group · Tag `module.qbittorrent` |
| **Effort** | L — no native engine to write, but a full HTTP API client + live-updating torrent list UI is substantial. |

## What the user asked for
A torrent client inside WinForge: add torrents and magnet links, list/pause/resume/delete torrents, see speeds and progress, manage categories, and adjust core settings — all from a native WinUI surface.

## Recommended approach
**CLI/binary wrap (HTTP API front-end).** qBittorrent is a large Qt/C++ application with libtorrent-rasterbar at its core; reimplementing a BitTorrent engine in C# is out of scope (per global strategy step 2). qBittorrent ships a stable, documented Web API (WebUI). The right move is: install `qBittorrent.qBittorrent` via winget, run it with the WebUI enabled (default `http://localhost:8080`), and build a rich native WinUI front-end over its API. No external redirects — WinForge owns the UI; qBittorrent runs headless-ish in the background.

Realistic v1: connect to the local WebUI, authenticate, and provide the core torrent lifecycle (add/list/pause/resume/delete) with live progress and speeds.

## Features to implement (v1 → later)
- v1: Connect + login (`/api/v2/auth/login`, cookie session); torrent list with name, progress, state, DL/UL speed, ETA, size (`/torrents/info`); add torrent file via FileDialogs and add magnet (`/torrents/add`, multipart); pause/resume/delete (with "also delete files" toggle); global speed display; auto-refresh (timer ~1.5s) or `/sync/maindata`.
- later: categories + tags CRUD and assignment; per-torrent detail (files, trackers, peers); set global speed limits and alt-speed toggle; preferences pane (`/app/preferences`); drag-and-drop .torrent files; .torrent file association; RSS.

## Integration plan (WinForge specifics)
- New files: `Services/QBittorrentService.cs` (HttpClient + CookieContainer; methods Login, GetTorrents, AddFile, AddMagnet, Pause, Resume, Delete, GetGlobalStats, settings get/set), `Pages/QBittorrentModule.xaml(.cs)` (toolbar + ListView/ItemsRepeater of torrents, add bar, status footer). Optional `Catalog/QBittorrentOperations.cs` only if exposing settings as TweakCards.
- Nav wiring: add `NavigationViewItem Tag="module.qbittorrent"` in `MainWindow.xaml` under the Apps/Downloads group; add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) with bilingual title/keywords for master search; wire the Tag in `MainWindow.xaml.cs` `MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page qbittorrent` deep-link.
- Engine/install: winget id `qBittorrent.qBittorrent` surfaced via `EngineBars.AutoInstallButton("qBittorrent.qBittorrent", en, zh, recheck, rescan)`. On first run, detect WebUI reachability; if qBittorrent is installed but WebUI is off, show an InfoBar guiding the user to enable Options > Web UI (or launch it). Use `ShellRunner` to start the qBittorrent process if needed.
- Key APIs/CLIs to call: qBittorrent Web API v2 base `http://localhost:8080/api/v2/...` — `auth/login`, `torrents/info`, `torrents/add`, `torrents/pause`, `torrents/resume`, `torrents/delete`, `transfer/info`, `sync/maindata`, `app/preferences`. Use FileDialogs (Services/FileDialogs.cs) for picking .torrent files — never WinRT pickers.

## Dependencies & risks
- WebUI must be enabled and credentials/host configurable (store host, port, user in app settings). Default qBittorrent ships with a randomized WebUI password on recent versions — surface a clear connect/credentials dialog.
- Version skew: WebUI API is stable across v4/v5 but confirm endpoint shapes at build time.
- Background lifecycle: decide whether WinForge launches/keeps qBittorrent running; handle "not reachable" gracefully.
- Localhost-only by default; avoid exposing the WebUI beyond loopback.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav and master search; connect/login works; can add a magnet and a .torrent file; list shows live progress and speeds; pause/resume/delete work; all user-facing strings bilingual (English + 粵語); FileDialogs used for file picking, no WinRT pickers.
