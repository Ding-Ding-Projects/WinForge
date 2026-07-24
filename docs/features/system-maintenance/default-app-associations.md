# Default app association templates · 預設程式關聯範本

## Behavior · 行為

Export selects a destination `.xml` and runs DISM `/Online /Export-DefaultAppAssociations:`. Import selects an existing `.xml`, explains the scope, requires a decision, and runs `/Import-DefaultAppAssociations:`. These are machine templates for new user profiles; they do not forge or bypass the protected per-user `UserChoice` hash.

匯出會揀 `.xml` 目的地再執行 DISM；匯入會揀現有 `.xml`、解釋範圍並要求決定。呢啲係新使用者設定檔嘅全機範本，唔會偽造或繞過受保護嘅每使用者 `UserChoice` hash。

## Failure and security · 失敗同安全

Only a fully qualified local XML path is accepted, and import requires the file to exist. User paths travel as one process argument, not shell text. The operation fails closed unless WinForge is already elevated; the page offers an explicit administrator relaunch.

只接受完整本機 XML 路徑，匯入檔案一定要存在。用戶路徑係獨立程序參數，唔會拼入 shell。WinForge 未提升權限會安全拒絕，頁面可明確用管理員身分重開。

## Verification · 驗證

Focused tests cover relative paths, extension validation, missing import files, and argument preservation. Smoke verification never imports a machine template.
