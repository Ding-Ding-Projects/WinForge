# KeePass Vault · 密碼保險庫

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.keepass</code> |
| Deep-link alias · 深層連結別名 | <code>keepass</code> |
| Category · 分類 | Apps & Git · 程式與 Git |
| Page class · 頁面類別 | <code>KeePassModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/KeePassModule.xaml</code> |
| Button docs · 按鈕文件 | 13 |

## What It Covers · 功能範圍

**EN —** KeePass Vault is registered in WinForge search and navigation with these keywords: <code>keepass kdbx kee pass password vault local offline manager database master password key file open create entry group tree generator generate clipboard auto clear search lock unlock aes chacha20 argon2 salsa20 native encrypt decrypt 密碼保險庫 密碼庫 密碼 管理 本機 離線 主密碼 鎖匙檔 群組 項目 產生器 搜尋 鎖定 解鎖 加密 解密 原生</code>.

**粵語 —** 密碼保險庫 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>keepass kdbx kee pass password vault local offline manager database master password key file open create entry group tree generator generate clipboard auto clear search lock unlock aes chacha20 argon2 salsa20 native encrypt decrypt 密碼保險庫 密碼庫 密碼 管理 本機 離線 主密碼 鎖匙檔 群組 項目 產生器 搜尋 鎖定 解鎖 加密 解密 原生</code>。

## KDBX KDF Compatibility · KDBX KDF 相容性

**EN —** The native KDBX 4 reader follows the database KDF UUID and derives with AES-KDF, Argon2d, or Argon2id as declared. It never treats an Argon2id vault as Argon2d.

**粵語 —** 原生 KDBX 4 讀取器會跟資料庫 KDF UUID，用返聲明咗嘅 AES-KDF、Argon2d 或 Argon2id 衍生金鑰；Argon2id vault 絕對唔會當成 Argon2d。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [BrowseBtn](../../buttons/apps-git-git/keepass/001-browsebtn.md) | `Button` | `BrowseBtn` | `Browse_Click` |
| [KeyFileBtn](../../buttons/apps-git-git/keepass/002-keyfilebtn.md) | `Button` | `KeyFileBtn` | `PickKeyFile_Click` |
| [KeyFileClearBtn](../../buttons/apps-git-git/keepass/003-keyfileclearbtn.md) | `Button` | `KeyFileClearBtn` | `ClearKeyFile_Click` |
| [OpenBtn](../../buttons/apps-git-git/keepass/004-openbtn.md) | `Button` | `OpenBtn` | `Open_Click` |
| [CreateBtn](../../buttons/apps-git-git/keepass/005-createbtn.md) | `Button` | `CreateBtn` | `Create_Click` |
| [AddEntryBtn](../../buttons/apps-git-git/keepass/006-addentrybtn.md) | `Button` | `AddEntryBtn` | `AddEntry_Click` |
| [AddGroupBtn](../../buttons/apps-git-git/keepass/007-addgroupbtn.md) | `Button` | `AddGroupBtn` | `AddGroup_Click` |
| [GenBtn](../../buttons/apps-git-git/keepass/008-genbtn.md) | `Button` | `GenBtn` | `Generator_Click` |
| [SaveBtn](../../buttons/apps-git-git/keepass/009-savebtn.md) | `Button` | `SaveBtn` | `Save_Click` |
| [SaveAsBtn](../../buttons/apps-git-git/keepass/010-saveasbtn.md) | `Button` | `SaveAsBtn` | `SaveAs_Click` |
| [LockBtn](../../buttons/apps-git-git/keepass/011-lockbtn.md) | `Button` | `LockBtn` | `Lock_Click` |
| [EditBtn](../../buttons/apps-git-git/keepass/012-editbtn.md) | `Button` | `EditBtn` | `EditEntry_Click` |
| [DeleteEntryBtn](../../buttons/apps-git-git/keepass/013-deleteentrybtn.md) | `Button` | `DeleteEntryBtn` | `DeleteEntry_Click` |
