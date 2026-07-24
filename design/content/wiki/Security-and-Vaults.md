# Security & Vaults · 安全與保險庫

Tools for encrypting volumes and managing your passwords and secrets. · 用嚟加密磁碟區同管理你嘅密碼同機密資料嘅工具。

## AI Chat provider credentials · AI Chat 供應商憑證

**EN —** AI Chat stores OpenAI-compatible provider API keys with CurrentUser DPAPI. An unreadable encrypted key is retained as ciphertext, so changing a provider name, URL, or default model cannot erase it. A DPAPI-protect failure cancels the entire provider settings write and reports the error instead of writing an empty secret. Only a deliberately supplied non-empty replacement key can replace an unreadable key.

**粵語 —** AI Chat 會用 CurrentUser DPAPI 儲存 OpenAI-compatible 供應商嘅 API 金鑰。加密金鑰讀唔到時會保留 ciphertext，所以改供應商名稱、網址或者預設模型都唔會抹走佢。DPAPI 保護失敗會取消成份供應商設定寫入並報錯，而唔會寫個空白 secret。只可以由人手輸入、而且唔係空白嘅替代金鑰取代讀唔到嘅金鑰。

## WinForge Vault · WinForge 保險庫

On-the-fly encrypted volume containers (VeraCrypt-derived). · 即時加密嘅磁碟區容器（源自 VeraCrypt）。

Open in-app: `WinForge.exe --page vault-volumes`

![WinForge Vault](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-vault.png)

## Dew Encryption · Dew 加密

Keep Git-backed local history for one file or folder, inspect changed files, take debounced automatic snapshots, restore through a staged rollback-safe workflow, and export the complete history as a password-encrypted 7z with hidden file names. The adjacent Git repository remains plaintext and the module says so explicitly. · 為一個檔案或者資料夾保留 Git 本機歷史、檢視改過嘅檔案、影 debounced 自動快照、經 staged 可 rollback 流程安全還原，再將完整歷史匯出成連檔名都隱藏嘅密碼加密 7z。旁邊嘅 Git 倉庫仍然係明文，模組會清楚講明。

Open in-app: `WinForge.exe --page dew-encryption`

The native module preserves Dew's `Dew Encryption Archives/.dew-encryption-repo/files/<source>` layout, but never launches the upstream Python or Avalonia applications. Encrypted exports use the installed `7z.dll` in-process through SharpSevenZip, so passwords never enter command-line arguments, environment variables, response files, or logs. · 原生模組保留 Dew 嘅 `Dew Encryption Archives/.dew-encryption-repo/files/<source>` 版面，但永遠唔會啟動上游 Python 或 Avalonia app。加密匯出會喺程序內經 SharpSevenZip 使用已安裝嘅 `7z.dll`，所以密碼唔會放喺命令列參數、環境變數、回應檔或者日誌。

Restore understands commits where the selected source was deleted: a writable project first records the live replacement as a safety snapshot and then restores the historical deletion, while an extracted read-only history treats the same deletion as a safe no-op. Repository imports and restore paths reject reparse points in the selected path **and its existing ancestors**. Commit discovery, source-name enumeration, repository traversal, depth, and helper output are bounded and cancellation-aware to prevent untrusted imported history from consuming unbounded work or memory. · 還原識得處理「選取來源喺該 commit 已刪除」嘅情況：可寫 project 會先將目前替代內容影成 safety snapshot，再還原歷史刪除；extracted read-only 歷史遇到同一刪除就會安全 no-op。匯入 repository 同還原路徑會拒絕選取路徑**以及現存祖先**入面嘅 reparse point；commit 探索、來源名稱、repository traversal、深度同 helper output 都有上限兼支援取消，避免不受信任匯入無限消耗工作或記憶體。

The adaptive page stacks its action, snapshot, password, history, watcher, and Vault controls at narrow widths; section headings, accessible names, live status announcements, tooltips, and 40-pixel action targets remain available in all three language modes. **Open Vault** stays available whenever the page is idle. Cancellation is checkpoint-based: Git and traversal work observe cancellation promptly, while an in-process 7-Zip call finishes its current native call before the requested cancellation takes effect. Errors are categorized and localized while preserving technical detail. · adaptive 頁面喺窄闊度會將 action、snapshot、password、history、watcher 同 Vault 控制直向排列；三種語言模式都有 section heading、無障礙名稱、live status announcement、tooltip 同 40-pixel action target。頁面 idle 時 **Open Vault** 一定可用。取消係 checkpoint-based：Git 同 traversal 會盡快回應，但程序內 7-Zip 要完成目前 native call，先會套用已要求嘅取消。錯誤會分類同本地化，同時保留技術細節。

Auto-history marks itself running **before** enabling operating-system file events, and rolls that state back if watcher activation fails, so a change arriving during startup is not discarded. The regression harness allows 45 seconds for event-to-commit completion under a loaded host: the existing 30-second external-process allowance plus 15 seconds for the 500 ms debounce, scheduling, and contention. It performs three rapid writes and proves they produce exactly one new commit containing the final value. · Auto-history 會喺啟用作業系統檔案事件**之前**先標記為 running；如果 watcher 啟動失敗就 rollback 狀態，所以啟動途中到達嘅變更唔會被掉走。Regression harness 喺高負載 host 俾 event-to-commit 45 秒：既有 30 秒 external-process allowance，再加 15 秒俾 500 ms debounce、排程同 contention。測試會快速連寫三次，並證明只產生一個包含最終值嘅新 commit。

Verification: `dotnet run --project tests\DewEncryption.Tests -c Debug --no-build` passed **23/23**; the solution build completed with zero errors; the XAML literal-safety gate protects Dew's managed toggle default; and the source audit resolved all **2,888/2,888** referenced handlers. The self-contained driver published and launched `--page dew-encryption`, but `CopyFromScreen` was unavailable and `PrintWindow` returned a blank or near-uniform frame. LowLevel MCP then confirmed a fresh live `WinUIDesktopWin32WindowClass` and visible child bridge on a dedicated headless desktop, but its returned capture was fully black. No invalid image was retained or presented as visual proof; a fresh canonical screenshot remains pending a graphics-capable session. · 驗證結果係 Dew **23/23**、solution 零 errors、XAML literal-safety gate 有保護 Dew managed toggle default，而且 source audit 解析晒 **2,888/2,888** 個 handler reference。自包含 driver 成功 publish 同直達 Dew 頁，但 `CopyFromScreen` 不可用，`PrintWindow` 又只得空白／近乎單色 frame；LowLevel MCP 之後喺專用 headless desktop 確認有新鮮 live WinUI window 同 visible child bridge，但 capture 係全黑。冇保留或者冒充無效圖片做 visual proof；要等 graphics-capable session 先補正式截圖。

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
