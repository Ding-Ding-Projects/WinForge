# Reactor Industrial Loads · 反應堆工業負載

**EN —** WinForge now includes two focused industrial loads that consume the flagship reactor's live simulated electrical bus. Both are in-process, bilingual, reversible, and fail dark; neither controls real equipment or changes reactor protection.

**粵語 —** WinForge 而家有兩個專項工業負載，會用旗艦反應堆嘅即時模擬電力母線。兩個都係程式內、雙語、可還原，而且冇電就停；唔會控制真實設備或者改反應堆保護。

## Ammonia / Fertilizer Plant · 合成氨／肥料廠

Open · 開啟：`WinForge.exe --page ammonia` (`fertilizer`, `fertiliser`)

The Haber–Bosch model turns electrolytic hydrogen and air-separated nitrogen into green ammonia. The operator selects 0–350 MW; the default 280 MW is above the model's approximately 263 MW steady synthesis threshold. Loop pressure must exceed 150 bar before production starts. Reactor loss stops production and bleeds pressure toward ambient. Duplicate ticks never manufacture extra pressure or tonnes.

哈柏法模型會將電解氫同空分氮變成綠氨。操作員可以揀 0–350 MW；預設 280 MW 高過模型約 263 MW 嘅穩定合成門檻。迴路壓力要超過 150 bar 先開始生產。反應堆失電會停產並將壓力降向環境值；重複 tick 絕對唔會憑空增加壓力或者噸數。

## Grid Load-Shed Dispatcher · 電網卸載調度器

Open · 開啟：`WinForge.exe --page loadshed` (`mwbudget`)

Eight city feeders total 990 MW of enabled demand. The dispatcher subtracts 0–30% spinning reserve, then serves complete feeders from P1 to P5. The first feeder that does not fit and everything below it shed immediately; reclose waits ten distinct stable ticks. A cold bus reports enabled demand as shed without inventing trip events, and duplicate ticks cannot add unserved MWh or anti-flap progress.

八條城市饋線全部啟用時合共需求 990 MW。調度器先扣 0–30% 旋轉備用，再由 P1 到 P5 完整供電。第一條放唔入預算嘅饋線同以下全部會即時卸載；重合閘要等十個唔重複穩定 tick。冷母線會如實顯示啟用需求已卸載，但唔會虛構跳脫事件；重複 tick 亦唔會增加未供電 MWh 或防拍翼進度。

## Accessibility, failure, and safety · 無障礙、故障同安全

Both pages wrap long bilingual labels, use theme semantic brushes, keep interactive targets at least 44 pixels high, expose automation names/help, and communicate state with text rather than color alone. Non-finite power values fail closed to zero. Simulation state remains local except for the ammonia page's existing, explicit Reactor Bank economy credit path.

兩頁都會換行顯示長雙語標籤、使用 theme 語意 brush、互動目標最少 44 像素高、提供 automation 名稱／說明，並用文字而唔係淨靠顏色表達狀態。非有限功率會 fail closed 當零。除咗合成氨頁既有、明確嘅 Reactor Bank 經濟入帳路徑，模擬狀態全部留喺本機。

## Verification · 驗證

The Windows x64 solution builds with zero errors and the production-source harness passes **65/65** scenarios. Both deep links opened in fresh WinUI windows on a dedicated LowLevel headless desktop. Screenshot capture is honestly **blocked**: both 1574×887 client captures were solid black, the repository driver rejected a blank fallback, and the attempted visible-desktop switch was denied. No invalid image is published. Detailed feature records: [Ammonia Plant](../features/reactor-loads/ammonia-fertilizer-plant.md) and [Load-Shed Dispatcher](../features/reactor-loads/grid-load-shed-dispatcher.md).

Windows x64 solution 零 errors，直接編譯正式 service source 嘅 harness **65/65** 全過。兩個 deep link 都喺專用 LowLevel headless desktop 成功開出新 WinUI 視窗。截圖如實係 **blocked**：兩張 1574×887 client capture 都係全黑，repo driver 亦拒絕空白 fallback，而嘗試切換到可見 desktop 就被拒絕。冇無效圖片會發佈。詳細功能記錄：[合成氨廠](../features/reactor-loads/ammonia-fertilizer-plant.md) 同 [卸載調度器](../features/reactor-loads/grid-load-shed-dispatcher.md)。

---

[← Reactor Hub · 返回反應堆中心](Reactor-Hub.md)
