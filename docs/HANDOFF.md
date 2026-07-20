# WinForge Handoff Reference · WinForge 交接參考

WinForge is the canonical .NET 11 / WinUI 3 application. For the current task state, validation contract, and Git completion record, see [`handoff-summary.md`](../handoff-summary.md).

WinForge 係正式 .NET 11／WinUI 3 app。目前任務狀態、驗證合約同 Git 完成記錄請睇 [`handoff-summary.md`](../handoff-summary.md)。

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

- Managed feature `fe791aa6167dbe26dc358df3a31acce51bd0f931` merged as `165477c4461c6bd33e30d3856ec076f638193e10`; the expected generated-data commit advanced remotely proven `main` to `be054aa737df860b1185bd7b1102d8dd9e80ae8e`. · Managed 分拆功能已 merge；預期 site-data commit 將已驗證 remote `main` 更新到 `be054aa7`。
- [Managed run 29715701032](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715701032), [site-data run 29715516151](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715516151), and [Pages run 29715705513](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715705513) passed. [`v1.1.259`](https://github.com/Ding-Ding-Projects/WinForge/releases/tag/v1.1.259) is stable Latest at exact current `main`, with only `WinForge-Setup.exe` and the matching managed portable ZIP. · Managed CI、site-data 同 Pages 全綠；`v1.1.259` 係準確 current-main SHA 嘅 stable Latest，只得 managed setup 同 portable ZIP。
- Standalone native `main` is `a64e8e30ed8b5fe376197448ba760d1374244c69`; [native run 29715120945](https://github.com/codingmachineedge/WinForge-Native/actions/runs/29715120945) and [Pages run 29715120958](https://github.com/codingmachineedge/WinForge-Native/actions/runs/29715120958) passed, and [`native-v1.1.7`](https://github.com/codingmachineedge/WinForge-Native/releases/tag/native-v1.1.7) is its stable Latest. · 獨立原生 `main`、CI、Pages 同 stable Latest 已準確驗證。
- Managed Wiki `be2571545ee81b9286f36a8a96aa72fdc92769b2` is pushed and live. The native GitHub Wiki remains uninitialized because no authenticated browser or supported Wiki API was available; [native Pages](https://codingmachineedge.github.io/WinForge-Native/) and tracked Markdown are live instead. · Managed Wiki 已 push；原生 GitHub Wiki 因未有已登入 browser／支援 API 而未初始化，改由 native Pages 同 tracked Markdown 上線。
- No managed UI changed, so no canonical screenshot was replaced. Only clean ancestry-proven task refs/worktrees were removed; dirty, unique, or exact-tip-divergent pre-existing work remains preserved. · 冇 managed UI 改動，所以毋須換截圖；只清理已證明合併嘅 task refs／worktrees，其餘 dirty／獨特／tip 未合併工作全部保留。
