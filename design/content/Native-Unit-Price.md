# Native Unit Price · 原生單位價格

`priceper`, `unitprice`, and `module.unitprice` now resolve to a genuine C++/WinRT page backed by the standard-C++ `UnitPrice` core. It keeps managed-compatible valid-row filtering, free/infinity and tolerance-tie behavior, currency formatting, first-unit Add, lifecycle reset, language-state retention, and explicit-only comparison Copy. All computation is local; no network, process, file, registry, elevation, persistence, or secret path exists.

`priceper`、`unitprice` 同 `module.unitprice` 而家會開真正 C++/WinRT 頁，由標準 C++ `UnitPrice` core 支援。保留 managed 相容嘅有效行篩選、免費／infinity、容差平手、貨幣格式、第一行單位 Add、lifecycle reset、語言狀態保留同只限明確 Copy。全部運算只喺本機做，冇網絡、程序、檔案、registry、提升權限、持久化或者 secret 路徑。

In the controlled integration, Debug/Release native builds are 0-error, each combined core suite is **828/828** (Unit Price **13/13**), focused Unit Price UI Automation is **15/15**, Utility UIA is **39/39** including CSS Unit Converter, catalog parity is **346 + 5**, and the installer contract passes. LowLevel MCP is not callable in this session and the required driver rejected a blank fallback, so no screenshot was promoted and visual evidence remains `capture-blocked`. The broad aggregate did not yield a captured final footer; it is not claimed as a completed full-shell result.

喺受控整合，Debug／Release native build 都係 0-error、合併 core 各 **828/828**（Unit Price **13/13**）、專項 Unit Price UI Automation **15/15**、包括 CSS Unit Converter 嘅 Utility UIA **39/39**、catalog parity **346 + 5** 同 installer contract 都通過。今個 session 冇可呼叫 LowLevel MCP，required driver 拒絕空白 fallback，所以冇升格截圖，visual 保持 `capture-blocked`。廣泛 aggregate 冇最後 footer，唔聲稱係完成 full-shell 結果。
