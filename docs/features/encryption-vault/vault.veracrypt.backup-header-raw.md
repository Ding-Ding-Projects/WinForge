# Snapshot raw header (first 131072 bytes) · 快照原始檔頭（首 131072 位元組）

| Field · 欄位 | Value · 值 |
|---|---|
| **ID** | `vault.veracrypt.backup-header-raw` |
| **Module · 模組** | Encryption & Vault · 加密與保險庫 |
| **Type · 種類** | Action |
| **Administrator · 管理員** | No · 唔使 |
| **Destructive · 具破壞性** | No · 唔係 |
| **Restart · 重啟** | None |
| **Action · 動作** | Snapshot · 快照 |

## English
CLI fallback: copy the first 131072 bytes of a vault container to a .hdrbak file next to it. This captures the embedded primary header as a RAW copy — it is NOT the official backup-header format. Edit the volume path first.

## 粵語
命令列備援：將保險庫容器嘅首 131072 位元組複製去旁邊嘅 .hdrbak 檔。呢個係抓內嵌主檔頭嘅原始複本 — 唔係官方嘅備份檔頭格式。先改容器路徑。

---
_Keywords · 關鍵字: vault, header, raw, 131072, 檔頭, 快照, backup, 備份, 保險庫_

_Part of WinForge · WinForge 套件嘅一部分_
