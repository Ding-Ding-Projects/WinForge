# Handoff: FileZilla-style FTP/SFTP Client

| | |
|---|---|
| **Status** | Not started |
| **Source** | download.filezilla-project.org (FileZilla, C++/wxWidgets) — reference only. Native via NuGet: FluentFTP (FTP/FTPS) + SSH.NET (SFTP). |
| **License** | FileZilla = GPL-2.0 (not linked — reference for behavior only). FluentFTP = MIT; SSH.NET = MIT. WinForge code is original C#, no license conflict. |
| **Proposed module** | "FTP / SFTP" (粵語: FTP／SFTP 檔案傳輸) · Dev/Network group · Tag `module.filezilla` |
| **Effort** | L — no FTP/SFTP engine to write (libraries exist), but a dual-pane browser, a transfer queue with resume, and a site manager are each substantial UI/state work. |

## What the user asked for
A native FileZilla-style file-transfer client inside WinForge: a Site Manager of saved sites with encrypted credentials, connect over FTP/FTPS/SFTP, a dual-pane local+remote browser, upload/download with a transfer queue, rename/delete/mkdir on both sides, and resumable transfers.

## Recommended approach
**Native C# clone.** Per global-strategy step 1, FileZilla's *core* (protocol clients + a file browser) is fully reimplementable in managed C#: FluentFTP covers FTP/FTPS (explicit + implicit TLS, passive/active, resume via REST), and SSH.NET's `SftpClient` covers SFTP (download/upload streams, `ListDirectory`, `RenameFile`, `CreateDirectory`, `DeleteFile`, append-resume). We do NOT need to bundle or wrap the wxWidgets binary. Realistic v1: one connection at a time, a working dual-pane browser, and a serial transfer queue with resume. Defer FileZilla's heavier extras (multi-tab connections, parallel transfer threads, transfer-speed limits, directory-tree comparison) to later.

## Features to implement (v1 → later)
- v1: Site Manager (CRUD: name, protocol [FTP/FTPS/SFTP], host, port, user, password or SFTP key path); DPAPI-encrypted secret store; connect; left=local pane, right=remote pane (name, size, modified, type); navigate up/into folders; upload/download selected; serial transfer queue with per-item progress + overall progress; rename/delete/mkdir on both panes; resume interrupted transfers (FTP `REST`; SFTP byte-offset append).
- later: Quickconnect bar; parallel/threaded transfers; transfer speed limits; drag-and-drop between panes and from Explorer; recursive folder transfer with skip/overwrite/resume conflict rules; remote directory tree; FTPS cert / SFTP host-key TOFU prompt; bookmarks; transfer log pane.

## Integration plan (WinForge specifics)
- New files: `Services/FtpService.cs` (unified abstraction wrapping FluentFTP `AsyncFtpClient` and SSH.NET `SftpClient` behind common Connect/List/Upload/Download/Rename/Delete/Mkdir/Resume methods with `IProgress<double>`), `Services/FtpSiteStore.cs` (JSON site list + `ProtectedData` DPAPI for secrets — **reuse the SSH credential store from handoff 02**; share the schema/store rather than duplicating), `Pages/FileZillaModule.xaml(.cs)` (Site Manager pane + dual `ListView`/`ItemsRepeater` panes + queue/progress footer).
- Nav wiring: add `NavigationViewItem Tag="module.filezilla"` in `MainWindow.xaml` under the Dev/Network group; add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) with bilingual title/keywords (FTP, SFTP, FTPS, file transfer, 檔案傳輸) for master search; wire the Tag in `MainWindow.xaml.cs` `MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page filezilla`.
- Engine/install: winget id **n/a** for the native path — FluentFTP + SSH.NET are NuGet references, no binary needed. (Optionally offer `TimKosse.FileZilla.Client` via `EngineBars.AutoInstallButton` only as a "launch the real FileZilla" escape hatch; not required for v1.)
- Key APIs/CLIs: FluentFTP `AsyncFtpClient` (`Connect`, `GetListing`, `UploadStream/UploadFile` with `FtpRemoteExists.Resume`, `DownloadFile`, `Rename`, `DeleteFile/DeleteDirectory`, `CreateDirectory`); SSH.NET `SftpClient`/`PrivateKeyFile`; `System.Security.Cryptography.ProtectedData`; use `Services/FileDialogs.cs` for local folder/file selection and key-file picking — never WinRT pickers.

## Dependencies & risks
- **Secret store coordination**: share handoff 02's DPAPI store so credentials live in one place; align with export/secrets handling (handoff 03) so saved sites round-trip on export.
- **TLS / host-key trust**: FluentFTP validates FTPS certs and SSH.NET does not prompt for unknown host keys — implement a cert/host-key TOFU prompt or connections are MITM-exposed.
- **Resume correctness**: not all FTP servers support `REST`; detect capability and fall back to overwrite. SFTP resume needs accurate remote size.
- **Long-running transfers on UI thread**: keep all I/O async with cancellation; marshal progress back via `DispatcherQueue`.
- **NuGet trimming/AOT**: confirm FluentFTP + SSH.NET work under the app's publish settings on x64.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav and master search; create a saved site, connect over FTP and over SFTP; dual-pane browse works; upload and download a file with visible progress; rename/delete/mkdir work on both sides; a resumed transfer completes correctly; credentials stored DPAPI-encrypted (shared with handoff 02); every user-facing string is bilingual (English + 粵語); FileDialogs used throughout, no WinRT pickers.
