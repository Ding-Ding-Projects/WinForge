# Base32 / 58 / 85 · Base32 / 58 / 85 編解碼

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.base32</code> |
| Deep-link alias · 深層連結別名 | <code>base32</code> |
| Category · 分類 | Encoding, IDs & Codes · 編碼識別碼與條碼 |
| Page class · 頁面類別 | <code>Base32Module</code> |
| Page XAML · 頁面 XAML | <code>Pages/Base32Module.xaml</code> |
| Button docs · 按鈕文件 | 4 |

## What It Covers · 功能範圍

**EN —** Base32 / 58 / 85 is registered in WinForge search and navigation with these keywords: <code>base32 base58 base85 ascii85 rfc4648 bitcoin adobe encode decode codec 編碼 解碼 編解碼 位元組</code>.

**粵語 —** Base32 / 58 / 85 編解碼 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>base32 base58 base85 ascii85 rfc4648 bitcoin adobe encode decode codec 編碼 解碼 編解碼 位元組</code>。

## Native C++ Rewrite Status · 原生 C++ 重寫狀態

**EN —** `module.base32` is implemented in native C++20/C++/WinRT and is reachable through `base32`, `base58`, and `module.base32`. The standard-C++ core matches the managed UTF-8 behavior for padded/unpadded RFC 4648 Base32, Bitcoin Base58, and Adobe Ascii85, including leading zero bytes, `<~ ~>` markers, and `z` zero groups. Codec selection, input, and output survive language rerenders; malformed input clears output atomically; UI Automation IDs and a polite live status are exposed; clipboard writes happen only after explicit Copy. Focused codec tests pass 15/15, aggregate native tests pass 296/296, and the live native UI Automation smoke passes 128/128, including horizontal-clipping bounds for the codec and Package Manager controls. Fresh native screenshot capture is `capture-blocked`: the repository driver and LowLevel MCP off-screen desktop both returned a blank/near-uniform WinUI client frame.

**粵語 —** `module.base32` 已經用原生 C++20/C++/WinRT 實作，可以經 `base32`、`base58` 同 `module.base32` 開啟。標準 C++ core 跟足受控版 UTF-8 行為，支援有填充／無填充 RFC 4648 Base32、Bitcoin Base58 同 Adobe Ascii85，包括開頭零位元組、`<~ ~>` 標記同 `z` 零組。轉語言時保留編碼方式、輸入同輸出；無效輸入會原子式清空；有 UI Automation ID 同 polite live status；剪貼簿只會喺明確 Copy 後先寫入。編解碼專項測試 15/15、整體原生測試 296/296、live 原生 UI Automation smoke 128/128，當中包括 codec 同套件管理控制項嘅水平裁切邊界。最新原生截圖係 `capture-blocked`：repository driver 同 LowLevel MCP 無頭 desktop 都只得到空白／接近單色 WinUI client frame。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [EncodeBtn](../../buttons/encoding-ids-codes/base32/001-encodebtn.md) | `Button` | `EncodeBtn` | `Encode_Click` |
| [DecodeBtn](../../buttons/encoding-ids-codes/base32/002-decodebtn.md) | `Button` | `DecodeBtn` | `Decode_Click` |
| [SwapBtn](../../buttons/encoding-ids-codes/base32/003-swapbtn.md) | `Button` | `SwapBtn` | `Swap_Click` |
| [CopyBtn](../../buttons/encoding-ids-codes/base32/004-copybtn.md) | `Button` | `CopyBtn` | `Copy_Click` |
