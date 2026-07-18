# Native C++ Installer CI · 原生 C++ 安裝程式 CI

The native release workflow now calls the reusable installer contract gate before packaging, after Inno Setup builds WinForge-Native-Setup.exe, and after the silent installer writes its payload.

**EN.** The gate verifies the per-user/x64 Inno policy, WinForge.exe PE payload, explicit third-party notice, absence of packaged debug symbols, exact setup filename, silent uninstallation, and guarded LocalAppData cleanup.

**粵語.** 呢個 gate 會驗證每用戶/x64 Inno 政策、WinForge.exe PE payload、第三方 notices、冇 package 到 debug symbols、setup 檔名正確、靜默解除安裝，同埋受保護 LocalAppData 清理完成。

Every branch push and every pull request into `main` runs this complete gate. A successful `main` push additionally publishes the portable ZIP and installer in a unique `native-v1.0.<run>` GitHub Release; branch and pull-request builds only retain Actions artifacts. The generated site-data commit explicitly dispatches the gate because GitHub suppresses recursive `GITHUB_TOKEN` push events. · 每次推送任何 branch 同每個合併去 `main` 嘅 pull request 都會執行完整 gate。成功推送去 `main` 亦會將可攜 ZIP 同安裝程式發佈到獨立 `native-v1.0.<run>` GitHub Release；branch 同 pull-request 建置只會保留 Actions artifacts。Generated site-data commit 會明確 dispatch 呢個 gate，因為 GitHub 會抑制遞迴 `GITHUB_TOKEN` push event。

| Boundary | Evidence |
| --- | --- |
| Runtime | WinForge.exe exists and has a PE header before packaging. |
| Setup | Exactly one WinForge-Native-Setup.exe exists and has a PE header. |
| Installed app | WinForge.exe, unins000.exe, and THIRD-PARTY-NOTICES.txt exist; .pdb / .ilk do not. |
| Cleanup | Silent uninstaller exits successfully and leaves no guarded install directory. |

Run the policy-only check locally with eng/native/Test-NativeInstallerContract.ps1. The full lifecycle runs in .github/workflows/native-release.yml.
