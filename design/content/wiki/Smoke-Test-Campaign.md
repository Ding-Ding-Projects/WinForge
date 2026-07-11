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
| First-party source files in review queue · source files 審查佇列 | 1,256 |
| First-party source lines in review queue · source lines 審查佇列 | 341,934 |
| Test projects · 測試專案 | 7 |
| Wiki pages · Wiki 頁面 | 2,217 |

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

## Launch-only Batch 01 · 第一批淨啟動測試

**EN —** On 2026-07-11, manifest route records 0–24 were launched one at a
time through the isolated self-contained driver with a 2.5-second wait. All
25 routable records returned `launch-pass`; there were no launch failures.
At that point, `shell.allapps` was a NavigationView group tag without a
deep-link alias, so it was recorded as `not-launchable` rather than as a pass
or failure. That routing defect is now fixed and the **Shell Dialog Route**
section records its focused safe UI Automation evidence. The generated batch
evidence is retained under the ignored
`artifacts/smoke/launch-batches/batch-01/` directory.

**粵語 —** 2026-07-11 用獨立 self-contained driver，逐個以 2.5 秒等待時間
開 manifest route records 0–24。25 條可以 deep-link 嘅 records 全部係
`launch-pass`，冇 launch failure。嗰陣 `shell.allapps` 亦有見到，但佢係未有
deep-link alias 嘅 NavigationView 分組 tag，所以記做 `not-launchable`，唔當 pass
或 failure。呢個 routing defect 而家已經修正，**Shell Dialog Route** 段落有記低
focused、安全嘅 UI Automation 證據。產生嘅 batch 證據保留喺 Git 忽略嘅
`artifacts/smoke/launch-batches/batch-01/`。

- The route runner now keeps going when an individual child launch fails and
  records that outcome in the ledger; native-command error promotion can no
  longer abort an entire batch before later routes are exercised.
- This is process-level launch evidence only. Screenshots remain
  capture-blocked in this session, so Batch 01 grants neither visual nor
  behavioral completion.

## Launch-only Batch 02 · 第二批淨啟動測試

**EN —** Manifest launchable-route indices 25–49 were exercised on
2026-07-11. The first 2.5-second isolated sweep reached 22 routes directly.
`module.bulkops` and `module.cementkiln` subsequently reached dedicated
windows in their focused follow-up run, so they have no reproducible route
defect. The runner now records a bounded longer retry after a nonzero child
exit, including both waits and exit codes in its JSON/CSV ledger instead of
silently turning a slow first render into a false result.

`module.baseconvert` continued to fail after the 15-second retry. Its exact
crash-log reproduction was a `XamlParseException` while assigning
`NumberBox.Value` from XAML. The numeric defaults were moved to managed
initialization under the existing suppression guard. A full Debug x64 solution
build then passed with 0 errors (318 warnings), and a forced fresh
self-contained `--page baseconvert` launch passed with a dedicated process.
The generated first sweep and focused retry evidence remain in ignored
`artifacts/smoke/launch-batches/batch-02*/` directories.

**粵語 —** 2026-07-11 測咗 manifest 入面 launchable-route indices 25–49。第一輪
每條 2.5 秒嘅獨立 sweep 有 22 條直接開到。`module.bulkops` 同
`module.cementkiln` 喺 focused follow-up 裏面都開到自己嘅視窗，所以冇可重現嘅
route defect。runner 而家喺 child exit 非零嗰陣會記錄一個有限時間、較長嘅 retry，
並將兩次 wait 同 exit code 寫入 JSON/CSV ledger，唔會將慢嘅第一次 render 靜靜地
誤當成結果。

`module.baseconvert` 喺 15 秒 retry 後仍然失敗。確實 crash-log 重現係由 XAML
設定 `NumberBox.Value` 引起嘅 `XamlParseException`。數字預設值已搬去既有
suppression guard 裏面嘅 managed initialization。之後完整 Debug x64 solution
build 以 0 errors 通過（318 warnings），強制新 self-contained
`--page baseconvert` launch 亦以獨立 process 通過。第一輪 sweep 同 focused retry
證據會保留喺 Git 忽略嘅 `artifacts/smoke/launch-batches/batch-02*/` 目錄。

- Screenshot capture remains blocked in this desktop session; no visual-pass
  or replacement screenshot is claimed for the unchanged Base Converter
  layout.
- Routing-review diagnostics now use ASCII-safe separators, so evidence files
  stay parseable across PowerShell host encodings.

## Launch-only Batch 03 · 第三批淨啟動測試

**EN —** On 2026-07-11, manifest launchable-route indices 50–74 were run
through the 5-second/15-second isolated protocol. Twenty-four routes reached
their dedicated windows. `module.csvjson` failed both attempts; the exact
crash log was a `XamlParseException` assigning `ToggleSwitch.IsOn` from XAML.
The default “first row is header” state was moved into guarded managed
initialization, preserving behavior without relying on the failing XAML
conversion. A fresh self-contained `--page csvjson` launch and the focused
runner retest both passed, bringing the batch to 25/25 launch-pass routes.

**粵語 —** 2026-07-11 用 5 秒／15 秒嘅獨立 protocol 跑咗 manifest
launchable-route indices 50–74。有 24 條開到自己嘅視窗。`module.csvjson`
兩次都失敗；確實 crash log 係由 XAML 設定 `ToggleSwitch.IsOn` 引起嘅
`XamlParseException`。預設「第一行係標題」狀態已搬去有 guard 嘅 managed
initialization，保留原本行為但唔再依賴失敗嘅 XAML conversion。新嘅
self-contained `--page csvjson` launch 同 focused runner retest 都通過，令呢批
變成 25/25 launch-pass routes。

- No screenshot is claimed: the desktop capture environment remains blocked,
  so the retest supplies launch evidence rather than visual evidence.

## AI Chat DPAPI persistence regression · AI Chat DPAPI 持久化回歸

**EN —** The source review found that a DPAPI protect or unprotect failure could
previously collapse an existing provider API key to an empty string on a later
save. `AiProviderPersistence` now receives an injectable secret protector. An
unreadable `dpapi:` blob is retained verbatim while unrelated provider fields
are edited; a failed protect aborts the complete provider-file write; and a
user-entered non-empty replacement is the only path that replaces an unreadable
key. `AiChatPersistence.Tests` exercised these branches with a fake protector:
**5/5 passed** on 2026-07-11. A full Debug x64 solution build also passed with
0 errors (318 warnings).

**粵語 —** source review 發現，以前 DPAPI protect 或 unprotect 失敗之後，下一次
save 有機會將已有嘅供應商 API 金鑰變成空字串。`AiProviderPersistence` 而家會收一個
可以注入嘅 secret protector。讀唔到嘅 `dpapi:` blob 喺改其他供應商欄位嗰陣會原封不動
保留；protect 失敗會取消成份供應商檔案寫入；而只有人手輸入嘅非空白替代金鑰先可以取代
讀唔到嘅金鑰。`AiChatPersistence.Tests` 用 fake protector 跑過呢啲 branches：2026-07-11
**5/5 通過**。完整 Debug x64 solution build 都以 0 errors 通過（318 warnings）。

- **Visual evidence · 視覺證據：** The warning/error is a failure-only AI Chat
  state, so no real credential or DPAPI failure was induced. A safe capture
  attempt published the current self-contained build, then
  `driver.ps1 -Page aichat -WaitMs 8000` stopped before capture because no
  dedicated WinForge window appeared; an existing instance may have intercepted
  the launch. Therefore this state is `capture-blocked`, no screenshot was
  created or replaced, and no visual-pass claim is made.
- **Safety disposition · 安全處置：** The regression test uses an isolated
  temporary provider file and fake protector only. It never writes a real API
  key or invokes a provider network request.
- **Launch confirmation · 啟動確認：** Merge validation later ran the freshly
  published build with `driver.ps1 -Page aichat -NoCapture -WaitMs 15000` and
  received `OK launch-only`. That is route-launch evidence only; it does not
  replace the capture-blocked visual state above. 合併驗證其後用同一條命令跑新發佈
  build 並收到 `OK launch-only`；只係 route-launch 證據，唔會取代上面
  capture-blocked 嘅視覺狀態。

## Launch-only Batch 04 · 第四批淨啟動測試

**EN —** On 2026-07-11, launchable-route indices 75–99 were exercised using
the isolated 5-second/15-second protocol. All 25 routes returned
`launch-pass` on the initial attempt; no retry, failure, or manual-routing
entry remained. The generated evidence is retained under the ignored
`artifacts/smoke/launch-batches/batch-04/` directory. This brings the
campaign’s current route-launch evidence to the first 100 of 321 manifest
routes; it remains launch-only, not visual or behavioral completion.

**粵語 —** 2026-07-11 用獨立 5 秒／15 秒 protocol 測咗 launchable-route
indices 75–99。25 條全部喺第一次就 `launch-pass`；冇 retry、failure 或者
manual-routing entry 留低。產生嘅證據保留喺 Git 忽略嘅
`artifacts/smoke/launch-batches/batch-04/` 目錄。依家 campaign 對 321 條
manifest routes 入面頭 100 條有 current route-launch 證據；佢仍然只係 launch，
唔係 visual 或 behavioral completion。

## Reactor Harness Exit-Code Gate · 反應堆測試框架退出碼閘門

**EN —** On 2026-07-11 the focused `ReactorSim.Tests` console harness was changed from a reporting-only summary to a CI gate. It now prints the existing per-scenario result and summary, returns **0 only for a complete pass**, and returns **1** if any scenario fails or throws. The fast `--verify-exit-code-contract` mode checks the all-pass and partial-failure mappings without running simulator scenarios.

**粵語 —** 2026-07-11 專注嘅 `ReactorSim.Tests` console 測試框架由淨係報告結果，改成 CI gate。佢而家會保留原本每個情景嘅結果同總結，**完全通過**先會回傳 **0**；任何情景失敗或者拋出例外就會回傳 **1**。快速嘅 `--verify-exit-code-contract` 模式會唔跑模擬器情景、驗證全部通過同部分失敗嘅 mapping。

- Visual evidence: not applicable. This task changes a headless console harness and documentation only; no WinUI page or canonical screenshot changed, and no screenshot replacement is claimed.
- **Standalone linkage · 獨立連結：** Merge validation found that this linked-source
  harness also needs `SettingsStorePersistence.cs` whenever it compiles
  `SettingsStore.cs`; the project now links both files, and the exit-contract
  test plus the complete **63/63** suite pass again. 合併驗證亦發現呢個
  linked-source harness 編譯 `SettingsStore.cs` 時需要一齊連結
  `SettingsStorePersistence.cs`；而家兩個檔都已連結，exit-contract 測試同完整
  **63/63** 套件再次通過。

[← Wiki Home](Home.md) · [Developer](Developer.md) · [Screenshots](Screenshots.md)
