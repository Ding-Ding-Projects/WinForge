# Developer · 開發者

Developer tools for coding, debugging, databases, cloud and automation — all driven from inside WinForge. · 喺 WinForge 入面一站式驅動嘅開發者工具，涵蓋編碼、除錯、資料庫、雲端同自動化。

## VS Code · VS Code 編輯器

Drive the VS Code CLI to open files, diffs and manage extensions. · 驅動 VS Code CLI 開檔、比對同管理擴充功能。

Open in-app: `WinForge.exe --page vscode`

![VS Code](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-vscode.png)

## Windows Terminal · Windows 終端機

Edit Windows Terminal profiles and run an embedded shell. · 編輯 Windows 終端機設定檔同執行內嵌殼層。

Open in-app: `WinForge.exe --page terminal`

![Windows Terminal](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-terminal.png)

## SSH Toolset · SSH 工具

SSH/SFTP/SCP profiles, key generation and passwordless deploy. · SSH／SFTP／SCP 設定檔、金鑰產生同免密碼部署。

Open in-app: `WinForge.exe --page ssh`

![SSH Toolset](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-ssh.png)

## quicktype · JSON 轉型別

Generate types and code from JSON for many languages. · 由 JSON 為多種語言產生型別同程式碼。

Open in-app: `WinForge.exe --page quicktype`

![quicktype](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-quicktype.png)

## API Client · REST API 用戶端

Postman-style REST client with collections and environments. · Postman 式 REST 用戶端，支援集合同環境變數。

Open in-app: `WinForge.exe --page api`

![API Client](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-api.png)

## Diff & Merge (WinMerge) · 比對與合併

Side-by-side file/folder diff and merge with patch export. · 並排比對同合併檔案／資料夾，可匯出修補檔。

Open in-app: `WinForge.exe --page diff`

![Diff & Merge (WinMerge)](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-diff.png)

## Diagram Editor · 圖表編輯器

draw.io-style flowchart and diagram editor with PNG/JSON export. · draw.io 式流程圖同圖表編輯器，可匯出 PNG／JSON。

Open in-app: `WinForge.exe --page diagram`

![Diagram Editor](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-diagram.png)

## .NET Decompiler · .NET 反編譯器

Browse and decompile .NET assemblies to C# (ILSpy-style). · 瀏覽同反編譯 .NET 組件成 C#（ILSpy 式）。

Open in-app: `WinForge.exe --page decompiler`

![.NET Decompiler](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-decompiler.png)

## Postgres Tool · Postgres 工具 / pgAdmin

Connect to and query PostgreSQL databases. · 連接同查詢 PostgreSQL 資料庫。

Open in-app: `WinForge.exe --page pgadmin`

![Postgres Tool](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-pgadmin.png)

## SQLite Browser · SQLite 資料庫瀏覽器

Browse, query and edit SQLite databases. · 瀏覽、查詢同編輯 SQLite 資料庫。

Open in-app: `WinForge.exe --page sqlite`

![SQLite Browser](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-sqlite.png)

## Packer (Image Builder) · Packer（映像建置器）

Build machine images from HCL templates with HashiCorp Packer. · 用 HashiCorp Packer 由 HCL 範本建置機器映像。

Open in-app: `WinForge.exe --page packer`

![Packer (Image Builder)](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-packer.png)

## AWS CLI · AWS 命令列

Drive the AWS CLI for S3, EC2, IAM, Lambda and more. · 驅動 AWS CLI 操作 S3、EC2、IAM、Lambda 等。

Open in-app: `WinForge.exe --page aws`

![AWS CLI](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-aws.png)

## Website Cloner · 網站複製器

Scrape, download assets and rebuild a website. · 抓取、下載資源同重建網站。

Open in-app: `WinForge.exe --page webcloner`

![Website Cloner](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-webcloner.png)

## Resume Writer · 履歷與求職信寫手

AI-assisted resume and cover-letter writer with export. · AI 輔助履歷同求職信寫手，可匯出。

Open in-app: `WinForge.exe --page resume`

![Resume Writer](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-resume.png)

## Exhaustive Smoke Verification · 全面冒煙驗證

**EN —** Contributors verifying WinForge broadly use the repository-local [WinForge Exhaustive Smoke skill](../../.agents/skills/winforge-exhaustive-smoke/SKILL.md). It inventories the actual module registry, navigation, deep links, XAML controls, companions, external launchers, tests and source files before any route is marked complete.

**粵語 —** 要全面驗證 WinForge 嘅貢獻者會用儲存庫入面嘅 [WinForge Exhaustive Smoke skill](../../.agents/skills/winforge-exhaustive-smoke/SKILL.md)。佢會先盤點真正嘅 module registry、導航、深層連結、XAML 控制項、companions、外部 launcher、測試同 source files，任何路線標示完成之前都要做到。

**EN —** Build, launch, screenshot, behavior and side-effect evidence remain separate. A blocked capture is reported as blocked, never as a visual pass; stateful features use fixtures, dry-runs or reversible probes unless live execution is explicitly authorized.

**粵語 —** 建置、啟動、截圖、行為同副作用證據會分開。截圖被阻擋就如實報告，唔會當作視覺通過；有狀態嘅功能會用 fixtures、dry-runs 或者可還原 probe，除非明確批准真實執行。

[← Wiki Home](Home.md)
