# Repair RAR (recovery record) · 修復 RAR（復原記錄）

| Field · 欄位 | Value · 值 |
|---|---|
| **ID** | `arc.rar-repair` |
| **Module · 模組** | Archives · 壓縮檔 |
| **Type · 種類** | Action |
| **Administrator · 管理員** | No · 唔使 |
| **Destructive · 具破壞性** | No · 唔係 |
| **Restart · 重啟** | None |
| **Action · 動作** | Repair · 修復 |

## English
Repair the selected .rar using its embedded recovery record / recovery volumes (.rev) via the RARLAB unrar CLI. 7-Zip cannot repair RAR, so this needs unrar.exe (WinRAR or bundled next to WinForge). Writes a fixed.<name>.rar beside the original.

## 粵語
用 RARLAB unrar CLI，靠 RAR 內嵌嘅復原記錄／復原卷（.rev）修復揀咗嗰個 .rar。7-Zip 修唔到 RAR，所以要 unrar.exe（WinRAR 或者放喺 WinForge 旁邊）。會喺原檔旁邊整一個 fixed.<名>.rar。

---
_Keywords · 關鍵字: rar, repair, recovery, record, 修復, 復原, unrar_

_Part of WinForge · WinForge 套件嘅一部分_
