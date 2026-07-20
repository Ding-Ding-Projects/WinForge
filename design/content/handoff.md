# WinForge Handoff · WinForge 交接

WinForge is the canonical .NET 11 / WinUI 3 application. The experimental C++20/C++/WinRT port has moved to [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native) with its own source, tests, parity evidence, installer, documentation, and releases.

WinForge 係正式 .NET 11／WinUI 3 app。實驗性 C++20/C++/WinRT 移植版已搬去 [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native)，並獨立保存 source、tests、parity 證據、installer、文件同 release。

## What remains here · 呢度保留乜

- Managed application source and tests · 正式 app source 同 tests
- Managed installer, updater, portable-package, and release contract · 正式 installer、updater、portable package 同 release 合約
- Bilingual documentation, wiki, generated feature references, and Pages content · 雙語文件、wiki、自動產生功能 reference 同 Pages 內容
- Small C++ companion apps below `native/`, built and launched by managed WinForge · `native/` 下由正式 WinForge 建置同啟動嘅細型 C++ companion app

## Validation · 驗證

The managed compile gate is `dotnet build WinForge.sln -c Debug -p:Platform=x64` with zero errors. The reactor/dependent-service gate is `dotnet run --project tests/ReactorSim.Tests -c Debug`, currently 63/63 with nonzero exit on failure. Use the repository's managed-only run driver for a process-owned deep-link launch or screenshot.

正式編譯 gate 係指定 `dotnet build` command 同零 errors；反應堆／依賴服務 gate 係指定 `ReactorSim.Tests` command，目前 63/63，失敗時非零退出。直接開頁或者截圖要用 repo 嘅 managed-only driver，只控制自己開嘅 process。

## Repository split proof · Repository 分拆證明

The managed split feature `fe791aa6167dbe26dc358df3a31acce51bd0f931` was merged and remotely proved. The expected site-data refresh advanced canonical `main` to `be054aa737df860b1185bd7b1102d8dd9e80ae8e`; [managed CI](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715701032), [site data](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715516151), and [Pages](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715705513) passed. [`v1.1.259`](https://github.com/Ding-Ding-Projects/WinForge/releases/tag/v1.1.259) is stable GitHub Latest at that exact SHA with only the managed setup and portable ZIP.

Managed 分拆功能已 merge 同做完 remote proof；預期 site-data 將正式 `main` 更新到 `be054aa7`，CI、site-data 同 Pages 全綠。`v1.1.259` 係準確指向該 SHA 嘅 stable GitHub Latest，只得 managed setup 同 portable ZIP。

Standalone native `main` is `a64e8e30ed8b5fe376197448ba760d1374244c69`; its [CI/release](https://github.com/codingmachineedge/WinForge-Native/actions/runs/29715120945), [Pages](https://codingmachineedge.github.io/WinForge-Native/), and [`native-v1.1.7`](https://github.com/codingmachineedge/WinForge-Native/releases/tag/native-v1.1.7) are green. Managed Wiki commit `be2571545ee81b9286f36a8a96aa72fdc92769b2` is live. GitHub has not initialized the native Wiki and no authenticated browser or supported Wiki API was available, so native tracked docs and Pages are the published documentation.

獨立原生 `main` `a64e8e30` 嘅 CI／release／Pages 全綠，managed Wiki 亦已上線。GitHub 尚未初始化原生 Wiki，而今次冇已登入 browser／支援 API，所以原生 tracked docs 同 Pages 就係正式發佈文件。

See the repository [handoff summary](https://github.com/codingmachineedge/WinForge/blob/main/handoff-summary.md) for final commit, remote-integration, workflow, release, and cleanup proof.
