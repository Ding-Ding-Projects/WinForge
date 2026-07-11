# WinForge Exhaustive Smoke Campaign · WinForge 全面冒煙驗證

**Status · 狀態：** Baseline active · 基線已啟動

**EN —** This page records the repeatable evidence model for whole-app WinForge verification. It is deliberately a coverage ledger, not a marketing feature count: a route is complete only when its applicable routing, build, test, launch, visual, behavior, side-effect, and documentation evidence is recorded.

**粵語 —** 呢一頁記錄點樣可以重複做到成個 WinForge app 驗證。佢係涵蓋證據清單，唔係宣傳用功能數字：每條 route 只有喺適用嘅 routing、build、test、launch、visual、behavior、副作用同文件證據都記錄好先算完成。

## Baseline Snapshot · 基線快照

Generated on 2026-07-11 from the live source:

| Coverage surface · 涵蓋範圍 | Baseline count · 基線數量 |
| --- | ---: |
| Registered/map/navigation route records · 已登記／對映／導航 routes | 321 |
| Deep-link aliases · 深層連結別名 | 784 |
| Companion specifications · Companion 規格 | 4 |
| External-app launcher specifications · 外部 app launcher 規格 | 15 |
| First-party source files in review queue · source files 審查佇列 | 1,243 |
| First-party source lines in review queue · source lines 審查佇列 | 334,567 |
| Test projects · 測試專案 | 3 |
| Wiki pages · Wiki 頁面 | 2,191 |

**EN —** Counts are regenerated when registry, navigation, pages, or source files change; they are a point-in-time audit snapshot rather than a permanent product claim.

**粵語 —** registry、導航、頁面或者 source files 有變就要重新產生數量；佢只係某一刻嘅審查快照，唔係永久產品聲稱。

## Reproduce the Inventory · 重做盤點

Run the repository-local skill and its extractor from the repository root:

~~~powershell
powershell -ExecutionPolicy Bypass -File .agents\skills\winforge-exhaustive-smoke\scripts\New-WinForgeSmokeInventory.ps1 -RepoRoot . -OutputDirectory artifacts\smoke\<campaign-id>
~~~

The command creates manifest.json, manifest.csv, and summary.md. Artifacts are
ignored by Git until selected evidence is intentionally promoted to docs.

## Evidence Rules · 證據規則

- **EN —** Build success is compilation evidence only. Keep static routing,
  focused tests, launch, inspected screenshots, behavior, side-effect safety,
  and documentation evidence separate.
  **粵語 —** build 成功只代表編譯到。static routing、focused tests、launch、
  已檢查截圖、behavior、副作用安全同文件證據要分開。
- **EN —** A screenshot failure is logged as capture-blocked; it is never a
  visual pass. Current, inspected screenshots replace stale documentation
  images when a visual surface changes.
  **粵語 —** 截圖失敗要記做 capture-blocked，唔可以當 visual pass。視覺介面
  有改時，要用已檢查嘅最新截圖換走過時文件圖片。
- **EN —** Privileged, destructive, networked, package, credential, system
  tweak, and real-world integration actions use fixtures, dry-runs, validation
  paths, or reversible probes unless live execution is explicitly authorized.
  **粵語 —** 有權限、破壞性、網絡、套件、認證、系統調校同真實世界整合操作，
  除非明確批准真實執行，否則用 fixtures、dry-runs、validation paths 或者可還原
  probes。
- **EN —** Changed methods and meaningful branches must map to a focused test,
  safe manual exercise, or an explicit exclusion.
  **粵語 —** 有改嘅 methods 同重要 branches 必須對應 focused test、安全手動
  操作或者明確排除理由。

## Baseline Verification · 基線驗證

- Repository-local WinForge Exhaustive Smoke skill structure: valid.
- Inventory extractor: completed successfully with the snapshot above.
- No WinForge visual surface changed while this verification infrastructure was
  introduced, so this baseline claims no new visual-pass result.

[← Wiki Home](Home.md) · [Developer](Developer.md) · [Screenshots](Screenshots.md)
