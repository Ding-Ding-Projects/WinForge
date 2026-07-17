# Native App Uninstaller task memory / 原生 App 解除安裝器任務記憶

Verified integration record for the safe native C++/WinRT Store/UWP App Uninstaller slice.

- **Task commit / 任務提交：** 20fd3bb5813ade9056b1215de25473aeaa72660c.
- **Merge commit / 合併提交：** 477d2b2691e6c99a4b0de5237b6ed92ed70fc09e.
- **Scope / 範圍：** real current-user Store/UWP inventory through PackageManager, cached literal-default and bounded-PCRE2 Regex filtering, invalid-regex result retention, reviewed separate Confirm removal, and a normal-integrity fail-closed gate.
- **Safety / 安全：** RemovePackageAsync is the sole mutation. The native route has no deep-cleanup action, no local-data deletion API/path, and no retained deep-cleanup state. Package removal cannot start while elevation or token integrity is unsafe/unavailable.
- **Regex / Regex：** the shared registry has six native search surfaces: Shell, All Apps, cached Package Discover, Regex Cheatsheet, Symbols Palette, and cached App Uninstaller. App Uninstaller is builder target index 5; Tester-only is index 6.
- **Evidence / 證據：** native Debug build passed with 0 warnings and 0 errors; Debug and Release core executables each passed 417/417; catalog parity passed 346 fixed routes, five dynamic families, 319 registry records, 22 categories, and 346 ledger rows; PowerShell syntax and static no-deletion checks passed.
- **Headless visual status / Headless 視覺狀態：** Invoke-NativeAppUninstallerHeadlessSmoke.ps1 launches both WinForge and its tester only through Cheap LowLevel's off-screen desktop. On 2026-07-17 it correctly blocked when no NativePageTitle appeared after 30 seconds and PrintWindow returned a title bar plus blank client frame. It never falls back to a visible desktop; no screenshot is claimed.
- **Remote proof / 遠端證明：** after fetch, the task commit, pushed feature tip, and merge commit were proven ancestors of origin/main. The native source, tests, headless harness, docs, and GitHub Pages mirror were confirmed in the remote main tree before this memory record was written.

**粵語摘要：** 呢個 slice 係真 C++/WinRT Store/UWP cache、local literal/Regex 搜尋、review 加獨立 Confirm 同 normal-integrity fail-closed gate。唯一 mutation 係 RemovePackageAsync；冇 deep cleanup、冇本機資料刪除。Debug/Release core 各自 417/417，parity 已過。Cheap LowLevel off-screen desktop 的 WinUI client frame 空白，所以 UI/視覺證據如實受阻，絕不搶焦點回退到可見桌面。
