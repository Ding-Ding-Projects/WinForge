# Capability audits · 功能審核

This folder holds source-backed capability reconciliations. An item is marked shipped only when the repository contains a reachable user control, the handler or catalog binding that invokes a real implementation mechanism, and supporting documentation or verification evidence.

呢個資料夾收錄以原始碼為依據嘅功能核對。只有同時搵到可達嘅使用者控制、實際執行機制嘅 handler／catalog binding，同埋文件或驗證證據，先會標做已交付。

## Audits · 審核報告

- [Core roadmap capability audit — 2026-07-24](roadmap-core-capability-audit-2026-07-24.md) · 核心路線圖功能審核 — Windows 11, ViveTool, Media, Maintenance, Dev & Terminal, Home Assistant, Archives, and Browser Control; Browser Control is now 14/14, bringing the matrix to 85/115 shipped with 30 retained gaps.

## Verification · 驗證

Run `powershell -ExecutionPolicy Bypass -File tools/Test-RoadmapCoreAudit.ps1` from the repository root. The check locks the audited section totals, shipped counts, exact item coverage, Browser Control implementation markers, evidence-document links, and the 85/115 aggregate.

由儲存庫根目錄執行 `powershell -ExecutionPolicy Bypass -File tools/Test-RoadmapCoreAudit.ps1`。檢查會鎖實每節項目數、已交付數、每項審核覆蓋、瀏覽器實作標記、證據連結同 85/115 總數。
