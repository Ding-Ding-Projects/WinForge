# Native-only C++ Release and Installer CI · 只限原生 C++ 發佈同安裝程式 CI

**EN.** `.github/workflows/native-release.yml` is the repository's sole GitHub Release publisher. It builds and tests the C++20/C++/WinRT application on Windows, creates one clean runtime staging directory, packages `installer/WinForge.Native.iss`, silently installs/uninstalls the result under a guarded per-user LocalAppData path, and publishes only the native portable ZIP and native installer after every gate passes. The former managed publisher `.github/workflows/release.yml` is removed.

**粵語.** `.github/workflows/native-release.yml` 係 repository 唯一 GitHub Release publisher。佢會喺 Windows 上 build 同測試 C++20／C++/WinRT app、建立一個乾淨 runtime staging directory、封裝 `installer/WinForge.Native.iss`、喺受保護嘅每用戶 LocalAppData 路徑靜默安裝／解除安裝，再喺所有 gate 通過後只發佈原生 portable ZIP 同原生 installer。舊 managed publisher `.github/workflows/release.yml` 已移除。

## Release behavior on every push · 每次 push 嘅發佈行為

- Every push runs the complete native build/test/parity/package/installer gate without path filtering. A successful branch push publishes a uniquely tagged **prerelease** for that exact workflow SHA. · 每次 push 都會執行完整原生 gate；成功嘅 branch push 會對準確 workflow SHA 發佈唯一 tag 嘅 **prerelease**。
- A successful run for the current `origin/main` tip publishes a stable release and makes it **Latest**. If an older `main` run finishes later, exact-SHA comparison makes it stable but explicitly non-Latest, so it cannot steal the channel from the newer tip. · 目前 `origin/main` tip 成功後會發 stable 版並設為 **Latest**；如果較舊 `main` run 之後先完成，exact-SHA 檢查會將佢設為非 Latest，不會搶走新 tip 嘅 channel。
- Pull requests run the same read-only quality gate but do not publish. Manual dispatch can test without publishing or explicitly publish the exact selected SHA. The site-data workflow dispatches the native workflow with its source SHA after a generated `main` commit. · Pull request 會執行同樣嘅只讀 gate 但不發佈；manual dispatch 可只測試，或明確發佈所選 SHA。Site-data workflow 在生成 `main` commit 後會帶 source SHA dispatch 原生 workflow。

## Native-only payload contract · 只限原生 payload 合約

The portable ZIP and `WinForge-Native-Setup.exe` are built from the same clean runtime staging directory, and exactly those **two assets** are transferred into the release job. The staging, ZIP, installer, and installed tree must not contain a managed WinForge payload, the managed launcher/updater, updater runtime, `.deps.json`, `.runtimeconfig.json`, `coreclr`, `hostfxr`, `hostpolicy`, PDB/ILK/LIB/EXP build artifacts, or apphost/CLR-bearing WinForge binaries. Every staged `.exe` and `.dll` is parsed as a PE and must have a zero CLR header and no managed-apphost markers; `WinForge.exe` must be AMD64 PE32+.

Portable ZIP 同 `WinForge-Native-Setup.exe` 由同一個乾淨 runtime staging directory 產生，而且只有呢 **兩個 asset** 會傳去 release job。Stage、ZIP、installer 同安裝後目錄都不可包含 managed WinForge payload、managed launcher／updater、updater runtime、`.deps.json`、`.runtimeconfig.json`、`coreclr`、`hostfxr`、`hostpolicy`、PDB／ILK／LIB／EXP build artifact，或帶 managed apphost／CLR 嘅 WinForge binary。每個 staged `.exe` 同 `.dll` 都會以 PE 解析，要求 CLR header 為零且無 managed-apphost marker；`WinForge.exe` 必須係 AMD64 PE32+。

## Permission and provenance boundary · 權限同來源邊界

- Workflow-level and `build-test-package` permissions are `contents: read`; only the separate `release-native` job receives `contents: write`, and only for eligible push/manual release modes.
- Release creation pins the tag target to the workflow SHA, verifies that the resulting tag resolves to that same SHA, and chooses prerelease/stable/Latest state from the trigger plus exact remote-main comparison.
- Runtime staging is reused for the ZIP and installer so the two deliverables cannot silently diverge. Release-job downloads are limited to the two named native artifacts.

- Workflow 同 `build-test-package` 只有 `contents: read`；只有獨立 `release-native` job 喺合資格 push／manual 發佈模式才有 `contents: write`。
- Release tag 明確指向 workflow SHA，建立後再核對 tag 仍解析到同一 SHA；prerelease／stable／Latest 由 trigger 同 remote-main exact comparison 決定。
- ZIP 同 installer 共用同一 runtime stage，兩份交付品不會靜默分叉；release job 只會下載兩個指定原生 artifact。

## CI sequence · CI 次序

1. Restore native dependencies, build the Release C++ solution, run the native core tests and catalog parity.
2. Stage one clean runtime, scan every PE and reject managed/runtime/build artifacts.
3. Validate the staged installer contract and create the portable ZIP from that same directory.
4. Compile exactly one Inno Setup executable and validate its PE and embedded payload policy.
5. Silent-install, validate the installed payload, silent-uninstall, and prove the guarded install root is gone.
6. Upload only the portable ZIP and installer as inter-job artifacts.
7. On every eligible successful push/manual publish, create the exact-SHA native release in the correct prerelease/stable/Latest mode with exactly those two assets.

## Local check · 本機檢查

Run the static publisher/installer/payload policy check without building:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng\native\Test-NativeInstallerContract.ps1
~~~

After a Release build, pass `-PublishDir`; after Inno Setup, pass `-InstallerPath`; after a disposable silent-install test, pass `-InstallDir`. The durable negative fixtures cover competing release publishers. CLR DLL, .NET apphost, and packaged-PDB rejection were exercised as manual local validation of the workflow and payload checks; they are not contract-owned fixtures.

## Remote legacy cleanup and hosted proof · 遙距舊 workflow 清理同 hosted 證明

Legacy managed release workflow ID `301226619` is remotely `disabled_manually`. Exactly **28** obsolete failed/cancelled workflow run records that had produced no release were deleted after a full-pagination audit; successful workflow history and all existing releases were retained. This cleanup prevents the obsolete publisher from being run while preserving useful provenance. · 舊 managed release workflow ID `301226619` 已遠距設為 `disabled_manually`。完整分頁審查後，準確刪除 **28** 條沒有產生 release 嘅過時失敗／取消 run record；成功 workflow 歷史同所有既有 release 都保留。

The revised workflow's local PowerShell parser, Prettier YAML parser, actionlint 1.7.12, installer contract, **292-file** clean-stage simulation, all-**48-PE** scan, release-mode matrix, manual CLR/apphost/PDB rejection checks, and existing Inno native-PE check are green. Exact hosted proof for the current text-analysis branch push and subsequent `main` integration is still pending and must not be inferred from older runs. · 新 workflow 嘅本機 PowerShell parser、Prettier YAML parser、actionlint 1.7.12、installer contract、**292-file** 乾淨 stage 模擬、全部 **48 個 PE** scan、release-mode matrix、手動 CLR／apphost／PDB 拒絕檢查同既有 Inno native-PE 檢查已通過。目前文字分析 branch push 同之後 `main` 整合嘅準確 hosted 證明仍待完成，不可用舊 run 推斷。

## Visual evidence · 視覺證據

This is packaging and CI work, not a changed app page. No UI screenshot is claimed or replaced; installer behavior is covered by executable contract, silent lifecycle, and payload-cleanup checks.
