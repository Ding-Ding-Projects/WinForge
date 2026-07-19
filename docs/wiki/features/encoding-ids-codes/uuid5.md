# Namespaced UUID · 具名空間 UUID

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.uuidv5</code> |
| Deep-link alias · 深層連結別名 | <code>uuid5</code> |
| Category · 分類 | Encoding, IDs & Codes · 編碼識別碼與條碼 |
| Page class · 頁面類別 | <code>UuidV5Module</code> |
| Page XAML · 頁面 XAML | <code>Pages/UuidV5Module.xaml</code> |
| Button docs · 按鈕文件 | 3 |

## What It Covers · 功能範圍

**EN —** Namespaced UUID is registered in WinForge search and navigation with these keywords: <code>uuid guid v5 v3 sha1 md5 namespace rfc 4122 deterministic hash dns url oid x500 具名空間 命名空間 雜湊 確定性 標識符</code>.

**粵語 —** 具名空間 UUID 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>uuid guid v5 v3 sha1 md5 namespace rfc 4122 deterministic hash dns url oid x500 具名空間 命名空間 雜湊 確定性 標識符</code>。

## Native C++/WinRT Status · 原生 C++/WinRT 狀態

**EN —** `uuid5`, `uuidv5`, and `module.uuidv5` now open a genuine native Namespaced UUID page backed by the standard-C++ `UuidV5` core. It implements RFC 4122 deterministic v3 (MD5) and v5 (SHA-1) generation in network byte order for DNS, URL, OID, X500, and custom namespaces. The custom parser follows managed `Guid.TryParse` D/N/B/P/X behavior; malformed UTF-16 is replacement-encoded, the exact managed whitespace set is used (so U+180E remains name data), bulk rows keep managed trim and CRLF behavior, and only an explicit nonempty Copy can touch the clipboard.

**粵語 —** `uuid5`、`uuidv5` 同 `module.uuidv5` 而家會開真正原生具名空間 UUID 頁面，由標準 C++ `UuidV5` core 支援。佢會用 network byte order，為 DNS、URL、OID、X500 同自訂 namespace 產生 RFC 4122 確定性 v3（MD5）同 v5（SHA-1）。自訂 parser 跟 managed `Guid.TryParse` 嘅 D/N/B/P/X 行為；無效 UTF-16 會 replacement-encode，空白集合同 managed 一致（U+180E 會保留做名稱資料），bulk 行保留 managed trim 同 CRLF 行為，而且只有明確兼非空嘅 Copy 先會改剪貼簿。

**Current controlled-integration evidence · 目前受控整合證據：** renderer accounting is **33/346 fixed routes**, leaving **313 fixed routes** plus five dynamic families; the ledger is **33 `in-progress` / 313 `not-started`**. Fresh native Debug and Release x64 solution builds each exit 0 with 0 errors, and both combined core executables pass **815/815**, including **14/14** UUID v3/v5 contracts. Focused process-owned UI Automation passes **21/21** across the three aliases, covering RFC vectors, custom D/N/B/P/X parsing, empty bulk-copy no-op, language retention, accessibility, clipping, lifecycle release, and reset. Catalog parity passes all 346 fixed routes plus five dynamic families, 319 registry entries, 22 categories, and 346 ledger rows; the native installer contract and full native shell **469/469** pass. LowLevel Computer Use MCP is not callable in this Codex session. The fresh `uuid5` driver found `CopyFromScreen` unavailable and rejected a blank/near-uniform `PrintWindow` frame; no PNG was created or retained, no process remained, and no canonical screenshot changed. Visual evidence is honestly `capture-blocked`. · Renderer 計 **33/346**，仲有 **313** 條固定 route 同五組動態家族；ledger 係 **33 `in-progress` / 313 `not-started`**。最新 Debug／Release build 各 0 errors、兩個合併 core 各 **815/815**，包括 UUID v3/v5 **14/14**；三個 alias 專項 UIA **21/21**，覆蓋 RFC vector、自訂 D/N/B/P/X parser、空 bulk-copy no-op、語言 state retention、無障礙、裁切、route 釋放同 reset。catalog parity 通過 346 條固定 route 加五組動態家族、319 條 registry、22 個分類同 346 條 ledger；native installer contract 同完整 native shell **469/469** 都通過。今次 session 冇可呼叫 LowLevel MCP；最新 `uuid5` driver 發現 `CopyFromScreen` 唔可用，並拒絕空白／近乎單色 `PrintWindow` 畫面。冇建立或保留 PNG、冇殘留 process、冇改 canonical 截圖，所以 visual 如實係 `capture-blocked`。

**Full controlled sweep · 完整受控掃描：** the UUID-merged native shell passes **469/469 (469 passed, 0 failed)** and leaves no root-worktree app process. · UUID merge 後完整 native shell 通過 **469/469（469 passed、0 failed）**，而且冇殘留 root worktree app process。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [CopyBtn](../../buttons/encoding-ids-codes/uuid5/001-copybtn.md) | `Button` | `CopyBtn` | `Copy_Click` |
| [BulkRunBtn](../../buttons/encoding-ids-codes/uuid5/002-bulkrunbtn.md) | `Button` | `BulkRunBtn` | `BulkRun_Click` |
| [BulkCopyBtn](../../buttons/encoding-ids-codes/uuid5/003-bulkcopybtn.md) | `Button` | `BulkCopyBtn` | `BulkCopy_Click` |
