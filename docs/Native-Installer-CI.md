# Native-only C++ Release and Installer CI · 只限原生 C++ 發佈同安裝程式 CI

**EN.** `.github/workflows/native-release.yml` is the repository's sole GitHub Release publisher. It builds and tests the C++20/C++/WinRT application on Windows, creates one clean runtime staging directory, packages `installer/WinForge.Native.iss`, silently installs/uninstalls the result under a guarded per-user LocalAppData path, and publishes only the native portable ZIP and native installer after every gate passes. The former managed publisher `.github/workflows/release.yml` is removed.

**粵語.** `.github/workflows/native-release.yml` 係 repository 唯一 GitHub Release publisher。佢會喺 Windows 上 build 同測試 C++20／C++/WinRT app、建立一個乾淨 runtime staging directory、封裝 `installer/WinForge.Native.iss`、喺受保護嘅每用戶 LocalAppData 路徑靜默安裝／解除安裝，再喺所有 gate 通過後只發佈原生 portable ZIP 同原生 installer。舊 managed publisher `.github/workflows/release.yml` 已移除。

## Release behavior on every push · 每次 push 嘅發佈行為

- Every push runs the complete native build/test/parity/package/installer gate without path filtering. A successful branch push publishes a uniquely tagged **prerelease** for that exact workflow SHA. · 每次 push 都會執行完整原生 gate；成功嘅 branch push 會對準確 workflow SHA 發佈唯一 tag 嘅 **prerelease**。
- The target policy makes the current `origin/main` tip stable and **Latest**, while an older exact-SHA `main` run is stable but non-Latest. Corrective hardening now explicitly edits the release to the selected state and then fails closed unless the exact asset set and `/releases/latest` endpoint match. Hosted proof of that automatic postcondition is still pending. · 目標政策會將目前 `origin/main` tip 設為 stable 同 **Latest**，較舊 exact-SHA `main` run 則係 stable 但非 Latest。修正加固而家會明確 edit release 去所選狀態，之後除非準確 asset 集合同 `/releases/latest` endpoint 都吻合，否則 fail closed；呢個自動 postcondition 嘅 hosted 證明仍待完成。
- Pull requests run the same read-only quality gate but do not publish. Manual dispatch can test without publishing or explicitly publish the exact selected SHA. The site-data workflow dispatches the native workflow with its source SHA after a generated `main` commit. · Pull request 會執行同樣嘅只讀 gate 但不發佈；manual dispatch 可只測試，或明確發佈所選 SHA。Site-data workflow 在生成 `main` commit 後會帶 source SHA dispatch 原生 workflow。

## Native-only payload contract · 只限原生 payload 合約

The portable ZIP and `WinForge-Native-Setup.exe` are built from the same clean runtime staging directory, and exactly those **two assets** are transferred into the release job. The staging, ZIP, installer, and installed tree must not contain a managed WinForge payload, the managed launcher/updater, updater runtime, `.deps.json`, `.runtimeconfig.json`, `coreclr`, `hostfxr`, `hostpolicy`, PDB/ILK/LIB/EXP build artifacts, or apphost/CLR-bearing WinForge binaries. Every staged `.exe` and `.dll` is parsed as a PE and must have a zero CLR header and no managed-apphost markers; `WinForge.exe` must be AMD64 PE32+.

Portable ZIP 同 `WinForge-Native-Setup.exe` 由同一個乾淨 runtime staging directory 產生，而且只有呢 **兩個 asset** 會傳去 release job。Stage、ZIP、installer 同安裝後目錄都不可包含 managed WinForge payload、managed launcher／updater、updater runtime、`.deps.json`、`.runtimeconfig.json`、`coreclr`、`hostfxr`、`hostpolicy`、PDB／ILK／LIB／EXP build artifact，或帶 managed apphost／CLR 嘅 WinForge binary。每個 staged `.exe` 同 `.dll` 都會以 PE 解析，要求 CLR header 為零且無 managed-apphost marker；`WinForge.exe` 必須係 AMD64 PE32+。

## Permission and provenance boundary · 權限同來源邊界

- Workflow-level and `build-test-package` permissions are `contents: read`; only the separate `release-native` job receives `contents: write`, and only for eligible push/manual release modes.
- Release creation pins the tag target to the workflow SHA, verifies that the resulting tag resolves to that same SHA, and chooses prerelease/stable/Latest state from the trigger plus exact remote-main comparison. The corrective path explicitly applies that state and verifies the release metadata, exact two-asset set, and Latest endpoint before succeeding.
- Runtime staging is reused for the ZIP and installer so the two deliverables cannot silently diverge. Release-job downloads are limited to the two named native artifacts.
- Corrective CI uses the official Node 24 action lines: `actions/checkout@v7`, `actions/upload-artifact@v7`, `actions/download-artifact@v8`, and `microsoft/setup-msbuild@v3`.

- Workflow 同 `build-test-package` 只有 `contents: read`；只有獨立 `release-native` job 喺合資格 push／manual 發佈模式才有 `contents: write`。
- Release tag 明確指向 workflow SHA，建立後再核對 tag 仍解析到同一 SHA；prerelease／stable／Latest 由 trigger 同 remote-main exact comparison 決定。修正路徑會明確套用該狀態，並喺成功前核對 release metadata、準確兩個 asset 同 Latest endpoint。
- ZIP 同 installer 共用同一 runtime stage，兩份交付品不會靜默分叉；release job 只會下載兩個指定原生 artifact。
- 修正 CI 使用官方 Node 24 action line：`actions/checkout@v7`、`actions/upload-artifact@v7`、`actions/download-artifact@v8` 同 `microsoft/setup-msbuild@v3`。

## CI sequence · CI 次序

1. Restore native dependencies, build the Release C++ solution, run the native core tests and catalog parity.
2. Stage one clean runtime, scan every PE and reject managed/runtime/build artifacts.
3. Validate the staged installer contract and create the portable ZIP from that same directory.
4. Compile exactly one Inno Setup executable and validate its PE and embedded payload policy.
5. Silent-install, validate the installed payload, silent-uninstall, and prove the guarded install root is gone.
6. Upload only the portable ZIP and installer as inter-job artifacts.
7. On every eligible successful push/manual publish, create the exact-SHA native release with exactly those two assets, explicitly apply the intended prerelease/stable/Latest state, then fail closed unless tag, assets, metadata, and Latest endpoint satisfy the selected mode.

## Local check · 本機檢查

Run the static publisher/installer/payload policy check without building:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng\native\Test-NativeInstallerContract.ps1
~~~

After a Release build, pass `-PublishDir`; after Inno Setup, pass `-InstallerPath`; after a disposable silent-install test, pass `-InstallDir`. The durable negative fixtures cover competing release publishers. CLR DLL, .NET apphost, and packaged-PDB rejection were exercised as manual local validation of the workflow and payload checks; they are not contract-owned fixtures.

## Remote legacy cleanup and hosted proof · 遙距舊 workflow 清理同 hosted 證明

Legacy managed release workflow ID `301226619` is remotely `disabled_manually`. Exactly **28** obsolete failed/cancelled workflow run records that had produced no release were deleted after a full-pagination audit; successful workflow history and all existing releases were retained. This cleanup prevents the obsolete publisher from being run while preserving useful provenance. · 舊 managed release workflow ID `301226619` 已遠距設為 `disabled_manually`。完整分頁審查後，準確刪除 **28** 條沒有產生 release 嘅過時失敗／取消 run record；成功 workflow 歷史同所有既有 release 都保留。

Text-analysis feature commit `fc2b76e52171e4f81ab1d15f9fb1da5818791171` completed hosted branch run [29673079883](https://github.com/codingmachineedge/WinForge/actions/runs/29673079883), which published exact-SHA prerelease [native-v1.0.43](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.43) with exactly the native setup executable and portable ZIP. Merge commit `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` completed hosted `main` run [29673310778](https://github.com/codingmachineedge/WinForge/actions/runs/29673310778), which published stable exact-SHA [native-v1.0.44](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.44) with exactly those two native assets. · 文字分析功能 commit `fc2b76e52171e4f81ab1d15f9fb1da5818791171` 嘅 branch run [29673079883](https://github.com/codingmachineedge/WinForge/actions/runs/29673079883) 已成功，並發佈 exact-SHA prerelease [native-v1.0.43](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.43)，準確只含原生 setup 同 portable ZIP。Merge commit `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` 嘅 `main` run [29673310778](https://github.com/codingmachineedge/WinForge/actions/runs/29673310778) 亦成功，並發佈 stable exact-SHA [native-v1.0.44](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.44)，同樣準確只含兩個原生 asset。

An independent download audit of `native-v1.0.44` matched the published digests and found **292 ZIP entries**, **48 PE files**, zero CLR headers or managed-apphost markers, zero forbidden managed runtime/build artifacts, and an AMD64 PE32+ `WinForge.exe`. This proves the text-analysis branch/main release payloads are native-only. · 獨立下載審查 `native-v1.0.44` 後，digest 同發佈值完全吻合；ZIP 有 **292 個 entry**、**48 個 PE file**，CLR header、managed-apphost marker 同禁止嘅 managed runtime／build artifact 全部係零，而 `WinForge.exe` 係 AMD64 PE32+。呢項證據確認文字分析 branch／main release payload 只含原生版。

The hosted run also exposed a real Latest-channel defect: `gh release create --latest` created the correct stable native release but did **not** move `/releases/latest` from historical managed `v1.0.256`. The official manual repair `gh release edit native-v1.0.44 --latest` moved the endpoint, and exact verification then resolved `/releases/latest` to `native-v1.0.44`. Therefore runs 29673079883 and 29673310778 prove exact-SHA publication and native-only assets, but they do not prove the automatic Latest postcondition. The corrective branch is adding explicit release edit plus fail-closed Latest/asset verification and the official Node 24 action versions listed above. Exact corrective branch/main run IDs, tags, and SHAs remain pending and must be recorded only after hosted success. · Hosted run 同時揭露真實 Latest-channel 缺陷：`gh release create --latest` 雖然建立咗正確 stable 原生 release，但**冇**將 `/releases/latest` 由歷史 managed `v1.0.256` 移走。用官方命令 `gh release edit native-v1.0.44 --latest` 手動修復後，準確核對確認 `/releases/latest` 已解析到 `native-v1.0.44`。所以 run 29673079883 同 29673310778 證明 exact-SHA 發佈同 native-only asset，但未證明自動 Latest postcondition。修正 branch 正加入明確 release edit、fail-closed Latest／asset 驗證，同上列官方 Node 24 action 版本；準確修正 branch／main run ID、tag 同 SHA 仍待 hosted 成功後先可記錄。

## Visual evidence · 視覺證據

This is packaging and CI work, not a changed app page. No UI screenshot is claimed or replaced; installer behavior is covered by executable contract, silent lifecycle, and payload-cleanup checks.
