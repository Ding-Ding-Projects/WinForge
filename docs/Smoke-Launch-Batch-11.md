# Smoke Launch Batch 11 · 第十一批冒煙啟動

## Scope · 範圍

**EN —** This isolated evidence slice covers launchable inventory indices 250–274:
Short ID Encoder, Slugify, Smelter, SQL Formatter, SQLite Browser, SSH, Startup,
Steel Mill, String Compare, String Inspector, Subnet Calculator, IPv6 Subnet
Calculator, Symbols, Table Formatter, Tally Counter, Taskbar Tweaker, Scheduled
Tasks, Terminal, TestDisk, Text Columns, Text Diff, Text Escape, Text OCR, Text
Redact, and Find & Replace.

**粵語 —** 呢個隔離證據範圍覆蓋 inventory 可啟動 indices 250–274：Short ID
Encoder、Slugify、Smelter、SQL Formatter、SQLite Browser、SSH、Startup、Steel
Mill、String Compare、String Inspector、Subnet Calculator、IPv6 Subnet
Calculator、Symbols、Table Formatter、Tally Counter、Taskbar Tweaker、Scheduled
Tasks、Terminal、TestDisk、Text Columns、Text Diff、Text Escape、Text OCR、Text
Redact 同 Find & Replace。

## Evidence · 證據

- The generated inventory has 323 routes, 805 aliases, 1,280 source-review files,
  347,984 source lines, 12 test projects, and zero routing issues or unmapped aliases.
- The repository-local `Invoke-WinForgeRouteSmoke.ps1` ran indices 250–274 with a
  5-second launch wait and a bounded 15-second retry. All **25/25** passed at the
  first wait; there were no retries or failures.
- Static review found all **171** declared XAML handler names in the matching
  code-behind files, balanced `Loc.I.LanguageChanged` subscription counts, and no
  direct-scope TODO, FIXME, or `NotImplementedException` marker. The XAML literal
  guard passed, and `dotnet build WinForge.sln -c Debug -p:Platform=x64` completed
  with 0 errors.

**Safety disposition · 安全處置：** This is launch/static evidence, not permission to
operate user services. SQLite database changes, SSH/network activity, Startup and
Taskbar registry changes, Scheduled Tasks, Terminal launches, TestDisk recovery,
Text OCR capture, clipboard writes, and file writes were not live-executed. The
remaining behavior work is intentionally tracked separately rather than inferred from
the green launch result. · 呢個係啟動／靜態證據，唔代表有權操作使用者服務。SQLite
資料庫變更、SSH／網絡活動、Startup 同 Taskbar 登錄檔變更、Scheduled Tasks、Terminal
啟動、TestDisk 復原、Text OCR 擷取、剪貼簿寫入同檔案寫入都冇 live-run。其餘行為工作
會分開追蹤，唔會由 green launch 推斷出嚟。

## Visual status · 視覺狀態

The fresh self-contained `driver.ps1 -Page shortid -Out
artifacts/smoke/batch11/screenshots/shortid-default.png -WaitMs 5000` attempt reached
the WinForge window, but `CopyFromScreen` was unavailable. Its `PrintWindow` fallback
returned a uniform frame, so the driver stopped with `graphics capture is unavailable
in this desktop session`. No PNG was produced, inspected, replaced, or reused. Batch
11 is **capture-blocked**, never visual-pass. · 新嘅 self-contained `shortid` capture
已去到 WinForge 視窗，但 `CopyFromScreen` 唔可用；`PrintWindow` fallback 得到 uniform
frame，driver 因為呢個 desktop session graphics capture 唔可用而停止。冇 PNG 產生、
檢查、替換或者重用。Batch 11 係 **capture-blocked**，絕對唔係 visual-pass。

Raw, ignored evidence is under `artifacts/smoke/batch11/` in the task worktree.
