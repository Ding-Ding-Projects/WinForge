# Home Assistant validated restart · Home Assistant 驗證後重啟

`WinForge.exe --page homeassistant` now requires an exact successful `check_config` JSON result before restart. The in-memory proof is bound to the current endpoint/token fingerprint, expires in two minutes, is checked again after confirmation, and is consumed by one attempt.

`WinForge.exe --page homeassistant` 而家一定要有準確成功 `check_config` JSON 結果先重啟。記憶體證明綁定目前 endpoint／權杖 fingerprint、兩分鐘過期、確認後再驗，而且一次嘗試就消耗。

![Home Assistant](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-homeassistant.png)
