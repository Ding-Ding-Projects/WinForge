# Ammonia / Fertilizer Plant · 合成氨／肥料廠

Open in app · 喺 app 內開啟：`WinForge.exe --page ammonia`

Aliases · 別名：`fertilizer`, `fertiliser`

## Behavior · 行為

**EN —** The in-process `AmmoniaPlantService` models a green-ammonia Haber–Bosch train. Available reactor power feeds electrolysis and compression; loop pressure approaches a power-dependent equilibrium up to 200 bar. Production begins above 150 bar at roughly 10 MWh per tonne. Lifetime output records avoided grey-ammonia CO₂, and whole tonnes can enter the existing Reactor Bank economy.

**粵語 —** 程式內 `AmmoniaPlantService` 模擬綠氨哈柏法生產線。可用反應堆電力會供應電解同壓縮；合成迴路壓力按功率逼近平衡，最高 200 bar。超過 150 bar 先開始生產，每噸大約用 10 MWh。累積產量會計算相對灰氨減少嘅 CO₂，整噸產量亦可以進入既有 Reactor Bank 經濟。

## Configuration · 設定

- Power setpoint: 0–350 MW in 10 MW steps; default **280 MW**. · 功率設定：0–350 MW，每級 10 MW；預設 **280 MW**。
- The present pressure model needs about **263 MW** to hold the 150 bar synthesis threshold. A lower setpoint may pressurise the loop without reaching steady production. · 目前壓力模型大約要 **263 MW** 先可以守住 150 bar 合成門檻；較低設定可能只會加壓，未必到達穩定生產。
- Start/Stop changes operator intent; Reset stops the plant, restores the default setpoint, depressurises to ambient, and clears counters. · 開／停廠會改操作員意圖；重設會停廠、還原預設功率、降到環境壓力，同清除計數。

## Failure modes · 故障模式

- A cold, scrammed, melted, offline, or non-generating reactor supplies zero power; the plant stops producing and the loop bleeds toward ambient pressure. · 反應堆冷停、SCRAM、熔毀、離線或者冇發電時供電係零；工廠停止生產，迴路壓力會跌向環境值。
- Reactor output below the setpoint limits actual draw. Below the steady threshold, pressure and production respond continuously rather than pretending the requested power arrived. · 反應堆輸出低過設定點時，實際用電會受限；低過穩定門檻時，壓力同產量會連續反應，唔會扮已收到要求功率。
- Non-finite available-power inputs fail closed to zero. Duplicate or backward integer ticks update the instantaneous readout without advancing pressure or lifetime production. · 非有限可用功率會 fail closed 當零；重複或者倒退整數 tick 只更新即時讀數，唔會推進壓力或者累積產量。

## Accessibility and localization · 無障礙同本地化

The page uses separate English/Cantonese strings through the persisted language mode, wrapped labels for bilingual/narrow layouts, theme semantic brushes, 44-pixel minimum action targets, and automation names/help text for the power slider, meters, and dynamic Start/Stop action.

頁面跟持久語言模式分開提供英文／粵語，長標籤可以換行以支援雙語同窄版面，使用 theme 語意 brush、最少 44 像素操作目標，並為功率 slider、儀表同動態開／停廠動作提供 automation 名稱同說明。

## Security and verification · 安全同驗證

The module is a local simulation and does not actuate real plant equipment or add network access. The focused harness verifies idle gating, pressurisation, synthesis, CO₂ accounting, power-loss depressurisation, reset, and duplicate-tick stability. The current Windows run passes as part of **65/65** scenarios.

模組只係本機模擬，唔會控制真實廠房設備或者新增網絡存取。專項 harness 驗證閒置閘門、加壓、合成、CO₂ 計數、失電降壓、重設，同重複 tick 穩定性；目前 Windows 執行屬於 **65/65** 全綠結果。
