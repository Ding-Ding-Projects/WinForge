---
name: winforge-exhaustive-smoke
description: "Run when verifying WinForge comprehensively: inventory every registered module, deep link, page, control surface, service path, and companion; build and exercise the app; capture evidence; triage defects; update documentation; and hand off Git changes safely. Use for requests such as 'smoke test every feature', 'test every page', 'capture screenshots of WinForge', or a feature-completeness audit."
---

# WinForge Exhaustive Smoke

## Purpose

Use this skill for an evidence-backed, repeatable verification campaign of the
WinForge WinUI application. It turns the live registry, navigation mappings,
XAML pages, services, tests, and documentation into an explicit coverage
ledger. It never calls a feature tested merely because the app builds.

This skill is deliberately conservative around user-machine side effects.
Exercise every UI and safe functional path; use dry-run, fixture, validation,
or existing automated-test paths for external, destructive, privileged, or
stateful actions unless the user explicitly authorizes live execution.

## Required Reading and Safety Gate

1. Read the repository's complete AGENTS.md before touching code, running the
   app, or deciding a test command. It is authoritative over this skill.
2. Read [references/coverage-schema.md](references/coverage-schema.md) before
   creating or updating a coverage ledger.
3. Inspect git status --short, git worktree list, current branch, and
   origin/main. Do not stage, revert, or fold in pre-existing work from other
   people or agents.
4. Work in a dedicated worktree when the current one is dirty or belongs to
   another task. Keep any smoke artifacts in an ignored, task-local artifacts
   directory until selected evidence is ready for docs.
5. Treat security settings, package installs, network scans, disk writes,
   system tweaks, external API calls, real-world reactor integrations,
   credential workflows, and companion launchers as safety-sensitive. Test
   rendering, validation, command construction, cancellation, and fixtures by
   default. Only perform a live side effect when it is explicitly in scope,
   reversible, and its before/after/revert evidence is recorded.

## What "Every Feature" Means

Build the inventory from code rather than a hand-written guess. The minimum
coverage universe is:

- every ModuleRegistry.All entry;
- every NavigationViewItem Tag, category/shell route, and MapType
  registration;
- every ApplyStartPage deep-link alias;
- every page, XAML control surface, dialog/flyout, companion, and external-app
  launcher reachable from a registered module;
- every public or user-invoked service action reachable from those pages;
- all existing test projects and their documented focused harnesses;
- every changed line, with a source-review/behavior mapping or an honest
  exemption.

"Line by line" is not permission to claim impossible source-line execution
without instrumentation. For each source file in scope, record the relevant
lines or methods, the behavior they implement, and the test, manual exercise,
or reason they are excluded. A green build is only compilation evidence.

## Campaign Workflow

### 1. Create a baseline and machine-readable inventory

Run the supplied extractor from the repository root:

~~~powershell
powershell -ExecutionPolicy Bypass -File "$env:USERPROFILE\.codex\skills\winforge-exhaustive-smoke\scripts\New-WinForgeSmokeInventory.ps1" -RepoRoot . -OutputDirectory artifacts\smoke\<campaign-id>
~~~

It writes manifest.json, manifest.csv, and summary.md. Review the
routingIssues and unmappedAliases arrays before marking anything as tested. The
extractor discovers routes; it does not prove that an action works.

Create a ledger from the manifest using the schema reference. Preserve these
states separately:

- discovery and static-routing evidence;
- build/test-harness evidence;
- launch evidence;
- visual/screenshot evidence;
- safe interaction or behavioral evidence;
- live-side-effect evidence, if explicitly authorized;
- documentation evidence.

Never compress unavailable evidence into "pass."

### 2. Establish build and test evidence

Use the repository's prescribed build command. For WinForge this normally is:

~~~powershell
dotnet build WinForge.sln -c Debug -p:Platform=x64
~~~

Run focused test projects before and after related edits. For the reactor
simulation, run:

~~~powershell
dotnet run --project tests/ReactorSim.Tests -c Debug
~~~

Record exact command, exit code, date/time, test count, and relevant log path
in the ledger. Do not substitute a broad test suite for a focused regression
test when one exists.

### 3. Exercise every route and capture visual evidence

Use the repository's run-winforge driver rather than a framework-dependent
Debug launch:

~~~powershell
powershell -ExecutionPolicy Bypass -File .agents\skills\run-winforge\driver.ps1 -Page <alias> -Out artifacts\smoke\<campaign-id>\screenshots\<route>.png -WaitMs 5000
~~~

Use the manifest aliases in batches, then inspect each produced image. Capture
additional focused screenshots for changed states, dialogs, error states, and
long surfaces; one launch image is not enough for a multi-state feature.

`shell.allapps` is a **shell-dialog route**, not a Frame page. Its direct
alias is `shell.allapps`; it must open the modal All Apps / Open new tab picker
(automation ID `NewTabPickerDialog`). Record dialog-specific launch and visual
evidence, rather than calling the Dashboard behind it a route pass.

After a self-contained publish, run its focused UI-automation regression:

~~~powershell
powershell -ExecutionPolicy Bypass -File .agents\skills\winforge-exhaustive-smoke\scripts\Test-WinForgeShellAllAppsRoute.ps1 -RepoRoot .
~~~

It checks the picker dialog, search box, and selected All Apps navigation item
without choosing a module or causing a feature side effect.

If screenshot capture fails, record capture-blocked with the exact command,
error, attempted fallback, and environment constraint. A route may be
launch-pass without visual evidence, but it must never be visual-pass.
Do not invent or retain stale screenshots as replacements.

For pages with actions, cover at least:

- default rendering and bilingual text;
- one representative valid input/action and expected output;
- validation or a meaningful error/empty state;
- cancellation/undo/revert where the feature supports it;
- the action's source/service boundary.

Prefer local, fixture, mock, or dry-run data. For an actual system mutation,
record the original state, exact action, verified result, and restoration.

### 4. Review source line by line

For each page/service batch:

1. Read the page's XAML, code-behind, direct services, and registrations.
2. Map every handler, command, and meaningful conditional branch to a test
   case, manual interaction, or clear exemption.
3. Verify that XAML control names/handlers resolve, language-change handlers
   are unsubscribed where required, and navigation/deep-link mappings are
   complete.
4. Use targeted searches for TODO, FIXME, empty handlers, swallowed
   exceptions, NotImplementedException, unbounded loops, unsafe shell
   interpolation, and missing cancellation paths. Investigate findings rather
   than counting them as defects automatically.
5. Treat typed XAML property literals as runtime evidence, not just build
   evidence. In the current self-contained runtime, a **reproduced** failing
   `NumberBox.Value`, `ToggleSwitch.IsOn`, or closely equivalent typed default
   must move to guarded managed initialization and receive a fresh deep-link
   launch check. Preserve bindings and passing literals: do not turn a
   page-local reproduction into a blanket NumberBox/CheckBox migration.
   Run the literal safety guard after a related change:

   ~~~powershell
   powershell -ExecutionPolicy Bypass -File .agents\skills\winforge-exhaustive-smoke\scripts\Test-WinForgeXamlLiteralSafety.ps1 -RepoRoot .
   ~~~

   It blocks direct XAML `IsOn="True|False"` values while allowing bindings,
   and verifies the 16 migrated ToggleSwitch defaults, the reproduced Markdown
   TOC `CheckBox.IsChecked` default, and ten page-local NumberBox defaults.
   The latter two checks are deliberately scoped to proven failures; default
   state belongs in code-behind after `InitializeComponent`.
6. Add findings to the ledger with severity, reproduction, source location,
   owner/status, and retest evidence.

### 5. Fix, retest, and preserve evidence

Fix confirmed defects in a small logical batch. Retest the exact reproduction,
the direct focused harness, and the surrounding route. Replace affected
canonical screenshots only after visually inspecting the new capture. Update
the ledger and docs in the same batch.

Do not bury a known failure. Use blocked, failed, not-applicable, or
unsafe-without-authorization precisely and explain the reason.

### 6. Documentation and Git handoff

Follow AGENTS.md's mandatory completion policy exactly. Before committing,
update all affected:

- README and feature references;
- docs/wiki pages and GitHub Pages content;
- the documentation embedded in GitHub Pages;
- detailed screenshot assets and their captions/links;
- test reports or coverage ledgers necessary to reproduce the result.

For each completed task/batch: commit only its scoped files, push the branch,
merge it into main, push main, verify origin/main contains the merge, then
delete only the fully integrated task branch and worktree. Never delete a
branch or worktree merely because a local merge command succeeded.

## Completion Gate

An exhaustive smoke campaign is complete only when:

1. Every manifest item has a terminal, evidence-backed status.
2. Every route has static-routing and launch evidence; every reachable visual
   surface has inspected current screenshot evidence or a documented capture
   blocker.
3. Every user-facing behavior has a test or safe exercise; side-effecting
   behavior has a documented safety disposition.
4. All confirmed defects are fixed and retested, or remain explicitly tracked
   with reproducible blockers.
5. Build and relevant focused tests are green.
6. Docs, screenshots, and the coverage report match the code.
7. The final commit is pushed and verified on origin/main before branch and
   worktree cleanup.

## Resources

- [scripts/New-WinForgeSmokeInventory.ps1](scripts/New-WinForgeSmokeInventory.ps1)
  extracts a route/page/control inventory into a repeatable manifest, using
  ASCII-safe routing-review diagnostics across PowerShell host encodings.
- [scripts/Invoke-WinForgeRouteSmoke.ps1](scripts/Invoke-WinForgeRouteSmoke.ps1)
  launches manifest routes in safe, isolated, no-capture batches when visual
  capture is blocked. It records a bounded, longer retry after a nonzero
  launch result so slow first renders are evidence rather than silent false
  failures.
- [scripts/Test-WinForgeShellAllAppsRoute.ps1](scripts/Test-WinForgeShellAllAppsRoute.ps1)
  validates the direct All Apps modal route with UI Automation.
- [scripts/Test-WinForgeXamlLiteralSafety.ps1](scripts/Test-WinForgeXamlLiteralSafety.ps1)
  blocks direct Boolean `IsOn` XAML literals and protects the explicitly
  reproduced Markdown TOC CheckBox and six-page NumberBox managed defaults;
  it does not prohibit unproven numeric literals.
- [references/coverage-schema.md](references/coverage-schema.md) defines the
  ledger states and required evidence.
