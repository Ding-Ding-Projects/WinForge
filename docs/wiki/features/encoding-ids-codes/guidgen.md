# GUID & ID Generator · GUID 同 ID 產生器

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.guidgen</code> |
| Deep-link alias · 深層連結別名 | <code>guidgen</code> |
| Category · 分類 | Encoding, IDs & Codes · 編碼識別碼與條碼 |
| Page class · 頁面類別 | <code>GuidGenModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/GuidGenModule.xaml</code> |
| Button docs · 按鈕文件 | 8 |

## What It Covers · 功能範圍

**EN —** GUID & ID Generator is registered in WinForge search and navigation with these keywords: <code>guid uuid ulid nanoid random id generator identifier crockford base32 version variant bytes GUID UUID 唯一識別碼 隨機 產生器 識別碼 位元組 版本</code>.

**粵語 —** GUID 同 ID 產生器 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>guid uuid ulid nanoid random id generator identifier crockford base32 version variant bytes GUID UUID 唯一識別碼 隨機 產生器 識別碼 位元組 版本</code>。

## Native C++ Rewrite Status · 原生 C++ 重寫狀態

**EN —** The native rewrite now implements this route in C++/WinRT through `guidgen` and `module.guidgen`. The standard-C++ core generates RFC 4122 v4 GUIDs in D/N/B/P/X formats, supports uppercase and 1–1000 bulk output, creates ULIDs and nano-IDs, and inspects GUID bytes, version, and variant. State is preserved across language rerenders and clipboard access is explicit Copy-only.

**粵語 —** 原生重寫而家已經用 C++/WinRT 實作呢條 route，可以經 `guidgen` 同 `module.guidgen` 開啟。標準 C++ core 會產生 RFC 4122 v4 GUID（D/N/B/P/X 格式）、支援大楷同 1–1000 個批量輸出、產生 ULID 同 nano-ID，亦會檢查 GUID bytes、version 同 variant。轉語言時會保留狀態，剪貼簿只會喺明確 Copy 後使用。

**EN —** Evidence: Debug and Release native tests pass **281/281**, including **16** focused GUID cases, and the native UI Automation smoke passes **115/115** after driving format switching, uppercase output, bulk generation, ULID, nano-ID, inspector valid/invalid states, and language rerender preservation. Fresh native screenshot capture is still `capture-blocked`: both the repository driver and the local LowLevel headless-desktop fallback produced blank/near-uniform WinUI client frames, so no stale, blank, or managed screenshot is accepted as native visual proof.

**粵語 —** 證據：Debug 同 Release 原生測試都係 **281/281** 通過，當中包括 **16** 個 GUID 專項案例；原生 UI Automation smoke 亦係 **115/115**，已操作格式切換、大楷輸出、批量產生、ULID、nano-ID、inspector 有效／無效狀態同轉語言保留。最新原生截圖仍然係 `capture-blocked`：repo driver 同本機 LowLevel 無頭 desktop fallback 都只得到空白／接近單色 WinUI client frame，所以唔會用舊圖、空白圖或者受控版截圖當原生視覺證據。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [GuidGenBtn](../../buttons/encoding-ids-codes/guidgen/001-guidgenbtn.md) | `Button` | `GuidGenBtn` | `GenGuid_Click` |
| [GuidCopyBtn](../../buttons/encoding-ids-codes/guidgen/002-guidcopybtn.md) | `Button` | `GuidCopyBtn` | `CopyGuid_Click` |
| [BulkGenBtn](../../buttons/encoding-ids-codes/guidgen/003-bulkgenbtn.md) | `Button` | `BulkGenBtn` | `GenBulk_Click` |
| [BulkCopyBtn](../../buttons/encoding-ids-codes/guidgen/004-bulkcopybtn.md) | `Button` | `BulkCopyBtn` | `CopyBulk_Click` |
| [UlidGenBtn](../../buttons/encoding-ids-codes/guidgen/005-ulidgenbtn.md) | `Button` | `UlidGenBtn` | `GenUlid_Click` |
| [UlidCopyBtn](../../buttons/encoding-ids-codes/guidgen/006-ulidcopybtn.md) | `Button` | `UlidCopyBtn` | `CopyUlid_Click` |
| [NanoGenBtn](../../buttons/encoding-ids-codes/guidgen/007-nanogenbtn.md) | `Button` | `NanoGenBtn` | `GenNano_Click` |
| [NanoCopyBtn](../../buttons/encoding-ids-codes/guidgen/008-nanocopybtn.md) | `Button` | `NanoCopyBtn` | `CopyNano_Click` |
