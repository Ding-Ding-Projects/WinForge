# Shared settings and School mode · 共用設定同 School mode

## Behavior · 行為

WinForge stores the emoji-message preference, School-mode state, the user-selected School-mode display name, and the previous language choice in the shared WinForge settings file under the current user's LocalAppData directory. Open WinForge windows observe the same file and receive change notifications, so a setting change is applied without requiring a restart where the current surface can update live. · WinForge 將 emoji 訊息偏好、School mode 狀態、用戶改名同之前語言選擇放喺目前用戶 LocalAppData 下面嘅共用 WinForge 設定檔。已開啟嘅 WinForge 視窗會睇同一份檔案同收到變更通知，所以支援即時更新嘅介面唔使重啟。

The emoji switch affects decorative emoji in notification/dialog copy only. It never changes button labels, accessible names, facts, or command identifiers. It defaults to enabled and is persisted as a boolean setting. · Emoji 開關只會影響通知／對話文字嘅裝飾 emoji，唔會改按鈕名、accessible name、事實或者 command identifier。預設開啟，並以 boolean 設定保存。

When School mode is enabled, WinForge forces English presentation and removes the language, funny-level, personal-vocabulary, and dim-sum controls from the Settings surface. The previous language choice remains stored and is restored after a successful unlock. The display name is user-renamable and replaces the shipped label on this surface. · 開啟 School mode 後，WinForge 強制用英文，並從設定介面移除語言、搞笑等級、個人詞彙同點心控制。之前嘅語言選擇會保存，成功解鎖後還原。顯示名稱可以由用戶改，設定介面會用用戶揀嘅名稱。

Narration is a separate opt-in control and defaults to off. When enabled, notification events are debounced and rate-limited per category, queued through the single announcement pump, and superseded queued lines in the same category are replaced. The user chooses English, Cantonese, or both; School mode suppresses narration. · 旁白係另一個選擇性開關，預設關閉。開啟後通知事件會按分類 debounce 同限制頻率，經單一廣播隊列序列化，同分類排緊隊嘅舊句會由新句取代。用戶可以揀英文、粵語或者兩種；School mode 會停旁白。

## Unlock and recovery · 解鎖同重設

The unlock value is stored in the current-user Windows credential vault under a stable WinForge resource name. It is not exported, written to JSON, placed in history, logged, or sent to a service. A successful verification is required before School mode can be turned off. · 解鎖值放喺目前用戶 Windows credential vault 嘅固定 WinForge resource 名稱下面，唔會匯出、寫入 JSON、放入 history、寫 log 或傳去服務。關閉 School mode 前必須成功驗證。

This is a user-experience lock, not encryption or protection from another person using the machine. Deleting the local WinForge application-data folder resets the local record; the app states that recovery route beside the control. · 呢個係使用體驗鎖，唔係加密，亦唔可以防止其他人使用部機。刪除本機 WinForge application-data folder 可以重設記錄，控制旁邊會講明呢條路。

## Failure modes and security · 失敗處理同安全

- A malformed or missing setting falls back to the compiled-in value and the settings surface identifies that provenance.
- A vault read/write problem leaves School mode enabled or reports that the value was not saved; it never places the value in a settings file as a fallback.
- An incorrect unlock value leaves the mode enabled and names the vault/folder recovery route.
- The shared file watcher is best-effort. Direct changes still reload the setting store; if a watcher cannot be created, the control must not claim that cross-window live propagation is available.

## Verification · 驗證

The implementation is in `Services/UniversalSettingsService.cs` and the settings surface is in `Pages/SettingsPage.xaml.cs`. The local build verifies the vault/API wiring compiles; headless UI verification must exercise both the enabled and unlocked states. · 實作喺 `Services/UniversalSettingsService.cs`，設定介面喺 `Pages/SettingsPage.xaml.cs`。本機 build 會驗證 vault/API wiring 可以編譯；headless UI 驗證要覆蓋開啟同成功解鎖兩個狀態。

## Suggested articles · 建議文章

- [Offline changelog](offline-changelog.md) — the local history viewer.
- [Pinned tabs](pinned-tabs.md) — the session state that shares the same local persistence boundary.
- [Managed release contract](../delivery/managed-release-contract.md) — update warnings and restart behavior.
