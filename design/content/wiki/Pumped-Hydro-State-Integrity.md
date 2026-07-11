# Pumped-Hydro State Integrity · 抽水蓄能狀態完整性

**Status · 狀態:** Remediated and covered by a focused, headless deterministic harness on 2026-07-11. · 已完成修正，並由專注、headless、確定性嘅測試框架覆蓋。

## Finding · 發現

**EN —** Batch 09 source review found two page-lifecycle paths that could advance the Pumped-Storage Hydro simulation without a timer event: `Render()` called the state-step method, so a language refresh could charge/discharge the reservoir and mint currency; `OnLoaded` called both `Render()` and the state-step method, causing two immediate advances. The generation reward also mixed MWh with kWh and multiplied the intended economy mint by 1,000.

**粵語 —** Batch 09 source review 發現抽水蓄能頁面有兩條唔使 timer event 都會推進模擬嘅生命週期路徑：`Render()` 會叫狀態步進方法，所以轉語言都可能令水塘充／放電兼鑄幣；`OnLoaded` 又叫 `Render()` 又叫狀態步進，結果一開頁就推進兩次。發電獎勵亦將 MWh 同 kWh 混埋，令原本嘅經濟鑄幣多咗 1,000 倍。

## Deterministic state boundary · 確定性狀態邊界

**EN —** `OnLoaded` now initializes controls, subscribes the language handler, snapshots reactor display data, renders, and starts the timer once; it never ticks the hydro service. Only the loaded-guarded `OnTick` path calls `AdvanceSimulation()`, which applies one fixed `0.5`-second service tick and then renders. `Render()`, `RenderText()`, language refresh, mode/pump/auto input, and reset redraw from existing state without progressing stored MWh or earnings. `Unloaded` stops the timer and removes the language handler; a later load subscribes once again.

**粵語 —** `OnLoaded` 而家只會初始化控制項、訂閱語言 handler、讀 reactor 顯示快照、render，同埋開始一次 timer；佢唔會 tick hydro service。只有有 `_isLoaded` guard 嘅 `OnTick` 會叫 `AdvanceSimulation()`；嗰度先會套用一次固定 `0.5` 秒 service tick，之後先 render。`Render()`、`RenderText()`、轉語言、mode／pump／auto 輸入同重設只會由既有狀態重畫，唔會推進已儲存 MWh 或收入。`Unloaded` 會停 timer 同移除語言 handler；之後再 load 只會再訂閱一次。

## Reward units · 獎勵單位

**EN —** `ReactorEconomyService.MintPerMWSecond` is `0.00001 ⚡/(MWe·s)`. One delivered MWh is `3,600 MW·s`, so the correct rate is `0.036 ⚡/MWh`. `PumpedHydroService.WattsFromDeliveredMWh(deliveredMWh)` now owns that conversion and returns the delivered-energy mint after the generate-leg loss. The old extra `×1000` kWh conversion is removed.

**粵語 —** `ReactorEconomyService.MintPerMWSecond` 係 `0.00001 ⚡/(MWe·s)`。一個已送出嘅 MWh 係 `3,600 MW·s`，所以正確比率係 `0.036 ⚡/MWh`。`PumpedHydroService.WattsFromDeliveredMWh(deliveredMWh)` 而家集中處理呢個轉換，並喺發電 leg 損耗之後按送出能量計鑄幣。舊有多餘嘅 `×1000` kWh 轉換已移除。

## Regression evidence · 回歸證據

- `dotnet run --project tests/PumpedHydroService.Tests -c Debug` passed **4/4**: `explicit ticks alone advance stored energy`; `charge and discharge use the documented split round-trip efficiency`; `generation mints from delivered MWh without a kWh multiplier`; and `page loading and rendering are observational; only the guarded timer advances`. The harness links the pure service and checks page-source lifecycle boundaries with local doubles; it does not load the user wallet or settings. · `dotnet run --project tests/PumpedHydroService.Tests -c Debug` **4/4** 通過：`explicit ticks alone advance stored energy`、`charge and discharge use the documented split round-trip efficiency`、`generation mints from delivered MWh without a kWh multiplier` 同 `page loading and rendering are observational; only the guarded timer advances`。框架會 link 純 service，並用本機 double 檢查頁面 source 生命週期邊界；唔會載入使用者 wallet 或 settings。
- `dotnet build WinForge.sln -c Debug -p:Platform=x64` completed with **0 errors**. · `dotnet build WinForge.sln -c Debug -p:Platform=x64` 以 **0 errors** 完成。

## Visual/capture status · 視覺／截圖狀態

**EN —** This repair changes service and code-behind state ownership only; no XAML layout or visible control surface changed. While Batch 09 was sweeping routes, no competing WinForge GUI was launched and no screenshot was attempted, created, replaced, reused, or claimed as visual verification. Screenshot replacement is not applicable to this nonvisual repair.

**粵語 —** 呢次修正只改 service 同 code-behind 嘅狀態所屬；冇改 XAML 排版或者可見控制介面。Batch 09 跑 route sweep 期間冇開另一個 WinForge GUI，亦冇嘗試、產生、替換、重用或者聲稱任何 screenshot 係視覺驗證。呢個非視覺修正唔適用截圖替換。

[← Wiki Home](Home.md) · [Pumped-Storage Hydro](features/reactor-loads/pumpedhydro.md) · [Smoke Test Campaign](Smoke-Test-Campaign.md)
