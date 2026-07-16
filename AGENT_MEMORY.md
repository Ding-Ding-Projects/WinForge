# Persistent Agent Task Memory / 長期代理任務記憶

This file is the durable execution contract for every agent working in this repository. It supplements, and never weakens, `AGENTS.md`.

## Completion and Git / 完成與 Git

- Treat every bounded task as incomplete until its intentional bilingual commit has been pushed.
- Complete each task on a temporary `codex/` branch, merge it into `main`, and push `main` before calling it finished.
- After pushing `main`, fetch the remote and prove both the task commit and branch tip are ancestors of `origin/main`; also verify the expected changed files exist in the remote `main` tree.
- Only after that proof may an agent delete the merged remote/local branch or its worktree. Never delete unmerged, unpushed, or unverified work.
- Never force-push and never discard unrelated user changes.

## Documentation and evidence / 文件與證據

- A feature or page change includes its documentation work: update the relevant `README.md`, `docs/wiki/`, `docs/`, and GitHub Pages content under `design/content/wiki/`, keeping English and Cantonese aligned with the shipped UI.
- For visual changes, capture and inspect current, high-detail evidence for every changed page; replace stale canonical screenshots and matching wiki/Pages assets. If capture is blocked, record the exact blocker and do not claim visual verification.
- Generated documentation remains generated: use its repository generator instead of hand-editing generated feature/reference artifacts.

## LowLevel headless operation / LowLevel 無頭運作

- Use the inexpensive LowLevel Computer Use MCP runner for task commands and all app interaction.
- Launch and drive WinForge only in a dedicated headless desktop. Do not open a visible instance that can steal the user's focus.
- Capture UI evidence through LowLevel MCP from that headless desktop, inspect it, and close test processes/desktops when their evidence is complete.

## Security and hygiene / 安全與整潔

- Never persist, log, copy, or screenshot secrets unnecessarily.
- Keep the task focused; preserve unrelated work already present in the worktree.
- Record verification results honestly, including failures and recovery steps.
