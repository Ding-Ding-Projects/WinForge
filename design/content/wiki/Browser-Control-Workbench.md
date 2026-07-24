# Browser Control Workbench · 瀏覽器控制工作台

Open **Tools → Browser Control** or deep-link with `WinForge.exe --page browser`. The parameterized workbench sits above the 100 catalog quick actions.

由 **Tools → Browser Control** 開，或者用 `WinForge.exe --page browser`。參數化工作台放喺 100 個 catalog 快捷操作上面。

## Shipped surface · 已交付介面

- configurable HTTP(S) app-window and full-screen kiosk launches;
- real Chrome/Edge profile names from `Local State`, with selected-profile launch;
- installed PWA discovery and launch from parsed Start-menu app IDs/profiles;
- flags plus policy pages for both browsers;
- browser-closed, profile-contained `Cache` + `Code Cache` cleanup;
- isolated proxy/bypass, throwaway, validated feature-switch, and loopback remote-debug sessions; and
- review-first exact-ID winget install/update.

已交付指定網址 App／Kiosk、真實設定檔、已裝 PWA、flags／policy、關閉瀏覽器後安全清快取、隔離 Proxy／用完即棄／功能開關／loopback 除錯，同先確認 winget 安裝／更新。

## Safety · 安全

Every browser value is a separate argument-vector entry. Inputs are bounded; no launch concatenates user text into a shell command. URL/proxy values are session-only, embedded credentials are rejected, and browser launches fail closed while WinForge is elevated. Isolated profiles are GUID-scoped below WinForge's owned temporary root and cleaned after the owned browser exits. Remote debugging binds to `127.0.0.1` only. Cache and package mutations require an explicit decision.

所有值都係獨立參數；輸入有限制，唔會串成 shell 指令。網址／Proxy 只留今次 session，內嵌憑證會被拒絕，WinForge 用管理員權限時亦唔會開瀏覽器。隔離設定檔喺 WinForge 自家 temp root 用 GUID 分隔，自家瀏覽器退出後清理。遠端除錯只綁 `127.0.0.1`；快取同套件變更要明確確認。

## Evidence · 證據

`tests/BrowserControl.Tests` covers 23 disposable-fixture contracts. See the repository feature guide for configuration and failure modes, and the [core roadmap audit](#/wiki/Roadmap-Core-Capability-Audit) for the 14/14 evidence ledger.

`tests/BrowserControl.Tests` 有 23 項 disposable-fixture contract；詳細設定／失敗情況喺 repository 功能指南，[核心路線圖審核](#/wiki/Roadmap-Core-Capability-Audit)記錄 14/14 證據。

![Browser Control workbench](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-browser-control.png)

Fresh inspected app-owned evidence: 1033×637, SHA-256 `400AF4B89FE16B6A22023BE1259442D8D1A0BF88C39C0445C9A7E7DFE161FB3C`; the 784×691 narrow companion is `docs/screenshot-browser-control-narrow.png` with SHA-256 `BDB186204A24F1AFFF927F1347E315A77FBCCD218D8B09D7423C7E4282DF94B3`. Both were captured on a dedicated LowLevel headless desktop without invoking browser, cache, package, or debugging actions. · 已檢視 1033×637 正式圖同 784×691 窄版圖（SHA-256 如上）；兩張都喺專用 LowLevel headless desktop 擷取，而且冇執行瀏覽器、快取、套件或者除錯操作。
