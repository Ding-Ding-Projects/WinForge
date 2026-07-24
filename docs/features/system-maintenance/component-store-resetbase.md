# Component-store ResetBase · 元件存放區 ResetBase

## Behavior · 行為

The workflow runs `dism /Online /Cleanup-Image /StartComponentCleanup /ResetBase` only after a persistent warning, a separate acknowledgement checkbox, and a final decision dialog.

流程只會喺持續警告、獨立確認勾選同最後決定對話框全部通過後，先執行 `dism /Online /Cleanup-Image /StartComponentCleanup /ResetBase`。

## Irreversible effect · 不可逆影響

ResetBase removes superseded WinSxS component versions. Installed Windows updates cannot be uninstalled afterward. The action requires elevation and the PC must remain powered until DISM exits. This is not presented as ordinary cleanup or an undoable operation.

ResetBase 會移除已取代 WinSxS 元件版本，完成後無法解除安裝現有 Windows 更新。操作需要管理員，DISM 完成前要保持供電；唔會扮成普通可還原清理。

## Verification · 驗證

Focused tests lock the exact four arguments. Visual/smoke verification exercises the safety surface only; it never runs ResetBase.
