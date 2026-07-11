# Security & Vaults · 安全與保險庫

Tools for encrypting volumes and managing your passwords and secrets. · 用嚟加密磁碟區同管理你嘅密碼同機密資料嘅工具。

## AI Chat provider credentials · AI Chat 供應商憑證

**EN —** AI Chat stores OpenAI-compatible provider API keys with CurrentUser DPAPI. An unreadable encrypted key is retained as ciphertext, so changing a provider name, URL, or default model cannot erase it. A DPAPI-protect failure cancels the entire provider settings write and reports the error instead of writing an empty secret. Only a deliberately supplied non-empty replacement key can replace an unreadable key.

**粵語 —** AI Chat 會用 CurrentUser DPAPI 儲存 OpenAI-compatible 供應商嘅 API 金鑰。加密金鑰讀唔到時會保留 ciphertext，所以改供應商名稱、網址或者預設模型都唔會抹走佢。DPAPI 保護失敗會取消成份供應商設定寫入並報錯，而唔會寫個空白 secret。只可以由人手輸入、而且唔係空白嘅替代金鑰取代讀唔到嘅金鑰。

## WinForge Vault · WinForge 保險庫

On-the-fly encrypted volume containers (VeraCrypt-derived). · 即時加密嘅磁碟區容器（源自 VeraCrypt）。

Open in-app: `WinForge.exe --page vault-volumes`

![WinForge Vault](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-vault.png)

## Bitwarden Vault · Bitwarden 密碼庫

Drive the Bitwarden CLI for logins, TOTP and generators. · 驅動 Bitwarden CLI 管理登入、TOTP 同密碼產生。

Open in-app: `WinForge.exe --page bitwarden`

![Bitwarden Vault](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-bitwarden.png)

## KeePass Vault · 密碼保險庫

Local offline KeePass (kdbx) password vault, natively encrypted. · 本機離線 KeePass（kdbx）密碼庫，原生加密。

KDBX 4 reads the KDF UUID in the database header and derives the matching AES-KDF, Argon2d, or Argon2id key; one Argon2 variant is never substituted for another. · KDBX 4 會讀資料庫 header 入面嘅 KDF UUID，再用返相應嘅 AES-KDF、Argon2d 或 Argon2id 衍生金鑰；絕對唔會用一種 Argon2 冒充另一種。

Delayed secret-clipboard cleanup first reads the current text and clears it only when it exactly matches the secret WinForge copied; a generation guard also prevents an older timer from clearing a newer copy. This preserves content copied by the user after a password, TOTP, or other secret. · 延遲清除密碼剪貼簿之前會先讀目前文字，只有仲同 WinForge 複製嘅密碼完全一樣先會清除；generation guard 仲會阻止舊 timer 清除較新嘅複製內容。即係使用者複製密碼、TOTP 或其他秘密之後嘅新內容會被保留。

Open in-app: `WinForge.exe --page keepass`

![KeePass Vault](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-keepass.png)

## Local Settings Recovery Artifacts · 本機設定復原檔案

`settings.json`, `settings.json.bak`, and any `settings.json.corrupt-*` recovery copy stay inside `%LOCALAPPDATA%\WinForge`. They can contain sensitive configuration or protected credential blobs, so they are not support bundles and must not be shared without review. The store uses flushed, atomic replacement and a previous-snapshot backup; an unrecoverable malformed file blocks routine writes instead of silently discarding it.

`settings.json`、`settings.json.bak` 同任何 `settings.json.corrupt-*` 復原副本都會留喺 `%LOCALAPPDATA%\WinForge`。佢哋可以有敏感設定或者受保護嘅認證 blob，所以唔係支援包，未檢查唔可以分享。儲存層會用 flush 後嘅原子式替換同上一個快照備份；遇到冇得復原嘅壞檔案會封鎖平常寫入，唔會靜雞雞掉走佢。

This is an internal storage safeguard, not a changed vault page; screenshot capture and replacement are not applicable. · 呢個係內部儲存保障，唔係改咗保險庫頁面；唔適用截圖擷取或者替換。

[← Wiki Home](Home.md)
