# WinForge Exhaustive Smoke Campaign · WinForge 全面冒煙驗證

**Status · 狀態：** Baseline active · 基線已啟動

**EN —** This page records the repeatable evidence model for whole-app WinForge verification. It is deliberately a coverage ledger, not a marketing feature count: a route is complete only when its applicable routing, build, test, launch, visual, behavior, side-effect, and documentation evidence is recorded.

**粵語 —** 呢一頁記錄點樣可以重複做到成個 WinForge app 驗證。佢係涵蓋證據清單，唔係宣傳用功能數字：每條 route 只有喺適用嘅 routing、build、test、launch、visual、behavior、副作用同文件證據都記錄好先算完成。

## Baseline Snapshot · 基線快照

Generated on 2026-07-11 from the live source:

| Coverage surface · 涵蓋範圍 | Baseline count · 基線數量 |
| --- | ---: |
| Registered/map/navigation route records · 已登記／對映／導航 routes | 321 |
| Deep-link aliases · 深層連結別名 | 785 |
| Companion specifications · Companion 規格 | 4 |
| External-app launcher specifications · 外部 app launcher 規格 | 15 |
| First-party source files in review queue · source files 審查佇列 | 1,243 |
| First-party source lines in review queue · source lines 審查佇列 | 334,812 |
| Test projects · 測試專案 | 3 |
| Wiki pages · Wiki 頁面 | 2,192 |

**EN —** Counts are regenerated when registry, navigation, pages, or source files change; they are a point-in-time audit snapshot rather than a permanent product claim.

**粵語 —** registry、導航、頁面或者 source files 有變就要重新產生數量；佢只係某一刻嘅審查快照，唔係永久產品聲稱。

## Reproduce the Inventory · 重做盤點

Run the repository-local skill and its extractor from the repository root:

~~~powershell
powershell -ExecutionPolicy Bypass -File .agents\skills\winforge-exhaustive-smoke\scripts\New-WinForgeSmokeInventory.ps1 -RepoRoot . -OutputDirectory artifacts\smoke\<campaign-id>
~~~

The command creates manifest.json, manifest.csv, and summary.md. Artifacts are
ignored by Git until selected evidence is intentionally promoted to docs.

## Shell Dialog Route · Shell 對話框 route

`shell.allapps` is the one tagged navigation route that intentionally renders
the modal **Open new tab / 開新分頁** picker instead of a Frame page. Launch it
with `WinForge.exe --page shell.allapps` (or the route driver using the same
alias), then verify automation ID `NewTabPickerDialog` and a current dialog
screenshot. Do not treat the Dashboard visible behind the modal as evidence
that the route fell back successfully.

`shell.allapps` 係唯一有 tag 嘅導航 route，刻意開 modal「**開新分頁／Open new
tab**」選擇器，而唔係 Frame 頁面。用 `WinForge.exe --page shell.allapps`
（或者用同一個 alias 嘅 route driver）開佢，之後要驗證 automation ID
`NewTabPickerDialog` 同最新對話框截圖；唔可以將 modal 後面見到嘅 Dashboard
當成 route 成功 fallback 證據。

After a self-contained publish, run the focused safe regression check:

~~~powershell
powershell -ExecutionPolicy Bypass -File .agents\skills\winforge-exhaustive-smoke\scripts\Test-WinForgeShellAllAppsRoute.ps1 -RepoRoot .
~~~

The check only verifies the dialog, its search box, and the selected navigation
item; it does not open a module or invoke a side effect. On 2026-07-11, this
check passed. The driver launch also reached a dedicated WinForge window, but
the current desktop session blocked its screenshot at `CopyFromScreen` with
`The handle is invalid`; therefore no visual-pass claim or replacement image
is recorded for this dialog yet.

self-contained publish 後，請跑呢個安全嘅 focused regression check：

~~~powershell
powershell -ExecutionPolicy Bypass -File .agents\skills\winforge-exhaustive-smoke\scripts\Test-WinForgeShellAllAppsRoute.ps1 -RepoRoot .
~~~

個 check 只會驗證對話框、搜尋框同已選取嘅導航項目；唔會開任何模組，亦唔會觸發
副作用。喺 2026-07-11 呢個 check 已經通過。driver launch 都開到獨立嘅 WinForge
視窗，但而家 desktop session 喺 `CopyFromScreen` 報 `The handle is invalid`，所以暫時
唔會聲稱 visual pass，亦未有可替換嘅對話框圖片。

## Evidence Rules · 證據規則

- **EN —** Build success is compilation evidence only. Keep static routing,
  focused tests, launch, inspected screenshots, behavior, side-effect safety,
  and documentation evidence separate.
  **粵語 —** build 成功只代表編譯到。static routing、focused tests、launch、
  已檢查截圖、behavior、副作用安全同文件證據要分開。
- **EN —** A screenshot failure is logged as capture-blocked; it is never a
  visual pass. Current, inspected screenshots replace stale documentation
  images when a visual surface changes.
  **粵語 —** 截圖失敗要記做 capture-blocked，唔可以當 visual pass。視覺介面
  有改時，要用已檢查嘅最新截圖換走過時文件圖片。
- **EN —** Privileged, destructive, networked, package, credential, system
  tweak, and real-world integration actions use fixtures, dry-runs, validation
  paths, or reversible probes unless live execution is explicitly authorized.
  **粵語 —** 有權限、破壞性、網絡、套件、認證、系統調校同真實世界整合操作，
  除非明確批准真實執行，否則用 fixtures、dry-runs、validation paths 或者可還原
  probes。
- **EN —** Changed methods and meaningful branches must map to a focused test,
  safe manual exercise, or an explicit exclusion.
  **粵語 —** 有改嘅 methods 同重要 branches 必須對應 focused test、安全手動
  操作或者明確排除理由。

## Baseline Verification · 基線驗證

- Repository-local WinForge Exhaustive Smoke skill structure: valid.
- Inventory extractor: completed successfully with the snapshot above.
- Full Debug x64 solution build with the local .NET 11 SDK: passed with 0 errors; the current compiler reported 318 warnings.
- ReactorSim headless scenarios: 63/63 passed with the installed net8 runtime.
- Package-manager core tests: 21/21 passed with the local .NET 11 SDK.
- Companion build-log tests: 4/4 passed with the installed net8 runtime.
- Launch-only route-smoke pilot: Dashboard and Nuclear Reactor reached dedicated launched windows, 2/2 passed. This proves process-level route launch only, not visual or behavioral completion.
- Dashboard capture is currently capture-blocked in this desktop session: the self-contained publish completed, then CopyFromScreen returned The handle is invalid. A passive Windows.Graphics.Capture retry also failed with IGraphicsCaptureItemInterop.CreateForMonitor error 0x80070057. No visual-pass result is claimed.
- No WinForge visual surface changed while this verification infrastructure was
  introduced, so this baseline claims no new visual-pass result.

[← Wiki Home](Home.md) · [Developer](Developer.md) · [Screenshots](Screenshots.md)
