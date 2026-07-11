# Reactor Test Report · 反應堆測試報告

**EN —** This GitHub Pages summary covers the headless verification harness for the real C# reactor engine and its fuel, waste, water, app-gating, and cake-factory dependencies. It compiles the production service sources directly; it does not use a mock reactor.

**粵語 —** 呢份 GitHub Pages 摘要涵蓋真實 C# 反應堆引擎，同燃料、廢料、水處理、app 閘門同蛋糕工廠相依服務嘅無介面驗證框架。佢直接編譯正式服務程式碼，唔係用假反應堆。

## Run and CI contract · 執行同 CI 規約

```powershell
# ReactorSim.Tests targets net8.0-windows.
dotnet run --project tests/ReactorSim.Tests -c Debug

# Fast deterministic check of the exit-code mapping only.
dotnet run --project tests/ReactorSim.Tests -c Debug -- --verify-exit-code-contract
```

**EN —** The normal run retains clear PASS/FAIL output for every scenario. It exits **0 only when every scenario passes** and exits **1** when any assertion fails or a scenario exception is caught. CI must fail the verification on every nonzero exit code.

**粵語 —** 正常運行會保留每個情景清楚嘅 PASS／FAIL 輸出。**全部情景通過**先會退出 **0**；任何斷言失敗或者捉到情景例外就會退出 **1**。CI 見到任何非零 exit code 都要當驗證失敗。

## Current verified result · 現時已驗證結果

**Latest verified run · 最新已驗證執行：** 2026-07-11

**63 / 63 scenarios PASS · 63／63 個情景全部通過**

The suite covers reactor physics and protection, all 16 accident enum values, fuel lifecycle, waste-cap and disk-floor controls, water treatment, reactor-dependent app gating, and the cake-factory dependency chain. The canonical report at `docs/wiki/Reactor-Test-Report.md` contains the measured reactor evidence and detailed coverage table.

**Visual evidence · 視覺證據 —** Not applicable / 不適用. This verification is a console harness and documentation surface only; no WinUI visual page changed and no screenshot replacement is claimed.

[← Reactor Hub · 反應堆總覽](Nuclear-Reactor.md) · [Developer · 開發者](Developer.md) · [Wiki Home · Wiki 主頁](Home.md)
