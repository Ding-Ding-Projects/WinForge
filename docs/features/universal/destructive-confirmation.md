# Destructive-action super confirmation · 破壞性動作超級確認

## Behavior · 行為

Destructive actions routed through the shared surface identify the exact affected operation, require two independently entered keys, enable a full-range authorization slider only after both keys match, and keep an Emergency exit/Escape cancellation path. The progress bar reflects the slider value and the final authorization state is distinct from ordinary informational notifications. · 經共用介面處理嘅破壞性動作會講清楚受影響嘅準確操作，要求輸入兩條獨立匙，兩條匙啱晒先會開啟完整授權滑桿，並保留緊急離開／Escape 取消路線。進度條反映滑桿數值，最後授權狀態同普通通知分開。

The control is a local user-experience guard, not encryption or a security boundary. It is used by the shared tweak-row action route, bulk file operations, browser profile-cache deletion, scheduled-rule removal, authenticator-entry removal, and Support Tickets bulk deletion as each surface migrates to the common contract. · 呢個控制係本機用戶體驗 guard，唔係加密，亦唔係安全邊界。共用 tweak row 動作、批量檔案操作、瀏覽器設定檔快取刪除、排程規則移除、驗證器項目移除，同支援工單批量刪除會使用呢個合約，其他介面逐步遷移。

## Failure and privacy · 失敗同私隱

One key, a partial slider, Escape, Emergency exit, or a dialog error performs no destructive action. Key values are compared in memory for the dialog and never written to settings, logs, exports, history, or public records. · 只輸入一條匙、滑桿未完成、Escape、緊急離開或者對話框出錯，都唔會執行破壞性動作。匙值只喺對話框記憶體內比較，永遠唔會寫入設定、log、匯出、歷史或者公開紀錄。

## Verification · 驗證

`Controls/SuperConfirmationDialog.cs` is the native implementation. `tests/ManagedReleaseContract.Tests/Program.cs` checks the two-key, full-slider, Emergency exit, and shared-caller source contract. The full WinUI solution build is the compilation gate; focused UI driving remains part of the real-artifact screenshot capture pass. · `Controls/SuperConfirmationDialog.cs` 係原生實作。`tests/ManagedReleaseContract.Tests/Program.cs` 檢查兩條匙、完整滑桿、緊急離開同共用 caller source contract。完整 WinUI solution build 係編譯 gate；focused UI driving 會喺真實 artifact screenshot pass 處理。

## Built-artifact evidence · 真實建置證據

The Settings capture in the repository README shows the shared experience controls in the real
build. The dialog itself is exercised by the source contract and by the migrated destructive
callers; a dedicated dialog capture is still pending while the remaining callers are migrated.
· Repository README 入面嘅 Settings 擷取顯示真實 build 嘅共用體驗控制。對話框本身由 source contract 同已遷移破壞性 caller 驗證；其餘 caller 遷移期間，專門 dialog 擷取仍然待做。

## Suggested articles · 建議文章

- [Support Tickets](support-tickets.md) — local bulk deletion and recovery disclosure.
- [Scheduled settings](scheduled-settings.md) — schedule-rule removal and rollback semantics.
- [Shared settings](shared-settings.md) — the common language, accessibility, and notification contract.
