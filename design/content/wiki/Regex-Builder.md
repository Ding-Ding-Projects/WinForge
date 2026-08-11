# Managed Regex Builder · 正式受管理正則砌法

WinForge's canonical .NET 11 Regex Tester now combines a raw pattern editor with guided construction for literals, character classes, anchors, groups, alternation, and quantifiers. It identifies the exact `System.Text.RegularExpressions` dialect and escaping rules, supports five flags, evaluates bounded local sample text live, lists matches and capture groups, previews replacement output, and copies only on an explicit action. · WinForge 正式 .NET 11 Regex Tester 而家將原始 pattern 編輯器同字面文字、字元類、錨點、群組、二選一、量詞引導砌法放埋一齊；清楚標明 `System.Text.RegularExpressions` 方言／跳脫規則，支援五個旗標，只喺本機即時運算有界 sample，列出配對／擷取群組、預覽替換，而且只會喺明確操作後複製。

Open it with `WinForge.exe --page regextester`. · 用 `WinForge.exe --page regextester` 開啟。

![Managed Regex Tester · 正式受管理正則測試器](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-regextester.png)

The same full builder is now directly synchronized with the real Dashboard, Category, Search Results, Manual, App Launcher, Licenses, Native OSS Hub, Settings Hub, Offline Documentation, Support Tickets, and Authenticator result sets. The code-built Settings and About searches use the same session; About passes the complete `Spec` into the bounded changelog matcher, and plain text remains the default. · 同一套完整版砌法而家已直接同步 Dashboard、Category、Search Results、Manual、App Launcher、Licenses、Native OSS Hub、Settings Hub、Offline Documentation、Support Tickets 同 Authenticator 嘅真實結果。code-built Settings 同 About 搜尋都用同一個 session；About 會將完整 `Spec` 交畀有限 changelog matcher，預設仍然係純文字。

![About changelog search and adjacent regex builder action · About 變更紀錄搜尋同隔籬正則 builder 動作](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-about-changelog-regex-2026-08-11.png)

![Visible keyboard focus on the guided builder · 引導砌法嘅可見鍵盤 focus](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-regextester-builder.png)

![Shared search control on Dashboard · Dashboard 共用搜尋控制](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-regex-search-core.png)

![Narrow bilingual synchronized search row · 窄畫面雙語同步搜尋列](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-regex-search-core-narrow.png)

## Safety contract · 安全合約

- 4,096-character pattern, 1,000,000-character sample, 65,536-character replacement, and 2,000 displayed-match caps. · Pattern 4,096、sample 1,000,000、replacement 65,536 字元，同 2,000 個顯示配對上限。
- One-second .NET match/replace timeout plus a conservative replacement-work guard. · 一秒 .NET 配對／替換超時，另加保守替換工作量閘。
- Correct zero-width progression, invalid-pattern feedback, and local-only processing with no persistence or transmission. · 正確零寬度推進、錯誤 pattern 提示，同只喺本機處理；唔保存亦唔傳送。
- The shared plain-text/regex matching contract keeps plain text as the default. The exhaustive inventory classifies 102 controls across 83 source files: 13 integrated builder-backed searches, 68 ordinary searches retained, 9 specialized dialect adapters, 7 dedicated pattern tools, 2 read-only outputs, and 3 shared-control internals. The global roadmap remains open and does not mislabel the remaining work. · 共用純文字／regex 合約保持純文字預設；完整清單分類晒 83 個來源檔案入面 102 個控制：13 個已接 builder、68 個一般搜尋保留、9 個專用方言 adapter、7 個專用 pattern 工具、兩個唯讀輸出，同三個共用控制內部欄位。全域路線圖保持未剔，唔會誤報餘下工作。

## Verification · 驗證

The focused harness passes **35/35** across evaluator, session, surface, changelog-spec, builder-UI, and inventory contracts. A fresh 1574×887 About/changelog capture was inspected from the self-contained Debug build; the search field, adjacent builder action, date controls, and export action are visible without clipping. The exact capture hash is recorded above, while the remaining ordinary searches and dropdown/menu/picker migration stay open. · 專項 harness **35/35**，涵蓋 evaluator、session、surface、changelog-spec、builder UI 同 inventory 合約。最新 1574×887 About／changelog 真實 build 圖已檢視；搜尋欄、隔籬 builder 動作、日期控制同匯出動作都冇裁切。準確 capture hash 已記喺上面，其餘一般搜尋同 dropdown／menu／picker 整合仍然開住。
