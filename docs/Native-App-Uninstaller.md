# Native App Uninstaller / 原生 App 解除安裝器

> **Migration status / 遷移狀態:** in progress · visual evidence is capture-blocked / 進行中 · 視覺證據為 capture-blocked

> **2026-07-17 headless correction / headless 更正：** the LowLevel off-screen desktop shows a WinUI title bar with a blank client frame and no NativePageTitle after 30 seconds. The smoke deliberately does not fall back to a focus-stealing visible desktop, so headless UI and visual evidence remain blocked.

The native C++/WinRT module.uninstall route inventories current-user Store/UWP packages with Windows::Management::Deployment::PackageManager. It keeps returned metadata in plain C++ records and never starts PowerShell, a managed helper, or an external package manager.

原生 C++/WinRT module.uninstall 會用 Windows::Management::Deployment::PackageManager 整理現有使用者的 Store/UWP 套件。回傳 metadata 只會留喺純 C++ 記錄；唔會啟動 PowerShell、受管理 helper、或者外部 package manager。

## Behaviour and safety / 行為同安全

- uninstall, apps, and module.uninstall resolve to the native route. Framework and resource packages are excluded before presentation.
- Literal search is local and case-insensitive by default. Opt-in Regex mode uses bounded PCRE2 only on the already-returned in-memory cache; it never performs a fresh package query. An invalid expression keeps the prior visible result set.
- A package can be removed only after the operator opens a review card and presses a separate **Confirm uninstall** action. Both review availability and final removal fail closed unless the process is at normal, non-elevated integrity.
- The confirmed full package name is passed only to PackageManager::RemovePackageAsync for the current user. No test invokes that action against a real package.
- **Deep cleanup is intentionally unavailable.** This migration slice never deletes a LocalAppData folder, package-family folder, or any other local data. It remains disabled until a future implementation has a handle-relative, stable-identity deletion primitive.

- uninstall、apps 同 module.uninstall 都會去原生 route；framework 同 resource 套件會喺顯示前排除。
- literal 搜尋預設只係本機兼唔分大小寫；選擇 Regex 後先會用有上限嘅 PCRE2 篩選已回傳嘅記憶體快取，絕對唔會重新查套件。無效 expression 會保留之前見到嘅結果。
- 操作員要先開啟覆核卡，再按獨立嘅 **Confirm uninstall** 先可以移除套件。覆核同最後移除都要求正常、非提升 integrity；否則會 fail closed。
- 已確認嘅完整套件名稱只會交畀現有使用者嘅 PackageManager::RemovePackageAsync。測試從來冇對真實套件執行呢個動作。
- **深層清理刻意未開放。** 呢個 migration slice 絕對唔會刪 LocalAppData 資料夾、package-family 資料夾，或者其他本機資料；要等到有 handle-relative、穩定身份嘅安全刪除 primitive 先會重新考慮。

## Verification / 驗證

- The native Debug solution builds with **0 warnings and 0 errors**.
- Debug and Release core executables each pass **417/417** tests, including six focused App Uninstaller pure-C++ cases.
- eng/native/Invoke-NativeAppUninstallerHeadlessSmoke.ps1 launches both WinForge and the UI Automation client through Cheap LowLevel's off-screen desktop. On 2026-07-17 it blocked honestly: NativePageTitle remained absent for 30 seconds, while PrintWindow returned a title bar plus a blank client frame. It does not fall back to a focus-stealing visible desktop.
- A prior visible-desktop focused smoke is historical and is not current UI evidence under the headless-only policy. A broad Invoke-NativeShellSmoke.ps1 rerun after this safety hardening exceeded the outer isolated-runner timeout while unrelated Password Generator checks were still passing; it was terminated cleanly and is **not** post-hardening full-suite evidence.
- The required native screenshot driver and the LowLevel headless PrintWindow capture were retried on 2026-07-17. Both yielded a blank or near-uniform client frame, so no PNG was accepted. Four stale managed App Uninstaller images were retired rather than relabelled as native evidence.

> **粵語 2026-07-17 更正：** Cheap LowLevel 的 off-screen desktop 會啟動 WinForge 同 UI Automation client，但 30 秒後仍冇 NativePageTitle，而 PrintWindow 只得 title bar 加空白 client frame。因此 headless UI 與視覺證據如實受阻；絕不回退到會搶焦點的可見桌面。舊 visible-desktop smoke 只屬歷史。

- 原生 Debug solution build 係 **0 warnings、0 errors**。
- Debug 同 Release core executable 都通過 **417/417**，包括六個針對 App Uninstaller 嘅純 C++ case。
- eng/native/Invoke-NativeAppUninstallerSmoke.ps1 喺 LowLevel headless UI Automation 通過安全訊息、冇深層清理動作、literal 快取篩選、無效 Regex 保留結果，同 Regex Builder 返回第 5 個 target 嘅合約。
- 安全 hardening 後重跑廣泛 Invoke-NativeShellSmoke.ps1 時，無關嘅 Password Generator check 仲喺 pass，但已超過 isolated runner 外層 timeout；程序已乾淨終止。佢**唔係**呢次 hardening 後嘅完整 suite 證據；上面 focused smoke 先係本 slice 最新嘅 UI 證據。
- 2026-07-17 已重試原生 screenshot driver。CopyFromScreen 不可用，而 PrintWindow 只得到 blank 或 near-uniform client frame，所以冇接受任何 PNG。四張舊 managed App Uninstaller 圖已退休，冇假裝係原生證據。

Run the focused check with:

    powershell -ExecutionPolicy Bypass -File eng\native\Invoke-NativeAppUninstallerHeadlessSmoke.ps1 -LowLevelRunner <path-to-lowlevel-computer-use-cheap.exe>

Visual capture stays capture-blocked until this desktop can produce a real, inspectable composited native frame.

要等呢個 desktop 可以產生真正、可檢查嘅 native composited frame，視覺擷取先會由 capture-blocked 解除。
