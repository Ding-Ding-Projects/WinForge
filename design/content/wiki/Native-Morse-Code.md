# Native Morse Code · 原生摩斯電碼

`module.morse` is a dedicated C++/WinRT implementation backed by standard-C++ Morse logic. It preserves managed-compatible UTF-16 casing, Morse aliases, separator presets, local flash timing, explicit-only clipboard Copy, and timer cleanup.

`module.morse` 係專用 C++/WinRT 實作，用標準 C++ Morse logic；保留 managed 相容 UTF-16 大小寫、Morse alias、separator preset、本機閃燈 timing、只限明確 Copy 嘅剪貼簿同 timer cleanup。

Native Debug/Release core passed **741/741** with **24/24** focused Morse tests, and focused UI Automation passed **13/13** across `morse` and `module.morse`. The LowLevel MCP checkout was not callable here; the driver rejected its blank fallback, so visual evidence remains `capture-blocked` and no image was published.

Native Debug／Release core 通過 **741/741**、其中 Morse **24/24**；專項 UI Automation 喺 `morse` 同 `module.morse` 通過 **13/13**。呢度冇可呼叫 LowLevel MCP，driver 拒絕空白 fallback，visual 保持 `capture-blocked`，冇發佈任何圖片。
