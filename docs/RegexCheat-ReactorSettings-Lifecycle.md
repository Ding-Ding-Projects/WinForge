# Regex Cheatsheet & Reactor Settings Lifecycle Repair · 正則速查同反應堆設定生命週期修復

## Scope · 範圍

**EN —** This repair is deliberately narrow: it corrects a .NET-regex documentation claim and the lifetime of two UI event subscriptions. It does not change reactor physics, system linkage, Home Assistant configuration or I/O, any toggle default, status-API policy, autosave, keep-awake behavior, or the real-shutdown path.

**粵語 —** 呢次修正刻意保持細範圍：只會更正一個 .NET 正則文件聲稱，同埋兩個 UI event subscription 嘅生命週期。佢唔會改反應堆物理、系統連動、Home Assistant 設定或者 I/O、任何開關預設、status-API 政策、自動儲存、保持喚醒行為或者真實關機路徑。

## Correctness changes · 正確性變更

**EN —** The Regex Cheatsheet previously described `*+` as a .NET possessive quantifier. .NET does not support that syntax, so the entry now shows `(?>a*)`: an atomic zero-or-more group that is the .NET equivalent for a possessive `a*`. The broader atomic-group reference remains available too.

**粵語 —** Regex Cheatsheet 之前將 `*+` 描述成 .NET 佔有量詞。.NET 唔支援呢個語法，所以條目而家顯示 `(?>a*)`：一個原子式零次或以上群組，係 .NET 裏面佔有 `a*` 嘅等價寫法。較通用嘅原子群組參考亦保留咗。

**EN —** Reactor Settings now attaches its `DispatcherTimer` callback once, by the named `OnLiveTimerTick` handler in the constructor. `OnLoaded` uses an idempotent named language subscription; `OnUnloaded` stops the timer and releases that subscription. Repeated load/unload cycles therefore cannot accumulate live-API refresh callbacks or leave a reused page without localized rerendering.

**粵語 —** Reactor Settings 而家只會喺 constructor 用具名 `OnLiveTimerTick` handler 掛一次 `DispatcherTimer` callback。`OnLoaded` 會用 idempotent 具名語言訂閱；`OnUnloaded` 會停 timer 同解除嗰個訂閱。所以重複 load/unload 唔會累積 live-API refresh callbacks，亦唔會令重用嘅頁面失去本地化 rerender。

## Focused verification · 專注驗證

```powershell
dotnet run --project tests/RegexCheatService.Tests -c Debug
dotnet run --project tests/ReactorSettingsLifecycle.Tests -c Debug
dotnet build WinForge.sln -c Debug -p:Platform=x64
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Users\Administrator\.codex\skills\winforge-exhaustive-smoke\scripts\Test-WinForgeXamlLiteralSafety.ps1 -RepoRoot .
```

**EN —** On 2026-07-11, each focused harness passed **3/3**. The Debug x64 solution build passed with **0 errors** (318 existing warnings), and the XAML literal-safety guard passed. The lifecycle test is source-level/headless, so it never starts the reactor or an external integration.

**粵語 —** 喺 2026-07-11，兩個專注 harness 各自 **3/3** 通過。Debug x64 solution build 以 **0 errors**（318 個既有 warnings）通過，XAML literal-safety guard 亦通過。生命週期測試係 source-level／headless，所以絕對唔會啟動反應堆或者外部整合。

## Route and capture evidence · Route 同截圖證據

**EN —** Fresh self-contained 15-second `driver.ps1 -Out` attempts reached both `regexcheat` and `reactorsettings`, but this desktop session is `capture-blocked`: `CopyFromScreen` was unavailable, the `PrintWindow` fallback produced a uniform frame, and graphics capture was unavailable. No PNG was created, inspected, replaced, or reused. Both routes subsequently passed `-NoCapture` launch-only checks without operating a control. The old Reactor Settings image was removed rather than presented as current visual evidence.

**粵語 —** 新鮮 self-contained 15 秒 `driver.ps1 -Out` 嘗試都開到 `regexcheat` 同 `reactorsettings`，但呢個 desktop session 係 `capture-blocked`：`CopyFromScreen` 唔可用、`PrintWindow` fallback 產生 uniform frame，而 graphics capture 亦唔可用。冇 PNG 產生、檢查、替換或者重用。兩個 route 之後都以 `-NoCapture` 通過 launch-only check，過程冇操作任何控制項。舊嘅 Reactor Settings 圖片已移除，唔會當成最新視覺證據展示。

See [the smoke campaign](wiki/Smoke-Test-Campaign.md) for the cross-page evidence ledger. · 跨頁證據清單請睇[冒煙測試](wiki/Smoke-Test-Campaign.md)。
