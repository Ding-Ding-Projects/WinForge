# Grid Load-Shed Dispatcher · 電網卸載調度器

Open in app · 喺 app 內開啟：`WinForge.exe --page loadshed`

Alias · 別名：`mwbudget`

## Behavior · 行為

**EN —** `GridLoadShedService` dispatches eight simulated city feeders with priorities P1–P5 and 990 MW of enabled catalog demand. Usable power is live reactor output minus an operator-selected spinning-reserve percentage. A strict cutoff serves complete feeders in priority order; when the next feeder does not fit, it and every lower-priority feeder shed. Tripping is immediate, while automatic reclose requires ten distinct stable ticks.

**粵語 —** `GridLoadShedService` 會調度八條模擬城市饋線，優先級 P1–P5，全部啟用時目錄需求係 990 MW。可用功率等於即時反應堆輸出扣除操作員揀嘅旋轉備用百分比。嚴格截斷會按優先級完整供電；下一條饋線放唔入預算時，佢同所有較低優先級饋線都會卸載。跳脫即時發生，自動重合閘就要等十個唔重複嘅穩定 tick。

## Configuration · 設定

- Spinning reserve: 0–30%, default 10%. · 旋轉備用：0–30%，預設 10%。
- Each feeder has an operator breaker. Turning one off removes it from enabled demand and does not count as unserved energy. · 每條饋線有操作員斷路器；手動關閉會由啟用需求移除，唔會當未供電能量。
- Reset restores the default reserve, enables all feeders, and clears service, shed-event, unserved-energy, and reclose state. · 重設會還原預設備用、啟用全部饋線，並清除供電、卸載事件、未供電能量同重合閘狀態。

## Failure modes · 故障模式

- On a cold or de-energised bus, all enabled demand is reported as shed and served power is zero. Because no feeder transitioned from served to shed, startup darkness does not invent shed events. · 母線冷停或者冇電時，全部啟用需求會顯示已卸載，供電功率係零；因為冇饋線由供電轉成卸載，所以起始黑暗唔會虛構卸載事件。
- A sag may shed multiple lower-priority feeders at once. Reclose progress resets if the budget stops fitting; this prevents breaker flapping. · 電壓／功率下跌可以一次卸載多條低優先級饋線；預算再次唔夠時，重合閘進度會清零，防止斷路器拍翼。
- Non-finite power, reserve, or demand values are sanitized. Duplicate or backward ticks cannot integrate unserved MWh or advance the reclose delay. · 非有限功率、備用或者需求值會被安全化；重複或者倒退 tick 唔會累積未供電 MWh 或者推進重合閘延遲。

## Accessibility and localization · 無障礙同本地化

The page uses separate English/Cantonese copy, wrapped captions, semantic theme brushes, 44-pixel minimum input targets, and responsive two-row feeder cards. Each breaker and feeder card exposes a localized automation name, priority, demand, and current status; status is conveyed by text as well as color.

頁面提供分開英文／粵語、可換行說明、theme 語意 brush、最少 44 像素輸入目標，同響應式雙行饋線卡。每個斷路器同饋線卡都有本地化 automation 名稱、優先級、需求同即時狀態；狀態除咗顏色亦有文字表達。

## Security and verification · 安全同驗證

This is a local dispatcher simulation; it does not connect to or switch a real grid. The focused harness covers cold-bus accounting, exact-fit healthy dispatch, a 350/640 MW sag split, unserved-MWh integration, ten-tick anti-flap reclose, operator-off semantics, blackout, reset, and duplicate-tick stability. The current Windows run passes as part of **65/65** scenarios.

呢個只係本機調度模擬，唔會連接或者切換真實電網。專項 harness 覆蓋冷母線計數、剛好放得落嘅健康調度、350/640 MW 下跌分割、未供電 MWh 累積、十 tick 防拍翼重合閘、操作員關閉語意、全黑、重設，同重複 tick 穩定性；目前 Windows 執行屬於 **65/65** 全綠結果。
