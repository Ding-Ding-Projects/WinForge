# Handoff: Email Client (Thunderbird-style)

| | |
|---|---|
| **Status** | Not started |
| **Source** | github.com/mozilla/thunderbird (C++/JS reference). Native build via MailKit/MimeKit (github.com/jstedfast/MailKit) |
| **License** | Thunderbird: MPL-2.0 (open source). MailKit/MimeKit: MIT. Both safe to depend on / ship. |
| **Proposed module** | Mail · "Connectivity & Network" (near Connections / VPN) group · Tag `module.mail` |
| **Effort** | L — IMAP/SMTP sync, MIME parsing, OAuth handoff, and a three-pane UI are each non-trivial; MailKit removes the protocol risk. |

## What the user asked for
A native, multi-account email client inside WinForge (not a full Thunderbird clone): add IMAP/SMTP accounts, list folders and messages, read/compose/reply/forward with attachments, and search. Store credentials encrypted. Launch the real Thunderbird via winget as a fallback.

## Recommended approach
**Native C# clone (MailKit/MimeKit).** Per the global strategy, prefer a native WinUI reimplementation. Thunderbird itself is a huge C++/JS/XUL app that cannot be cloned in scope, but its *core* — IMAP/SMTP/POP3 + MIME — is exactly what MailKit/MimeKit provide as mature MIT libraries. So we clone the useful 20% natively rather than wrapping Thunderbird's binary.

Realistic **v1 scope:** a working three-pane client (folders | message list | reader) for one or more IMAP+SMTP accounts using app passwords, plus OAuth2 for Gmail/Outlook reusing the WebView2 OAuth handoff (#01). Not in v1: local message DB/offline cache beyond in-session, server-side rules, calendar, PGP, address book. Keep the Thunderbird winget install as a one-click escape hatch for anything we do not cover.

## Features to implement (v1 → later)
- v1: Add account (IMAP/SMTP host, port, TLS/STARTTLS, username, app password); auto-detect common providers (Gmail/Outlook/iCloud). OAuth2 path via WebView2 handoff #01. Folder tree, message list (paged, newest first), read pane (HTML via WebView2 sandboxed, plus text fallback), open/save attachments (FileDialogs only). Compose / reply / reply-all / forward with attachments, send via SMTP. Mark read/unread, delete (move to Trash), basic IMAP SEARCH. Encrypted credential storage (DPAPI).
- later: Offline cache / local store, multiple-identity send, signatures, server folder management, unified inbox, threaded conversation view, contacts, push/IDLE notifications, S/MIME or OpenPGP.

## Integration plan (WinForge specifics)
- New files: `Services/MailService.cs` (MailKit ImapClient/SmtpClient wrappers; account CRUD; DPAPI encrypt via `ProtectedData`), `Services/MailAccountStore.cs` (persist accounts to `SettingsStore`, secrets DPAPI-encrypted), `Pages/MailModule.xaml(.cs)` (three-pane UI), `Pages/MailComposeWindow.xaml(.cs)` (compose dialog). Reuse OAuth from handoff #01.
- Nav wiring: add `NavigationViewItem` `Tag="module.mail"` in `MainWindow.xaml` under a sensible group; add a `ModuleRegistry.All` entry `new() { Tag = "module.mail", En = "Mail", Zh = "電郵", Glyph = ((char)0xE715).ToString(), Keywords = "mail email imap smtp thunderbird inbox compose reply attachment oauth gmail outlook 電郵 郵件 收件匣 撰寫 回覆 附件" }`; wire `MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` (`--page mail`) in `MainWindow.xaml.cs`.
- Engine/install: winget id `Mozilla.Thunderbird` via `EngineBars.AutoInstallButton("Mozilla.Thunderbird", "Install Thunderbird", "安裝 Thunderbird", recheck, rescan)` — only the launch-Thunderbird fallback needs a binary; the native client itself needs no external CLI.
- Key APIs: MailKit `ImapClient.Connect/Authenticate/GetFolder/Search/Fetch`, `SmtpClient.Send`; MimeKit `MimeMessage`, `BodyBuilder`, `MimeEntity`; `System.Security.Cryptography.ProtectedData` for DPAPI; WebView2 for HTML rendering and OAuth.

## Dependencies & risks
- NuGet: `MailKit` + `MimeKit` (MIT). Confirm clean restore for .NET 11 / WinUI 3 x64.
- OAuth2 is mandatory for Gmail/Outlook (Google blocks Basic Auth, Microsoft is deprecating it) — depends on handoff #01 being usable. App passwords still cover providers that allow them.
- HTML email is an XSS/tracking-pixel surface: render in a sandboxed WebView2 with remote-content blocked by default.
- DPAPI secrets are per-user/per-machine — document that exported settings cannot decrypt elsewhere. Never log credentials.
- Long-running IMAP calls must be async/cancellable so the UI thread never blocks.

## Acceptance criteria
- Builds clean (Debug + Release x64); `module.mail` appears in nav and master search.
- Core flow works: add an IMAP+SMTP account, browse folders, read a message with attachments, compose and send a reply.
- Every user-facing string is bilingual (English + 粵語).
- Credentials stored DPAPI-encrypted; no plaintext secrets persisted or logged.
- All file open/save uses `FileDialogs` (Win32 COM), never WinRT pickers.
- "Install / launch Thunderbird" fallback works via `EngineBars.AutoInstallButton`.
