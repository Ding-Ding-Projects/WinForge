# Volume Mixer · 音量混合器

## Behavior and configuration · 行為同設定

The page enumerates active playback endpoints and Core Audio sessions, changes master or per-session volume/mute state, selects the system default endpoint, and requests a per-app endpoint route. Choosing “System default” clears the app-specific route instead of persisting an empty device identifier. All work stays on the local Windows host. · 呢頁列出有效播放 endpoint 同 Core Audio session，可改主音量、逐 session 音量／靜音、系統預設 endpoint，同要求逐 app endpoint 路由。揀「系統預設」會清除 app 專用路由，唔會保存空白裝置識別碼。所有操作只留喺本機 Windows。

## Failure modes and security · 故障模式同安全

COM activation and every returned interface are checked before use. A missing endpoint, unsupported undocumented routing contract, disappearing session, or rejected default-device call fails closed with a localized result; it never redirects a different process. WinForge does not record audio, transmit audio content, or persist endpoint/session identifiers on this page. · 使用前會檢查 COM 啟動同每個回傳介面。缺少 endpoint、未公開路由合約唔支援、session 消失，或者預設裝置呼叫被拒，都會以本地化結果安全停止，絕不改到另一個程序。WinForge 呢頁唔會錄音、傳送音訊內容，亦唔會保存 endpoint／session 識別碼。

## Accessibility and verification · 無障礙同驗證

Device controls stack vertically at constrained width. Icon actions expose localized automation names and tooltips; sliders are keyboard reachable; action targets are at least 44 px. The managed project publishes with zero compiler warnings after the nullable COM audit. Fresh live-tree captures were inspected at 1284×811 and 784×691 with no clipped, overlapping, or off-screen mixer content. The headless test desktop exposed no audio endpoint, so the inspected error state is expected and no synthetic session data was introduced. · 窄畫面裝置控制會直排；圖示操作有本地化 automation name 同 tooltip、slider 可用鍵盤到達、操作範圍最少 44 px。Nullable COM audit 後 managed project publish 零 compiler warning。已檢視 1284×811 同 784×691 即時 visual-tree capture，mixer 內容冇裁切、重疊或走出畫面。Headless 測試 desktop 冇 audio endpoint，所以已檢視錯誤狀態屬預期，而且冇加入虛構 session 資料。
