# Validated Home Assistant restart · 驗證後重啟 Home Assistant

## Behavior · 行為

The restart control calls `POST /api/config/core/check_config` before it can call `POST /api/services/homeassistant/restart`. Only a successful HTTP request whose parsed JSON object contains the exact string property `"result":"valid"` arms the gate. Error text that merely contains the word “valid” does not pass.

重啟控制一定要先呼叫 `POST /api/config/core/check_config`，先有機會呼叫 `POST /api/services/homeassistant/restart`。HTTP 成功之餘，解析後 JSON object 必須有準確字串屬性 `"result":"valid"` 先會開閘；錯誤文字就算包含 “valid” 都唔算。

The in-memory proof is bound to a SHA-256 fingerprint of the normalized endpoint and current token, expires after two minutes, is checked again after the confirmation dialog, and is consumed after one restart attempt. Changing endpoint/token, an invalid/failed check, clock rollback, expiry, or a prior restart blocks the call and requires a new check.

記憶體內證明會綁定正規化 endpoint 同目前權杖嘅 SHA-256 fingerprint，兩分鐘後過期；確認對話框之後會再驗一次，而且一次重啟嘗試就消耗。改 endpoint／權杖、檢查無效／失敗、時鐘倒退、過期或者之前已嘗試重啟，都會封鎖並要求重新檢查。

## Security, privacy, and failures · 安全、私隱同失敗

The raw token is never retained by the gate or written to logs/docs. A fingerprint is useful only for equality within the current process and is cleared on failure/consume. Network failures, malformed JSON, invalid config, and expiry fail closed; the result bar explains that restart was not sent. Restart remains a blocking decision because it interrupts the whole HA instance.

安全閘唔會保存原始權杖，亦唔會寫落 log／文件；fingerprint 只用嚟喺目前程序比較相等，失敗／消耗後會清除。網絡失敗、JSON 格式錯、設定無效同過期全部 fail closed，結果列會講清楚冇送出重啟。重啟會中斷成個 HA，所以保留做阻擋式決定。

## Verification · 驗證

`tests/RoadmapWorkflowCore.Tests` covers exact valid/invalid response parsing, endpoint/token changes, expiry, clearing, and one-use consumption without contacting a Home Assistant instance. Live verification must not submit a restart to a real server.

專項測試會離線驗證準確 valid／invalid 解析、endpoint／權杖變更、過期、清除同一次性消耗，唔會連去 Home Assistant。真實畫面驗證唔可以向伺服器送出重啟。
