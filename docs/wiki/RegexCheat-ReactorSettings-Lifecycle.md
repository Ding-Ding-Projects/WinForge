# Regex Cheatsheet & Reactor Settings Lifecycle · 正則速查同反應堆設定生命週期

**Repository scope · 儲存庫範圍：** This page records the canonical .NET Regex Cheatsheet and Reactor Settings fixes. C++/WinRT port evidence now lives in [WinForge-Native](https://github.com/codingmachineedge/WinForge-Native). · 呢頁記錄正式 .NET Regex Cheatsheet 同 Reactor Settings 修正；C++/WinRT 移植證據而家放喺 [WinForge-Native](https://github.com/codingmachineedge/WinForge-Native)。

## What changed · 改咗乜

**EN —** Regex Cheatsheet now documents `(?>a*)`, the valid .NET atomic equivalent of possessive `a*`, instead of the unsupported `*+` syntax. Reactor Settings keeps one named live-API timer callback per page instance and balances its named language handler across every load/unload cycle. Every Reactor page now shares one canonical simulation: the newest visible control room is the sole driver, parallel pages are live read-only observers whose gauges/status keep rendering while plant controls are disabled, and a minimal no-page UI-thread loop advances only physics, auto-start progression, the mission clock, and the truthful status API.

**粵語 —** Regex Cheatsheet 而家文件寫 `(?>a*)`，即係 .NET 入面佔有 `a*` 嘅有效原子等價寫法，唔再寫唔支援嘅 `*+` 語法。Reactor Settings 每個 page instance 只會有一個具名 live-API timer callback，並會喺每次 load/unload 間平衡具名語言 handler。全部反應堆頁而家共用一個正式模擬：最新可見控制室係唯一 driver；並行頁面會保持即時唯讀觀察，儀錶／狀態繼續更新但機組控制會停用；冇頁面時，最小化 UI-thread loop 只會推進物理、自動起動進度、任務時鐘同如實狀態 API。

**EN —** Real-world effects stay visible-page-owned. Authority demotion closes mutating HTML/full-control-room, startup-checklist, and SCRAM-widget companions while read-only core/status/startup-gauge widgets may remain. A demoted meltdown page keeps a live overlay without ABORT/reset and collapses it when the authoritative driver resets shared state, preventing stale overlay state on later promotion. Closing the last Reactor page disarms/reset real shutdown, restores Home Assistant/Awake/SystemLink, and stops audio while the safe background simulation/API continue. An off-page ARM request is consumed only after the destination finishes loading and a low-priority UI callback can expose ABORT. The deadline and accepted/refused OS result are session-global and truthful, but a foreground page/window handoff automatically aborts an active countdown before authority changes; OS acceptance hides ABORT because it can no longer cancel the accepted request. Command-line auto-start is latched once per app session.

**粵語 —** 真實世界效果繼續只由可見頁面持有。Authority 降級會關閉可改動模擬嘅 HTML／完整控制室、起動 checklist 同 SCRAM widget companion，但唯讀爐心／狀態／起動儀錶 widget 可以留低。降級頁面嘅熔毀 overlay 會保持即時顯示但冇 ABORT／重設，authoritative driver 重設共用狀態時會收起，避免之後升級時殘留舊 overlay。關閉最後一個反應堆頁面會解除／重設真實關機、還原 Home Assistant／Awake／SystemLink 同停止音效，而安全背景模擬／API 繼續。離頁 ARM 要求只會喺目的地完成載入兼低優先次序 UI callback 可以顯示 ABORT 後消耗。Deadline 同 OS 接受／拒絕結果係如實嘅全 session 狀態，但前景頁面／視窗一交接，就會喺 authority 改變之前自動中止進行中倒數；OS 接受後會收起 ABORT，因為已經唔可以取消獲接受嘅要求。命令列自動起動每個 app session 只 latch 一次。

## Safe verification · 安全驗證

```powershell
dotnet run --project tests/RegexCheatService.Tests -c Debug
dotnet run --project tests/ReactorSettingsLifecycle.Tests -c Debug
```

**EN —** Regex Cheatsheet passed **3/3** and Reactor Settings lifecycle passed **10/10**. They prove catalog/parser correctness plus live read-only observers, plant-input lockout, mutating-companion cleanup, safe observer-meltdown-overlay synchronization, session ownership, post-load ARM ordering, foreground-handoff abort, truthful shutdown outcomes, last-owner cleanup, and once-per-session auto-start without starting Home Assistant, changing Windows linkage, keeping the PC awake, or requesting a real shutdown. The Debug x64 solution build passed with 0 errors, and the XAML literal-safety guard passed.

**粵語 —** Regex Cheatsheet **3/3**、反應堆設定生命週期 **10/10** 通過。佢哋會證明 catalog／parser 正確，加埋即時唯讀觀察、機組輸入鎖定、可改動 companion 清理、安全同步觀察頁熔毀 overlay、session ownership、載入後 ARM 次序、前景交接中止、如實關機結果、最後 owner 清理同每 session 一次自動起動，而唔會啟動 Home Assistant、改 Windows 連動、保持電腦喚醒或者提出真實關機要求。Debug x64 solution build 以 0 errors 通過，XAML literal-safety guard 都通過。

## Launch and screenshots · 啟動同截圖

**EN —** The earlier managed 15-second capture attempts for `regexcheat` and `reactorsettings` remain `capture-blocked`: `CopyFromScreen` was unavailable, the `PrintWindow` fallback was uniform, and graphics capture was unavailable. No PNG was created or reused; both routes subsequently passed launch-only checks without operating controls. The old Reactor Settings image was removed rather than claimed as current evidence.

**粵語 —** `regexcheat` 同 `reactorsettings` 新鮮 15 秒截圖嘗試都係 `capture-blocked`：`CopyFromScreen` 唔可用、`PrintWindow` fallback 係 uniform，而 graphics capture 亦唔可用。冇 PNG 產生或者重用；兩條 route 之後喺冇操作控制項下都通過 launch-only check。舊 Reactor Settings 圖片已移除，唔會當成最新證據。

**2026-07-27 follow-up · 2026-07-27 跟進：** The replacement headless-only driver and callable LowLevel path subsequently produced and verified fresh real-build Reactor Settings captures for the default-off, enabled-empty, starting, and running states. They used dedicated non-input desktops, opened no visible terminal, stole no focus, and are published in the [screenshot gallery](Screenshots.md). The older failed attempts above remain historical evidence for that earlier capture environment, not the current Reactor Settings disposition. · 之後新版只限 headless driver 同可呼叫 LowLevel 路徑成功由真實 build 擷取並檢視反應堆設定預設關閉、已容許空缸、啟動中同運行狀態；全部用專用非輸入 desktop，冇可見終端、冇搶焦點，並已放入[截圖集](Screenshots.md)。上面舊失敗嘗試只係當時 capture 環境嘅歷史證據，唔係而家反應堆設定嘅處置。

[← Developer](Developer.md) · [Reactor Safety & Integrations](Reactor-Safety-and-Integrations.md) · [Smoke Campaign](Smoke-Test-Campaign.md)
