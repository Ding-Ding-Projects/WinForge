# Native C++ Installer CI · 原生 C++ 安裝程式 CI

The native release workflow now calls the reusable installer contract gate before packaging, after Inno Setup builds WinForge-Native-Setup.exe, and after the silent installer writes its payload.

**EN.** The gate verifies the per-user/x64 Inno policy, WinForge.exe PE payload, explicit third-party notice, absence of packaged debug symbols, exact setup filename, silent uninstallation, and guarded LocalAppData cleanup.

**粵語.** 呢個 gate 會驗證每用戶/x64 Inno 政策、WinForge.exe PE payload、第三方 notices、冇 package 到 debug symbols、setup 檔名正確、靜默解除安裝，同埋受保護 LocalAppData 清理完成。

Every push and every pull request into `main` runs this complete gate. `.github/workflows/native-release.yml` is the sole publisher and emits exactly two native assets from a clean runtime stage: the portable ZIP and `WinForge-Native-Setup.exe`. Every successful branch push publishes a native-only prerelease; the current `origin/main` tip publishes stable/latest, while an older exact-SHA `main` run publishes stable non-latest. Pull requests validate without publishing. The generated site-data commit explicitly dispatches the gate because GitHub suppresses recursive `GITHUB_TOKEN` push events. · 每次 push 同每個合併去 `main` 嘅 pull request 都會執行完整 gate。`.github/workflows/native-release.yml` 係唯一 publisher，並由乾淨 runtime stage 只發佈兩個原生 asset：可攜 ZIP 同 `WinForge-Native-Setup.exe`。成功 branch push 會發佈 native-only prerelease；目前 `origin/main` tip 會發佈 stable/latest，而較舊 exact-SHA `main` run 會發佈 stable non-latest。Pull request 只驗證，唔發佈。Generated site-data commit 會明確 dispatch 呢個 gate，因為 GitHub 會抑制遞迴 `GITHUB_TOKEN` push event。

| Boundary | Evidence |
| --- | --- |
| Runtime | WinForge.exe exists and has a PE header before packaging. |
| Setup | Exactly one WinForge-Native-Setup.exe exists and has a PE header. |
| Installed app | WinForge.exe, unins000.exe, and THIRD-PARTY-NOTICES.txt exist; .pdb / .ilk do not. |
| Cleanup | Silent uninstaller exits successfully and leaves no guarded install directory. |

**Immutable release provenance · 不可變版本來源：** Every release tag explicitly targets the workflow SHA, so a concurrent `main` update cannot relabel artifacts from the completed run. · 每個 release 標籤都會明確指向 workflow SHA，所以 `main` 同時更新都唔會改標完成咗嘅 run 所產生嘅 artifacts。

Run the policy-only check locally with eng/native/Test-NativeInstallerContract.ps1. The full lifecycle runs in .github/workflows/native-release.yml.

**Proof status · 證明狀態：** Local parser, formatter, actionlint, installer-contract, clean-stage, PE-scan, release-mode, manual CLR/apphost/PDB rejection, and Inno native-PE checks pass. Exact hosted proof for the current branch push and subsequent `main` integration is still pending; the older non-`main` run predates and does not describe this policy. · 本機 parser、formatter、actionlint、installer contract、乾淨 stage、PE scan、release-mode、手動 CLR／apphost／PDB 拒絕同 Inno native-PE 檢查已通過。目前 branch push 同之後 `main` 整合嘅準確 hosted 證明仍待完成；舊 non-`main` run 早於而家政策，唔代表目前行為。
