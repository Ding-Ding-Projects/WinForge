# WinForge Exhaustive Smoke Campaign · WinForge 全面冒煙驗證

**Status · 狀態：** Baseline active · 基線已啟動

**EN —** This page records the repeatable evidence model for whole-app WinForge verification. It is deliberately a coverage ledger, not a marketing feature count: a route is complete only when its applicable routing, build, test, launch, visual, behavior, side-effect, and documentation evidence is recorded.

**粵語 —** 呢一頁記錄點樣可以重複做到成個 WinForge app 驗證。佢係涵蓋證據清單，唔係宣傳用功能數字：每條 route 只有喺適用嘅 routing、build、test、launch、visual、behavior、副作用同文件證據都記錄好先算完成。

## Baseline Snapshot · 基線快照

Generated on 2026-07-11 from the live source:

| Coverage surface · 涵蓋範圍 | Baseline count · 基線數量 |
| --- | ---: |
| Registered/map/navigation route records · 已登記／對映／導航 routes | 322 |
| Deep-link aliases · 深層連結別名 | 800 |
| Companion specifications · Companion 規格 | 4 |
| External-app launcher specifications · 外部 app launcher 規格 | 15 |
| First-party source files in review queue · source files 審查佇列 | 1,264 |
| First-party source lines in review queue · source lines 審查佇列 | 343,904 |
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
- **2026-07-11 — YAML current-option regression:** a clean `origin/main`
  run proved the `\\n` assertion was intentional: a multiline pre-install
  command was flattened to a space by YAML serialization, so the current
  option did not round-trip. `BundleService` now emits and restores YAML
  double-quoted `\\n`, `\\r`, and `\\t` escapes; the complete package-manager
  core harness passes 21/21. This is a nonvisual serialization correction, so
  no package-manager screenshot was changed or claimed.
  **粵語 —** 乾淨嘅 `origin/main` run 證明 `\\n` 斷言本身係有意義：多行嘅
  pre-install 指令喺 YAML 序列化時被壓平做一個空格，所以 current option
  round-trip 唔完整。`BundleService` 而家會寫出同還原 YAML 雙引號嘅
  `\\n`、`\\r` 同 `\\t` escapes；完整套件管理核心 harness 21/21 通過。
  呢個係非視覺嘅序列化修正，所以冇更新亦冇聲稱套件管理截圖。
- Companion build-log tests: 4/4 passed with the installed net8 runtime.
- Launch-only route-smoke pilot: Dashboard and Nuclear Reactor reached dedicated launched windows, 2/2 passed. This proves process-level route launch only, not visual or behavioral completion.
- Dashboard capture is currently capture-blocked in this desktop session: a
  fresh self-contained `CopyFromScreen` attempt reproduced `The handle is
  invalid`. A prior passive monitor retry also failed with
  `IGraphicsCaptureItemInterop.CreateForMonitor` error `0x80070057`. No
  visual-pass result is claimed.
- A direct window-render fallback was re-probed with
  `PrintWindow(PW_RENDERFULLCONTENT)`: it returned success but the inspected
  682×1311 PNG was uniformly `ARGB #FF000000` across 3,198 sampled pixels.
  It is therefore not valid visual evidence. 呢個 desktop session 再試咗直接
  window-render fallback `PrintWindow(PW_RENDERFULLCONTENT)`：雖然回傳成功，
  但已檢查嘅 682×1311 PNG 喺 3,198 個抽樣像素都係同一隻
  `ARGB #FF000000`，同樣唔係有效嘅視覺證據。
- A minimal Windows.Graphics.Capture `CreateForWindow` probe compiled and
  created items for the launched WinForge HWND (668×1304) and a separate owned
  coloured WinForms diagnostic HWND (706×473). In both cases,
  `Direct3D11CaptureFramePool.CreateFreeThreaded` delivered no `FrameArrived`
  callback in 12 seconds, so it emitted no PNG. This proves a desktop-session
  frame-delivery block rather than a missing projection/runtime API; do not
  wire this unverified fallback into the driver. 最小化嘅
  Windows.Graphics.Capture `CreateForWindow` probe 可以編譯，亦為已開嘅
  WinForge HWND（668×1304）同另一個自有有色 WinForms 診斷 HWND（706×473）
  建立到 item；但兩個 `Direct3D11CaptureFramePool.CreateFreeThreaded` 喺 12 秒
  內都冇 `FrameArrived` callback，亦冇輸出 PNG。呢個證明係 desktop-session
  frame-delivery 阻礙，唔係 projection/runtime API 缺失；未驗證成功前唔好將呢個
  fallback 接入 driver。
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

## Launch-only Batch 05 · 第五批淨啟動測試

**EN —** On 2026-07-11, launchable-route indices 100–124 were exercised with
the isolated 5-second/15-second protocol. All 25 routes returned
`launch-pass` on their initial attempt; no retry, failure, or manual-routing
entry remained. The batch covered `envvars`, `epoch`, `evcharge`, `events`,
`everything`, `expensesplit`, `faker`, `fancyzones`, `fastboot`, `feedreader`,
`filelocksmith`, `fileserver`, `filesplit`, `filezilla`, `flashcards`, `fonts`,
`giflab`, `git`, `githubdesktopprofiles`, `gitignore`, `glazewm`, `globtester`,
`gradient`, `griddispatch`, and `guidgen`. Generated route evidence is retained
under the ignored `artifacts/smoke/launch-batches/batch-05/` directory. After
the final XAML-literal audit sync, the same current-manifest slice was rerun:
all 25 again returned `launch-pass` on the initial attempt, with no retry,
failure, or manual-routing entry. That final evidence is retained under
`artifacts/smoke/launch-batches/batch-05-final/`.

**Visual evidence · 視覺證據：** Initial `envvars` and final-current-build
`githubdesktopprofiles` `driver.ps1 -Out ...` capture attempts launched their
routes but both failed at `CopyFromScreen` with `The handle is invalid`,
leaving no PNG. The documented desktop-session capture fallback remains
unavailable as well, so the batch has `capture-blocked` visual evidence: no
screenshot was created or replaced, and no visual-pass claim is made. Page
actions were intentionally not invoked; this batch is route-launch evidence
only. It brings the campaign to the first 125 of 321 manifest routes with
current process-level launch evidence.

**粵語 —** 2026-07-11 用獨立 5 秒／15 秒 protocol 測咗 launchable-route
indices 100–124。25 條全部喺第一次就 `launch-pass`；冇 retry、failure 或者
manual-routing entry 留低。呢批包括 `envvars`、`epoch`、`evcharge`、`events`、
`everything`、`expensesplit`、`faker`、`fancyzones`、`fastboot`、`feedreader`、
`filelocksmith`、`fileserver`、`filesplit`、`filezilla`、`flashcards`、`fonts`、
`giflab`、`git`、`githubdesktopprofiles`、`gitignore`、`glazewm`、`globtester`、
`gradient`、`griddispatch` 同 `guidgen`。產生嘅 route 證據保留喺 Git 忽略嘅
`artifacts/smoke/launch-batches/batch-05/` 目錄。最終 XAML-literal audit sync
之後，用同一個 current-manifest slice 再跑一次：25 條再次全部第一次就
`launch-pass`，冇 retry、failure 或者 manual-routing entry。最終證據保留喺
`artifacts/smoke/launch-batches/batch-05-final/`。

**視覺證據 · Visual evidence：** 初始 `envvars` 同最終 current-build
`githubdesktopprofiles` 嘅 `driver.ps1 -Out ...` 截圖嘗試都有開到 route，但兩次
都喺 `CopyFromScreen` 報 `The handle is invalid`，所以冇 PNG。已記錄嘅
desktop-session 截圖 fallback 都仲係用唔到，因此呢批嘅視覺證據係
`capture-blocked`：冇建立或替換截圖，亦唔會聲稱 visual pass。頁面 actions 有意
冇執行；呢批只係 route-launch 證據。依家 campaign 對 321 條 manifest routes
入面頭 125 條有 current process-level launch 證據。

## Launch-only Batch 06 · 第六批淨啟動測試

**EN —** On 2026-07-11, a freshly generated 321-route / 785-alias manifest
reported no structural routing mismatch. Its launchable-route indices 125–149
were then exercised with the isolated 5-second/15-second protocol. All 25
routes finished `launch-pass`: `h2plant`, `habittracker`, `haranalyzer`,
`hasher`, `headerscore`, `hexdump`, `hexeditor`, `homeassistant`, `hosts`,
`hostsedit`, `hotkeys`, `hpc`, `htmlentities`, `htmlformat`, `htmlpreview`,
`htmltable`, `htmltomd`, `httpheaderref`, `httpheaders`, `httpstatus`,
`icalendar`, `imageeditor`, `imaging`, `imgbase64`, and `iniedit`.
`h2plant` did not expose its dedicated window within the initial 5-second
observation during the first self-contained-publish run (exit 1), then passed
the required 15-second retry; a focused post-publish 5-second `-NoCapture`
retest also passed. The other 24 routes passed on their first attempt. Exact
launch logs and manifest output are retained under the ignored
`artifacts/smoke/launch-batches/batch-06/` and
`artifacts/smoke/baseline-batch06/` directories.

**Source-review and safety evidence · 來源審查同安全證據：** The direct XAML,
code-behind, and service scope for all 25 routes was reviewed. All 206 XAML
event-handler references resolve; every page balances its `LanguageChanged`
subscription; the XAML literal-safety guard passed with all 16 protected
managed defaults; and no direct-scope `TODO`, `FIXME`, or
`NotImplementedException` marker was found. Home Assistant, Header/HTTP
lookups, hosts editing, file writes, hotkey process launches, imaging/USB
actions, and other stateful or external actions were intentionally not invoked
without a disposable target and explicit authorization. A full Debug x64
solution build passed with 0 errors. This is static, build, and route-launch
evidence only—not behavioral or live-side-effect completion.

**Visual evidence · 視覺證據：** A fresh 15-second H2 Plant `driver.ps1 -Out`
attempt reached the dedicated page window but `CopyFromScreen` returned `The
handle is invalid`, producing no PNG. A `WinForgeShot` `PrintWindow` fallback
attempt launched the same page but stopped with `ERROR: bad window rect`; the
previous direct `PrintWindow(PW_RENDERFULLCONTENT)` probe remains uniformly
black and is not valid visual evidence. Therefore Batch 06 is
`capture-blocked`: no screenshot was created or replaced, and no visual-pass
claim is made. The batch brings current process-level route-launch evidence to
the first 150 of 321 manifest routes.

**粵語 —** 2026-07-11 新產生嘅 321-route／785-alias manifest 冇報 structural
routing mismatch。之後用獨立 5 秒／15 秒 protocol 測咗 launchable-route
indices 125–149。25 條都最終 `launch-pass`：`h2plant`、`habittracker`、
`haranalyzer`、`hasher`、`headerscore`、`hexdump`、`hexeditor`、
`homeassistant`、`hosts`、`hostsedit`、`hotkeys`、`hpc`、`htmlentities`、
`htmlformat`、`htmlpreview`、`htmltable`、`htmltomd`、`httpheaderref`、
`httpheaders`、`httpstatus`、`icalendar`、`imageeditor`、`imaging`、
`imgbase64` 同 `iniedit`。第一次 self-contained-publish run 入面，`h2plant`
喺初始 5 秒 observation 未顯示到自己嘅 dedicated window（exit 1），之後通過咗
required 15 秒 retry；發佈完成後再用 5 秒 `-NoCapture` focused retest 都通過。
其餘 24 條全部第一次通過。確實 launch log 同 manifest output 保留喺 Git 忽略嘅
`artifacts/smoke/launch-batches/batch-06/` 同
`artifacts/smoke/baseline-batch06/` 目錄。

**來源審查同安全證據 · Source-review and safety evidence：** 已審查 25 條 route
直屬嘅 XAML、code-behind 同 service scope。206 個 XAML event-handler reference
全部 resolve；每頁嘅 `LanguageChanged` subscription 都有對應解除；XAML
literal-safety guard 連 16 個受保護嘅 managed default 都通過；direct scope 冇搵到
`TODO`、`FIXME` 或 `NotImplementedException` marker。Home Assistant、
Header／HTTP lookup、hosts editing、file write、hotkey process launch、
imaging／USB action 同其他 stateful 或 external action，冇 disposable target 同
明確授權下有意唔執行。完整 Debug x64 solution build 以 0 errors 通過。呢啲只係
static、build 同 route-launch 證據，唔係 behavioral 或 live-side-effect completion。

**視覺證據 · Visual evidence：** 最新 15 秒 H2 Plant `driver.ps1 -Out` 嘗試有
開到 dedicated page window，但 `CopyFromScreen` 回傳 `The handle is invalid`，
所以冇 PNG。`WinForgeShot` 嘅 `PrintWindow` fallback 嘗試都有開同一頁，但
`ERROR: bad window rect` 就停咗；之前 direct
`PrintWindow(PW_RENDERFULLCONTENT)` probe 仍然只係 uniform-black，唔係有效視覺
證據。所以 Batch 06 係 `capture-blocked`：冇建立或替換截圖，亦唔會聲稱
visual-pass。依家 campaign 對 321 條 manifest routes 入面頭 150 條有 current
process-level route-launch 證據。

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

## XAML Boolean-Literal Reliability Audit · XAML Boolean Literal 可靠性審查

**EN —** On 2026-07-11, a focused audit found that `module.htmltable` failed
twice through `--page htmltable`; the crash log identified
`XamlParseException` while assigning `ToggleSwitch.IsOn` at
`Pages/HtmlTableModule.xaml:31`. This is the same self-contained runtime path
previously observed for CSV ⇄ JSON. The audit therefore removed every direct
`IsOn="True|False"` literal from source XAML (16 defaults across 12 pages),
left all bindings intact, and restored each exact default in managed
initialization after `InitializeComponent` (with the existing suppression
guard where an immediate `Toggled` handler requires it).

The new focused guard
`Test-WinForgeXamlLiteralSafety.ps1 -RepoRoot .` passed; it blocks direct
Boolean `IsOn` XAML literals under `Pages/` and `Controls/` and confirms all
16 migrated defaults. It intentionally
does not treat every typed `NumberBox.Value` as a proven defect: the existing
Base Converter reproduction remains fixed, while focused numeric candidates
(`aspectratio`, `ciphers`, `giflab`, `passgen`, and `virtualbox`) reached their
dedicated windows unchanged.

A full Debug x64 solution build passed with 0 errors. A fresh
self-contained 7-second launch-only sweep passed for all affected aliases:
`apiclient`, `httpheaders`, `htmltable`, `proxmox`, `homeassistant`, `hexdump`,
`loremtext`, `native`, `mdtable`, `connections`, `minecraftserver`, and
`githubdesktopprofiles`. A current HTML Table screenshot was attempted, but
`CopyFromScreen` returned `The handle is invalid`; no visual-pass claim,
canonical image replacement, or stale-image substitution is recorded.

**粵語 —** 2026-07-11 做 focused 審查時，`module.htmltable` 用
`--page htmltable` 連續兩次失敗；crash log 指出
`Pages/HtmlTableModule.xaml:31` 設定 `ToggleSwitch.IsOn` 時有
`XamlParseException`。呢個就係之前 CSV ⇄ JSON 見過嘅 self-contained runtime
路徑。所以審查將 source XAML 入面所有 direct `IsOn="True|False"` literal
移走（12 個頁面共 16 個預設），bindings 一個都冇掂，並喺
`InitializeComponent` 後用 managed initialization 還原每個原本預設；要即時
處理 `Toggled` 嘅頁面就用返既有 suppression guard。

新 focused guard `Test-WinForgeXamlLiteralSafety.ps1 -RepoRoot .` 已通過，
會阻止 `Pages/` 同 `Controls/` 入面 direct Boolean `IsOn` XAML literal，亦確認
全部 16 個搬咗位嘅預設。
佢刻意唔會當每一個 typed `NumberBox.Value` 都已證明有問題：既有 Base Converter
重現已修好，而 focused numeric candidates（`aspectratio`、`ciphers`、`giflab`、
`passgen`、`virtualbox`）冇改動都成功開到自己嘅視窗。

完整 Debug x64 solution build 以 0 errors 通過。全新
self-contained、7 秒 launch-only sweep 入面全部受影響 alias 都通過：
`apiclient`、`httpheaders`、`htmltable`、`proxmox`、`homeassistant`、`hexdump`、
`loremtext`、`native`、`mdtable`、`connections`、`minecraftserver` 同
`githubdesktopprofiles`。已嘗試攞最新 HTML Table 截圖，但
`CopyFromScreen` 回傳 `The handle is invalid`；所以冇 visual-pass 聲稱、冇換
canonical image，亦冇用舊圖頂替。

## Launch-only Batch 07 · 第七批淨啟動測試

**EN —** On 2026-07-11, a freshly generated 321-route / 785-alias manifest
again reported no structural routing mismatch. Launchable-route indices 150–174
then ran through the isolated 5-second/15-second protocol: `ipinfo`,
`jsondiff`, `jsonflatten`, `jsonltools`, `jsonmergepatch`, `jsonpatch`,
`jsonpath`, `jsonpointer`, `jsonschema`, `jsonsort`, `jsonstat`, `jsontools`,
`jsontots`, `jwtbuild`, `jwtinspect`, `keepass`, `keyboard`, `komorebi`,
`leet`, `libreoffice`, `lightswitch`, `linetools`, `loan`, `loremimg`, and
`loremtext`. All 25 are `launch-pass` from one initial 5,000 ms attempt; there
were no retries, nonzero initial exits, or failures. Raw per-route logs and the
manifest stay in ignored `artifacts/smoke/launch-batches/batch-07/`.
After a normal merge of current `origin/main`, a final inventory retained the
same 25 route IDs at these indices and found 321 routes / 790 aliases with no
structural routing mismatch. `tools/regen-site-data.ps1` then wrote the
GitHub Pages payload from the merged app: 317 modules, 22 categories, 1,212
features, and 2,214 authored wiki pages.

**Source-review and safety evidence · 來源審查同安全證據：** All 25 direct XAML
and code-behind surfaces were reviewed. All 164 named XAML handlers resolve,
all `LanguageChanged` subscriptions are paired with unsubscription, the
literal-safety guard passed its 16 protected managed defaults, and no
direct-scope `TODO`, `FIXME`, or `NotImplementedException` marker was found.
The JSON/text/JWT transformations were reviewed as local logic; the IP page’s
documented load-time public-IP lookup is a read-only HTTPS request. Vault
files/secret copies, keyboard remapping, Komorebi commands, LibreOffice
conversion, LightSwitch theme/scheduled-task actions, file saves, and other
stateful or external controls were not invoked without a disposable target and
explicit authorization. During that review, KeePass’s delayed secret
clipboard cleanup was corrected: it now compares current clipboard text with
the owned secret and also checks a copy generation before clearing. The
focused `KeePassCrypto.Tests` harness passed 4/4, including matching,
replacement, null, empty, and stale-generation ownership cases. A full Debug x64 solution build
passed with 0 errors (318 warnings), and the focused post-fix
`--page keepass -NoCapture` launch passed.

**Visual evidence · 視覺證據：** After the post-fix KeePass launch, a fresh
15-second `driver.ps1 -Page keepass -Out artifacts/smoke/launch-batches/batch-07/screenshots/keepass-clipboard-safety.png`
attempt reached `CopyFromScreen`, which returned `The handle is invalid`. No
PNG was created, inspected, replaced, or reused; Batch 07 is
`capture-blocked`, not visual-pass. This batch brings current process-level
route-launch evidence to the first 175 of 321 manifest routes.

**粵語 —** 2026-07-11 新產生嘅 321-route／785-alias manifest 再次冇報
structural routing mismatch。之後用獨立 5 秒／15 秒 protocol 測咗
launchable-route indices 150–174：`ipinfo`、`jsondiff`、`jsonflatten`、
`jsonltools`、`jsonmergepatch`、`jsonpatch`、`jsonpath`、`jsonpointer`、
`jsonschema`、`jsonsort`、`jsonstat`、`jsontools`、`jsontots`、`jwtbuild`、
`jwtinspect`、`keepass`、`keyboard`、`komorebi`、`leet`、`libreoffice`、
`lightswitch`、`linetools`、`loan`、`loremimg` 同 `loremtext`。25 條都係一個
初始 5,000 ms attempt `launch-pass`；冇 retry、冇 nonzero initial exit、冇
failure。每條 route 嘅原始 log 同 manifest 放喺 Git 忽略嘅
`artifacts/smoke/launch-batches/batch-07/`。
正常合併 current `origin/main` 之後，最終 inventory 仍然喺呢啲 indices 保留同一組
25 條 route ID，並搵到 321 routes／790 aliases，冇 structural routing mismatch。
跟住 `tools/regen-site-data.ps1` 用已合併 app 寫咗 GitHub Pages payload：317 個
modules、22 個 categories、1,212 個 features 同 2,214 個 authored wiki pages。

**來源審查同安全證據 · Source-review and safety evidence：** 已審查晒 25 條
route 直屬 XAML 同 code-behind surface。164 個有名 XAML handler 全部 resolve，
全部 `LanguageChanged` subscription 都有對應 unsubscribe，literal-safety guard
連 16 個受保護 managed default 都通過，direct scope 冇搵到 `TODO`、`FIXME` 或
`NotImplementedException` marker。JSON／文字／JWT transformation 都係本機 logic
審查；IP 頁面記錄咗 load-time public-IP lookup 係 read-only HTTPS request。冇
disposable target 同明確授權下，vault file／secret copy、keyboard remapping、
Komorebi command、LibreOffice conversion、LightSwitch 主題／排程工作、file save
同其他 stateful 或 external control 都冇撳。審查期間修正咗 KeePass 延遲清除
秘密剪貼簿嘅問題：而家清除前會比較目前文字同 owned secret，仲會檢查 copy
generation。專注嘅 `KeePassCrypto.Tests` harness 4/4 通過，包括 matching、
replacement、null、empty 同 stale-generation ownership case。完整 Debug x64 solution build 以 0
errors（318 warnings）通過，修正後專注嘅 `--page keepass -NoCapture` launch 都
通過。

**視覺證據 · Visual evidence：** 修正後 KeePass launch 之後，新嘅 15 秒
`driver.ps1 -Page keepass -Out artifacts/smoke/launch-batches/batch-07/screenshots/keepass-clipboard-safety.png`
嘗試開到 `CopyFromScreen`，但回傳 `The handle is invalid`。冇 PNG 產生、檢查、
替換或者重用；Batch 07 係 `capture-blocked`，唔係 visual-pass。呢批令 campaign
對 321 條 manifest route 入面頭 175 條有 current process-level route-launch
證據。
## XAML Numeric-Literal Reliability Audit · XAML 數值 Literal 可靠性審查

**EN —** On 2026-07-11, a bounded runtime audit generated a fresh
321-route / 785-alias manifest, then statically found direct
`NumberBox.Value` defaults in 79 XAML files. Seventy-eight of those pages have
a deep link. Fresh self-contained launch checks exercised all 78: 72 reached
`launch-pass` unchanged, while six routes failed twice after the 4-second /
12-second retry protocol. Their crash-log first-property failures were
`MarkdownTocModule` line 23 (`MinBox`), `NameGenModule` line 25 (`CountBox`),
`NumberFormatModule` line 36 (`DecimalsBox`), `SciNotationModule` line 26
(`SigBox`), `SubnetCalcModule` line 27 (`CidrBox`), and `UnitConvertModule`
line 25 (`ValueBox`): each was `XamlParseException` assigning
`Microsoft.UI.Xaml.Controls.NumberBox.Value`.

The parser reports only the first failing property, so the related direct
numeric defaults on each already-proven page were moved together as a small
page-local batch: ten defaults across Markdown TOC, Name Generator, Number
Formatter, Scientific Notation, Subnet Calculator, and Unit Converter. No
bindings changed, and the 72 passing routes were not migrated. After the
Markdown numeric defaults were removed, its next fresh launch exposed one
separate `ToggleButton.IsChecked` XAML conversion failure at line 35 for
`IncludeH1Chk`; that one existing CheckBox default was also moved under the
same suppression guard. The guard now blocks direct `IsOn="True|False"`
globally, and explicitly protects the 16 ToggleSwitch defaults, the one
Markdown TOC CheckBox default, and the ten demonstrated NumberBox defaults;
it deliberately does not prohibit unproven numeric or CheckBox literals.

`Test-WinForgeXamlLiteralSafety.ps1 -RepoRoot .` passed. A forced
self-contained publish launched `--page markdowntoc`, followed by a fresh
6-second launch-only retest of `markdowntoc`, `namegen`, `numberformat`,
`scinotation`, `subnetcalc`, and `unitconvert`: all six were `launch-pass`
without retry or failure. A bounded single-worker Debug x64 solution build
also passed with 0 errors. The prior unrestricted build command exceeded its
execution window without an error result and is recorded as inconclusive, not
as a pass. Each of the six pages then received a current 12-second capture
attempt; every `CopyFromScreen` call returned `The handle is invalid`. No PNG
was produced or substituted, so this batch is launch evidence plus
`capture-blocked`, never a visual-pass claim.

**粵語 —** 2026-07-11 做咗一個受限嘅 runtime 審查：先產生新鮮嘅
321-route／785-alias manifest，再由 static search 搵到 79 個 XAML 檔有 direct
`NumberBox.Value` 預設；當中 78 個頁面有 deep link。用新鮮 self-contained
launch check 跑晒 78 個：72 個冇改動都係 `launch-pass`；6 條 route 經過 4 秒／
12 秒 retry protocol 後都失敗兩次。crash log 報嘅第一個 property failure 係
`MarkdownTocModule` 第 23 行（`MinBox`）、`NameGenModule` 第 25 行
（`CountBox`）、`NumberFormatModule` 第 36 行（`DecimalsBox`）、
`SciNotationModule` 第 26 行（`SigBox`）、`SubnetCalcModule` 第 27 行
（`CidrBox`）同 `UnitConvertModule` 第 25 行（`ValueBox`）；全部都係將
`Microsoft.UI.Xaml.Controls.NumberBox.Value` 賦值時嘅 `XamlParseException`。

個 parser 只會報第一個失敗 property，所以每個已經證實會出事嘅頁面入面、相關嘅
direct 數值預設一齊用細小 page-local batch 搬走：Markdown 目錄、名稱產生器、數字
格式化、科學記數法、子網計算器同單位換算器一共 10 個預設。冇改 bindings，72 條
已經通過嘅 route 都冇搬。移走 Markdown 嘅數值預設後，下一次新鮮 launch 顯示咗另一個
獨立嘅 XAML conversion failure：第 35 行 `IncludeH1Chk` 嘅
`ToggleButton.IsChecked`；所以只將呢一個 CheckBox 原有預設用同一個 suppression
guard 搬走。guard 而家全域阻止 direct `IsOn="True|False"`，同時明確保護 16 個
ToggleSwitch 預設、Markdown 目錄一個 CheckBox 預設，同埋 10 個已證實嘅 NumberBox
預設；佢刻意唔會禁止未證實有問題嘅數值或 CheckBox literal。

`Test-WinForgeXamlLiteralSafety.ps1 -RepoRoot .` 通過。forced self-contained
publish 成功開到 `--page markdowntoc`，之後新鮮 6 秒 launch-only retest 跑咗
`markdowntoc`、`namegen`、`numberformat`、`scinotation`、`subnetcalc` 同
`unitconvert`：6 條全部 `launch-pass`，冇 retry 或 failure。受限 single-worker
Debug x64 solution build 都以 0 errors 通過。之前 unrestricted build command 過咗
execution window 都冇 error result，係 inconclusive，唔可以當 pass。之後 6 個頁面
逐個用最新 12 秒 capture 嘗試；每次 `CopyFromScreen` 都回傳 `The handle is invalid`。
冇 PNG 產生或者頂替，所以呢批係 launch evidence 加 `capture-blocked`，絕對唔係
visual-pass 聲稱。

## Package-source preservation P0 · 套件來源保留 P0

**EN —** Source tracing found that `PackageItem.Source` survived into a bundle
and into the coordinator's copied item, but was previously absent from command
preview, live-operation de-duplication and `PackageOperations.RunAsync`.
`PackageSourcePolicy` is now the single validation and normalization boundary
before all three. It preserves the configured/default behavior for an empty
source; emits only strict manager-specific forms for valid selected sources
(`--source` for WinGet/Chocolatey, a bucket-qualified Scoop install reference,
fixed trusted endpoints for PyPI/npm/NuGet/crates.io, and the supported
PowerShell repository parameters); and validates vcpkg's displayed triplet as
metadata already encoded in its package id. For update/uninstall commands with
no source selector, a known safe source remains part of the source-aware
selection/bundle/queue identity and reaches the runner, but deliberately emits
no invented CLI flag. Local, unknown, malformed and unsupported sources fail
before preview, queueing or execution, with generic messages that never echo
untrusted source text. Imported bundle entries with such a source move to the
incompatible log instead of silently installing from a different default.

**Focused evidence · 專注證據：** `PackageManagerCore.Tests` passed **27/27**,
including blank-default compatibility, valid preview/runner forwarding, Scoop
qualification, canonical same-source de-duplication, distinct same-ID/
different-source operations, known-source uninstall compatibility, and
malicious-source rejection before either builder or runner. The complete local
harness also passed: ADB **6/6**, AI persistence **5/5**, settings store **6/6**,
companion build log **4/4**, KeePass crypto/clipboard **4/4**, and ReactorSim
**63/63** plus its exit-code contract. The final Debug x64 solution build completed with **0 errors**. A fresh self-contained publish launched
`--page packages` successfully (`OK launch-only page='packages'`).

**Visual evidence · 視覺證據：** The source-aware command preview is a
presentation change, so a fresh Package Manager capture was attempted with
`driver.ps1 -Page packages -Publish -WaitMs 15000`. `CopyFromScreen` was
unavailable; its `PrintWindow` fallback returned a uniform frame and graphics
capture was unavailable in this desktop session. The output PNG was not
created, inspected, replaced or reused. The subsequent `-NoCapture` launch
passed, but this is `capture-blocked` launch evidence, never visual
verification.

**粵語 —** 來源追蹤發現 `PackageItem.Source` 以前可以入到清單同協調器複本，但
指令預覽、進行中操作去重同 `PackageOperations.RunAsync` 都冇帶住佢。依家
`PackageSourcePolicy` 係三個位置之前唯一嘅驗證同正規化邊界。空白來源會保留
管理器已設定嘅預設；有效、所揀來源只會輸出嚴格嘅管理器專用形式（WinGet／
Chocolatey 用 `--source`、Scoop 安裝用 bucket-qualified 參照、PyPI／npm／NuGet／
crates.io 用固定可信 endpoint、PowerShell 用真正支援嘅 repository 參數）；vcpkg
顯示嘅 triplet 會驗證成已經寫喺套件 id 入面嘅中繼資料。更新／解除安裝指令冇來源
選擇器時，已知安全來源仍然會留喺來源感知嘅多選／清單／佇列身份並傳入 runner，但
刻意唔會亂加 CLI 旗標。本機、未知、格式錯誤同唔支援來源會喺預覽、排隊或者執行前
失敗，訊息唔會回顯唔可信來源文字。匯入清單遇到呢類來源會搬去不相容記錄，唔會靜靜
改用另一個預設來源安裝。

**專注證據：** `PackageManagerCore.Tests` **27/27** 通過，覆蓋空白預設相容、有效
預覽／runner 傳遞、Scoop qualification、同來源 canonical 去重、同 ID／唔同來源操作、
已知來源解除安裝相容，同埋惡意來源未到 builder 或 runner 已拒絕。完整本機 harness
亦通過：ADB **6/6**、AI persistence **5/5**、settings store **6/6**、companion build
log **4/4**、KeePass crypto／clipboard **4/4**，以及 ReactorSim **63/63** 加 exit-code
contract。最終 Debug x64 solution build 以 **0 errors** 完成。新嘅 self-contained publish 成功開到 `--page packages`
（`OK launch-only page='packages'`）。

**視覺證據：** 來源感知指令預覽屬於呈現變更，所以已經用
`driver.ps1 -Page packages -Publish -WaitMs 15000` 嘗試新嘅 Package Manager capture。
`CopyFromScreen` 唔可用；`PrintWindow` fallback 得到 uniform frame，而呢個 desktop
session 嘅 graphics capture 亦唔可用。輸出 PNG 冇產生、檢查、替換或者重用。之後
`-NoCapture` launch 通過，但呢個只係 `capture-blocked` launch 證據，絕對唔係視覺驗證。

## Launch-only Batch 08 · 第八批淨啟動測試

**EN —** On 2026-07-11, the final post-merge inventory recorded **322 routes**,
**800 deep-link aliases**, 1,264 source-review files, and 343,904 source-review
lines, with no structural routing mismatch. It retained the same 25 route IDs
at launchable indices 175–199: mactools, mail, markdown, markdowntoc, mdtable,
media, mediaplayer, metatags, mime, minecraftlauncher, minecraftserver,
minecraftworldtools, mixer, monitor, morse, mouse, mouseutils, mwb, namegen,
native, newplus, nilesoftshell, nmap, notes, and numberformat. The isolated
5-second/15-second protocol ended **25/25 launch-pass**. mactools logged
initial exit 1 at 5 seconds because no dedicated window was observed, then
passed the bounded 15-second retry; the other 24 passed on their initial
five-second attempt. Raw manifests and every attempt log remain in ignored
artifacts/smoke/launch-batches/batch-08/ and artifacts/smoke/launch-batch-08/
directories.

**Source-review and safety evidence · 來源審查同安全證據：** The direct XAML,
code-behind, and service scope for all 25 routes was reviewed. All **201** named
XAML event-handler references resolve, every route balances its
LanguageChanged subscription, and direct scope contains no TODO, FIXME, or
NotImplementedException marker. The focused
Test-WinForgeXamlLiteralSafety.ps1 -RepoRoot . guard passed its 16 managed
ToggleSwitch defaults, one protected CheckBox default, and ten reproduced
NumberBox defaults. The merged full Debug x64 build passed with **0 errors**.
Mail/IMAP, media conversion and file writes, Minecraft downloads/launches,
audio and mouse changes, network scanning, shell/configuration changes, and
note saves were intentionally not invoked without a disposable target and
explicit authorization. No concrete defect was reproduced; the Mac Tools retry
is recorded as a slow first-render observation, not an initial-pass result.

**Visual evidence · 視覺證據：** A fresh
driver.ps1 -Page mactools -Out artifacts/smoke/launch-batches/batch-08/screenshots/mactools-default.png -WaitMs 15000
attempt reached the page window but could not capture it. CopyFromScreen was
unavailable; the driver tried PrintWindow, detected a uniform frame, and
stopped with CopyFromScreen is unavailable and the PrintWindow fallback
produced a uniform frame; graphics capture is unavailable in this desktop
session. No PNG was saved, inspected, replaced, or reused. Batch 08 is
capture-blocked, never visual-pass or behavioral completion. This brings
current process-level route-launch evidence to the first **200 of 322** routes.

**粵語 —** 2026-07-11 合併後嘅最終 inventory 記錄咗 **322 條 routes**、
**800 個 deep-link aliases**、1,264 個 source-review files 同 343,904 行
source-review lines，冇 structural routing mismatch。launchable indices 175–199
仍然係同一組 25 條：mactools、mail、markdown、markdowntoc、mdtable、media、
mediaplayer、metatags、mime、minecraftlauncher、minecraftserver、
minecraftworldtools、mixer、monitor、morse、mouse、mouseutils、mwb、namegen、
native、newplus、nilesoftshell、nmap、notes 同 numberformat。獨立 5 秒／15 秒
protocol 最終係 **25/25 launch-pass**。mactools 第一次五秒記錄到 exit 1，因為未
見到獨立視窗；受限 15 秒 retry 之後通過。其餘 24 條第一次五秒就通過。原始
manifest 同每次 attempt log 會留喺 Git 忽略嘅
artifacts/smoke/launch-batches/batch-08/ 同 artifacts/smoke/launch-batch-08/
目錄。

**來源審查同安全證據 · Source-review and safety evidence：** 已審查晒 25 條
route 直屬嘅 XAML、code-behind 同 service scope。全部 **201** 個有名 XAML
event-handler reference 都 resolve，每條 route 嘅 LanguageChanged subscription
都有對應解除，direct scope 冇 TODO、FIXME 或 NotImplementedException marker。
專注嘅 Test-WinForgeXamlLiteralSafety.ps1 -RepoRoot . guard 通過咗 16 個 managed
ToggleSwitch defaults、一個受保護 CheckBox default，同 10 個已重現嘅 NumberBox
defaults。合併後完整 Debug x64 build 以 **0 errors** 通過。Mail／IMAP、媒體轉檔
同檔案寫入、Minecraft 下載／啟動、音訊同滑鼠變更、網絡掃描、shell／設定變更，
同 notes 儲存，冇 disposable target 同明確授權下都刻意冇執行。冇重現到具體
defect；Mac Tools retry 只記為慢嘅第一次 render observation，唔當第一次 pass。

**視覺證據 · Visual evidence：** 最新
driver.ps1 -Page mactools -Out artifacts/smoke/launch-batches/batch-08/screenshots/mactools-default.png -WaitMs 15000
嘗試有開到頁面視窗，但攞唔到截圖。CopyFromScreen 唔可用；driver 試咗
PrintWindow、發現係 uniform frame，之後以 CopyFromScreen is unavailable and the
PrintWindow fallback produced a uniform frame; graphics capture is unavailable
in this desktop session. 停止。冇 PNG 儲存、檢查、替換或者重用。Batch 08 係
capture-blocked，絕對唔係 visual-pass 或 behavioral completion。依家 current
process-level route-launch 證據去到 322 條 routes 入面頭 **200** 條。

## Hardware-monitor driver lifecycle remediation · 硬件監察驅動生命週期修復

**EN —** A Batch 08 follow-up found that the `monitor` route had opened a static
LibreHardwareMonitor `Computer` without a matching `Close()`. That could leave the
WinForge-owned `R0WinForge` driver service running with an `ImagePath` in the temporary
publish tree that the isolated route driver removed. System Monitor and Battery & Thermal
now share a reference-counted `Computer` lease: the final page release, normal close, tray
quit, or process-exit fallback runs the upstream `Computer.Close()` for that exact object.
The failed-open path closes its candidate too. The fix intentionally does not enumerate,
stop, delete, or alter services, so an already stale or externally-created driver service is
outside normal-runtime cleanup. The driver-free lifecycle harness passed **4/4** and the
Debug x64 solution build passed with **0 errors**.

**粵語 —** Batch 08 後續發現 `monitor` route 開咗 static LibreHardwareMonitor
`Computer` 但冇對應 `Close()`。咁樣可以令 WinForge 所屬嘅 `R0WinForge` driver service
繼續運行，而 `ImagePath` 指向隔離 route driver 已經刪除嘅暫存 publish tree。System
Monitor 同 Battery & Thermal 而家共用 reference-counted `Computer` lease：最後一個頁面
釋放、正常關窗、tray 退出或者 process-exit 後備先會對嗰個確實 object 跑 upstream
`Computer.Close()`。failed-open 路徑都會 close candidate。修正有意唔 enumerate、stop、
delete 或更改 service，所以已過期或外部建立嘅 driver service 唔係正常 runtime cleanup
範圍。無真 driver lifecycle harness **4/4** 通過，Debug x64 solution build 以 **0 errors**
通過。

**Visual/capture status · 視覺／截圖狀態：** This is nonvisual lifecycle code. No competing
WinForge GUI launch or real driver load was performed while Batch 09 was sweeping routes; no
screenshot was created, replaced, or claimed as visual verification. · 呢個係非視覺生命週期
程式碼。Batch 09 跑 route sweep 期間冇開另一個 WinForge GUI 或載入真 driver；冇 screenshot
產生、替換或聲稱係視覺驗證。See [Hardware Monitor Driver Lifecycle](Hardware-Monitor-Driver-Lifecycle.md)
for the complete boundary and test record. · 完整邊界同測試記錄請睇[硬件監察驅動生命週期](Hardware-Monitor-Driver-Lifecycle.md)。

## Pumped-Hydro state-integrity remediation · 抽水蓄能狀態完整性修復

**EN —** Batch 09 source review confirmed that `Render()` had invoked the Pumped-Storage Hydro state step, so a language refresh could advance reservoir energy and rewards. `OnLoaded` then called both render and the state step, producing two immediate advances. The page now has one deterministic boundary: `OnLoaded` initializes/subscribes/snapshots/renders/starts the timer without ticking; only loaded-guarded `OnTick` calls `AdvanceSimulation()` once at a fixed `0.5` seconds; `Render`, `RenderText`, language refresh, controls, and reset are observational; and `Unloaded` stops the timer and unsubscribes the language handler.

**Focused evidence · 專注證據：** `tests/PumpedHydroService.Tests` passed **4/4**: `explicit ticks alone advance stored energy`; `charge and discharge use the documented split round-trip efficiency`; `generation mints from delivered MWh without a kWh multiplier`; and `page loading and rendering are observational; only the guarded timer advances`. The test directly proves `WattsFromDeliveredMWh(1) == 0.036`: `0.00001 ⚡/(MWe·s) × 3,600 MW·s/MWh`, without the former `×1000` kWh error. It uses test doubles rather than the user wallet/settings. The Debug x64 solution build completed with **0 errors**.

**Visual/capture status · 視覺／截圖狀態：** This is nonvisual service/code-behind work with no XAML layout or control-surface change. No competing WinForge GUI was launched while Batch 09 was active; no screenshot was attempted, created, replaced, reused, or claimed as visual verification. Screenshot replacement is not applicable.

**粵語 —** Batch 09 source review 證實 `Render()` 以前會叫抽水蓄能狀態步進，所以轉語言都可以推進水塘能量同獎勵。`OnLoaded` 再同時叫 render 同狀態步進，造成一開頁就推進兩次。頁面而家得一個確定性邊界：`OnLoaded` 只會初始化／訂閱／讀快照／render／開始 timer，唔會 tick；只有有 loaded guard 嘅 `OnTick` 會喺固定 `0.5` 秒叫一次 `AdvanceSimulation()`；`Render`、`RenderText`、轉語言、控制項同重設都只係觀察性；而 `Unloaded` 會停 timer 同取消語言 handler 訂閱。

**專注證據：** `tests/PumpedHydroService.Tests` **4/4** 通過：`explicit ticks alone advance stored energy`、`charge and discharge use the documented split round-trip efficiency`、`generation mints from delivered MWh without a kWh multiplier` 同 `page loading and rendering are observational; only the guarded timer advances`。測試直接證明 `WattsFromDeliveredMWh(1) == 0.036`：`0.00001 ⚡/(MWe·s) × 3,600 MW·s/MWh`，冇咗以前 `×1000` kWh 錯誤。佢用 test double，唔會碰使用者 wallet／settings。Debug x64 solution build 以 **0 errors** 完成。

**視覺／截圖狀態：** 呢個係非視覺嘅 service／code-behind 工作，冇 XAML 排版或控制介面變更。Batch 09 進行期間冇開另一個 WinForge GUI；冇嘗試、產生、替換、重用 screenshot，亦冇聲稱係視覺驗證。唔適用截圖替換。
## Launch-only Batch 09 · 第九批淨啟動測試

**EN —** The final post-merge inventory records **323 routes**, **805 deep-link
aliases**, 1,274 source-review files, 346,641 source-review lines, 10 test
projects, and no structural routing mismatch. Batch 09 covers launchable indices
200–224: numseq, numwords, numwordsx, ollama, onedrive, ossapps, packages,
packer, passgen, passwordstrength, pathdoctor, pdftoolkit, peek, percentcalc,
pgadmin, phonetic, ping, pixeleditor, portscan, powerdisplay, powertoys, procexp,
proxmox, pumpedhydro, and qbittorrent. The original isolated 5-second/15-second
sweep passed 24/25 at five seconds; Percent Calculator failed both attempts with
the reproducible `ToggleButton.IsChecked` XAML conversion fault at `C4Inc`.
Its guarded managed default and fresh 6-second deep-link retest passed, yielding
**25/25 final launch-pass**. The raw first-attempt logs and focused retests remain
under ignored `artifacts/smoke/launch-batches/batch-09/`; the runner's auxiliary
no-alias `shell.allapps` row was outside this numbered slice and was not treated
as a launch pass.

**Source-review and repair evidence · 來源審查同修正證據：** All **240** named XAML
handler references in the slice resolve; language subscriptions are balanced after
the qBittorrent named-handler unsubscribe repair. The direct scope has no unresolved
TODO, FIXME, or NotImplementedException marker—the only `todo` search hit is Peek's
intentional well-known filename classifier. Passing, unproven direct `IsChecked`
literals were deliberately left untouched. The literal-safety guard now verifies 16
managed ToggleSwitch defaults, two reproduced `IsChecked` defaults (Markdown TOC
and Percentage Calculator), and 10 reproduced NumberBox defaults. Pixel Editor's
index-based undo actions could target deleted/reordered frame or layer slots; topology
changes now invalidate that history and selection movement is bounded before flat-buffer
access. `PixelEditorCore.Tests` passed **5/5** after a **4/5** failing baseline.
Proxmox now replaces/cancels a per-page operation token on load, reconnect, disconnect,
and unload; refresh and config results require the captured generation and selected guest
key before changing the UI. Its opt-in certificate callback accepts normally trusted TLS,
or only a current, self-issued one-leaf chain whose sole meaningful status is
`UntrustedRoot`; missing certificates, hostname mismatch, expiry, and multi-element
issuer chains remain rejected. `ProxmoxSecurity.Tests` passed **6/6**. Fresh
self-contained `--page pixeleditor` and `--page proxmox` launches passed. The
merged Debug x64 solution build completed with **0 errors**.

**Safety disposition · 安全處置：** This is still launch/static/test evidence, not a
claim that every action passed. No package installation, OneDrive registry or file
operation, Ollama pull/chat, image build, PDF write, file launch, network scan/ping,
display or process control, Proxmox connection/power action, pumped-hydro control, or
qBittorrent credential/download action was live-executed without a disposable target
and explicit authorization.

**Visual evidence · 視覺證據：** Fresh 15-second `driver.ps1 -Out` attempts for the
changed Percentage Calculator, qBittorrent, Pixel Editor, and Proxmox pages each reached
the capture stage but failed identically: `CopyFromScreen` was unavailable and the
`PrintWindow` fallback produced a uniform frame while graphics capture remained
unavailable in this desktop session. No PNG was created, inspected, replaced, or reused;
these pages are `capture-blocked`, never visual-pass. Batch 09 brings current
process-level launch evidence to the first **225 of 323** routes.

**粵語 —** 最後合併後嘅 inventory 記錄咗 **323 條 routes**、**805 個 deep-link
aliases**、1,274 個來源審查檔、346,641 行來源審查程式、10 個 test projects，亦都冇
structural routing mismatch。Batch 09 覆蓋 launchable indices 200–224：numseq、
numwords、numwordsx、ollama、onedrive、ossapps、packages、packer、passgen、
passwordstrength、pathdoctor、pdftoolkit、peek、percentcalc、pgadmin、phonetic、
ping、pixeleditor、portscan、powerdisplay、powertoys、procexp、proxmox、
pumpedhydro 同 qbittorrent。原本隔離嘅 5 秒／15 秒 sweep 有 24/25 條五秒通過；
Percentage Calculator 兩次都失敗，重現到 `C4Inc` 嘅
`ToggleButton.IsChecked` XAML conversion fault。搬咗去受 guard 保護嘅 managed
default 之後，新鮮 6 秒 deep-link retest 通過，最後係 **25/25 launch-pass**。
原始 first-attempt logs 同 focused retests 留喺 Git 忽略嘅
`artifacts/smoke/launch-batches/batch-09/`；runner 額外產生嘅無 alias
`shell.allapps` row 唔屬於呢個編號範圍，亦冇當成 launch pass。

**來源審查同修正證據 · Source-review and repair evidence：** 呢批全部 **240** 個有名
XAML handler references 都 resolve；修正 qBittorrent 用有名 handler 解除訂閱後，
LanguageChanged subscriptions 都平衡。直屬範圍冇未解決 TODO、FIXME 或
NotImplementedException marker；唯一 `todo` search hit 係 Peek 有意識嘅
well-known filename classifier。通過 launch、但未證實有問題嘅 direct `IsChecked`
literals 刻意冇郁。literal-safety guard 而家驗證 16 個 managed ToggleSwitch defaults、
兩個已重現嘅 `IsChecked` defaults（Markdown TOC 同 Percentage Calculator），同
10 個已重現嘅 NumberBox defaults。Pixel Editor 嘅 index-based undo actions 以前可能
指去已刪除／重排咗嘅 frame 或 layer slot；而家一有 topology change 就會作廢舊 history，
選取移動亦會喺 flat-buffer access 之前限制範圍。`PixelEditorCore.Tests` 由原本
**4/5** failing baseline 變成 **5/5** 通過。Proxmox 而家喺 load、reconnect、
disconnect 同 unload 會替換／取消 per-page operation token；refresh 同 config
results 一定要同 captured generation 同所揀 guest key 相符先可以改 UI。佢嘅 opt-in
certificate callback 會接受正常受信任嘅 TLS，或者只接受仍然有效、自發、一個 leaf、
唯一 meaningful status 係 `UntrustedRoot` 嘅 chain；冇 certificate、hostname mismatch、
過期同 multi-element issuer chain 都會拒絕。`ProxmoxSecurity.Tests` **6/6** 通過。
新鮮 self-contained `--page pixeleditor` 同 `--page proxmox` launches 都通過。合併後嘅
Debug x64 solution build 以 **0 errors** 完成。

**安全處置 · Safety disposition：** 呢個仍然係 launch／static／test 證據，唔係聲稱
每一個 action 都通過。冇 disposable target 同明確授權下，冇 live-run 套件安裝、
OneDrive registry 或檔案操作、Ollama pull/chat、image build、PDF 寫入、開檔、
network scan/ping、display 或 process control、Proxmox 連線／電源操作、
pumped-hydro control，或者 qBittorrent credential/download action。

**視覺證據 · Visual evidence：** 改過嘅 Percentage Calculator、qBittorrent、
Pixel Editor 同 Proxmox pages 都用咗新鮮 15 秒 `driver.ps1 -Out` 嘗試；全部都到咗
capture stage，但同樣失敗：`CopyFromScreen` 唔可用，而 `PrintWindow` fallback 產生
uniform frame，呢個 desktop session 嘅 graphics capture 仍然唔可用。冇 PNG 產生、
檢查、替換或者重用；呢啲頁面係 `capture-blocked`，絕對唔係 visual-pass。Batch 09
令到而家 process-level launch evidence 去到 **323 條 routes 入面頭 225 條**。

## Launch-only Batch 11 · 第十一批淨啟動測試

**EN —** The isolated Batch 11 inventory records **323 routes**, **805 deep-link
aliases**, 1,280 source-review files, 347,984 source lines, 12 test projects, and no
routing issue or unmapped alias. It covers launchable indices 250–274: shortid,
slugify, smelter, sqlformat, sqlitebrowser, ssh, startup, steelmill, stringcompare,
stringinspector, subnetcalc, subnetv6, symbols, tableformat, tallycounter,
taskbar-tweaker, tasks, terminal, testdisk, textcolumns, textdiff, textescape,
textocr, textredact, and textreplace. The corrected local batch runner used its
5-second/15-second protocol and finished **25/25 launch-pass**, all at the first
five-second attempt with no retry or failure. Raw manifests, logs, and source-review
tables remain ignored under `artifacts/smoke/batch11/`. This is an independently
numbered slice; contiguous coverage depends on the companion Batch 10 range 225–249,
not on a shortcut in this report.

**Static/source and safety evidence · 靜態／來源同安全證據：** The 25 route
registrations, navigation tags, MapType registrations, and deep-link aliases came from
the generated manifest. All **171** declared XAML handlers resolve in their matching
code-behind; `Loc.I.LanguageChanged` subscription counts are balanced; and direct scope
has no TODO, FIXME, or `NotImplementedException` marker. The literal-safety guard
passed, and the Debug x64 solution build completed with **0 errors**. This does not
claim behavior or live side effects: SQLite changes, SSH/network activity, Startup and
Taskbar registry changes, Scheduled Tasks, Terminal launches, TestDisk recovery, OCR,
clipboard, and file writes were intentionally not executed without a disposable target
and explicit authorization.

**Visual evidence · 視覺證據：** A fresh self-contained
`driver.ps1 -Page shortid -Out artifacts/smoke/batch11/screenshots/shortid-default.png -WaitMs 5000`
attempt reached the page but could not capture it. `CopyFromScreen` was unavailable;
the `PrintWindow` fallback produced a uniform frame and stopped with `graphics capture
is unavailable in this desktop session`. No PNG was created, inspected, replaced, or
reused. Batch 11 is `capture-blocked`, never visual-pass.

**粵語 —** 隔離嘅 Batch 11 inventory 記錄咗 **323 條 routes**、**805 個 deep-link
aliases**、1,280 個 source-review files、347,984 行 source 同 12 個 test projects，
冇 routing issue 或 unmapped alias。佢覆蓋 indices 250–274：shortid、slugify、smelter、
sqlformat、sqlitebrowser、ssh、startup、steelmill、stringcompare、stringinspector、
subnetcalc、subnetv6、symbols、tableformat、tallycounter、taskbar-tweaker、tasks、
terminal、testdisk、textcolumns、textdiff、textescape、textocr、textredact 同 textreplace。
修正後嘅 local batch runner 用 5 秒／15 秒 protocol，最後係 **25/25 launch-pass**；
全部第一次五秒已通過，冇 retry 同 failure。原始 manifest、logs 同 source-review tables
留喺 Git 忽略嘅 `artifacts/smoke/batch11/`。呢個係獨立編號嘅 slice；連續 coverage 要連
同 Batch 10 嘅 225–249 範圍一齊睇，唔可以由呢份報告走捷徑。

**靜態／來源同安全證據 · Static/source and safety evidence：** 呢 25 條 route 嘅
registration、navigation tags、MapType registrations 同 deep-link aliases 全部由 generated
manifest 盤點。全部 **171** 個 XAML handlers 都喺相應 code-behind resolve；
`Loc.I.LanguageChanged` 訂閱數量平衡；direct scope 冇 TODO、FIXME 或
`NotImplementedException` marker。literal-safety guard 通過，而 Debug x64 solution build
以 **0 errors** 完成。呢個唔聲稱行為或者 live side effects：SQLite 變更、SSH／網絡活動、
Startup 同 Taskbar 登錄檔變更、Scheduled Tasks、Terminal launches、TestDisk recovery、OCR、
clipboard 同 file writes，冇 disposable target 同明確授權下都刻意冇執行。

**視覺證據 · Visual evidence：** 新嘅 self-contained
`driver.ps1 -Page shortid -Out artifacts/smoke/batch11/screenshots/shortid-default.png -WaitMs 5000`
嘗試有開到頁面，但攞唔到 capture。`CopyFromScreen` 唔可用；`PrintWindow` fallback
產生 uniform frame，然後以 `graphics capture is unavailable in this desktop session` 停止。
冇 PNG 產生、檢查、替換或者重用。Batch 11 係 `capture-blocked`，絕對唔係 visual-pass。

[← Wiki Home](Home.md) · [Developer](Developer.md) · [Screenshots](Screenshots.md)
