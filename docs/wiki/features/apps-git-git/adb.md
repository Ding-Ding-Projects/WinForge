# Android (ADB) · Android（ADB）

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.adb</code> |
| Deep-link alias · 深層連結別名 | <code>adb</code> |
| Category · 分類 | Apps & Git · 程式與 Git |
| Page class · 頁面類別 | <code>AndroidAdbModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/AndroidAdbModule.xaml</code> |
| Button docs · 按鈕文件 | 24 |

## What It Covers · 功能範圍

**EN —** Android (ADB) is registered in WinForge search and navigation with these keywords: <code>android adb apk logcat shell screenshot reboot fastboot scrcpy push pull file backup mirror 手機 安卓 鏡像 備份</code>.

**粵語 —** Android（ADB） 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>android adb apk logcat shell screenshot reboot fastboot scrcpy push pull file backup mirror 手機 安卓 鏡像 備份</code>。

## Command Safety · 指令安全

**EN —** Device IDs, wireless `host:port` targets, paths, logcat filters, and remote-shell text are sent to `adb` as separate process arguments. WinForge never passes these values through local `cmd.exe` or PowerShell. Wireless targets accept a host/IP address with an optional port; a malformed value is rejected before `adb` starts. A command entered in the Shell tab is still intentionally executed by the selected Android device, not by the Windows host.

**粵語 —** 裝置識別碼、無線 `host:port` 目標、路徑、logcat 篩選同遠端 shell 文字，都會當做獨立程序參數傳畀 `adb`。WinForge 絕對唔會將呢啲值交畀本機 `cmd.exe` 或 PowerShell。無線目標只接受主機／IP 加可選連接埠；格式唔啱會喺啟動 `adb` 前拒絕。Shell 分頁輸入嘅指令仍然會刻意喺所選 Android 裝置執行，唔會喺 Windows 主機執行。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [RefreshBtn](../../buttons/apps-git-git/adb/001-refreshbtn.md) | `Button` | `RefreshBtn` | `Refresh_Click` |
| [ConnectBtn](../../buttons/apps-git-git/adb/002-connectbtn.md) | `Button` | `ConnectBtn` | `Connect_Click` |
| [InstallBtn](../../buttons/apps-git-git/adb/003-installbtn.md) | `Button` | `InstallBtn` | `Install_Click` |
| [ShotBtn](../../buttons/apps-git-git/adb/004-shotbtn.md) | `Button` | `ShotBtn` | `Shot_Click` |
| [LogcatBtn](../../buttons/apps-git-git/adb/005-logcatbtn.md) | `Button` | `LogcatBtn` | `Logcat_Click` |
| [PackagesBtn](../../buttons/apps-git-git/adb/006-packagesbtn.md) | `Button` | `PackagesBtn` | `Packages_Click` |
| [RebootBtn](../../buttons/apps-git-git/adb/007-rebootbtn.md) | `Button` | `RebootBtn` | `` |
| [RebootSystem](../../buttons/apps-git-git/adb/008-rebootsystem.md) | `MenuFlyoutItem` | `RebootSystem` | `RebootSystem_Click` |
| [RebootBootloader](../../buttons/apps-git-git/adb/009-rebootbootloader.md) | `MenuFlyoutItem` | `RebootBootloader` | `RebootBootloader_Click` |
| [RebootRecovery](../../buttons/apps-git-git/adb/010-rebootrecovery.md) | `MenuFlyoutItem` | `RebootRecovery` | `RebootRecovery_Click` |
| [ShellRunBtn](../../buttons/apps-git-git/adb/011-shellrunbtn.md) | `Button` | `ShellRunBtn` | `ShellRun_Click` |
| [FilesUpBtn](../../buttons/apps-git-git/adb/012-filesupbtn.md) | `Button` | `FilesUpBtn` | `FilesUp_Click` |
| [FilesGoBtn](../../buttons/apps-git-git/adb/013-filesgobtn.md) | `Button` | `FilesGoBtn` | `FilesGo_Click` |
| [PushBtn](../../buttons/apps-git-git/adb/014-pushbtn.md) | `Button` | `PushBtn` | `Push_Click` |
| [PullBtn](../../buttons/apps-git-git/adb/015-pullbtn.md) | `Button` | `PullBtn` | `Pull_Click` |
| [FileDeleteBtn](../../buttons/apps-git-git/adb/016-filedeletebtn.md) | `Button` | `FileDeleteBtn` | `FileDelete_Click` |
| [ApkLoadBtn](../../buttons/apps-git-git/adb/017-apkloadbtn.md) | `Button` | `ApkLoadBtn` | `ApkLoad_Click` |
| [ApkBackupBtn](../../buttons/apps-git-git/adb/018-apkbackupbtn.md) | `Button` | `ApkBackupBtn` | `ApkBackup_Click` |
| [LiveStartBtn](../../buttons/apps-git-git/adb/019-livestartbtn.md) | `Button` | `LiveStartBtn` | `LiveStart_Click` |
| [LiveStopBtn](../../buttons/apps-git-git/adb/020-livestopbtn.md) | `Button` | `LiveStopBtn` | `LiveStop_Click` |
| [LiveClearBtn](../../buttons/apps-git-git/adb/021-liveclearbtn.md) | `Button` | `LiveClearBtn` | `LiveClear_Click` |
| [MirrorStartBtn](../../buttons/apps-git-git/adb/022-mirrorstartbtn.md) | `Button` | `MirrorStartBtn` | `MirrorStart_Click` |
| [MirrorRecordBtn](../../buttons/apps-git-git/adb/023-mirrorrecordbtn.md) | `Button` | `MirrorRecordBtn` | `MirrorRecord_Click` |
| [MirrorStopBtn](../../buttons/apps-git-git/adb/024-mirrorstopbtn.md) | `Button` | `MirrorStopBtn` | `MirrorStop_Click` |
