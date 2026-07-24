# WinForge Full Development Handoff

## Current 2026-07-24 Dew Encryption current-main integration — local gates green · 2026-07-24 Dew Encryption current-main 整合 — 本機 gate 全綠

- **Scope and safety / 範圍同安全：** The native bilingual Dew workspace is integrated onto the current managed baseline with compatible adjacent Git history, history/details, manual and debounced snapshots, deletion-aware rollback-safe restore, and secret-safe password/header-encrypted 7z export. The current-main audit rejects reparse points in selected paths and existing ancestors; bounds imported commits, names, entries, depth, and helper output; propagates cancellation through traversal and tools; and preserves a live replacement in a safety snapshot before applying a historical deletion. Extracted read-only history accepts the same deletion as a no-op. · 原生雙語 Dew 工作區已整合到目前 managed baseline，提供相容旁置 Git 歷史、history／details、人手同 debounced snapshot、識得處理刪除並可 rollback 嘅還原，以及 secret-safe 密碼／檔名加密 7z 匯出。審核強化會拒絕選取路徑同現存祖先嘅 reparse point，限制匯入 commit／名稱／entry／深度／helper output，將取消傳入 traversal 同工具，並喺套用歷史刪除前先 safety-snapshot 目前替代內容；extracted read-only history 對同一刪除安全 no-op。
- **UI and language / UI 同語言：** The page adaptively stacks action, snapshot, password, history, watcher, and Vault surfaces at narrow widths; adds semantic section headings, accessible names, live status announcements, localized tooltips, and 40-pixel action targets; keeps **Open Vault** enabled whenever idle; and categorizes errors in English, Cantonese, and bilingual modes while retaining technical detail. Cancellation copy now states the real checkpoint boundary: Git/traversal can stop promptly, whereas an in-process 7-Zip native call must return first. · 頁面喺窄闊度會 adaptive 直排 action、snapshot、password、history、watcher 同 Vault，加入語義 section heading、無障礙名稱、live status、localized tooltip 同 40-pixel action target；idle 時 **Open Vault** 保持可用；三種語言模式會分類錯誤兼保留技術細節。取消文案如實講明 checkpoint：Git／traversal 可以盡快停，但程序內 7-Zip native call 要先返回。
- **Local verification / 本機驗證：** `DewEncryption.Tests` passed **23/23** with no skips, including writable and extracted historical-deletion restores, ancestor reparse rejection, rollback, dangerous Git config, watcher, SHA-256 history, pinned-upstream interoperability, and encrypted/unencrypted archive cases. `dotnet build WinForge.sln -c Debug -p:Platform=x64` completed with zero errors. The XAML literal-safety gate passed and now protects **17** managed ToggleSwitch defaults including Dew; the source audit resolved **2,888/2,888** referenced handlers across **335** XAML files with zero mismatches or tracked markers. Generated references report **320** feature pages and **1,917** button pages; regenerated Pages data reports **320** modules, **22** categories, **1,215** features, and **2,288** wiki pages. · Dew 測試 **23/23** 全過兼零 skip，包括兩種歷史刪除還原、祖先 reparse 拒絕、rollback、危險 Git config、watcher、SHA-256、上游 interop 同加密／非加密 archive；solution build 零 errors；XAML gate 通過並保護包括 Dew 在內 **17** 個 managed toggle default；source audit 喺 **335** 個 XAML file 解析晒 **2,888/2,888** handler，零 mismatch／marker。生成 reference 有 **320** 個 feature page 同 **1,917** 個 button page；Pages data 係 **320** modules／**22** categories／**1,215** features／**2,288** wiki pages。
- **Visual evidence disposition / 視覺證據處置：** The owned self-contained driver published and launched the correct `dew-encryption` route, but `CopyFromScreen` was unavailable and the `PrintWindow` fallback was blank or near-uniform. LowLevel MCP then created a dedicated headless desktop and confirmed a fresh live 1574×887 `WinUIDesktopWin32WindowClass` with a visible child bridge; its reported capture was inspected and found completely black, and showing the desktop was denied by the host. The owned process was stopped and the desktop handle closed. No invalid image is retained or claimed as proof; the fresh canonical screenshot is explicitly pending a graphics-capable session. · 自家 driver 成功 publish 同開啟正確 Dew route，但 `CopyFromScreen` 不可用而 `PrintWindow` 只得空白／近乎單色。LowLevel MCP 喺專用 headless desktop 確認新鮮 1574×887 live WinUI window 同 visible child bridge，但回傳 capture 經檢視係全黑，host 亦拒絕顯示 desktop；之後已停止自家 process 同關閉 desktop handle。冇保留或聲稱無效圖片係證據；正式新截圖明確等 graphics-capable session 補回。
- **Delivery state / 交付狀態：** The unique Dew feature was cherry-picked as `d15eaf6b8` and the audit hardening, regression coverage, generated references, and this handoff are carried on `codex/integrate-dew-current` for the parent integrator. No merge to `main`, branch deletion, hosted workflow, release, or external Wiki/Discussion update is claimed by this scoped integration task. · Dew 功能以 `d15eaf6b8` cherry-pick；審核強化、regression coverage、生成 reference 同呢份交接由 `codex/integrate-dew-current` 交俾 parent integrator。今次 scoped task 唔會聲稱已 merge `main`、刪 branch、跑 hosted workflow、發 release 或更新外部 Wiki／Discussion。

## Current 2026-07-20 native legacy-ref retirement — destination retention recorded · 2026-07-20 原生舊 ref 退役 — 已記錄目標保留

- **Scope / 範圍：** The legacy C++20/C++/WinRT checkout refs for ASCII Table, Base Converter, BMI, Date Calculator, Duration Calculator, Loan Calculator, Percentage Calculator, Unit Price, and the release-boundary work are retained in the standalone WinForge-Native closure history before their old managed-checkout worktrees are retired. The closure uses source-tree-neutral retention merges: it preserves provenance without putting rewrite files back into this managed tree. · ASCII Table、Base Converter、BMI、Date／Duration／Loan Calculator、Percentage Calculator、Unit Price 同 release-boundary 舊 C++20/C++/WinRT checkout ref，會先保留喺獨立 WinForge-Native 嘅 closure history，之後先退役舊 managed checkout worktree。closure 用唔改 source tree 嘅 retention merge，保留來源而唔會將 rewrite file 放返入呢個 managed tree。
- **Destination evidence / 目標證據：** ASCII, BMI, and Unit Price source/tests are byte-identical in native main; Base Converter and Percentage Calculator are retained with deliberate whitespace hardening; TextAnalysis is byte-identical and the standalone workflow supersedes the old shared release setup. Date, Duration, and Loan feature snapshots are also retained on pushed native WIP refs (99557bcf, 23f63ebf, and 559a23a8) and are not claimed as integrated native-main features. · ASCII、BMI、Unit Price source／test 喺 native main byte-identical；Base Converter 同 Percentage Calculator 保留咗刻意嘅 whitespace hardening；TextAnalysis byte-identical，而獨立 workflow 已取代舊 shared release。Date、Duration、Loan feature snapshot 亦喺已 push 嘅 native WIP ref（99557bcf、23f63ebf、559a23a8），唔會誤報成已整合 native-main feature。
- **Boundary and disposition / 界線同處置：** Managed main continues to carry only the .NET 11/WinUI 3 app and its AudioForge/ImageForge companion C++ programs, not the rewrite. No managed UI or screenshot changes are part of this cleanup. Unrelated dirty PowerToys and Reactor/Dew work remain preserved and are outside this native-ref retirement. · managed main 繼續只帶 .NET 11／WinUI 3 app 同 AudioForge／ImageForge companion C++ program，唔包括 rewrite。今次清理冇 managed UI／截圖改動；唔相關嘅 dirty PowerToys 同 Reactor／Dew 工作會保留，唔屬於今次 native-ref 退役。

## Current 2026-07-20 AWS Manager native EC2 continuation — local gates green · 2026-07-20 AWS Manager 原生 EC2 延續開發 — 本機 gate 全綠

- **Scope / 範圍：** The canonical .NET 11 / WinUI 3 AWS Manager now includes a native EC2 workspace beside its existing S3, Resource Explorer, credential/profile, CLI, CloudFormation, Cost Explorer, and Cloud Control surfaces. `aws`, `awscli`, `s3`, and the new `ec2` route open the intended workspace. EC2 provides local filtering, paged instance discovery, bilingual details, and explicit Start, Stop, Reboot, and Terminate review flows backed by AWSSDK.EC2; it does not shell out to the AWS CLI. · 正式 .NET 11／WinUI 3 AWS Manager 而家喺既有 S3、Resource Explorer、profile、CLI、CloudFormation、Cost Explorer 同 Cloud Control 旁邊加入原生 EC2 工作區；`aws`／`awscli`／`s3`／`ec2` route 會直達正確畫面。EC2 有本機篩選、分頁 instance 清單、雙語詳情，同埋要明確確認先會執行嘅 Start／Stop／Reboot／Terminate，直接使用 AWSSDK.EC2，唔會 shell out 去 AWS CLI。
- **Safety and lifecycle / 安全同生命週期：** Profile/region context generations, operation ownership, stale-result rejection, selection/context revalidation after dialogs, and fail-closed state/action policy prevent old async work from mutating a new account context. S3 upload overwrite is atomic through `If-None-Match: *`; its shield and operation lease prevent overlapping actions from cancelling or exposing partial setup. Credential discovery returns profile metadata only and never retains secret values. Destructive EC2 and Cloud Control mutations require a fresh review bound to the exact context and selection. · profile／region generation、operation ownership、過期結果拒絕、dialog 後重新核對 selection／context，同 fail-closed state policy，會阻止舊 async 工作改到新 account context。S3 覆寫用 `If-None-Match: *` 原子保護，operation shield／lease 亦避免重疊操作；credential discovery 只回傳 profile metadata，唔保留 secret。EC2 同 Cloud Control 破壞性改動都要用綁定當前 context／selection 嘅新 confirmation。
- **Local verification / 本機驗證：** `dotnet build WinForge.sln -c Debug -p:Platform=x64` completed with **0 errors** (318 existing warnings). All **27/27** managed test projects passed in Release, including AWS Manager **11/11** and Reactor **63/63**. The XAML literal-safety gate passed. The full wiki generator produced **319** feature documents and **1,902** button documents, including **45** AWS button references; Pages data reports 319 modules, 22 categories, 1,214 features, and 2,272 wiki pages. · solution build 零 errors；Release test project **27/27** 全過，包括 AWS **11/11** 同 Reactor **63/63**；XAML safety 通過。完整 wiki generator 產生 319 個 feature 文件同 1,902 個 button 文件（AWS 45 個），Pages data 係 319 modules／22 categories／1,214 features／2,272 wiki pages。
- **Visual evidence / 視覺證據：** LowLevel MCP exists on disk but no headless tool is callable in this session. The managed self-contained driver published and launched the owned `ec2` route; desktop `CopyFromScreen` was unavailable and its `PrintWindow` fallback was blank, so an app-owned debug `RenderTargetBitmap` capture was used with empty temporary AWS config/credentials and the credential-safe capture flag. The inspected `2077×1302` direct-EC2 image is promoted to `docs/screenshot-aws.png` and its wiki mirror; both have SHA-256 `E49861D99B9FBA1942A09AAEA1E3BD88006F122CB0CA770BE0FDA019CF84BA49`. No live AWS account query or mutation was performed. · 今次 session 冇可呼叫嘅 LowLevel headless tool；managed self-contained driver 成功 publish 同開啟自家 `ec2` route。因為桌面 capture 不可用、`PrintWindow` 係空白，所以用 app-owned debug `RenderTargetBitmap`，配合空白暫存 AWS config／credentials 同安全 capture flag。已檢視嘅 `2077×1302` EC2 圖已升格到正式同 wiki mirror，SHA-256 相同；全程冇查詢或改動真實 AWS account。
- **Delivery and remote proof / 交付同遙距證明：** Feature commit `ea7238d7ca3f14abffeb6da81d43b20b8416cd55` was pushed on `codex/aws-ec2-manager`, merged to `main` as `84220d29a3c6057ead25957e4c99f1ad01f1ab77`, and remains an ancestor of the remotely proved site-data tip `17fb451c58c4a1aa61b99494172a008b21d68030`; remote-tree checks found the EC2 policy/mapper, test project, AWS guide, and screenshot on `origin/main`. Branch run [29721000525](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29721000525), merged-main run [29721014631](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29721014631), site-data run [29721014577](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29721014577), exact-tip run [29721270091](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29721270091), and Pages runs [29721014619](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29721014619) / [29721275818](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29721275818) all passed. [`v1.1.264`](https://github.com/Ding-Ding-Projects/WinForge/releases/tag/v1.1.264) is non-draft, non-prerelease GitHub Latest at exact `17fb451c`, with exactly `WinForge-Setup.exe` and `WinForge-portable-x64-1.1.264.zip`; the feature and merge releases `v1.1.262` / `v1.1.263` are honestly retained as prereleases. Managed Wiki commit `ab16d14eea6890198d51daa08d9f25b8fef6cb27` publishes the updated guide and byte-identical EC2 screenshot without deleting unrelated Wiki-only content. The proved task branch was removed locally and remotely. This completion record is a docs-only follow-up and remains subject to the repository's same tests-first release contract. · 功能 commit `ea7238d7` 已 push、以 `84220d29` merge 入 `main`，並係已做 remote proof 嘅 site-data tip `17fb451c` 祖先；remote tree 有齊 EC2 policy／mapper、test、AWS 指南同截圖。branch、merge、site-data、exact-tip、Pages run 全綠；`v1.1.264` 係準確指向 `17fb451c` 嘅 stable GitHub Latest，只得 setup 同 portable ZIP，而 `v1.1.262`／`v1.1.263` 如實保留為 prerelease。managed Wiki `ab16d14` 已發佈更新指南同 byte-identical EC2 圖，冇刪除其他 Wiki-only 內容；經證明嘅 task branch 已喺本機同 remote 移除。呢個 completion record 只改文件，仍然受同一先測試後發佈合約約束。
- **Repository hygiene and preservation / Repository 衛生同保留：** The final branch/worktree/stash audit found no AWS task work outside `main`. It intentionally preserves unrelated unique-history Dew, ASCII Table, Base Converter, Health Calculators, Percentage Calculator, Unit Price, release-boundary, and remote Claude branches; uncommitted Date/Duration/Loan and PowerToys worktrees; and two nonredundant package-management stashes. None is merged, deleted, or rewritten by this AWS task. · 最終 branch／worktree／stash audit 冇發現 AWS task 工作散落喺 `main` 之外。獨特歷史嘅 Dew、ASCII Table、Base Converter、Health Calculators、Percentage Calculator、Unit Price、release-boundary、remote Claude branch，未提交嘅 Date／Duration／Loan／PowerToys worktree，同兩個非重複 package-management stash 全部保留；今次 AWS task 冇合併、刪除或者改寫佢哋。

## Current 2026-07-20 repository split completion — remote proof green · 2026-07-20 repository 分拆完成 — 遙距證明全綠

- **Repository boundary / Repository 界線：** The split feature commit `fe791aa6167dbe26dc358df3a31acce51bd0f931` was merged as `165477c4461c6bd33e30d3856ec076f638193e10`; the expected site-data refresh advanced the remotely proved integration tip to `be054aa737df860b1185bd7b1102d8dd9e80ae8e` before this completion record. Remote-tree proof confirms [Ding-Ding-Projects/WinForge](https://github.com/Ding-Ding-Projects/WinForge) contains the managed solution, managed release workflow and installer, but none of `WinForge.Native.sln`, `src/WinForge.App`, `src/WinForge.Core`, `tests/native`, the parity ledger, or the native installer/workflow. Standalone native `main` is `a64e8e30ed8b5fe376197448ba760d1374244c69` and proves the inverse boundary. · 分拆 commit 已經 merge；呢段完成記錄之前，site-data 將已做 remote proof 嘅 integration tip 更新到 `be054aa7`。遙距 tree 證明正式 repo 只保留 managed solution／release／installer，原生 rewrite source、tests、ledger、installer 同 workflow 已搬走，而獨立原生 `main` `a64e8e30` 就準確保留相反界線。
- **Hosted managed proof / Managed hosted 證明：** branch run [29715061742](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715061742) passed and published exact-SHA prerelease `v1.1.257`; merged-main run [29715516125](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715516125) passed and honestly kept superseded `165477c4` as prerelease `v1.1.258`; site-data run [29715516151](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715516151) and integration-tip run [29715701032](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715701032) passed. [Managed `v1.1.259`](https://github.com/Ding-Ding-Projects/WinForge/releases/tag/v1.1.259) was non-draft, non-prerelease, GitHub Latest at that proof point, and targets exact `be054aa7`; it contains exactly `WinForge-Setup.exe` and `WinForge-portable-x64-1.1.259.zip`. Pages run [29715705513](https://github.com/Ding-Ding-Projects/WinForge/actions/runs/29715705513) is green and publishes 319 modules, 22 categories, 1,214 features, and no rewrite metadata. Every later docs-only `main` commit, including this completion record, remains subject to the same tests-first release contract. · branch、merge、site-data、integration-tip 同 Pages hosted run 全部通過；`v1.1.259` 喺該證明點係準確指向 `be054aa7` 嘅 stable Latest，只得 managed installer 同 portable ZIP，Pages data 亦冇原生 rewrite metadata。之後包括呢段記錄在內嘅 docs-only `main` commit，仍然要通過同一套先測試後發佈合約。
- **Hosted native proof / 原生 hosted 證明：** [native run 29715120945](https://github.com/codingmachineedge/WinForge-Native/actions/runs/29715120945) and [Pages run 29715120958](https://github.com/codingmachineedge/WinForge-Native/actions/runs/29715120958) passed at exact `a64e8e30`; [native-v1.1.7](https://github.com/codingmachineedge/WinForge-Native/releases/tag/native-v1.1.7) is the native stable Latest with exactly `WinForge-Native-Setup.exe` and `WinForge-native-x64-1.1.7.zip`. Preserved WIP branches remain remotely exact at Date `99557bcf`, Duration `23f63ebf`, and Loan `559a23a8`. · 原生 CI／Pages 同 stable Latest 全部準確指向 `a64e8e30`，只得兩個原生 asset；三條未完成計算器 WIP branch 亦已獨立 push 同保留。
- **Documentation and visual disposition / 文件同視覺處置：** managed Wiki commit `be2571545ee81b9286f36a8a96aa72fdc92769b2` is pushed and live; both [managed Pages](https://ding-ding-projects.github.io/WinForge/) and [native Pages](https://codingmachineedge.github.io/WinForge-Native/) return HTTP 200. GitHub has not initialized `WinForge-Native.wiki.git`: the Wiki URL redirects to the repository, the Git endpoint returns repository-not-found, and no authenticated browser or supported Wiki API was available, so tracked native docs plus Pages are the published native documentation. No managed UI changed; the process-owned Dashboard launch-only check passed and no managed screenshot was replaced. · managed Wiki 同兩個 Pages site 已上線；新原生 Wiki 因 GitHub 未初始化、冇已登入 browser／支援 API 而未能建立第一頁，所以以 tracked docs 同 Pages 發佈。今次冇改 managed UI，Dashboard launch-only 通過，亦毋須換截圖。
- **Cleanup and preservation / 清理同保留：** ancestry-proven split/proof/bootstrap branches, five clean merged worktrees, nine merged original remote branches, stale metadata, and one byte-identical redundant stash were removed only after remote proof; the temporary native remote was removed from the managed checkout. Dirty Date/Duration/Loan and PowerToys worktrees, unique Dew work, exact-tip-divergent historical native/release branches, and the two nonredundant stashes remain untouched. Post-cleanup audit found both default checkouts clean and synchronized. · 遙距證明後先清除已合併 branch／worktree／remote branch／重複 stash 同暫時 remote；有未提交或獨特歷史嘅工作樹、branch 同兩個有效 stash 全部保留，兩個 default checkout 最後都乾淨並同 remote 同步。

## Current 2026-07-19 repository split — managed app restored as canonical · 2026-07-19 repository 分拆 — 正式 app 回復為 managed 版

- **Scope / 範圍：** The experimental C++20/C++/WinRT rewrite moved to [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native). This repository again owns only the canonical .NET 11 / WinUI 3 app and its managed installer, updater, documentation, Pages data, and releases. `native/` stays here because it contains small companion applications used by the managed app, not the rewrite. · 實驗性 C++20／C++/WinRT 重寫版已搬去獨立 WinForge-Native；呢個 repository 再次只負責正式 .NET 11／WinUI 3 app、managed installer／updater、文件、Pages data 同 release。`native/` 入面係 managed app 使用嘅細型 companion，唔係重寫版，所以保留。
- **Native remote state / 原生 remote 狀態：** standalone `main` baseline `842f8dacbdde96a54fc015cf2bdbd7e92813dc8f` passed hosted native CI/installer run `29713539685` and Pages run `29713539675`. Preserved WIP tips also passed hosted CI and exact-SHA prereleases: Date `99557bcf481c963c7a4700b32fe6c0f5d7811b6d`, Duration `23f63ebfd69e07fe0f7d6b4ed51e4af40996848a`, and Loan `559a23a81840130c01316393ae45986a855e6c87`. · 獨立原生 `main` 同三條保留 WIP branch 嘅 hosted CI、installer、Pages／prerelease 證據已通過。
- **Managed local validation / 正式版本機驗證：** `dotnet build WinForge.sln -c Debug -p:Platform=x64` completed with zero errors; all **26/26** managed test projects passed in Release, including Reactor **63/63**; the XAML literal-safety gate passed; the self-contained driver published and completed an owned Dashboard launch-only check; workflow YAML and changed PowerShell parsed successfully. · managed solution 零 errors、Release test project **26/26**（包括 Reactor **63/63**）、XAML safety、self-contained Dashboard owned launch-only、workflow YAML 同 PowerShell parse 全部通過。
- **Visual evidence / 視覺證據：** The repository split changes no managed UI surface, so no managed canonical screenshot was replaced. Native visual evidence and the accepted Percentage Calculator image moved with WinForge-Native; its About page received launch-only evidence during standalone validation. · 分拆冇改 managed UI，所以冇替換 managed canonical 截圖；原生 visual 證據同已接受嘅 Percentage Calculator 圖已跟 WinForge-Native 搬走，獨立驗證亦完成 About launch-only check。
- **Git state / Git 狀態：** Native source, tests, parity tooling/ledger, dedicated installer, workflow, and feature evidence are removed from this tree only after being pushed to the new repository. Final managed branch/main ancestry, hosted managed release, GitHub Wiki sync, and task-worktree cleanup are recorded in the follow-up completion entry after remote proof. · 原生 source、tests、parity tooling／ledger、專用 installer、workflow 同功能證據已先 push 去新 repository，之後先由呢個 tree 移除；managed branch／main ancestor、hosted release、GitHub Wiki 同 worktree cleanup 會喺 remote proof 後下一段 completion entry 記錄。

## Current 2026-07-20 native Percentage Calculator controlled integration — local gates green · 2026-07-20 原生百分比計算器受控整合 — 本機 gate 已通過

- **Scope / 範圍：** `percent`, `percentage`, and `module.percentcalc` are integrated as dependency-free standard-C++ `PercentCalc` plus a genuine C++/WinRT renderer. Six local cards preserve percent-of, reverse percent, signed change, increase/decrease, tip splitting, ratio simplification, current-culture/invariant parsing, managed Unicode trimming, six-place away-from-zero display rounding, banker’s people rounding, localization retention, fresh-route reset, accessibility, and explicit-only clipboard Copy; no CLR host or managed delegation is included. The sole native workflow is also hardened for test-gated, idempotent C++-only release retries.
- **Current evidence / 目前證據：** Debug and Release x64 native builds have 0 errors; both cores pass **915/915**, including Percentage Calculator **37/37**; focused `-PercentCalcRoutesOnly -AllowClipboardMutation` UIA passes **14/14**; catalog parity is **346 fixed routes + five dynamic families**, 319 registry records, and 346 ledger rows; installer contract passes; renderer accounting is **38/346** (`38 in-progress / 308 not-started`).
- **Visual and delivery / 視覺同發佈：** the repository-local LowLevel checkout has no callable headless tool, but the required native driver obtained a valid 1962×1311 PrintWindow fallback after CopyFromScreen was unavailable. It was visually inspected and promoted as `docs/screenshot-percent.png` and its wiki-local copy; visual evidence is `pass`. Publishing remains C++-only; earlier hosted GitHub API-outage failures are pending remote repair after this controlled push.

**粵語摘要：** 三個百分比 alias 已受控整合成標準 C++ core 同真正 C++/WinRT renderer，六張卡保留 managed 百分比、反求、變化、加減、貼士同化簡比例，以及相容 Unicode 修剪、取捨、語言保留、新 route 重設、無障礙同只限明確 Copy。Debug／Release 0 errors、core 各 **915/915**（Percentage Calculator **37/37**）、UIA **14/14**、catalog parity 346+5／319 registry／346 ledger 同 installer contract 通過；renderer **38/346**。LowLevel MCP 未可呼叫，但 driver 有效 PrintWindow 截圖已檢視並升格，所以 visual 係 `pass`；唯一 C++ publisher 已有測試 gate／idempotent retry，controlled push 後仲要 repair 較早 hosted API outage。

## Current 2026-07-20 native ASCII Table controlled integration — local gates green · 2026-07-20 原生 ASCII 表受控整合 — 本機 gate 已通過

- **Scope / 範圍：** `ascii`, `asciitable`, and `module.asciitable` are integrated as a dependency-free standard-C++ `AsciiTable` core plus genuine C++/WinRT renderer. It preserves 0–127 by default, explicit Latin-1 through 255, control/space/DEL/C1/NBSP distinctions, radix columns, invariant local search, virtualization, fresh-route reset, language-state retention, accessibility, and explicit-only raw-character Copy; no CLR host, managed delegation, workflow, or release-policy change is included.
- **Current evidence / 目前證據：** Debug and Release x64 native solution builds have 0 errors; both combined core suites pass **878/878**, including ASCII Table **21/21**; focused `-AsciiTableRoutesOnly -AllowClipboardMutation` UIA passes **16/16** across all aliases and language modes; catalog parity is **346 fixed routes + five dynamic families**, 319 registry records, and 346 ledger rows; and the native installer contract passes. Renderer accounting is **37/346 fixed routes**, **37 `in-progress` / 309 `not-started`**.
- **Visual and shell disposition / 視覺同 shell 狀態：** the repository-local LowLevel checkout exists but no headless MCP tool is callable in this session. The required native driver rejected a blank/near-uniform fallback after `CopyFromScreen` was unavailable; no PNG was retained and no root WinForge process remained, so visual evidence remains `capture-blocked`. The broader shell invocation was stopped after the observed pre-existing `wordfreq` launch stalled and is explicitly not a full-shell pass. Publishing remains C++-only.

**粵語摘要：** 三個 ASCII alias 已受控整合成唔靠依賴嘅標準 C++ core 同真正 C++/WinRT renderer，保留 0–127／明確 Latin-1 255、控制碼／空格／DEL／C1／NBSP、進制欄、invariant 搜尋、虛擬化、新 route 重設、語言保留、無障礙同只限明確 raw-character Copy。Debug／Release 0 errors、core 各 **878/878**（ASCII **21/21**）、專項 UIA **16/16**、catalog parity 346+5／319 registry／346 ledger 同 installer contract 已通過；renderer **37/346**（**37 `in-progress` / 309 `not-started`**）。LowLevel MCP 未可呼叫，driver 拒絕空白 fallback，冇 PNG／冇殘留 process，所以 visual 係 `capture-blocked`。較廣 shell 喺觀察到既有 `wordfreq` launch 卡住後已停止，唔當 full-shell pass；發佈繼續只限 C++。

## Current 2026-07-19 native Health Calculators controlled integration — local gates green · 2026-07-19 原生健康計算器受控整合 — 本機 gate 已通過

- **Scope / 範圍：** `bmi`, `health`, and `module.bmi` are now integrated as a pure standard-C++ `Bmi` core plus C++/WinRT Health Calculators renderer. It preserves WHO BMI bands, Mifflin–St Jeor BMR, five TDEE factors, US Navy male/female body-fat rules, raw metric/imperial relabelling, invalid recovery, all three language modes, lifecycle reset, and no clipboard-write path; no CLR host, managed delegation, workflow, or release-policy change is included.
- **Current evidence / 目前證據：** Debug and Release x64 native solution builds have 0 errors; both combined core suites pass **842/842**, including BMI **14/14**; focused `-BmiRoutesOnly` UIA passes **14/14** across all aliases; catalog parity is **346 fixed routes + five dynamic families**, 319 registry records, and 346 ledger rows; the native installer contract passes. Renderer accounting is **35/346 fixed routes**, **35 `in-progress` / 311 `not-started`**.
- **Visual and release state / 視覺同發佈狀態：** the local LowLevel checkout exists but no headless MCP tool is callable in this session. The required native driver rejected its blank/near-uniform fallback after `CopyFromScreen` was unavailable; no PNG was retained or promoted, so visual evidence remains `capture-blocked`. Publishing remains C++-only.

**粵語摘要：** 三個健康計算器 alias 而家已同原生 shell 受控整合，係純標準 C++ core 加 C++/WinRT renderer，保留 WHO BMI 分級、Mifflin–St Jeor BMR、五個 TDEE 系數、美國海軍男女體脂規則、原始公制／英制重標籤、無效輸入復原、三種語言、route reset 同冇寫剪貼簿路徑。Debug／Release 0 errors、core 各 **842/842**（BMI **14/14**）、專項 UIA **14/14**、catalog parity 346+5／319 registry／346 ledger 同 installer contract 都通過；renderer **35/346**（**35 `in-progress` / 311 `not-started`**）。LowLevel MCP 不可呼叫，driver 拒絕空白 fallback，冇 PNG，所以 visual 保持 `capture-blocked`。發佈繼續只限 C++。

## Current 2026-07-19 native Unit Price controlled integration — local gates green · 2026-07-19 原生單位價格受控整合 — 本機 gate 已通過

- **Scope / 範圍：** `priceper`, `unitprice`, and `module.unitprice` are now integrated as a pure standard-C++ `UnitPrice` core plus C++/WinRT renderer. It preserves managed valid-row filtering, free/infinity/tolerance ties, invariant output, Add/remove/release/reset lifecycle, all three language modes, and explicit-only Copy; no CLR host, managed delegation, workflow, or release-policy change is included.
- **Current evidence / 目前證據：** Debug and Release x64 native solution builds have 0 errors; both combined core suites pass **828/828**, including Unit Price **13/13**; focused Unit Price UIA passes **15/15**; Utility UIA passes **39/39** including CSS Unit Converter; catalog parity is **346 fixed routes + five dynamic families**, 319 registry records, and 346 ledger rows; the native installer contract passes. Renderer accounting is **34/346 fixed routes**, **34 `in-progress` / 312 `not-started`**.
- **Visual and shell disposition / 視覺同 shell 狀態：** the local LowLevel checkout exists but no headless MCP tool is callable in this session. The required native driver rejected its blank/near-uniform fallback after `CopyFromScreen` was unavailable; no PNG was retained or promoted, so visual evidence remains `capture-blocked`. A broad aggregate reached the Unit Price assertions but did not return a captured final footer; it is explicitly not a completed full-shell claim. Publishing remains C++-only.

**粵語摘要：** 三個 Unit Price alias 而家已同原生 shell 受控整合，係純標準 C++ core 加 C++/WinRT renderer，保留有效行／免費／平手／invariant 顯示、增減／release／reset、三種語言同只限明確 Copy，冇 CLR／managed delegation／workflow／release-policy 改動。Debug／Release 0 errors、core 各 **828/828**（Unit Price **13/13**）、專項 UIA **15/15**、包括 CSS 嘅 Utility UIA **39/39**、catalog parity 346+5／319 registry／346 ledger 同 installer contract 都通過；renderer **34/346**（**34 `in-progress` / 312 `not-started`**）。LowLevel MCP 不可呼叫，driver 拒絕空白 fallback，冇 PNG，所以 visual 保持 `capture-blocked`；廣泛 aggregate 冇最後 footer，唔當 full-shell 完成。發佈繼續只限 C++。
## Historical 2026-07-19 native Health Calculators feature handoff (pre-integration) · 歷史原生健康計算器功能交接（整合前）

- **Scope / 範圍：** `bmi`, `health`, and `module.bmi` now resolve to one genuine C++/WinRT Health Calculators page over pure standard-C++ `Bmi` logic. It preserves managed BMI WHO bands, Mifflin–St Jeor BMR, five TDEE factors, US Navy body-fat formulae/validation, raw metric/imperial relabelling, all language modes, route lifecycle reset, and no clipboard mutation.
- **Evidence / 證據：** native Debug and Release x64 solution builds both have 0 errors; Debug and Release core each pass **829/829**, including **14/14 BMI Calculator** contracts; catalog parity is **346 fixed routes + five dynamic families**; the native installer contract passes; and focused `-BmiRoutesOnly` UI Automation passes **14/14** across all three aliases, including UIA bounds/names, formulae, invalid recovery, unit-state retention, localization, release, and in-process re-entry reset.
- **Visual and boundary / 視覺同界線：** LowLevel MCP is not callable in this Codex session. The required `bmi` driver found `CopyFromScreen` unavailable and rejected a blank/near-uniform `PrintWindow` fallback; no PNG was created or retained and no worktree process remained, so visual evidence is honestly `capture-blocked`. This is an isolated feature-only handoff: no workflow, release, GitHub, `main`, or push mutation occurred.

**粵語摘要：** `bmi`、`health` 同 `module.bmi` 已經係真正 C++/WinRT 健康計算器頁，用純標準 C++ `Bmi` logic；保留 managed BMI WHO 分級、Mifflin–St Jeor BMR、五個 TDEE 系數、美國海軍體脂公式／驗證、原始公制／英制重標籤、三種語言、route reset，同埋冇剪貼簿改動。Debug／Release 0 errors、core 各 **829/829**（BMI **14/14**）、catalog parity 346+5、installer contract 同三個 alias UIA **14/14** 已通過。今個 session 冇可呼叫 LowLevel MCP；driver 因 `CopyFromScreen`／空白 fallback 受阻，冇 PNG／冇殘留 process，所以 visual 如實係 `capture-blocked`。呢個係孤立功能 handoff，冇改 workflow／release／GitHub／`main`，亦冇 push。

**Remote proof / 遙距證明：** Unit Price merge `37cc0e8a1d4605864756751265d379a954978b27` is an ancestor of `origin/main`. [Native run 29706847786](https://github.com/codingmachineedge/WinForge/actions/runs/29706847786) passed every native gate and published stable `native-v1.0.76` exactly at that SHA with only `WinForge-Native-Setup.exe` and `WinForge-native-x64-1.0.76.zip`. Its successful site-data run committed `fbaff0788`; dispatched [native run 29707001900](https://github.com/codingmachineedge/WinForge/actions/runs/29707001900) passed and made stable `native-v1.0.77` Latest at that current-main SHA, again exactly the native setup and ZIP. · Unit Price merge 係 `origin/main` 祖先；native run 29706847786 通過並準確出 stable `native-v1.0.76`，只有原生 setup 同 ZIP。site-data 提交 `fbaff0788` 後，dispatch run 29707001900 通過並將準確 current-main SHA 嘅 `native-v1.0.77` 設為 Latest，仍然只得兩個原生 asset。

## Current 2026-07-19 native Namespaced UUID controlled integration — local gates green · 2026-07-19 原生具名空間 UUID 受控整合 — 本機 gate 已通過

- **Scope / 範圍：** `uuid5`, `uuidv5`, and `module.uuidv5` are now a pure standard-C++/C++/WinRT RFC 4122 v3/v5 renderer: DNS/URL/OID/X500/custom namespaces, managed-compatible D/N/B/P/X parsing, UTF-16 replacement, U+180E parity, local bulk rows, language retention, route reset, and explicit-only Copy.
- **Current evidence / 目前證據：** Debug and Release x64 native solution builds have 0 errors; both core suites pass **815/815**; focused UUID UIA passes **21/21**; catalog parity is **346 fixed routes + five dynamic families**, 319 registry records, and 346 ledger rows; and the native installer contract passes. Renderer accounting is **33/346 fixed routes**, **33 `in-progress` / 313 `not-started`**.
- **Visual and release state / 視覺同發佈狀態：** no callable LowLevel MCP tool is exposed in this Codex session despite the local checkout. The fresh UUID driver capture and final aggregate shell are still required before the controlled `main` push; no visual success is claimed yet. This merge changes no workflow and preserves the C++-only release publisher.

- **Final gate update / 最終 gate 更新：** the aggregate native shell now passes **469/469**. The fresh `uuid5` driver found `CopyFromScreen` unavailable and rejected the blank/near-uniform PrintWindow fallback; no PNG or root app process remains, so visual evidence is honestly `capture-blocked`. · 完整 native shell 而家通過 **469/469**。最新 `uuid5` driver 發現 `CopyFromScreen` 不可用並拒絕空白／近乎單色 PrintWindow fallback；冇 PNG 或 root app process，所以 visual 如實係 `capture-blocked`。

**粵語摘要：** 三個 UUID alias 而家係純標準 C++／C++/WinRT RFC 4122 v3/v5 renderer，有 DNS／URL／OID／X500／自訂 namespace、managed 相容 D/N/B/P/X parser、UTF-16 replacement、U+180E parity、本機 bulk 行、語言保留、route reset 同只限明確 Copy。Debug／Release 0 errors、core 各 **815/815**、UUID UIA **21/21**、catalog parity 346+5／319 registry／346 ledger 同 installer contract 已通過；renderer 係 **33/346**（**33 `in-progress` / 313 `not-started`**）。雖然有本機 LowLevel checkout，今個 Codex session 冇可呼叫工具；最新 driver 擷取同完整 aggregate shell 未完成，未會聲稱 visual success。冇改 workflow，繼續由只限 C++ publisher 發佈。
## Current 2026-07-19 native Base Converter controlled integration — local gates green · 2026-07-19 原生進位轉換受控整合 — 本機 gate 已通過

- **Scope / 範圍：** `baseconvert` and `module.baseconvert` are integrated as a genuine local C++/WinRT renderer over a dependency-free standard-C++ arbitrary-precision core. It preserves 2–36 signed conversion, grouped binary, 64-bit two's-complement display, BigInteger-compatible bitwise operations, localization/state lifecycle, accessibility, explicit-only clipboard Copy, and managed Unicode `Trim()` diagnostics.
- **Evidence / 證據：** Debug and Release x64 solution builds exit 0 with 0 errors; both combined cores pass **857/857**, including **15/15** Base Converter contracts; focused `-BaseConvertRoutesOnly -AllowClipboardMutation` UI Automation passes **14/14** across both aliases; catalog parity is **346 fixed routes + five dynamic families**, 319 registry records, and 346 ledger rows; the native installer contract passes; and renderer accounting is **36/346** fixed routes (**36 `in-progress` / 310 `not-started`**).
- **Visual evidence / 視覺證據：** the local LowLevel MCP checkout is present but no headless MCP tool is callable in this Codex session. The fresh current `driver.ps1 -Native -Page baseconvert -WaitMs 16000` attempt reported CopyFromScreen unavailable and rejected a blank/near-uniform PrintWindow client. No PNG was created, retained, replaced, or promoted, cleanup left no WinForge process, and the route is honestly `capture-blocked`, not visual-pass.
- **Boundary / 界線：** this integration changes no `.github` workflow or release policy; the next step is a controlled `main` commit/push and verification of the existing C++-only release flow.

**粵語摘要：** `baseconvert` 同 `module.baseconvert` 已整合成真正本機 C++/WinRT renderer 同唔靠依賴嘅標準 C++ 任意精度 core；保留 2–36 有符號轉換、二進制分組、64-bit 二補數、BigInteger 相容 bitwise、本地化／狀態 lifecycle、無障礙、只限明確 Copy 同 managed Unicode `Trim()` 診斷。Debug／Release 0 errors、合併 core 各 **857/857**（Base Converter **15/15**）、兩個 alias UIA **14/14**、catalog parity 346+5（319 registry／346 ledger）、installer contract 通過，renderer 係 **36/346**（**36 `in-progress` / 310 `not-started`**）。LowLevel MCP 今個 session 不可呼叫；最新目前 driver 因 CopyFromScreen 不可用而拒絕空白／近乎單色 PrintWindow client，冇 PNG／冇 process，所以 visual 如實係 `capture-blocked`。整合冇改 `.github`／release policy；下一步係受控 `main` commit／push 同驗證只限 C++ release flow。

## Current 2026-07-19 native Slugify controlled integration — hosted release proven · 2026-07-19 原生網址別名受控整合 — hosted 發佈已證明
## Historical 2026-07-19 native Unit Price feature branch (pre-integration) · 歷史原生單位價格功能分支（整合前）

- **Scope / 範圍：** `priceper`, `unitprice`, and `module.unitprice` are a dedicated C++/WinRT renderer over pure standard-C++ `UnitPrice`. It preserves managed valid-row filtering, free/infinity and tolerance-tie decisions, invariant formatting, first-unit Add, removal/release/reset lifecycle, three-language state retention, and explicit-only clipboard Copy. No CLR host, managed-app launch, IPC delegation, workflow, or release-policy change is included.
- **Verification / 驗證：** native Debug and Release x64 solution builds exit 0 with 0 errors; Debug and Release core each pass **814/814**, including Unit Price **13/13**; catalog parity passes **346 fixed routes + five dynamic families**; and focused `-UnitPriceRoutesOnly -AllowClipboardMutation` UI Automation passes **15/15** across every alias. Renderer accounting is **33/346 fixed routes**, **33 `in-progress` / 313 `not-started`**.
- **Full-shell disposition / 完整 shell 狀態：** a broad run passed the full Unit Price assertion block but later stalled silently in unrelated CSS Unit Converter work. The integration owner directed an orderly stop of only the owned WinForge/smoke processes, so this is explicitly **not** a completed full-shell result; the authoritative full shell must run after controlled integration.
- **Visual / 視覺：** the local LowLevel checkout is present but no headless MCP tool is callable in this session. The required driver found `CopyFromScreen` unavailable and rejected a blank/near-uniform `PrintWindow` fallback. No PNG or canonical screenshot changed, no process…6303 tokens truncated…0 errors; XAML literal safety passed; full owned shell **300/300**, strengthened utility shell **39/39**, and catalog parity **346 + five families** passed. A deterministic one-million-finite-double .NET 11 differential found zero Aspect Ratio display-format mismatches.
- **Headless visual status / 無頭視覺狀態：** LowLevel Computer Use MCP 1.28.1 launched all three routes from one immutable 294-file runtime snapshot on separate named desktops, confirmed each exact launch PID, resolved one 1320×880 WinUI frame per route, captured and inspected full/client frames, killed each PID, and closed each desktop. Every 1304×841 client frame was one white color with zero standard deviation/non-white fraction. The repository driver separately launched each route and rejected the same blank `PrintWindow` fallback when `CopyFromScreen` was unavailable. The six invalid PNGs and immutable stage were deleted; no canonical image was replaced, so all three rows are honestly `capture-blocked`.
- **Branch release proof / 分支版本證明：** hosted native run [29663954724](https://github.com/codingmachineedge/WinForge/actions/runs/29663954724) passed every build/test/parity/package/installer-smoke gate and published [native-v1.0.37](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.37). Its tag ref and `target_commitish` both resolve exactly to `ce879cc6626eae328ec72e0143761c0edfbae340`; `WinForge-Native-Setup.exe` and `WinForge-native-x64-1.0.37.zip` are present with recorded SHA-256 digests.
- **Remote integration proof / 遙距整合證明：** after the final main push and fetch, verify `828c3279` and `ce879cc6` as ancestors of `origin/main`, confirm the expected core/app/tests/smoke/generator/docs/parity/handoff paths from the remote main tree, and only then delete `codex/native-utility-four` locally/remotely.

**粵語摘要：** 文字差異比對、長寬比計算同 CSS 單位換算已經真正原生化，功能／測試／文件／分支版本證據完成；三頁喺指定 LowLevel MCP 同 repository driver 都成功開啟，但呢個 desktop session 冇 WinUI composition，client frame 全白，所以視覺如實係 `capture-blocked`。今批完成後仍有 **325 條固定 route** 加五組動態家族要繼續移植。


## Native App Uninstaller integration record / 原生 App 解除安裝器整合記錄

- **Task commit / 任務提交：** 20fd3bb5813ade9056b1215de25473aeaa72660c.
- **Merge commit / 合併提交：** 477d2b2691e6c99a4b0de5237b6ed92ed70fc09e.
- **Scope / 範圍：** native current-user Store/UWP inventory, cache-only literal/PCRE2 Regex filtering, reviewed Confirm removal, and normal-integrity fail-closed protection; no deep cleanup or local-data deletion.
- **Evidence / 證據：** Debug/Release core 417/417, native Debug build 0 warnings/0 errors, catalog parity passed. LowLevel off-screen UI is honestly blocked by a blank WinUI client and missing NativePageTitle after 30 seconds; it never falls back to the visible desktop.
- **Remote proof / 遠端證明：** after fetch, task commit, pushed feature tip, and merge were ancestors of origin/main, with source, tests, docs, Pages mirror, and headless harness present. Detailed record: handoff-app-uninstaller.md and design/content/handoff-app-uninstaller.md.

**粵語：** 呢個 native slice 冇 deep cleanup 或本機資料刪除；Debug/Release core 各自 417/417。Cheap LowLevel off-screen WinUI frame 空白，headless UI 證據如實受阻，絕不回退去可見桌面。

## Native installer CI integration record · 原生安裝程式 CI 整合紀錄

- **Task commit / 任務提交：** b5cae63dd53e1892aca61e039597d1f3b9a6b73c.
- **Merge commit / 合併提交：** 1c3c9a1a.
- **Scope / 範圍：** reusable native installer contract verification at staged runtime, compiled Inno Setup executable, and installed payload boundaries; exact setup-output enforcement; CI documentation and Pages mirrors.
- **Evidence / 證據：** local static installer contract and three-gate workflow-wiring checks passed. The hosted Windows 2022 CI owns Inno Setup compilation and silent lifecycle execution.
- **Remote proof / 遙距證明：** after fetch, the task commit, pushed feature tip, and merge commit are ancestors of origin/main. The workflow, verifier, documentation, Pages mirrors, generated site data, and handoff memory exist in the remote main tree.
- **粵語摘要：** task、已推送 branch tip 同 merge commit 都係 origin/main ancestor；workflow、verifier、docs、Pages mirror、site data 同 handoff memory 都已確認喺 remote main。


## Native Symbols Palette integration record · 原生特殊符號調色盤整合紀錄

- **Task commit / 任務提交：** ba1a6c6192c1a150e35ebf09c0242d4c1d686177.
- **Merge commit / 合併提交：** 04a593f8.
- **Branch / 分支：** codex/native-symbols-palette was pushed and merged; cleanup is permitted only after this verified-memory commit is pushed and rechecked.
- **Remote proof / 遙距證明：** after fetch, the task commit, pushed feature tip, and merge commit are ancestors of origin/main. The native source, tests, docs, Pages mirror, capture status, and handoff records exist in the remote main tree.
- **粵語摘要：** 任務提交、已推送分支 tip 同合併提交 fetch 後全部係 origin/main ancestor；原生 source、tests、docs、Pages mirror、capture status 同 handoff memory 都喺 remote main。
- **Scope / 範圍：** native C++ catalog and C++/WinRT page for 226 local symbols, bilingual categories, safe literal/PCRE2 search, explicit Copy, and Regex Builder handoff.
- **Evidence / 證據：** core tests 411/411 in Debug and Release; owned LowLevel MCP UI Automation 238/238; catalog parity passed.
- **Visual status / 視覺狀態：** capture-blocked. The isolated driver rejected its blank/near-uniform fallback, so no screenshot is claimed or retained.
- **Detailed task memory / 詳細任務記憶：** handoff-symbols.md and design/content/handoff-symbols.md.


## Latest integration record — 2026-07-16

**Native Regex Tester all-match and replacement continuation / 原生 Regex Tester all-match 及 replacement 延續**

- `module.regextester` now uses the native bounded PCRE2 core to enumerate up to **100** non-overlapping matches, keep named capture metadata, safely progress zero-length matches under one shared deadline, and preview local replacements. PCRE2 `(x)` extended whitespace and `(n)` named-capture-only flags travel through the selected Shell, All Apps, cache-only Package Discover, or Regex Cheatsheet target.
- The replacement preview is deliberately not full .NET compatibility: it accepts only `$$`, existing `$0`–`$99`, and `${name}`, and invalid replacement text or the 32 KiB output cap fails closed without applying a target. Package Discover remains local-cache-only and never sends a pattern to argv or HTTPS.
- Evidence before integration: Debug and Release native suites each passed **403/403**; isolated LowLevel MCP headless UI Automation passed **226/226** for flags, all-match rows/named captures, valid/invalid replacement preview, the cap, target Apply, accessibility, and clipping. The inspected 852×880 full-window and 836×841 client-only captures were blank and discarded, so visual evidence is honestly `capture-blocked`.
- Git integration (verified): task commit `72ce549110b3d235b406de397736e89ecbcdb055` and remote feature tip `72ce549110b3d235b406de397736e89ecbcdb055` merged into `main` as `f7cba1a4694df705cd483868755af079e6250fda`. After fetch, all three commits were proven ancestors of `origin/main`, and the implementation, tests, docs, Pages mirrors, parity ledger, and these handoff files were confirmed in the remote main tree before cleanup.

**原生 Regex Tester all-match 及 replacement 延續 / Native Regex Tester all-match and replacement continuation**

- `module.regextester` 而家用原生有界 PCRE2 core 列舉最多 **100** 個非重疊相符、保留命名 capture metadata、喺同一個 deadline 下安全處理零長度相符，並預覽本機 replacement。PCRE2 `(x)` 忽略 pattern 空白同 `(n)` 只保留命名 capture 旗標會跟住已揀 Shell、All Apps、只限快取嘅 Package Discover 或 Regex Cheatsheet target。
- Replacement preview 刻意唔係完整 .NET 相容：只接受 `$$`、存在嘅 `$0`–`$99` 同 `${name}`；無效 replacement 或 32 KiB output cap 都會 fail closed，唔會套用 target。Package Discover 保持只限本機快取，絕對唔會將模式傳去 argv 或 HTTPS。
- 整合前證據：Debug 同 Release 原生 suite 都通過 **403/403**；isolated LowLevel MCP headless UI Automation 通過 **226/226**，覆蓋旗標、all-match rows／命名 capture、有效／無效 replacement preview、cap、target Apply、accessibility 同 clipping。852×880 full-window 同 836×841 client-only 截圖係空白、已丟棄，所以視覺證據如實係 `capture-blocked`。
- Git 整合（已驗證）：task commit `72ce549110b3d235b406de397736e89ecbcdb055` 同 remote feature tip `72ce549110b3d235b406de397736e89ecbcdb055` 已經以 `f7cba1a4694df705cd483868755af079e6250fda` 合併入 `main`。fetch 後已證明三個 commit 都係 `origin/main` 嘅 ancestor，清理前亦已確認 implementation、tests、docs、Pages mirrors、parity ledger 同呢兩份 handoff file 都喺 remote main tree。

## Latest continuation record — 2026-07-16

**Native Regex Cheatsheet / 原生 Regex 速查表**

- `module.regexcheat` is now a real C++/WinRT route, not a pending page. Its pure-C++ immutable catalog preserves 67 bilingual reference rows in nine categories and eight copy-only ready-made patterns. .NET-only reference syntax stays documentation; only an explicitly enabled, bounded PCRE2 local filter is evaluated.
- The native builder now targets this fourth registered local search surface. Invalid filters retain the preceding visible rows; static reference data never reaches a command line, package engine, network, or process. Clipboard writes require an explicit Copy button.
- Evidence: Debug and Release native suites each passed **395/395**; catalog parity passed 346 fixed routes, five dynamic families, 319 registry entries, and 22 categories; the isolated LowLevel MCP headless UI Automation shell passed **224/224**, including Cheatsheet filtering, invalid-pattern retention, explicit Copy, builder handoff, and horizontal bounds.
- Visual evidence is honestly `capture-blocked`: inspected LowLevel MCP full-window **852×880** and client-only **836×841** frames had a title bar/blank client and a blank client respectively. Both temporary PNGs were discarded; no stale, synthetic, or managed substitute was used as native proof.
- Git integration (verified): task commit `24f32ba85eade7244dc839760807ea3ea3d1a5d9` merged as `2872b234022188d70f250fdbae3d78a740f68fa8`; after fetch, both the task commit and `origin/codex/native-regex-cheatsheet` tip were proven ancestors of `origin/main`, with the implementation, docs, and memory files present in the remote tree before cleanup.

**原生 Regex 速查表 / Native Regex Cheatsheet**

- `module.regexcheat` 而家係真正嘅 C++/WinRT route，唔再係 pending page。純 C++ 不變 catalog 保留 67 項雙語參考、九個分類同八個只可明確複製嘅現成模式。
- 速查表成為第四個已註冊嘅本機 regex 搜尋 surface；只有明確開啟時先會以有資源限制嘅 PCRE2 篩選靜態文字。無效模式會保留原有結果，唔會送去命令列、套件引擎、網絡或者程序。
- 驗證：Debug/Release 都通過 **395/395**；catalog parity 通過；隔離 LowLevel MCP headless UI Automation 通過 **224/224**。852×880 full-frame 同 836×841 client-frame 都係空白客戶端，所以已丟棄，視覺證據係 `capture-blocked`。

## Project

**Repository:** WinForge  
**Current completion state:** Major launcher, companion apps, updater, reactor, and security hardening work completed.

## Git State

Final pushed state:

- `main`: `5aab5e5`
- Feature branch: `codex/finish-companions-reactor-p3`
- Feature commit: `f2a054e`
- Working tree: clean
- Feature branch merged into `main`
- `main` pushed successfully

The repository should be continued from `main`.

---

# Completed Work Summary

## 1. Companion App System

Implemented and hardened the companion application architecture.

Completed:
- Native companion launch support.
- Companion installation flow.
- Companion window management.
- Secondary window reuse.
- Safer process launching.
- Better failure handling.
- Explicit install state handling.

Fixed:
- False install success reporting.
- Race conditions when opening companion windows.
- Unsafe external process behavior.
- Elevated execution issues.

---

# Native Companion Fixes

## Problem

The native ImageForge/AudioForge companions built successfully but failed on machines missing MinGW runtime DLLs.

Affected runtime dependencies:
- `libgcc_s_seh-1.dll`
- `libwinpthread-1.dll`

## Resolution

Updated native build configuration:

- Added full static runtime linking.
- Removed dependency on external MinGW runtime DLLs.
- Verified resulting binaries only depend on Windows system libraries.

Validated:
- Native editor builds.
- Native editor launches from WinForge.
- No missing DLL dialogs.

---

# App Launcher

Completed launcher improvements.

Implemented:

- Launcher hub.
- Companion discovery.
- Install flow.
- Explicit installation state.
- Better launch error handling.
- Improved module navigation.
- Better secondary window lifecycle.

Validated:
- Launcher opens correctly.
- Modules load correctly.
- Companion routes work.

---

# Reactor Simulation

## Completed Fixes

The reactor simulation had a full-power thermal balance issue.

Fixed:

- Thermal equilibrium calculations.
- High-power stability behavior.
- Sustained operating plateau handling.

Added/improved:
- Reactor documentation.
- Operating procedure documentation.
- Emergency scenario documentation.
- Test reporting.

Validation:
- Reactor reaches stable high-power operation.
- No runaway thermal behavior in tested scenarios.

---

# Security Hardening

## Archive Extraction

Fixed:
- Archive traversal vulnerabilities.

Added:
- Safe extraction path validation.
- Protected extraction boundaries.

---

## Elevated Execution

Fixed:
- User-writable executable execution risk while elevated.

Added:
- Refusal of unsafe elevated native compilation.
- Safer launch behavior.

Applications now avoid inheriting unnecessary administrator privileges.

---

## Web Bridge

Hardened:

- Origin handling.
- Payload size limits.
- Save operation handling.
- Cancellation behavior.

---

## Diagram Import

Fixed:

- Unsafe imported IDs being inserted into SVG.

Added:
- Sanitization of imported identifiers.

---

## Admin Detection

Improved:

- Elevation checks.
- Fail-closed behavior when inspection fails.

---

# Updater

## Completed Updater Hardening

Implemented:

- SHA-256 verification.
- Side-by-side updater runtime.
- External updater helper.
- Mutex protection.
- Bounded download handling.
- Persistent updater logs.
- Legacy bootstrap recovery.

---

# Installer Fixes

Resolved:

- Installer exit code 3 handling.
- Bootstrap/relaunch issues.
- Update handoff failures.

Updated:
- Installer script.
- Launcher update recovery path.
- Updater startup flow.

---

# Logging

Added/improved:

- Persistent logs.
- Update diagnostics.
- Failure visibility.

---

# Build Validation

Completed:

- WinForge build.
- Launcher build.
- Updater build.
- Native companion build.
- Integration validation.

Important validation results:

- 0 build errors.
- Native companions launch successfully.
- Updater builds successfully.
- Git checks passed.

---

# UI Validation

Completed checks:

## WinForge Launcher
Passed:
- Application startup.
- Module loading.
- Launcher UI rendering.

## Image Editor
Passed:
- Module opening.
- Native editor launch path.
- Runtime dependency validation.

## CodeForge
Passed:
- First-run installation path testing.
- Monaco install security path validation.

---

# Run Skill / Automation Updates

Updated:

`.agents/skills/run-winforge`

Changes:
- Better publish failure handling.
- Stops stale WinForge processes before publishing.
- Avoids continuing after failed builds.
- Improved validation reliability.

Desktop automation was intentionally stopped before completion to avoid interfering with active applications.

---

# Deferred Request: Task Scheduler Auto Start

A request was made:

> Add Task Scheduler auto-run without UAC.

Decision:

Not implemented.

Reason:
- Creating a privileged scheduled task to bypass UAC would weaken Windows security.
- It could create a persistence/elevation risk.

Current behavior:
- Runs at normal user integrity.
- No UAC bypass.
- No hidden privileged startup.

Possible future safe alternative:
- Normal-user scheduled task.
- Startup shortcut.
- User-approved background service design.

---

# Continuation Update — Visible First-Run Compiler UX

Completed and committed on 2026-07-09:

- Expanded the native companion preparation popup into a resizable, bilingual terminal-style build window.
- Added separate phase/status UI, indeterminate progress, live batched stdout/stderr, selectable scrollback,
  Retry/Close states, and stable automation IDs.
- Blocked title-bar close while preparation is active. Cancel now waits for compiler process-tree cleanup before
  the window closes; a bounded cleanup-timeout state prevents an unclosable trap, disables unsafe Retry, and
  quarantines later native builds in that WinForge process until restart. Native preparation is process-wide
  serialized so a second companion cannot overlap cleanup or race the quarantine transition.
- Moved compiler discovery off the UI thread and made its filesystem/vswhere probes cancellation-aware.
- Added durable per-attempt logs under `%LOCALAPPDATA%\WinForge\logs\companion-builds`, with UTF-8 output,
  per-companion retention, log-folder access, complete result diagnostics, and fail-open disk-error handling.
- Preserved the prebuilt/source-hash cache fast paths, temporary-exe cleanup, atomic publication, normal-integrity
  execution, and static MinGW linking.
- Added `tests/CompanionBuildLog.Tests` and registered it in `WinForge.sln`.

Validation completed:

- `dotnet build WinForge.sln -c Debug -p:Platform=x64` — 0 errors.
- `dotnet run --project tests/CompanionBuildLog.Tests -c Debug` — 4/4 passed.
- Self-contained publish and Image Editor module render — passed.
- Injected compiler failure — live stdout/stderr, blocked close, failure UI, Retry, and persistent log passed.
- Explicit Cancel — compiler exited before the preparation window closed; cancellation log passed.
- Genuine MSVC build — ImageForge compiled, cached, launched, and logged `SUCCESS`; prior cache was restored.

---

# Remaining Work / Future Tasks

## Updater UX Improvements

Potential improvements:

- Better progress display.
- Retry button.
- Detailed error messages.
- Update history.
- Recovery diagnostics.

---

## Logging Improvements

Potential:

- Central application log viewer.
- Export diagnostics bundle.
- Log rotation.
- Crash reporting.

---

# Important Development Notes

- Continue from `main`.
- Do not reset to old feature branches.
- Existing companion/security work is already merged.
- Avoid reintroducing elevated auto-start behavior.
- Preserve static native linking.
- Keep updater verification and integrity checks.

---

# Recommended Next Session Start

1. Review the committed visible compiler/log UX changes.
2. Re-run:
   - `git status`
   - build validation.
3. Continue with updater UX improvements (retry, richer errors, update history/recovery diagnostics).
4. Consider a central application log viewer and diagnostics-bundle export.

End state: WinForge is in a completed hardened state with remaining work focused mainly on UX improvements.
