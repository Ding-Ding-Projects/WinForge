<div align="center">

# WinForge · 視窗鑄造

**A fully bilingual Windows convenience suite — a kiosk-style control center that really changes Windows 11.**
**一個全程雙語嘅 Windows 便利套件 — kiosk 全螢幕控制中心，真係會改到 Windows 11。**

`30+ in-app modules` · `1100+ tweaks & ops` · `PowerToys + Winaero built in` · `kiosk full-screen` · `tray / runs in background` · `WinUI 3` · `English + 粵語`

`releases on every push` · `master search` · `no redirects — everything runs in-app`

</div>

> **Now a multi-module suite.** WinForge is growing from a Windows 11 tweaker into a full convenience
> suite. Each "module" is a real, working tool. Built and expanded continuously.
> **而家係一個多模組套件。** WinForge 由 Windows 11 調校工具，逐步變成一個完整嘅便利套件，
> 每個「模組」都係真正用得嘅工具，會持續擴充。

## 🧩 Suite modules · 套件模組

| Module · 模組 | What's inside · 內容 |
|---|---|
| **Windows 11 control · Windows 11 控制** | 169 real tweaks across 13 categories (registry, power, privacy, cleanup, security, the no-UAC launcher…). <br> 13 個分類、169 項真實調校（登錄檔、電源、私隱、清理、安全、免 UAC 啟動器…）。 |
| **Git & GitHub · Git 與 GitHub** | A multi-**repository list** (add / scan / clone), stage/commit/branch/sync, the **chunked uploader** (N-MB-per-commit, push one at a time), a free-form **git & gh command runner**, and a **553-operation** library covering the complete git CLI **and** everything GitHub exposes through `gh` / `gh api`. <br> **多儲存庫清單**（加／掃描／複製）、暫存／提交／分支／同步、**分批上載**（每個 commit N-MB、逐個 push）、**git 同 gh 指令執行器**，加埋 **553** 個操作嘅資料庫，涵蓋成個 git CLI **同埋** GitHub 透過 `gh` / `gh api` 提供嘅一切。 |
| **Package Manager · 套件管理（UniGetUI 式）** | One front-end over **8 package managers** — winget, Scoop, Chocolatey, pip, npm, .NET tools, PowerShell Gallery, Cargo — with Discover / Updates / Installed / **Bundles** (export-import) / Setup, per-manager filtering, batch update, and one-click engine bootstrap. <br> 一個介面統管 **8 個套件管理器**（winget、Scoop、Chocolatey、pip、npm、.NET 工具、PowerShell Gallery、Cargo）— 搜尋／更新／已安裝／**清單**（匯出匯入）／設定、逐個引擎篩選、批次更新、一鍵安裝引擎。 |

<div align="center">

![Dashboard](docs/screenshot-dashboard.png)

</div>

### 🖼️ Module gallery · 模組畫廊

<!-- Canonical screenshot set lives in docs/. Keep this gallery in sync with docs/wiki/. -->

| | |
|---|---|
| ![Dashboard](docs/screenshot-dashboard.png) <br> **Dashboard · 儀表板** — master search across every module; everything runs in-app. <br> 跨所有模組嘅總搜尋；一切喺 app 內運行。 | ![Git & GitHub](docs/screenshot-git.png) <br> **Git & GitHub · Git 與 GitHub** — multi-repo list, chunked uploader, 553-op git/gh library. <br> 多儲存庫清單、分批上載、553 個 git/gh 操作庫。 |
| ![Package Manager](docs/screenshot-packages.png) <br> **Package Manager · 套件管理** — one front-end over 8 package managers; batch update & bundles. <br> 一個介面統管 8 個套件管理器；批次更新同清單。 | ![Cloudflare & Tunnel](docs/screenshot-cloudflare.png) <br> **Cloudflare & Tunnel · Cloudflare 與 Tunnel** — named/quick tunnels, DNS routing, Access, DoH, WARP via cloudflared. <br> 具名／快速 tunnel、DNS 路由、Access、DoH、WARP（cloudflared）。 |
| ![AI Agents](docs/screenshot-aiagents.png) <br> **AI Agents · AI 代理** — install, configure & launch terminal AI coding agents, one click each. <br> 一鍵安裝、設定同啟動終端機 AI 編程代理。 | ![Media](docs/screenshot-media.png) <br> **Media · 媒體** — ffmpeg-powered video/audio convert, trim and GIF export. <br> 用 ffmpeg 嘅影片／音訊轉檔、剪裁同 GIF 匯出。 |
| ![Settings & Control Panel](docs/screenshot-settingshub.png) <br> **Settings & Control Panel · 設定與控制台** — change Windows settings in-app, or open any Settings page / CPL applet, grouped & searchable. <br> 喺 app 內改 Windows 設定，或打開任何設定頁／控制台 applet，已分類同可搜尋。 | ![Clipboard](docs/screenshot-clipboard.png) <br> **Clipboard · 剪貼簿** — history of text, images & files; QR, plain-text and format conversions. <br> 文字、圖片同檔案歷史；QR、純文字同格式轉換。 |
| ![Connections](docs/screenshot-connections.png) <br> **Connections (SSH / network) · 連線** — every live TCP/UDP socket and the app that owns it; drop a connection or end the process. <br> 每一條即時 TCP/UDP 連線同擁有佢嘅程式；可切斷連線或結束程序。 | ![System Monitor](docs/screenshot-monitor.png) <br> **System Monitor · 系統監察** — live CPU / RAM / network, process priority & affinity. <br> 即時 CPU／RAM／網絡、程序優先順序同親和性。 |

> **EN —** The canonical screenshot set lives in [`docs/`](docs/). The matching wiki pages are in [`docs/wiki/`](docs/wiki/) and on the [GitHub wiki](https://github.com/codingmachineedge/WinForge/wiki). Some shots are labelled placeholders pending a fresh capture run — see [`docs/wiki/Screenshots.md`](docs/wiki/Screenshots.md).
> **粵語 —** 正式截圖放喺 [`docs/`](docs/)，對應嘅 wiki 頁喺 [`docs/wiki/`](docs/wiki/) 同 [GitHub wiki](https://github.com/codingmachineedge/WinForge/wiki)。部分截圖係標明咗嘅佔位符，等緊重新擷取 — 詳見 [`docs/wiki/Screenshots.md`](docs/wiki/Screenshots.md)。

---

## 🌏 Overview · 概覽

**EN —** WinForge is a modern **WinUI 3** desktop app for Windows 11. Every single feature is shown in
**both English and Cantonese (粵語) at the same time**, and every toggle, choice and action **actually
changes the system** — it writes real registry keys, switches power plans, resets the network stack,
flips privacy settings, cleans caches and more. Nothing is fake: each card maps to a documented Windows
setting or a real command.

**粵語 —** WinForge 係一個畀 Windows 11 用嘅現代化 **WinUI 3** 桌面應用程式。每一項功能都會
**同時用英文同粵語顯示**，而且每個開關、選項同動作都會**真正改到你部機** —
佢會寫真實嘅登錄檔、轉電源計劃、重設網絡堆疊、改私隱設定、清快取等等。冇一樣係假嘅：
每張卡都對應一個有文件記載嘅 Windows 設定或者一句真實指令。

---

## ⚠️ Safety first · 安全至上

**EN —** These tweaks modify **real** Windows settings. Most are reversible (toggle them back), but some
need **administrator rights** or a **restart / sign-out** to take effect. Always read a card's description
before applying it. Use at your own risk — see the [LICENSE](LICENSE).

**粵語 —** 呢啲調校會改到**真實**嘅 Windows 設定。大部分都可以還原（撳返轉頭就得），
但有部分需要**管理員權限**，或者要**重啟／登出**先生效。套用之前請睇清楚每張卡嘅說明。
自行承擔風險 — 詳見 [LICENSE](LICENSE)。

---

## ✨ Feature categories · 功能分類

| # | Category · 分類 | What it does · 做乜 |
|---|---|---|
| 🎨 | **Appearance & Personalisation · 外觀與個人化** | Dark mode, accent colour, transparency, animations and visual effects. <br> 深色模式、強調色、透明度、動畫同視覺特效。 |
| 📁 | **File Explorer · 檔案總管** | Show extensions, hidden files, classic right-click menu, Explorer behaviour. <br> 顯示副檔名、隱藏檔案、經典右鍵選單、檔案總管行為。 |
| 📌 | **Taskbar & Start · 工作列與開始功能表** | Alignment, Search, Widgets, Task View, Copilot, combine buttons, Start layout. <br> 對齊、搜尋、小工具、工作檢視、Copilot、合併按鈕、開始版面。 |
| 🔒 | **Privacy & Telemetry · 私隱與遙測** | Advertising ID, telemetry level, activity history, tailored ads, suggestions. <br> 廣告 ID、遙測等級、活動記錄、個人化廣告、建議內容。 |
| ⚡ | **Performance & Power · 效能與電源** | Visual-effects mode, fast startup, power plans, Game Mode, hibernation. <br> 視覺效果模式、快速啟動、電源計劃、遊戲模式、休眠。 |
| 🌐 | **Network & Internet · 網絡與互聯網** | Flush DNS, reset Winsock/TCP-IP, change DNS servers, inspect connections. <br> 清 DNS、重設 Winsock／TCP-IP、轉 DNS 伺服器、檢視連線。 |
| 🧹 | **Cleanup & Storage · 清理與儲存** | Temp files, caches, Recycle Bin, Update cache, thumbnails, event logs. <br> 暫存檔、快取、回收筒、更新快取、縮圖、事件記錄。 |
| 🛡️ | **Security · 安全** | UAC, SmartScreen, firewall, Remote Desktop, sign-in protections, Defender scan. <br> UAC、SmartScreen、防火牆、遠端桌面、登入保護、Defender 掃描。 |
| 🧩 | **System & Boot · 系統與開機** | Long paths, Developer Mode, restore points, clipboard history, boot options. <br> 長路徑、開發人員模式、還原點、剪貼簿記錄、開機選項。 |
| 📦 | **Apps & Startup · 應用程式與啟動** | winget upgrades, startup items, running processes, restart Explorer/services. <br> winget 更新、啟動項目、執行中程序、重啟檔案總管／服務。 |
| 🔧 | **Power Tools · 進階工具** | SFC, DISM, God Mode, hosts file, battery report, lock / sleep / restart. <br> SFC、DISM、上帝模式、hosts 檔、電池報告、鎖定／睡眠／重啟。 |
| ℹ️ | **System Information · 系統資訊** | Live read-out of OS, CPU, RAM, GPU, disk, uptime and more. <br> 即時顯示系統、CPU、RAM、GPU、磁碟、運行時間等等。 |

> Every feature carries a bilingual title **and** description, plus admin / restart badges where relevant.
> 每項功能都有雙語標題**同**說明，需要時仲會標示管理員／重啟徽章。

---

## 🖥️ Requirements · 系統需求

**EN**
- Windows 11 (also runs on Windows 10 1809+).
- [.NET SDK](https://dotnet.microsoft.com/download) (8.0 or newer) to build.
- [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/) runtime to run.
- Some tweaks require running **as administrator** (the app offers a one-click relaunch).

**粵語**
- Windows 11（Windows 10 1809 或以上都用得）。
- 編譯需要 [.NET SDK](https://dotnet.microsoft.com/download)（8.0 或更新）。
- 執行需要 [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/) 執行階段。
- 部分調校需要**以管理員身分**運行（app 有一鍵重新啟動）。

---

## 🚀 Build & run · 編譯同執行

```powershell
# Clone · 複製
git clone https://github.com/codingmachineedge/WinForge.git
cd WinForge

# Restore + build · 還原同編譯
dotnet build -c Release -p:Platform=x64

# Run · 執行
dotnet run -c Release -p:Platform=x64
```

**EN —** Or open `WinForge.csproj` in **Visual Studio 2022** (with the *.NET Desktop* and *Windows App SDK*
workloads) and press **F5**.

**粵語 —** 或者喺 **Visual Studio 2022**（裝咗 *.NET 桌面* 同 *Windows App SDK* 工作負載）
打開 `WinForge.csproj`，撳 **F5** 就得。

---

## 🧱 How it works · 運作原理

**EN —** WinForge is **data-driven**. Each feature is a `TweakDefinition` that carries its own behaviour
(a registry coordinate, a choice set, or a shell/PowerShell command) plus bilingual text. A thin,
reusable `TweakCard` renders any tweak, and a single catalog aggregates everything by category — so the
UI is just a window over the catalog.

```
Models/      LocalizedText, TweakDefinition, AppCategory   (the data shapes)
Services/    Registry, ShellRunner, Admin, Localization, SystemInfo, Tweak factory
Catalog/     Categories + 12 category files = 164 TweakDefinitions
Controls/    TweakCard  (renders any tweak, always bilingual)
Pages/       Dashboard, CategoryPage, Settings, About
```

**粵語 —** WinForge 係**資料驅動**嘅。每項功能都係一個 `TweakDefinition`，自己帶住行為
（一個登錄檔位置、一組選項，或者一句 shell／PowerShell 指令）同雙語文字。一個輕巧、可重用嘅
`TweakCard` 負責顯示任何一項調校，而一個總目錄就按分類集合晒所有嘢 — 所以個介面只係目錄嘅一個窗口。

---

## 🈳 Bilingual by design · 雙語設計

**EN —** Language is never hidden behind a menu: **both** English and Cantonese appear on every card at
once. The Settings page lets you pick which language *leads*, and the whole UI updates live.

**粵語 —** 語言唔會收埋喺選單後面：**兩種**語言喺每張卡都會即時一齊出現。
設定頁可以揀邊種語言*排前面*，成個介面會即時更新。

---

## 📄 License · 授權條款

**EN —** Released under the [MIT License](LICENSE). Provided "as is", without warranty of any kind.

**粵語 —** 以 [MIT 授權條款](LICENSE) 發佈。按「現狀」提供，不附任何形式嘅保證。

---

<div align="center">

Made with WinUI 3 · 用 WinUI 3 製作 · `English + 粵語`

</div>
