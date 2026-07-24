# Reactor Industrial Loads · 反應堆工業負載

**EN —** Two local, reversible industrial simulations consume the flagship reactor's live electrical bus. Neither controls real equipment or changes reactor protection.

**粵語 —** 兩個本機、可還原工業模擬會用旗艦反應堆嘅即時電力母線；兩者都唔會控制真實設備或者改反應堆保護。

## Ammonia / Fertilizer Plant · 合成氨／肥料廠

Open · 開啟：`WinForge.exe --page ammonia` (`fertilizer`, `fertiliser`)

The Haber–Bosch model uses 0–350 MW for electrolysis and compression. Its 280 MW default is above the approximately 263 MW steady threshold needed to hold 150 bar and produce ammonia. Reactor loss stops output and depressurises the loop; duplicate ticks do not create extra pressure or tonnes.

哈柏法模型用 0–350 MW 做電解同壓縮。預設 280 MW 高過守住 150 bar 並生產合成氨所需嘅約 263 MW 穩定門檻。反應堆失電會停產降壓；重複 tick 唔會憑空增加壓力或者噸數。

## Grid Load-Shed Dispatcher · 電網卸載調度器

Open · 開啟：`WinForge.exe --page loadshed` (`mwbudget`)

Eight P1–P5 feeders total 990 MW. After reserving 0–30%, complete feeders are served in strict priority order; the first feeder that does not fit and every lower priority shed. A cold bus reports enabled demand as shed without inventing a trip, and duplicate ticks do not add unserved MWh or reclose progress.

八條 P1–P5 饋線合共 990 MW。預留 0–30% 後，饋線會按嚴格優先級完整供電；第一條放唔落預算嘅饋線同所有較低優先級都會卸載。冷母線會顯示啟用需求已卸載但唔虛構跳脫；重複 tick 亦唔會增加未供電 MWh 或重合閘進度。

## Verification · 驗證

Windows x64 build: zero errors. Reactor/dependent harness: **65/65**. Both routes opened on a dedicated LowLevel headless desktop. Visual status is `capture-blocked` because both inspected 1574×887 WinUI frames were solid black, the repository driver rejected its blank fallback, and switching the headless desktop visible was denied. No invalid PNG is published.

Windows x64 build 零 errors；Reactor／相依 harness **65/65**。兩個 route 都喺專用 LowLevel headless desktop 成功開啟。視覺狀態係 `capture-blocked`：兩張已檢視 1574×887 WinUI frame 全黑、repo driver 拒絕空白 fallback，而且 headless desktop 無法切換可見；冇無效 PNG 會發佈。

[← Reactor Hub · 返回反應堆中心](Reactor-Hub.md)
