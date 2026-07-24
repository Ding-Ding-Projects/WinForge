# Windows Update pause/resume · Windows Update 暫停／恢復

## Behavior · 行為

Choose a bounded 7, 14, 21, 28, or 35 day pause. WinForge writes the global, feature-update, and quality-update start/end/expiry values under `HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings`. Resume removes every present pause value and flag; an individual deletion failure prevents a false success result.

可以揀 7、14、21、28 或最多 35 日暫停。WinForge 會寫全域、功能更新同品質更新嘅開始／結束／到期值；恢復會移除全部存在嘅暫停值同 flag，任何一個刪除失敗都唔會假扮成功。

## Failure and security · 失敗同安全

Elevation is required. Dates are normalized to stable UTC ISO-8601 text. The workflow does not disable update services or bypass the 35-day Windows limit.

需要管理員權限；日期會正規化成穩定 UTC ISO-8601。流程唔會停用更新服務，亦唔會繞過 Windows 35 日上限。

## Verification · 驗證

Focused tests cover every supported duration, rejection beyond 35 days, UTC conversion, and timestamp formatting. Smoke verification reads status only and does not pause the host.
