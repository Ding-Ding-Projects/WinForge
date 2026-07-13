# Exhaustive Smoke Campaign Closeout · 完整冒煙測試結案

## What this closes · 今次結案涵蓋乜

**EN —** This closes the repository’s safe exhaustive smoke baseline: every registered route has current launch evidence, every shell-only route has its own focused verifier, every declared XAML event surface resolves to code, and every headless regression project passes. It does not pretend that a launch or static check performed an unrestricted real-world action on the user’s machine.

**粵語 —** 呢份結案完成儲存庫安全嘅完整 smoke baseline：每條已登記 route 都有最新 launch 證據，每條 shell-only route 都有專用 verifier，每個已宣告 XAML event surface 都 resolve 去程式碼，而且所有 headless regression project 都通過。佢唔會將 launch 或 static check 冒充成已喺使用者電腦做咗無限制嘅真實操作。

## Route coverage union · 路線覆蓋聯集

The final generated manifest records **323 routes**, **805 aliases**, 319 registry entries, 318 MapType entries, four companion specs, 15 external-app specs, 1,297 source-review files, 350,643 source lines, 23 test projects, and no structural routing mismatch.

| Evidence slice | Inclusive indices | Final disposition |
| --- | --- | --- |
| Batches 01–12 | 0–299, in contiguous 25-route slices | Final launch evidence is recorded in the campaign ledger and Batch reports; earlier launch defects were fixed and retested in their owning batch. |
| Batch 13 | 300–321 | 22/22 first-attempt launch-pass, no retry or failure. |
| All Apps | 322 / shell.allapps | Focused UI Automation pass: picker dialog, search box, and selected navigation item present; no module selected. |

The reusable range-union verifier proved that the 13 recorded numeric ranges exactly cover **322/322** numeric manifest indices 0–321 with no gap or duplicate, while index 322 is separately identified as the shell.allapps dialog route.

**粵語 —** 最後生成嘅 manifest 有 **323 條 routes**、**805 個 aliases**、319 個 registry entries、318 個 MapType entries、4 個 companion specs、15 個 external-app specs、1,297 個 source-review files、350,643 行 source、23 個 test projects，同埋冇 structural routing mismatch。

| 證據範圍 | 包含 indices | 最後結果 |
| --- | --- | --- |
| Batches 01–12 | 0–299，每段連續 25 routes | 最後 launch 證據喺 campaign ledger 同 Batch reports；之前搵到嘅 launch defects 已喺所屬 batch 修正同重測。 |
| Batch 13 | 300–321 | 22/22 第一次 launch-pass，冇 retry 或 failure。 |
| All Apps | 322 / shell.allapps | Focused UI Automation 通過：有 picker dialog、search box 同已選取 navigation item；冇揀任何 module。 |

可重用嘅 range-union verifier 證明 13 個記錄咗嘅 numeric ranges 準確覆蓋 **322/322** numeric manifest indices 0–321，冇 gap 或 duplicate；index 322 會獨立標記為 shell.allapps dialog route。

## Source and control surfaces · 來源同控制介面

Test-WinForgeSourceSurfaceAudit.ps1 passed against the final tree:

- **334** XAML files and **2,858/2,858** declared event handlers resolved; zero unresolved.
- **1,969** button-like action controls and **1,910/1,910** direct handlers resolved; zero unresolved. The 59 controls without a direct event are explicitly classed as template/binding/no-direct-action surfaces rather than assumed to have a click handler.
- Zero page Loc.I.LanguageChanged add/remove mismatches and zero actionable source TODO/FIXME/throw new NotImplementedException markers.
- **316** generated feature reference pages and **1,853** generated button reference pages.

This is structural, line-level wiring evidence—not a claim that a static scan has executed every external operation. The full source inventory remains available through the generated manifest and feature/button references.

**粵語 —** Test-WinForgeSourceSurfaceAudit.ps1 喺最後 tree 通過：

- **334** 個 XAML files 同 **2,858/2,858** declared event handlers resolve；零 unresolved。
- **1,969** 個 button-like action controls 同 **1,910/1,910** direct handlers resolve；零 unresolved。冇 direct event 嘅 59 個 controls 會明確標為 template/binding/no-direct-action surfaces，唔會假設佢哋一定有 click handler。
- 零 page Loc.I.LanguageChanged add/remove mismatches，同零 actionable source TODO/FIXME/throw new NotImplementedException markers。
- **316** 個生成嘅 feature reference pages 同 **1,853** 個生成嘅 button reference pages。

呢個係結構／逐行 wiring 證據，唔係話 static scan 已執行每個 external operation。完整 source inventory 仍然可以由生成嘅 manifest 同 feature/button references 睇到。

## Test and build evidence · 測試同建置證據

Invoke-WinForgeAllTests.ps1 passed all **23/23** headless projects, including the 63/63 reactor scenario suite, Package Manager 27/27, security/credential fixtures, persistence, lifecycle, source-contract, and final route regressions. The runner deliberately chooses the system x64 dotnet host so net8.0 fixtures execute beside net11.0 fixtures.

The Debug x64 solution build completed with **0 errors**. The XAML literal-safety guard also passed, preserving the known managed-default protections.

**粵語 —** Invoke-WinForgeAllTests.ps1 全部 **23/23** headless projects 通過，當中包括 63/63 reactor scenarios、Package Manager 27/27、安全／credential fixtures、persistence、lifecycle、source-contract 同最後 route regressions。runner 會特登揀 system x64 dotnet host，令 net8.0 fixtures 可以同 net11.0 fixtures 一齊執行。

Debug x64 solution build 以 **0 errors** 完成。XAML literal-safety guard 都通過，已知嘅 managed-default protections 會保留。

## Visual and live-action disposition · 視覺同 live-action 處置

Fresh self-contained driver attempts throughout the campaign reached WinForge but were unable to yield an inspectable PNG: CopyFromScreen was unavailable and the PrintWindow fallback produced a uniform frame. The exact final observed blocker was:

> CopyFromScreen is unavailable and the PrintWindow fallback produced a uniform frame; graphics capture is unavailable in this desktop session.

No synthetic, stale, or uninspected screenshot is used as evidence. Affected routes are therefore **capture-blocked**, never visual-pass.

Live package installation/removal, VM and WSL changes, registry/tweak writes, service actions, network scans/downloads, credential logins, device actions, destructive file operations, and external-app launches were not executed against this user machine without an explicit, disposable target and authorization. Their source, validation, dry-run/fixture, route-launch, and safety boundaries are covered; real effects remain deliberately unclaimed.

**粵語 —** campaign 全程嘅新 self-contained driver 嘗試都有去到 WinForge，但攞唔到可檢查 PNG：CopyFromScreen 唔可用，而 PrintWindow fallback 產生 uniform frame。最後見到嘅 exact blocker 係：

> CopyFromScreen is unavailable and the PrintWindow fallback produced a uniform frame; graphics capture is unavailable in this desktop session.

冇 synthetic、stale 或未檢查嘅 screenshot 當證據。受影響 routes 因此係 **capture-blocked**，絕對唔係 visual-pass。

冇喺呢部使用者電腦、冇明確 disposable target 同授權下執行 live package installation/removal、VM 同 WSL changes、registry/tweak writes、service actions、network scans/downloads、credential logins、device actions、destructive file operations，同 external-app launches。佢哋嘅 source、validation、dry-run/fixture、route-launch 同 safety boundary 都有覆蓋；真實 effects 會刻意唔聲稱。

## Reproduce the closeout · 重現結案

~~~powershell
powershell -ExecutionPolicy Bypass -File .agents\skills\winforge-exhaustive-smoke\scripts\New-WinForgeSmokeInventory.ps1 -RepoRoot . -OutputDirectory artifacts\smoke\final\inventory
powershell -ExecutionPolicy Bypass -File .agents\skills\winforge-exhaustive-smoke\scripts\Test-WinForgeSourceSurfaceAudit.ps1 -RepoRoot . -Detailed
powershell -ExecutionPolicy Bypass -File .agents\skills\winforge-exhaustive-smoke\scripts\Invoke-WinForgeAllTests.ps1 -RepoRoot .
powershell -ExecutionPolicy Bypass -File .agents\skills\winforge-exhaustive-smoke\scripts\Test-WinForgeXamlLiteralSafety.ps1 -RepoRoot .
dotnet build WinForge.sln -c Debug -p:Platform=x64
~~~

Then use Test-WinForgeRouteCoverageUnion.ps1 with the 13 ranges in the table above and run Test-WinForgeShellAllAppsRoute.ps1 after a self-contained publish.
