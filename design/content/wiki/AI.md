# AI · 人工智能

Tools for installing, running and chatting with AI coding agents and large language models, both local and cloud.
安裝、運行同對話 AI 編程代理同大型語言模型嘅工具，本機同雲端都支援。

## AI Agents · AI 代理

Install and launch terminal AI coding agents (Claude Code, Codex and more).
安裝同啟動終端機 AI 編程代理（Claude Code、Codex 等）。

Cake-gated YOLO mode can feed one valid chocolate cake and then write permissive config for supported agents, with a review report.
蛋糕閘門 YOLO 模式可以餵一個有效朱古力蛋糕，之後為支援嘅代理寫入寬鬆設定，並提供報告畀你覆核。

The YOLO transaction consumes and deletes one trusted `.cake` file through the shared Cake Factory credit store before touching any agent config. Existing configs are backed up and the report lands in `%LOCALAPPDATA%\WinForge\ai-agents`.
YOLO 交易會先透過共用蛋糕工廠額度儲存消耗並刪除一個可信 `.cake` 檔，之後先改代理設定。已有設定會先備份，報告會放喺 `%LOCALAPPDATA%\WinForge\ai-agents`。

Open in-app · 喺 App 內開啟: `WinForge.exe --page ai`

![AI Agents](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-ai.png)

## AI Chat · AI 聊天

OpenWebUI-style chat over local and cloud LLMs.
OpenWebUI 式聊天，連接本機同雲端大模型。

### Provider key protection · 供應商金鑰保護

**EN —** OpenAI-compatible provider API keys are stored in `%LOCALAPPDATA%\WinForge\ai-providers.json` under CurrentUser DPAPI. If that Windows user context cannot decrypt an existing key, the opaque encrypted value stays on disk during unrelated provider edits. If DPAPI cannot encrypt a changed non-empty key, WinForge aborts the complete provider-file write and shows a save error; it never replaces the prior encrypted value with an empty key. Entering a new non-empty key is the explicit recovery path.

**粵語 —** OpenAI-compatible 供應商嘅 API 金鑰會用 CurrentUser DPAPI 存喺 `%LOCALAPPDATA%\WinForge\ai-providers.json`。如果而家嘅 Windows 用戶環境解唔到原有金鑰，改其他供應商設定時磁碟上嗰個加密值會原封不動保留。DPAPI 加密改過而又唔係空白嘅金鑰失敗時，WinForge 會取消成份供應商檔案嘅寫入並顯示儲存錯誤；絕對唔會用空白金鑰蓋過舊有加密值。輸入新嘅非空白金鑰先係明確嘅復原方法。

Open in-app · 喺 App 內開啟: `WinForge.exe --page aichat`

![AI Chat](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-aichat.png)

## Ollama · 本地大模型

Pull, serve and chat with local GGUF models via Ollama.
用 Ollama 下載、提供同對話本機 GGUF 模型。

Open in-app · 喺 App 內開啟: `WinForge.exe --page ollama`

![Ollama](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-ollama.png)

[← Wiki Home](Home.md)
