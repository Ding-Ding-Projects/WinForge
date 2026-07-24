# Screen Recorder & Registry Editor Reliability · 螢幕錄影與登錄編輯器可靠性

Date · 日期：2026-07-24
Scope · 範圍：bounded ffmpeg shutdown and truthful Registry Editor value deletion only. · 只限有時限嘅 ffmpeg 停止流程同登錄編輯器如實回報值刪除。

## Outcome · 結果

**EN —** Screen Recorder begins a managed, discarded stderr drain before the
recording session is exposed. That drain now copies raw bytes to `Stream.Null`
instead of decoding and dispatching one callback per ffmpeg progress line. Dense
diagnostics therefore cannot consume the graceful-save budget under host load.
Its Stop path still gives the `q` command, graceful wait, and forced-exit wait
explicit deadlines. A forced or unconfirmed stop remains a failure, never a
successfully saved recording.

**粵語 —** 螢幕錄影會喺公開錄影 session 之前，以受管理方式排走並丟棄 stderr。
而家會將 raw byte 整批複製去 `Stream.Null`，唔再逐行解碼同派發 ffmpeg progress
callback，所以高負載下密集診斷都唔會食晒正常儲存時間。Stop 流程嘅 `q` 指令、正常
等候同強制退出等候仍然有明確時限；強制或者未能確認停止會如實顯示失敗。

**EN —** Registry Editor now calls a result-returning delete boundary. It
shows “Value deleted” only after the registry write succeeds; denied, missing,
or concurrent failures remain visible to the operator. Existing non-UI
`RegistryHelper.DeleteValue` callers retain their intentionally best-effort
cleanup behavior.

**粵語 —** 登錄編輯器而家會呼叫會回傳結果嘅刪除邊界。只有登錄檔寫入成功先會顯示
「已刪除值」；拒絕存取、遺失或者同時修改等失敗會如實畀操作員見到。既有非 UI 嘅
`RegistryHelper.DeleteValue` 呼叫者仍然保留原本刻意嘅 best-effort 清理行為。

## Safe Regression Evidence · 安全回歸證據

`dotnet run --project tests/RecorderRegistrySafety.Tests -c Debug` passed **10/10**:

- managed stderr-drain startup and a process-free bulk-copy probe over 10,000 diagnostic lines;
- graceful, forced, and still-running recorder stop outcomes;
- a never-completing fake process wait, proving the outer deadline returns;
- registry delete success and denied-delete result mapping, using a fake backend only;
- preservation of the old best-effort delete call shape.
- source-level wiring that Screen Recorder enters the managed lifecycle and Registry Editor gates its success notice on the result API.

The unchanged `ScreenRecorderLifecycle.Tests` fixture reproduced the aggregate
failure on base `ec7c4bcb8`: the complete 29-project runner failed only that
project with `Stop did not report the fixture as saved`. A captured-output
stress loop then failed **5/12** base runs. With the byte drain, the same
unchanged loop passed **12/12**, and the focused fixture passed **1/1**.

未改動嘅 `ScreenRecorderLifecycle.Tests` fixture 喺 base `ec7c4bcb8` 重現問題：
29 個 project aggregate runner 只係呢個 project 報 `not saved`；captured-output
stress loop base 亦有 **5/12** 失敗。改用 byte drain 後，同一個未改 fixture
**12/12** 全過，focused fixture 亦 **1/1** 通過。

**EN —** No ffmpeg process was launched, no recording was created, and no live
registry key/value was opened for modification by this regression suite.

**粵語 —** 呢個回歸套件冇啟動 ffmpeg、冇建立錄影，亦冇開啟或者修改任何實際登錄檔
機碼／值。

## Visual Evidence · 視覺證據

**EN —** This repair changes only the process adapter, focused tests, and
documentation. No XAML, layout, localization, accessibility surface, or other
visible state changed, so no screenshot was required or replaced. The existing
canonical image is not claimed as evidence for this lifecycle repair.

**粵語 —** 今次只改 process adapter、專項測試同文件；冇改 XAML、版面、本地化、
無障礙介面或者其他可見狀態，所以毋須亦冇替換截圖。既有 canonical 圖唔會冒充今次
lifecycle 修正嘅證據。
