# Native C++ Installer CI · 原生 C++ 安裝程式 CI

The Pages documentation mirrors the native installer contract used by .github/workflows/native-release.yml.

**EN.** CI verifies the staged runtime, the compiled Inno Setup executable, and the installed payload. It enforces a per-user x64 installer, checks PE files and third-party notices, rejects debug artifacts, silently uninstalls, and proves the guarded LocalAppData directory is removed.

**粵語.** CI 會驗證 stage runtime、編譯好嘅 Inno Setup executable 同已安裝 payload。佢強制每用戶 x64 installer、檢查 PE files 同 third-party notices、拒絕 debug artifacts、靜默解除安裝，並確認受保護 LocalAppData directory 已移除。

The reusable implementation is eng/native/Test-NativeInstallerContract.ps1; its three CI invocations make the packaging contract auditable from source through uninstall cleanup.

`.github/workflows/native-release.yml` is the sole release publisher and emits exactly two native assets: the portable ZIP and `WinForge-Native-Setup.exe`. Successful branch pushes publish native-only prereleases. The target `main` policy makes the current exact SHA stable/Latest and older SHAs stable non-Latest; corrective hardening explicitly edits the state and fails closed on Latest or asset mismatch. Pull requests validate without publishing. · `.github/workflows/native-release.yml` 係唯一 release publisher，並只發佈可攜 ZIP 同 `WinForge-Native-Setup.exe` 兩個原生 asset。成功 branch push 會發佈 native-only prerelease；`main` 目標政策係目前 exact SHA 做 stable/Latest、較舊 SHA 做 stable non-Latest，修正加固會明確 edit 狀態，Latest 或 asset 唔吻合就 fail closed。Pull request 只驗證，唔發佈。

Text-analysis feature `fc2b76e52171e4f81ab1d15f9fb1da5818791171` passed branch run [29673079883](https://github.com/codingmachineedge/WinForge/actions/runs/29673079883) and published exact-SHA prerelease [native-v1.0.43](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.43). Merge `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` passed `main` run [29673310778](https://github.com/codingmachineedge/WinForge/actions/runs/29673310778) and published stable exact-SHA [native-v1.0.44](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.44). Both releases contain exactly setup + portable ZIP; an independent download audit matched digests and found 292 ZIP entries, 48 PEs, zero CLR/apphost/forbidden managed/build artifacts, and AMD64 PE32+ `WinForge.exe`.

`gh release create --latest` did not move `/releases/latest` from historical managed `v1.0.256`; official manual `gh release edit native-v1.0.44 --latest` repaired the endpoint to `native-v1.0.44`. Automatic Latest proof remains pending while the corrective branch adds explicit edit, fail-closed Latest/asset verification, and official Node 24 `checkout@v7`, `upload-artifact@v7`, `download-artifact@v8`, and `setup-msbuild@v3`. Exact corrective branch/main run IDs, tags, and SHAs must be added after hosted success. · Exact-SHA native-only 發佈同獨立 payload 審查已完成，但自動 Latest postcondition 有真實缺陷；官方手動 edit 已修復，修正 branch 嘅準確 hosted 證明仍待完成。
