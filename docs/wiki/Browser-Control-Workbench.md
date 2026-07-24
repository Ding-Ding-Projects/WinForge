# Browser Control Workbench · 瀏覽器控制工作台

Browser Control now has a parameterized, accessible workbench above its 100 quick actions. Open **Tools → Browser Control** or run `WinForge.exe --page browser`.

瀏覽器控制而家喺 100 個快捷操作上面加入參數化、無障礙工作台。由 **Tools → Browser Control** 開，或者用 `WinForge.exe --page browser`。

## What it can do · 可以做乜

| Surface · 介面 | Shipped behavior · 已交付行為 |
|---|---|
| URL launch | Chosen HTTP(S) URL as Chrome/Edge app window or full-screen kiosk · 指定網址 App 視窗／Kiosk |
| Profiles | Reads `Local State/profile.info_cache`, shows real names, launches the selected directory · 讀真實設定檔名稱再開揀選資料夾 |
| PWAs | Parses installed Start-menu shortcuts for runtime app IDs/profiles, then launches the selection · 解析已裝 PWA 捷徑再開揀選項目 |
| Internal pages | Both flags and policy pages for Chrome and Edge · 兩個瀏覽器嘅 flags 同 policy 頁 |
| Cache | Deletes only selected-profile `Cache` and `Code Cache`, after confirmation and a browser-closed check · 確認同關閉檢查後只刪指定快取 |
| Isolated sessions | Proxy+bypass, throwaway, validated feature switches, and loopback remote debugging · Proxy／略過、用完即棄、功能開關、loopback 遠端除錯 |
| Packages | Review-first winget install/upgrade with exact Chrome/Edge IDs · 先確認再用正確 ID 安裝／更新 |

## Safety boundaries · 安全界線

- Browser launches use discrete argument vectors, never a user-built shell command. · 瀏覽器啟動只用獨立參數，唔會砌用戶 shell 指令。
- URL, proxy, bypass, feature, profile, PWA, and debug-port inputs are bounded and validated. · 所有輸入都有長度／格式限制。
- URL/proxy values are session-only, embedded credentials are rejected, and browser launch is disabled while WinForge is elevated. · 網址／Proxy 只留喺今次 session、內嵌憑證會被拒絕，而且 WinForge 用管理員權限時唔會開瀏覽器。
- Isolated profiles are GUID-scoped, tracked to owned-process exit, and cleaned within the owned root. · 隔離設定檔用 GUID，追蹤自家程序退出，再喺自家 root 清理。
- Remote debugging binds to `127.0.0.1`; close the session when finished. · 遠端除錯只綁 loopback，用完要關。
- Real cache/package mutations are explicit decisions and are not executed by the test harness. · 真實快取／套件變更一定要明確決定，測試唔會執行。

## Evidence · 證據

The 23-case `tests/BrowserControl.Tests` harness exercises the pure contracts with disposable fixtures. The [feature guide](../features/browser-control/browser-workbench.md) documents configuration, failure modes, and the exact verification commands. The [core roadmap audit](Roadmap-Core-Capability-Audit.md) now records Browser Control at 14/14.

23 項 `tests/BrowserControl.Tests` 用 disposable fixture 驗證 pure contract；[功能指南](../features/browser-control/browser-workbench.md)列出設定、失敗情況同準確 command；[核心路線圖審核](Roadmap-Core-Capability-Audit.md)而家記錄瀏覽器控制 14/14。

![Browser Control workbench](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-browser-control.png)

Fresh inspected app-owned evidence: 1033×637, SHA-256 `400AF4B89FE16B6A22023BE1259442D8D1A0BF88C39C0445C9A7E7DFE161FB3C`. A second 784×691 narrow capture (`docs/screenshot-browser-control-narrow.png`, SHA-256 `BDB186204A24F1AFFF927F1347E315A77FBCCD218D8B09D7423C7E4282DF94B3`) confirms readable bilingual wrapping without overlap. Both came from a dedicated LowLevel headless desktop and exercised no browser, cache, package, or debugging action. · 最新已檢視 app-owned 證據：1033×637 正式圖，同 784×691 窄版圖（SHA-256 如上）；雙語換行清楚、冇重疊，亦冇執行任何瀏覽器、快取、套件或者除錯操作。
