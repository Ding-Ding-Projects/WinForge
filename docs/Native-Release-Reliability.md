# Native Release Reliability · 原生發佈可靠性

`native-release.yml` is the sole GitHub Release publisher. It publishes only the tested C++/WinRT installer (`WinForge-Native-Setup.exe`) and matching native portable ZIP; the native installer contract rejects managed runtime payloads and any other workflow publisher.

`.github/workflows/native-release.yml` 係唯一 GitHub Release publisher。佢只會發佈已測試嘅 C++/WinRT 安裝程式（`WinForge-Native-Setup.exe`）同相配嘅原生 portable ZIP；native installer contract 會拒絕 managed runtime payload 同任何其他 workflow publisher。

## Delivery rules · 發佈規則

- A release job runs only after its native build/test/package job succeeds. Pushes and opted-in dispatches both use the same C++-only path.
- Dispatch retries use an immutable `release_version`, and a generated-data dispatch uses `1.0.$GITHUB_RUN_ID`, so a retried request reconciles one tag instead of creating a second release.
- Release creation is attempted once. If GitHub returns an ambiguous error, the workflow waits for that exact tag to become visible, then validates its SHA, draft/prerelease state, and exactly two native assets. Idempotent edit/upload readbacks have bounded retries.
- A run whose source is no longer `main` verifies its own immutable release and exits non-Latest. Only the source-specific current-main run can promote and verify Latest.
- The site-data queue is not canceled mid-run: after it pushes generated data, it retries its explicit native release dispatch so GitHub-token suppression cannot leave that generated-data commit without delivery.

- 發佈 job 一定要 native build/test/package 成功先會跑；push 同明確 dispatch 都行同一條只限 C++ 路徑。
- Dispatch retry 用不可變 `release_version`；生成資料 dispatch 用 `1.0.$GITHUB_RUN_ID`，所以重試只會對同一個 tag 做 reconciliation，唔會整第二個 release。
- 建立 release 只試一次。如果 GitHub 回覆含糊錯誤，workflow 會等確切 tag 可見，再驗 SHA、draft/prerelease 狀態同剛好兩個原生 asset；可重複嘅 edit/upload/readback 有有限 retry。
- 已經唔係 `main` 嘅 source run 只驗證自己嘅不可變 release，然後 non-Latest 成功結束；只有目前 main source run 可以升級同驗證 Latest。
- site-data queue 唔會中途取消：生成資料 push 後會 retry 明確 native dispatch，避免 GitHub-token suppression 令嗰個資料 commit 冇發佈。

## Current verification · 目前驗證

Local YAML parsing, every workflow PowerShell block, and `eng/native/Test-NativeInstallerContract.ps1` pass. Two earlier hosted native release attempts failed after their native test/package gates during a GitHub API outage; their remote release repair remains pending until the hardened workflow is pushed and GitHub accepts the retries. No managed release publisher is introduced.

本機 YAML、每個 workflow PowerShell block 同 `eng/native/Test-NativeInstallerContract.ps1` 都通過。兩次較早 hosted native release 嘗試喺 native test/package gate 成功後遇到 GitHub API outage 而失敗；要等加固 workflow 推上去同 GitHub 接受 retry 先會完成遙距 repair。冇加入 managed release publisher。
