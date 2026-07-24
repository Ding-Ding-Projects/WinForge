# Screen Recorder lifecycle reliability · 螢幕錄影 lifecycle 可靠性

## Behavior · 行為

Screen Recorder launches the discovered ffmpeg executable directly with
`UseShellExecute = false`, captures the whole desktop through `gdigrab`, encodes
H.264 MP4, and clamps frame rate to 5–60 fps. Stop sends ffmpeg `q`, waits up to
eight seconds for a clean container finalization, and uses a bounded forced-stop
path only when the encoder does not exit. · 螢幕錄影會直接啟動已發現嘅 ffmpeg，
用 `gdigrab` 錄全桌面並輸出 H.264 MP4，幀率限制 5–60 fps。Stop 會傳送 `q`，
最多等八秒完成正常封裝；encoder 真係唔退出先行有時限強制停止。

ffmpeg writes dense progress diagnostics to stderr. WinForge bulk-copies those
raw bytes to `Stream.Null` for the lifetime of the owned process. It does not
decode or dispatch a callback per line, which prevents pipe back-pressure and
keeps diagnostic volume from consuming the graceful-save budget. · ffmpeg 會喺
stderr 寫密集進度；WinForge 會喺自家 process 生命週期內將 raw byte 整批複製去
`Stream.Null`，唔會逐行解碼／派 callback，避免 pipe back-pressure 同診斷量食晒
正常儲存時間。

## Configuration · 設定

- Output is an operator-selected `.mp4` path; the default is a timestamped file
  in the Videos folder. · 輸出係操作員揀嘅 `.mp4`，預設放 Videos 並加時間戳。
- Frame rate is clamped to 5–60 fps even if a caller supplies a value outside
  the UI range. · 就算 caller 傳入 UI 範圍外數值，幀率都會限制喺 5–60 fps。
- ffmpeg is resolved by `MediaService`; a missing engine is a localized failure
  and the page offers the existing explicit install flow. · ffmpeg 由
  `MediaService` 尋找；搵唔到會本地化報錯，頁面保留明確安裝流程。

## Failure modes · 失敗模式

- Failure to start ffmpeg leaves no retained recorder process. · ffmpeg 啟動失敗
  唔會留低 recorder process。
- Failure to send `q` triggers bounded cleanup and reports that the file may be
  incomplete. · 傳送唔到 `q` 會做有時限清理，並提示檔案可能唔完整。
- An encoder that ignores `q` is terminated after the graceful deadline and is
  never reported as saved. · encoder 無視 `q` 會喺正常期限後終止，絕對唔會報已儲存。
- A process that still cannot be confirmed exited remains retained so a later
  Stop can retry; it is not disposed or falsely released. · 如果仍確認唔到 process
  已退出，就會保留俾下一次 Stop 重試，唔會假裝釋放。

## Security and privacy · 安全同私隱

Whole-desktop recording can capture notifications, credentials, and private
windows; recording is always an explicit operator action. WinForge does not use
a shell to construct the ffmpeg command and owns only the process it launched.
The MP4 path is visible in the page, and no recording or diagnostic text is
uploaded by this service. · 全桌面錄影可能錄到通知、credential 同私人視窗，所以
一定要操作員明確開始。WinForge 唔會經 shell 組 ffmpeg command，只管理自己啟動嘅
process；MP4 路徑會喺頁面顯示，呢個 service 唔會上載錄影或診斷文字。

## Verification · 驗證

```powershell
dotnet run --project tests\RecorderRegistrySafety.Tests -c Debug
dotnet run --project tests\ScreenRecorderLifecycle.Tests -c Debug
```

The process-free suite covers the bulk stream sink plus bounded graceful,
forced, still-running, and never-completing outcomes (**10/10**). The separate
Windows fixture launches only an isolated temporary `cmd.exe` script, emits
10,000 stderr lines, accepts `q`, and proves the production adapter reports a
clean save (**1/1**). No real ffmpeg recording or desktop capture occurs. ·
Process-free suite 會驗證 bulk stream sink，同正常、強制、仍運行、永不完成等有界結果
（**10/10**）。另一個 Windows fixture 只會啟動隔離臨時 `cmd.exe` script，寫
10,000 行 stderr、接收 `q`，並證明 production adapter 如實報正常儲存（**1/1**）；
全程冇真實 ffmpeg 錄影或桌面擷取。
