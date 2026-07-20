# Native C++ Port Moved · 原生 C++ 移植版已搬遷

The experimental C++20/C++/WinRT port now lives at [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native), with its own source, tests, parity evidence, installer, documentation, roadmap, and releases.

實驗性 C++20/C++/WinRT 移植版而家喺 [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native)，獨立保存 source、tests、parity 證據、installer、文件、路線圖同 release。

This repository remains the canonical .NET 11 / WinUI 3 WinForge application. Historical native artifacts here are provenance only.

呢個 repository 繼續係正式 .NET 11／WinUI 3 WinForge app；留低嘅歷史原生 artifact 只作來源記錄。

Legacy C++/WinRT checkout refs are retained in the standalone native repository before their old worktrees are retired. That archival retention does not reintroduce rewrite source into managed main. Date Calculator, Duration Calculator, and Loan Calculator remain separate pushed native WIP work, not native-main integrations.

舊 C++/WinRT checkout ref 會喺退役 worktree 前先保留喺獨立 native repository。呢個 archive retention 唔會將 rewrite source 放返入 managed main。Date Calculator、Duration Calculator、Loan Calculator 仍然係獨立已 push 嘅 native WIP，唔係 native-main 整合。
