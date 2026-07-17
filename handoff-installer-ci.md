# Native C++ Installer CI task memory · 原生 C++ 安裝程式 CI 任務記憶

Verified integration record for the native installer contract task.

- Added eng/native/Test-NativeInstallerContract.ps1, a reusable static/runtime/install payload verifier.
- Wired it into .github/workflows/native-release.yml before packaging, after Inno Setup compilation, and during the silent install smoke.
- The workflow now rejects ambiguous multiple setup executables and validates the exact compiled setup executable rather than selecting an arbitrary first result.
- Local static contract validation passed. Inno Setup is intentionally installed by the hosted Windows 2022 CI workflow; no visual app page changed.

- Git integration: task commit b5cae63dd53e1892aca61e039597d1f3b9a6b73c; merge commit 1c3c9a1a. After fetch, the task commit, the pushed feature-branch tip, and the merge commit were proven ancestors of origin/main, with the workflow, verifier, docs, Pages mirrors, generated site data, and handoff files present in the remote main tree.

整合已驗證：任務提交 b5cae63dd53e1892aca61e039597d1f3b9a6b73c 合併為 1c3c9a1a；fetch 後任務、已推送 branch tip 同合併提交都係 origin/main ancestor，workflow、verifier、docs、Pages mirror、site data 同 handoff files 都已確認喺 remote main。

原生安裝程式合約 task 已加入可重用 verifier，同埋喺 runtime、編譯後 setup、已安裝 payload 三個位置驗證；本機 static contract 已通過。完整 Inno Setup lifecycle 由 Windows 2022 CI 執行，冇改任何 app visual page。
