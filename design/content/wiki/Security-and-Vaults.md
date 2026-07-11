# Security & Vaults · 安全與保險庫

Tools for encrypting volumes and managing your passwords and secrets. · 用嚟加密磁碟區同管理你嘅密碼同機密資料嘅工具。

## WinForge Vault · WinForge 保險庫

On-the-fly encrypted volume containers (VeraCrypt-derived). · 即時加密嘅磁碟區容器（源自 VeraCrypt）。

Open in-app: `WinForge.exe --page vault-volumes`

![WinForge Vault](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-vault.png)

## Dew Encryption · Dew 加密

Keep Git-backed local history for one file or folder, inspect changed files, take debounced automatic snapshots, restore through a staged rollback-safe workflow, and export the complete history as a password-encrypted 7z with hidden file names. The adjacent Git repository remains plaintext and the module says so explicitly. · 為一個檔案或者資料夾保留 Git 本機歷史、檢視改過嘅檔案、影 debounced 自動快照、經 staged 可 rollback 流程安全還原，再將完整歷史匯出成連檔名都隱藏嘅密碼加密 7z。旁邊嘅 Git 倉庫仍然係明文，模組會清楚講明。

Open in-app: `WinForge.exe --page dew-encryption`

The native module preserves Dew's `Dew Encryption Archives/.dew-encryption-repo/files/<source>` layout, but never launches the upstream Python or Avalonia applications. Encrypted exports use the installed `7z.dll` in-process through SharpSevenZip, so passwords never enter command-line arguments, environment variables, response files, or logs. · 原生模組保留 Dew 嘅 `Dew Encryption Archives/.dew-encryption-repo/files/<source>` 版面，但永遠唔會啟動上游 Python 或 Avalonia app。加密匯出會喺程序內經 SharpSevenZip 使用已安裝嘅 `7z.dll`，所以密碼唔會放喺命令列參數、環境變數、回應檔或者日誌。

## Bitwarden Vault · Bitwarden 密碼庫

Drive the Bitwarden CLI for logins, TOTP and generators. · 驅動 Bitwarden CLI 管理登入、TOTP 同密碼產生。

Open in-app: `WinForge.exe --page bitwarden`

![Bitwarden Vault](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-bitwarden.png)

## KeePass Vault · 密碼保險庫

Local offline KeePass (kdbx) password vault, natively encrypted. · 本機離線 KeePass（kdbx）密碼庫，原生加密。

Open in-app: `WinForge.exe --page keepass`

![KeePass Vault](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-keepass.png)

[← Wiki Home](Home.md)
