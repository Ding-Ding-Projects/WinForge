# Managed Regex Builder · 正式受管理正則砌法

WinForge's canonical .NET 11 Regex Tester now combines a raw pattern editor with guided construction for literals, character classes, anchors, groups, alternation, and quantifiers. It identifies the exact `System.Text.RegularExpressions` dialect and escaping rules, supports five flags, evaluates bounded local sample text live, lists matches and capture groups, previews replacement output, and copies only on an explicit action. · WinForge 正式 .NET 11 Regex Tester 而家將原始 pattern 編輯器同字面文字、字元類、錨點、群組、二選一、量詞引導砌法放埋一齊；清楚標明 `System.Text.RegularExpressions` 方言／跳脫規則，支援五個旗標，只喺本機即時運算有界 sample，列出配對／擷取群組、預覽替換，而且只會喺明確操作後複製。

Open it with `WinForge.exe --page regextester`. · 用 `WinForge.exe --page regextester` 開啟。

![Managed Regex Tester · 正式受管理正則測試器](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-regextester.png)

The same full builder is now directly synchronized with the real Dashboard, Category, Search Results, Manual, App Launcher, Licenses, Native OSS Hub, and Settings Hub result sets. The compact query, raw pattern, explicit regex mode, and five flags share one session; plain text remains the default. · 同一套完整版砌法而家已直接同步 Dashboard、Category、Search Results、Manual、App Launcher、Licenses、Native OSS Hub 同 Settings Hub 嘅真實結果。精簡 query、原樣 pattern、明確 regex mode 同五旗標共用同一個 session；預設仍然係純文字。

![Visible keyboard focus on the guided builder · 引導砌法嘅可見鍵盤 focus](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-regextester-builder.png)

![Shared search control on Dashboard · Dashboard 共用搜尋控制](images/screenshot-regex-search-core.png)

![Narrow bilingual synchronized search row · 窄畫面雙語同步搜尋列](images/screenshot-regex-search-core-narrow.png)

## Safety contract · 安全合約

- 4,096-character pattern, 1,000,000-character sample, 65,536-character replacement, and 2,000 displayed-match caps. · Pattern 4,096、sample 1,000,000、replacement 65,536 字元，同 2,000 個顯示配對上限。
- One-second .NET match/replace timeout plus a conservative replacement-work guard. · 一秒 .NET 配對／替換超時，另加保守替換工作量閘。
- Correct zero-width progression, invalid-pattern feedback, and local-only processing with no persistence or transmission. · 正確零寬度推進、錯誤 pattern 提示，同只喺本機處理；唔保存亦唔傳送。
- The shared plain-text/regex matching contract keeps plain text as the default. The exhaustive inventory classifies 93 controls across 74 XAML files: 8 integrated, 64 ordinary searches retained, 9 specialized dialect adapters, 7 dedicated pattern tools, 2 read-only outputs, and 3 shared-control internals. The global roadmap remains open and does not mislabel the remaining work. · 共用純文字／regex 合約保持純文字預設；完整清單分類晒 74 個 XAML 檔案入面 93 個控制：八個已整合、64 個一般搜尋保留、九個專用方言 adapter、七個專用 pattern 工具、兩個唯讀輸出，同三個共用控制內部欄位。全域路線圖保持未剔，唔會誤報餘下工作。

## Verification · 驗證

The focused harness passes **33/33** across evaluator, session, surface, builder-UI, and inventory contracts. Fresh 852×646 normal LowLevel and 760×720 app-owned narrow bilingual captures were inspected; the compact query, explicit regex state, and builder action fit without clipping. A preceding live flyout audit directly found the two-column flag and long-placeholder defects; both were fixed to one-column flags plus a short prompt, while the complete builder contract remains source/test locked. The exact owned process/desktop were closed, and hashes are recorded in `handoff-summary.md`. · 專項 harness **33/33**，涵蓋 evaluator、session、surface、builder UI 同 inventory 合約。已檢視新鮮 852×646 正常 LowLevel 同 760×720 app-owned 窄畫面雙語圖；精簡 query、明確 regex 狀態同 builder action 冇裁切。較早 live flyout 審核直接搵到雙欄旗標同過長 placeholder 缺陷；兩樣已修成單欄旗標加短提示，而完整 builder contract 由原始碼／測試鎖實。準確自家 process／desktop 已關閉，hash 記喺 `handoff-summary.md`。

[Generated module reference · 生成模組參考](features/dev-helpers/regextester.md) · [Detailed feature contract · 詳細功能合約](../features/developer-tooling/regex-builder.md)
