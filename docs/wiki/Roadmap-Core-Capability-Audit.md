# Core roadmap capability audit · 核心路線圖功能審核

## Result · 結果

The 2026-07-24 source audit reconciles 115 previously unchecked entries across Windows 11, ViveTool, Media, Maintenance, Dev & Terminal, Home Assistant, Archives, and Browser Control. A roadmap box is checked only when WinForge contains a reachable control, a concrete handler/service/registry/command implementation, and documentation or verification evidence. Fixed demonstrations and separate steps that do not complete the promised workflow remain gaps.

2026-07-24 原始碼審核逐項核對 Windows 11、ViveTool、Media、Maintenance、Dev & Terminal、Home Assistant、Archives 同 Browser Control 共 115 個原本未剔選項目。只有可達控制、實際 handler／service／registry／command 實作，同埋文件或驗證證據齊晒先會剔選；固定示例同未砌成完整流程嘅分散步驟仍然當缺口。

| Section · 章節 | Audited · 審核 | Shipped · 已交付 | Remaining · 餘下 |
|---|---:|---:|---:|
| Windows 11 | 13 | 10 | 3 |
| ViveTool | 15 | 15 | 0 |
| Media | 15 | 15 | 0 |
| Maintenance | 15 | 10 | 5 |
| Dev & Terminal | 15 | 9 | 6 |
| Home Assistant | 14 | 13 | 1 |
| Archives | 14 | 10 | 4 |
| Browser Control | 14 | 3 | 11 |
| **Total · 總數** | **115** | **85** | **30** |

## What the audit protects · 審核守住乜

- The exact checked count for every audited roadmap section. · 鎖實每個章節嘅剔選數量。
- Evidence placement: checked titles must be in the shipped ledger; unchecked titles must be in the factual gap ledger. · 已交付同缺口項目一定要放喺正確證據區。
- All 115 titles must remain represented; a count-preserving swap cannot silently pass. · 115 個標題全部要有記錄，唔可以偷偷交換狀態但保持總數蒙混過關。
- The focused guard runs with `tools/Test-RoadmapCoreAudit.ps1`. · 專項 gate 係 `tools/Test-RoadmapCoreAudit.ps1`。
- Media is now 15/15: eleven new guided workflows are reachable through bilingual controls and protected by the 17-case `MediaWorkflowCore.Tests` harness. The animated WebP evidence remains exact: 15 fps, 480px scale, `libwebp`, `-loop 0`, and no explicit quality value. · Media 而家 15/15；11 個新引導式工作流程有雙語控制同 17 項專測保護，動態 WebP 證據仍然準確。

## Detailed evidence · 詳細證據

Read the [categorized source evidence and gap ledger](../audits/roadmap-core-capability-audit-2026-07-24.md). It names the actual catalog IDs, page handlers, service methods, command/registry mechanisms, documentation evidence, and the precise missing behavior for every unchecked item.

請睇[分類原始碼證據同缺口清單](../audits/roadmap-core-capability-audit-2026-07-24.md)，入面逐項列出 catalog ID、page handler、service method、command／registry 機制、文件證據，同每個未剔選項目仲欠乜。

## Visual evidence · 視覺證據

The Media controls and layout changed. A fresh process-owned live-tree capture was inspected and promoted to both canonical Media screenshot paths; LowLevel MCP headless tools were not callable in this session.

今次 Media 控制同版面有改，已檢查 repo driver 嘅 process-owned live-tree 截圖並更新兩個正式 Media 圖片位置；今次 session 冇可呼叫嘅 LowLevel MCP headless 工具。
