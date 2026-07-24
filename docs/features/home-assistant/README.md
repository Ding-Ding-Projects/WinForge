# Home Assistant integration · Home Assistant 整合

This maintained category documents safety and lifecycle behavior that is not visible in the generated module/button inventory.

呢個維護分類記錄自動生成模組／按鈕清單睇唔到嘅安全同生命週期行為。

## Guides · 指南

- [Validated restart gate](validated-restart.md) · 驗證後重啟安全閘。

## HTTP/API disposition · HTTP／API 處置

WinForge consumes the user's own Home Assistant REST API but does not expose or proxy an HTTP API. A raw restart Postman request would bypass the WinForge validation gate, so a project Postman collection is intentionally not provided. · WinForge 只會使用使用者自己嘅 Home Assistant REST API，冇提供或者代理 HTTP API；原始 Postman 重啟 request 反而會繞過 WinForge 安全閘，所以刻意唔提供 project collection。
