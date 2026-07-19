# Native C++ Installer CI · 原生 C++ 安裝程式 CI

**EN.** Native release CI validates the installer contract at runtime staging, setup compilation, and post-install payload inspection. The silent smoke test then uninstalls from a guarded LocalAppData location and requires cleanup.

**粵語.** Native release CI 會喺 runtime staging、setup compilation 同 post-install payload inspection 驗證 installer contract。靜默 smoke test 之後會由受保護 LocalAppData 位置解除安裝，並要求清理完成。

Every push and every pull request into `main` runs the full native gate. `.github/workflows/native-release.yml` is the sole publisher and emits exactly two native assets from a clean runtime stage: the portable ZIP and `WinForge-Native-Setup.exe`. Successful branch pushes publish native-only prereleases. The target `main` policy makes the current exact SHA stable/Latest and an older SHA stable non-Latest; corrective hardening explicitly edits the state and fails closed on Latest or asset mismatch. Pull requests validate without publishing. · 每次 push 同每個合併去 `main` 嘅 pull request 都會執行完整原生 gate。`.github/workflows/native-release.yml` 係唯一 publisher，並由乾淨 runtime stage 只發佈可攜 ZIP 同 `WinForge-Native-Setup.exe` 兩個原生 asset。成功 branch push 會發佈 native-only prerelease；`main` 目標政策係目前 exact SHA 做 stable/Latest、較舊 SHA 做 stable non-Latest，修正加固會明確 edit 狀態，Latest 或 asset 唔吻合就 fail closed。Pull request 只驗證，唔發佈。

- Runtime: non-empty PE WinForge.exe.
- Setup: exactly one PE WinForge-Native-Setup.exe.
- Payload: app, uninstaller, notices; no .pdb or .ilk.
- Policy: per-user, x64, deterministic name, static-PCRE2 notice.

**Immutable release provenance · 不可變版本來源：** Every release tag explicitly targets the workflow SHA, so a concurrent `main` update cannot relabel artifacts from the completed run. · 每個 release 標籤都會明確指向 workflow SHA，所以 `main` 同時更新都唔會改標完成咗嘅 run 所產生嘅 artifacts。

**Completed proof · 已完成證明：** `fc2b76e52171e4f81ab1d15f9fb1da5818791171` passed [29673079883](https://github.com/codingmachineedge/WinForge/actions/runs/29673079883) and published exact-SHA prerelease [native-v1.0.43](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.43); merge `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` passed [29673310778](https://github.com/codingmachineedge/WinForge/actions/runs/29673310778) and published stable exact-SHA [native-v1.0.44](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.44). Both have exactly setup + portable ZIP. Independent download audit matched digests: 292 ZIP entries, 48 PEs, zero CLR/apphost/forbidden managed/build artifacts, AMD64 PE32+ app. · Branch 同 `main` exact-SHA 發佈已成功，兩個 release 都準確只含 setup 同 portable ZIP；獨立下載審查確認 digest、292 個 ZIP entry、48 個 PE、零 CLR／apphost／禁止 artifact，同 AMD64 PE32+ app。

**Latest correction · Latest 修正：** `gh release create --latest` left the endpoint at historical managed `v1.0.256`; official manual `gh release edit native-v1.0.44 --latest` repaired it to `native-v1.0.44`. Automatic proof remains pending while the corrective branch adds explicit edit, fail-closed Latest/asset checks, and official Node 24 `checkout@v7`, `upload-artifact@v7`, `download-artifact@v8`, and `setup-msbuild@v3`. Exact corrective run IDs, tags, and SHAs are pending hosted success. · 自動 Latest 有真實缺陷，官方手動 edit 已修復；修正 branch 嘅準確 hosted run／tag／SHA 證明仍待完成。
