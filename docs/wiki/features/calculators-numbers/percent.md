# Percentage Calculator · 百分比計算器

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.percentcalc</code> |
| Deep-link alias · 深層連結別名 | <code>percent</code> |
| Category · 分類 | Calculators & Numbers · 計算與數字 |
| Page class · 頁面類別 | <code>PercentCalcModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/PercentCalcModule.xaml</code> |
| Button docs · 按鈕文件 | 6 |

## What It Covers · 功能範圍

**EN —** Percentage Calculator is registered in WinForge search and navigation with these keywords: <code>percent percentage ratio tip change increase decrease calculator split gcd simplify 百分比 比例 貼士 分帳 變化率 加減 化簡 計算器</code>.

**粵語 —** 百分比計算器 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>percent percentage ratio tip change increase decrease calculator split gcd simplify 百分比 比例 貼士 分帳 變化率 加減 化簡 計算器</code>。

## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `percent`, `percentage`, and `module.percentcalc` open a genuine native Percentage Calculator backed by the dependency-free standard-C++ `PercentCalc` core. The six local cards preserve managed percent-of, what-percent, signed percentage change, increase/decrease, tip splitting, and ratio simplification behavior. Inputs accept the current decimal separator with an invariant-dot fallback and managed-equivalent Unicode trimming; finite displays round to six places away from zero, tip people counts use banker’s rounding, each fresh route resets its state, and only an explicit Copy can write the clipboard.

**粵語 —** `percent`、`percentage` 同 `module.percentcalc` 會開真正原生百分比計算器，由唔靠依賴嘅標準 C++ `PercentCalc` core 支援。六張本機卡保留 managed 百分比乘數、反求百分比、有正負號嘅百分比變化、加／減、貼士分帳同化簡比例行為。輸入接受目前小數點同 invariant-dot fallback，以及同 managed 一致嘅 Unicode 修剪；有限結果會向遠離零方向取六位小數，人數用 banker’s rounding，新 route 會重設狀態，而且只會喺明確 Copy 後先寫剪貼簿。

**Controlled integration evidence · 受控整合證據：** renderer accounting is **38/346 fixed routes** (`38 in-progress / 308 not-started`, plus five dynamic families). Fresh Debug and Release x64 native solution builds exit 0 with 0 errors; both core suites pass **915/915**, including Percentage Calculator **37/37**; focused `-PercentCalcRoutesOnly -AllowClipboardMutation` UI Automation passes **14/14** across all aliases, language states, guarded errors, reset, bounds, and explicit clipboard Copy; catalog parity covers 346 fixed routes plus five dynamic families, 319 registry records, and 346 ledger rows; and the native installer contract passes. LowLevel Computer Use MCP is not callable in this session. The required driver used a valid `PrintWindow` fallback after `CopyFromScreen` was unavailable; the fresh 1962×1311 frame was visually inspected and promoted as the current canonical screenshot, so visual evidence is `pass`. The sole C++ publisher is test-gated and retry-hardened; earlier hosted API-outage repairs remain pending after the controlled push. · Renderer 而家係 **38/346** 條固定 route（`38 in-progress / 308 not-started`，另加五組動態家族）。最新 Debug／Release x64 原生 solution build 各 0 errors；兩個 core 各 **915/915**，包括 Percentage Calculator **37/37**；`-PercentCalcRoutesOnly -AllowClipboardMutation` 專項 UI Automation 喺全部 alias、語言狀態、守衛錯誤、重設、邊界同明確 Copy 通過 **14/14**；catalog parity 覆蓋 346 固定 route 加五組動態家族、319 registry 同 346 ledger；native installer contract 亦通過。今個 session 冇可呼叫 LowLevel Computer Use MCP；`CopyFromScreen` 唔可用後，必需 driver 用有效嘅 `PrintWindow` fallback 取得最新 1962×1311 圖，已檢視並升格做目前 canonical 截圖，所以 visual 係 `pass`。唯一 C++ publisher 已有測試 gate 同 retry 加固；較早 hosted API outage repair 要等 controlled push 後完成。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [C1Copy](../../buttons/calculators-numbers/percent/001-c1copy.md) | `Button` | `C1Copy` | `Copy1` |
| [C2Copy](../../buttons/calculators-numbers/percent/002-c2copy.md) | `Button` | `C2Copy` | `Copy2` |
| [C3Copy](../../buttons/calculators-numbers/percent/003-c3copy.md) | `Button` | `C3Copy` | `Copy3` |
| [C4Copy](../../buttons/calculators-numbers/percent/004-c4copy.md) | `Button` | `C4Copy` | `Copy4` |
| [C5Copy](../../buttons/calculators-numbers/percent/005-c5copy.md) | `Button` | `C5Copy` | `Copy5` |
| [C6Copy](../../buttons/calculators-numbers/percent/006-c6copy.md) | `Button` | `C6Copy` | `Copy6` |