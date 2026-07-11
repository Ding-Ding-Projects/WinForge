# Accessibility · 無障礙

WinForge follows a keyboard-first, screen-reader-aware baseline for the main shell and new native OSS tabs. · WinForge 主外殼同新原生開源分頁採用鍵盤優先、照顧螢幕閱讀器嘅基本標準。

## Current Shell Coverage · 目前外殼覆蓋

- Main navigation, global search, open module tabs and tab-session actions have explicit automation names. · 主要導航、全域搜尋、已開啟模組分頁同分頁工作階段動作都有明確 automation 名稱。
- Main navigation, open tab strip, individual tab items, session actions and generated navigation items also expose stable automation IDs for UI automation. · 主要導航、已開啟分頁列、個別分頁項目、工作階段動作同產生出嚟嘅導航項目亦有穩定 automation ID，方便 UI 自動化。
- Tab session flyout commands, generated tab context commands and app update notices expose stable automation IDs for shell tests. · 分頁工作階段 flyout 指令、產生出嚟嘅分頁右鍵指令同 app 更新通知都有穩定 automation ID，方便外殼測試。
- The new-tab picker exposes a named search box, category filter and named app-result buttons, so automation can open frequent apps, suggestions or category-filtered modules without relying on visual text only. · 新分頁選擇器提供具名搜尋框、分類篩選同具名 app 結果按鈕，所以自動化可以開啟常用 app、建議項目或者按分類篩選嘅模組，唔使只靠畫面文字。
- The new-tab picker focuses search when it opens; typing a query and pressing `Enter` opens the first rendered result. Section labels are exposed as headings, and result buttons expose both title and context in their automation names. · 新分頁選擇器開啟時會聚焦搜尋；輸入查詢再撳 `Enter` 會開啟第一個已顯示結果。區段標籤會以標題形式提供畀輔助工具，結果按鈕嘅 automation 名稱亦同時包括標題同語境。
- Shell search suggestions now carry stable route keys, so choosing a module or tweak category opens it directly while plain text still falls back to the search-results page. · 外殼搜尋建議而家帶穩定路由 key，揀模組或者調校分類會直接開啟；直接輸入文字仍然會回落到搜尋結果頁。
- Shell search suggestions show both the destination name and action context, making module/category/app-picker results easier to distinguish before opening. · 外殼搜尋建議會同時顯示目的地名稱同操作語境，開啟前更容易分清模組、分類或者 app 選擇器結果。
- Picker cards and dashboard stats use responsive sizing and wrapped bilingual text to reduce clipping at narrow widths or higher text scale. · 選擇器卡片同概覽統計用響應式尺寸同可換行雙語文字，減少窄視窗或者較大文字比例時被截斷。
- New native pages expose heading levels for page title and major content regions. · 新原生頁面為頁面標題同主要內容區域提供標題層級。
- The Feed Reader supports keyboard flow: `Enter` adds the typed feed URL, `F5` refreshes feeds, and `Ctrl+C` copies the selected article link. · RSS 閱讀器支援鍵盤流程：`Enter` 新增已輸入 feed 網址、`F5` 重新整理、`Ctrl+C` 複製已選文章連結。
- Copy-link and busy states are reflected through enabled/disabled controls and named progress indicators. · 複製連結同忙碌狀態會透過啟用／停用控制項同具名進度指示器反映。

## Standard For New Tabs · 新分頁標準

Every new WinForge tab should keep user workflows in-app and include automation names for icon-only or ambiguous controls, heading levels for screen-reader navigation, keyboard access for primary actions, and visible enabled/disabled states while work is running. · 每個新 WinForge 分頁都應保持流程喺 app 內完成，並為純圖示或容易混淆嘅控制項加入 automation 名稱、為螢幕閱讀器導航加入標題層級、為主要動作提供鍵盤操作，以及喺工作執行中顯示清楚啟用／停用狀態。

## Light and Dark Contrast · 淺色與深色對比

**EN —** The reactor-green palette is selected through theme resources, not a fixed foreground color. Dark mode uses `#54E07E` on near-black surfaces with dark on-brand ink; Light mode uses `#0F6B3A` for readable accent text and `#FFFFFF` on green-filled cards. The shared primary, secondary, and tertiary text brushes are also explicitly contrast-safe for the light shell.

**粵語 —** 反應堆綠色調色板而家會經主題資源選取，唔會再固定做前景色。深色模式喺近黑表面用 `#54E07E`，並喺品牌色卡片用深色字；淺色模式就用易讀嘅 `#0F6B3A` 做強調文字，同時喺綠色卡片用 `#FFFFFF`。共用主要、次要同第三層文字筆刷亦都為淺色殼明確設定咗安全對比。
