# chmod Calculator · chmod 計算機

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.unixperm</code> |
| Deep-link alias · 深層連結別名 | <code>unixperm</code> |
| Category · 分類 | Crypto & Passwords · 加密與密碼 |
| Page class · 頁面類別 | <code>UnixPermModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/UnixPermModule.xaml</code> |
| Button docs · 按鈕文件 | 3 |

## What It Covers · 功能範圍

**EN —** chmod Calculator is registered in WinForge search and navigation with these keywords: <code>chmod unix permission octal symbolic rwx setuid setgid sticky linux file mode 權限 八進位 符號 檔案模式</code>.

**粵語 —** chmod 計算機 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>chmod unix permission octal symbolic rwx setuid setgid sticky linux file mode 權限 八進位 符號 檔案模式</code>。

## Native C++ Parity · 原生 C++ 對等

**EN —** `unixperm`, `chmod`, and `module.unixperm` now open a dedicated C++/WinRT page backed by testable standard C++. It starts at `0644` (`rw-r--r--`), keeps the owner/group/other read-write-execute matrix plus setuid, setgid, and sticky bits synchronized with two-way octal and symbolic editors, and previews—but never executes—the corresponding `chmod` command. Invalid input preserves the last valid mode atomically. Clipboard access occurs only through the three explicit Copy buttons.

**粵語 —** `unixperm`、`chmod` 同 `module.unixperm` 而家會開啟專用 C++/WinRT 頁，由可獨立測試嘅標準 C++ core 支援。預設係 `0644`（`rw-r--r--`）；擁有者／群組／其他人嘅讀寫執行矩陣、setuid、setgid、sticky 位，會同雙向八進位及符號編輯器同步。頁面只預覽相應 `chmod` 指令，絕不執行；無效輸入會完整保留上一個有效模式。只有三個明確 Copy 按鈕先會存取剪貼簿。

The focused core suite covers 20 contracts, including every one of the 4,096 permission modes. Stable UI Automation IDs cover all 12 permission bits, both editors, command preview, status, and Copy actions. Fresh headless visual evidence is `capture-blocked`: the requested LowLevel MCP launched the exact native window on an isolated desktop, but hidden WinUI composition stayed blank and `SwitchDesktop` failed with Win32 error 5. The visible desktop was not used and no blank image was promoted. · 專項 core suite 覆蓋 20 個合約，包括全部 4,096 個權限模式。所有 12 個權限位、兩個編輯器、指令預覽、狀態同 Copy 動作都有穩定 UI Automation ID。最新無頭視覺證據係 `capture-blocked`：指定 LowLevel MCP 喺隔離 desktop 開到準確原生視窗，但隱藏 WinUI composition 仍然空白，而 `SwitchDesktop` 以 Win32 錯誤 5 失敗；冇使用可見 desktop，亦冇將空白圖當證據。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [CopyOctalBtn](../../buttons/crypto-passwords/unixperm/001-copyoctalbtn.md) | `Button` | `CopyOctalBtn` | `CopyOctal_Click` |
| [CopySymbolicBtn](../../buttons/crypto-passwords/unixperm/002-copysymbolicbtn.md) | `Button` | `CopySymbolicBtn` | `CopySymbolic_Click` |
| [CopyCommandBtn](../../buttons/crypto-passwords/unixperm/003-copycommandbtn.md) | `Button` | `CopyCommandBtn` | `CopyCommand_Click` |
