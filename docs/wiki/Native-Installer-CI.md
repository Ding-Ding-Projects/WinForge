# Native C++ Installer CI · 原生 C++ 安裝程式 CI

The native release workflow now calls the reusable installer contract gate before packaging, after Inno Setup builds WinForge-Native-Setup.exe, and after the silent installer writes its payload.

**EN.** The gate verifies the per-user/x64 Inno policy, WinForge.exe PE payload, explicit third-party notice, absence of packaged debug symbols, exact setup filename, silent uninstallation, and guarded LocalAppData cleanup.

**粵語.** 呢個 gate 會驗證每用戶/x64 Inno 政策、WinForge.exe PE payload、第三方 notices、冇 package 到 debug symbols、setup 檔名正確、靜默解除安裝，同埋受保護 LocalAppData 清理完成。

Every push and every pull request into `main` runs this complete gate. `.github/workflows/native-release.yml` is the sole publisher and emits exactly two native assets from a clean runtime stage: the portable ZIP and `WinForge-Native-Setup.exe`. Every successful branch push publishes a native-only prerelease. The target `main` policy is stable/Latest for the current exact SHA and stable non-Latest for an older SHA; corrective hardening explicitly edits the release and fails closed on Latest or asset mismatch. Pull requests validate without publishing. · 每次 push 同每個合併去 `main` 嘅 pull request 都會執行完整 gate。`.github/workflows/native-release.yml` 係唯一 publisher，並由乾淨 runtime stage 只發佈可攜 ZIP 同 `WinForge-Native-Setup.exe` 兩個原生 asset。成功 branch push 會發佈 native-only prerelease；`main` 目標政策係目前 exact SHA 做 stable/Latest、較舊 SHA 做 stable non-Latest，修正加固會明確 edit release，Latest 或 asset 唔吻合就 fail closed。Pull request 只驗證，唔發佈。

| Boundary | Evidence |
| --- | --- |
| Runtime | WinForge.exe exists and has a PE header before packaging. |
| Setup | Exactly one WinForge-Native-Setup.exe exists and has a PE header. |
| Installed app | WinForge.exe, unins000.exe, and THIRD-PARTY-NOTICES.txt exist; .pdb / .ilk do not. |
| Cleanup | Silent uninstaller exits successfully and leaves no guarded install directory. |

**Immutable release provenance · 不可變版本來源：** Every release tag explicitly targets the workflow SHA, so a concurrent `main` update cannot relabel artifacts from the completed run. · 每個 release 標籤都會明確指向 workflow SHA，所以 `main` 同時更新都唔會改標完成咗嘅 run 所產生嘅 artifacts。

Run the policy-only check locally with eng/native/Test-NativeInstallerContract.ps1. The full lifecycle runs in .github/workflows/native-release.yml.

**Completed hosted proof · 已完成 hosted 證明：** Feature `fc2b76e52171e4f81ab1d15f9fb1da5818791171` passed [branch run 29673079883](https://github.com/codingmachineedge/WinForge/actions/runs/29673079883) and published exact-SHA prerelease [native-v1.0.43](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.43). Merge `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` passed [`main` run 29673310778](https://github.com/codingmachineedge/WinForge/actions/runs/29673310778) and published stable exact-SHA [native-v1.0.44](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.44). Both releases contain exactly the native setup executable and portable ZIP. Independent download verification matched digests and found 292 ZIP entries, 48 PEs, zero CLR/apphost or forbidden managed/build artifacts, and AMD64 PE32+ `WinForge.exe`. · 功能 `fc2b76e52171e4f81ab1d15f9fb1da5818791171` 通過 [branch run 29673079883](https://github.com/codingmachineedge/WinForge/actions/runs/29673079883)，發佈 exact-SHA prerelease [native-v1.0.43](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.43)；merge `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` 通過 [`main` run 29673310778](https://github.com/codingmachineedge/WinForge/actions/runs/29673310778)，發佈 stable exact-SHA [native-v1.0.44](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.44)。兩個 release 都準確只含原生 setup 同 portable ZIP；獨立下載驗證確認 digest 吻合、292 個 ZIP entry、48 個 PE、零 CLR／apphost／禁止 managed 或 build artifact，同 AMD64 PE32+ `WinForge.exe`。

**Latest correction and pending proof · Latest 修正同待完成證明：** `gh release create --latest` left `/releases/latest` at historical managed `v1.0.256`. Official manual `gh release edit native-v1.0.44 --latest` repaired the endpoint to `native-v1.0.44`. Automatic Latest proof is therefore still pending while the corrective branch adds explicit edit, fail-closed Latest/asset verification, and official Node 24 `checkout@v7`, `upload-artifact@v7`, `download-artifact@v8`, and `setup-msbuild@v3`. Exact corrective branch/main run IDs, tags, and SHAs must be recorded after hosted success. · `gh release create --latest` 將 `/releases/latest` 留喺歷史 managed `v1.0.256`；官方手動 `gh release edit native-v1.0.44 --latest` 已修復 endpoint 去 `native-v1.0.44`。自動 Latest 證明仍待完成；修正 branch 正加入明確 edit、fail-closed Latest／asset 驗證，同官方 Node 24 actions。準確修正 branch／main run ID、tag 同 SHA 要等 hosted 成功後先記錄。
