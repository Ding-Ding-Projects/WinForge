# WinForge Handoff Reference · WinForge 交接參考

WinForge is the canonical .NET 11 / WinUI 3 application. For the current task state, validation contract, and Git completion record, see [`handoff-summary.md`](../handoff-summary.md).

WinForge 係正式 .NET 11／WinUI 3 app。目前任務狀態、驗證合約同 Git 完成記錄請睇 [`handoff-summary.md`](../handoff-summary.md)。

## 2026-07-24 audio and safe-capture hardening · 音訊同安全截圖修實

The Volume Mixer COM boundary is nullable-clean and fail-closed, system-default routing now clears the per-app override explicitly, narrow device controls stack, and icon/slider accessibility names and 44–48 px targets are present. A DEBUG-only shell capture service renders the real WinUI tree to a bounded absolute PNG request. The driver validates and promotes that owned image first, never reads raw desktop pixels, and may use only HWND-targeted `PrintWindow` as fallback. · Volume Mixer COM 邊界已清理 nullable 同 fail-closed；系統預設路由會明確清除逐 app override；窄畫面控制會直排，圖示／slider 有無障礙名稱同 44–48 px target。DEBUG-only shell capture service 會按有限絕對 PNG 要求輸出真實 WinUI tree；driver 先驗證同升格自家圖片，永遠唔讀原始 desktop pixels，後備只限針對 HWND 嘅 `PrintWindow`。

The managed publish completes with zero compiler warnings; XAML literal safety and the full source-surface audit pass. LowLevel headless captures at 1284×811 and 784×691 and a separate 1033×637 driver capture were inspected with no mixer clipping or overlap. The expected no-endpoint state is retained honestly in the refreshed canonical image; no audio/session data was invented. · Managed publish 零 compiler warning，XAML literal safety 同完整 source audit 全過。已檢視 LowLevel headless 1284×811、784×691，同獨立 driver 1033×637 圖，mixer 冇裁切或重疊。更新正式圖片如實保留預期 no-endpoint 狀態，冇虛構 audio／session 資料。

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
