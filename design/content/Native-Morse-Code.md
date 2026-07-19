# Native Morse Code · 原生摩斯電碼

`module.morse` is now a dedicated C++/WinRT route over the standard-C++ `WinForge.Core/Morse` library. It keeps managed-compatible UTF-16 casing, International Morse encoding/decoding aliases, unknown-unit reporting, separator presets, local timing preview, explicit-only clipboard Copy, and timer cleanup on completion, route reset, and window close.

`module.morse` 而家係專用 C++/WinRT route，用標準 C++ `WinForge.Core/Morse` library；保留 managed 相容 UTF-16 大小寫、國際摩斯編碼／解碼 alias、未知 unit 提示、separator preset、本機 timing preview、只限明確 Copy 嘅剪貼簿，同完成／route reset／關窗嘅 timer cleanup。

Debug and Release native core each passed **741/741**, including **24/24** Morse contracts; focused native UI Automation passed **13/13** over `morse` and `module.morse`. The local LowLevel checkout was not callable in this session and the native driver rejected a blank capture fallback, so no screenshot was promoted and visual evidence is honestly `capture-blocked`.

Debug 同 Release native core 各自通過 **741/741**，包括 **24/24** Morse contract；專項 native UI Automation 喺 `morse` 同 `module.morse` 合共通過 **13/13**。今個 session 冇可呼叫嘅 LowLevel tool，而 native driver 拒絕咗空白 capture fallback，所以冇升格截圖，visual 如實係 `capture-blocked`。
