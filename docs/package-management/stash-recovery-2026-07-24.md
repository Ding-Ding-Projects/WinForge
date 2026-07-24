# Package Manager stash recovery and verified cleanup — 2026-07-24

## Provenance · 來源

The primary audited object was preserved commit `5cc3aa712f9e326dd8d9ae0bdd4c16d8771e1cb6` (`stash@{1}` at audit time, message `codex-preserve-unrelated`), compared with its first parent `27f343be170c43675e4a97f3de152eafb6c99e20`; companion object `181fc231c93b2533392344a405cb18750b4eaa48` (`codex-temp-powertoys`) was audited in parallel. Both were initially inspected read-only and neither stale patch was applied or popped. The primary patch spans ten files with 987 insertions and 217 deletions. · 主要審核對象係保留 commit `5cc3aa7`，同佢第一個 parent `27f343b` 比較；另一份 `181fc231c` PowerToys object 亦同步審核。兩份一開始都只讀檢查，舊 patch 從未 apply 或 pop；主要 patch 橫跨十個檔案，共 987 行新增／217 行刪除。

## Ten-file disposition · 十個檔案處置

| Preserved file | Retained useful behavior | Rejected or superseded behavior |
|---|---|---|
| `App.xaml.cs` | App-global package-update scheduler starts even if the page is never opened. | Page-local scheduler startup was removed as duplicate lifecycle ownership. |
| `Pages/PackageManagerModule.xaml.cs` | Native provenance card and explicit confirmation before removing sources. | External UniGetUI launch/install escape hatch; current native queue/source identity and responsive/a11y work supersede the old page implementation. |
| `Services/BundleService.cs` | JSON/YAML/XML current and legacy round-trips, reference/source validation, safe previews/scripts. | Older permissive import/command construction is superseded by current source-aware fail-closed policy and bounded parsing. |
| `Services/LicenseCatalogService.cs` | Pinned UniGetUI MIT provenance notice. | None; notice remains data-only and does not make upstream code a runtime dependency. |
| `Services/PackageManagerSettings.cs` | Credentials are deliberately excluded from proxy URLs and command arguments. | Credential-in-URL construction and UI collection of unused credentials are rejected; structured proxy and vcpkg values now pass a dedicated fail-closed policy, while any legacy secret stays DPAPI-protected until explicitly forgotten/reset. |
| `Services/PackageManagers.cs` | PyPI discovery; NuGet-backed .NET-tool updates; PowerShell Gallery/PSResourceGet, optional cargo-update, non-mutating vcpkg, and Bun global update discovery. | Silent helper installation and mutating update probes remain rejected; current implementations add later correctness/security fixes. |
| `Services/PackageOperations.cs` | Per-manager settings, configured executable resolution, vcpkg root support, and correct Windows PowerShell/PowerShell 7 hosts. | Strip-only triplet sanitization and fixed command builders are superseded by token validation, source-aware policy, corrected .NET-tool scope, and safer PSResource operations. |
| `Services/PackageService.cs` | Real WinGet availability detection and duplicate `scrcpy` dependency cleanup while retaining one valid `Genymobile.scrcpy` entry. | The old unconditional “WinGet exists on every Windows 11 image” assumption. |
| `Services/SourceManager.cs` | Reject unsafe names/URLs and register PowerShell repositories untrusted by default. | Silently stripping shell characters and auto-trusting new sources; current PowerShell literal quoting adds another defense layer. |
| `ThirdParty/UniGetUI.UPSTREAM.md` | Immutable upstream commit/license/build-exclusion provenance. | Treating the upstream executable as a shipped fallback or runtime dependency. |

Current `origin/main` already contained every retained behavior, often with later source identity, queue, cancellation, redaction, and validation hardening. Recovery therefore did not replay the stale patch. The bounded follow-up fixes the remaining structured-setting command boundary and responsive/accessibility defects, with focused tests and fresh visual evidence. · 目前 `origin/main` 已經包含全部保留行為，而且好多位置已有更新嘅來源 identity、佇列、取消、遮蔽同驗證強化，所以今次冇重播過時 patch；有限 follow-up 只修正餘下結構化設定指令界線，同響應式／無障礙問題，並加入專項測試同最新視覺證據。

## Verified integration and cleanup · 已驗證整合及清理

The safe union tip `d8399b1c8` and its source/documentation tips were pushed and proven ancestors of remote `main` at `6524543c565a6796a74101ef38dcbb6248eec651`, with the expected package files present in the remote tree. Only after that proof were the two clean package worktrees and merged local/remote integration branches removed. The stash refs were then rechecked against the exact object IDs above and dropped as redundant. The shipped behavior remains recoverable through immutable `main` history; no unmerged or unpushed work was deleted. · 安全 union tip 同來源／文件 tip 已 push，並證明全部係 remote `main` `6524543c5` 嘅 ancestor，預期套件檔亦喺 remote tree。完成呢項證明後先移除兩個乾淨 package worktree 同已合併本機／remote branch，再按上面準確 object ID 重驗並刪除冗餘 stash ref。已交付行為由不可變 `main` 歷史保存；冇刪除任何未合併或未 push 工作。
