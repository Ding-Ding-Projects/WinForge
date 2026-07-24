# Core roadmap capability audit · 核心路線圖功能審核

## Result · 結果

The 2026-07-24 source audit reconciles 115 previously unchecked entries across Windows 11, ViveTool, Media, Maintenance, Dev & Terminal, Home Assistant, Archives, and Browser Control. A roadmap box is checked only when WinForge contains a reachable control, a concrete handler/service/registry/command implementation, and documentation or verification evidence. Fixed demonstrations and separate steps that do not complete the promised workflow remain gaps.

2026-07-24 原始碼審核逐項核對 Windows 11、ViveTool、Media、Maintenance、Dev & Terminal、Home Assistant、Archives 同 Browser Control 共 115 個原本未剔選項目。只有可達控制、實際 handler／service／registry／command 實作，同埋文件或驗證證據齊晒先會剔選；固定示例同未砌成完整流程嘅分散步驟仍然當缺口。

| Section · 章節 | Audited · 審核 | Shipped · 已交付 | Remaining · 餘下 |
|---|---:|---:|---:|
| Windows 11 | 13 | 13 | 0 |
| ViveTool | 15 | 15 | 0 |
| Media | 15 | 15 | 0 |
| Maintenance | 15 | 15 | 0 |
| Dev & Terminal | 15 | 15 | 0 |
| Home Assistant | 14 | 14 | 0 |
| Archives | 14 | 14 | 0 |
| Browser Control | 14 | 14 | 0 |
| **Total · 總数** | **115** | **115** | **0** |

## What the audit protects · 審核守住乜

- The exact checked count for every audited roadmap section. · 鎖實每個章節嘅剔選數量。
- Evidence placement: checked titles must be in the shipped ledger; unchecked titles must be in the factual gap ledger. · 已交付同缺口項目一定要放喺正確證據區。
- All 115 titles must remain represented; a count-preserving swap cannot silently pass. · 115 個標題全部要有記錄，唔可以偷偷交換狀態但保持總數蒙混過關。
- The focused guard runs with `tools/Test-RoadmapCoreAudit.ps1`. · 專項 gate 係 `tools/Test-RoadmapCoreAudit.ps1`。
- A follow-up pass rechecked all 43 Media, Archives, and Browser Control dispositions. Animated WebP evidence follows the exact catalog action: 15 fps, 480px scale, `libwebp`, `-loop 0`, and no explicit quality value. · 跟進覆核重新檢查 Media、Archives 同 Browser Control 全部 43 項；動態 WebP 證據準確跟 catalog：15 fps、480px、`libwebp`、`-loop 0`，冇明確 quality 參數。
- Browser Control subsequently closed all eleven gaps with its parameterized workbench and 23-case focused harness. · 瀏覽器控制之後用參數化工作台同 23 項專測補齊十一個缺口。
- Media is now 15/15: eleven new guided workflows are reachable through bilingual controls and protected by the 17-case `MediaWorkflowCore.Tests` harness. The animated WebP evidence remains exact: 15 fps, 480px scale, `libwebp`, `-loop 0`, and no explicit quality value. · Media 而家 15/15；11 個新引導式工作流程有雙語控制同 17 項專測保護，動態 WebP 證據仍然準確。
- Developer & Terminal is 15/15, Home Assistant is 14/14, and Archives is 14/14. The review-first controls and 44-case pure harness close eleven gaps without mutating the verification host. · 開發與終端機 15/15、Home Assistant 14/14、壓縮檔 14/14；先審閱控制同 44 項純測試補齊十一個缺口，驗證期間冇改主機狀態。
- All eight sections are now fully complete at 115/115 shipped with 0 remaining gaps. · 全部八個章節 115 項已全部交付，零剩低缺口。

## Detailed evidence · 詳細證據

Read the [categorized source evidence and gap ledger](../audits/roadmap-core-capability-audit-2026-07-24.md). It names the actual catalog IDs, page handlers, service methods, command/registry mechanisms, documentation evidence, and the precise missing behavior for every unchecked item.

請睇[分類原始碼證據同缺口清單](../audits/roadmap-core-capability-audit-2026-07-24.md)，入面逐項列出 catalog ID、page handler、service method、command／registry 機制、文件證據，同每個未剔選項目仲欠乜。

## Visual evidence · 視覺證據

The Windows/System + Maintenance follow-up changes the live System Doctors surface. Fresh headless evidence and its exact capture disposition are recorded in the System Doctors guide and task handoff; destructive operating-system actions were not executed for screenshot evidence.

Windows／System 加 Maintenance 跟進改咗即時「系統醫生」畫面；最新 headless 證據同準確擷取處置會記錄喺系統醫生指南同交接。截圖驗證冇執行破壞性作業系統操作。

Browser Control changes the live `CategoryPage` layout. Its fresh inspected route capture is documented in [Browser Control Workbench](Browser-Control-Workbench.md); no cache, package, or remote-debug side effect is used for the capture.

瀏覽器控制改咗即時 `CategoryPage` 版面；最新已檢視 route 截圖記錄喺[瀏覽器控制工作台](Browser-Control-Workbench.md)，擷取過程冇做快取、套件或者遠端除錯副作用。

The Media controls and layout changed. A fresh process-owned live-tree capture was inspected and promoted to both canonical Media screenshot paths; LowLevel MCP headless tools were not callable in this session.

今次 Media 控制同版面有改，已檢查 repo driver 嘅 process-owned live-tree 截圖並更新兩個正式 Media 圖片位置；今次 session 冇可呼叫嘅 LowLevel MCP headless 工具。

Developer & Terminal, Home Assistant, and Archives also have changed live surfaces. Their fresh LowLevel headless and canonical driver captures are recorded in the three focused workflow pages; no termination, exclusion, TCP, cache, archive, or Home Assistant mutation is exercised. · 三個頁面都有最新 headless 同正式 driver 畫面，擷取期間冇執行任何修改動作。
