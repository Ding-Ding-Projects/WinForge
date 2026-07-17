# Native App Uninstaller / 原生 App 解除安裝器

This GitHub Pages mirror records the native C++/WinRT module.uninstall migration slice. It inventories only current-user Store/UWP packages, filters the returned local cache with literal-default or opt-in bounded PCRE2 Regex search, retains prior results for invalid Regex, and requires review plus a separate Confirm before the normal-integrity-gated native package removal call.

呢份 GitHub Pages mirror 記錄原生 C++/WinRT module.uninstall migration slice。佢只會整理現有使用者 Store/UWP 套件，用 literal 預設或者選擇性有上限 PCRE2 Regex 篩選已回傳嘅本機快取；無效 Regex 會保留之前結果，而且一定要覆核再獨立 Confirm，先會去正常 integrity gate 後嘅原生套件移除 call。

**Deep cleanup is intentionally unavailable.** This slice never deletes a local-data folder, including package-family paths. Debug/Release core tests pass **417/417**. The isolated LowLevel headless UI Automation smoke is currently blocked because the off-screen WinUI frame exposes no NativePageTitle and PrintWindow is blank; it never falls back to the visible desktop. Fresh visual capture remains capture-blocked, and legacy managed screenshots were retired.

> **粵語目前狀態：** 深層清理刻意不可用，呢個 slice 不會刪除本機資料。Debug/Release core 是 **417/417**。LowLevel off-screen WinUI frame 冇 NativePageTitle，PrintWindow 空白，所以 headless UI smoke 如實受阻，絕不回退去可見桌面。

**深層清理刻意未開放。** 呢個 slice 絕對唔會刪任何本機資料夾，包括 package-family path。Debug/Release core 測試通過 **417/417**，而 isolated LowLevel focused UI Automation smoke 通過安全、篩選、無效 Regex、同 Regex Builder 返回合約。新視覺擷取係 capture-blocked：required driver 拒絕 blank/near-uniform fallback，而舊 managed screenshot 已退休。
