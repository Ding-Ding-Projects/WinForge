# Native C++ Installer CI · 原生 C++ 安裝程式 CI

**EN.** The native release workflow on Windows 2022 builds the C++20/WinUI 3 runtime, packages installer/WinForge.Native.iss, silently installs it under a guarded per-user LocalAppData path, and silently uninstalls it again. The reusable eng/native/Test-NativeInstallerContract.ps1 gate now makes those guarantees explicit.

**粵語.** Windows 2022 上嘅 native release workflow 會 build C++20/WinUI 3 runtime、封裝 installer/WinForge.Native.iss、喺受保護嘅每用戶 LocalAppData 路徑靜默安裝，之後再靜默解除安裝。可重用嘅 eng/native/Test-NativeInstallerContract.ps1 gate 而家會明確驗證呢啲保證。

Every branch push and every pull request into `main` runs the complete native gate, without path filtering. Every successful push to `main` also creates a uniquely tagged GitHub prerelease containing the native portable ZIP and installer; prerelease status keeps the incomplete migration from replacing the managed app consumed through `/releases/latest`. Branch and pull-request builds upload Actions artifacts but cannot publish unmerged binaries as releases. The site-data workflow explicitly dispatches this gate after its own generated `main` commit because GitHub intentionally suppresses recursive push events created with `GITHUB_TOKEN`. · 每次推送任何 branch 同每個合併去 `main` 嘅 pull request 都會執行完整原生 gate，唔會再按路徑略過。每次成功推送去 `main` 亦會建立獨立標籤 GitHub prerelease，包含原生可攜 ZIP 同安裝程式；prerelease 狀態會避免未完成嘅遷移取代由 `/releases/latest` 提供畀受控 app 嘅版本。Branch 同 pull-request 建置只會上載 Actions artifacts，唔可以將未合併 binary 發佈成版本。Site-data workflow 用自己嘅 generated commit 推送 `main` 後會明確 dispatch 呢個 gate，因為 GitHub 會刻意抑制由 `GITHUB_TOKEN` 建立嘅遞迴 push event。

## Contract boundaries · 合約邊界

**Immutable release provenance · 不可變版本來源：** Release creation passes the workflow SHA as the explicit tag target. A concurrent `main` update therefore cannot make a finished run tag binaries as though they came from the newer commit. · 建立版本時會將 workflow SHA 明確傳入做標籤目標；所以就算 `main` 同時更新，完成咗嘅 run 都唔會將 binary 錯標成來自較新 commit。

- **Staged runtime / 已 stage runtime：** requires a non-empty PE WinForge.exe before Inno Setup runs.
- **Installer binary / 安裝程式 binary：** requires exactly one WinForge-Native-Setup.exe and validates its PE header after compilation.
- **Installed payload / 已安裝 payload：** requires WinForge.exe, unins000.exe, and THIRD-PARTY-NOTICES.txt; rejects packaged .pdb or .ilk files.
- **Installer policy / 安裝程式政策：** checks the fixed per-user AppId, PrivilegesRequired=lowest, LocalAppData target, x64-only settings, deterministic output name, static-PCRE2 notice, and debug-artifact exclusion.

## CI sequence · CI 次序

1. Restore vcpkg PCRE2 and native NuGet dependencies.
2. Build and test the Release C++ solution, then verify catalog parity.
3. Stage the native runtime and validate the installer contract.
4. Compile exactly one Inno Setup executable and validate it.
5. Silent-install, validate the installed payload, silent-uninstall, and prove the guarded install root is gone.
6. Upload the portable ZIP and installer only after all gates pass.
7. On a successful `main` push, publish those two files in a unique `native-v1.0.<run>` GitHub prerelease, pin its tag to the workflow SHA, and preserve the managed stable channel.

## Local check · 本機檢查

Run the static policy check without building:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng\native\Test-NativeInstallerContract.ps1
~~~

After a Release build, pass -PublishDir; after Inno Setup, pass -InstallerPath; after a disposable silent-install test, pass -InstallDir.

## Hosted proof · Hosted 證明

The 2026-07-18 [non-`main` push run](https://github.com/codingmachineedge/WinForge/actions/runs/29655932405) proves the unrestricted push trigger and branch safety gate: C++ Release, 417 core route/package-manager tests, 46 parser tests, catalog parity, all three contract invocations, silent install/uninstall, and the 89,642,539-byte artifact upload passed; public release creation was skipped for the unmerged branch. · 2026-07-18 嘅[非 `main` 推送執行](https://github.com/codingmachineedge/WinForge/actions/runs/29655932405)證明無路徑限制嘅推送觸發同 branch 安全 gate：C++ Release、417 個 core route／package-manager 測試、46 個 parser 測試、目錄對等、三次合約調用、靜默安裝／解除安裝，同 89,642,539-byte artifact 上載全部通過；未合併 branch 已略過公開版本建立。

## Visual evidence · 視覺證據

This is packaging and CI work, not a changed app page. No UI screenshot is claimed or replaced; the installer’s silent lifecycle is covered by executable contract and cleanup checks.
