# Storage Sense policy · 儲存空間感知政策

## Behavior · 行為

System Doctors reads and writes the current user's StoragePolicy values: enable (`01`), run cadence (`2048`: low-space/daily/weekly/monthly), Recycle Bin retention (`256`), and untouched Downloads retention (`512`). All four values are validated as one settings snapshot before any write.

系統醫生會讀寫目前使用者 StoragePolicy 嘅啟用值、執行週期、回收筒保留期，同冇用過嘅下載檔案保留期；寫入前會先一次過驗證四個值。

## Failure and security · 失敗同安全

This is a per-user registry change and does not require elevation. Unsupported cadence/retention values fail closed. Choosing `Never` writes `0`; it does not delete user files during Apply—the Windows Storage Sense scheduler remains the component that later evaluates cleanup eligibility.

呢個係每使用者 registry 設定，唔需要管理員。唔支援嘅日數會安全拒絕；揀「永不」會寫 `0`，撳套用嗰刻唔會直接刪檔，之後仍由 Windows Storage Sense 排程判斷。

## Verification · 驗證

The focused harness iterates every supported cadence/retention combination and rejects out-of-contract values. Visual verification expands the workflow only; it does not trigger cleanup.
