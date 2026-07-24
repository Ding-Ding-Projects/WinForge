# Notification Centre · 通知中心

WinForge now has one shell-level place for non-blocking information, success, progress, warnings, and errors. Active cards stack without covering each other in the bottom-right corner; informational and success cards leave automatically, while warnings and errors wait for an explicit dismiss. · WinForge 而家有一個外殼級位置顯示非阻塞資訊、成功、進度、警告同錯誤。通知卡會喺右下角有序堆疊；資訊／成功會自動離開，警告／錯誤就等用戶明確關閉。

## Using it · 點用

1. Read or act on an active notification. A notification may expose one button or a compact action menu.
2. Select the bell/history button at the bottom-right to review recent notices.
3. Use **Clear dismissed** to remove old history. Active warnings/errors remain visible until dismissed.

1. 閱讀或者操作目前通知；通知可以有一個按鈕或者精簡動作選單。
2. 按右下角鐘／記錄掣翻查近期通知。
3. 用 **清除已關閉記錄** 移除舊記錄；仍然生效嘅警告／錯誤會保持可見。

The newest 200 review entries stay only in the current Windows profile. App updates and package-manager work use the shared centre even when a Windows desktop toast is unavailable. · 最新 200 個翻查項目只會留喺目前 Windows 使用者設定檔。就算 Windows desktop toast 用唔到，App 更新同套件管理工作仍然會用共用中心。

## Safety and accessibility · 安全同無障礙

Notification content is bounded; callbacks are never persisted; only explicit HTTP(S) links can be opened. Producers can keep sensitive notices session-only. Screen readers receive polite or assertive live announcements according to severity, the history button names its unread count, every card is dismissible by keyboard, and narrow bilingual text wraps inside a scrolling surface. · 通知內容有上限、callback 永遠唔保存，亦只會開啟明確 HTTP(S) 連結。敏感通知可以只留今次 session。螢幕閱讀器會按嚴重程度收到 polite／assertive 即時公告；記錄掣會讀出未讀數，每張卡可以用鍵盤關閉，窄版雙語文字亦會喺可捲動介面換行。

## Evidence · 證據

The deterministic core harness passes **16/16** without starting the app or changing the live system. Fresh normal/narrow LowLevel headless screenshots and their hashes will be recorded here after final shell validation. · 確定性 core harness **16/16** 通過，唔會啟動 app 或者改動真實系統。完成外殼驗證後，會喺呢度記錄最新正常／窄版 LowLevel headless 截圖同 hash。

Implementation and failure-mode details live in the [feature reference](../features/application-shell/notification-centre.md). · 實作同失敗情況詳情請睇[功能參考](../features/application-shell/notification-centre.md)。
