# Archive safety workflows · 壓縮檔安全工作流程

Open `WinForge.exe --page archives` for parameterized create and in-archive delete controls alongside the generated 7-Zip quick actions.

用 `WinForge.exe --page archives` 開參數化建立同檔內刪除控制，旁邊仍然保留自動生成 7-Zip 快捷操作。

Create accepts bounded include/exclude masks and the complete 7z NTFS time/access switch set. Move mode packs first, runs a separate password- and split-volume-aware `7z t`, and only then sends the source to the Recycle Bin; it never relies on `-sdel`. Arbitrary entry deletion shows every validated relative mask in a destructive confirmation before `7z d` runs.

建立流程接受有界 include／exclude 樣式同完整 7z NTFS 時間／存取開關。搬走模式先壓縮，再用支援密碼同分卷嘅獨立 `7z t` 測試，最後先送來源去回收筒，絕對唔依賴 `-sdel`。任意檔內刪除會喺 `7z d` 前用破壞性確認顯示晒已驗證相對樣式。

![Archives](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-archives.png)

See the [safe archive workflow guide](../features/archives/safe-create-and-delete.md) for supported inputs, failure modes, and focused tests. · 支援輸入、失敗模式同專項測試請睇[安全壓縮檔指南](../features/archives/safe-create-and-delete.md)。

[← Wiki Home](Home.md)
