# Password Generator · 密碼產生器

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.passgen</code> |
| Deep-link alias · 深層連結別名 | <code>passgen</code> |
| Category · 分類 | Crypto & Passwords · 加密與密碼 |
| Page class · 頁面類別 | <code>PassGenModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/PassGenModule.xaml</code> |
| Button docs · 按鈕文件 | 2 |

## What It Covers · 功能範圍

**EN —** Password Generator is registered in WinForge search and navigation with these keywords: <code>password passphrase generator random secure entropy strength diceware csprng 密碼 通行短語 隨機 產生器 安全 熵值 強度</code>.

**粵語 —** 密碼產生器 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>password passphrase generator random secure entropy strength diceware csprng 密碼 通行短語 隨機 產生器 安全 熵值 強度</code>。

## Native C++ parity · 原生 C++ 對等

**EN —** The active native route is implemented by the testable `PassGen` C++ core and native WinUI page. It uses Windows BCrypt cryptographic randomness with rejection sampling; supports selected-class passwords, ambiguity removal, no-repeat enforcement, entropy, and 1–100 rows; and generates configurable 3–10-word passphrases from the current 252-word dictionary. Generated state survives language rerenders, while the clipboard is changed only by explicit Copy. Debug and Release native core tests each pass 368/368, including 14 focused Password Generator checks; the native UI Automation smoke passes 200/200. Fresh visual capture is `capture-blocked`: `CopyFromScreen` is unavailable and `PrintWindow` is blank/near-uniform; the requested LowLevel MCP is not registered in the active Codex session, so no LowLevel capture is claimed.

**粵語 —** 目前原生 route 由可測試嘅 `PassGen` C++ core 同原生 WinUI 頁實作。佢用 Windows BCrypt 加密碼學隨機值配合 rejection sampling；支援已揀類別密碼、移除易混淆字元、禁止重複、熵值同 1–100 行；亦會由現有 252 字詞字典產生可設定嘅 3–10 字通行短語。已產生狀態會喺轉語言時保留，而剪貼簿只會經明確 Copy 改動。Debug 同 Release 原生 core 測試各自通過 368/368，包括 14 個 Password Generator 專項檢查；原生 UI Automation smoke 通過 200/200。最新視覺擷取係 `capture-blocked`：`CopyFromScreen` 用唔到，而 `PrintWindow` 係空白／接近單色；要求嘅 LowLevel MCP 未有登記喺目前 Codex session，所以唔會聲稱有 LowLevel 擷取。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [GenerateBtn](../../buttons/crypto-passwords/passgen/001-generatebtn.md) | `Button` | `GenerateBtn` | `Generate_Click` |
| [CopyBtn](../../buttons/crypto-passwords/passgen/002-copybtn.md) | `Button` | `CopyBtn` | `Copy_Click` |
