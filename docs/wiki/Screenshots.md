# Screenshots · 截圖

**EN —** The canonical screenshot set lives in the repo at [`docs/`](https://github.com/codingmachineedge/WinForge/tree/main/docs) as `screenshot-<key>.png`. README embeds them with repo-relative paths; wiki pages embed them with absolute `raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/...` URLs.

**粵語 —** 正式截圖放喺儲存庫嘅 [`docs/`](https://github.com/codingmachineedge/WinForge/tree/main/docs)，命名為 `screenshot-<key>.png`。README 用相對路徑嵌入；wiki 頁用絕對 `raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/...` 連結嵌入。

## How to capture · 點樣擷取

**EN**
1. Build Debug x64: `dotnet build WinForge.csproj -c Debug -p:Platform=x64`.
2. Launch the app and resize to a consistent window size; keep the default leading language so bilingual text shows naturally.
3. Navigate each NavigationView item, screenshot the content area, crop to the app window.
4. **Redact private info before saving:** IP addresses, tokens/API keys, usernames, emails, machine names, repo paths. Use placeholder values (`MYTUNNEL`, `app.example.com`, `http://localhost:8080`).
5. Save as `docs/screenshot-<key>.png`, overwriting the placeholder.

**粵語**
1. 編譯 Debug x64：`dotnet build WinForge.csproj -c Debug -p:Platform=x64`。
2. 啟動 app 並調到一致嘅視窗大小；保持預設主導語言，等雙語文字自然顯示。
3. 前往每個 NavigationView 項目，擷取內容區，裁剪到 app 視窗。
4. **儲存前先遮蔽私隱資料：** IP 位址、token／API key、使用者名、電郵、機器名、repo 路徑。用佔位值（`MYTUNNEL`、`app.example.com`、`http://localhost:8080`）。
5. 儲存做 `docs/screenshot-<key>.png`，覆蓋佔位符。

## Capture status · 擷取狀態

| Key · 鍵 | Module · 模組 | Status · 狀態 |
|---|---|---|
| `dashboard` | Dashboard · 儀表板 | ✅ captured · 已擷取 |
| `git` | Git & GitHub · Git 與 GitHub | ✅ captured · 已擷取 |
| `packages` | Package Manager · 套件管理 | ✅ captured · 已擷取 |
| `media` | Media · 媒體 | ✅ captured · 已擷取 |
| `clipboard` | Clipboard · 剪貼簿 | ✅ captured · 已擷取 |
| `connections` | Connections (SSH / network) · 連線 | ✅ captured · 已擷取 |
| `monitor` | System Monitor · 系統監察 | ✅ captured · 已擷取 |
| `cloudflare` | Cloudflare & Tunnel · Cloudflare 與 Tunnel | 🟨 placeholder · 佔位符 (capture pending · 待擷取) |
| `aiagents` | AI Agents · AI 代理 | 🟨 placeholder · 佔位符 (capture pending · 待擷取) |
| `settingshub` | Settings & Control Panel · 設定與控制台 | 🟨 placeholder · 佔位符 (capture pending · 待擷取) |

> **Note on `ssh` · 關於 `ssh`:** the handoff lists a `screenshot-ssh.png`, but the suite has no dedicated SSH module. Its network-connections surface ships as the **Connections** module (`module.connections`), already captured as `screenshot-connections.png`. No separate `ssh` shot is needed.
> handoff 列咗 `screenshot-ssh.png`，但套件冇獨立嘅 SSH 模組。佢嘅網絡連線介面係 **Connections** 模組（`module.connections`），已擷取為 `screenshot-connections.png`。唔需要另開 `ssh` 截圖。

> **Why placeholders · 點解有佔位符:** the three 🟨 shots were generated as labelled placeholders because capturing live screenshots needs interactive control of the running app (computer-use), which was not available in the automated run that produced this docs pass. They are clearly marked and contain **no fabricated data** — replace them with real captures using the steps above.
> 三張 🟨 截圖係標明咗嘅佔位符，因為擷取實時截圖需要互動式控制運行中嘅 app（computer-use），喺產生呢次文件嘅自動化流程入面用唔到。佢哋有清楚標示，亦**冇任何虛構數據** — 請按上面步驟換成真實截圖。

## Full image inventory · 完整圖檔清單

**EN —** Beyond the gallery above, `docs/` also holds shots for many more modules (ADB, Archives, Awake, Bulk Ops, Color Picker, Context Menu, Devices, Disk Analyzer, Drives, Duplicates, Env Vars, Event Viewer, Hosts, Keyboard, Maintenance, Mixer, Mouse, Net Pro, Recipes, Recorder, RegEdit, Rename, Search, Services, Startup, Scheduled Tasks, Uninstaller, VPN Mesh, Winaero, Window Manager, and more). The full list is the set of `docs/screenshot-*.png` files.

**粵語 —** 除咗上面嘅畫廊，`docs/` 仲有好多其他模組嘅截圖（ADB、Archives、Awake、批量操作、取色器、右鍵選單、裝置、磁碟分析、磁碟機、重複檔、環境變數、事件檢視器、Hosts、鍵盤、維護、混音器、滑鼠、Net Pro、食譜、錄製、登錄編輯、重新命名、搜尋、服務、啟動、排程工作、解除安裝、VPN Mesh、Winaero、視窗管理等等）。完整清單就係所有 `docs/screenshot-*.png` 檔。

---
[← Module index · 模組索引](Home) · [README](https://github.com/codingmachineedge/WinForge/blob/main/README.md)
