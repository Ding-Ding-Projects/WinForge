# Persistent Agent Task Memory · 長期代理任務記憶

This file is the durable execution contract for the canonical WinForge repository. It supplements, and never weakens, `AGENTS.md`.

呢份檔案係正式 WinForge repository 嘅長期執行合約；只會補充 `AGENTS.md`，絕對唔會削弱佢。

## Repository boundary · Repository 界線

- This repository is the canonical .NET 11 / WinUI 3 application and the source of its managed installer, portable package, updater metadata, wiki, and GitHub Pages content. · 呢個 repository 係正式 .NET 11／WinUI 3 app，同受控 installer、portable package、updater metadata、wiki 同 GitHub Pages 內容嘅來源。
- The experimental C++20/C++/WinRT port lives at [codingmachineedge/WinForge-Native](https://github.com/codingmachineedge/WinForge-Native). Native-port source, tests, parity ledgers, installer automation, feature records, and releases belong there. · 實驗性 C++20/C++/WinRT 移植版喺獨立 WinForge-Native repository；原生 source、tests、parity ledger、installer automation、功能記錄同 release 都要放嗰邊。
- The `native/` directory in this repository is different: it contains small C++ companion applications launched by the managed app and remains part of canonical WinForge. Do not confuse companions with the relocated port. · 呢個 repository 嘅 `native/` 目錄係正式 app 會開啟嘅細型 C++ companion，唔係已搬走嘅移植版，唔好混淆。
- Historical native-port commits, tags, releases, and archived links in Git history remain provenance. Do not revive them as the current managed release path. · Git 歷史入面嘅原生移植 commit、tag、release 同封存連結只係來源記錄，唔好重新當成目前正式發佈路徑。

## Managed application contract · 正式 app 合約

- Compile with `dotnet build WinForge.sln -c Debug -p:Platform=x64`; success means exit 0 and zero errors, not a fixed warning count. · 用指定 command 編譯；成功係 exit 0 同零 errors，唔係固定 warning 數量。
- Run through a self-contained publish or `.agents/skills/run-winforge/driver.ps1`; a framework-dependent Debug executable is not the runnable artifact in this workspace. · 要用自包含 publish 或 repo driver 執行；呢個 workspace 嘅 framework-dependent Debug executable 唔係可執行交付物。
- Managed release assets must stay compatible with `Services/AppUpdateService.cs`, including the expected `WinForge-Setup.exe` installer name and GitHub-provided SHA-256 digest verification. · 正式 release asset 要同 updater 合約相容，包括 `WinForge-Setup.exe` 名稱同 GitHub SHA-256 digest 驗證。
- Preserve the three persisted language modes exactly: English, playful respectful Hong Kong-style Cantonese, and compact bilingual. Keep localization resources separate from logic. · 保留英文、好玩但尊重嘅香港粵語、同精簡雙語三種持久模式；本地化資源要同邏輯分開。

## Reactor invariants · 反應堆 invariant

- The reactor boots held in MODE 5 cold shutdown; the operator must start it. · 反應堆啟動時保持 MODE 5 冷停堆，要由操作員啟動。
- Meltdown-to-real-PC-shutdown is off by default and abortable when explicitly armed. · 熔毀觸發真實電腦關機預設關閉，明確啟用後仍然可以中止。
- Waste writes preserve a disk free-space floor and default 50 GB cap. · 廢料寫入要保留磁碟可用空間底線同預設 50 GB 上限。
- Real-world side effects remain opt-in and reversible. · 現實世界副作用要保持明確 opt-in 同可還原。
- The focused harness is `dotnet run --project tests/ReactorSim.Tests -c Debug`; its current contract is 65/65 and nonzero exit on any failure or exception. · 專項 harness 目前合約係 65/65，任何失敗或例外都要非零退出。

## Completion and Git · 完成同 Git

- Treat every bounded task as incomplete until its intentional bilingual commit has been pushed. · 每個有限任務都要等雙語 commit push 咗先算完成。
- Work on a temporary `codex/` branch, push it, merge completed work into `main`, push `main`, fetch, and prove task and branch tips are ancestors of `origin/main`. · 用暫時 `codex/` branch 工作；push、合併入 `main`、push `main`、fetch，再證明 task 同 branch tip 都係 `origin/main` ancestor。
- Confirm expected files on remote `main` before deleting only the task branches and worktrees proven merged. Never delete or overwrite unrelated user work, dirty worktrees, branches, or stashes. · 刪除前先確認 remote main 有預期檔案；只可刪已證明合併嘅 task branch／worktree，絕對唔可以刪或覆寫不相關使用者工作。
- Never force-push. If authentication, protection, hosted CI, or release publication prevents remote proof, report the exact blocker and keep recoverable state. · 絕不 force-push；如果認證、保護、hosted CI 或發佈阻礙遙距證明，要報告確實問題並保留可恢復狀態。

## Documentation and evidence · 文件同證據

- Update `README.md`, the relevant categorized docs, `ROADMAP.md`, `handoff-summary.md`, `docs/wiki/`, and Pages content under `design/content/` for every project-changing task. · 每次改 project 都要同步 README、分類文件、路線圖、handoff、wiki 同 Pages 內容。
- Generated feature and button references remain generated; use their repository generator rather than hand-editing generated output. · 自動產生嘅功能同按鈕參考要繼續由 generator 產生，唔好手改 generated output。
- Visual changes require a fresh inspected screenshot for every changed page. If capture is blocked, record the exact error, retain no invalid/stale replacement, and keep visual status separate from functional evidence. · 視覺改動要有最新已檢視截圖；擷取受阻時要記確實錯誤、唔保留無效或舊替代圖，並將視覺狀態同功能證據分開。
- Use LowLevel Computer Use MCP on a dedicated headless desktop when callable. Otherwise use the process-owned repository driver, state the exact tool blocker, and never claim unavailable evidence. · 可呼叫時要喺專用 headless desktop 用 LowLevel MCP；否則用只控制自己 process 嘅 repo driver，記低工具阻礙，唔好聲稱冇做過嘅證據。

## Security and hygiene · 安全同整潔

- Never persist, log, copy, or screenshot secrets. Use DPAPI-backed stores already present in the application. · 絕對唔好保存、記錄、複製或截圖秘密；用 app 既有 DPAPI store。
- Keep destructive, financial, security, package, and external-integration actions explicit, reviewable, least-privileged, and reversible where possible. · 破壞性、財務、安全、套件同外部整合動作要明確、可檢視、最小權限，而且盡量可還原。
- Preserve unrelated working-tree changes and report verification honestly, including incomplete hosted or visual proof. · 保留不相關 working-tree 變更，如實報告驗證，包括未完成嘅 hosted 或視覺證明。
