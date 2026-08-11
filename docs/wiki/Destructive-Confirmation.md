# Destructive-action super confirmation · 破壞性動作超級確認

WinForge uses a native two-key and full-range slider confirmation for destructive actions. Escape and Emergency exit cancel without mutation; the key values never enter persistent data. · WinForge 用原生兩條匙同完整範圍滑桿確認破壞性動作。Escape 同緊急離開會取消而唔改資料；匙值永遠唔會進入保存資料。

Source: `Controls/SuperConfirmationDialog.cs`; source contract: `tests/ManagedReleaseContract.Tests/Program.cs`. A dedicated dialog capture remains pending while the remaining destructive callers migrate. · 來源：`Controls/SuperConfirmationDialog.cs`；source contract：`tests/ManagedReleaseContract.Tests/Program.cs`。其餘破壞性 caller 遷移期間，專門 dialog 擷取仍然待做。
