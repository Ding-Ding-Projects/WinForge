# Browser launch workbench · 瀏覽器啟動工作台

## Behavior · 行為

The workbench sits above Browser Control's quick-action catalog at `--page browser`. Select Chrome or Edge, choose a real profile, enter an HTTP(S) URL, and use the focused launch groups:

- app-window and full-screen kiosk launches;
- a selected on-disk profile or installed PWA;
- browser flags and enterprise-policy inspection pages;
- an isolated proxy session with an optional bypass list;
- a GUID-scoped throwaway session;
- validated enable/disable Chromium feature names;
- loopback-only remote debugging with an isolated user-data directory;
- selected-profile `Cache` plus `Code Cache` cleanup; and
- exact Chrome/Edge install or upgrade through winget.

工作台放喺 `--page browser` 快捷操作目錄上面。揀 Chrome 或 Edge、真實設定檔同 HTTP(S) 網址之後，可以開 App 視窗／Kiosk、指定設定檔／PWA、flags／policy 頁、隔離 Proxy、用完即棄 session、功能旗標、loopback 遠端除錯、設定檔快取清理，同 winget 安裝／更新。

## Configuration · 設定

The selected browser, bypass list, feature mode/names, debug port, and per-browser profile directory are persisted in WinForge settings. URL and proxy-server fields are deliberately session-only because either can contain sensitive tokens. Embedded URL/proxy credentials are rejected; configure an authenticated proxy through an appropriate protected Windows/browser mechanism instead.

揀選瀏覽器、略過清單、功能模式／名稱、除錯埠，同每個瀏覽器設定檔資料夾會保存到 WinForge settings。網址同 Proxy 伺服器只會留喺今次 session，因為兩者都可能有敏感 token；內嵌網址／Proxy 憑證亦會被拒絕。

Profiles come from Chrome/Edge `User Data/Local State`, where `profile.info_cache` maps directory names to display names. PWAs come from user/common Start-menu `.lnk` files; WinForge reads their existing `--app-id` and `--profile-directory` values and never invents IDs.

設定檔名稱來自 Chrome／Edge `User Data/Local State` 嘅 `profile.info_cache`。PWA 來自使用者／共用開始功能表 `.lnk`；WinForge 只會讀現有 `--app-id` 同 `--profile-directory`，唔會作一個 ID 出嚟。

## Failure modes · 失敗情況

- A missing browser returns a clear result and leaves inputs untouched; install it with the review-first winget action if appropriate.
- Browser launches fail closed while WinForge is elevated, so Chrome/Edge never inherit administrator rights; restart WinForge normally and retry.
- Invalid/non-HTTP URLs, malformed proxies/bypass entries, invalid feature names, and ports outside 1024–65535 fail before process creation.
- A PWA list can be empty when no compatible Chrome/Edge Start-menu shortcut exists.
- Cache cleanup fails closed if any selected-browser process is running, the profile escapes its user-data root, or a cache tree contains a reparse point.
- An isolated session can leave its owned directory behind if Windows/browser file locks outlive bounded exit cleanup. A later Browser Control launch retries stale owned directories only while both supported browsers are closed.
- winget can report source, network, policy, or permission errors; its real exit code/output is surfaced.

缺少瀏覽器、WinForge 正用管理員權限、輸入無效、冇 PWA、瀏覽器未關、路徑唔安全、檔案鎖、winget 網絡／政策／權限問題都會如實失敗，唔會扮成功。

## Security and privacy · 安全同私隱

All user values cross the process boundary as separate `ProcessStartInfo.ArgumentList` entries; no browser launch uses `cmd.exe`, PowerShell, or concatenated shell input. App/kiosk/profile/PWA plans are bounded. Proxy, feature, debug, and throwaway launches always use a fresh directory below `%TEMP%\WinForge\BrowserSessions` and add `--no-first-run --disable-sync`. Profile, cache, isolated-session, and owned-root reparse points are rejected before launch or deletion.

所有用戶值都係獨立 `ArgumentList` 參數；瀏覽器啟動唔會經 `cmd.exe`、PowerShell 或串接 shell 輸入。Proxy、功能、除錯同用完即棄 session 一律用 `%TEMP%\WinForge\BrowserSessions` 下面嘅全新資料夾，亦會加 `--no-first-run --disable-sync`。

Remote debugging binds to `127.0.0.1` only. It still grants powerful browser automation access to local processes while the session runs, so use it only when needed and close that browser afterward. Cache deletion is explicitly confirmed and limited to the selected profile's `Cache` and `Code Cache`. Browser package changes are also review-first and use exact `Google.Chrome` / `Microsoft.Edge` winget IDs.

遠端除錯只綁定 `127.0.0.1`，但 session 運行期間本機程序仍然可以攞到強大瀏覽器控制權，所以用完要關。快取刪除要明確確認，只限揀選設定檔嘅 `Cache` 同 `Code Cache`；瀏覽器套件變更亦要先確認。

## Verification · 驗證

Run:

```powershell
dotnet run --project tests\BrowserControl.Tests -c Debug
powershell -ExecutionPolicy Bypass -File tools\Test-RoadmapCoreAudit.ps1
powershell -ExecutionPolicy Bypass -File .agents\skills\winforge-exhaustive-smoke\scripts\Test-WinForgeXamlLiteralSafety.ps1 -RepoRoot .
powershell -ExecutionPolicy Bypass -File .agents\skills\run-winforge\driver.ps1 -Page browser -Out docs\screenshot-browser-control.png -WaitMs 12000
```

The focused harness covers URL boundaries, Local State profile mapping, PWA shortcut parsing/deduplication, flags/policy pages, cache containment and browser-closed refusal, proxy argument boundaries, GUID cleanup, feature-name validation, loopback remote debugging, and exact winget plans. Package installation, cache deletion, and real remote-debug launches are not performed by the harness.

專項測試會用 disposable fixture 驗證以上合約；唔會真係安裝套件、刪用戶快取或者開遠端除錯瀏覽器。

The inspected app-owned visual evidence is `docs/screenshot-browser-control.png` at 1033×637 (SHA-256 `400AF4B89FE16B6A22023BE1259442D8D1A0BF88C39C0445C9A7E7DFE161FB3C`) plus `docs/screenshot-browser-control-narrow.png` at 784×691 (SHA-256 `BDB186204A24F1AFFF927F1347E315A77FBCCD218D8B09D7423C7E4282DF94B3`). Both were rendered on a dedicated LowLevel headless desktop; labels wrap cleanly without overlap, and no action that launches a browser, deletes cache, changes a package, or opens debugging was invoked.

已檢視嘅 app-owned 證據包括 1033×637 正式圖同 784×691 窄版圖（SHA-256 如上）；兩張都喺專用 LowLevel headless desktop 擷取，雙語標籤正常換行、冇重疊，亦冇執行瀏覽器、清快取、改套件或者開除錯。
