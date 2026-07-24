# WinForge Roadmap · 路線圖

This roadmap covers the canonical .NET/WinUI 3 application. The experimental C++ port has its own roadmap in [WinForge-Native](https://github.com/codingmachineedge/WinForge-Native).

呢份路線圖只涵蓋正式 .NET／WinUI 3 app。實驗性 C++ 移植版嘅路線圖喺 [WinForge-Native](https://github.com/codingmachineedge/WinForge-Native)。

## Current priorities · 目前優先項目

- [ ] Keep the managed installer, portable build, updater contract, and release metadata aligned so the application always receives compatible `WinForge-Setup.exe` updates. · 保持受控 installer、portable build、updater 合約同 release metadata 一致，確保 app 永遠收到相容嘅 `WinForge-Setup.exe` 更新。
- [ ] Continue the exhaustive managed feature audit: registered routes, deep links, page loads, control surfaces, service paths, companions, launchers, accessibility, and clipping. · 繼續完整審查正式 app 嘅 route、deep link、頁面載入、控制介面、service、companion、launcher、無障礙同裁切。
- [ ] Keep all three persisted language modes complete and usable at narrow widths: English, Cantonese, and bilingual. · 保持英文、粵語同雙語三種持久語言模式完整，並喺窄畫面仍然易用。
- [ ] Finish the remaining rich-table and review-first UX work for device, package, archive, and other command-backed modules. · 完成裝置、套件、壓縮檔同其他 command-backed 模組餘下嘅豐富表格同先檢視後執行 UX。
- [ ] Expand safe import/export, configuration sync, diagnostics, and recovery while keeping secrets protected with DPAPI and destructive actions explicit. · 擴充安全 import／export、設定同步、診斷同復原，同時用 DPAPI 保護秘密，破壞性動作亦要明確確認。
- [ ] Continue AWS service-specific workspaces beyond native S3 and EC2: verified Cloud Control identifiers, live operations dashboards, and review-first controls for the next highest-value services. · 繼續將 AWS 專用工作區擴展到原生 S3 同 EC2 之外：加入已驗證 Cloud Control identifier、即時營運儀表板，同下一批高價值服務嘅先覆核控制。
- [ ] Preserve the reactor's 65-scenario gate and safety invariants while extending simulation, companion, and integration coverage. · 擴充模擬、companion 同整合覆蓋時，保持反應堆 65 個情境 gate 同安全 invariant。

## Completed structural work · 已完成結構工作

- [x] Make this repository and its release line unambiguously canonical for the .NET application. · 將呢個 repository 同 release 線明確定為正式 .NET app。
- [x] Move the C++20/C++/WinRT experiment to [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native), with its own source, tests, documentation, automation, and releases. · 將 C++20/C++/WinRT 實驗移植版搬去獨立 repository，連 source、tests、文件、自動化同 release 一齊分開。
- [x] Establish the reactor's stable backward-Euler kinetics, protection logic, thermal equilibrium, opt-in real-world effects, and 65/65 regression harness. · 完成反應堆穩定 backward-Euler kinetics、保護邏輯、熱平衡、可選現實效果，同 65/65 regression harness。
- [x] Ship reactor-powered ammonia production and strict-priority grid load shedding with live-bus gating, duplicate-tick-safe accounting, responsive bilingual controls, and focused regression scenarios. · 推出反應堆供電合成氨同嚴格優先級電網卸載，加入即時母線閘門、重複 tick 安全計數、響應式雙語控制，同專項回歸情境。
- [x] Establish the managed AWS Console shell, account/Region generation isolation, native S3 management, native EC2 inventory/lifecycle controls, and an optional CLI escape hatch. · 建立受管理 AWS Console shell、帳戶／Region generation 隔離、原生 S3 管理、原生 EC2 清單／生命週期控制，同選用 CLI 後備入口。
- [x] Ship the opt-in Command Palette extension-host protocol with current-state enablement checks, local-drive SHA-256 pinning, hash-to-launch file leasing, bounded cancellable JSON-lines I/O, accessible native structured pages, and a focused security harness. · 發佈明確選用嘅指令面板擴充主機協定，附即時啟用狀態核對、本機磁碟 SHA-256 釘選、由雜湊到啟動嘅檔案鎖、有界可取消 JSON-lines I/O、無障礙原生結構頁，同專項安全測試。
- [x] Integrate the native bilingual Dew Encryption workspace with compatible Git history, race-safe and load-tested debounced auto-history, rollback-safe deletion-aware restore, bounded imported-history handling, and secret-safe encrypted export. · 整合原生雙語 Dew Encryption 工作區，包括相容 Git 歷史、race-safe 並經負載測試嘅 debounced auto-history、可 rollback 並識得處理刪除嘅還原、有界匯入歷史處理，同 secret-safe 加密匯出。
- [x] Harden the native Core Audio mixer with checked COM activation, explicit system-default routing, narrow bilingual layout, and named keyboard/screen-reader controls. · 加固原生 Core Audio 混音器：檢查 COM 啟動結果、清楚還原系統預設路由、支援窄闊度雙語排版，同為鍵盤／螢幕閱讀器提供具名控制項。
- [x] Audit both preserved Package Manager recovery snapshots, retain their useful scheduler, coordinator, engine, bundle, source, schema, PowerToys-discoverability, and provenance intent in current native implementations, reject obsolete external-launch and credential-in-URL paths, make portable-bundle saves atomic and truthfully fail-aware, validate structured settings, and keep bilingual controls usable at narrow widths. · 審核兩份保留套件管理 recovery snapshot，喺現行原生實作保留有用排程器、coordinator、引擎、清單、來源、schema、PowerToys discoverability 同來源證明，拒絕過時外部啟動／URL 內嵌認證路徑，令可攜清單原子儲存兼如實回報失敗，驗證結構化設定，並保持雙語控制喺窄畫面可用。
- [x] Reconcile eight stale core backlog sections against reachable controls and real implementation paths: 74 of 115 capabilities are source-proven shipped, while 41 partial or absent workflows remain explicitly unchecked in the [categorized audit](docs/audits/roadmap-core-capability-audit-2026-07-24.md). · 將八個過時核心待辦章節同可達控制／真實實作路徑逐項核對：115 項有 74 項證實已交付，41 項部分或未有實作嘅流程就按[分類審核](docs/audits/roadmap-core-capability-audit-2026-07-24.md)明確保留未剔選。
