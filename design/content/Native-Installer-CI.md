# Native C++ Installer CI · 原生 C++ 安裝程式 CI

The Pages documentation mirrors the native installer contract used by .github/workflows/native-release.yml.

**EN.** CI verifies the staged runtime, the compiled Inno Setup executable, and the installed payload. It enforces a per-user x64 installer, checks PE files and third-party notices, rejects debug artifacts, silently uninstalls, and proves the guarded LocalAppData directory is removed.

**粵語.** CI 會驗證 stage runtime、編譯好嘅 Inno Setup executable 同已安裝 payload。佢強制每用戶 x64 installer、檢查 PE files 同 third-party notices、拒絕 debug artifacts、靜默解除安裝，並確認受保護 LocalAppData directory 已移除。

The reusable implementation is eng/native/Test-NativeInstallerContract.ps1; its three CI invocations make the packaging contract auditable from source through uninstall cleanup.
