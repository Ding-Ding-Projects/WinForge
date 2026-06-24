# WinForge — Command-line options · 指令行選項

WinForge is mainly a GUI, but it accepts a few command-line switches. · WinForge 主要係圖形介面，但都接受幾個指令行參數。

```
WinForge.exe                         Launch the full-screen suite (tray + clipboard monitor).
                                    啟動全螢幕套件（系統匣 + 剪貼簿監察）。

WinForge.exe --page <id>             Open straight to a module / page.
                                    直接開去某個模組／頁面。

WinForge.exe --page search:<query>   Open the master search pre-filled with <query>.
                                    開總搜尋，預先填入 <query>。

WinForge.exe --export-docs <dir>     Headless: write one Markdown file per feature into <dir>
                                    (organized into per-module subfolders) then exit.
                                    無視窗：將每個功能寫一個 Markdown 檔入 <dir>
                                    （按模組分入子資料夾）然後退出。
```

## `--page <id>` values · 可用的頁面 id

| id (aliases) | Module · 模組 |
|---|---|
| `dashboard` | Dashboard · 概覽 |
| `git`, `github` | Git & GitHub · Git 與 GitHub |
| `archives`, `archive` | Archives · 壓縮檔 |
| `media` | Media · 媒體 |
| `registry`, `regedit` | Registry Editor · 登錄編輯器 |
| `services` | Services · 服務 |
| `tasks`, `scheduledtasks` | Scheduled Tasks · 排程工作 |
| `devices` | Devices · 裝置 |
| `startup` | Startup Apps · 開機程式 |
| `rename` | Batch Rename · 批次改名 |
| `bulkops`, `bulk` | Bulk File Ops · 批次檔案操作 |
| `duplicates`, `dupes` | Duplicate Finder · 重複檔案搜尋 |
| `disk`, `diskanalyzer` | Disk Analyser · 磁碟分析 |
| `drives` | Drives · 磁碟機 |
| `uninstall`, `apps` | App Uninstaller · 應用程式解除安裝 |
| `windows`, `windowmanager` | Window Manager · 視窗管理 |
| `keyboard`, `remap` | Keyboard Remapper · 鍵盤重新對應 |
| `hosts` | Hosts Editor · hosts 編輯器 |
| `mouse` | Mouse & Pointer · 滑鼠與指標 |
| `recorder`, `record` | Screen Recorder · 螢幕錄影 |
| `monitor`, `sysmon` | System Monitor · 系統監察 |
| `connections`, `netstat`, `tcp` | Connections · 連線 |
| `events`, `eventlog`, `eventviewer` | Event Viewer · 事件檢視器 |
| `mixer`, `volume`, `audio` | Volume Mixer · 音量混合器 |
| `contextmenu`, `rightclick` | Context Menu · 右鍵選單 |
| `awake` | Awake · 保持喚醒 |
| `colorpicker`, `color` | Color Picker · 螢幕取色 |
| `envvars`, `env` | Environment Variables · 環境變數 |
| `clipboard`, `clip` | Clipboard · 剪貼簿 |
| `about` | About · 關於 |

## Notes · 備註

- Closing the window **hides it to the system tray** so the clipboard monitor keeps running; right-click the tray icon → **Quit** to exit fully. · 關窗會**收入系統匣**，令剪貼簿監察繼續運行；右鍵系統匣圖示 →**結束**先完全退出。
- The app launches as a standard user; a **Relaunch as admin** button on the Dashboard elevates it. · App 以標準使用者啟動；概覽上嘅**以管理員身分重新啟動**可提權。

_Part of WinForge · WinForge 套件嘅一部分_
