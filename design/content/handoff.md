# WinForge Handoff · WinForge 交接

WinForge is the canonical .NET 11 / WinUI 3 application. The experimental C++20/C++/WinRT port has moved to [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native) with its own source, tests, parity evidence, installer, documentation, and releases.

WinForge 係正式 .NET 11／WinUI 3 app。實驗性 C++20/C++/WinRT 移植版已搬去 [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native)，並獨立保存 source、tests、parity 證據、installer、文件同 release。

## Preserved package-stash reconciliation · 已保存套件 stash 對帳

The six-file `codex-temp-powertoys` stash (`181fc231c...`) was reviewed without applying or dropping it. Its useful coordinator, schema-compatibility, solution-registration, and PowerToys discoverability intent already exists in newer code; malformed and stale hunks were rejected. Portable-bundle saves now stage beside the destination and report success only after an atomic replace/move, preserving the old file and dirty editor state on failure. Both package surfaces use bilingual non-blocking failure status.

六個檔嘅 `codex-temp-powertoys` stash 已逐一對帳，冇 apply 或 drop。有用 coordinator、schema 相容、solution 註冊同 PowerToys discoverability 已喺較新 code；格式壞咗或者過時 hunk 已拒絕。可攜清單而家先同資料夾暫存，再原子交換成功先報成功；失敗會保留舊檔同未儲存狀態，兩個介面用雙語非阻塞狀態回報。

The Package Manager now exposes semantic headings and polite live status, honors all three persisted language modes in manager labels, and uses narrow-safe action rows. Package tests pass **28/28**, the exact solution build has **0 errors**, final publish/XAML/source audits pass, and a fresh inspected **1049×646** LowLevel headless capture replaced both canonical screenshots (SHA-256 `AE5A0CB21847BD907FE3011CE3E68AFB5965C41E35149FD8DBECBBB4B3AC0414`). Feature commit `06d876bd6c4512f5ad9eaeb0e7895728c5e69f17` is pushed and remotely proved on the bounded integration branch; the preserved stash remains unchanged for the coordinating agent.

套件管理器新增語意 heading、polite live status、三種持久語言模式管理器名稱，同窄畫面安全動作分行。專項 **28/28**、solution 零 errors、publish／XAML／source audit 全過；新 LowLevel **1049×646** 圖已檢視並替換兩份正式截圖。功能 commit 已 push 同 remote prove；stash 原封不動保留畀統籌 agent。

## Command Palette extension hosts · Command Palette extension host

Command Palette extension packs now have an explicit trusted-local-host option alongside declarative module, URL, copy, and structured-page commands. Every invocation rereads current enablement and the manifest, rechecks the declared command, accepts only a fully qualified local-drive `.exe`, rejects relative/UNC/network/device paths and unsafe arguments, verifies the pinned SHA-256 under a read-only launch lease, refuses elevated execution, and bounds JSON-lines responses to 64 KiB and eight seconds. Cancellation and timeout kill the owned process tree. This is a deliberate local trust boundary, not a sandbox.

Command Palette extension pack 而家除咗 declarative module／URL／copy／structured-page command，仲可以明確選擇受信任本機 host。每次執行都會重讀最新 enablement／manifest，再核對宣告 command；只接受完整本機 drive `.exe`，拒絕 relative／UNC／network／device path 同危險 argument；喺唯讀 launch lease 下驗證指定 SHA-256；elevated 狀態一律拒絕；JSON-lines response 上限 64 KiB／八秒。取消或 timeout 會終止自家 process tree。呢個係刻意設計嘅本機信任界線，唔係 sandbox。

The palette remains responsive during host work, exposes cancellable progress and bilingual feedback, and renders structured pages with associated accessible names, live-region notices, bounded fields, one clear primary action, 44-pixel targets, narrow-safe stacking, and language changes that preserve only the active page's values. The focused harness passes **17/17**; the managed project builds with **0 errors**; XAML literal safety and the detailed source-surface audit pass. The self-contained `cmdpalette` deep-link launch passes, but desktop capture is honestly blocked because `CopyFromScreen` is unavailable, `PrintWindow` returns a blank WinUI client, and LowLevel MCP is not callable in this session; no screenshot was promoted.

Host 工作期間 palette 仍然暢順，有可取消進度同雙語提示；structured page 有關聯 accessible name、live-region 通知、有限欄位、一個清楚 primary action、44-pixel target、窄畫面直排，轉語言亦只保留當前頁值。專項 harness **17/17**、managed project 零 errors、XAML safety 同詳細 source-surface audit 全過。Self-contained `cmdpalette` deep-link launch 通過，但 `CopyFromScreen` 不可用、`PrintWindow` 只回傳空白 WinUI client，而且今次 session 冇可呼叫 LowLevel MCP，所以 capture 如實受阻，冇升格截圖。
## Reactor industrial loads · 反應堆工業負載

The managed app now has two more reactor-bus consumers: an ammonia/fertilizer Haber–Bosch plant (`ammonia`, `fertilizer`, `fertiliser`) and an eight-feeder strict-priority load-shed dispatcher (`loadshed`, `mwbudget`). The plant's 280 MW default can reach its approximately 263 MW steady synthesis threshold; the dispatcher reports cold-bus enabled demand as shed without inventing a trip. Both services reject non-finite inputs and keep duplicate ticks from advancing accumulated state.

正式 app 新增兩個反應堆母線負載：合成氨／肥料哈柏法工廠，同八饋線嚴格優先級卸載調度器。合成氨預設 280 MW 可以到約 263 MW 穩定門檻；卸載器會如實顯示冷母線啟用需求已卸載，但唔會虛構跳脫。兩個 service 都會拒絕非有限輸入，重複 tick 亦唔會推進累積狀態。

Windows x64 build completed with zero errors and the production-source reactor harness passed **65/65**. Literal safety and source-surface audits passed. Generated documentation reports 321 module pages, 1,905 actionable-control pages, 1,216 app features, and 2,278 Pages wiki records. Both deep links opened on a dedicated LowLevel headless desktop, but the inspected 1574×887 WinUI captures were solid black; the repository driver also rejected a blank fallback and switching the desktop visible was denied. No invalid image is published, so visual evidence remains `capture-blocked`. Full behavior, failure, accessibility, and safety details are in [Reactor Industrial Loads](wiki/Reactor-Industrial-Loads.md).

Windows x64 build 零 errors、正式 source reactor harness **65/65**、literal safety 同 source audit 全過；生成文件係 321 module／1,905 actionable control／1,216 app feature／2,278 Pages wiki。兩個 deep link 都喺專用 LowLevel desktop 成功開啟，但已檢視 WinUI capture 全黑；repo driver 亦拒絕空白 fallback，而切換 desktop 就被拒絕。冇無效圖片會發佈，所以 visual 如實係 `capture-blocked`。完整行為、故障、無障礙同安全資料喺 [反應堆工業負載](wiki/Reactor-Industrial-Loads.md)。

Delivery commits `cf7b28b8` and `593b89d1` are pushed on `codex/integrate-claude-remaining`; fetch plus `ls-remote` proved exact remote tip `593b89d13bef8d8444faf8f3367269023a6e0e9c`. This integration branch is deliberately left unmerged for the parent owner. · 交付 commit 已 push，fetch 同 `ls-remote` 證明 remote tip 完全一致；branch 按分工保留未 merge，交畀上層負責人整合。

## AWS Manager EC2 continuation · AWS Manager EC2 延續開發

The managed AWS Manager now has a native, bilingual EC2 workspace alongside S3 and Resource Explorer. It supports direct `ec2` and `s3` routes, paged instance discovery, local filtering, details, and guarded Start, Stop, Reboot, and Terminate reviews through AWSSDK.EC2. Context generations, operation ownership, stale-result rejection, post-dialog revalidation, fail-closed state policy, and atomic S3 `If-None-Match: *` uploads keep account and mutation boundaries explicit; discovered credentials remain metadata-only.

正式 AWS Manager 而家喺 S3／Resource Explorer 旁邊加入原生雙語 EC2 工作區，支援 `ec2`／`s3` 直達 route、分頁 instance 清單、本機篩選、詳情，同受保護嘅 Start／Stop／Reboot／Terminate confirmation。Context generation、operation ownership、過期結果拒絕、dialog 後重驗、fail-closed state policy，同 S3 `If-None-Match: *` 原子 upload，會清楚守住 account 同 mutation 界線；credential discovery 只保留 metadata。

Local gates are green: the solution builds with zero errors; all **27/27** Release test projects pass, including AWS **11/11** and Reactor **63/63**; XAML literal safety passes; and generated documentation reports 319 modules, 1,214 features, and 1,902 button references. The inspected credential-safe `2077×1302` direct-EC2 screenshot is mirrored in tracked docs. LowLevel MCP was present but not callable, so the owned self-contained driver launch used the app's debug `RenderTargetBitmap` capture after desktop capture and blank `PrintWindow` fallbacks failed; no live AWS query or mutation occurred. Feature `ea7238d7` was merged as `84220d29` and is an ancestor of remote `main` tip `17fb451c`; all branch, merge, site-data, exact-tip release, and Pages workflows passed. Stable GitHub Latest [`v1.1.264`](https://github.com/Ding-Ding-Projects/WinForge/releases/tag/v1.1.264) targets exact `17fb451c` with only the managed installer and portable ZIP, and managed Wiki `ab16d14` publishes the updated guide and matching image.

本機 gate 全綠：solution 零 errors、Release test project **27/27**（AWS **11/11**、Reactor **63/63**）、XAML safety 同生成文件全部通過；tracked docs 已同步經檢視、credential-safe 嘅 `2077×1302` EC2 截圖。LowLevel MCP 雖然喺 disk，但今次唔可呼叫；desktop capture 同空白 `PrintWindow` fallback 失敗後，改用 owned self-contained driver launch 加 app debug `RenderTargetBitmap`，全程冇查詢或改動真實 AWS。功能 `ea7238d7` 以 `84220d29` merge，並係 remote `main` tip `17fb451c` 祖先；branch、merge、site-data、exact-tip release 同 Pages workflow 全綠。stable GitHub Latest [`v1.1.264`](https://github.com/Ding-Ding-Projects/WinForge/releases/tag/v1.1.264) 準確指向 `17fb451c`，只有 managed installer 同 portable ZIP；managed Wiki `ab16d14` 亦已發佈更新指南同一致圖片。

## What remains here · 呢度保留乜

- Managed application source and tests · 正式 app source 同 tests
- Managed installer, updater, portable-package, and release contract · 正式 installer、updater、portable package 同 release 合約
- Bilingual documentation, wiki, generated feature references, and Pages content · 雙語文件、wiki、自動產生功能 reference 同 Pages 內容
- Small C++ companion apps below `native/`, built and launched by managed WinForge · `native/` 下由正式 WinForge 建置同啟動嘅細型 C++ companion app

## Validation · 驗證

The managed compile gate is `dotnet build WinForge.sln -c Debug -p:Platform=x64` with zero errors. The reactor/dependent-service gate is `dotnet run --project tests/ReactorSim.Tests -c Debug`, currently 65/65 with nonzero exit on failure. Use the repository's managed-only run driver for a process-owned deep-link launch or screenshot.

正式編譯 gate 係指定 `dotnet build` command 同零 errors；反應堆／依賴服務 gate 係指定 `ReactorSim.Tests` command，目前 65/65，失敗時非零退出。直接開頁或者截圖要用 repo 嘅 managed-only driver，只控制自己開嘅 process。

## Repository split proof · Repository 分拆證明

The managed split feature `fe791aa6167dbe26dc358df3a31acce51bd0f931` was merged and remotely proved. Before this completion record, the expected site-data refresh advanced the proved integration tip to `be054aa737df860b1185bd7b1102d8dd9e80ae8e`; [managed CI](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715701032), [site data](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715516151), and [Pages](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715705513) passed. [`v1.1.259`](https://github.com/Ding-Ding-Projects/WinForge/releases/tag/v1.1.259) was stable GitHub Latest at that exact integration tip with only the managed setup and portable ZIP; every later docs-only `main` commit remains subject to the same workflow.

Managed 分拆功能已 merge 同做完 remote proof；呢段完成記錄之前，預期 site-data 將已驗證 integration tip 更新到 `be054aa7`，CI、site-data 同 Pages 全綠。`v1.1.259` 喺該 integration tip 係 stable GitHub Latest，只得 managed setup 同 portable ZIP；之後 docs-only `main` commit 仍然要跑同一 workflow。

Standalone native `main` is `a64e8e30ed8b5fe376197448ba760d1374244c69`; its [CI/release](https://github.com/codingmachineedge/WinForge-Native/actions/runs/29715120945), [Pages](https://codingmachineedge.github.io/WinForge-Native/), and [`native-v1.1.7`](https://github.com/codingmachineedge/WinForge-Native/releases/tag/native-v1.1.7) are green. Managed Wiki commit `be2571545ee81b9286f36a8a96aa72fdc92769b2` is live. GitHub has not initialized the native Wiki and no authenticated browser or supported Wiki API was available, so native tracked docs and Pages are the published documentation.

獨立原生 `main` `a64e8e30` 嘅 CI／release／Pages 全綠，managed Wiki 亦已上線。GitHub 尚未初始化原生 Wiki，而今次冇已登入 browser／支援 API，所以原生 tracked docs 同 Pages 就係正式發佈文件。

### Legacy native checkout retirement · 舊原生 checkout 退役

Before retiring the listed legacy C++/WinRT worktrees and refs, their exact provenance is retained in the standalone-native closure history. Date, Duration, and Loan snapshots remain pushed on their dedicated WIP refs and are not represented as native-main integration. The managed default tree stays rewrite-free, apart from the AudioForge and ImageForge companion programs. Unrelated dirty PowerToys and Reactor/Dew work is preserved outside this cleanup.

退役已列明嘅舊 C++/WinRT worktree／ref 之前，會先喺獨立 native closure history 保留 exact provenance。Date、Duration、Loan snapshot 繼續喺各自已 push 嘅 WIP ref，唔會當成已整合 native main。managed default tree 除咗 AudioForge／ImageForge companion program 外繼續冇 rewrite；唔相關嘅 dirty PowerToys 同 Reactor／Dew 工作唔會喺今次清理郁到。

See the repository [handoff summary](https://github.com/codingmachineedge/WinForge/blob/main/handoff-summary.md) for final commit, remote-integration, workflow, release, and cleanup proof.
