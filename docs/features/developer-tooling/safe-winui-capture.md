# Safe WinUI automation capture · 安全 WinUI 自動截圖

## Contract · 合約

DEBUG builds may opt in with an absolute `.png` path in `WINFORGE_CAPTURE_PATH`. `WINFORGE_CAPTURE_DELAY_MS` is bounded to 1–30 seconds; optional width/height values are bounded to 640–3840 and 480–2160. After the shell loads, WinForge renders its real root visual through `RenderTargetBitmap`, encodes an opaque PNG, and logs only dimensions. Release builds and launches without the path do nothing. · DEBUG build 可以用 `WINFORGE_CAPTURE_PATH` 絕對 `.png` 路徑明確啟用；delay 限 1–30 秒，可選闊／高限 640–3840 同 480–2160。Shell 載入後，WinForge 用 `RenderTargetBitmap` 輸出真實 root visual、編碼不透明 PNG，只記錄尺寸。Release build 或冇 path 嘅 launch 完全唔做嘢。

## Safety and failure modes · 安全同故障模式

The repository driver creates a unique temporary path, restores its parent environment immediately after launch, validates dimensions and color diversity, promotes only a valid image, deletes the temporary file, and stops the original process through its retained handle rather than a reusable PID. It never uses `CopyFromScreen`; this prevents an overlapping app from leaking into WinForge evidence. A targeted `PrintWindow` attempt is the only fallback and blank frames are rejected. Capture exceptions are logged but can never crash the app. · Repo driver 建立唯一 temp path、launch 後即時還原 parent environment、驗證尺寸同色彩多樣性、只升格有效圖片、刪除 temp file，並經保留嘅原始 process handle 停止自家 app，唔會追一個可重用 PID。佢永遠唔用 `CopyFromScreen`，避免遮住 WinForge 嘅其他 app 漏入證據；唯一後備係針對自家 HWND 嘅 `PrintWindow`，空白畫面會被拒絕。Capture 例外只會記錄，唔可以拖冧 app。

## Verification · 驗證

The PowerShell driver parses cleanly and completed a process-owned `mixer` capture through the live-tree path. Separate LowLevel headless runs produced and inspected current 1284×811 and 784×691 mixer frames, including correct translucent M3 surfaces after switching the PNG encoder to opaque composited pixels. · PowerShell driver parser 全過，亦經 live-tree 路徑完成 process-owned `mixer` capture。獨立 LowLevel headless run 已輸出並檢視目前 1284×811 同 784×691 mixer 圖；PNG encoder 改用不透明合成 pixels 後，半透明 M3 surface 顯示正確。
