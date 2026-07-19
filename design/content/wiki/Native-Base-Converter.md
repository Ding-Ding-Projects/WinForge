# Native Base Converter · 原生進位轉換

## Controlled native integration · 受控原生整合

`baseconvert` and `module.baseconvert` are integrated in the native C++ working tree as a genuine local C++/WinRT renderer over a dependency-free standard-C++ arbitrary-precision core. Release automation remains unchanged and C++-only.

`baseconvert` 同 `module.baseconvert` 已受控整合入原生 C++ working tree，係真正本機 C++/WinRT renderer 加唔靠依賴嘅標準 C++ 任意精度 core。release automation 冇改，仍然只限 C++。

## Evidence checkpoint · 證據 checkpoint

Debug and Release x64 builds have 0 errors; both combined core suites pass **857/857**, including **15/15** Base Converter contracts; focused UI Automation passes **14/14** across both aliases; catalog parity is 346 fixed routes plus five dynamic families with 319 registry entries and 346 ledger rows; and the native installer contract passes. Renderer accounting is **36/346** (`36 in-progress / 310 not-started`), including managed Unicode `Trim()` parity for invalid-input diagnostics.

Debug／Release x64 build 0 errors；合併 core 各通過 **857/857**，包括 Base Converter **15/15** 個合約；兩個 alias 專項 UI Automation 通過 **14/14**；catalog parity 係 346 固定 route 加五組動態家族、319 個 registry entry 同 346 條 ledger row；native installer contract 都通過。renderer 計數係 **36/346**（`36 in-progress / 310 not-started`），無效輸入診斷亦同 managed Unicode `Trim()` 對等。

## Visual status · 視覺狀態

The local LowLevel MCP checkout is present but no headless-desktop tool is callable in this session. The fresh current `driver.ps1 -Native -Page baseconvert -WaitMs 16000` attempt found `CopyFromScreen` unavailable, rejected the blank/near-uniform PrintWindow fallback, and retained no PNG or WinForge process; this route is `capture-blocked`, not visual-pass.

本機 LowLevel MCP checkout 喺度，但今個 session 冇可呼叫嘅 headless-desktop 工具。最新目前 `driver.ps1 -Native -Page baseconvert -WaitMs 16000` 發現 `CopyFromScreen` 不可用、拒絕空白／近乎單色 PrintWindow fallback，亦冇保留 PNG／WinForge process；route 係 `capture-blocked`，唔係 visual-pass。
