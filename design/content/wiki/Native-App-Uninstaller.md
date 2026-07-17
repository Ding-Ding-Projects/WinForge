# Native App Uninstaller / 原生 App 解除安裝器

Native C++/WinRT App Uninstaller evidence mirrors the repository wiki. Store/UWP metadata stays in the current-user cache; literal/Regex filtering never starts another package query; removal needs review, separate Confirm, and normal integrity. Deep cleanup is deliberately unavailable, so no local-data folder can be deleted by this migration slice.

原生 C++/WinRT App Uninstaller 證據同 repository wiki 一致。Store/UWP metadata 留喺現有使用者快取；literal/Regex 篩選唔會再開 package query；移除要覆核、獨立 Confirm 同正常 integrity。深層清理刻意未開放，所以呢個 migration slice 唔可能刪本機資料夾。

Debug/Release core is **417/417**. The LowLevel headless UI Automation smoke is currently blocked: no NativePageTitle appears in the off-screen WinUI frame and PrintWindow is blank, so it deliberately does not fall back to the visible desktop. Fresh native capture is capture-blocked, not a visual-pass claim.

> **粵語目前狀態：** Debug/Release core 是 **417/417**。off-screen WinUI frame 冇 NativePageTitle，而 PrintWindow 是空白；LowLevel headless UI smoke 因而受阻，絕不回退去可見桌面。native capture 是 capture-blocked，不是 visual-pass。

Debug/Release core 係 **417/417**。最新 focused LowLevel UI Automation smoke 覆蓋安全 gate、冇 deep-cleanup action、只篩本機快取、無效 Regex 保留結果，同 Regex Builder handoff。新 native capture 係 capture-blocked，唔係 visual-pass 聲稱。
