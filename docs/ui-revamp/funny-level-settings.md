# Independent Funny-Level Settings · 英粵分開搞笑等級

## Behavior · 行為

Settings exposes two independent exact-step sliders from 1 through 5: one for English and one for playful Hong Kong-style Cantonese. Level 1 is fully serious; level 5 is the most playful. Each value persists immediately, survives restart, reloads after a settings import, and updates the safe live preview and Dashboard hero without rebuilding the page. Bilingual mode resolves each language with its own selected level. · 設定頁提供兩個互相獨立、由 1 至 5 準確步進嘅 slider：英文一個、香港地道玩味粵語一個。第 1 級完全正經，第 5 級最玩得。每個值都會即時保存、重開 app 後保留、匯入設定後重新載入，並即時更新安全預覽同 Dashboard 首頁句子；雙語模式會按兩種語言各自級別顯示。

Only copy explicitly authored as `PlayfulText` can vary. The first reviewed catalog entry is `PlayfulCopy.DashboardHero`, with five complete variants per language. Ordinary `LocalizedText` remains unchanged. · 只有明確寫成 `PlayfulText` 嘅文案先可以改語氣；第一個經覆核項目係 `PlayfulCopy.DashboardHero`，每種語言各有五個完整版本。普通 `LocalizedText` 完全唔變。

## Configuration and persistence · 設定同持久化

| Setting key · 設定 key | Range · 範圍 | Default · 預設 |
|---|---:|---:|
| `tone.englishFunnyLevel` | 1–5 | 2 |
| `tone.cantoneseFunnyLevel` | 1–5 | 3 |

Values are invariant-culture integers stored through the existing atomic `%LOCALAPPDATA%\WinForge\settings.json` settings service. Invalid or missing values fall back independently; assignments outside 1–5 are rejected before persistence. Import/export carries both keys with the rest of the settings. · 數值係 invariant-culture 整數，經現有原子設定服務保存喺 `%LOCALAPPDATA%\WinForge\settings.json`。缺失／無效值會各自用預設；超出 1–5 嘅寫入會喺保存前拒絕。匯入／匯出會連同其他設定一齊處理兩個 key。

## Accessibility and layout · 無障礙同版面

Each slider has a programmatic name, help text explaining Left/Right Arrow operation, a 44-pixel minimum target, exact integer snapping, a visible current-value sentence, and a polite live preview region. The card wraps at narrow widths; at 720 pixels the shell collapses navigation and keeps both sliders, labels, and value summaries readable without overlap. Theme-aware surfaces and secondary text use explicit light/dark contrast. · 每個 slider 都有程式化名稱、解釋左右方向鍵嘅 help text、最少 44 像素 target、整數吸附、可見目前值句子，同 polite live preview。卡片會喺窄畫面換行；720 像素時 shell 會收窄導覽，而兩個 slider、標籤同數值摘要仍然清楚、冇重疊。Surface 同次要文字按 light／dark theme 用明確對比色。

## Failure and safety boundaries · 失敗同安全界線

- Malformed persisted values fail to the per-language defaults; one bad language never changes the other. · 壞咗嘅持久值會各自回退預設；一邊有問題唔會改另一邊。
- No generated rewriting, translation, remote service, telemetry, or pattern/sample persistence is involved. · 冇自動改寫、翻譯、遠端服務、telemetry 或額外樣本持久化。
- Errors, warnings, destructive confirmations, financial/security copy, accessibility wording, and operational instructions must remain ordinary exact localization and must never use `PlayfulText`. · 錯誤、警告、破壞性確認、金融／安全文案、無障礙文字同操作指示必須保持普通準確本地化，永遠唔可以用 `PlayfulText`。
- A no-op assignment writes nothing and raises no duplicate change event. · 同值設定唔會重寫檔案，亦唔會重複發 change event。

## Verification · 驗證

The focused `tests/FunnyLevelSettings.Tests` harness passes **6/6**, covering defaults and malformed data, independent persistence, all three language modes, import reload, out-of-range rejection, and unchanged ordinary safety-sensitive localization. The combined solution build has **0 warnings / 0 errors**; self-contained publish/site generation, XAML literal safety, and the detailed source audit pass with zero lifecycle/actionable findings. The UI was inspected on a dedicated LowLevel headless desktop at 1049×646 and 720×646. UI Automation changed the real sliders to English 5 / Cantonese 1, the visible value labels and preview changed independently, and the original 2 / 3 persisted values were restored before the exact owned process and desktop were closed. A final repository-driver launch-only check also ran inside a fresh LowLevel desktop and left zero windows/processes. Canonical captures live in `docs/screenshot-funny-level-settings*.png`. · 專項 `FunnyLevelSettings.Tests` **6/6**，涵蓋預設／壞資料、獨立保存、三種語言模式、匯入重載、越界拒絕，同普通安全文案完全不變。Combined solution build 零 warning／零 error；self-contained publish／site generation、XAML safety 同詳細 source audit 全過，零 lifecycle／actionable finding。App 已喺專用 LowLevel headless desktop 以 1049×646 同 720×646 檢視；UI Automation 將真 slider 改做英文 5／粵語 1，畫面數值同預覽各自更新，之後再還原原本 2／3 持久值，最後關閉準確自家 process 同 desktop。最後 repository-driver launch-only 亦喺全新 LowLevel desktop 通過並零殘留。正式圖放喺 `docs/screenshot-funny-level-settings*.png`。
