# Home Assistant validated restart · Home Assistant 驗證後重啟

Open `WinForge.exe --page homeassistant`, configure the endpoint/token, and use **Validate & restart HA** on the Config tab.

用 `WinForge.exe --page homeassistant` 開啟，設定 endpoint／權杖，再喺 Config 分頁用 **驗證再重啟 HA**。

Restart cannot be sent until `/api/config/core/check_config` returns parsed JSON with the exact `"result":"valid"` value. The in-memory proof is bound to the current endpoint/token fingerprint, expires after two minutes, is checked again after the decision dialog, and is consumed by one restart attempt. Invalid config, malformed/error responses, endpoint/token changes, or expiry fail closed.

重啟一定要等 `/api/config/core/check_config` 回傳解析後準確 `"result":"valid"` 先會送出。記憶體證明會綁定目前 endpoint／權杖 fingerprint、兩分鐘過期、確認對話框後再檢查，而且一次重啟嘗試就消耗；設定無效、格式／網絡錯誤、連線資料改變或者過期全部 fail closed。

![Home Assistant](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-homeassistant.png)

See the [validated-restart feature guide](../features/home-assistant/validated-restart.md) for privacy, failure, and offline-test details. · 私隱、失敗同離線測試詳情請睇[驗證後重啟指南](../features/home-assistant/validated-restart.md)。

[← Wiki Home](Home.md)
