# WinForge Handoff Reference · WinForge 交接參考

WinForge is the canonical .NET 11 / WinUI 3 application. For the current task state, validation contract, and Git completion record, see [`handoff-summary.md`](../handoff-summary.md).

WinForge 係正式 .NET 11／WinUI 3 app。目前任務狀態、驗證合約同 Git 完成記錄請睇 [`handoff-summary.md`](../handoff-summary.md)。

## 2026-07-24 audio and safe-capture hardening · 音訊同安全截圖修實

The Volume Mixer COM boundary is nullable-clean and fail-closed, system-default routing clears the per-app override explicitly, invalid process IDs stop before COM activation, and interface aliases sharing one RCW release exactly once through their owner. Narrow device controls stack, while icon/slider accessibility names and 44–48 px targets remain present. The DEBUG-only shell capture accepts only bounded PNG paths on fixed/removable local drives, composites against the root's actual theme, and flushes a unique same-directory partial image. The driver removes stale output, restores its capture environment, validates the owned image first, and sends every live-tree or `PrintWindow` result through same-directory write-through atomic promotion; no path writes directly to the requested filename and no target path enters persistent app logs. It never reads raw desktop pixels. · Volume Mixer COM 邊界已清理 nullable 同 fail-closed；系統預設路由會明確清除逐 app override；無效 process ID 喺 COM activation 前停止，而共用同一 RCW 嘅 interface alias 只經 owner release 一次。窄畫面控制會直排，圖示／slider 繼續有無障礙名稱同 44–48 px target。DEBUG-only shell capture 只接受 fixed／removable 本機 drive 上有限 PNG 路徑，按 root 實際 theme 合成並 flush 同目錄唯一 partial 圖。Driver 會移除舊 output、還原 capture environment、先驗證自家圖片；所有 live-tree／`PrintWindow` 結果都經同目錄 write-through 原子升格，冇路徑直接寫要求檔名，persistent app log 亦冇 target path。佢永遠唔讀原始 desktop pixels。

The final solution build completes with **0 warnings and 0 errors**; focused audio interop and capture-policy/source-contract harnesses pass **6/6** and **9/9**, PowerShell 5.1 parsing, XAML literal safety, and the full source-surface audit pass. Fresh app-owned LowLevel captures at **1264×791**, **784×691**, and canonical **1284×811**, independent fresh-HWND frames at **1280×800**, **800×700**, and **1300×820**, and an atomic-driver capture were inspected with no clipping, overlap, stale/foreign pixels, or retained process/temp file. The expected no-endpoint state remains honest; no audio/session data was invented. · 最終 solution build **0 warning／0 error**；專項 audio interop／capture-policy／source-contract harness **6/6**、**9/9**，PowerShell 5.1 parser、XAML literal safety 同完整 source audit 全過。已檢視 app-owned LowLevel **1264×791**、**784×691**、正式 **1284×811**，獨立 fresh-HWND **1280×800**、**800×700**、**1300×820**，同 atomic-driver 圖，冇裁切、重疊、舊／其他視窗 pixels，亦冇殘留 process／temp file。預期 no-endpoint 狀態繼續如實顯示，冇虛構 audio／session 資料。

The repository-wide runner is not claimed green: it stopped at the unchanged `ScreenRecorderLifecycle.Tests` fixture when its synthetic recorder reported “not saved”, and an isolated rerun reproduced that unrelated base failure. The recorder service, lifecycle seam, and fixture have no review-lane diff. · 唔會聲稱 full repo runner 全綠：佢去到未改過嘅 `ScreenRecorderLifecycle.Tests` fixture 時，synthetic recorder 回報「not saved」而停止，獨立重跑亦重現呢個無關 base failure。Recorder service、lifecycle seam 同 fixture 喺今次 review lane 完全冇 diff。

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
