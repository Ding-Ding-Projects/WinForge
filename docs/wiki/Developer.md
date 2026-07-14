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

## AWS Manager · AWS 管理中心

Manage AWS through a Console-style shell with 149 curated services plus live CLI discovery, cross-service resource search, Cloud Control lifecycle APIs, a native S3 workspace, and an optional advanced CLI workbench. · 透過 Console 式介面管理 AWS：149 個精選服務加即時 CLI 探索、跨服務資源搜尋、Cloud Control 生命週期 API、原生 S3 工作區，同選用進階 CLI 工作台。

[Full AWS Manager guide · 完整 AWS 管理中心指南](AWS-Manager.md)

Open in-app: `WinForge.exe --page aws`

![AWS Manager](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-aws.png)

## Website Cloner · 網站複製器

Scrape, download assets and rebuild a website. · 抓取、下載資源同重建網站。

Open in-app: `WinForge.exe --page webcloner`

![Website Cloner](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-webcloner.png)

## Resume Writer · 履歷與求職信寫手

AI-assisted resume and cover-letter writer with export. · AI 輔助履歷同求職信寫手，可匯出。

Open in-app: `WinForge.exe --page resume`

![Resume Writer](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-resume.png)

## Regex Cheatsheet · 正則速查

Search the embedded bilingual .NET-regex reference by token, category, description, or example. The possessive-style zero-or-more entry uses the valid .NET atomic equivalent `(?>a*)`; .NET does not accept the `*+` syntax. · 可按 token、分類、說明或者例子搜尋內置嘅雙語 .NET 正則參考。佔有式零次或以上項目用有效嘅 .NET 原子等價寫法 `(?>a*)`；.NET 唔接受 `*+` 語法。

Open in-app: `WinForge.exe --page regexcheat`

Run the parser regression after changing the reference:

```powershell
dotnet run --project tests/RegexCheatService.Tests -c Debug
```

**Capture status · 截圖狀態：** The fresh 2026-07-11 `regexcheat` attempt is `capture-blocked`: `CopyFromScreen` was unavailable, the `PrintWindow` fallback was uniform, and graphics capture was unavailable. The route itself passed a subsequent launch-only check; no image is presented as fresh visual verification. · 2026-07-11 新嘅 `regexcheat` 嘗試係 `capture-blocked`：`CopyFromScreen` 唔可用、`PrintWindow` 後備畫面係 uniform，而 graphics capture 亦唔可用。之後 route launch-only check 通過；冇圖片會當成最新視覺驗證。

See [Regex Cheatsheet & Reactor Settings Lifecycle](RegexCheat-ReactorSettings-Lifecycle.md) for the complete, safe test boundary. · 完整、安全嘅測試邊界請睇[正則速查同反應堆設定生命週期](RegexCheat-ReactorSettings-Lifecycle.md)。

## Settings Store Integrity Regression · 設定儲存完整性回歸測試

**EN —** Run the focused storage regression after changing `SettingsStore`, import/export, or configuration backup behavior. It exercises valid load/write compatibility, atomic backup rotation, truncated/missing-primary recovery, fail-closed ordinary writes, and explicit-import repair.

**粵語 —** 改咗 `SettingsStore`、匯入／匯出或者設定備份行為之後，要行呢個專注嘅儲存回歸測試。佢會驗證有效讀寫相容性、原子式備份輪替、截斷／唔見咗主檔案嘅復原、平常寫入 fail closed，同埋明確匯入修復。

```powershell
dotnet run --project tests/SettingsStore.Tests -c Debug
```

**EN —** The harness uses disposable `%TEMP%\WinForge.SettingsStore.Tests\<guid>` fixtures only; it never reads or writes the real `%LOCALAPPDATA%\WinForge\settings.json`.

**粵語 —** harness 只會用可刪除嘅 `%TEMP%\WinForge.SettingsStore.Tests\<guid>` fixtures；佢絕對唔會讀寫真正嘅 `%LOCALAPPDATA%\WinForge\settings.json`。

**Visual evidence · 視覺證據：** This is storage-only code with no XAML/page-layout change, so screenshot capture/replacement is not applicable. · 呢個係純儲存程式碼，冇 XAML／頁面排版改動，所以唔適用截圖擷取／替換。

## Pumped-Hydro State Integrity Regression · 抽水蓄能狀態完整性回歸測試

**EN —** Run this platform-neutral harness after changing the Pumped-Storage Hydro service or page lifecycle. It verifies explicit-only state progression, charge/discharge efficiency, the `0.036 ⚡/MWh` delivered-energy conversion, and the source-level guarantee that load, render, and language refresh do not advance the simulation.

**粵語 —** 改咗抽水蓄能 service 或頁面生命週期之後，要跑呢個 platform-neutral 框架。佢會驗證只可以明確 tick 先推進狀態、充／放電效率、`0.036 ⚡/MWh` 已送出能量轉換，同埋 source-level 保證 load、render 同轉語言都唔會推進模擬。

```powershell
dotnet run --project tests/PumpedHydroService.Tests -c Debug
```

**Visual evidence · 視覺證據：** The repair is nonvisual service/code-behind state ownership; no XAML layout changed, so screenshot replacement is not applicable. See [Pumped-Hydro State Integrity](Pumped-Hydro-State-Integrity.md). · 呢次修正係非視覺嘅 service／code-behind 狀態所屬；冇 XAML 排版改變，所以唔適用截圖替換。詳情請睇[抽水蓄能狀態完整性](Pumped-Hydro-State-Integrity.md)。

## Exhaustive Smoke Verification · 全面冒煙驗證

**EN —** Contributors verifying WinForge broadly use the repository-local [WinForge Exhaustive Smoke skill](../../.agents/skills/winforge-exhaustive-smoke/SKILL.md). It inventories the actual module registry, navigation, deep links, XAML controls, companions, external launchers, tests and source files before any route is marked complete.

**粵語 —** 要全面驗證 WinForge 嘅貢獻者會用儲存庫入面嘅 [WinForge Exhaustive Smoke skill](../../.agents/skills/winforge-exhaustive-smoke/SKILL.md)。佢會先盤點真正嘅 module registry、導航、深層連結、XAML 控制項、companions、外部 launcher、測試同 source files，任何路線標示完成之前都要做到。

**EN —** Build, launch, screenshot, behavior and side-effect evidence remain separate. A blocked capture is reported as blocked, never as a visual pass; stateful features use fixtures, dry-runs or reversible probes unless live execution is explicitly authorized.

**粵語 —** 建置、啟動、截圖、行為同副作用證據會分開。截圖被阻擋就如實報告，唔會當作視覺通過；有狀態嘅功能會用 fixtures、dry-runs 或者可還原 probe，除非明確批准真實執行。

### GitHub Pages Payload Regeneration · GitHub Pages 資料重建

**EN —** GitHub Pages embeds the authored `docs/wiki` Markdown in
`design/winforge-data.js`. After any wiki or generated-reference change, run
the generator so the published site cannot serve stale documentation. The
script exports the live registry from a self-contained WinForge build, rejects
wiki paths outside `docs/wiki`, and serializes a plain data graph that works in
Windows PowerShell 5.1 as well as newer PowerShell versions.

**粵語 —** GitHub Pages 會將寫好嘅 `docs/wiki` Markdown 嵌入
`design/winforge-data.js`。任何 wiki 或生成 reference 有改之後，都要跑
generator，避免發佈咗嘅網站仲顯示舊文件。個 script 會由 self-contained
WinForge build 匯出 live registry、拒絕 `docs/wiki` 以外嘅 wiki path，並將資料
轉成 Windows PowerShell 5.1 同新版本 PowerShell 都用到嘅普通資料圖。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\regen-site-data.ps1
```

**Visual evidence · 視覺證據：** This is a documentation-payload build step;
no WinUI layout changes, so screenshot replacement is not applicable. · 呢個係
文件 payload 建置步驟，冇 WinUI 排版改動，所以唔適用截圖替換。

### Reactor Harness Exit Contract · 反應堆測試框架退出規約

**EN —** `tests/ReactorSim.Tests` is a focused `net8.0-windows` console harness for the real reactor and dependent services. Its normal run prints every scenario, returns **0 only when every scenario passes**, and returns **1** when any scenario fails or throws; it can therefore be used directly as a CI gate. Run `dotnet run --project tests/ReactorSim.Tests -c Debug -- --verify-exit-code-contract` for the fast deterministic check of that mapping without running the simulator scenarios. If this workspace has `DOTNET_ROOT` pointed at the local .NET 11 SDK, clear it or use the system .NET 8 runtime first.

**粵語 —** `tests/ReactorSim.Tests` 係針對真實反應堆同相依服務嘅 `net8.0-windows` console 測試框架。正常運行會列印每個情景，**全部情景 PASS** 先會回傳 **0**；任何情景失敗或者拋出例外就會回傳 **1**，所以可以直接做 CI gate。用 `dotnet run --project tests/ReactorSim.Tests -c Debug -- --verify-exit-code-contract` 可以唔跑模擬器情景、快速而確定噉驗證呢個 mapping。如果此 workspace 將 `DOTNET_ROOT` 指向本機 .NET 11 SDK，先清除佢或者使用系統 .NET 8 runtime。

### KeePass KDF Regression · KeePass KDF 回歸測試

**EN —** Run the focused headless KDBX KDF check after changing the native vault crypto. It verifies that the KDBX Argon2d and Argon2id UUIDs select different, matching derivations without opening a real vault or exposing credentials.

**粵語 —** 改咗原生密碼庫加密之後，要行呢個專注、headless 嘅 KDBX KDF 檢查。佢會驗證 KDBX Argon2d 同 Argon2id UUID 分別揀返正確又唔同嘅衍生方式，唔會開真 vault 或暴露認證資料。

```powershell
dotnet run --project tests/KeePassCrypto.Tests -c Debug
```

## Native C++ Rewrite Toolchain · 原生 C++ 重寫工具鏈

**EN —** The migration target is `WinForge.Native.sln`: a self-contained, unpackaged C++20/C++/WinRT WinUI 3 executable. Restore/build through the repository driver, then run the native core, catalog-parity, and process-owned UI Automation suites. A pending route is navigation scaffolding only and must never be marked as a ported feature.

**粵語 —** 遷移目標係 `WinForge.Native.sln`：自包含、免封裝 C++20／C++/WinRT WinUI 3 executable。用 repo driver 還原／建置，再跑原生核心、目錄對等同自有 process UI Automation suites。Pending 路線只係導航基礎，絕對唔可以標記做已移植功能。

**EN —** Keep the legacy managed SDK project isolated from the rewrite: `WinForge.csproj` excludes both the ignored NuGet restore tree (`packages/**`) and the native source/generated tree (`src/**`) from its recursive items. Without that boundary, the managed XAML compiler can ingest MSIX property-page XAML or native generated XAML. The regular managed compile check must still finish with zero errors.

**粵語 —** 舊受控 SDK project 要同重寫保持分隔：`WinForge.csproj` 會由遞迴項目排除已忽略嘅 NuGet restore tree（`packages/**`）同原生 source／generated tree（`src/**`）。冇呢條邊界，受控 XAML compiler 可能會錯食 MSIX property-page XAML 或原生 generated XAML。平常受控 compile check 仍然必須以零錯誤完成。

```powershell
powershell -ExecutionPolicy Bypass -File .agents\skills\run-winforge\driver.ps1 -Native -Publish -Page dashboard -NoCapture
dotnet build WinForge.sln -c Debug -p:Platform=x64
tests\native\WinForge.Core.Tests\bin\x64\Debug\WinForge.Core.Tests.exe
dotnet run --project tests\CheckDigitCore.Tests\CheckDigitCore.Tests.csproj -c Release
dotnet run --project tests\BinaryTextCore.Tests\BinaryTextCore.Tests.csproj -c Release
powershell -ExecutionPolicy Bypass -File eng\native\Test-NativeCatalogParity.ps1
powershell -ExecutionPolicy Bypass -File eng\native\Invoke-NativeShellSmoke.ps1
```

See [Native C++ Rewrite](Native-Cpp-Rewrite.md) for the 346-route + five-family contract, current Debug/Release 233/233 native test executable (191 core + 42 parser), linked managed-reference Check Digit 24/24 and Text to Binary 18/18 regressions, elevated 59/59 shell evidence, the two native utility slices, normal-integrity and screenshot blockers, safety invariants, and no-CLR cutover rule. · 346 路線 + 五組動態合約、目前 Debug／Release 233/233 原生測試 executable（191 core + 42 parser）、連結嘅受控參考檢查碼 24/24 同文字轉二進位 18/18 回歸、提權 59/59 shell 證據、兩個原生 utility 批次、正常 integrity 同截圖阻礙、安全 invariant 同 no-CLR 切換規則，請睇[原生 C++ 重寫](Native-Cpp-Rewrite.md)。

[← Wiki Home](Home.md)
