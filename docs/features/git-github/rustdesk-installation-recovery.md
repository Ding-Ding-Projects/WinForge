# RustDesk installer catalog recovery · RustDesk 安裝 catalog 復原

## Behavior · 行為

The RustDesk page first calls WinGet with the exact package ID `RustDesk.RustDesk`. The current WinGet catalog can return `No package found matching input criteria.` because the package is absent from that catalog. Only that catalog-unavailable result triggers the fallback; installer hash errors, UAC cancellation, network errors, and other WinGet failures remain visible as their original failures.

RustDesk's official latest-release API is then read with a bounded timeout and bounded metadata size. WinForge selects only the Windows x64 `.exe` asset from the official `rustdesk/rustdesk` GitHub release. The downloaded file is bounded to 100 MiB, must match the API's declared byte count, must begin with the Windows `MZ` header, and must match the release asset's SHA-256 digest. Only after those checks does WinForge run the official installer with `--silent-install` through the existing UAC-aware process runner. The temporary installer is removed after the run.

## Failure modes · 失敗模式

- A missing or malformed release feed, missing x64 asset, untrusted URL, missing digest, size mismatch, non-PE payload, hash mismatch, HTTP error, installer failure, or post-install executable lookup failure keeps the install unsuccessful.
- If the WinGet attempt and official fallback both fail, the progress result retains both diagnostics so the user can see whether the catalog or the official release path was responsible.
- A RustDesk configuration directory alone is not treated as proof that `rustdesk.exe` is installed; the executable must be found in a supported installation location.

## Security boundary · 安全界線

The fallback does not bundle or modify RustDesk. It accepts only HTTPS downloads whose host, repository path, release tag, asset name, size, and SHA-256 are validated against the official GitHub release API. The download is stored in a unique temporary path, is not logged, and is deleted after installation. No credentials are sent to the release API.

## Verification · 驗證

- `tests/RustDeskInstaller.Tests` covers the accepted official asset, untrusted URLs, missing digests, draft releases, and the precise catalog-unavailable signal.
- `dotnet run --project tests/RustDeskInstaller.Tests -c Debug` passes `5/5`.
- `dotnet build WinForge.csproj -c Debug -p:Platform=x64` passes with `0` warnings and `0` errors.
- The RustDesk module must still receive a fresh built-artifact capture through `.agents/skills/run-winforge/driver.ps1 -Page rustdesk`; the capture is evidence of the UI path only and does not claim a live install was performed.

## Suggested articles · 建議文章

- [RustDesk module reference](../../wiki/features/apps-git-git/rustdesk.md)
- [Package Manager](../../wiki/Package-Manager.md)
- [Build and install](../../../README.md#build-and-install--建置同安裝)
