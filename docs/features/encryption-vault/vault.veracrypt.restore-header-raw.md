# Restore raw header snapshot · 還原原始檔頭快照

| Field · 欄位 | Value · 值 |
|---|---|
| **ID** | `vault.veracrypt.restore-header-raw` |
| **Module · 模組** | Encryption & Vault · 加密與保險庫 |
| **Type · 種類** | Action |
| **Administrator · 管理員** | No · 唔使 |
| **Destructive · 具破壞性** | Yes · 係 |
| **Restart · 重啟** | None |
| **Action · 動作** | Restore · 還原 |

## English
CLI fallback: write up to the first 131072 bytes from a .hdrbak snapshot back over a vault container header. Edit both paths first. This is destructive and is NOT the official restore-header format.

## 粵語
命令列備援：將 .hdrbak 快照最多首 131072 位元組寫返到保險庫容器檔頭。先改兩個路徑。呢個係破壞性操作，唔係官方還原檔頭格式。

---
_Keywords · 關鍵字: vault, restore, header, raw, 131072, 檔頭, 快照, 還原, 保險庫_

_Part of WinForge · WinForge 套件嘅一部分_
