# Native C++ Installer CI · 原生 C++ 安裝程式 CI

**EN.** The native release workflow on Windows 2022 builds the C++20/WinUI 3 runtime, packages installer/WinForge.Native.iss, silently installs it under a guarded per-user LocalAppData path, and silently uninstalls it again. The reusable eng/native/Test-NativeInstallerContract.ps1 gate now makes those guarantees explicit.

**粵語.** Windows 2022 上嘅 native release workflow 會 build C++20/WinUI 3 runtime、封裝 installer/WinForge.Native.iss、喺受保護嘅每用戶 LocalAppData 路徑靜默安裝，之後再靜默解除安裝。可重用嘅 eng/native/Test-NativeInstallerContract.ps1 gate 而家會明確驗證呢啲保證。

## Contract boundaries · 合約邊界

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

## Local check · 本機檢查

Run the static policy check without building:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng\native\Test-NativeInstallerContract.ps1
~~~

After a Release build, pass -PublishDir; after Inno Setup, pass -InstallerPath; after a disposable silent-install test, pass -InstallDir.

## Visual evidence · 視覺證據

This is packaging and CI work, not a changed app page. No UI screenshot is claimed or replaced; the installer’s silent lifecycle is covered by executable contract and cleanup checks.
