# Native C++ Installer CI · 原生 C++ 安裝程式 CI

**EN.** Native release CI validates the installer contract at runtime staging, setup compilation, and post-install payload inspection. The silent smoke test then uninstalls from a guarded LocalAppData location and requires cleanup.

**粵語.** Native release CI 會喺 runtime staging、setup compilation 同 post-install payload inspection 驗證 installer contract。靜默 smoke test 之後會由受保護 LocalAppData 位置解除安裝，並要求清理完成。

Every push and every pull request into `main` runs the full native gate. `.github/workflows/native-release.yml` is the sole publisher and emits exactly the portable ZIP plus `WinForge-Native-Setup.exe`. Branch pushes publish native-only prereleases. For fresh current main, the amended target explicitly applies Latest + stable + non-draft and verifies exact SHA, stable native metadata, and exactly two assets through `/releases/latest`. Every release run enforces this invariant; noncurrent runs restore a verified current-main stable native candidate when Latest is managed or invalid. Pull requests validate without publishing. · 每次 push 同每個合併去 `main` 嘅 pull request 都會執行完整原生 gate。`.github/workflows/native-release.yml` 係唯一 publisher，只發佈可攜 ZIP 同 `WinForge-Native-Setup.exe`。對最新 current main，修正版目標明確套用 Latest + stable + non-draft，再經 `/releases/latest` 驗證準確 SHA、stable 原生 metadata 同兩個 asset；每個 release run 都要守住 invariant，noncurrent run 見到 managed 或無效 Latest 就恢復已核實 current-main stable 原生 candidate。

- Runtime: non-empty PE WinForge.exe.
- Setup: exactly one PE WinForge-Native-Setup.exe.
- Payload: app, uninstaller, notices; no .pdb or .ilk.
- Policy: per-user, x64, deterministic name, static-PCRE2 notice.

**Immutable release provenance · 不可變版本來源：** Every release tag explicitly targets the workflow SHA, so a concurrent `main` update cannot relabel artifacts from the completed run. · 每個 release 標籤都會明確指向 workflow SHA，所以 `main` 同時更新都唔會改標完成咗嘅 run 所產生嘅 artifacts。

**Completed proof · 已完成證明：** `fc2b76e52171e4f81ab1d15f9fb1da5818791171` passed [29673079883](https://github.com/codingmachineedge/WinForge/actions/runs/29673079883) and published exact-SHA prerelease [native-v1.0.43](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.43); merge `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` passed [29673310778](https://github.com/codingmachineedge/WinForge/actions/runs/29673310778) and published stable exact-SHA [native-v1.0.44](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.44). Both have exactly setup + portable ZIP. Independent download audit matched digests: 292 ZIP entries, 48 PEs, zero CLR/apphost/forbidden managed/build artifacts, AMD64 PE32+ app. · Branch 同 `main` exact-SHA 發佈已成功，兩個 release 都準確只含 setup 同 portable ZIP；獨立下載審查確認 digest、292 個 ZIP entry、48 個 PE、零 CLR／apphost／禁止 artifact，同 AMD64 PE32+ app。

**Latest correction · Latest 修正：** `gh release create --latest` left managed `v1.0.256` as Latest. A later bare `gh release edit native-v1.0.44 --latest` was insufficient: release 44 was observed as `prerelease=true`, so Latest fell back to managed. Explicit `gh release edit native-v1.0.44 --latest --prerelease=false --draft=false` restored stable/non-draft `native-v1.0.44` and exact `/releases/latest` at `f7a9eec44aeffdf829f5c07f5eeb364f08a7677f` with two assets. · Bare edit 令 release 44 變成 prerelease，Latest 跌返 managed；明確 stable／draft flags 先修復 exact native Latest。

`53da21446a5c2cded97e1387f3e36f557770a3c5` passed [29673965179](https://github.com/codingmachineedge/WinForge/actions/runs/29673965179) and published [native-v1.0.47](https://github.com/codingmachineedge/WinForge/releases/tag/native-v1.0.47) with official Node 24 actions and two native assets. Its old noncurrent check passed despite managed Latest, so the automatic invariant remains pending exact amended branch/main run IDs, tags, and SHAs. · 呢個只證明 Node 24／two-asset branch 發佈；修正版自動 invariant 嘅準確 hosted 證明仍待完成。
