# WinForge Roadmap · 路線圖

## Native C++ migration · 原生 C++ 遷移

- [x] Deliver the native Slugify route: `slug`, `slugify`, and `module.slugify` now use standard-C++ `Slugify` logic plus a C++/WinRT renderer, with managed-compatible line, Unicode, option, preview, lifecycle, and explicit-copy behavior. Local evidence is Debug/Release core **777/777** (Slugify **18/18**), catalog parity **346 + 5**, and focused UIA **12/12**; visual evidence remains honestly `capture-blocked` because this session cannot call LowLevel MCP and the driver rejected its blank fallback. · 完成原生網址別名 route：三個 alias 已用標準 C++ `Slugify` 同 C++/WinRT renderer，保留 managed 分行、Unicode、選項、預覽、生命週期同明確 Copy 行為。本機證據係 core **777/777**（Slugify **18/18**）、catalog parity **346 + 5** 同 UIA **12/12**；今次冇可呼叫 LowLevel MCP，而 driver 拒絕空白 fallback，所以 visual 如實係 `capture-blocked`。
- [ ] Continue porting every remaining fixed native route and five dynamic route families with route-specific core, UIA, documentation, and fresh visual evidence. A pending renderer is not a completed port. · 繼續逐項移植其餘固定 route 同五組動態家族，每項都要有 core、UIA、文件同最新視覺證據；pending renderer 唔算完成。
- [ ] Keep native CI/release publishing C++ artifacts only after tests pass, with exactly one uniquely tagged non-draft release for each eligible push. · 保持原生 CI 只喺測試通過後發佈 C++ artifact，而且每次合資格 push 只出一個唯一 tag、非草稿 release。
