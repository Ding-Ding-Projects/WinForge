# Native C++ Installer CI · 原生 C++ 安裝程式 CI

**EN.** Native release CI validates the installer contract at runtime staging, setup compilation, and post-install payload inspection. The silent smoke test then uninstalls from a guarded LocalAppData location and requires cleanup.

**粵語.** Native release CI 會喺 runtime staging、setup compilation 同 post-install payload inspection 驗證 installer contract。靜默 smoke test 之後會由受保護 LocalAppData 位置解除安裝，並要求清理完成。

Every push and every pull request into `main` runs the full native gate. `.github/workflows/native-release.yml` is the sole publisher and emits exactly two native assets from a clean runtime stage: the portable ZIP and `WinForge-Native-Setup.exe`. Every successful branch push publishes a native-only prerelease; the current `origin/main` tip publishes stable/latest, while an older exact-SHA `main` run publishes stable non-latest. Pull requests validate without publishing. The generated site-data commit explicitly dispatches the gate because GitHub suppresses recursive `GITHUB_TOKEN` push events. · 每次 push 同每個合併去 `main` 嘅 pull request 都會執行完整原生 gate。`.github/workflows/native-release.yml` 係唯一 publisher，並由乾淨 runtime stage 只發佈兩個原生 asset：可攜 ZIP 同 `WinForge-Native-Setup.exe`。成功 branch push 會發佈 native-only prerelease；目前 `origin/main` tip 會發佈 stable/latest，而較舊 exact-SHA `main` run 會發佈 stable non-latest。Pull request 只驗證，唔發佈。Generated site-data commit 會明確 dispatch 呢個 gate，因為 GitHub 會抑制遞迴 `GITHUB_TOKEN` push event。

- Runtime: non-empty PE WinForge.exe.
- Setup: exactly one PE WinForge-Native-Setup.exe.
- Payload: app, uninstaller, notices; no .pdb or .ilk.
- Policy: per-user, x64, deterministic name, static-PCRE2 notice.

**Immutable release provenance · 不可變版本來源：** Every release tag explicitly targets the workflow SHA, so a concurrent `main` update cannot relabel artifacts from the completed run. · 每個 release 標籤都會明確指向 workflow SHA，所以 `main` 同時更新都唔會改標完成咗嘅 run 所產生嘅 artifacts。

**Proof status · 證明狀態：** Local parser, formatter, actionlint, installer-contract, clean-stage, PE-scan, release-mode, manual CLR/apphost/PDB rejection, and Inno native-PE checks pass. Exact hosted proof for the current branch push and subsequent `main` integration is still pending; the older non-`main` run predates and does not describe this policy. · 本機 parser、formatter、actionlint、installer contract、乾淨 stage、PE scan、release-mode、手動 CLR／apphost／PDB 拒絕同 Inno native-PE 檢查已通過。目前 branch push 同之後 `main` 整合嘅準確 hosted 證明仍待完成；舊 non-`main` run 早於而家政策，唔代表目前行為。
