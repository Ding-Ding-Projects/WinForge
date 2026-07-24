# Reactor-powered industrial loads · 反應堆供電工業負載

**EN —** These modules consume the simulated reactor's live electrical bus. They are local simulation features: they do not switch real utility equipment, contact an external market, or weaken reactor protection.

**粵語 —** 呢啲模組會用模擬反應堆嘅即時電力母線。佢哋只係本機模擬功能，唔會控制真實電網設備、聯絡外部市場，亦唔會削弱反應堆保護。

## Features · 功能

- [Ammonia / Fertilizer Plant](ammonia-fertilizer-plant.md) · [合成氨／肥料廠](ammonia-fertilizer-plant.md)
- [Grid Load-Shed Dispatcher](grid-load-shed-dispatcher.md) · [電網卸載調度器](grid-load-shed-dispatcher.md)

## Shared safety contract · 共通安全合約

- Work is gated by `ReactorStatusApiService.I.LastSnapshot`; a cold, tripped, scrammed, melted, or non-generating bus supplies zero usable power. · 工作由即時反應堆狀態閘門控制；冷停、跳脫、SCRAM、熔毀或者冇發電時，可用功率係零。
- All state stays inside WinForge unless an existing, explicitly selected economy action applies. No physical actuator or network control is added. · 狀態留喺 WinForge 內；除咗既有而且由使用者明確揀嘅經濟操作，唔會加任何實體致動器或者網絡控制。
- Duplicate integer ticks do not advance accumulated production, energy, or anti-flap timers. · 重複整數 tick 唔會推進累積產量、能量或者防拍翼計時。
- Inputs are bounded and non-finite reactor values fail closed to zero. · 輸入有界限；非有限反應堆數值會 fail closed 當零處理。

## Verification · 驗證

The production-service harness is `dotnet run --project tests/ReactorSim.Tests -c Debug`; the current Windows contract is **65/65**. The solution compile gate is `dotnet build WinForge.sln -c Debug -p:Platform=x64` with zero errors. Visual capture for the 2026-07-24 integration was blocked by solid-black WinUI frames in the available headless desktop; launch, functional, source, and accessibility evidence are recorded separately in [the handoff](../../../handoff-summary.md).

正式 service harness 係上述 `ReactorSim.Tests` command，現時 Windows 合約係 **65/65**；solution 編譯 gate 必須零 errors。2026-07-24 整合時，現有 headless desktop 只擷取到全黑 WinUI frame，所以視覺證據受阻；啟動、功能、source 同無障礙證據已分開記錄喺 [handoff](../../../handoff-summary.md)。
