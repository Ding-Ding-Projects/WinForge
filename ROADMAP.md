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
- [ ] Preserve the reactor's 63-scenario gate and safety invariants while extending simulation, companion, and integration coverage. · 擴充模擬、companion 同整合覆蓋時，保持反應堆 63 個情境 gate 同安全 invariant。

## Completed structural work · 已完成結構工作

- [x] Make this repository and its release line unambiguously canonical for the .NET application. · 將呢個 repository 同 release 線明確定為正式 .NET app。
- [x] Move the C++20/C++/WinRT experiment to [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native), with its own source, tests, documentation, automation, and releases. · 將 C++20/C++/WinRT 實驗移植版搬去獨立 repository，連 source、tests、文件、自動化同 release 一齊分開。
- [x] Establish the reactor's stable backward-Euler kinetics, protection logic, thermal equilibrium, opt-in real-world effects, and 63/63 regression harness. · 完成反應堆穩定 backward-Euler kinetics、保護邏輯、熱平衡、可選現實效果，同 63/63 regression harness。
- [x] Establish the managed AWS Console shell, account/Region generation isolation, native S3 management, native EC2 inventory/lifecycle controls, and an optional CLI escape hatch. · 建立受管理 AWS Console shell、帳戶／Region generation 隔離、原生 S3 管理、原生 EC2 清單／生命週期控制，同選用 CLI 後備入口。
- [x] Ship the opt-in Command Palette extension-host protocol with current-state enablement checks, local-drive SHA-256 pinning, hash-to-launch file leasing, bounded cancellable JSON-lines I/O, accessible native structured pages, and a focused security harness. · 發佈明確選用嘅指令面板擴充主機協定，附即時啟用狀態核對、本機磁碟 SHA-256 釘選、由雜湊到啟動嘅檔案鎖、有界可取消 JSON-lines I/O、無障礙原生結構頁，同專項安全測試。
