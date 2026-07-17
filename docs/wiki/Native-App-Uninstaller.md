# Native App Uninstaller / 原生 App 解除安裝器

See the canonical [Native App Uninstaller evidence](../Native-App-Uninstaller.md).

module.uninstall is a real C++/WinRT current-user Store/UWP inventory with local literal-default and bounded-PCRE2 Regex search, invalid-regex retention, review plus explicit Confirm removal, and a normal-integrity fail-closed gate. Deep cleanup is intentionally unavailable: this slice never deletes local data folders.

module.uninstall 係真正嘅 C++/WinRT 現有使用者 Store/UWP 清單，提供本機 literal 預設同有上限 PCRE2 Regex 搜尋、無效 Regex 保留結果、覆核加獨立 Confirm 移除，同正常 integrity fail-closed gate。深層清理刻意未開放：呢個 slice 絕對唔會刪本機資料夾。

Current proof is Debug/Release core **417/417** plus static/native safety checks. The LowLevel headless UI smoke is presently blocked: its off-screen WinUI frame has no NativePageTitle after 30 seconds and PrintWindow is blank. It deliberately does not fall back to the visible desktop; no stale managed image substitutes for a native frame.

> **粵語目前證據：** Debug/Release core 是 **417/417** 加 static/native safety checks。LowLevel headless UI smoke 的 off-screen WinUI frame 30 秒後冇 NativePageTitle，PrintWindow 亦是空白，所以如實 capture-blocked，絕不回退去可見桌面。

最新證據係 Debug/Release core **417/417** 加埋 isolated LowLevel focused App Uninstaller UI Automation smoke。新視覺證據誠實標示為 capture-blocked；絕對冇用舊 managed 圖代替 native frame。
