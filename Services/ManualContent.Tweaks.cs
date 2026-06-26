using System.Collections.Generic;

namespace WinForge.Services;

// 調校與輸入章節嘅教學條目 · How-to entries for the Tweaks & input section.
public static partial class ManualContent
{
    private static List<ManualEntry> TweaksEntries() => new()
    {
        new ManualEntry
        {
            Tag = "module.hosts", Glyph = "",
            TitleEn = "Hosts Editor", TitleZh = "hosts 編輯器",
            SummaryEn = "Edit your Windows hosts file to block or redirect domains, with one-click backup and DNS flush.",
            SummaryZh = "編輯 Windows hosts 檔案嚟封鎖或者重新導向網域，仲可以一鍵備份同清 DNS。",
            StepsEn = new[]
            {
                "Click Reload to load the current hosts file into the editor.",
                "Type a domain in the box and click Block to add a 0.0.0.0 entry, or edit the text directly.",
                "Click Backup first to keep a copy, then click Save to write the file (needs admin).",
                "Click Flush DNS so the changes take effect immediately.",
            },
            StepsZh = new[]
            {
                "撳 Reload 將而家嘅 hosts 檔案載入編輯器。",
                "喺格仔打個網域再撳 Block 加一條 0.0.0.0 紀錄，或者直接改文字。",
                "改之前先撳 Backup 留個底，跟住撳 Save 寫入檔案（要管理員權限）。",
                "撳 Flush DNS 等改動即刻生效。",
            },
            TipEn = "Editing hosts needs administrator rights; always back up before you save.",
            TipZh = "改 hosts 要管理員權限；存之前記得 backup 留底。",
            Keywords = "hosts block domain redirect dns flush 封鎖 網域 重新導向",
        },
        new ManualEntry
        {
            Tag = "module.mouse", Glyph = "",
            TitleEn = "Mouse & Pointer", TitleZh = "滑鼠與指標",
            SummaryEn = "Tune pointer speed, acceleration and other mouse behaviours from one page.",
            SummaryZh = "喺一頁度調指標速度、加速同其他滑鼠行為。",
            StepsEn = new[]
            {
                "Open the Mouse & Pointer page.",
                "Use the speed slider to set how fast the pointer moves.",
                "Toggle pointer acceleration (Enhance pointer precision) on or off.",
                "Adjust the other switches to taste; changes apply right away.",
            },
            StepsZh = new[]
            {
                "開「滑鼠與指標」頁。",
                "用速度滑桿調指標郁得幾快。",
                "開或者熄指標加速（增強指標精確度）。",
                "其他開關按自己鍾意調；改完即刻生效。",
            },
            Keywords = "mouse pointer speed acceleration precision 滑鼠 指標 速度 加速",
        },
        new ManualEntry
        {
            Tag = "module.mouseutils", Glyph = "",
            TitleEn = "Mouse Utilities", TitleZh = "滑鼠工具",
            SummaryEn = "PowerToys mouse helpers: find your cursor with a spotlight, highlight clicks, draw crosshairs and jump the pointer.",
            SummaryZh = "PowerToys 滑鼠小幫手：用聚光燈搵游標、標示點擊、畫十字線同跳轉指標。",
            StepsEn = new[]
            {
                "Open the Mouse Utilities page and turn on the helpers you want.",
                "Find My Mouse: press Left Ctrl twice (or shake the mouse) to spotlight the cursor.",
                "Mouse Highlighter: show a coloured ring whenever you click.",
                "Crosshairs and Mouse Jump have their own toggles and hotkeys on the page.",
            },
            StepsZh = new[]
            {
                "開「滑鼠工具」頁，揀你想要嘅小幫手開。",
                "Find My Mouse：㩒兩下左 Ctrl（或者㨃下滑鼠）就會用聚光燈照住游標。",
                "Mouse Highlighter：撳滑鼠嗰陣會出一個有色光圈。",
                "Crosshairs 同 Mouse Jump 喺頁面各自有開關同熱鍵。",
            },
            Keywords = "find my mouse spotlight highlighter crosshairs mouse jump 聚光燈 標示 十字線 跳轉",
        },
        new ManualEntry
        {
            Tag = "module.mwb", Glyph = "",
            TitleEn = "Mouse Without Borders", TitleZh = "無界滑鼠",
            SummaryEn = "Share one keyboard and mouse across several PCs on your LAN, with synced clipboard.",
            SummaryZh = "喺區域網用一套鍵盤滑鼠控制幾部電腦，仲可以同步剪貼簿。",
            StepsEn = new[]
            {
                "Turn on the master switch, then optionally tick Share clipboard and Wrap around.",
                "On this PC, note the Security key (copy it) and its IP shown on the card.",
                "On the other PC, fill in the machine name, IP, port and security key, then click Add to pair.",
                "Drag the machine tiles in the 1x4 layout to match where your PCs sit, then move your mouse to the screen edge to cross over.",
            },
            StepsZh = new[]
            {
                "撳開主開關，跟住可以揀埋 Share clipboard 同 Wrap around。",
                "喺呢部電腦，睇住個 Security key（copy 低）同卡片上嘅 IP。",
                "喺另一部電腦，填機器名、IP、port 同安全密鑰，再撳 Add 配對。",
                "喺 1x4 版面拖機器格仔，排到同你部電腦位置一樣，跟住將滑鼠㨃去螢幕邊就會過機。",
            },
            TipEn = "All paired PCs must use the same security key and be on the same network.",
            TipZh = "所有配對嘅電腦都要用同一個安全密鑰，仲要喺同一個網絡。",
            Keywords = "mouse without borders mwb kvm share keyboard clipboard pair 無界滑鼠 共享 配對 剪貼簿",
        },
        new ManualEntry
        {
            Tag = "module.keyboard", Glyph = "",
            TitleEn = "Keyboard Remapper", TitleZh = "鍵盤重新對應",
            SummaryEn = "Remap keys system-wide — make one key act as another, like Caps Lock to Esc.",
            SummaryZh = "全系統重新對應按鍵 — 等一個鍵當另一個用，例如 Caps Lock 變 Esc。",
            StepsEn = new[]
            {
                "Pick the physical key in the From box and the key it should become in the To box.",
                "Click Add to add the mapping to the list.",
                "Repeat for more keys; use the trash button to remove any row.",
                "Click Apply to write the remap (it takes effect after sign-out or reboot).",
            },
            StepsZh = new[]
            {
                "喺 From 揀實體鍵，喺 To 揀佢應該變成乜鍵。",
                "撳 Add 將呢個對應加入清單。",
                "想加多幾個就重複；撳垃圾桶掣可以移除任何一行。",
                "撳 Apply 寫入對應（登出或者重開機後生效）。",
            },
            TipEn = "Mappings are stored in the registry and apply after you sign out and back in.",
            TipZh = "對應存喺登錄檔，要登出再登入先生效。",
            Keywords = "keyboard remap key sharpkeys caps lock 鍵盤 重新對應 按鍵",
        },
        new ManualEntry
        {
            Tag = "module.hotkeys", Glyph = "",
            TitleEn = "Hotkey & Macro Runner", TitleZh = "熱鍵與巨集",
            SummaryEn = "Bind global hotkeys to launch apps, run PowerShell or send keystrokes, plus a text expander for snippets.",
            SummaryZh = "綁全域熱鍵嚟開程式、跑 PowerShell 或者送按鍵，仲有文字展開器幫你打片語。",
            StepsEn = new[]
            {
                "Tick the modifiers (Ctrl, Alt, Shift, Win) and pick a key to form the chord.",
                "Choose an action — Launch app, PowerShell or Send keys — and fill in its fields.",
                "Click Add binding; toggle any binding on or off in the list below.",
                "For the text expander, turn on the switch, then add a trigger word and its expansion.",
            },
            StepsZh = new[]
            {
                "剔修飾鍵（Ctrl、Alt、Shift、Win）再揀一個鍵砌出組合鍵。",
                "揀一個動作 — Launch app、PowerShell 或者 Send keys — 然後填好欄位。",
                "撳 Add binding；喺下面清單可以逐個開關。",
                "想用文字展開器就撳開個掣，再加觸發字同展開內容。",
            },
            Keywords = "hotkey macro chord launch powershell send keys text expander snippet 熱鍵 巨集 組合鍵 文字展開 片語",
        },
        new ManualEntry
        {
            Tag = "module.quickaccent", Glyph = "",
            TitleEn = "Quick Accent", TitleZh = "快速重音符",
            SummaryEn = "Hold a letter and tap an activation key to pop up accented variants like é, ñ or ü.",
            SummaryZh = "撳住一個字母再㩒啟動鍵，就會彈出重音變體例如 é、ñ、ü。",
            StepsEn = new[]
            {
                "Turn on the Quick Accent switch.",
                "Pick the activation key (Space, or the arrow keys) and where the popup appears.",
                "Tick the character sets / languages you need (or All).",
                "Type a letter in the live preview box to check the variants you'll get.",
            },
            StepsZh = new[]
            {
                "撳開 Quick Accent 開關。",
                "揀啟動鍵（Space 或者方向鍵）同候選框出喺邊。",
                "剔你需要嘅字元集／語言（或者揀 All）。",
                "喺即時預覽格打個字母，睇下會出咩變體。",
            },
            TipEn = "In any app, hold the letter then tap the activation key to choose a variant.",
            TipZh = "喺任何程式，撳住字母再㩒啟動鍵就揀到變體。",
            Keywords = "quick accent diacritic accents french spanish german 重音符 變音 候選 法文 德文",
        },
        new ManualEntry
        {
            Tag = "module.shortcutguide", Glyph = "",
            TitleEn = "Shortcut Guide", TitleZh = "快捷鍵指南",
            SummaryEn = "Hold the Windows key to overlay a cheat sheet of Windows shortcuts, plus a searchable reference table.",
            SummaryZh = "撳住視窗鍵就彈出 Windows 快捷鍵速查表，仲有得搜尋嘅參考表。",
            StepsEn = new[]
            {
                "Turn on the Shortcut Guide switch.",
                "Set the hold duration and overlay opacity / theme to taste.",
                "Hold the Windows key in any app to show the overlay; release to dismiss.",
                "Use the search box to find any shortcut in the reference table below.",
            },
            StepsZh = new[]
            {
                "撳開 Shortcut Guide 開關。",
                "調好揿住時間同覆蓋層透明度／主題。",
                "喺任何程式撳住視窗鍵就會出覆蓋層；放手就收返。",
                "用搜尋框喺下面參考表搵任何快捷鍵。",
            },
            Keywords = "shortcut guide windows key overlay cheat sheet 快捷鍵 指南 視窗鍵 覆蓋層 速查表",
        },
        new ManualEntry
        {
            Tag = "module.cmdpalette", Glyph = "",
            TitleEn = "Command Palette", TitleZh = "指令面板",
            SummaryEn = "A global quick launcher — press a hotkey to search apps, run commands, do calculations and system actions.",
            SummaryZh = "全域快速啟動器 — 㩒個熱鍵就搵程式、跑指令、計數同做系統動作。",
            StepsEn = new[]
            {
                "Turn on the Command Palette switch and pick its global hotkey (e.g. Alt+Space).",
                "Set how many results to show, and tick the providers you want (apps, calculator, system actions, web search).",
                "Click Open now, or press the hotkey anywhere, to bring up the palette.",
                "Start typing to filter, then press Enter to run the top result.",
            },
            StepsZh = new[]
            {
                "撳開 Command Palette 開關，揀佢嘅全域熱鍵（例如 Alt+Space）。",
                "設定顯示幾多個結果，再剔你想要嘅來源（程式、計算機、系統動作、網絡搜尋）。",
                "撳 Open now，或者喺邊度㩒個熱鍵，都會彈出面板。",
                "開始打字篩選，再㩒 Enter 跑最上面嗰個結果。",
            },
            Keywords = "command palette launcher run alt space calculator system action web search 指令面板 啟動器 計算機",
        },
        new ManualEntry
        {
            Tag = "module.contextmenu", Glyph = "",
            TitleEn = "Context Menu", TitleZh = "右鍵選單",
            SummaryEn = "Add your own commands to the Windows right-click menu — run a script, open a tool, anything.",
            SummaryZh = "喺 Windows 右鍵選單加你自己嘅指令 — 跑 script、開工具，乜都得。",
            StepsEn = new[]
            {
                "Pick the scope (file, folder, desktop background, etc.) and type a menu label.",
                "Enter the command to run, or click Browse to pick a program; add an icon if you like.",
                "Try the PowerShell / Command Prompt presets for a quick start.",
                "Click Add to write the entry; right-click in Explorer to see it. Use the trash button to remove one.",
            },
            StepsZh = new[]
            {
                "揀範圍（檔案、資料夾、桌面背景等等）再打選單標籤。",
                "輸入要跑嘅指令，或者撳 Browse 揀程式；想要嘅可以加 icon。",
                "可以試吓 PowerShell／Command Prompt 預設快手啲。",
                "撳 Add 寫入；喺檔案總管撳右鍵就見到。撳垃圾桶掣可以移除。",
            },
            Keywords = "context menu right click verb script explorer 右鍵 選單 指令",
        },
        new ManualEntry
        {
            Tag = "module.taskbar-tweaker", Glyph = "",
            TitleEn = "Taskbar Tweaker", TitleZh = "工作列調校",
            SummaryEn = "Apply registry-backed taskbar tweaks (alignment, combine buttons, tray, multi-monitor) and launch 7+ Taskbar Tweaker for the deep stuff.",
            SummaryZh = "套用登錄檔嘅工作列調校（對齊、合併按鈕、系統匣、多螢幕），深層行為就交畀 7+ Taskbar Tweaker。",
            StepsEn = new[]
            {
                "Browse or filter the tweak cards (alignment, combine buttons, search box, Task View, tray icons, seconds clock, etc.).",
                "Flip a card's switch to apply it; some need an Explorer restart to show.",
                "Click Restart Explorer if a tweak hasn't appeared yet.",
                "If 7+ Taskbar Tweaker or Windhawk is detected, use the Launch button for deep behaviours WinForge can't do natively.",
            },
            StepsZh = new[]
            {
                "瀏覽或者篩選調校卡（對齊、合併按鈕、搜尋框、Task View、系統匣圖示、秒時鐘等等）。",
                "撳卡片嘅開關套用；有啲要重啟 Explorer 先見到。",
                "如果調校未出，撳 Restart Explorer。",
                "如果偵測到 7+ Taskbar Tweaker 或者 Windhawk，撳 Launch 用嗰啲 WinForge 原生做唔到嘅深層行為。",
            },
            TipEn = "Deep behaviours (middle-click to close, scroll-to-switch) need the real 7+TT or Windhawk mods.",
            TipZh = "深層行為（中鍵關閉、滾輪切換）要靠真正嘅 7+TT 或者 Windhawk 模組。",
            Keywords = "taskbar tweaker 7+ align combine tray multi monitor seconds clock 工作列 調校 對齊 合併 系統匣",
        },
        new ManualEntry
        {
            Tag = "module.lightswitch", Glyph = "",
            TitleEn = "LightSwitch (Auto Dark Mode)", TitleZh = "自動深淺色",
            SummaryEn = "Automatically switch between light and dark mode on a fixed schedule or by local sunrise/sunset.",
            SummaryZh = "按固定時間或者本地日出日落，自動喺淺色同深色模式之間切換。",
            StepsEn = new[]
            {
                "Use Light now / Dark now to switch instantly, and choose the scope (apps, system, or both).",
                "Pick a mode: Off, Fixed hours, or Sunrise / sunset.",
                "For fixed hours, set the light and dark times; for sun mode, enter your latitude/longitude or click Detect.",
                "Turn on the background job switch so it keeps switching even when WinForge is closed.",
            },
            StepsZh = new[]
            {
                "用 Light now／Dark now 即刻切換，再揀範圍（apps、system 或者兩樣）。",
                "揀模式：Off、Fixed hours 或者 Sunrise／sunset。",
                "固定時間就設淺色同深色時間；日出日落模式就填緯度／經度或者撳 Detect。",
                "撳開背景工作開關，咁就算 WinForge 關咗都會繼續切換。",
            },
            Keywords = "lightswitch auto dark mode theme schedule sunrise sunset 自動 深淺色 深色 淺色 排程 日出 日落",
        },
        new ManualEntry
        {
            Tag = "module.nilesoftshell", Glyph = "",
            TitleEn = "Nilesoft Shell", TitleZh = "Nilesoft 右鍵選單",
            SummaryEn = "Install and configure Nilesoft Shell, a modern themeable replacement for the Explorer right-click menu.",
            SummaryZh = "安裝同設定 Nilesoft Shell — 一個現代、可換主題嘅檔案總管右鍵選單替代品。",
            StepsEn = new[]
            {
                "If it's not installed, use the install button (winget Nilesoft.Shell).",
                "Use Register / Unregister / Reload to turn the custom menu on or off; Restart Explorer to apply.",
                "Edit shell.nss in the config editor; insert ready-made snippets (Copy as path, Open PowerShell here, theme.dark, etc.).",
                "Click Backup before saving, then Save & reload; use Restore default if something breaks.",
            },
            StepsZh = new[]
            {
                "如果未裝，撳安裝掣（winget Nilesoft.Shell）。",
                "用 Register／Unregister／Reload 開關自訂選單；撳 Restart Explorer 套用。",
                "喺設定編輯器改 shell.nss；插入現成片語（Copy as path、Open PowerShell here、theme.dark 等等）。",
                "存之前撳 Backup，再撳 Save & reload；搞壞咗就撳 Restore default。",
            },
            TipEn = "shell.nss lives under Program Files, so editing and registering need admin; always back up first.",
            TipZh = "shell.nss 喺 Program Files 底下，所以改同註冊都要管理員；記得先 backup。",
            Keywords = "nilesoft shell context menu nss register reload theme snippet 右鍵 選單 主題 註冊 片語",
        },
        new ManualEntry
        {
            Tag = "module.windows", Glyph = "",
            TitleEn = "Window Manager", TitleZh = "視窗管理",
            SummaryEn = "List every open window and snap, tile, move or pin them with one click.",
            SummaryZh = "列出每個開住嘅視窗，一撳就貼齊、平鋪、移動或者置頂。",
            StepsEn = new[]
            {
                "Click Refresh to list all open windows.",
                "Select a window in the list on the left.",
                "Use the snap pad on the right to move it to a half, quarter, or other position.",
                "The same panel lets you set always-on-top or center the window.",
            },
            StepsZh = new[]
            {
                "撳 Refresh 列出所有開住嘅視窗。",
                "喺左邊清單揀一個視窗。",
                "用右邊嘅貼齊面板將佢移去半邊、四分一或者其他位置。",
                "同一個面板仲可以將視窗置頂或者置中。",
            },
            Keywords = "window manager snap tile always on top center 視窗 貼齊 平鋪 置頂 置中",
        },
        new ManualEntry
        {
            Tag = "module.workspaces", Glyph = "",
            TitleEn = "Workspaces", TitleZh = "工作區",
            SummaryEn = "Capture a set of open apps and their window positions, then relaunch the whole layout in one click.",
            SummaryZh = "擷取一組開住嘅 app 同佢哋嘅視窗位置，之後一撳就重開成個佈局。",
            StepsEn = new[]
            {
                "Arrange your apps and windows the way you want them.",
                "Click Capture to save the current set as a workspace.",
                "Select a workspace and click Launch to reopen all its apps in place.",
                "Use Recapture, Rename, Export or Delete to manage saved workspaces.",
            },
            StepsZh = new[]
            {
                "將你啲 app 同視窗排好。",
                "撳 Capture 將而家呢組存做一個工作區。",
                "揀一個工作區再撳 Launch，就會原位重開晒所有 app。",
                "用 Recapture、Rename、Export 或者 Delete 管理已存嘅工作區。",
            },
            Keywords = "workspaces capture layout relaunch app positions 工作區 擷取 佈局 重開",
        },
        new ManualEntry
        {
            Tag = "module.altsnap", Glyph = "",
            TitleEn = "AltSnap", TitleZh = "Alt 拖曳視窗",
            SummaryEn = "Move and resize any window by holding a modifier (Alt by default) and dragging anywhere inside it.",
            SummaryZh = "撳住修飾鍵（預設 Alt）喺視窗任何位置拖，就可以移動同縮放視窗。",
            StepsEn = new[]
            {
                "If AltSnap isn't installed, click Install (winget RamonUnch.AltSnap).",
                "Click Launch to start it; hold Alt and drag a window to move it, Alt + right-drag to resize.",
                "Turn on Run at startup so it loads with Windows.",
                "Tweak the curated options (modifier key, snapping, top-maximizes) and restart AltSnap to apply.",
            },
            StepsZh = new[]
            {
                "如果未裝 AltSnap，撳 Install（winget RamonUnch.AltSnap）。",
                "撳 Launch 起動佢；撳住 Alt 拖視窗就移動，Alt + 右鍵拖就縮放。",
                "撳開 Run at startup，等佢跟 Windows 一齊開。",
                "調好嗰啲精選選項（修飾鍵、貼齊、頂部最大化）再重啟 AltSnap 套用。",
            },
            TipEn = "To control admin windows, launch AltSnap elevated; config changes need a restart.",
            TipZh = "想控制管理員視窗就要用管理員身分起 AltSnap；改完設定要重啟先生效。",
            Keywords = "altsnap alt drag move resize modifier snap 拖曳 移動 縮放 修飾鍵 貼齊",
        },
        new ManualEntry
        {
            Tag = "module.fancyzones", Glyph = "",
            TitleEn = "FancyZones", TitleZh = "視窗分區",
            SummaryEn = "PowerToys window tiling: define zones, then hold Shift while dragging to snap windows into them.",
            SummaryZh = "PowerToys 視窗平鋪：自訂分區，拖視窗嗰陣撳住 Shift 就貼入去。",
            StepsEn = new[]
            {
                "If PowerToys isn't installed, use the install button (winget Microsoft.PowerToys).",
                "Turn on the FancyZones module switch, then click Open Zone Editor to design your layout.",
                "Pick a built-in layout (Focus, Columns, Rows, Grid, Priority Grid) or make a custom one.",
                "Hold Shift while dragging a window to drop it into a zone; Win+Ctrl+Arrow moves between zones.",
            },
            StepsZh = new[]
            {
                "如果未裝 PowerToys，撳安裝掣（winget Microsoft.PowerToys）。",
                "撳開 FancyZones 模組開關，再撳 Open Zone Editor 設計你嘅佈局。",
                "揀一個內置佈局（Focus、Columns、Rows、Grid、Priority Grid）或者整個自訂。",
                "拖視窗嗰陣撳住 Shift 就放入分區；Win+Ctrl+方向鍵喺分區之間移動。",
            },
            Keywords = "fancyzones powertoys zones tiling shift drag win ctrl arrow layout 分區 平鋪 貼齊 佈局",
        },
        new ManualEntry
        {
            Tag = "module.komorebi", Glyph = "",
            TitleEn = "Komorebi (Tiling WM)", TitleZh = "Komorebi 平鋪視窗管理",
            SummaryEn = "Control the Komorebi tiling window-manager daemon: start it, switch layouts, navigate workspaces and edit its config.",
            SummaryZh = "操控 Komorebi 平鋪視窗管理守護程序：起動佢、切換佈局、navigate 工作區同改設定。",
            StepsEn = new[]
            {
                "If Komorebi isn't installed, use the install button (winget LGUG2Z.komorebi); create a default config if prompted.",
                "Click Start to launch the daemon; the status dot turns green and the monitor / workspace tree fills in.",
                "Pick a layout (bsp, columns, rows, grid, etc.) and click Apply, or cycle next/previous.",
                "Use the workspace controls to focus or move windows; add float/workspace rules and reload the config.",
            },
            StepsZh = new[]
            {
                "如果未裝 Komorebi，撳安裝掣（winget LGUG2Z.komorebi）；有提示就整個預設設定。",
                "撳 Start 起守護程序；狀態點變綠，monitor／工作區樹就會填好。",
                "揀一個佈局（bsp、columns、rows、grid 等等）撳 Apply，或者 cycle 上一個／下一個。",
                "用工作區控制 focus 或者移動視窗；加 float／workspace 規則再 reload 設定。",
            },
            TipEn = "Komorebi rearranges your windows once running; WinForge manages the daemon, not the keybindings (use whkd).",
            TipZh = "Komorebi 一開就會重排你啲視窗；WinForge 管守護程序，唔管鍵盤綁定（用 whkd）。",
            Keywords = "komorebi komorebic tiling daemon layout bsp workspace 平鋪 守護程序 佈局 工作區",
        },
        new ManualEntry
        {
            Tag = "module.glazewm", Glyph = "",
            TitleEn = "GlazeWM Tiling", TitleZh = "GlazeWM 平鋪視窗",
            SummaryEn = "Run the GlazeWM tiling window manager and edit its config (gaps, workspaces, startup) in-app.",
            SummaryZh = "跑 GlazeWM 平鋪視窗管理，喺 app 內改佢嘅設定（gaps、工作區、開機指令）。",
            StepsEn = new[]
            {
                "If GlazeWM isn't installed, use the install button (winget glzr-io.glazewm).",
                "Click Start to run the daemon; use Stop or Reload to control it and tick Start with Windows if you like.",
                "In the structured editor set inner/outer gap and focus-on-hover, then Save (it auto-reloads).",
                "Add or rename workspaces, or drop to the raw YAML editor for anything not modelled.",
            },
            StepsZh = new[]
            {
                "如果未裝 GlazeWM，撳安裝掣（winget glzr-io.glazewm）。",
                "撳 Start 跑守護程序；用 Stop 或者 Reload 控制佢，想要就剔 Start with Windows。",
                "喺結構化編輯器設 inner／outer gap 同 focus-on-hover，再撳 Save（會自動 reload）。",
                "加或者改工作區名，其他未模型化嘅就去 raw YAML 編輯器改。",
            },
            TipEn = "GlazeWM aggressively manages all windows once started; the config lives at ~/.glzr/glazewm/config.yaml.",
            TipZh = "GlazeWM 一開就會大力管晒所有視窗；設定喺 ~/.glzr/glazewm/config.yaml。",
            Keywords = "glazewm tiling daemon gaps workspace yaml config 平鋪 守護程序 邊距 工作區 設定",
        },
        new ManualEntry
        {
            Tag = "module.fonts", Glyph = "",
            TitleEn = "Font Manager", TitleZh = "字型管理",
            SummaryEn = "Install, preview and remove fonts, with a live sample so you can see each typeface.",
            SummaryZh = "安裝、預覽同移除字型，有即時樣本畀你睇每款字體。",
            StepsEn = new[]
            {
                "Click Browse to pick .ttf / .otf files to install; tick Install for all users for machine-wide.",
                "Type your own text in the sample box to preview every installed font.",
                "Scroll the list to find a font; each row shows the live preview.",
                "Use a font's remove action to uninstall it, then Refresh to update the list.",
            },
            StepsZh = new[]
            {
                "撳 Browse 揀 .ttf／.otf 檔安裝；想全機用就剔 Install for all users。",
                "喺樣本格打你自己嘅文字，預覽每款已裝字型。",
                "捲動清單搵字型；每行都有即時預覽。",
                "撳字型嘅移除動作解除安裝，再撳 Refresh 更新清單。",
            },
            TipEn = "Installing for all users needs administrator rights.",
            TipZh = "幫全部使用者安裝要管理員權限。",
            Keywords = "font install preview uninstall ttf otf typeface 字型 安裝 預覽 移除 字體",
        },
        new ManualEntry
        {
            Tag = "module.awake", Glyph = "",
            TitleEn = "Awake", TitleZh = "保持喚醒",
            SummaryEn = "Keep your PC awake (and optionally the screen on) without changing your power settings.",
            SummaryZh = "唔使改電源設定就令部電腦保持唔瞓（仲可以順便唔熄螢幕）。",
            StepsEn = new[]
            {
                "Turn on the Awake switch to keep the PC from sleeping.",
                "Tick Keep screen on if you also want to stop the display turning off.",
                "Set a number of minutes for a timed session, or leave 0 to stay awake indefinitely.",
                "Turn the switch off to hand control back to your normal power plan.",
            },
            StepsZh = new[]
            {
                "撳開 Awake 開關令部電腦唔瞓。",
                "想連螢幕都唔熄就剔 Keep screen on。",
                "想定時就設分鐘數，留 0 就一直唔瞓。",
                "撳熄個開關就交返畀你平時嘅電源計劃。",
            },
            Keywords = "awake keep awake no sleep caffeine display 唔瞓 喚醒 螢幕 電源",
        },
        new ManualEntry
        {
            Tag = "module.advancedpaste", Glyph = "",
            TitleEn = "Advanced Paste", TitleZh = "進階貼上",
            SummaryEn = "Transform whatever's on your clipboard before pasting — plain text, Markdown, JSON, case changes and more.",
            SummaryZh = "貼上之前先轉換剪貼簿內容 — 純文字、Markdown、JSON、大小寫轉換等等。",
            StepsEn = new[]
            {
                "Turn on the switch and pick the hotkey (e.g. Win+Shift+V) and a default action.",
                "In Try it now, choose a transform and click Preview to see the result on your current clipboard.",
                "Click Copy result to put the transformed text back on the clipboard.",
                "Use the actions checklist to enable just the transforms you want in the popup.",
            },
            StepsZh = new[]
            {
                "撳開開關，揀熱鍵（例如 Win+Shift+V）同預設動作。",
                "喺 Try it now 揀一個轉換，撳 Preview 睇下而家剪貼簿轉完點。",
                "撳 Copy result 將轉換好嘅文字放返去剪貼簿。",
                "用動作清單剔返你想喺彈窗出現嘅轉換。",
            },
            TipEn = "Smart / AI transforms need an AI provider configured; the plain transforms work offline.",
            TipZh = "Smart／AI 轉換要設定 AI 供應商；普通轉換離線都用得。",
            Keywords = "advanced paste transform plain text markdown json case win shift v 進階 貼上 轉換 純文字 大小寫",
        },
        new ManualEntry
        {
            Tag = "module.powertoys", Glyph = "",
            TitleEn = "PowerToys Extras", TitleZh = "PowerToys 額外工具",
            SummaryEn = "Four built-in PowerToys-style utilities: Image Resizer, Text Extractor (OCR), Always On Top and Paste as Plain Text.",
            SummaryZh = "四個內置 PowerToys 風格工具：圖片縮放、文字擷取（OCR）、視窗置頂同純文字貼上。",
            StepsEn = new[]
            {
                "Image Resizer: add images, pick a size preset, set an output folder and click Run.",
                "Text Extractor: pick a language, click the OCR button to grab text from screen, then Copy.",
                "Always On Top: refresh the window list and toggle pin on any window.",
                "Paste as Plain Text: strip formatting from the clipboard, or turn on its hotkey.",
            },
            StepsZh = new[]
            {
                "Image Resizer：加圖片、揀尺寸預設、設輸出資料夾再撳 Run。",
                "Text Extractor：揀語言、撳 OCR 掣由畫面攞文字，再撳 Copy。",
                "Always On Top：刷新視窗清單，喺任何視窗 toggle 置頂。",
                "Paste as Plain Text：清走剪貼簿格式，或者開佢嘅熱鍵。",
            },
            Keywords = "powertoys image resizer ocr text extractor always on top paste plain text 圖片縮放 文字擷取 置頂 純文字",
        },
        new ManualEntry
        {
            Tag = "module.windhawk", Glyph = "",
            TitleEn = "Windhawk Mods", TitleZh = "Windhawk 模組",
            SummaryEn = "Install Windhawk and browse a curated gallery of popular mods that customize the taskbar, clock, Start menu and more.",
            SummaryZh = "安裝 Windhawk，瀏覽精選熱門模組嚟自訂工作列、時鐘、開始功能表等等。",
            StepsEn = new[]
            {
                "If Windhawk isn't installed, use the install button (winget RamenSoftware.Windhawk).",
                "Click Launch Windhawk to open its UI for installing and configuring mods.",
                "Browse or filter the curated mod gallery (taskbar height, clock customization, Start Menu Styler, classic taskbar, etc.).",
                "Click Open in Windhawk on a mod to deep-link straight to its page.",
            },
            StepsZh = new[]
            {
                "如果未裝 Windhawk，撳安裝掣（winget RamenSoftware.Windhawk）。",
                "撳 Launch Windhawk 開佢嘅介面去安裝同設定模組。",
                "瀏覽或者篩選精選模組（工作列高度、時鐘自訂、Start Menu Styler、經典工作列等等）。",
                "撳模組上嘅 Open in Windhawk 直接跳去佢嘅頁面。",
            },
            TipEn = "Windhawk needs elevation and installs a service; the install may trigger a UAC prompt.",
            TipZh = "Windhawk 要管理員權限同會裝個服務；安裝可能會彈 UAC。",
            Keywords = "windhawk mod mods taskbar clock start menu styler classic 模組 工作列 時鐘 開始功能表",
        },
        new ManualEntry
        {
            Tag = "module.voice", Glyph = "",
            TitleEn = "Voice & Read-Aloud", TitleZh = "語音朗讀",
            SummaryEn = "Type or paste text and have Windows read it aloud, or export the speech to a WAV file.",
            SummaryZh = "打字或者貼文字，等 Windows 讀出嚟，或者匯出做 WAV 檔。",
            StepsEn = new[]
            {
                "Type or paste the text into the box.",
                "Pick a voice, then adjust the rate and volume sliders.",
                "Click Play to hear it (Stop to halt).",
                "Click Export to save the speech as a WAV file.",
            },
            StepsZh = new[]
            {
                "喺格仔打字或者貼文字。",
                "揀一把聲，再調速度同音量滑桿。",
                "撳 Play 聽（撳 Stop 停）。",
                "撳 Export 將語音存做 WAV 檔。",
            },
            Keywords = "voice tts text to speech read aloud wav sapi 語音 朗讀 文字轉語音 讀出",
        },
        new ManualEntry
        {
            Tag = "module.rainmeter", Glyph = "",
            TitleEn = "Rainmeter Widgets", TitleZh = "Rainmeter 桌面小工具",
            SummaryEn = "Install Rainmeter, load or unload desktop skins, install .rmskin packs and run common skin commands.",
            SummaryZh = "安裝 Rainmeter、載入或者卸載桌面皮膚、安裝 .rmskin 包同跑常用皮膚指令。",
            StepsEn = new[]
            {
                "If Rainmeter isn't installed, use the install button (winget Rainmeter.Rainmeter).",
                "On the Skins tab, click Rescan, then flip a skin's toggle to load or unload it.",
                "Use the per-skin buttons to show, hide, refresh or edit a skin's ini.",
                "Click Install .rmskin to add a downloaded skin pack; the Layouts and Operations tabs cover the rest.",
            },
            StepsZh = new[]
            {
                "如果未裝 Rainmeter，撳安裝掣（winget Rainmeter.Rainmeter）。",
                "喺 Skins 分頁撳 Rescan，再撳皮膚嘅 toggle 載入或者卸載。",
                "用每個皮膚嘅掣去顯示、隱藏、重新整理或者編輯佢嘅 ini。",
                "撳 Install .rmskin 加下載返嚟嘅皮膚包；其餘嘅喺 Layouts 同 Operations 分頁。",
            },
            Keywords = "rainmeter skin skins widget desktop rmskin bang load unload 桌面 小工具 皮膚 美化",
        },
    };
}
