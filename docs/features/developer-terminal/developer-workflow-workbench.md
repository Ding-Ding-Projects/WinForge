# Developer workflow workbench · 開發工作流程工作台

## Behavior · 行為

The workbench sits above the existing Developer & Terminal catalog. It resolves the process listening on a user-entered TCP port and shows the PID/name before a destructive decision; the listener set is re-read immediately before termination and a changed identity fails closed. It also detects fnm, Volta, and nvm-windows, lists/installs bounded Node versions, and opens an isolated fnm/Volta PowerShell. nvm-windows remains list/install-only because its active-version symlink is machine-wide.

工作台放喺原有開發快捷操作上面。佢會由使用者輸入嘅 TCP port 搵出 PID／程序名，先顯示再確認；真正終止之前會重新讀一次，身份有變就 fail closed。亦會偵測 fnm、Volta 同 nvm-windows、列出／安裝有界 Node 版本，並用 fnm／Volta 開隔離 PowerShell。nvm-windows 因為切換 symlink 係全機共用，所以只供列出同安裝。

Corepack exposes version, enable, and `prepare` for pnpm/yarn. Defender exclusions have folder selection, inventory, reviewed add/remove, and reject drive, Windows, and Program Files roots. TCP tuning first shows the live dynamic range and `TcpTimedWaitDelay`, then validates the port range and the Windows-supported 30–300 second TIME_WAIT value before elevation. Cache cleanup stays disabled until a bounded local size scan and Docker `system df` report have completed.

Corepack 有版本、啟用同 pnpm／yarn `prepare`。Defender 例外有資料夾揀選、清單、確認加入／移除，亦會拒絕磁碟、Windows 同 Program Files 根目錄。TCP 調校先顯示目前範圍同 `TcpTimedWaitDelay`，再驗證連接埠範圍同 Windows 支援嘅 30–300 秒 TIME_WAIT 先提權。快取清理要完成有界本機大小掃描同 Docker `system df` 報告先會啟用。

## Configuration and failure modes · 設定同失敗模式

- Port and TCP values are whole numbers with explicit Windows bounds.
- Node versions/channels accept only a short semantic/channel grammar; shell metacharacters are rejected.
- Missing CLIs are reported without installing anything automatically.
- Cache measurement skips reparse points and stops after 250,000 files; a truncated scan is labelled.
- Every operation has a two-minute cancellation timeout. Partial tool failures stop a cleanup batch and retain the captured output.

所有使用者值會成為獨立 argv 項目；只有 fnm 環境初始化需要 PowerShell 程式碼，而 executable 同版本會做 literal escaping。未安裝工具會如實顯示，唔會自動裝嘢。快取量度會跳過 reparse point，最多掃 250,000 個檔案；截斷會清楚標示。每個操作有兩分鐘取消／逾時，批次有一步失敗就停止並保留輸出。

## Security and privacy · 安全同私隱

The page never mutates settings during inspection. Termination, Defender changes, TCP changes, and cache cleanup require an explicit decision. Defender/TCP elevation is requested only for the confirmed mutation. No paths, process lists, package versions, or cache reports leave the computer or persist in WinForge.

檢視期間唔會改設定。終止程序、Defender、TCP 同清快取全部要明確確認；Defender／TCP 只會喺確認修改時要求提權。路徑、程序清單、版本同快取報告唔會離開部機，亦唔會由 WinForge 保存。

## Verification · 驗證

`tests/RoadmapWorkflowCore.Tests` covers listener parsing/identity drift, argv boundaries, Node/Corepack injection rejection, Defender root rejection, TCP ranges, and all four cleanup plans. Verification must not click any mutating action against the live host.

`tests/RoadmapWorkflowCore.Tests` 會測 listener 解析／身份漂移、argv 邊界、Node／Corepack 注入拒絕、Defender 根目錄拒絕、TCP 範圍同四種清理計劃。驗證時唔可以對真實主機撳任何修改動作。
