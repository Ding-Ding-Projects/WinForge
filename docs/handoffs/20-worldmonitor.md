# Handoff: WorldMonitor

| | |
|---|---|
| **Status** | Not started |
| **Source** | https://github.com/koala73/worldmonitor |
| **License** | AGPL-3.0-only (open source). Note: AGPL is copyleft and network-triggered. WinForge must NOT statically link or bundle modified WM source; embedding the public hosted web app in a WebView2, or shelling out to the unmodified upstream desktop binary, keeps WinForge cleanly separated. Do not vendor or recompile WM TS/Rust into WinForge. |
| **Proposed module** | World Monitor · "Information / Web" group · Tag `module.worldmonitor` |
| **Effort** | M — no native engine to build; effort is WebView2 hosting, deep-link/variant UI, and bilingual chrome. Goes to L only if we also wrap the Tauri desktop binary. |

## What the user asked for
Bring koala73/worldmonitor — a real-time global intelligence dashboard (news, geopolitics, finance, energy, instability index) — into WinForge as an integrated module rather than an external redirect.

## Recommended approach
**Hybrid (WebView2 embed + native WinForge chrome).** Per the global strategy, native C# is preferred, but WorldMonitor is a vanilla-TypeScript + Vite app built on globe.gl/Three.js (3D globe), deck.gl/MapLibre (WebGL flat map), Tauri 2 (Rust) desktop shell, a Node.js sidecar, AI providers (Ollama/Groq/OpenRouter/Transformers.js), and 65+ live data providers behind Vercel Edge Functions + a Railway relay. Reimplementing the WebGL globe, the correlation engine, and 500+ feed integrations in WinUI is far out of scope. This is squarely the "large Electron/Tauri/WebGL app" case where we wrap rather than clone.

Realistic v1: host the hosted web app (or a locally-run `npm run dev` instance) in a `WebView2` filling the page, with a native WinForge toolbar for variant switching, reload, external-browser fallback, and zoom. No scraping, no reimplementation of the dashboard itself.

## Features to implement (v1 → later)
- v1: WebView2 fills the module; native top bar with variant picker (world / tech / finance / commodity / energy / happy), Reload, Home, and "Open in browser"; remember last variant in settings; bilingual chrome.
- v1: Offline/error InfoBar when the embed fails to load (no network), with retry.
- later: Optionally wrap the upstream **Tauri desktop binary** — install via winget if/when a package exists, otherwise download the signed release — and launch it instead of WebView2 for full 3D-globe performance.
- later: Local self-host mode (run the dev server / sidecar) gated behind a toggle; pass user-supplied AI provider keys through WinForge settings, never hard-coded.

## Integration plan (WinForge specifics)
- New files: `Pages/WorldMonitorModule.xaml(.cs)` (hosts `Microsoft.Web.WebView2.WinUI.WebView2`), `Services/WorldMonitorService.cs` (variant→URL map, settings persistence, optional binary launch via `ShellRunner`).
- Nav wiring: add `NavigationViewItem Tag="module.worldmonitor"` in `MainWindow.xaml` under the Information/Web group; add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) for master search; wire the Tag in `MainWindow.xaml.cs` `MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page worldmonitor` deep-links.
- Engine/install: winget id `n/a` for v1 (web embed needs no binary; WebView2 Runtime ships with Windows 11). If wrapping the desktop binary later and no winget id exists, use a download+verify flow rather than `EngineBars.AutoInstallButton`.
- Key APIs/CLIs to call: none server-side. WebView2 `Source`/`Reload`/`ExecuteScriptAsync`; `ShellRunner.Run` only if launching a wrapped binary; `FileDialogs` (never WinRT pickers) if a self-host folder must be chosen.

## Dependencies & risks
- AGPL compliance: keep WM as a separate process/web origin; do not fork its code into WinForge.
- WebView2 Runtime must be present (assume yes on Win11; detect and show an InfoBar otherwise).
- Hosted app availability and upstream URL/variant changes can break the embed — make the URL map config-driven.
- AI features and some feeds require third-party keys / network; surface this, don't block the core map.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav; WebView2 loads the selected variant; variant switch + reload + open-in-browser work; load failure shows a bilingual retry InfoBar; all chrome strings have English AND Cantonese (粵語); no WinRT pickers.
