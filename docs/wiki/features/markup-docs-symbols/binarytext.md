# Text to Binary · 文字轉二進位

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.binarytext</code> |
| Deep-link alias · 深層連結別名 | <code>binarytext</code> |
| Category · 分類 | Markup, Docs & Symbols · 標記文件與符號 |
| Page class · 頁面類別 | <code>BinaryTextModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/BinaryTextModule.xaml</code> |
| Button docs · 按鈕文件 | 4 |

## What It Covers · 功能範圍

**EN —** Text to Binary is registered in WinForge search and navigation with these keywords: <code>binary text codes utf-8 encode decode ascii hex octal decimal base converter 二進位 文字 編碼 解碼 十六進位 八進位 十進位 位元組 轉換</code>.

**粵語 —** 文字轉二進位 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>binary text codes utf-8 encode decode ascii hex octal decimal base converter 二進位 文字 編碼 解碼 十六進位 八進位 十進位 位元組 轉換</code>。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [EncodeBtn](../../buttons/markup-docs-symbols/binarytext/001-encodebtn.md) | `Button` | `EncodeBtn` | `Encode_Click` |
| [DecodeBtn](../../buttons/markup-docs-symbols/binarytext/002-decodebtn.md) | `Button` | `DecodeBtn` | `Decode_Click` |
| [SwapBtn](../../buttons/markup-docs-symbols/binarytext/003-swapbtn.md) | `Button` | `SwapBtn` | `Swap_Click` |
| [CopyBtn](../../buttons/markup-docs-symbols/binarytext/004-copybtn.md) | `Button` | `CopyBtn` | `Copy_Click` |

## Native C++ Migration · 原生 C++ 遷移

**EN —** `module.binarytext` now has a genuine C++20/C++/WinRT implementation rather than a pending shell. The standard-C++ core in `src/WinForge.Core/BinaryText.cpp` preserves the managed UTF-8 byte contract: padded binary, decimal, octal, uppercase two-digit hexadecimal, supported separators plus managed Unicode trimming around a code, matching `0b`/`0o`/`0x` prefixes, atomic malformed/range failure, and replacement-character behavior for malformed UTF-8 continuation-prefix/invalid-scalar boundaries and UTF-16. The native page is reached by `binarytext`, `textbinary`, and `module.binarytext`; its stable automation IDs are `NativeBinaryTextBase`, `NativeBinaryTextInput`, `NativeBinaryTextEncode`, `NativeBinaryTextDecode`, `NativeBinaryTextSwap`, `NativeBinaryTextCopy`, `NativeBinaryTextOutput`, and `NativeBinaryTextStatus`.

**粵語 —** `module.binarytext` 而家有真正 C++20／C++/WinRT 實作，唔再係 pending shell。`src/WinForge.Core/BinaryText.cpp` 入面嘅標準 C++ core 保留受控 UTF-8 位元組規則：補足位嘅二進位、十進位、八進位、大楷兩位十六進位、支援分隔同數字碼前後嘅受控 Unicode 修剪、配對 `0b`／`0o`／`0x` prefix、原子式格式錯誤／範圍失敗，同無效 UTF-8 continuation-prefix／無效 scalar 邊界同 UTF-16 嘅替代字元行為。原生頁可以用 `binarytext`、`textbinary` 同 `module.binarytext` 開；穩定 automation ID 係 `NativeBinaryTextBase`、`NativeBinaryTextInput`、`NativeBinaryTextEncode`、`NativeBinaryTextDecode`、`NativeBinaryTextSwap`、`NativeBinaryTextCopy`、`NativeBinaryTextOutput` 同 `NativeBinaryTextStatus`。

**Evidence · 證據：** Native Debug and Release test executables pass 233/233 (191 core route/package-manager checks plus 42 parser checks), including 25 focused Text to Binary cases; the linked managed-reference regression passes 18/18; process-owned UI Automation passes 59/59 and drives Encode, Decode, Move output to input, explicit Copy, invalid-output clear, selected-base/input/output language-state preservation, and all aliases. The reference regression is not an automated native-vs-managed process comparison. The required 2026-07-13 `driver.ps1 -Native -Page binarytext -Out …` capture is `capture-blocked`: `CopyFromScreen` was unavailable and `PrintWindow` produced a blank or near-uniform WinUI client frame. No native screenshot was created, replaced, reused, or synthesized, so the ledger is honestly **in progress**, not visual-pass. · 原生 Debug 同 Release 測試 executable 係 233/233（191 個 core route／package-manager 檢查加 42 個 parser 檢查），包括 25 個文字轉二進位專項案例；連結嘅受控參考回歸係 18/18；自有 process UI Automation 係 59/59，會操作 Encode、Decode、搬輸出去輸入、明確 Copy、無效輸出清空、轉語言後保留已揀進位／輸入／輸出同全部 alias。參考回歸唔係自動原生對受控 process 比較。指定嘅 2026-07-13 `driver.ps1 -Native -Page binarytext -Out …` 截圖係 `capture-blocked`：`CopyFromScreen` 用唔到，而 `PrintWindow` 產生空白／接近單色 WinUI client frame。冇建立、替換、重用或者合成原生截圖，所以清單如實係**進行中**，唔係 visual-pass。
