# No-Redirect Audit · 無跳轉審核

Validated on the clean branch `work/no-redirect-audit-20260626` with a self-contained Debug publish and driver screenshots. · 已喺乾淨分支 `work/no-redirect-audit-20260626` 用 self-contained Debug publish 同 driver 截圖驗證。

## Fixed In This Pass · 今次已修

- Media Player embeds libVLC only; it no longer offers a VLC install/launch fallback. · 媒體播放器只用內嵌 libVLC；不再提供 VLC 安裝／跳出啟動後備。
- World Monitor keeps the hosted app inside WebView2 and copies URLs instead of opening a browser or external binary. · 世界監察留喺 WebView2 入面，網址只複製，不開外部瀏覽器或外部程式。
- Website Cloner previews the cloned page inside WinForge and no longer opens Explorer, a browser, or an AI terminal cleanup flow. · 網站複製器喺 WinForge 入面預覽複本，不再開檔案總管、瀏覽器或 AI terminal 清理流程。
- Audio Editor removed the Audacity fallback; waveform editing stays in-app. · 音訊編輯器移除 Audacity 後備；波形編輯留喺 app 內。
- Audio Editor, LightSwitch and Activity Timeline load reliably from `--page`; their early XAML event crashes were fixed. · 音訊編輯器、自動深淺色同活動時間軸可由 `--page` 正常開啟；已修早期 XAML 事件崩潰。
- Generic browser-style links in About, package details, AI Agents, Command Not Found, WebView2 fallback, Rich Preview, Mail, Bitwarden, ViaProxy and Rainmeter now copy URLs instead of launching the default browser. · About、套件詳情、AI Agents、Command Not Found、WebView2 後備、Rich Preview、Mail、Bitwarden、ViaProxy 同 Rainmeter 入面嘅瀏覽器式連結，改為複製網址。
- Command Palette web/run fallbacks, Communications protocol links, tab-session folder, file-search folder and yt-dlp output-folder actions now copy the target instead of using ShellExecute/Explorer. · 指令面板網絡／執行後備、通訊 protocol 連結、分頁 session 資料夾、檔案搜尋資料夾同 yt-dlp 輸出資料夾動作，改為複製目標而非 ShellExecute／檔案總管。

## Remaining Intentional Engines · 仍然刻意使用嘅引擎

These are not browser redirects; they are local engines or device CLIs that do the work while WinForge owns the UI. · 呢啲唔係瀏覽器跳轉；係本機引擎或裝置 CLI，WinForge 仍然掌控 UI。

- Media/recording: ffmpeg, yt-dlp, libVLC. · 媒體／錄製：ffmpeg、yt-dlp、libVLC。
- Developer/package flows: git, gh, winget, npm, pip, choco, scoop where the module is a package manager/front-end. · 開發／套件流程：git、gh、winget、npm、pip、choco、scoop，用於套件管理或前端操作。
- Device/system flows: adb, wsl.exe, cloudflared, Docker named pipe, SSH, scheduled tasks, elevated Windows tweaks. · 裝置／系統流程：adb、wsl.exe、cloudflared、Docker named pipe、SSH、排程工作、提權 Windows 調校。

## Bake-Into-C# Candidates · 可再原生化候選

- Replace “launch companion GUI” affordances with deeper native surfaces: pgAdmin advanced admin, Aseprite pixel workflows, Wireshark packet inspection, Windhawk mod browsing, Rainmeter skin authoring. · 將「開伴隨 GUI」改成更完整原生頁面：pgAdmin 進階管理、Aseprite 像素流程、Wireshark 封包檢視、Windhawk mod 瀏覽、Rainmeter skin 編輯。
- Replace Explorer convenience buttons with copy/path reveal panels or in-app file panes where the module already owns the file workflow. · 對已掌控檔案流程嘅模組，用複製／路徑面板或 app 內檔案窗格取代檔案總管快捷鍵。
- Prefer embedded terminal panels for long-running CLIs over Windows Terminal/cmd windows, except when the feature is explicitly a terminal launcher. · 長時間 CLI 優先用內嵌 terminal panel，除非功能本身就係 terminal launcher。

## Verification · 驗證

- `dotnet build WinForge.sln -c Debug -p:Platform=x64 -v minimal` passes with the existing warning set. · build 通過，只剩既有 warnings。
- `dotnet publish WinForge.csproj -c Debug -r win-x64 --self-contained true -p:Platform=x64 -p:WindowsAppSDKSelfContained=true -v quiet` passes. · self-contained publish 通過。
- Driver screenshots were refreshed for Dashboard, Media Player, World Monitor, Website Cloner, Audio Editor, LightSwitch and Activity Timeline. · 已刷新 Dashboard、媒體播放器、世界監察、網站複製器、音訊編輯器、自動深淺色、活動時間軸 driver 截圖。
