# Native C++ Installer CI · 原生 C++ 安裝程式 CI

The native release workflow now calls the reusable installer contract gate before packaging, after Inno Setup builds WinForge-Native-Setup.exe, and after the silent installer writes its payload.

**EN.** The gate verifies the per-user/x64 Inno policy, WinForge.exe PE payload, explicit third-party notice, absence of packaged debug symbols, exact setup filename, silent uninstallation, and guarded LocalAppData cleanup.

**粵語.** 呢個 gate 會驗證每用戶/x64 Inno 政策、WinForge.exe PE payload、第三方 notices、冇 package 到 debug symbols、setup 檔名正確、靜默解除安裝，同埋受保護 LocalAppData 清理完成。

Every branch push and every pull request into `main` runs this complete gate. A successful `main` push additionally publishes the portable ZIP and installer in a unique `native-v1.0.<run>` GitHub prerelease, preserving the managed `/releases/latest` channel; branch and pull-request builds only retain Actions artifacts. The generated site-data commit explicitly dispatches the gate because GitHub suppresses recursive `GITHUB_TOKEN` push events. · 每次推送任何 branch 同每個合併去 `main` 嘅 pull request 都會執行完整 gate。成功推送去 `main` 亦會將可攜 ZIP 同安裝程式發佈到獨立 `native-v1.0.<run>` GitHub prerelease，保留受控 `/releases/latest` channel；branch 同 pull-request 建置只會保留 Actions artifacts。Generated site-data commit 會明確 dispatch 呢個 gate，因為 GitHub 會抑制遞迴 `GITHUB_TOKEN` push event。

| Boundary | Evidence |
| --- | --- |
| Runtime | WinForge.exe exists and has a PE header before packaging. |
| Setup | Exactly one WinForge-Native-Setup.exe exists and has a PE header. |
| Installed app | WinForge.exe, unins000.exe, and THIRD-PARTY-NOTICES.txt exist; .pdb / .ilk do not. |
| Cleanup | Silent uninstaller exits successfully and leaves no guarded install directory. |

Run the policy-only check locally with eng/native/Test-NativeInstallerContract.ps1. The full lifecycle runs in .github/workflows/native-release.yml.

**Hosted proof · Hosted 證明：** The 2026-07-18 [non-`main` push run](https://github.com/codingmachineedge/WinForge/actions/runs/29655932405) passed C++ Release, 417 core route/package-manager tests, 46 parser tests, catalog parity, three contract checks, silent install/uninstall, and the 89,642,539-byte artifact upload. Release creation was correctly skipped for unmerged code. · 2026-07-18 嘅[非 `main` 推送執行](https://github.com/codingmachineedge/WinForge/actions/runs/29655932405)已通過 C++ Release、417 個 core route／package-manager 測試、46 個 parser 測試、目錄對等、三次合約檢查、靜默安裝／解除安裝，同 89,642,539-byte artifact 上載。未合併程式碼已正確略過版本建立。
