# Filter Keys & Slow Keys · 篩選鍵同慢速鍵

## Behavior · 行為

The workflow controls Filter Keys enablement, delay before acceptance, auto-repeat delay, auto-repeat rate, and bounce time. It writes the documented Keyboard Response profile and timing values, then calls `SystemParametersInfo(SPI_SETFILTERKEYS)` with persistent/live notification flags. The Windows 11 catalog toggle reuses the same path instead of writing a conflicting flag by itself.

流程會控制篩選鍵開關、接受前延遲、自動重複延遲／間距同彈跳忽略時間；寫入後會用 `SPI_SETFILTERKEYS` 即時套用。Windows 11 目錄開關亦重用同一路徑，唔會再單獨寫一個互相衝突嘅 flag。

## Failure and accessibility · 失敗同無障礙

Every timing is bounded to 0–20,000 ms. Windows API rejection is surfaced as a persistent error rather than success. Controls have bilingual accessible names and remain keyboard reachable.

每個時間值限制喺 0–20,000 毫秒；Windows API 拒絕會如實顯示錯誤。控制有雙語無障礙名稱，可用鍵盤操作。

## Verification · 驗證

Focused tests cover the upper bound, oversized rejection, registry profiles, and the exact live on-bit transition. Headless smoke does not intentionally change the operator's accessibility settings.
