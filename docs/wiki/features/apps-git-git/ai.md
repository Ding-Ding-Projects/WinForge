# AI Agents Â· AI ä»£ç†

**EN â€”** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**ç²µèªž â€”** å‘¢ä»½åŠŸèƒ½åƒè€ƒç”± WinForge æ¨¡çµ„ç™»è¨˜ã€å°Žè¦½åœ°åœ–åŒé é¢ XAML ç”Ÿæˆã€‚

| Field Â· æ¬„ä½ | Value Â· å€¼ |
|---|---|
| Tag Â· æ¨™ç±¤ | $(System.Collections.Specialized.OrderedDictionary["Tag"]) |
| Deep-link alias Â· æ·±å±¤é€£çµåˆ¥å | $(System.Collections.Specialized.OrderedDictionary["Alias"]) |
| Category Â· åˆ†é¡ž | Apps & Git Â· ç¨‹å¼èˆ‡ Git |
| Page class Â· é é¢é¡žåˆ¥ | $(System.Collections.Specialized.OrderedDictionary["Class"]) |
| Page XAML Â· é é¢ XAML | $(System.Collections.Specialized.OrderedDictionary["PageFile"]) |
| Button docs Â· æŒ‰éˆ•æ–‡ä»¶ | 3 |

## What It Covers Â· åŠŸèƒ½ç¯„åœ

**EN â€”** AI Agents is registered in WinForge search and navigation with these keywords: $(System.Collections.Specialized.OrderedDictionary["Keywords"]).

**ç²µèªž â€”** AI ä»£ç† å·²ç™»è¨˜å–º WinForge æœå°‹åŒå°Žè¦½ï¼Œé—œéµå­—åŒ…æ‹¬ï¼š$(System.Collections.Specialized.OrderedDictionary["Keywords"])ã€‚

## Buttons And Controls Â· æŒ‰éˆ•èˆ‡æŽ§åˆ¶é …

| Button Â· æŒ‰éˆ• | Type Â· é¡žåž‹ | XAML name Â· åç¨± | Handler Â· è™•ç†å‡½å¼ |
|---|---|---|---|
| [FeedCreditBtn](../../buttons/apps-git-git/ai/001-feedcreditbtn.md) | `Button` | `FeedCreditBtn` | `FeedCredit_Click` |
| [YoloBtn](../../buttons/apps-git-git/ai/003-yolobtn.md) | `Button` | `YoloBtn` | `Yolo_Click` |
| [WorkDirBtn](../../buttons/apps-git-git/ai/002-workdirbtn.md) | `Button` | `WorkDirBtn` | `WorkDir_Click` |

## Cake-gated YOLO mode · 蛋糕閘門 YOLO 模式

**EN —** The YOLO control is deliberately gated by one valid chocolate cake. After the cake is consumed, WinForge applies permissive configuration for supported coding agents and saves a report that lists the files touched.

**粵語 —** YOLO 控制項特意用一個有效朱古力蛋糕做閘門。蛋糕被消耗後，WinForge 會為支援嘅編程代理套用寬鬆設定，並儲存一份列出已修改檔案嘅報告。

**EN —** The transaction uses the same signed `.cake` files minted by Cake Factory. One trusted, edible cake is converted into 1,000,000 AI generated units and deleted from disk before config writes begin. If no edible file is available, the helper stops without modifying agent config.

**粵語 —** 呢個交易使用蛋糕工廠簽發嘅同一批 `.cake` 檔。一個可信、可食用蛋糕會先轉成 1,000,000 個 AI 生成單位並從磁碟刪除，之後先開始寫設定。如果無可食用檔案，輔助工具會停止，唔會改代理設定。

| Supported agent · 支援代理 | Config effect · 設定效果 |
|---|---|
| Codex | Writes permissive TOML settings for no approvals and full filesystem access. |
| Claude Code | Writes permissive JSON permissions and `dangerouslySkipPermissions`. |
| OpenCode | Allows edit, bash and webfetch permissions in the OpenCode config. |
| Pi / OpenClaw / Hermes | Writes best-effort JSON permission flags where a config file is known. |

**EN —** Every existing touched file gets a `.winforge-bak-*` backup, and the generated report is saved under `%LOCALAPPDATA%\WinForge\ai-agents`.

**粵語 —** 每個被修改而又已存在嘅檔案都會先有 `.winforge-bak-*` 備份；產生出嚟嘅報告會儲存喺 `%LOCALAPPDATA%\WinForge\ai-agents`。
