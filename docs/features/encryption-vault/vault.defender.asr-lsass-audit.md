# ASR: block LSASS theft (Audit) · ASR：封鎖 LSASS 竊取（稽核）

| Field · 欄位 | Value · 值 |
|---|---|
| **ID** | `vault.defender.asr-lsass-audit` |
| **Module · 模組** | Encryption & Vault · 加密與保險庫 |
| **Type · 種類** | Action |
| **Administrator · 管理員** | Yes · 需要 |
| **Destructive · 具破壞性** | No · 唔係 |
| **Restart · 重啟** | None |
| **Action · 動作** | Audit · 稽核 |

## English
Set the LSASS credential-theft ASR rule to AuditMode so it only logs what it would block, without enforcing.

## 粵語
將 LSASS 憑證竊取嘅 ASR 規則設為「稽核模式」，淨係記錄會封鎖咩，唔真係執行封鎖。

---
_Keywords · 關鍵字: defender, asr, lsass, audit, 稽核, 模式_

_Part of WinForge · WinForge 套件嘅一部分_
