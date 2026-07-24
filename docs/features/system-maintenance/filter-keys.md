# Filter Keys & Slow Keys · 篩選鍵同慢速鍵

## Behavior · 行為

The workflow controls Filter Keys enablement, delay before acceptance, auto-repeat delay, auto-repeat rate, and bounce time. It reads the live `FILTERKEYS` structure with `SPI_GETFILTERKEYS`, preserves unrelated accessibility flags, then calls `SPI_SETFILTERKEYS` with profile-persistence and live-notification flags. The Windows 11 catalog toggle reuses that same API path instead of writing a conflicting registry profile by itself.

流程會控制篩選鍵開關、接受前延遲、自動重複延遲／間距同彈跳忽略時間；先用 `SPI_GETFILTERKEYS` 讀取並保留其他無障礙旗標，再用 `SPI_SETFILTERKEYS` 即時套用同儲存。Windows 11 目錄開關亦重用同一條 API 路徑，唔會再單獨寫一個互相衝突嘅 registry profile。

## Failure and accessibility · 失敗同無障礙

Every timing is bounded to 0–20,000 ms. The mutation path fails closed if Windows cannot first return the live structure, and any Windows API rejection is surfaced as a persistent error rather than success. Controls have bilingual accessible names and remain keyboard reachable.

每個時間值限制喺 0–20,000 毫秒；Windows API 拒絕會如實顯示錯誤。控制有雙語無障礙名稱，可用鍵盤操作。

## Verification · 驗證

Focused tests cover the upper bound, oversized rejection, preservation of unrelated API flags, and the exact live on-bit transition. Headless smoke does not intentionally change the operator's accessibility settings.

## Platform references · 平台參考

- Microsoft [`FILTERKEYS` structure](https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-filterkeys) documents the flag bits and timing fields.
- Microsoft [`SystemParametersInfo`](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-systemparametersinfow) documents `SPI_GETFILTERKEYS`, `SPI_SETFILTERKEYS`, profile persistence, and change notification.
