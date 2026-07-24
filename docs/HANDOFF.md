# WinForge Handoff Reference · WinForge 交接參考

WinForge is the canonical .NET 11 / WinUI 3 application. For the current task state, validation contract, and Git completion record, see [`handoff-summary.md`](../handoff-summary.md).

WinForge 係正式 .NET 11／WinUI 3 app。目前任務狀態、驗證合約同 Git 完成記錄請睇 [`handoff-summary.md`](../handoff-summary.md)。

## Current preserved package-stash verification · 目前已保存套件 stash 驗證

- Preserved package commits `5cc3aa712f9e326dd8d9ae0bdd4c16d8771e1cb6` (ten files) and `181fc231c93b2533392344a405cb18750b4eaa48` (six files) were audited read-only. Neither stash was applied, popped, dropped, rewritten, or otherwise mutated. Useful scheduler/coordinator, schema, engine, bundle, source, PowerToys-discoverability, and provenance intent is retained in current native code; malformed/stale bodies plus obsolete external-launch, credential-in-URL, strip-only sanitization, and auto-trust paths remain rejected. · 兩份保留套件 commit 已逐檔只讀審核，兩個 stash 都冇 apply／pop／drop／rewrite／修改。有用排程／coordinator、schema、引擎、清單、來源、PowerToys discoverability 同來源證明已喺現行原生 code 保留；壞咗／過時 body、外部 launcher、URL 認證、只刪字元 sanitization 同自動信任路徑繼續拒絕。
- Bundle saves stage beside the target and atomically replace/move only after complete serialization. Failure is truthful, keeps any old file and dirty editor state, and uses bilingual non-blocking status. Proxy authority and vcpkg triplets now fail closed at persistence and command construction; proxy credentials never enter URLs, previews, or process arguments. · 清單先喺目的地旁暫存，完整序列化後先原子交換；失敗會如實回報、保留舊檔同未儲存狀態，再用雙語非阻塞狀態顯示。Proxy authority 同 vcpkg triplet 喺保存同建立指令兩邊 fail closed；proxy 認證永遠唔會進入 URL、預覽或者 process argument。
- Package actions/settings use narrow-safe rows, 44-pixel targets, semantic headings, polite live status, and language-mode-aware manager labels. The source lanes passed **29/29** and **28/28** independently; their merged contract passes **30/30**. Final combined build/publish, source/XAML audits, and LowLevel captures are recorded in the task handoff below. · 套件 action／settings 用窄畫面安全分行、44 像素 target、語意 heading、polite live status 同跟語言模式嘅管理器名稱；兩條來源線各自 **29/29**／**28/28**，合併 contract **30/30**。最終 combined build／publish、source／XAML audit 同 LowLevel capture 記錄喺下面 task handoff。

## Current Command Palette extension-host verification · 目前 Command Palette extension-host 驗證

- Trusted executable hosts complement declarative extension commands; they do not replace the safe declarative path and are explicitly documented as a local trust boundary rather than a sandbox. · 受信任 executable host 係 declarative extension command 嘅補充，唔會取代安全 declarative 路徑，文件亦清楚標明係本機信任界線，唔係 sandbox。
- Execution rereads current enablement/manifest state, revalidates the declared command, requires a hash-pinned fully qualified local-drive `.exe`, rejects relative/UNC/network/device paths and unsafe arguments, holds a read-only lease from hashing through launch, refuses elevation, and bounds time/output. Cancellation and timeout terminate the owned process tree. · 執行會重讀最新 enablement／manifest、再核對 command，只接受指定 hash 嘅完整本機 drive `.exe`，拒絕 relative／UNC／network／device path 同危險 argument，由 hash 到 launch 全程持有唯讀 lease，亦會拒絕 elevation 同限制時間／輸出；取消或 timeout 會終止自家 process tree。
- The focused harness passes **17/17**; the managed project build has **0 errors**; XAML literal safety and the detailed source-surface audit pass with no unresolved handlers/actions, subscription mismatches, or actionable markers. · 專項 harness **17/17**、managed project build 零 errors、XAML safety 同詳細 source-surface audit 全過，冇 unresolved handler／action、subscription mismatch 或 actionable marker。
- The self-contained `cmdpalette` launch-only fallback passes. LowLevel MCP is present on disk but not callable; `CopyFromScreen` is unavailable and `PrintWindow` returns a blank WinUI client, so no screenshot is promoted and visual status remains `capture-blocked`. · Self-contained `cmdpalette` launch-only fallback 通過；LowLevel MCP 喺 disk 但不可呼叫，`CopyFromScreen` 不可用、`PrintWindow` 回傳空白 WinUI client，所以冇升格截圖，視覺狀態保持 `capture-blocked`。

## Repository boundary · Repository 界線

- Managed app source, services, tests, installer/updater behavior, documentation, wiki, Pages, and managed releases stay here. · 正式 app source、service、tests、installer／updater 行為、文件、wiki、Pages 同 managed release 留喺呢度。
- The experimental C++20/C++/WinRT port now lives at [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native). · 實驗性 C++20/C++/WinRT 移植版而家喺 [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native)。
- The `native/` directory here contains managed-app companion executables and remains in scope for WinForge. · 呢度嘅 `native/` 目錄係正式 app companion executable，仍然屬於 WinForge。

## Build and verification · 建置同驗證

```powershell
dotnet build WinForge.sln -c Debug -p:Platform=x64
dotnet run --project tests\ReactorSim.Tests -c Debug
powershell -ExecutionPolicy Bypass -File .agents\skills\run-winforge\driver.ps1 `
  -Publish -Page dashboard -NoCapture
```

Use the categorized bilingual wiki from [`docs/wiki/Home.md`](wiki/Home.md) for module behavior, configuration, failure modes, security notes, and focused test evidence.

模組行為、設定、失敗模式、安全備註同專項測試證據，請由雙語分類 [`docs/wiki/Home.md`](wiki/Home.md)開始。

## Split completion proof · 分拆完成證明

- Managed feature `fe791aa6167dbe26dc358df3a31acce51bd0f931` merged as `165477c4461c6bd33e30d3856ec076f638193e10`; the expected generated-data commit advanced the remotely proved integration tip to `be054aa737df860b1185bd7b1102d8dd9e80ae8e` before this completion record. · Managed 分拆功能已 merge；呢段完成記錄之前，預期 site-data commit 將已做 remote proof 嘅 integration tip 更新到 `be054aa7`。
- [Managed run 29715701032](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715701032), [site-data run 29715516151](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715516151), and [Pages run 29715705513](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715705513) passed. [`v1.1.259`](https://github.com/Ding-Ding-Projects/WinForge/releases/tag/v1.1.259) was stable Latest at exact integration tip `be054aa7`, with only `WinForge-Setup.exe` and the matching managed portable ZIP; this later docs-only record remains subject to the same workflow. · Managed CI、site-data 同 Pages 全綠；`v1.1.259` 喺 integration tip `be054aa7` 係 stable Latest，只得 managed setup 同 portable ZIP，而之後呢段 docs-only 記錄仍然要跑同一 workflow。
- Standalone native `main` is `a64e8e30ed8b5fe376197448ba760d1374244c69`; [native run 29715120945](https://github.com/codingmachineedge/WinForge-Native/actions/runs/29715120945) and [Pages run 29715120958](https://github.com/codingmachineedge/WinForge-Native/actions/runs/29715120958) passed, and [`native-v1.1.7`](https://github.com/codingmachineedge/WinForge-Native/releases/tag/native-v1.1.7) is its stable Latest. · 獨立原生 `main`、CI、Pages 同 stable Latest 已準確驗證。
- Managed Wiki `be2571545ee81b9286f36a8a96aa72fdc92769b2` is pushed and live. The native GitHub Wiki remains uninitialized because no authenticated browser or supported Wiki API was available; [native Pages](https://codingmachineedge.github.io/WinForge-Native/) and tracked Markdown are live instead. · Managed Wiki 已 push；原生 GitHub Wiki 因未有已登入 browser／支援 API 而未初始化，改由 native Pages 同 tracked Markdown 上線。
- No managed UI changed, so no canonical screenshot was replaced. Only clean ancestry-proven task refs/worktrees were removed; dirty, unique, or exact-tip-divergent pre-existing work remains preserved. · 冇 managed UI 改動，所以毋須換截圖；只清理已證明合併嘅 task refs／worktrees，其餘 dirty／獨特／tip 未合併工作全部保留。
- Legacy C++/WinRT checkout retirement is destination-first: exact old refs are retained in standalone-native closure history, while Date/Duration/Loan stay on their pushed WIP refs and are not promoted into native main by that archival step. Managed main remains rewrite-free apart from its two companion C++ programs. · 舊 C++/WinRT checkout 會先喺獨立 native closure history 保留 exact ref；Date／Duration／Loan 保留喺已 push WIP ref，唔會因 archive 步驟升格做 native main feature。managed main 除咗兩個 companion C++ program 之外繼續冇 rewrite。
