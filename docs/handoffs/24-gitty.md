# Handoff: Gitty (Git workflow shortcuts)

| | |
|---|---|
| **Status** | Not started |
| **Source** | https://github.com/Omibranch/gitty (Go CLI, archived) |
| **License** | MIT (open source) |
| **Proposed module** | Fold into existing **Git & GitHub** module · "Developer" nav group · existing Tag `module.git` (no new module) |
| **Effort** | M — no new page/nav; add a "Workflows" section + alias store inside `GitHubModule`, reusing `GitService`/`ShellRunner`. |

## What the user asked for
Bring Gitty's value into WinForge. Gitty is a Go CLI that collapses common git + GitHub chores into short, human-readable commands (`gitty up`, `gitty fix`, `gitty checkpoint`, aliases via `.gittyconf`). The seed note asks to fold useful features into the existing Git & GitHub module rather than ship a separate module.

## Recommended approach
**Native C# clone, folded into the existing module — do NOT wrap the Go binary.** Gitty has no library or daemon; it is just a thin, opinionated front-end over `git`/`gh`, exactly the layer `GitHubModule` already is. Wrapping a 2-star archived Go binary adds an install dependency for zero gain, and WinForge already has `GitService` (init/stage/commit/branch/sync, `RunRaw`, chunked upload) plus the full `GitOperations`/`GitHubOperations` catalogs. v1 = reimplement Gitty's *workflows* as WinForge buttons/cards and add a per-repo alias store. Skip Gitty's symbolic syntax (`=` "to", `~` "from", `*` "in"); a GUI does not need terse command parsing — buttons and fields are clearer.

## Features to implement (v1 → later)
- v1: "Up" one-click = `add -A` + `commit -m <msg>` + `push` (Gitty `up`); reuse the existing commit box, add a "Commit & push" toggle.
- v1: "Checkpoint" = create + push a tag in the current/selected branch (`gitty checkpoint`); "Restore" = checkout a tag (warn about detached HEAD).
- v1: "Undo last commit" soft-reset keeping staged changes (`gitty undo` = `reset --soft HEAD~1`).
- v1: Per-repo **alias store** (mirrors `.gittyconf`): name → ordered list of git/gh ops, run sequentially; persist as JSON next to RepoStore. Surface saved aliases as one-click buttons.
- v1: "Push & share" = push then copy the repo/PR GitHub URL to clipboard (`gh repo view --json url` / `pr view`).
- later: Interactive merge-conflict resolver (`gitty fix`) — parse `<<<<<<< ======= >>>>>>>` markers per file, let the user pick ours/theirs/both, restage. Partial commit by line range (`gitty pick`) via `git apply --cached` on a generated patch. Time-filtered graph log (`gitty log --6h/--3day`). Restore a file from N commits ago (`gitty back <file> N`).

## Integration plan (WinForge specifics)
- New files: `Services/GitWorkflows.cs` (Up/Checkpoint/Restore/Undo/ShareUrl helpers over `GitService.RunRaw`); `Services/GitAliasStore.cs` (load/save/run aliases, JSON in the app data dir). No new Page.
- Edit existing: `Pages/GitHubModule.xaml(.cs)` — add a "Workflows / 工作流程" expander with the buttons above and an alias list/editor; reuse `AppendConsole`, `Refresh`, `BuildQuickActions`. Optionally add Gitty ops as `GitTweak`/`TweakDefinition` entries in `Catalog/GitOperations.cs` so they appear in the operation library + master search.
- Nav wiring: none — `module.git` is already registered in `MainWindow.xaml`, `ModuleRegistry`, and `MapType`/`NavView_SelectionChanged`. Just extend `ModuleRegistry` keywords (add "gitty, up, checkpoint, alias, undo").
- Engine/install: winget id `n/a`. Relies on existing `git` (and `gh` for share/PR). If `gh` is missing, reuse the existing AutoInstallButton pattern (`GitHub.cli`).
- Key CLIs to call: `git add/commit/push/tag/reset/checkout/log/apply`, `gh repo view --json url`, `gh pr view`.

## Dependencies & risks
- Conflict resolver and `pick` line-range patching are fiddly (encoding, CRLF, hunk offsets) — defer to "later" and ship the simple workflows first.
- Tag restore leaves a detached HEAD; warn the user bilingually before running.
- Run every multi-step alias through `ShellRunner`/`GitService` off the UI thread; stop the chain on first failure and report which step failed.
- Quote/escape commit messages and aliases (existing `Commit_Click` already replaces `"` → `'`).

## Acceptance criteria
- Builds clean (Debug + Release x64); no new module — features live in the existing Git & GitHub page; "Up", "Checkpoint", "Undo", alias save/run, and "Push & share" all work against a selected repo; aliases persist across restarts; every user-facing string is bilingual (English + Cantonese); uses FileDialogs (never WinRT pickers) for any folder/file picking; output streams to the existing console pane.
