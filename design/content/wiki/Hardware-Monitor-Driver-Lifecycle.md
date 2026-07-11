# Hardware Monitor Driver Lifecycle · 硬件監測器驅動生命週期

**EN —** Batch 08 exposed a concrete driver-lifecycle defect: the `monitor` route could leave the manually loaded `R0WinForge` LibreHardwareMonitor service running with an `ImagePath` in the deleted temporary publish tree. `SystemMonitor` opened a static `Computer` and never closed it.

**粵語 —** Batch 08 揭露咗一個具體驅動生命週期 defect：`monitor` route 可能留低手動載入嘅 `R0WinForge` LibreHardwareMonitor service 繼續運行，`ImagePath` 正好指向被刪嘅暫存 publish tree。`SystemMonitor` 開咗 static `Computer` 但冇 close 佢。

## Remediation · 修復

**EN —** System Monitor and Battery & Thermal now take reference-counted leases on one WinForge-owned `Computer`. The final page release calls that object's upstream `Computer.Close()`; failed opens close their candidate; normal window close, tray quit, and process exit provide a best-effort fallback. This prevents one monitoring page from tearing down the shared Ring0 transport while the other is still sampling.

**粵語 —** System Monitor 同 Battery & Thermal 而家向同一個 WinForge 所屬 `Computer` 攞 reference-counted lease。最後一個頁面釋放先呼叫嗰個 object 嘅 upstream `Computer.Close()`；失敗 open 會 close candidate；正常關窗、tray 退出同 process exit 有 best-effort 後備。亦防止一個監察頁面仲用緊時被另一個停掉 shared Ring0 transport。

## Scope and evidence · 範圍同證據

**EN —** This does not enumerate, stop, delete, or alter any Windows service. It only closes the in-process `Computer` WinForge opened; stale or external driver services are deliberately outside normal runtime cleanup. The driver-free `HardwareMonitorLifecycle.Tests` harness passed **4/4** and the Debug x64 solution build passed with **0 errors**. This was a nonvisual change: no competing GUI launch, real driver load, screenshot creation, replacement, or visual-pass claim was made while the Batch 09 route sweep was active.

**粵語 —** 呢個修正唔會 enumerate、stop、delete 或更改任何 Windows service。只會 close WinForge 程式內自己開啟嘅 `Computer`；過期或外部 driver service 有意排除咗喺正常 runtime cleanup 外。無真 driver 嘅 `HardwareMonitorLifecycle.Tests` harness **4/4** 通過，Debug x64 solution build 以 **0 errors** 通過。呢個係非視覺修正；Batch 09 route sweep 跑緊期間冇開另一個 GUI、載入真 driver、產生/替換 screenshot 或聲稱 visual-pass。

[Smoke Test Campaign](Smoke-Test-Campaign.md)
