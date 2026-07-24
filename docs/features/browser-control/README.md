# Browser Control · 瀏覽器控制

This category combines the parameterized Browser Control workbench with 100 catalog quick actions for Chrome, Edge, enterprise policies, profiles, and web tools. Open it from the navigation tree or with `WinForge.exe --page browser`.

呢個分類將參數化瀏覽器工作台，同 100 個 Chrome、Edge、企業政策、設定檔及網頁工具快捷操作放埋一齊。可以由導覽列開，亦可以用 `WinForge.exe --page browser`。

## Feature index · 功能索引

- [Browser launch workbench](browser-workbench.md) · 瀏覽器啟動工作台 — configurable URLs, real profile/PWA discovery, flags/policy pages, safe cache cleanup, proxy, throwaway sessions, feature switches, loopback remote debugging, and winget install/update.
- `br.chrome.*` · Chrome quick actions and internal pages.
- `br.edge.*` · Edge quick actions and internal pages.
- `br.policies.*` · ADMX-backed Chrome/Edge policies.
- `br.profiles.*` · Profile-folder, backup, cache, and version quick actions.
- `br.webtools.*` · Browser-adjacent Windows and network tools.

The individual generated catalog records in this folder document every `br.*` row. The workbench page below is authored because it coordinates multiple values and lifecycle rules rather than one catalog row.

呢個資料夾其餘自動生成記錄逐項解釋每個 `br.*` 行。工作台會協調多個輸入同生命週期規則，所以另外用一份人工維護文件講清楚。

## HTTP/API applicability · HTTP/API 適用性

No WinForge HTTP API is exposed by Browser Control, so a Postman collection is not applicable. Remote debugging starts the selected browser's own loopback-only CDP endpoint; WinForge does not proxy or persist that traffic.

瀏覽器控制冇提供 WinForge HTTP API，所以唔適用 Postman collection。遠端除錯只會啟動瀏覽器自己嘅 loopback CDP endpoint；WinForge 唔會代理或者保存流量。
