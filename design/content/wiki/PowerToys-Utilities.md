# PowerToys-style Utilities · PowerToys 式工具

A collection of PowerToys-style power-user utilities built into WinForge — remapping, launchers, color tools, taskbar tweaks and more. 一系列內建喺 WinForge 嘅 PowerToys 式進階工具，包括按鍵重對應、啟動器、取色工具、工作列調校等等。

## Keyboard Remapper · 鍵盤重新對應

Remap keys via the Scancode Map (SharpKeys-style). · 用 Scancode Map 重新對應按鍵（SharpKeys 式）。

Open in-app: `WinForge.exe --page keyboard`

![Keyboard Remapper](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-keyboard.png)

## Hotkey & Macro Runner · 熱鍵與巨集

Run hotkeys, macros and text expansion snippets. · 執行熱鍵、巨集同文字展開片語。

Open in-app: `WinForge.exe --page hotkeys`

![Hotkey & Macro Runner](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-hotkeys.png)

## Shortcut Guide · 快捷鍵指南

Hold-Win overlay cheat sheet of Windows shortcuts. · 揿住 Win 鍵顯示快捷鍵速查覆蓋層。

Open in-app: `WinForge.exe --page shortcutguide`

![Shortcut Guide](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-shortcutguide.png)

## Command Palette · 指令面板

Global launcher and Run box for apps, calc and system actions. · 全域啟動器同執行框，啟動應用程式、計算同系統動作。

Open in-app: `WinForge.exe --page cmdpalette`

![Command Palette](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-cmdpalette.png)

## Color Picker · 螢幕取色

System-wide color picker with hex/RGB/HSL output. · 全系統取色器，輸出 hex／RGB／HSL。

Open in-app: `WinForge.exe --page colorpicker`

![Color Picker](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-colorpicker.png)

## Screen Ruler · 螢幕間尺

Measure distances and pixels on screen. · 喺螢幕量度距離同像素。

Open in-app: `WinForge.exe --page screenruler`

![Screen Ruler](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-screenruler.png)

## Crop And Lock · 裁切與鎖定

Select an application window and drag a region to create one of three always-on-top views: a live Thumbnail, a safe reparent-style live Crop view, or a frozen Screenshot. Each flow has its own configurable global shortcut; screenshot pixels are captured in-process and intentionally do not update afterwards. · 揀一個應用程式視窗，再拖一個範圍，就可以建立三種置頂檢視：即時縮圖、安全重新指定父視窗風格嘅即時裁切檢視，或者靜態截圖。三種流程各自都有可設定全域快捷鍵；截圖像素會喺 app 內擷取，之後刻意唔會更新。

Open in-app: `WinForge.exe --page cropandlock`

## Video Conference Mute · 視像會議靜音

Mute the default communications microphone system-wide with a configurable global shortcut. A separate, explicitly enabled camera privacy gate changes only the current user's webcam consent between Allow and Deny; it never disables a driver and can be reversed from the module. Dedicated all-controls, microphone-only and camera-only shortcuts show a brief visible state confirmation, and the existing WinForge tray menu can toggle conference mute. · 用可設定全域快捷鍵將預設通訊咪喺全系統靜音。另一個要明確開啟嘅鏡頭私隱閘只會將目前用戶嘅 webcam 同意喺 Allow 同 Deny 之間切換；絕對唔會停用驅動程式，而且可以喺模組內還原。全部控制、咪專用同鏡頭專用快捷鍵都會顯示短暫可見狀態確認，現有 WinForge 系統匣選單亦可以切換會議靜音。

Open in-app: `WinForge.exe --page videoconference`

## Power Display · 顯示器控制

Managed DDC/CI controls for external monitors: brightness, contrast, monitor volume, input source, rotation, colour temperature and power state. Save one-click profiles, select a compact activation shortcut, expose selected monitors in the compact panel, add custom VCP mappings, and bind separate light/dark profiles to Light Switch. Hardware probing and writes stay off until the user explicitly enables the module. · 用受管 DDC/CI 控制外置螢幕：亮度、對比、螢幕音量、輸入來源、旋轉、色溫同電源狀態。可以儲存一鍵設定檔、揀精簡面板啟用快捷鍵、揀選邊部螢幕顯示喺精簡面板、加入自訂 VCP 對應，同埋將淺色／深色設定檔綁定 Light Switch。用家未明確開啟模組前，唔會做硬件偵測或者寫入。

Open in-app: `WinForge.exe --page powerdisplay`

## Mouse Utilities · 滑鼠工具

Find My Mouse, highlighter, crosshairs, pointer jump, CursorWrap and Grab and Move. · 搵滑鼠、點擊標示、十字線、指標跳轉、游標環繞同拖曳移動視窗。

Open in-app: `WinForge.exe --page mouseutils`

![Mouse Utilities](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-mouseutils.png)

### CursorWrap · 游標環繞

Wrap the pointer to the opposite outer edge of the active display. Choose always-on, Ctrl-held, or Shift-held activation; horizontal, vertical, or both-direction wrapping; and optionally pause it while only one monitor is connected. · 游標去到使用中顯示器嘅外邊會由對面再出現。可選長開、撳住 Ctrl、或者撳住 Shift 先啟用；亦都可以揀橫向、直向、或者兩個方向環繞，仲可以得一個螢幕時暫停。

### Grab and Move · 拖曳移動視窗

Hold Alt (or the Windows key) and left-drag anywhere in a normal window to move it. Optional right-drag resize selects the nearest edge or corner, while exclusions, full-screen-game pausing, Alt-menu suppression, and a live geometry label keep the gesture predictable. · 撳住 Alt（或者 Windows 鍵）之後喺普通視窗任何位置用左鍵拖曳就可以移動。可選右鍵拖曳會揀最近嘅邊或者角縮放；排除清單、全螢幕遊戲暫停、Alt 選單抑制同即時座標標籤令手勢更可預測。

## Mouse & Pointer · 滑鼠與指標

Adjust pointer speed, acceleration and behaviour. · 調整指標速度、加速同行為。

Open in-app: `WinForge.exe --page mouse`

![Mouse & Pointer](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-mouse.png)

## Mouse Without Borders · 無界滑鼠

Share one keyboard and mouse across multiple PCs (software KVM). · 跨多部電腦共享一套鍵盤滑鼠（軟件 KVM）。

Open in-app: `WinForge.exe --page mwb`

![Mouse Without Borders](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-mwb.png)

## Quick Accent · 快速重音符

Insert accented and special characters by holding a letter. · 揿住字母快速插入重音同特殊字元。

Open in-app: `WinForge.exe --page quickaccent`

![Quick Accent](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-quickaccent.png)

## Command Not Found · 搵唔到指令

Suggest a winget package for a missing PowerShell command. · 為搵唔到嘅 PowerShell 指令建議 winget 套件。

Open in-app: `WinForge.exe --page cmdnotfound`

![Command Not Found](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-cmdnotfound.png)

## Clipboard · 剪貼簿

Richer clipboard history with QR-code generation. · 更豐富嘅剪貼簿歷史，附二維碼產生。

Open in-app: `WinForge.exe --page clipboard`

![Clipboard](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-clipboard.png)

## Advanced Paste · 進階貼上

Paste-as transforms: plain text, Markdown, JSON, OCR and AI. · 貼上轉換：純文字、Markdown、JSON、OCR 同 AI。

Open in-app: `WinForge.exe --page advancedpaste`

![Advanced Paste](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-advancedpaste.png)

## Taskbar Tweaker · 工作列調校

Tweak taskbar alignment, button combining, tray and clock. · 調校工作列對齊、合併按鈕、系統匣同時鐘。

Open in-app: `WinForge.exe --page taskbar-tweaker`

![Taskbar Tweaker](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-taskbar-tweaker.png)

## Windhawk Mods · Windhawk 模組

Manage Windhawk mods that customize the taskbar, clock and shell. · 管理 Windhawk 模組，自訂工作列、時鐘同殼層。

Open in-app: `WinForge.exe --page windhawk`

![Windhawk Mods](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-windhawk.png)

## LightSwitch (Auto Dark Mode) · 自動深淺色

Automatically switch light/dark theme on a sunrise/sunset schedule. · 按日出日落排程自動切換深淺色主題。

Open in-app: `WinForge.exe --page lightswitch`

![LightSwitch (Auto Dark Mode)](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-lightswitch.png)

## Rainmeter Widgets · Rainmeter 桌面小工具

Install and toggle Rainmeter desktop skins and widgets. · 安裝同切換 Rainmeter 桌面皮膚同小工具。

Open in-app: `WinForge.exe --page rainmeter`

![Rainmeter Widgets](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-taskbar.png)

## Time & Unit Tools · 時間與單位工具

World clock, time-zone converter and unit converters. · 世界時鐘、時區換算同單位換算。

Open in-app: `WinForge.exe --page time`

![Time & Unit Tools](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-time.png)

## Flashcards · 間隔重複記憶卡

Spaced-repetition flashcard study with SM-2 scheduling. · 用 SM-2 排程嘅間隔重複記憶卡學習。

Open in-app: `WinForge.exe --page flashcards`

![Flashcards](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-flashcards.png)

[← Wiki Home](Home.md)
