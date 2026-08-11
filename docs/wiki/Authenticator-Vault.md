# Authenticator vault · 驗證器 vault

WinForge's TOTP page stores multiple local authenticator entries. Metadata is versioned and
searchable; secret material is held only in the Windows credential vault under a stable
per-entry key.

Registration requires a locally generated otpauth://totp/ QR and one current-code confirmation.
The code must match before the secret is written to the vault. Saved entries render the current
code, countdown, issuer/account/group metadata, and accessible actions without putting a secret
back into the editor.

Redacted JSON and CSV exports carry all visible metadata and state explicitly that secrets were
omitted. The normal export route never writes usable secrets. Removing an entry requires two
confirmation keys and a full slider, then removes both metadata and the vault credential.

RFC 6238 SHA-1, SHA-256, and SHA-512 vectors are covered by the TotpAuthenticator.Tests harness.
QR image-file and camera ingestion are tracked separately from the URI/clipboard route currently
shipped.

![TOTP authenticator with vault-backed entry and pairing controls](../screenshot-totp-authenticator-2026-08-11.png)
