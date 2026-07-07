# AI Agents · AI 代理

![AI Agents](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-aiagents.png)

**EN —** Install, configure and launch terminal AI coding agents — one click each. Most install via npm (Node.js); some via an official installer. Pick a launch folder, and if Node.js is missing a one-click winget install sets it up so every npm-based agent becomes available.

**粵語 —** 一鍵安裝、設定同啟動終端機 AI 編程代理。大部分用 npm（Node.js）安裝，部分用官方安裝器。揀一個啟動目錄，如果冇 Node.js，一鍵 winget 安裝幫你裝好，咁所有用 npm 嘅代理都用得。

## Cake-gated YOLO mode · 蛋糕閘門 YOLO 模式

**EN —** The **Feed chocolate cake + enable** button consumes one valid chocolate `.cake` file before it writes permissive agent configuration for supported tools such as Codex, Claude Code, OpenCode and Pi. The action writes a report path so the operator can review exactly which config files were touched.

**粵語 —** **餵朱古力蛋糕 + 啟用** 按鈕會先消耗一個有效朱古力 `.cake` 檔，先至幫 Codex、Claude Code、OpenCode、Pi 等支援工具寫入較寬鬆嘅代理設定。動作會產生報告路徑，方便操作員睇清楚改過邊啲設定檔。

## YOLO transaction · YOLO 交易流程

**EN —** This is a real cake-credit transaction, not a visual toggle. WinForge asks `CakeCreditService` to feed exactly one trusted, edible cake from the shared Cake Factory store. A valid cake adds 1,000,000 generated units, the physical `.cake` file is eaten/deleted by `CakeFileService`, and only then does the helper write agent settings.

**粵語 —** 呢個係真嘅蛋糕額度交易，唔係普通開關。WinForge 會叫 `CakeCreditService` 由共用蛋糕工廠儲存餵入正好一個可信、可食用蛋糕。有效蛋糕會加入 1,000,000 個生成單位，實體 `.cake` 檔會由 `CakeFileService` 食用／刪除，之後先寫入代理設定。

| Agent · 代理 | YOLO write · 寫入內容 |
|---|---|
| Codex | Updates `~/.codex/config.toml` with `approval_policy = "never"`, `sandbox_mode = "danger-full-access"` and visible reasoning. |
| Claude Code | Updates `.claude/settings.json` permissions, allows core file/shell/web actions and enables `dangerouslySkipPermissions`. |
| OpenCode | Updates the OpenCode JSON config with permissive edit/bash/webfetch permissions and `dangerouslySkipPermissions`. |
| Pi / OpenClaw / Hermes | Writes best-effort JSON permission flags for supported config files. |

**EN —** Existing config files are backed up with a `.winforge-bak-YYYYMMDDHHMMSS` suffix before they are touched. The review report is written under `%LOCALAPPDATA%\WinForge\ai-agents\yolo-mode-*.txt`.

**粵語 —** 已存在嘅設定檔會先用 `.winforge-bak-YYYYMMDDHHMMSS` 後綴備份，再作修改。覆核報告會寫到 `%LOCALAPPDATA%\WinForge\ai-agents\yolo-mode-*.txt`。

---
[← Module index · 模組索引](Home) · [README](https://github.com/codingmachineedge/WinForge/blob/main/README.md) · [Screenshots](Screenshots)
