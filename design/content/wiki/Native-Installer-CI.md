# Native C++ Installer CI · 原生 C++ 安裝程式 CI

**EN.** Native release CI validates the installer contract at runtime staging, setup compilation, and post-install payload inspection. The silent smoke test then uninstalls from a guarded LocalAppData location and requires cleanup.

**粵語.** Native release CI 會喺 runtime staging、setup compilation 同 post-install payload inspection 驗證 installer contract。靜默 smoke test 之後會由受保護 LocalAppData 位置解除安裝，並要求清理完成。

Every branch push and every pull request into `main` runs the full native gate. Every successful `main` push also publishes a unique `native-v1.0.<run>` GitHub Release with the portable ZIP and installer; unmerged branch and pull-request runs keep Actions artifacts only. The generated site-data commit explicitly dispatches the gate because GitHub suppresses recursive `GITHUB_TOKEN` push events. · 每次推送任何 branch 同每個合併去 `main` 嘅 pull request 都會執行完整原生 gate。每次成功推送去 `main` 亦會發佈獨立 `native-v1.0.<run>` GitHub Release，包含可攜 ZIP 同安裝程式；未合併 branch 同 pull-request 執行只會保留 Actions artifacts。Generated site-data commit 會明確 dispatch 呢個 gate，因為 GitHub 會抑制遞迴 `GITHUB_TOKEN` push event。

- Runtime: non-empty PE WinForge.exe.
- Setup: exactly one PE WinForge-Native-Setup.exe.
- Payload: app, uninstaller, notices; no .pdb or .ilk.
- Policy: per-user, x64, deterministic name, static-PCRE2 notice.
