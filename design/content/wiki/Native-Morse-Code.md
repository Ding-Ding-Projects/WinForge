# Native Morse Code · 原生摩斯電碼

`module.morse` is a dedicated C++/WinRT implementation backed by standard-C++ Morse logic. It preserves managed-compatible UTF-16 casing, Morse aliases, separator presets, local flash timing, explicit-only clipboard Copy, and timer cleanup.

`module.morse` 係專用 C++/WinRT 實作，用標準 C++ Morse logic；保留 managed 相容 UTF-16 大小寫、Morse alias、separator preset、本機閃燈 timing、只限明確 Copy 嘅剪貼簿同 timer cleanup。

After controlled integration, native Debug/Release core passed **783/783** with **24/24** focused Morse tests, focused UI Automation passed **13/13** across `morse` and `module.morse`, catalog parity passed, and the exhaustive native shell passed **430/430**. The LowLevel MCP checkout was not callable here; the driver rejected its blank fallback, so visual evidence remains `capture-blocked` and no image was published.

受控整合後 Native Debug／Release core 通過 **783/783**、其中 Morse **24/24**；專項 UI Automation 喺 `morse` 同 `module.morse` 通過 **13/13**、catalog parity 同完整 native shell **430/430**。呢度冇可呼叫 LowLevel MCP，driver 拒絕空白 fallback，visual 保持 `capture-blocked`，冇發佈任何圖片。
