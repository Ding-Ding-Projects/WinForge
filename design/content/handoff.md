# WinForge Handoff · WinForge 交接

WinForge is the canonical .NET 11 / WinUI 3 application. The experimental C++20/C++/WinRT port has moved to [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native) with its own source, tests, parity evidence, installer, documentation, and releases.

WinForge 係正式 .NET 11／WinUI 3 app。實驗性 C++20/C++/WinRT 移植版已搬去 [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native)，並獨立保存 source、tests、parity 證據、installer、文件同 release。

## AWS Manager EC2 continuation · AWS Manager EC2 延續開發

The managed AWS Manager now has a native, bilingual EC2 workspace alongside S3 and Resource Explorer. It supports direct `ec2` and `s3` routes, paged instance discovery, local filtering, details, and guarded Start, Stop, Reboot, and Terminate reviews through AWSSDK.EC2. Context generations, operation ownership, stale-result rejection, post-dialog revalidation, fail-closed state policy, and atomic S3 `If-None-Match: *` uploads keep account and mutation boundaries explicit; discovered credentials remain metadata-only.

正式 AWS Manager 而家喺 S3／Resource Explorer 旁邊加入原生雙語 EC2 工作區，支援 `ec2`／`s3` 直達 route、分頁 instance 清單、本機篩選、詳情，同受保護嘅 Start／Stop／Reboot／Terminate confirmation。Context generation、operation ownership、過期結果拒絕、dialog 後重驗、fail-closed state policy，同 S3 `If-None-Match: *` 原子 upload，會清楚守住 account 同 mutation 界線；credential discovery 只保留 metadata。

Local gates are green: the solution builds with zero errors; all **27/27** Release test projects pass, including AWS **11/11** and Reactor **63/63**; XAML literal safety passes; and generated documentation reports 319 modules, 1,214 features, and 1,902 button references. The inspected credential-safe `2077×1302` direct-EC2 screenshot is mirrored in tracked docs. LowLevel MCP was present but not callable, so the owned self-contained driver launch used the app's debug `RenderTargetBitmap` capture after desktop capture and blank `PrintWindow` fallbacks failed; no live AWS query or mutation occurred. Git/hosted completion proof will be added after the feature reaches remote `main`.

本機 gate 全綠：solution 零 errors、Release test project **27/27**（AWS **11/11**、Reactor **63/63**）、XAML safety 同生成文件全部通過；tracked docs 已同步經檢視、credential-safe 嘅 `2077×1302` EC2 截圖。LowLevel MCP 雖然喺 disk，但今次唔可呼叫；desktop capture 同空白 `PrintWindow` fallback 失敗後，改用 owned self-contained driver launch 加 app debug `RenderTargetBitmap`，全程冇查詢或改動真實 AWS。功能到達 remote `main` 後會補上 Git／hosted completion proof。

## What remains here · 呢度保留乜

- Managed application source and tests · 正式 app source 同 tests
- Managed installer, updater, portable-package, and release contract · 正式 installer、updater、portable package 同 release 合約
- Bilingual documentation, wiki, generated feature references, and Pages content · 雙語文件、wiki、自動產生功能 reference 同 Pages 內容
- Small C++ companion apps below `native/`, built and launched by managed WinForge · `native/` 下由正式 WinForge 建置同啟動嘅細型 C++ companion app

## Validation · 驗證

The managed compile gate is `dotnet build WinForge.sln -c Debug -p:Platform=x64` with zero errors. The reactor/dependent-service gate is `dotnet run --project tests/ReactorSim.Tests -c Debug`, currently 63/63 with nonzero exit on failure. Use the repository's managed-only run driver for a process-owned deep-link launch or screenshot.

正式編譯 gate 係指定 `dotnet build` command 同零 errors；反應堆／依賴服務 gate 係指定 `ReactorSim.Tests` command，目前 63/63，失敗時非零退出。直接開頁或者截圖要用 repo 嘅 managed-only driver，只控制自己開嘅 process。

## Repository split proof · Repository 分拆證明

The managed split feature `fe791aa6167dbe26dc358df3a31acce51bd0f931` was merged and remotely proved. Before this completion record, the expected site-data refresh advanced the proved integration tip to `be054aa737df860b1185bd7b1102d8dd9e80ae8e`; [managed CI](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715701032), [site data](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715516151), and [Pages](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715705513) passed. [`v1.1.259`](https://github.com/Ding-Ding-Projects/WinForge/releases/tag/v1.1.259) was stable GitHub Latest at that exact integration tip with only the managed setup and portable ZIP; every later docs-only `main` commit remains subject to the same workflow.

Managed 分拆功能已 merge 同做完 remote proof；呢段完成記錄之前，預期 site-data 將已驗證 integration tip 更新到 `be054aa7`，CI、site-data 同 Pages 全綠。`v1.1.259` 喺該 integration tip 係 stable GitHub Latest，只得 managed setup 同 portable ZIP；之後 docs-only `main` commit 仍然要跑同一 workflow。

Standalone native `main` is `a64e8e30ed8b5fe376197448ba760d1374244c69`; its [CI/release](https://github.com/codingmachineedge/WinForge-Native/actions/runs/29715120945), [Pages](https://codingmachineedge.github.io/WinForge-Native/), and [`native-v1.1.7`](https://github.com/codingmachineedge/WinForge-Native/releases/tag/native-v1.1.7) are green. Managed Wiki commit `be2571545ee81b9286f36a8a96aa72fdc92769b2` is live. GitHub has not initialized the native Wiki and no authenticated browser or supported Wiki API was available, so native tracked docs and Pages are the published documentation.

獨立原生 `main` `a64e8e30` 嘅 CI／release／Pages 全綠，managed Wiki 亦已上線。GitHub 尚未初始化原生 Wiki，而今次冇已登入 browser／支援 API，所以原生 tracked docs 同 Pages 就係正式發佈文件。

See the repository [handoff summary](https://github.com/codingmachineedge/WinForge/blob/main/handoff-summary.md) for final commit, remote-integration, workflow, release, and cleanup proof.
