# Check Digit Validator · 檢查碼驗證器

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.checkdigit</code> |
| Deep-link alias · 深層連結別名 | <code>checkdigit</code> |
| Category · 分類 | Encoding, IDs & Codes · 編碼識別碼與條碼 |
| Page class · 頁面類別 | <code>CheckDigitModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/CheckDigitModule.xaml</code> |
| Button docs · 按鈕文件 | 0 |

## What It Covers · 功能範圍

**EN —** Check Digit Validator is registered in WinForge search and navigation with these keywords: <code>checksum luhn credit card isbn ean upc iban mod97 check digit validator barcode 檢查碼 校驗碼 信用卡 條碼 銀行帳號</code>.

**粵語 —** 檢查碼驗證器 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>checksum luhn credit card isbn ean upc iban mod97 check digit validator barcode 檢查碼 校驗碼 信用卡 條碼 銀行帳號</code>。

## Native C++ parity · 原生 C++ 對等

**EN —** The native C++/WinRT page implements all six schemes directly: Luhn with Visa/Amex/Mastercard/Discover detection, ISBN-10 (including an `X` check), ISBN-13, EAN-13, UPC-A, and IBAN. It reports valid/invalid separately from malformed input and shows the expected check value. IBAN uses bounded incremental mod-97 arithmetic, so the native route does not depend on CLR `BigInteger` or an external process.

**粵語 —** 原生 C++/WinRT 頁面直接實作六個格式：Luhn 連 Visa／Amex／Mastercard／Discover 卡種識別、ISBN-10（包括 `X` 檢查碼）、ISBN-13、EAN-13、UPC-A 同 IBAN。頁面會分開顯示有效／無效同輸入格式錯誤，亦會列出應有檢查值。IBAN 用有界增量 mod-97 運算，所以原生 route 唔依賴 CLR `BigInteger` 或外部 process。

The standards gate is shared by the native implementation and managed behavioral oracle:

- Digit-oriented identifiers accept ASCII `0–9`; Unicode lookalike numerals fail as malformed. · 數字識別碼只接受 ASCII `0–9`；Unicode 相似數字會當成格式錯誤。
- Discover labelling follows the [official Discover Global Network range summary](https://www.discoverglobalnetwork.com/downloads/IPP_VAR_Compliance.pdf): `60110000–60119999` and `64400000–65899999`, with 16–19-digit PANs. The adjacent `6590` and UnionPay-footnoted ranges are not mislabelled as Discover. · Discover 標籤跟[官方 Discover Global Network range summary](https://www.discoverglobalnetwork.com/downloads/IPP_VAR_Compliance.pdf)：`60110000–60119999` 同 `64400000–65899999`，PAN 長度 16–19 位；相鄰 `6590` 同標為 UnionPay 嘅 ranges 唔會被錯標 Discover。
- ISBN-13 accepts only the [International ISBN Agency's](https://www.isbn-international.org/index.php/content/what-isbn/10) registered `978` or `979` prefixes; a checksum-valid product EAN is not silently labelled an ISBN. · ISBN-13 只接受 [International ISBN Agency](https://www.isbn-international.org/index.php/content/what-isbn/10) 登記嘅 `978` 或 `979` 開頭；checksum 正確嘅商品 EAN 唔會被錯當 ISBN。
- IBAN follows the [SWIFT ISO 13616 Registry, Release 102 (June 2026)](https://www.swift.com/swift-resource/9606/download?language=en): `[A-Z]{2}[0-9]{2}`, one of 89 registered prefixes, exact per-country length, and the registered BBAN numeric/letter/alphanumeric classes must pass before MOD97-10. · IBAN 跟 [SWIFT ISO 13616 Registry Release 102（2026 年 6 月）](https://www.swift.com/swift-resource/9606/download?language=en)：先通過 `[A-Z]{2}[0-9]{2}`、89 個已登記 prefix、逐國固定長度同 BBAN 數字／字母／字母數字類別，之後先做 MOD97-10。

The scheme picker and input have localized accessible names. Result changes raise a polite live-region notification, and malformed or empty input clears both visible and UI Automation detail so assistive technology never reads a stale expected check. · 格式選擇器同輸入框有本地化無障礙名稱；結果更新會觸發 polite live-region 通知，而錯誤或空白輸入會同時清除畫面同 UI Automation detail，輔助技術唔會讀出舊檢查值。

All route forms—`checkdigit`, `luhn`, and `module.checkdigit`—open the native page. The current Debug and Release native test executables each pass 233/233 (191 core route/package-manager checks plus 42 parser checks), including 48 focused Check Digit cases. Both implementations are compared exactly with the independent 89-row Release 102 fixture, enforce the official Discover range boundaries, and reject Unicode letters that case-fold to ASCII; the linked managed standards regression passes 24/24. The 59/59 process-owned UI Automation suite exercises every scheme, malformed/invalid state, accessibility cleanup, and language-state retention while retaining the later Text to Binary checks, including its explicit Copy and selected-base retention assertions. The implementation has no network, file, registry, process, elevation, persistence, clipboard, or secret side effect.

全部 route form——`checkdigit`、`luhn` 同 `module.checkdigit`——都會開原生頁面。目前 Debug 同 Release 原生測試 executable 各自通過 233/233（191 個 core route／package-manager 檢查加 42 個 parser 檢查），包括 48 個檢查碼專項案例。兩個實作都會同獨立 89 行 Release 102 fixture 精確比較、執行官方 Discover range 邊界，亦拒絕會 case-fold 做 ASCII 嘅 Unicode 字母；連結嘅受控標準回歸 24/24 通過。而 59/59 自有 process UI Automation 套件會實際操作每個格式、錯誤／無效狀態、無障礙清理同轉語言保留狀態，亦保留之後加嘅文字轉二進位檢查，包括明確 Copy 同已揀進位保留 assertion。實作冇網絡、檔案、registry、process、提升權限、persistence、clipboard 或機密副作用。

```powershell
dotnet run --project tests\CheckDigitCore.Tests\CheckDigitCore.Tests.csproj -c Release
```

> **Visual evidence · 視覺證據：** A fresh native `checkdigit` capture was attempted with the repository driver. `CopyFromScreen` was unavailable and the `PrintWindow` fallback returned a blank/near-uniform WinUI client frame, so the driver rejected it. No stale, blank, synthetic, or managed image was substituted; the route remains `capture-blocked`, not visual-pass. · 已經用 repo driver 嘗試重新擷取原生 `checkdigit`；`CopyFromScreen` 用唔到，而 `PrintWindow` 後備回傳空白／接近單色 WinUI client frame，所以 driver 拒絕咗。冇用舊、空白、合成或受控版圖片頂替；route 仍然係 `capture-blocked`，唔係 visual-pass。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| None detected from XAML · XAML 未偵測到 |  |  |  |
