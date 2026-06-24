# Handoff: Resume & Cover-letter Writer (AI)

| | |
|---|---|
| **Status** | Not started |
| **Source** | CLI: opencode (`opencode`) / Claude Code (`claude`) — reuse `Services/AiAgentService.cs` |
| **License** | App code is WinForge's own; opencode is open source (MIT). Underlying model usage governed by the user's own API key / agent subscription. |
| **Proposed module** | Resume Writer · "AI Agents" or "Documents" nav group · Tag `module.resume` |
| **Effort** | M — UI + persistence are straightforward; the only real work is prompt crafting, non-interactive CLI capture, and optional .docx/.pdf export. |

## What the user asked for
A module where the user stores one or more "base resumes" (markdown/rich text), pastes a target job description, clicks Generate, and WinForge shells out to an installed AI coding agent (opencode or claude) in non-interactive mode with a crafted prompt. It returns a tailored resume + matching cover letter in editable panes, saved alongside earlier outputs and exportable to md/txt (ideally .docx/.pdf).

## Recommended approach
**Hybrid (native WinUI front-end wrapping an existing CLI).** The "AI" cannot be cloned in C#, so reuse the already-built `AiAgentService` to detect/launch an agent. Everything else — the editor panes, the base-resume library, the saved-output history, and export — is native WinUI. This matches the global strategy: rich GUI in WinForge, the wrapped tool is the only external binary, no redirects. v1 scope: two side-by-side editors (resume + cover letter), a base-resume picker, a job-description box, a Generate button that runs the agent once and captures stdout, plus md/txt save/export. .docx/.pdf and multi-version diffing are "later."

## Features to implement (v1 → later)
- v1: Base-resume library (add/rename/delete, markdown stored in SettingsStore JSON); job-description textbox; agent picker (default to first installed opencode/claude); Generate button → non-interactive run → split output into resume + cover-letter editors; manual edit; save generated output to history; export to .md and .txt via FileDialogs.
- v1: InfoBar + `EngineBars.AutoInstallButton` when no agent is installed; reuse `AiAgentService.IsInstalledAsync` / `NodeAvailableAsync`.
- later: .docx export (reuse a docx skill/library or template), .pdf export, multiple cover-letter tones, side-by-side version compare, "regenerate just the cover letter," tokens/cost note, import base resume from an existing .docx/.pdf/.txt file.

## Integration plan (WinForge specifics)
- New files: `Services/ResumeWriterService.cs` (build the prompt, run the agent non-interactively via `ShellRunner.Capture`/`Run`, parse the two sections from stdout, never throw), `Services/ResumeStore.cs` (base resumes + generated outputs, modeled on `Services/RepoStore.cs` — a static store persisting a JSON list to `SettingsStore` under keys like `resume.bases` and `resume.outputs`, with a `Changed` event), `Pages/ResumeWriterModule.xaml(.cs)`.
- Nav wiring: add a `NavigationViewItem Tag="module.resume"` in `MainWindow.xaml` (near `module.aiagents`); add a `ModuleRegistry.All` entry (keywords: `resume cv cover letter job application tailor ai 履歷 求職信 應徵`); add `"module.resume" => typeof(ResumeWriterModule)` to `MapType` and a `case "module.resume":` in `NavView_SelectionChanged`; optionally extend `ApplyStartPage` for `--page resume`.
- Engine/install: winget id `n/a` — agents install via the existing AI Agents module (npm/official). Surface that with `EngineBars.AutoInstallButton("OpenJS.NodeJS.LTS", …)` only for the Node prerequisite; otherwise point the user at the AI Agents module.
- Key APIs/CLIs to call: `AiAgentService.All` / `.IsInstalledAsync` / `.GetEnvKey`; run non-interactively, e.g. `opencode run "<prompt>"` or `claude -p "<prompt>"` (verify exact non-interactive flag per CLI `--help`). Capture stdout with `ShellRunner.Capture`/`Run` using a `CancellationToken`. Use `Services/FileDialogs.cs` for all save/open (never WinRT pickers).

## Dependencies & risks
- Non-interactive flag differs per agent (`opencode run` vs `claude -p`); confirm at build time and gate the Generate button on a detected, supported agent.
- Long prompts (full resume + JD) may hit shell arg-length limits — pass the prompt via a temp file or stdin rather than a single command-line argument.
- Output parsing is fuzzy: instruct the model to emit clear delimiters (e.g. `===RESUME===` / `===COVER LETTER===`) and split on those, with a fallback that dumps everything into the resume pane.
- Generation can take 30s+ and needs a valid API key/auth — show a progress ring, allow cancel, and surface `GetEnvKey`-missing as a friendly bilingual InfoBar.
- .docx/.pdf export is optional for v1; keep md/txt as the guaranteed path.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav and master search; base resume can be saved and reused; pasting a JD and clicking Generate produces editable resume + cover-letter panes from a real agent run; outputs save to history and export to .md/.txt via FileDialogs; every user-facing string is bilingual (English + 粵語); no WinRT pickers; no unhandled exceptions when no agent/API key is present.
