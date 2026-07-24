# Notification centre · 通知中心

## Behavior · 行為

NotificationHost anchors a maximum four-notice stack in the shell's bottom-right corner. Informational and success notices close automatically; warnings and errors remain until dismissed. Progress notices normally close after a bounded interval, while a caller can keep a genuinely in-flight operation open and replace it later through a stable channel key. Overflow notices leave the visible stack but remain in history, so cards never overlap indefinitely.

NotificationHost 將最多四個通知疊喺外殼右下角。資訊同成功通知會自動關閉；警告同錯誤會留低直到用戶關閉。進度通知一般有限時自動關閉，真正進行中嘅工作亦可以用穩定 channel key 保持顯示，再由完成結果取代。超出可見堆疊嘅通知仍然會留喺記錄，避免卡片無限重疊。

The shell button opens a keyboard-accessible history flyout. The newest 200 notices are retained, newest first; opening the centre marks them viewed, and **Clear dismissed** removes old entries without hiding active notices. Toasts and the centre re-render when the persisted English, Cantonese, or bilingual language mode changes.

外殼按鈕會開啟鍵盤可用嘅記錄 flyout。最新 200 個通知會按新至舊保留；開啟中心會標記為已讀，而 **清除已關閉記錄** 唔會收埋仍然生效嘅通知。切換持久化英文、粵語或者雙語模式時，通知同中心會即時重畫。

## Configuration and API · 設定同 API

Call AppNotificationService.Publish(AppNoticeDraft) with separate English and Cantonese title/body text, a severity, and an optional stable key, duration, persistence flag, and up to three actions. HTTP(S) links and in-process callbacks are supported. Callbacks are never serialized. Use PersistInHistory: false for sensitive or ephemeral content.

呼叫 AppNotificationService.Publish(AppNoticeDraft) 時要分開提供英文／粵語標題同內容、嚴重程度，亦可以提供穩定 key、顯示時間、是否保存，同最多三個動作。支援 HTTP(S) 連結同 process 內 callback；callback 永遠唔會序列化。敏感或者短暫內容要用 PersistInHistory: false。

App-update progress/results and package-manager progress/success/failure now use this bus. Windows package toasts remain a best-effort mirror; the in-app centre still works when the unpackaged app cannot register an operating-system toast.

App 更新進度／結果同套件管理器進度／成功／失敗而家會用呢條 bus。Windows 套件 toast 只係盡力 mirror；未封裝 app 註冊唔到系統 toast 時，app 內中心仍然可用。

## Failure modes · 失敗情況

- Damaged or incompatible persisted JSON is ignored and starts with an empty history; it never blocks app startup.
- Text, actions, auto-dismiss time, active stack, and history count are bounded.
- Stable-key replacements keep one active card. A timer belonging to an older revision cannot dismiss its replacement.
- Only absolute HTTP(S) action links survive normalization. Invalid file, device, and other schemes become non-actionable.
- Persistence failures degrade to session-only notices.
- Page-local InfoBar migration is ongoing. Until each module is converted, its existing local status surface remains the truthful source for that operation.

損壞記錄、過長輸入、舊 timer、危險 link 同寫入失敗都會安全降級；唔會阻止 app 啟動。逐頁 InfoBar 遷移仍然進行中，未轉換模組會繼續以現有本地狀態介面如實顯示結果。

## Security, privacy, and accessibility · 安全、私隱同無障礙

History is local to %LOCALAPPDATA%\WinForge\settings.json; WinForge does not transmit it. Producers must opt out for secrets, access tokens, private samples, or other sensitive content. Titles and bodies are bounded and NUL-stripped. Links are explicit buttons and are never opened automatically.

記錄只會留喺 %LOCALAPPDATA%\WinForge\settings.json，WinForge 唔會傳送。秘密、access token、私人 sample 或其他敏感內容必須 opt out。標題／內容有長度上限兼會移除 NUL；連結一定要由用戶明確按掣，永遠唔會自動打開。

Every active card has a screen-reader name and a close target. Information/success/progress use polite live announcements; warnings/errors use assertive announcements. The centre button exposes its unread count, controls meet the shell's minimum hit-target, bilingual copy wraps, and the flyout scrolls rather than clipping.

每張通知卡都有螢幕閱讀器名稱同關閉目標。資訊／成功／進度用 polite live announcement；警告／錯誤用 assertive。中心按鈕會報未讀數、控制符合最小點擊尺寸、雙語文字會換行，記錄亦會捲動而唔會裁切。

## Verification · 驗證

    dotnet run --project tests\NotificationCenter.Tests\NotificationCenter.Tests.csproj -c Debug
    dotnet build WinForge.sln -c Debug -p:Platform=x64
    powershell -ExecutionPolicy Bypass -File .agents\skills\winforge-exhaustive-smoke\scripts\Test-WinForgeXamlLiteralSafety.ps1 -RepoRoot .
    powershell -ExecutionPolicy Bypass -File .agents\skills\winforge-exhaustive-smoke\scripts\Test-WinForgeSourceSurfaceAudit.ps1 -RepoRoot .

The pure harness covers 16 contracts: default lifetimes, persistent warning/error/progress behavior, stable replacement, stack/history bounds, retained dismissal, stale timers, unread state, clearing, restart restore, input/action bounds, safe links, and persistence opt-out. Visual evidence is recorded on the [wiki page](../../wiki/Notification-Centre.md).

純 harness 覆蓋 16 項合約：預設顯示時間、持續警告／錯誤／進度、穩定取代、有界堆疊／記錄、關閉後保留、舊 timer、未讀狀態、清理、重啟還原、輸入／動作上限、安全連結，同保存 opt-out。畫面證據記錄喺 [wiki 頁](../../wiki/Notification-Centre.md)。
