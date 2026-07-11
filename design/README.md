# WinForge · 視窗鑄造 — Documentation, Wiki & Simulators

A bilingual (English + 粵語) documentation site for **WinForge**, a Windows 11 control center with 318 in-app modules, 895 tweak-catalog items, and 1,213 total app features. Includes a searchable module manual, the full wiki, a working in-browser **app replica**, and playable **nuclear reactor**, **fuel factory** and **cake factory & farm** simulators.

## Pages

| File | What it is |
|---|---|
| `index.html` | GitHub Pages entry — redirects to the site |
| `WinForge.dc.html` | Landing page + global search + module manual + wiki reader + pricing |
| `App.dc.html` | Working WinUI-style app replica (deep links land & auto-run here) |
| `Reactor.dc.html` | Playable PWR nuclear-reactor control room (burns `.fuel`, writes the power bus) |
| `FuelFactory.dc.html` | Fabricates enriched fuel assemblies → signed `.fuel` files |
| `CakeFarm.dc.html` | Reactor-powered cake factory & farm → signed `.cake` files |
| `winforge-data.js` | All module / feature / wiki data (generated) |
| `assets/`, `content/` | Screenshots and source wiki markdown |

## How it fits together

```
Fuel Factory → .fuel → Reactor → electrical power → Cake Factory → cakes ($) → AI Agents
```

- The reactor writes its power status to `localStorage` (`winforge.reactor.v1`); the cake factory reads it as its power bus.
- The fuel factory and cake factory mint **real, signed, downloadable files** (`.fuel` / `.cake`). Load a `.fuel` into the reactor to refuel; import a `.cake` to validate/eat it.
- All simulator state is saved to `localStorage`, so closing the tab does not lose progress.
- Site deep links open the app replica **silently** (background iframe) and press the target control.
- Package Manager documentation is authored under `docs/wiki/` and embedded into `winforge-data.js` by `tools/regen-site-data.ps1`; its source-selection contract is therefore kept in the GitHub Pages wiki as well as the shipped README. · 套件管理器文件會喺 `docs/wiki/` 編寫，再由 `tools/regen-site-data.ps1` 嵌入 `winforge-data.js`；所以來源揀選契約會同時保留喺 GitHub Pages wiki 同 shipped README。

## Deploy to GitHub Pages

1. Push this folder to a GitHub repo.
2. Settings → Pages → Source: **Deploy from a branch** → `main` / root.
3. Open `https://<user>.github.io/<repo>/` — `index.html` forwards to the site.

No build step required; everything is static.

---
*Simulators are training toys — they control no real hardware.*
