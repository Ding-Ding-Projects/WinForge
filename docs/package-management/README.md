# Package Management · 套件管理

This category documents WinForge's managed, in-app package workflows and their recovery/audit history. · 呢個分類記錄 WinForge 正式 app 內套件流程，同相關 recovery／審核歷史。

- [Package Manager](package-manager.md) — behavior, configuration, failure modes, security, accessibility, and verification. · 行為、設定、失敗模式、安全、無障礙同驗證。
- [Preserved-stash recovery audit (2026-07-24)](stash-recovery-2026-07-24.md) — exact ten-file disposition for preserved commit `5cc3aa712f9e326dd8d9ae0bdd4c16d8771e1cb6`. · 保留 commit 十個檔案嘅逐項處置記錄。

## HTTP/API and Postman applicability · HTTP/API 同 Postman 適用性

The Package Manager does not expose a WinForge HTTP API. It invokes locally installed package engines and selected official registry APIs from the desktop process, so a Postman collection is not applicable to this category. · 套件管理器冇提供 WinForge HTTP API；佢由桌面程序呼叫本機已安裝套件引擎，同指定官方 registry API，所以呢個分類唔適用 Postman collection。
