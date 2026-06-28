# Blender (3D / Render) · Blender（3D／算圖）

WinForge drives the installed `blender.exe` for GUI launch, headless renders, batch queues, Python scripts, and Blender MCP server management. · WinForge 會驅動已安裝嘅 `blender.exe`，支援 GUI 開檔、無介面算圖、批次佇列、Python script，同 Blender MCP server 管理。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | `module.blender` |
| Deep-link alias · 深層連結別名 | `blender`, `render`, `3d` |
| Category · 分類 | Media & Capture · 媒體與擷取 |
| Page class · 頁面類別 | `WinForge.Pages.BlenderModule` |
| Page XAML · 頁面 XAML | `Pages/BlenderModule.xaml` |

## What It Covers · 功能範圍

Blender render jobs can be built in-app with input `.blend`, output folder, frame range, engine, format, samples and device options. The queue runs jobs sequentially with a live log and progress parser. · 可以喺 app 內砌 Blender 算圖工作，包括輸入 `.blend`、輸出資料夾、影格範圍、引擎、格式、取樣同裝置選項。佇列會順序執行，並顯示即時 log 同進度。

The Blender MCP manager can deploy/verify `uvx blender-mcp`, download the BlenderMCP add-on, save unlimited named MCP instances with separate host/port values, start or stop bridge processes, test the Blender socket, write Codex/Claude Code/OpenCode MCP config entries, export config bundles, and generate agent skills for multi-model workflows. · Blender MCP 管理器可以部署／驗證 `uvx blender-mcp`、下載 BlenderMCP add-on、儲存不限數量嘅命名 MCP 實例（每個有獨立 host/port）、啟動或停止 bridge process、測試 Blender socket、寫入 Codex／Claude Code／OpenCode MCP 設定、匯出設定包，並為多 model 工作流程產生 agent skills。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [OpenBlendBtn](../../buttons/media-capture/blender/001-openblendbtn.md) | `Button` | `OpenBlendBtn` | `OpenBlend_Click` |
| [PickInputBtn](../../buttons/media-capture/blender/002-pickinputbtn.md) | `Button` | `PickInputBtn` | `PickInput_Click` |
| [PickOutputBtn](../../buttons/media-capture/blender/003-pickoutputbtn.md) | `Button` | `PickOutputBtn` | `PickOutput_Click` |
| [RenderBtn](../../buttons/media-capture/blender/004-renderbtn.md) | `Button` | `RenderBtn` | `Render_Click` |
| [QueueBtn](../../buttons/media-capture/blender/005-queuebtn.md) | `Button` | `QueueBtn` | `Queue_Click` |
| [CancelBtn](../../buttons/media-capture/blender/006-cancelbtn.md) | `Button` | `CancelBtn` | `Cancel_Click` |
| [OpenOutBtn](../../buttons/media-capture/blender/007-openoutbtn.md) | `Button` | `OpenOutBtn` | `OpenOut_Click` |
| [RunQueueBtn](../../buttons/media-capture/blender/008-runqueuebtn.md) | `Button` | `RunQueueBtn` | `RunQueue_Click` |
| [ClearQueueBtn](../../buttons/media-capture/blender/009-clearqueuebtn.md) | `Button` | `ClearQueueBtn` | `ClearQueue_Click` |
| [RunScriptBtn](../../buttons/media-capture/blender/010-runscriptbtn.md) | `Button` | `RunScriptBtn` | `RunScript_Click` |
| [RunCustomScriptBtn](../../buttons/media-capture/blender/011-runcustomscriptbtn.md) | `Button` | `RunCustomScriptBtn` | `RunCustomScript_Click` |
| [ClearLogBtn](../../buttons/media-capture/blender/012-clearlogbtn.md) | `Button` | `ClearLogBtn` | `ClearLog_Click` |
| [McpCreateBtn](../../buttons/media-capture/blender/013-mcpcreatebtn.md) | `Button` | `McpCreateBtn` | `McpCreate_Click` |
| [McpDeployBtn](../../buttons/media-capture/blender/014-mcpdeploybtn.md) | `Button` | `McpDeployBtn` | `McpDeploy_Click` |
| [McpAddonBtn](../../buttons/media-capture/blender/015-mcpaddonbtn.md) | `Button` | `McpAddonBtn` | `McpAddon_Click` |
| [McpStartBtn](../../buttons/media-capture/blender/016-mcpstartbtn.md) | `Button` | `McpStartBtn` | `McpStart_Click` |
| [McpStopBtn](../../buttons/media-capture/blender/017-mcpstopbtn.md) | `Button` | `McpStopBtn` | `McpStop_Click` |
| [McpTestBtn](../../buttons/media-capture/blender/018-mcptestbtn.md) | `Button` | `McpTestBtn` | `McpTest_Click` |
| [McpConfigureBtn](../../buttons/media-capture/blender/019-mcpconfigurebtn.md) | `Button` | `McpConfigureBtn` | `McpConfigure_Click` |
| [McpBundleBtn](../../buttons/media-capture/blender/020-mcpbundlebtn.md) | `Button` | `McpBundleBtn` | `McpBundle_Click` |
| [McpSkillBtn](../../buttons/media-capture/blender/021-mcpskillbtn.md) | `Button` | `McpSkillBtn` | `McpSkill_Click` |
| [McpDeleteBtn](../../buttons/media-capture/blender/022-mcpdeletebtn.md) | `Button` | `McpDeleteBtn` | `McpDelete_Click` |

[← Media & Capture](../../Media-and-Capture.md)
