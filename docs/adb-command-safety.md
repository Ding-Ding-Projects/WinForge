# Android ADB Command-Boundary Safety · Android ADB 指令邊界安全

## Scope · 範圍

The Android (ADB) module runs `adb` as a direct Windows process with a real argument list. It does not build a local `cmd.exe` or PowerShell command from device input.

Android（ADB）模組會以真正嘅參數清單直接啟動 Windows `adb` 程序。佢唔會用裝置輸入砌成本機 `cmd.exe` 或 PowerShell 指令。

## Protected inputs · 受保護輸入

- Wireless connect/disconnect accepts a host or IP address with an optional port; invalid characters are rejected before a process starts.
- Device serials are constrained before they are placed after `adb -s`.
- APK paths, device paths, package IDs, and logcat filter tokens stay as individual process arguments.
- Shell-tab text is deliberately sent as one argument after `adb -s <serial> shell`. Android interprets it on the selected device; Windows does not parse it as a local command.

- 無線連接／中斷只接受主機或 IP 加可選連接埠；無效字元會喺程序啟動前拒絕。
- 裝置序號放入 `adb -s` 前會受限制。
- APK 路徑、裝置路徑、套件 ID 同 logcat 篩選 token 都維持做獨立程序參數。
- Shell 分頁文字會刻意以一個參數放喺 `adb -s <serial> shell` 後面。Android 會喺所選裝置解讀佢；Windows 唔會當佢係本機指令解析。

## Regression coverage · 回歸覆蓋

Run the focused harness:

```powershell
dotnet run --project tests/AdbSecurity.Tests -c Debug
```

It verifies that command metacharacters in a wireless endpoint are rejected, valid connect actions use `adb` argument vectors, remote-shell text remains one remote argument, logcat uses `ProcessStartInfo.ArgumentList`, and unsafe serials never reach a process runner.

測試會確認：無線目標嘅命令特殊字元會被拒絕；有效連接使用 `adb` 參數向量；遠端 shell 文字維持一個遠端參數；logcat 使用 `ProcessStartInfo.ArgumentList`；而無效序號永遠唔會傳到程序執行器。
