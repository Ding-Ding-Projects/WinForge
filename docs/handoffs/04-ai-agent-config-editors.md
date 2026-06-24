# Handoff: AI agent config editors (Claude / Codex / opencode / OpenClaw / Hermes)

| | |
|---|---|
| **Status** | Not started |
| **Source** | Local: `Services/AiAgentService.cs` + `Pages/AiAgentsModule.xaml(.cs)`; each tool's on-disk config (no upstream repo to clone — we read/write user config files) |
| **License** | n/a — WinForge feature editing the user's own local config files. Config formats are open (JSON / TOML / Markdown). |
| **Proposed module** | Extends existing **AI Agents** module · *Apps & Dev* group · existing Tag `module.aiagents` (no new nav entry) |
| **Effort** | M — UI is one editor panel per card; the work is correct per-agent path resolution, TOML/JSON awareness, and atomic save. |

## What the user asked for
In the AI Agents module, add a per-agent **config editor**: locate each tool's config file, load its current contents into an in-app editor, let the user edit, then save back. Expose model/provider/API-key fields where the format is known.

## Recommended approach
**Native C# clone (in-app editor).** Per the global strategy this is trivially native: these are plain local text files (JSON/TOML/Markdown), so no binary to wrap. Reuse the existing `AiAgentsModule` cards — add a "Config" expander to each card rather than a new module. v1 = a generic load/edit/save text editor (TextBox `AcceptsReturn`, monospace) per known config path, with a file picker fallback when the path is missing. Round-tripping arbitrary JSON/TOML through typed UI fields is brittle, so keep raw-text editing as the source of truth; offer convenience fields (model / provider / API key) as *additive helpers* only for Claude/Codex where the schema is stable.

## Features to implement (v1 → later)
- v1: Per agent, resolve config path(s); "Load current" reads file (or shows "not created yet"); multiline monospace editor; "Save" writes atomically (temp + replace) creating parent dir if needed; "Open folder" and "Browse…" (FileDialogs); JSON validity check before save with a non-blocking warning.
- v1: For Claude Code show `settings.json` **and** `CLAUDE.md` (tabbed/segmented); for opencode show `opencode.json` **and** `AGENTS.md`.
- later: Convenience fields parsed from config (model, provider, base URL); API-key write into the config file (not just env var); TOML pretty-validate for Codex; backup-before-save (`.bak`); diff view of unsaved changes.

## Integration plan (WinForge specifics)
- New files: `Services/AiAgentConfigService.cs` (path resolution + safe read/write, defensive, never throws — mirror `AiAgentService` style returning `TweakResult`). Extend `Pages/AiAgentsModule.xaml.cs` `BuildCard(...)` to append an `Expander` ("Config · 設定") containing the editor. No new XAML page, no `Catalog/*` needed.
- Data model: add `IReadOnlyList<AiConfigFile> ConfigFiles` to the `AiAgent` record in `AiAgentService.cs`, each with `{ LabelEn, LabelZh, RelativePath, Kind = Json|Toml|Markdown }`.
- Path resolution (use `%USERPROFILE%` via `Environment.SpecialFolder.UserProfile`; honor `XDG_CONFIG_HOME` when set, else `~/.config`):
  - **Claude Code**: `~/.claude/settings.json`, `~/.claude/CLAUDE.md`
  - **Codex**: `~/.codex/config.toml`
  - **opencode**: `~/.config/opencode/opencode.json` (also `~/AGENTS.md` / project `AGENTS.md`)
  - **Pi**: research at build time (likely `~/.pi/` — confirm, optional for v1)
  - **OpenClaw**: research at build time — gateway config likely under `~/.openclaw/` or `~/.config/openclaw/`; if not found, expose Browse-to-locate. Do not hard-fail.
  - **Hermes**: research at build time — likely `~/.hermes/`; same Browse fallback.
- Nav wiring: none new — module already registered (`ModuleRegistry` line 76, `MapType` line 413, `NavView_SelectionChanged` line 516).
- Engine/install: winget id `n/a` (no binary). Existing Node.js `EngineBars.AutoInstallButton` stays.
- Key APIs to call: `System.IO.File.ReadAllText/WriteAllText`, `Directory.CreateDirectory`, `System.Text.Json` (validate), `FileDialogs.OpenFolderAsync` / file picker, `Loc.I.Pick` for bilingual strings.

## Dependencies & risks
- Path drift: OpenClaw/Hermes/Pi config locations are not authoritatively documented in-repo — implementer must verify before shipping and always provide the Browse fallback so a wrong default never blocks the user.
- Editing while the agent is running could be overwritten by the tool; show an info note. Never auto-format/reorder unknown JSON/TOML (lossy) — write back the user's text verbatim.
- Secrets: config may contain API keys — do not log file contents; reuse the elevation-safe `FileDialogs`, never WinRT pickers.

## Acceptance criteria
- Builds clean (Debug + Release x64); AI Agents module still appears in nav; each card gains a working Config expander.
- Load → edit → Save round-trips for at least Claude `settings.json`/`CLAUDE.md`, Codex `config.toml`, opencode `opencode.json`/`AGENTS.md`; missing file is handled gracefully (create on save).
- Invalid JSON is flagged before overwrite; parent directories auto-created; all user-facing strings bilingual (English + 粵語); no WinRT pickers.
