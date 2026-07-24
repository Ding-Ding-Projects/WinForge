# Managed Regex Builder · 正式受管理正則砌法

WinForge's canonical .NET 11 Regex Tester now combines a raw pattern editor with guided construction for literals, character classes, anchors, groups, alternation, and quantifiers. It identifies the exact `System.Text.RegularExpressions` dialect and escaping rules, supports five flags, evaluates bounded local sample text live, lists matches and capture groups, previews replacement output, and copies only on an explicit action. · WinForge 正式 .NET 11 Regex Tester 而家將原始 pattern 編輯器同字面文字、字元類、錨點、群組、二選一、量詞引導砌法放埋一齊；清楚標明 `System.Text.RegularExpressions` 方言／跳脫規則，支援五個旗標，只喺本機即時運算有界 sample，列出配對／擷取群組、預覽替換，而且只會喺明確操作後複製。

Open it with `WinForge.exe --page regextester`. · 用 `WinForge.exe --page regextester` 開啟。

![Managed Regex Tester · 正式受管理正則測試器](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-regextester.png)

![Visible keyboard focus on the guided builder · 引導砌法嘅可見鍵盤 focus](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-regextester-builder.png)

## Safety contract · 安全合約

- 4,096-character pattern, 1,000,000-character sample, 65,536-character replacement, and 2,000 displayed-match caps. · Pattern 4,096、sample 1,000,000、replacement 65,536 字元，同 2,000 個顯示配對上限。
- One-second .NET match/replace timeout plus a conservative replacement-work guard. · 一秒 .NET 配對／替換超時，另加保守替換工作量閘。
- Correct zero-width progression, invalid-pattern feedback, and local-only processing with no persistence or transmission. · 正確零寬度推進、錯誤 pattern 提示，同只喺本機處理；唔保存亦唔傳送。
- The shared plain-text/regex matching contract keeps plain text as the default. Direct full-builder access and bidirectional pattern/flag synchronization across every search bar remains explicitly open work. · 共用純文字／regex 合約保持純文字預設；每個搜尋欄直接用完整版 builder 同雙向同步 pattern／旗標仍然明確係待完成工作。

## Verification · 驗證

The focused harness passes **13/13**, the x64 solution build passes with zero warnings/errors, and fresh app-owned plus LowLevel headless 1049×646 evidence was inspected. The option layout is vertical, long bilingual content scrolls, and keyboard focus is visible. · 專項 harness **13/13**、x64 solution build 零 warning／error；亦已檢視新鮮 app-owned 同 LowLevel headless 1049×646 證據。選項直排、長雙語內容可滾動，鍵盤 focus 清楚可見。

[Generated module reference · 生成模組參考](features/dev-helpers/regextester.md) · [Detailed feature contract · 詳細功能合約](../features/developer-tooling/regex-builder.md)
