# Screenshots · 截圖集

Canonical screenshots live in `docs/` and are embedded here through raw GitHub URLs. Entries are captured — and cropped, highlighted, annotated, and redacted — with [`winforge-shot`](https://github.com/codingmachineedge/WinForge/tree/main/tools/WinForgeShot). See the [Wiki Screenshot Workflow](Wiki-Screenshot-Workflow.md) for the full recipe.

正式截圖放喺 `docs/`，呢度用 raw GitHub URL 嵌入。截圖由 [`winforge-shot`](https://github.com/codingmachineedge/WinForge/tree/main/tools/WinForgeShot) 擷取，並裁切、加強調、標註同遮蔽。完整做法見 [Wiki 截圖工作流程](Wiki-Screenshot-Workflow.md)。

## Current Capture Status · 目前擷取狀態

**EN —** On 2026-07-11, a fresh self-contained Dashboard capture reproduced
`CopyFromScreen`: `The handle is invalid`. The direct `PrintWindow` fallback
returned success but its inspected 682×1311 PNG was uniformly
`ARGB #FF000000` across 3,198 samples. Windows.Graphics.Capture
`CreateForWindow` could create capture items for both WinForge and an owned
coloured diagnostic window, but neither free-threaded frame pool received a
frame within 12 seconds. Therefore this desktop session has no valid capture
fallback: no PNG was created/replaced, no stale image was substituted, and no
visual-pass result is published.

**粵語 —** 2026-07-11 嘅新 self-contained Dashboard 截圖重現咗
`CopyFromScreen`: `The handle is invalid`。直接 `PrintWindow` fallback 雖然
回傳成功，但已檢查嘅 682×1311 PNG 喺 3,198 個抽樣都係
`ARGB #FF000000`。Windows.Graphics.Capture `CreateForWindow` 雖然可以為
WinForge 同自有有色診斷視窗建立 capture item，但兩個 free-threaded frame pool
喺 12 秒內都收唔到 frame。所以呢個 desktop session 冇有效 capture fallback：
冇建立／替換 PNG、冇用舊圖頂替，亦冇發佈 visual-pass 結果。

**EN —** Batch 06 repeated the capture check against H2 Plant after its fresh
self-contained route launch. `driver.ps1 -Out` again stopped at
`CopyFromScreen`: `The handle is invalid`; a `PrintWindow` fallback attempt
then reported `ERROR: bad window rect`, while the previously successful-call
fallback output is uniformly black. No valid PNG exists for this batch, so no
canonical screenshot was replaced and no visual-pass status is claimed.

**粵語 —** Batch 06 喺新 self-contained route launch 之後，再試咗 H2 Plant
capture。`driver.ps1 -Out` 又喺 `CopyFromScreen` 報 `The handle is invalid`；
`PrintWindow` fallback 跟住報 `ERROR: bad window rect`，而之前成功 call 到嘅
fallback output 仍然係 uniform-black。呢批冇有效 PNG，所以冇換 canonical
截圖，亦唔會聲稱 visual-pass。

**EN —** The fresh self-contained Package Manager deep-link check selected Discover,
Updates, and Installed through UI Automation on 2026-07-11. Its required
`driver.ps1 -Out` screenshot attempt for `package-updates` again fell back from
`CopyFromScreen` to `PrintWindow`, then stopped because the result was a uniform
frame: `CopyFromScreen is unavailable and the PrintWindow fallback produced a uniform
frame; graphics capture is unavailable in this desktop session.` No PNG was created
or replaced, and this is not a visual-pass claim.

**粵語 —** 2026-07-11 嘅新 self-contained Package Manager 深層連結檢查用 UI
Automation 成功揀到搜尋安裝、可更新同已安裝。指定嘅 `package-updates`
`driver.ps1 -Out` 截圖再一次由 `CopyFromScreen` fallback 去 `PrintWindow`，但因為
結果係 uniform frame 而停止：`CopyFromScreen is unavailable and the PrintWindow
fallback produced a uniform frame; graphics capture is unavailable in this desktop
session.` 冇建立或者替換 PNG，亦唔係 visual-pass 聲稱。

**EN —** Batch 08 made a fresh 15-second mactools capture attempt after its
bounded launch retry had passed. CopyFromScreen was unavailable; the driver
then tried PrintWindow, detected a uniform frame, and stopped with
CopyFromScreen is unavailable and the PrintWindow fallback produced a uniform
frame; graphics capture is unavailable in this desktop session. No
mactools-default.png was saved, no canonical image was replaced or reused, and
the batch is capture-blocked, not visual-pass.

**粵語 —** Batch 08 喺受限 launch retry 通過之後，為 mactools 做咗新嘅 15 秒
capture 嘗試。CopyFromScreen 唔可用；driver 跟住試 PrintWindow、發現係 uniform
frame，再以 CopyFromScreen is unavailable and the PrintWindow fallback produced a
uniform frame; graphics capture is unavailable in this desktop session. 停止。
冇儲存 mactools-default.png、冇替換或者重用 canonical image，呢一批係
capture-blocked，唔係 visual-pass。

**EN —** Batch 07’s post-fix KeePass launch succeeded, then its fresh
15-second `driver.ps1 -Out` attempt again stopped at `CopyFromScreen`: `The
handle is invalid`. No `keepass-clipboard-safety.png` was produced, so the
existing canonical KeePass image was neither replaced nor reused as evidence;
the route has `capture-blocked`, not visual-pass, status.

**粵語 —** Batch 07 修正後嘅 KeePass launch 通過，之後新嘅 15 秒
`driver.ps1 -Out` 嘗試又喺 `CopyFromScreen` 停咗：`The handle is invalid`。
冇產生 `keepass-clipboard-safety.png`，所以既有 canonical KeePass 圖冇換、亦
冇當新證據使用；呢條 route 係 `capture-blocked`，唔係 visual-pass。
**EN —** The subsequent numeric-literal reliability audit attempted a fresh
12-second `driver.ps1 -Out` capture for each changed page: Markdown TOC, Name
Generator, Number Formatter, Scientific Notation, Subnet Calculator, and Unit
Converter. Every route reached the capture step but each `CopyFromScreen` call
returned `The handle is invalid`. No page produced a valid PNG, no stale
canonical screenshot was substituted, and these six pages are
`capture-blocked`, not visual-pass.

**粵語 —** 跟住嘅 numeric-literal reliability 審查，為每個改過頁面都用新鮮
12 秒 `driver.ps1 -Out` 試過截圖：Markdown 目錄、名稱產生器、數字格式化、科學
記數法、子網計算器同單位換算器。每條 route 都去到 capture step，但每次
`CopyFromScreen` 都回傳 `The handle is invalid`。冇一頁產生有效 PNG、冇用舊
canonical 截圖頂替，呢 6 頁係 `capture-blocked`，唔係 visual-pass。

**EN —** The Package Manager source-preservation P0 change received a fresh
`driver.ps1 -Page packages -Publish -WaitMs 15000 -Out …` attempt. The driver
reported `CopyFromScreen unavailable`; its `PrintWindow` fallback produced a
uniform frame and graphics capture was unavailable in this desktop session.
No new Package Manager PNG was produced, inspected, replaced or reused. Its
follow-up `-NoCapture` launch passed, but this evidence is `capture-blocked`,
not visual verification.

**粵語 —** Package Manager 來源保留 P0 變更已經用新嘅
`driver.ps1 -Page packages -Publish -WaitMs 15000 -Out …` 嘗試。driver 報
`CopyFromScreen unavailable`；`PrintWindow` fallback 產生 uniform frame，而
呢個 desktop session 嘅 graphics capture 亦唔可用。冇產生、檢查、替換或者重用
新嘅 Package Manager PNG。之後 `-NoCapture` launch 通過，但呢份證據係
`capture-blocked`，唔係視覺驗證。

**EN —** The Pumped-Hydro state-integrity repair is nonvisual service/code-behind work: no XAML layout or control surface changed. To avoid interfering with the active Batch 09 route sweep, no competing GUI, screenshot attempt, PNG creation/replacement, or visual-pass claim was made; screenshot replacement is not applicable.

**粵語 —** 抽水蓄能狀態完整性修正係非視覺嘅 service／code-behind 工作：冇改 XAML 排版或者控制介面。為咗唔干擾進行中嘅 Batch 09 route sweep，冇開另一個 GUI、冇試截圖、冇產生／替換 PNG，亦冇聲稱 visual-pass；唔適用截圖替換。
**EN —** Batch 09 made fresh 15-second capture attempts after the Percentage
Calculator typed-default repair, the qBittorrent lifecycle repair, and the
Pixel Editor and Proxmox safety repairs. Every changed route reached its
capture step; `CopyFromScreen` was unavailable and the `PrintWindow` fallback
produced a uniform frame, with graphics capture unavailable in this desktop
session. No PNG was created for Percentage Calculator, qBittorrent, Pixel
Editor, or Proxmox; no canonical image was replaced or reused. These are
`capture-blocked` results, never visual-pass claims.

**粵語 —** Batch 09 喺 Percentage Calculator typed-default 修正、qBittorrent
lifecycle 修正，同埋 Pixel Editor 同 Proxmox 安全修正之後，做咗新鮮 15 秒 capture
嘗試。每條改過嘅 route 都到咗 capture step；`CopyFromScreen` 唔可用，
`PrintWindow` fallback 產生 uniform frame，而呢個 desktop session 嘅 graphics
capture 亦唔可用。Percentage Calculator、qBittorrent、Pixel Editor 同 Proxmox 都冇
PNG 產生；冇 canonical image 被替換或者重用。呢啲係 `capture-blocked` 結果，
絕對唔係 visual-pass 聲稱。

## Redaction Rules · 遮蔽規則

**EN —** Before adding screenshots, redact or avoid personal data: Windows usernames, home-folder paths, repo paths outside WinForge, hostnames, IPs that identify private networks, account names, emails, API keys, tokens, session cookies, vault item names, SSH profiles, and real package/source credentials. Use `winforge-shot --redact "x|y|w|h|box|blur|pixelate"` to obscure regions irreversibly; see the [Wiki Screenshot Workflow](Wiki-Screenshot-Workflow.md).

**粵語 —** 新增截圖前，請遮蔽或者避開個人資料：Windows 用戶名、home folder 路徑、WinForge 以外嘅 repo 路徑、主機名、會識別私人網絡嘅 IP、帳戶名、電郵、API key、token、session cookie、保險庫項目名、SSH profile，同真實套件／來源憑證。用 `winforge-shot --redact "x|y|w|h|box|blur|pixelate"` 不可逆咁遮蔽範圍；詳見 [Wiki 截圖工作流程](Wiki-Screenshot-Workflow.md)。

---

## System & Tweaks · 系統與調校

### Dashboard · 概覽
> Screenshot refresh is blocked in this desktop session: `CopyFromScreen` is unavailable and the `PrintWindow` fallback produces a uniform frame. The Dashboard route remains launch-verified. · 呢個桌面工作階段未能更新截圖：`CopyFromScreen` 未可用，而且 `PrintWindow` 後備方案會產生單一畫面。Dashboard 路由仍已驗證可以啟動。

### Registry Editor · 登錄編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-regedit.png)

### System Doctors · 系統醫生
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-doctors.png)

### Services · 服務
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-services.png)

### Scheduled Tasks · 排程工作
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-tasks.png)

### Devices · 裝置
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-devices.png)

### ViVeTool · 功能旗標
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-vivetool.png)

### Startup Apps · 開機程式
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-startup.png)

### Environment Variables · 環境變數
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-envvars.png)

### Event Viewer · 事件檢視器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-events.png)

### System Info (Winfetch) · 系統資訊
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-winfetch.png)

### System Monitor · 系統監察
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-monitor.png)

### Process Explorer · 程序總管
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-procexp.png)

### Battery & Thermal · 電池與散熱
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-battery.png)

### Volume Mixer · 音量混合器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-mixer.png)

### Context Menu · 右鍵選單
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-contextmenu.png)

### Explorer Right-Click · 檔案總管右鍵選單
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-shellmenu.png)

### Nilesoft Shell · Nilesoft 右鍵選單
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-nilesoftshell.png)

### Awake · 保持喚醒
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-awake.png)

### Settings & Control Panel · 設定與控制台
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-settingshub.png)

### Native Utilities · 原生工具
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-native.png)

### PowerToys Extras · PowerToys 額外工具
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-powertoys.png)

### Power Display · 顯示器控制
Fresh capture is pending because the current desktop capture host rejects `CopyFromScreen` with “The handle is invalid” even for a one-pixel virtual-screen test, while `PrintWindow` returns a uniform black frame. No blank or misleading screenshot is published. · 新截圖暫時未能提供，因為目前桌面擷取主機連一像素虛擬螢幕測試都會令 `CopyFromScreen` 回傳「The handle is invalid」，而 `PrintWindow` 只會回傳全黑畫面；所以唔會發佈空白或者誤導嘅截圖。

### Video Conference Mute · 視像會議靜音
Fresh capture is pending because the current desktop capture host rejects `CopyFromScreen` with “The handle is invalid” even for a one-pixel virtual-screen test, while `PrintWindow` returns a uniform black frame. No blank or misleading screenshot is published. · 新截圖暫時未能提供，因為目前桌面擷取主機連一像素虛擬螢幕測試都會令 `CopyFromScreen` 回傳「The handle is invalid」，而 `PrintWindow` 只會回傳全黑畫面；所以唔會發佈空白或者誤導嘅截圖。

### World Monitor · 世界監察
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-worldmonitor.png)

### Activity Timeline · 活動時間軸
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-timelens.png)

---

## Files & Disks · 檔案與磁碟

### Archives · 壓縮檔
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-archives.png)

### Batch Rename · 批次改名
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-rename.png)

### Bulk File Ops · 批次檔案操作
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-bulkops.png)

### New+ · 範本新增
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-newplus.png)

### Duplicate Finder · 重複檔案搜尋
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-duplicates.png)

### Instant File Search · 即時檔案搜尋
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-everything.png)

### File Locksmith · 檔案鎖偵測
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-filelocksmith.png)

### Disk Analyser · 磁碟分析
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-disk.png)

### Hex Editor · 十六進位編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-hex.png)

### Drives · 磁碟機
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-drives.png)

### Disk Health (SMART) · 硬碟健康（SMART）
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-diskhealth.png)

### Disk Benchmark · 硬碟速度測試
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-diskbench.png)

### TestDisk / PhotoRec Recovery · TestDisk / PhotoRec 資料救援
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-testdisk.png)

### Peek · 快速預覽
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-peek.png)

### Rich Preview · 豐富預覽
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-richpreview.png)

### OneDrive · OneDrive
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-onedrive.png)

### Font Manager · 字型管理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-fonts.png)

### FTP / SFTP · FTP／SFTP 檔案傳輸
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-filezilla.png)

### Config & Backup · 設定與備份
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-configbackup.png)

---

## Media & Capture · 媒體與擷取

### Media · 媒體
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-media.png)

### Audio Editor · 音訊編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-audioeditor.png)

### Audio Tagger · 音訊標籤編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-tags.png)

### Media Player · 媒體播放器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-mediaplayer.png)

### Media Downloader · 媒體下載器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-ytdlp.png)

### Document Converter · 文件轉換器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-libreoffice.png)

### PDF Toolkit · PDF 工具箱
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-pdf.png)

### Screen Recorder · 螢幕錄影
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-recorder.png)

### Capture Studio · 擷取工作室
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-capture.png)

### Text Extractor (OCR) · 原生文字辨識
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-ocr.png)

### GIF Studio · 螢幕轉 GIF
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-giflab.png)

### Crop And Lock · 裁切與鎖定
Fresh capture is pending because the current desktop capture host rejects `CopyFromScreen` with “The handle is invalid” even for a one-pixel virtual-screen test, while `PrintWindow` returns a uniform black frame. No blank or stale screenshot is published. · 新截圖暫時未能提供，因為目前桌面擷取主機連一像素虛擬螢幕測試都會令 `CopyFromScreen` 回傳「The handle is invalid」，而 `PrintWindow` 只會回傳全黑畫面；所以唔會發佈空白或者過期嘅截圖。

### ZoomIt · 螢幕放大與標註
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-zoomit.png)

### Voice & Read-Aloud · 語音朗讀
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-voice.png)

### PA Announcements · 喇叭語音廣播
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-announce.png)

### Pixel Editor · 像素畫編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-pixeleditor.png)

### Image Editor · 點陣圖影像編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-imageeditor.png)

### Blender (3D / Render) · Blender（3D／算圖）
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-blender.png)

---

## Developer · 開發者

### VS Code · VS Code 編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-vscode.png)

### Windows Terminal · Windows 終端機
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-terminal.png)

### SSH Toolset · SSH 工具
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-ssh.png)

### quicktype · JSON 轉型別
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-quicktype.png)

### API Client · REST API 用戶端
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-api.png)

### Diff & Merge (WinMerge) · 比對與合併
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-diff.png)

### Diagram Editor · 圖表編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-diagram.png)

### .NET Decompiler · .NET 反編譯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-decompiler.png)

### Postgres Tool · Postgres 工具 / pgAdmin
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-pgadmin.png)

### SQLite Browser · SQLite 資料庫瀏覽器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-sqlite.png)

### Packer (Image Builder) · Packer（映像建置器）
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-packer.png)

### AWS Manager · AWS 管理中心
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-aws.png)

### Website Cloner · 網站複製器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-webcloner.png)

### Resume Writer · 履歷與求職信寫手
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-resume.png)

---

## Network · 網絡

### Connections · 連線
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-connections.png)

### Hosts Editor · hosts 編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-hosts.png)

### Packet Capture · 封包擷取
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-wireshark.png)

### Nmap Scanner · 網絡掃描
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-nmap.png)

### VPN & Mesh · VPN 與網狀網
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-vpn.png)

### RustDesk · 遠端桌面
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-rustdesk.png)

### Cloudflare & Tunnel · Cloudflare 與 Tunnel
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-cloudflare.png)

### Home Assistant · 家居助理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-homeassistant.png)

### In-App Login · 內置登入
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-weblogin.png)

---

## Apps, Git & Packages · 應用程式、Git 與套件

### Git & GitHub · Git 與 GitHub
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-git.png)

### Package Manager · 套件管理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-packages.png)

### Cake Factory & Farm · 蛋糕工廠與農場
![](images/screenshot-cakefactory.png)

### App Uninstaller · 應用程式解除安裝
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-uninstall.png)

### Android (ADB) · Android（ADB）
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-adb.png)

### Fastboot / Flasher · Fastboot／刷機
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-fastboot.png)

### Android Emulator & SDK · Android 模擬器與 SDK
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-emulator.png)

### qBittorrent · 種子下載
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-qbittorrent.png)

### Native Torrent · 原生種子下載
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-torrent.png)

### Communications · 通訊
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-comms.png)

### Mail · 電郵
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-mail.png)

---

## AI · 人工智能

### AI Agents · AI 代理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-ai.png)

### AI Chat · AI 聊天
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-aichat.png)

### Ollama · 本地大模型
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-ollama.png)

---

## Window Management · 視窗管理

### Window Manager · 視窗管理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-windows.png)

### Workspaces · 工作區
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-workspaces.png)

### FancyZones · 視窗分區
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-fancyzones.png)

### AltSnap · Alt 拖曳視窗
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-altsnap.png)

### Komorebi (Tiling WM) · Komorebi 平鋪視窗管理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-komorebi.png)

### GlazeWM Tiling · GlazeWM 平鋪視窗
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-glazewm.png)

---

## PowerToys-style Utilities · PowerToys 式工具

### Keyboard Remapper · 鍵盤重新對應
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-keyboard.png)

### Hotkey & Macro Runner · 熱鍵與巨集
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-hotkeys.png)

### Shortcut Guide · 快捷鍵指南
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-shortcutguide.png)

### Command Palette · 指令面板
> Screenshot refresh is blocked in this desktop session: `CopyFromScreen` is unavailable and the `PrintWindow` fallback produces a uniform frame. The `cmdpalette` deep link, bookmarks, credential-free Remote Desktop profiles, on-demand performance metrics, explicit command mode, Window Walker provider, and persistent Dock remain launch-verified. · 呢個桌面工作階段未能更新截圖：`CopyFromScreen` 未可用，而且 `PrintWindow` 後備方案會產生單一畫面。`cmdpalette` 深層連結、書籤、冇儲存登入資料嘅遠端桌面設定檔、按需效能指標、明確指令模式、Window Walker 提供者同常駐 Dock 仍已驗證可以啟動。

### Color Picker · 螢幕取色
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-colorpicker.png)

### Screen Ruler · 螢幕間尺
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-screenruler.png)

### Mouse Utilities · 滑鼠工具
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-mouseutils.png)

### Mouse & Pointer · 滑鼠與指標
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-mouse.png)

### Mouse Without Borders · 無界滑鼠
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-mwb.png)

### Quick Accent · 快速重音符
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-quickaccent.png)

### Command Not Found · 搵唔到指令
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-cmdnotfound.png)

### Clipboard · 剪貼簿
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-clipboard.png)

### Advanced Paste · 進階貼上
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-advancedpaste.png)

### Taskbar Tweaker · 工作列調校
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-taskbar-tweaker.png)

### Windhawk Mods · Windhawk 模組
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-windhawk.png)

### LightSwitch (Auto Dark Mode) · 自動深淺色
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-lightswitch.png)

### Rainmeter Widgets · Rainmeter 桌面小工具
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-taskbar.png)

### Time & Unit Tools · 時間與單位工具
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-time.png)

### Flashcards · 間隔重複記憶卡
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-flashcards.png)

---

## Virtualization & Containers · 虛擬化與容器

### Docker · Docker 容器管理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-docker.png)

### Docker over SSH · 透過 SSH 控制 Docker
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-dockerssh.png)

### WSL & VM Launcher · WSL 與 VM 啟動器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-wsl.png)

### VirtualBox Manager · VirtualBox 管理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-virtualbox.png)

### Proxmox VE · Proxmox VE 虛擬化
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-proxmox.png)

---

## Security & Vaults · 安全與保險庫

### WinForge Vault · WinForge 保險庫
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-vault.png)

### Bitwarden Vault · Bitwarden 密碼庫
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-bitwarden.png)

### KeePass Vault · 密碼保險庫
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-keepass.png)

---

## Gaming & Emulation · 遊戲與模擬

### Minecraft World Editor (Amulet) · Minecraft 世界編輯器（Amulet）
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-amulet.png)

### Minecraft Server · Minecraft 伺服器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-minecraftserver.png)

### ViaProxy · Minecraft 版本代理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-viaproxy.png)

### Imaging & Game Tools · 燒錄與遊戲工具
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-imaging.png)

---

## Nuclear Reactor · 核反應堆

### Nuclear Reactor · 核反應堆
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-reactor.png)

### Reactor Gauges · 反應堆儀表
![](images/screenshot-reactor-gauges.png)

### Reactor Meltdown Scenario · 反應堆熔毀情境
![](images/screenshot-reactor-meltdown.png)

---

## Additional Wiki Captures · 額外 Wiki 截圖

### AltSnap · Alt 拖曳視窗
![](images/screenshot-altsnap.png)

### Annoyances · 煩擾項目
![](images/screenshot-annoyances.png)

### Battery & Thermal · 電池與散熱
![](images/screenshot-battery.png)

### Maintenance · 維護
![](images/screenshot-maintenance.png)

### Nilesoft Shell · Nilesoft 右鍵選單
![](images/screenshot-nilesoftshell.png)

### qBittorrent · 種子下載
![](images/screenshot-qbittorrent.png)

### Recipes · 配方
![](images/screenshot-recipes.png)

### Search · 搜尋
![](images/screenshot-search.png)

### Taskbar Tweaker · 工作列調校
![](images/screenshot-taskbar-tweaker.png)

### App Uninstaller · 應用程式解除安裝
![](images/screenshot-uninstaller.png)

### Winaero · Winaero 調校
![](images/screenshot-winaero.png)
