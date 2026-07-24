# Core roadmap capability audit · 核心路線圖功能審核

## Result · 結果

The 2026-07-24 source audit reconciles 115 previously unchecked entries across Windows 11, ViveTool, Media, Maintenance, Dev & Terminal, Home Assistant, Archives, and Browser Control. A roadmap box is checked only when WinForge contains a reachable control, a concrete handler/service/registry/command implementation, and documentation or verification evidence. Fixed demonstrations and separate steps that do not complete the promised workflow remain gaps.

2026-07-24 原始碼審核逐項核對 Windows 11、ViveTool、Media、Maintenance、Dev & Terminal、Home Assistant、Archives 同 Browser Control 共 115 個原本未剔選項目。只有可達控制、實際 handler／service／registry／command 實作，同埋文件或驗證證據齊晒先會剔選；固定示例同未砌成完整流程嘅分散步驟仍然當缺口。

| Section · 章節 | Audited · 審核 | Shipped · 已交付 | Remaining · 餘下 |
|---|---:|---:|---:|
| Windows 11 | 13 | 10 | 3 |
| ViveTool | 15 | 15 | 0 |
| Media | 15 | 4 | 11 |
| Maintenance | 15 | 10 | 5 |
| Dev & Terminal | 15 | 9 | 6 |
| Home Assistant | 14 | 13 | 1 |
| Archives | 14 | 10 | 4 |
| Browser Control | 14 | 3 | 11 |
| **Total · 總數** | **115** | **74** | **41** |

## What the audit protects · 審核守住乜

- The exact checked count for every audited roadmap section. · 鎖實每個章節嘅剔選數量。
- Evidence placement: checked titles must be in the shipped ledger; unchecked titles must be in the factual gap ledger. · 已交付同缺口項目一定要放喺正確證據區。
- All 115 titles must remain represented; a count-preserving swap cannot silently pass. · 115 個標題全部要有記錄，唔可以偷偷交換狀態但保持總數蒙混過關。
- The focused guard runs with `tools/Test-RoadmapCoreAudit.ps1`. · 專項 gate 係 `tools/Test-RoadmapCoreAudit.ps1`。

## Detailed evidence · 詳細證據

Read the [categorized source evidence and gap ledger](../audits/roadmap-core-capability-audit-2026-07-24.md). It names the actual catalog IDs, page handlers, service methods, command/registry mechanisms, documentation evidence, and the precise missing behavior for every unchecked item.

請睇[分類原始碼證據同缺口清單](../audits/roadmap-core-capability-audit-2026-07-24.md)，入面逐項列出 catalog ID、page handler、service method、command／registry 機制、文件證據，同每個未剔選項目仲欠乜。

## Visual evidence · 視覺證據

This reconciliation changes documentation and a static verifier only. It does not change WinUI controls or layout, so no new application screenshot was required or claimed.

今次只改文件同靜態驗證器，冇改 WinUI 控制或版面，所以唔需要亦冇聲稱有新 app 截圖。
