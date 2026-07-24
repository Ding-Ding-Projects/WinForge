# Preserved Package Manager stash recovery — 2026-07-24

## Provenance · 來源

The audited object is preserved commit `5cc3aa712f9e326dd8d9ae0bdd4c16d8771e1cb6` (`stash@{1}` at audit time, message `codex-preserve-unrelated`), compared with its first parent `27f343be170c43675e4a97f3de152eafb6c99e20`. The stash was inspected read-only and was not applied, popped, dropped, rewritten, or otherwise mutated. Its exact patch spans ten files with 987 insertions and 217 deletions. · 審核對象係保留 commit `5cc3aa7`，同佢第一個 parent `27f343b` 比較；全程只讀檢查，冇 apply、pop、drop、rewrite 或其他修改。原 patch 橫跨十個檔案，共 987 行新增／217 行刪除。

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
