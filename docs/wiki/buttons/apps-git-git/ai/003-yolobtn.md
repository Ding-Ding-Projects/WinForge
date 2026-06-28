# YoloBtn · Cake-gated YOLO mode · Button

**EN —** Enables the AI Agents YOLO configuration helper after consuming one valid chocolate `.cake` file.

**粵語 —** 消耗一個有效朱古力 `.cake` 檔之後，啟用 AI 代理 YOLO 設定輔助。

| Field · 欄位 | Value · 值 |
|---|---|
| Module · 模組 | [AI Agents · AI 代理](../../../features/apps-git-git/ai.md) |
| Category · 分類 | Apps & Git · 程式與 Git |
| Control type · 控制類型 | `Button` |
| XAML name · XAML 名稱 | `YoloBtn` |
| Label / tooltip · 標籤／提示 | Feed chocolate cake + enable · 餵朱古力蛋糕 + 啟用 |
| Handler · 處理函式 | `Yolo_Click` |

## Operator Notes · 操作備註

**EN —** Use this only when a valid chocolate cake file is available. The button consumes that cake, writes permissive agent settings for supported tools, and shows the generated report path for review.

**粵語 —** 只喺有有效朱古力蛋糕檔時使用。按鈕會消耗該蛋糕、為支援工具寫入寬鬆代理設定，並顯示產生出嚟嘅報告路徑畀你覆核。

**EN —** The click path is transactional: one trusted `.cake` file is eaten/deleted first, then Codex, Claude Code, OpenCode and other known JSON-config agents are updated. Existing config files are backed up with `.winforge-bak-*`, and the report is written under `%LOCALAPPDATA%\WinForge\ai-agents`.

**粵語 —** 呢個 click 流程係交易式：先食用／刪除一個可信 `.cake` 檔，之後先更新 Codex、Claude Code、OpenCode 同其他已知 JSON 設定代理。已有設定檔會用 `.winforge-bak-*` 備份，報告會寫到 `%LOCALAPPDATA%\WinForge\ai-agents`。
