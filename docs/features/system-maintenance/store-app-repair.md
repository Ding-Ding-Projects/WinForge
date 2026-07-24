# Store-app reset and re-registration · 商店 app 重設同重新註冊

## Behavior · 行為

Load installed non-framework Store/UWP apps and select one. **Reset app data** runs `Reset-AppxPackage` for that exact package after a destructive decision. **Re-register manifest** verifies the selected package's installed `AppXManifest.xml`, then runs `Add-AppxPackage -DisableDevelopmentMode -Register` for that manifest.

載入已安裝非 framework 商店／UWP app 再揀一個；「重設 app 資料」經破壞性決定後對準嗰個套件執行 `Reset-AppxPackage`。「重新註冊 manifest」會先驗證所選套件嘅 `AppXManifest.xml`，再只註冊嗰份 manifest。

## Failure and security · 失敗同安全

Package identities accept only the PackageManager-provided safe character set. Reset can remove local settings, sessions, and unsynced data, so it is never automatic. Re-registration does not clear data. Missing packages/manifests or PowerShell errors remain visible failures.

套件身份只接受 PackageManager 提供嘅安全字元；重設可能刪本機設定、工作階段同未同步資料，所以永遠唔會自動做。重新註冊唔會清資料；套件／manifest 缺失或 PowerShell 錯誤會如實顯示。

## Verification · 驗證

Focused tests cover exact package binding, injection rejection, manifest existence checking, reset, and registration command contracts. Smoke verification loads/renders controls without resetting an app.
