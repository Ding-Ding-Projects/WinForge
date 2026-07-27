# Reactor Test Report · 反應堆測試報告

**EN —** This GitHub Pages summary covers the headless verification harness for the real C# reactor engine and its fuel, waste, water, feature-power/EDG gating, industrial-load, and cake-factory dependencies, plus the focused Reactor Settings/session lifecycle contract. The harnesses compile or inspect production sources directly; they do not use a mock reactor.

**粵語 —** 呢份 GitHub Pages 摘要涵蓋真實 C# 反應堆引擎，同燃料、廢料、水處理、功能電源／EDG 閘門、工業負載、蛋糕工廠相依服務，加埋專項反應堆設定／session 生命週期合約嘅無介面驗證框架。佢會直接編譯或者檢查正式程式碼，唔係用假反應堆。

## Run and CI contract · 執行同 CI 規約

```powershell
# ReactorSim.Tests targets net8.0-windows.
dotnet run --project tests/ReactorSim.Tests -c Debug

# Safe source-contract lifecycle verification.
dotnet run --project tests/ReactorSettingsLifecycle.Tests -c Debug

# Fast deterministic check of the exit-code mapping only.
dotnet run --project tests/ReactorSim.Tests -c Debug -- --verify-exit-code-contract
```

**EN —** The normal run retains clear PASS/FAIL output for every scenario. It exits **0 only when every scenario passes** and exits **1** when any assertion fails or a scenario exception is caught. CI must fail the verification on every nonzero exit code.

**粵語 —** 正常運行會保留每個情景清楚嘅 PASS／FAIL 輸出。**全部情景通過**先會退出 **0**；任何斷言失敗或者捉到情景例外就會退出 **1**。CI 見到任何非零 exit code 都要當驗證失敗。

## Current verified result · 現時已驗證結果

**Latest verified run · 最新已驗證執行：** 2026-07-27

**67 / 67 scenarios PASS · 67／67 個情景全部通過**

**10 / 10 focused lifecycle tests PASS · 10／10 項專項生命週期測試全部通過**

The suites cover reactor physics and protection, all 16 accident enum values, fuel lifecycle, waste-cap and disk-floor controls, water treatment, default-off feature-power EDG policy, stopped-only manual 60 L diesel fill, 1.0 L/min burn while starting or running, the exact 10-second start, nuclear-preferred per-module gating, exact owner/module isolation, the two-concurrent-module-instance owner-lease cap, atomic parallel contention with exactly two winners, third-entry waiting, ammonia production, strict-priority load shedding, and Cake Factory page/model preservation with exact active-CIP progress freeze/resume on owner power loss. That focused Cake equality assertion covers powered CIP progress; the production model deliberately keeps passive biological/spoilage/transport/order clocks running. The suites also prove the canonical foreground/background reactor session, live read-only parallel observers with plant-input lockout, mutating-companion closure on authority demotion, safe read-only meltdown-overlay synchronization, page-owned real-effect cleanup, post-load visible ARM, automatic countdown abort before every foreground handoff, truthful session-global accepted/refused OS shutdown outcomes, and once-per-session command-line auto-start without invoking those real integrations. Permission persists, while generator, fuel, and owner leases are session-only; the other 19 reactor-industrial simulations remain nuclear-only. The canonical report at `docs/wiki/Reactor-Test-Report.md` contains the measured reactor evidence and detailed coverage table.

**粵語 —** 測試覆蓋反應堆物理／保護、全部 16 個事故 enum、燃料生命週期、廢料容量／磁碟底線、水處理、預設關閉嘅功能電源 EDG 政策、只限停機時手動加滿 60 L、啟動中／運行中每分鐘耗油 1.0 L、準確 10 秒啟動、核電優先逐模組閘門、準確 owner／module 隔離、兩個同時模組 instance owner lease 上限、並行爭位時準確兩個贏家、第三個等位、合成氨、嚴格優先級卸載，以及蛋糕工廠喺 owner 失電時保留頁面／模型兼精準凍結／恢復進行中 CIP 進度。呢個專項 Cake 相等檢查針對有電 CIP 進度；production model 刻意保留被動生物／變壞／運輸／訂單時鐘繼續運行。測試亦會證明正式前景／背景反應堆 session、並行頁面即時唯讀觀察兼鎖定機組輸入、authority 降級時關閉可改動 companion、安全同步唯讀熔毀 overlay、頁面持有真實效果嘅清理、載入後可見 ARM、每次前景交接之前自動中止倒數、如實嘅全 session OS 接受／拒絕關機結果，同每 session 一次命令列自動起動，而且唔會真正觸發嗰啲整合。權限會保存，發電機、燃油同 owner lease 就只限今次 session；其餘 19 個反應堆工業模擬繼續只用核電。

**2026-07-24 industrial-load visual evidence · 2026-07-24 工業負載視覺證據 —** `capture-blocked`. Both industrial-load pages launched on a dedicated LowLevel headless desktop, but their inspected 1574×887 WinUI client captures were solid black. The repository driver rejected a blank fallback and the visible-desktop fallback was denied. No invalid image is published or called a pass. · 兩個工業負載頁都成功喺專用 LowLevel headless desktop 開啟，但已檢視 client capture 全黑；repo driver 拒絕空白 fallback，而可見 desktop fallback 被拒絕。冇無效圖片會發佈或者當通過。

[← Reactor Hub · 反應堆總覽](Nuclear-Reactor.md) · [Developer · 開發者](Developer.md) · [Wiki Home · Wiki 主頁](Home.md)
