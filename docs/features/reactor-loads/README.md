# Reactor-powered industrial loads · 反應堆供電工業負載

**EN —** This category documents the simulated reactor's live electrical loads and the optional local feature-power fallback. These are local simulation features: they do not switch real utility equipment, contact an external market, or weaken reactor protection.

**粵語 —** 呢個分類記錄模擬反應堆嘅即時電力負載，同可選嘅本機功能電源後備。佢哋只係本機模擬功能，唔會控制真實電網設備、聯絡外部市場，亦唔會削弱反應堆保護。

## Features · 功能

- [Ammonia / Fertilizer Plant](ammonia-fertilizer-plant.md) · [合成氨／肥料廠](ammonia-fertilizer-plant.md)
- [Grid Load-Shed Dispatcher](grid-load-shed-dispatcher.md) · [電網卸載調度器](grid-load-shed-dispatcher.md)
- [Optional feature-bus emergency diesel](optional-feature-bus-edg.md) · [可選功能匯流排應急柴油](optional-feature-bus-edg.md)

## Shared safety contract · 共通安全合約

- Nuclear-only loads read `ReactorStatusApiService.I.LastSnapshot` directly; a cold, tripped, scrammed, melted, or non-generating bus supplies them zero usable power. Eligible playful gates use the separate nuclear-preferred feature-power resolver. · 只限核電嘅負載會直接讀取 `ReactorStatusApiService.I.LastSnapshot`；冷停、跳脫、SCRAM、熔毀或者冇發電時，可用功率係零。合資格玩味閘門就用另一條核電優先功能電源 resolver。
- Emergency-diesel fallback is default-off and limited to nine playful feature gates. Its permission persists, but generator state, the 60 L simulated tank, and module-instance leases are session-only. Each app launch requires a stopped-state manual fill and a fresh 10-second start; the EDG burns 1.0 L/min while starting or running. Nuclear remains preferred. The other 19 reactor-industrial simulations remain nuclear-only. · 應急柴油後備預設關閉，只限九個玩味功能閘門；容許權限會保存，但發電機狀態、60 L 模擬油缸同模組 instance lease 只限今次 session。每次開 app 都要趁停機重新入滿油兼等 10 秒啟動，而啟動中或者運行中每分鐘耗油 1.0 L。核電仍然優先，其餘 19 個反應堆工業模擬繼續只用核電。
- The EDG's 250 MWe is a per-module threshold. At most two concurrently open module tabs/instances may hold owner-token EDG leases; a third waits until one closes, navigates away, or otherwise releases its slot. Cake Factory consumes its leased source continuously and loses power when the EDG stops or exhausts its fuel. · EDG 嘅 250 MWe 係逐模組門檻；同時最多兩個已開模組分頁／instance 可以持有 owner-token EDG lease，第三個要等其中一個關閉、轉去其他頁或者釋放個位。蛋糕工廠會持續消耗已租用電源，EDG 停機或者燒光油就會斷電。
- Parallel Reactor pages remain live read-only observers: shared gauges and status keep rendering while all plant controls and companion launchers are disabled. Authority demotion closes mutating HTML/full-control-room, startup-checklist, and SCRAM-widget companions while read-only widgets may remain, and any live real-shutdown countdown is aborted before handoff. A demoted meltdown page keeps its live overlay without ABORT/reset and auto-collapses it when the authoritative driver resets the plant. · 並行開住嘅其他反應堆頁會保持即時唯讀：共用儀錶同狀態繼續更新，但全部機組控制同 companion 啟動掣會停用。Authority 降級會關閉可改動模擬嘅 HTML／完整控制室、起動 checklist 同 SCRAM widget companion；唯讀 widget 可以留低，而任何進行中嘅真實關機倒數都會喺交接前中止。降級頁面嘅熔毀 overlay 會保持即時顯示但冇 ABORT／重設，authoritative driver 重設機組時會自動收起。
- The canonical reactor simulation and truthful status API continue on a minimal UI-thread loop after the last Reactor control room closes. Page-owned audio and real-world effects do not: real-shutdown ARM/countdown is cancelled, Home Assistant entities are restored off, keep-awake is released, and Windows settings are restored. Cake Factory remains loaded on exact-owner source loss and freezes or locks powered machinery and progress—including CIP—without discarding plant state; passive biological change, milk warming/spoilage risk, transport, and order clocks continue. · 最後一個反應堆控制室關閉後，正式反應堆模擬同如實狀態 API 會喺精簡 UI-thread loop 繼續；頁面擁有嘅音效同真實世界效果就唔會繼續：真實關機 ARM／倒數會取消、Home Assistant 實體還原為關、保持喚醒會釋放、Windows 設定會還原。蛋糕工廠喺準確 owner 失去電源時會保持載入，並鎖住或凍結包括 CIP 在內嘅有電機器同進度，而唔會丟棄廠房狀態；被動生物變化、牛奶升溫／變壞風險、運輸同訂單時鐘就會繼續。
- All state stays inside WinForge unless an existing, explicitly selected economy action applies. No physical actuator or network control is added. · 狀態留喺 WinForge 內；除咗既有而且由使用者明確揀嘅經濟操作，唔會加任何實體致動器或者網絡控制。
- Duplicate integer ticks do not advance accumulated production, energy, or anti-flap timers. · 重複整數 tick 唔會推進累積產量、能量或者防拍翼計時。
- Inputs are bounded and non-finite reactor values fail closed to zero. · 輸入有界限；非有限反應堆數值會 fail closed 當零處理。

## Verification · 驗證

The production-service harness is `dotnet run --project tests/ReactorSim.Tests -c Debug`; the current Windows contract is **67/67**. The safe source-contract harness `dotnet run --project tests/ReactorSettingsLifecycle.Tests -c Debug` is **10/10**, covering the canonical background session, live read-only parallel observers, mutating-companion cleanup and safe meltdown-overlay synchronization on authority demotion, page-owned real-effect cleanup, post-load visible ARM, foreground-handoff countdown abort, truthful session-global shutdown outcomes, and once-per-session command-line auto-start. The solution compile gate is `dotnet build WinForge.sln -c Debug -p:Platform=x64` with zero errors. Visual capture for the 2026-07-24 industrial-load integration was blocked by solid-black WinUI frames in the available headless desktop; launch, functional, source, and accessibility evidence are recorded separately in [the handoff](../../../handoff-summary.md).

正式 service harness 係上述 `ReactorSim.Tests` command，現時 Windows 合約係 **67/67**；安全原始碼合約 harness `ReactorSettingsLifecycle.Tests` 亦 **10/10**，覆蓋正式背景 session、並行頁面即時唯讀觀察、authority 降級時清理可改動 companion 同安全同步熔毀 overlay、頁面持有真實效果清理、載入後可見 ARM、前景交接中止倒數、如實全 session 關機結果，同每 session 一次命令列自動起動。Solution 編譯 gate 必須零 errors。2026-07-24 工業負載整合時，現有 headless desktop 只擷取到全黑 WinUI frame，所以視覺證據受阻；啟動、功能、source 同無障礙證據已分開記錄喺 [handoff](../../../handoff-summary.md)。
