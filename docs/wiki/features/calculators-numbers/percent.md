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

**EN —** `percent`, `percentage`, and `module.percentcalc` now open a dedicated native Percentage Calculator backed by the standard-C++ `PercentCalc` core. It preserves all six managed cards: percent-of, what-percent, percentage change, increase/decrease, tip splitting, and ratio simplification. Inputs accept the active decimal separator with an invariant-dot fallback; results use six-place away-from-zero rounding; tip shares preserve banker's rounding; invalid or zero-denominator inputs are guarded locally; and Copy is the only clipboard mutation.

**粵語 —** `percent`、`percentage` 同 `module.percentcalc` 而家會開專用原生百分比計算器頁，由標準 C++ `PercentCalc` core 支援。佢保留 managed 六張卡：百分比數值、反求百分比、百分比變化、加／減百分比、小費分帳同最簡比例。輸入接受目前小數分隔符同 invariant `.` fallback；結果用六位、小數向遠離零捨入；小費分帳保留 banker's rounding；無效或零分母輸入只會本機保護；唯一會改剪貼簿嘅操作係明確 Copy。

**Current local evidence · 目前本機證據：** renderer accounting is **31/346 fixed routes**, leaving **315 fixed routes** plus five dynamic route families. Native Debug and Release x64 solution builds each exit 0 with 0 errors; Debug and Release core each pass **796/796**, including Percentage Calculator **37/37**; catalog parity covers all 346 fixed routes plus five dynamic families; and focused `-PercentCalcRoutesOnly` UI Automation passes **13/13** across `percent`, `percentage`, and `module.percentcalc`, including language rerender, route re-entry reset, narrow bounds, guarded errors, and explicit-only clipboard policy. LowLevel Computer Use MCP is not callable in this Codex session. The repository-native driver found `CopyFromScreen` unavailable and rejected its blank/near-uniform `PrintWindow` fallback; no PNG was created or retained and no canonical screenshot changed, so visual evidence remains `capture-blocked`. · Renderer 計 **31/346**，仲有 **315** 條固定 route 同五組動態家族。Debug／Release build 各 0 errors、兩個 core 各 **796/796**（包括 Percentage Calculator **37/37**）、catalog parity 同 `percent`、`percentage`、`module.percentcalc` 專項 UIA **13/13** 已通過，涵蓋語言重繪、重新進入 route reset、窄闊度、受保護錯誤同只限明確 Copy 嘅剪貼簿政策。今次 session 冇 LowLevel MCP；repository-native driver 發現 `CopyFromScreen` 唔可用，並拒絕空白／近乎單色 `PrintWindow` fallback；冇建立或保留 PNG、冇改 canonical 截圖，所以 visual 保持 `capture-blocked`。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [C1Copy](../../buttons/calculators-numbers/percent/001-c1copy.md) | `Button` | `C1Copy` | `Copy1` |
| [C2Copy](../../buttons/calculators-numbers/percent/002-c2copy.md) | `Button` | `C2Copy` | `Copy2` |
| [C3Copy](../../buttons/calculators-numbers/percent/003-c3copy.md) | `Button` | `C3Copy` | `Copy3` |
| [C4Copy](../../buttons/calculators-numbers/percent/004-c4copy.md) | `Button` | `C4Copy` | `Copy4` |
| [C5Copy](../../buttons/calculators-numbers/percent/005-c5copy.md) | `Button` | `C5Copy` | `Copy5` |
| [C6Copy](../../buttons/calculators-numbers/percent/006-c6copy.md) | `Button` | `C6Copy` | `Copy6` |