# Smoke Launch Batch 12 · 第十二批冒煙啟動

## Scope · 範圍

**EN —** This isolated evidence slice covers launchable inventory indices 275–299:
Text Sort, Text Statistics, Text Template, Text Tools, Text Wrap, TimeLens, Timer,
Time & Unit, TOML ⇄ JSON, Torrent, TOTP, Time-zone Planner, ULID, Unicode Inspector,
Uninstall, Unit Converter, Unit Price, Unix Permissions, URL Tools, UUID v5, UUID v7,
Vault Volumes, Vertical Farm, ViaProxy, and Video Conference Mute.

**粵語 —** 呢個隔離證據範圍覆蓋 inventory 可啟動 indices 275–299：文字排序、文字
統計、文字範本、文字工具、文字換行、TimeLens、計時器、時間與單位、TOML ⇄ JSON、
Torrent、TOTP、時區規劃、ULID、Unicode 檢查、解除安裝、單位換算、單位價格、Unix
權限、URL 工具、UUID v5、UUID v7、Vault Volumes、垂直農場、ViaProxy 同視像會議靜音。

## Evidence · 證據

- The generated inventory records 323 routes, 805 aliases, 1,294 source-review files,
  350,182 source lines, and zero routing issues or unmapped aliases.
- The repository-local `Invoke-WinForgeRouteSmoke.ps1` ran indices 275–299 with a
  5-second launch wait and bounded 15-second retry. All **25/25** passed on the first
  attempt; there were no retries or failures.
- Source review found and repaired a Timer page-boundary lifecycle defect: an unload
  could stop dispatcher timers while leaving the stopwatch and running flags active.
  `tests/TimerLifecycle.Tests` passes **3/3**, proving idempotent named language
  subscriptions, truthful paused state at unload, and snapshot refresh after reload.
- The XAML literal-safety guard passed. `dotnet build WinForge.sln -c Debug
  -p:Platform=x64` completed with **0 errors**.

**Safety disposition · 安全處置：** This is launch/static evidence, not permission to
perform live actions. Torrent networking or downloads, TOTP/clipboard use, uninstall
operations, disk/volume actions, permission changes, proxy or conference-mute controls,
and writes to user files or settings were not live-executed. · 呢個係啟動／靜態證據，唔
代表有權做 live actions。Torrent 網絡／下載、TOTP／剪貼簿使用、解除安裝、磁碟／
volume actions、權限變更、proxy 或視像會議靜音控制，同埋寫入使用者檔案／設定都冇
live-run。

## Visual status · 視覺狀態

The fresh self-contained `driver.ps1 -Page textsort -Out
artifacts/smoke/batch12/screenshots/textsort-default.png -WaitMs 5000` attempt reached
the WinForge window, but `CopyFromScreen` was unavailable. Its `PrintWindow` fallback
produced a uniform frame, so the driver stopped with `graphics capture is unavailable
in this desktop session`. No PNG was produced, inspected, replaced, or reused. Batch
12 is **capture-blocked**, never visual-pass. · 新嘅 self-contained `textsort` capture
有去到 WinForge 視窗，但 `CopyFromScreen` 唔可用；`PrintWindow` fallback 產生
uniform frame，driver 因為呢個 desktop session graphics capture 唔可用而停止。冇 PNG
產生、檢查、替換或者重用。Batch 12 係 **capture-blocked**，絕對唔係 visual-pass。

Raw, ignored evidence is under `artifacts/smoke/batch12/` in the task worktree.
