# Screen Recorder & Registry Editor Reliability · 螢幕錄影與登錄編輯器可靠性

Date · 日期：2026-07-11
Scope · 範圍：bounded ffmpeg shutdown and truthful Registry Editor value deletion only. · 只限有時限嘅 ffmpeg 停止流程同登錄編輯器如實回報值刪除。

## Outcome · 結果

**EN —** Screen Recorder now begins a managed, discarded stderr drain before
the recording session is exposed. Its Stop path gives the `q` command, the
graceful wait, and the forced-exit wait explicit deadlines. A forced or
unconfirmed stop is reported as a failure, never as a successfully saved
recording.

**粵語 —** 螢幕錄影而家會喺公開錄影 session 之前，以受管理方式排走並丟棄 stderr
輸出。Stop 流程會為 `q` 指令、正常等候同強制退出等候設定明確時限。強制或者未能確認
嘅停止會如實顯示為失敗，絕對唔會當成已成功儲存錄影。

**EN —** Registry Editor now calls a result-returning delete boundary. It
shows “Value deleted” only after the registry write succeeds; denied, missing,
or concurrent failures remain visible to the operator. Existing non-UI
`RegistryHelper.DeleteValue` callers retain their intentionally best-effort
cleanup behavior.

**粵語 —** 登錄編輯器而家會呼叫會回傳結果嘅刪除邊界。只有登錄檔寫入成功先會顯示
「已刪除值」；拒絕存取、遺失或者同時修改等失敗會如實畀操作員見到。既有非 UI 嘅
`RegistryHelper.DeleteValue` 呼叫者仍然保留原本刻意嘅 best-effort 清理行為。

## Safe Regression Evidence · 安全回歸證據

`dotnet run --project tests/RecorderRegistrySafety.Tests -c Debug` passed **9/9**:

- managed stderr-drain startup;
- graceful, forced, and still-running recorder stop outcomes;
- a never-completing fake process wait, proving the outer deadline returns;
- registry delete success and denied-delete result mapping, using a fake backend only;
- preservation of the old best-effort delete call shape.
- source-level wiring that Screen Recorder enters the managed lifecycle and Registry Editor gates its success notice on the result API.

**EN —** No ffmpeg process was launched, no recording was created, and no live
registry key/value was opened for modification by this regression suite.

**粵語 —** 呢個回歸套件冇啟動 ffmpeg、冇建立錄影，亦冇開啟或者修改任何實際登錄檔
機碼／值。

## Visual Evidence · 視覺證據

**EN —** Fresh self-contained capture attempts ran for `recorder` with
`-Publish -WaitMs 15000` and for `regedit` with `-WaitMs 15000`. Both reached
the driver capture stage, where `CopyFromScreen` was unavailable and the
`PrintWindow` fallback produced a uniform frame; graphics capture is unavailable
in this desktop session. No PNG was created, inspected, replaced, or reused.
The follow-up `-NoCapture` launches for both routes passed. No Record, Stop,
registry deletion, or other live action was invoked.

**粵語 —** 已為 `recorder` 用 `-Publish -WaitMs 15000` 同為 `regedit` 用
`-WaitMs 15000` 跑新嘅 self-contained 截圖嘗試。兩個都去到 driver capture
階段，但 `CopyFromScreen` 未可用，`PrintWindow` fallback 亦產生 uniform frame；
呢個 desktop session 嘅 graphics capture 未可用。冇 PNG 被產生、檢查、替換或者
重用。之後兩條 route 嘅 `-NoCapture` launch 都通過。冇撳 Record、Stop、登錄檔刪除，
亦冇執行其他 live action。

These are `capture-blocked` results, never visual-pass claims. The matching
campaign and gallery entries are in `docs/wiki/Smoke-Test-Campaign.md` and
`docs/wiki/Screenshots.md`; old canonical screenshots are not evidence for this
repair.

呢啲係 `capture-blocked` 結果，絕對唔係 visual-pass 聲稱。對應嘅 campaign 同
gallery 記錄喺 `docs/wiki/Smoke-Test-Campaign.md` 同 `docs/wiki/Screenshots.md`；
舊 canonical 截圖絕對唔會當成呢個修正嘅證據。
