# Safe archive create and delete workflows · 安全壓縮檔建立同刪除流程

## Behavior and configuration · 行為同設定

The Create safety expander accepts up to 32 include and 32 exclude masks separated by semicolons or newlines. Relative masks become independent recursive `-ir!` and `-xr!` 7-Zip arguments. The 7z-only NTFS option emits `-mtc=on`, `-mta=on`, `-mtm=on`, and `-ssp` so created/accessed/modified times are stored without bumping source Last-Access during the read.

建立安全 expander 接受最多 32 個 include 同 32 個 exclude 樣式，用分號或者換行分隔。相對樣式會變成獨立遞迴 `-ir!`／`-xr!` 參數。只限 7z 嘅 NTFS 選項會完整送出 `-mtc=on`、`-mta=on`、`-mtm=on` 同 `-ssp`，保存建立／存取／修改時間之餘，讀來源時唔會推高 Last-Access。

Move mode is a two-phase safety workflow: create the archive, run a separate `7z t` against the correct first split volume and password when present, then send the source to the Windows Recycle Bin. It deliberately does not use `-sdel`, rejects a drive root, and rejects an output archive inside the source folder. The in-archive delete expander accepts arbitrary reviewed relative entry names/masks, optionally adds `-r`, shows the exact masks in a destructive decision, and then calls `7z d` in place.

搬走模式有兩個安全階段：先建立壓縮檔，再用正確首個分卷同密碼（如有）獨立跑 `7z t`，最後先將來源送去 Windows 回收筒。佢刻意唔用 `-sdel`，會拒絕磁碟根目錄，同埋拒絕輸出放喺將要搬走嘅來源資料夾入面。檔內刪除接受任意已審閱相對項目名／樣式，可選 `-r`，破壞性確認會顯示確實樣式，之後先用 `7z d` 原位修改。

## Failure modes and security · 失敗模式同安全

- Absolute masks, `..` traversal, leading-dash masks, control characters, oversized masks/passwords, unsupported formats, and malformed volume sizes fail before process launch.
- Create or integrity failure retains the source. A valid archive plus Recycle Bin failure reports the archive success and retained source separately.
- Arguments use `ProcessStartInfo.ArgumentList`; user values are never concatenated into a shell command.
- Destructive delete/move actions require explicit confirmation. Visual verification must not select real user data or execute either action.

絕對路徑、`..` 穿越、開頭 dash、控制字元、過長樣式／密碼、不支援格式同錯誤分卷大小都會喺開程序前拒絕。建立或者完整性失敗一定保留來源；壓縮檔有效但回收筒失敗會分開報告。所有值用 `ArgumentList`，唔會砌入 shell 字串。刪除／搬走要明確確認，畫面驗證唔可以揀真實使用者資料或者執行。

## Verification · 驗證

`tests/RoadmapWorkflowCore.Tests` covers mask bounds/traversal, exact include/exclude and timestamp switches, password argument isolation, first-volume integrity targeting, arbitrary deletion, and source/output containment. Tests create only GUID-owned temporary fixtures and remove them afterward.

專項測試會驗證樣式邊界／穿越、完整 include／exclude 同時間開關、密碼參數隔離、首分卷完整性目標、任意刪除同來源／輸出包含關係；只會建立 GUID 自有暫存 fixture，之後移除。
