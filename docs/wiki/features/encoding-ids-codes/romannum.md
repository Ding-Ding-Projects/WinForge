# Roman Numerals · 羅馬數字

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.romannum</code> |
| Deep-link alias · 深層連結別名 | <code>romannum</code> |
| Category · 分類 | Encoding, IDs & Codes · 編碼識別碼與條碼 |
| Page class · 頁面類別 | <code>RomanNumModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/RomanNumModule.xaml</code> |
| Button docs · 按鈕文件 | 2 |

## What It Covers · 功能範圍

**EN —** Roman Numerals is registered in WinForge search and navigation with these keywords: <code>roman numerals number convert MCMXCIV validate 羅馬 數字 轉換 大寫 驗證</code>.

**粵語 —** 羅馬數字 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>roman numerals number convert MCMXCIV validate 羅馬 數字 轉換 大寫 驗證</code>。

## Native Runtime Contract · 原生執行期合約

**EN —** The native `module.romannum` page is reachable through `roman`, `romannum`, and the canonical route. Its pure standard-C++ core converts 1–3,999 in canonical subtractive notation; Extended mode converts 1–3,999,999 with U+0305 combining-overline vinculum notation (`4,000 → I̅V̅`) and accepts canonical `(IV) → 4,000` input. It deliberately preserves the managed exact canonical round trip: direct lowercase, <code>IIII</code>, <code>IC</code>, <code>(I)</code>, and <code>(I)V</code> reject. Live inputs, outputs, and the Extended setting survive language rerenders; each input/output/copy control has a stable UI Automation ID and localized name, and the clipboard is written only after an explicit Copy.

**粵語 —** 原生 <code>module.romannum</code> 頁可以用 <code>roman</code>、<code>romannum</code> 同本體 route 開啟。純標準 C++ core 用標準相減寫法轉換 1–3,999；「擴充」模式會用 U+0305 組合上劃線 vinculum 寫法轉換 1–3,999,999（<code>4,000 → I̅V̅</code>），亦接受標準 <code>(IV) → 4,000</code> 輸入。佢有意保留受控版精確 canonical round trip：直接小楷、<code>IIII</code>、<code>IC</code>、<code>(I)</code> 同 <code>(I)V</code> 都會拒絕。即時輸入、輸出同「擴充」設定會喺轉語言時保留；每個輸入／輸出／複製 control 都有穩定 UI Automation ID 同本地化名稱，而剪貼簿只會喺明確 Copy 後先寫入。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [CopyRomanBtn](../../buttons/encoding-ids-codes/romannum/001-copyromanbtn.md) | `Button` | `CopyRomanBtn` | `CopyRoman_Click` |
| [CopyNumberBtn](../../buttons/encoding-ids-codes/romannum/002-copynumberbtn.md) | `Button` | `CopyNumberBtn` | `CopyNumber_Click` |

## Native Visual Evidence · 原生視覺證據

**EN —** The current native evidence is 17 focused core tests inside the 313/313 Debug-and-Release suite and 13 live Roman UI Automation assertions inside the 141/141 shell smoke. A fresh repository-driver launch with <code>-Native -Page romannum -WaitMs 15000</code> rejected its blank/near-uniform `PrintWindow` fallback; an inspected LowLevel headless-desktop HWND capture likewise showed only a title bar and blank client. No <code>screenshot-romannum.png</code> was created, reused, or synthesized. UI Automation is behavior evidence, not a visual pass, so this page is <code>capture-blocked</code> until a real composited frame can be inspected.

**粵語 —** 目前原生證據係 313/313 Debug／Release 套件入面嘅 17 個 core 專項測試，同 141/141 shell smoke 入面嘅 13 個即時 Roman UI Automation assertion。用 <code>-Native -Page romannum -WaitMs 15000</code> 做嘅最新 repository-driver 開啟拒絕咗空白／接近單色嘅 `PrintWindow` fallback；檢查過嘅 LowLevel 無頭 desktop HWND 擷取亦只有 title bar 同空白 client。冇建立、重用或者合成 <code>screenshot-romannum.png</code>。UI Automation 係行為證據，唔係視覺通過，所以喺可以檢查真正 composited frame 前，本頁係 <code>capture-blocked</code>。
