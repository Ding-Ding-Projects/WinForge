# Hardware Monitor Driver Lifecycle · 硬件監測器驅動生命週期

**Status · 狀態:** remediated in source and covered by a driver-free lifecycle harness on 2026-07-11. · 已經完成來源修正，2026-07-11 亦有無驅動生命週期 harness 覆蓋。

## Finding · 發現

**EN —** After the Batch 08 `monitor` smoke launch, the manually loaded `R0WinForge` LibreHardwareMonitor driver service was still running and its `ImagePath` pointed into the temporary self-contained publish directory that the route driver then removed. Source review traced that ownership gap to `SystemMonitor`: it created a static `LibreHardwareMonitor.Hardware.Computer`, called `Open()`, and never called `Close()`.

**粵語 —** Batch 08 跑過 `monitor` smoke launch 之後，手動載入嘅 `R0WinForge` LibreHardwareMonitor 驅動 service 仍然運行，而佢個 `ImagePath` 指向 route driver 稍後刪除嘅自包 publish 暫存目錄。來源審查追到 `SystemMonitor`：佢建立 static `LibreHardwareMonitor.Hardware.Computer`、呼叫 `Open()`，但從未呼叫 `Close()`。

## Fixed ownership model · 修好嘅所屬模式

**EN —** System Monitor and Battery & Thermal now lease one process-shared LibreHardwareMonitor `Computer`. The first lease opens the superset of their sensor categories. Releasing one page cannot tear down the upstream Ring0 transport while the other page is still sampling; releasing the final lease calls that exact `Computer.Close()`. A failed `Open()` closes its candidate immediately. Normal window close, tray quit, and `AppDomain.ProcessExit` all provide a best-effort final `Shutdown()` fallback.

**粵語 —** 系統監察同電池與散熱而家共用一個 process-shared LibreHardwareMonitor `Computer` lease。第一個 lease 會開佢哋需要嘅 sensor category 集合。一個頁面釋放唔會喺另一個仲用緊嘅頁面停掉 upstream Ring0 transport；最後一個 lease 釋放先會對嗰個確實由 WinForge 打開嘅 `Computer` 呼叫 `Close()`。`Open()` 失敗時候選實例會即時 close。正常關窗、tray 退出同 `AppDomain.ProcessExit` 都有 best-effort 最後 `Shutdown()` 作後備。

## Safety boundary · 安全邊界

**EN —** The fix is deliberately limited to LibreHardwareMonitor's own `Computer.Close()` path for the in-process object WinForge opened. It does not enumerate, stop, delete, alter, or otherwise clean up Windows services. A stale or externally created driver service is not a normal-runtime cleanup target and must be investigated separately; WinForge will not remove it globally.

**粵語 —** 修正只限於對 WinForge 程式內親自打開個 object 用 LibreHardwareMonitor 原生 `Computer.Close()` 路徑。唔會 enumerate、stop、delete、更改或者全域清理 Windows service。過期或外部建立嘅 driver service 不係正常 runtime 清理目標，需要另行調查；WinForge 絕不會全局刪佢。

## Regression evidence · 回歸證據

- `tests/HardwareMonitorLifecycle.Tests` passed **4/4**: overlapping System Monitor/Battery leases share one driver until final release; app shutdown closes once and invalidates stale leases; failed opens close their partial candidate and can retry; disposal is idempotent. · `tests/HardwareMonitorLifecycle.Tests` **4/4** 通過：同時嘅 System Monitor/Battery lease 共用一個 driver 直至最後釋放；app shutdown 只 close 一次並令過期 lease 無效；失敗開啟會 close 不完整 candidate 且可 retry；dispose 係 idempotent。
- `dotnet build WinForge.sln -c Debug -p:Platform=x64` completed with **0 errors**. · `dotnet build WinForge.sln -c Debug -p:Platform=x64` 以 **0 errors** 完成。
- This is a nonvisual lifecycle change. No competing WinForge GUI launch or real driver load was performed while Batch 09 was sweeping routes, so no new screenshot was created, replaced, or claimed as visual evidence. · 呢個係非視覺生命週期修正。Batch 09 逐條 route sweep 進行期間刻意冇開另一個 WinForge GUI 或載入真 driver，所以冇新 screenshot 產生、替換或聲稱為 visual evidence。
