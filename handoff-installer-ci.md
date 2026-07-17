# Native C++ Installer CI task memory · 原生 C++ 安裝程式 CI 任務記憶

Pending integration record for the native installer contract task.

- Added eng/native/Test-NativeInstallerContract.ps1, a reusable static/runtime/install payload verifier.
- Wired it into .github/workflows/native-release.yml before packaging, after Inno Setup compilation, and during the silent install smoke.
- The workflow now rejects ambiguous multiple setup executables and validates the exact compiled setup executable rather than selecting an arbitrary first result.
- Local static contract validation passed. Inno Setup is intentionally installed by the hosted Windows 2022 CI workflow; no visual app page changed.

原生安裝程式合約 task 已加入可重用 verifier，同埋喺 runtime、編譯後 setup、已安裝 payload 三個位置驗證；本機 static contract 已通過。完整 Inno Setup lifecycle 由 Windows 2022 CI 執行，冇改任何 app visual page。
