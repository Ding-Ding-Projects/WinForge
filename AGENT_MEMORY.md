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
- Run only through a self-contained publish driven by `.agents/skills/run-winforge/driver.ps1`; the driver creates a unique non-input Win32 desktop and launches the app there with native `CreateProcess`. Agent automation never launches WinForge directly or on the interactive desktop, and a framework-dependent Debug executable is not runnable here. · 只可以由 repo driver 驅動自包含 publish；driver 會建立獨立、非輸入 Win32 desktop，再用原生 `CreateProcess` 喺嗰度開 app。代理自動化絕對唔可以直接或者喺互動 desktop 開 WinForge，而 framework-dependent Debug executable 喺呢度亦唔可執行。
- For visual evidence, use LowLevel headless first or the driver's owned off-screen desktop. Never switch the user's input desktop or foreground a terminal, WinForge, or helper window. The DEBUG app renders its live WinUI tree to a unique validated PNG; only HWND-targeted `PrintWindow` on the owned off-screen desktop is a bounded fallback, and raw `CopyFromScreen` is forbidden. · 視覺證據要先用 LowLevel headless，或者用 driver 自家 off-screen desktop。唔可以切換使用者輸入 desktop，亦唔可以將 terminal、WinForge 或 helper window 搶到前景。DEBUG app 會將即時 WinUI tree 輸出去唯一、經驗證 PNG；有限後備只可以係對準自家 off-screen desktop HWND 嘅 `PrintWindow`，嚴禁原始 `CopyFromScreen`。
- Managed release assets must stay compatible with `Services/AppUpdateService.cs`, including the expected `WinForge-Setup.exe` installer name and GitHub-provided SHA-256 digest verification. · 正式 release asset 要同 updater 合約相容，包括 `WinForge-Setup.exe` 名稱同 GitHub SHA-256 digest 驗證。
- Preserve the three persisted language modes exactly: English, playful respectful Hong Kong-style Cantonese, and compact bilingual. Keep localization resources separate from logic. · 保留英文、好玩但尊重嘅香港粵語、同精簡雙語三種持久模式；本地化資源要同邏輯分開。

## Reactor invariants · 反應堆 invariant

- The reactor boots held in MODE 5 cold shutdown; the operator must start it. · 反應堆啟動時保持 MODE 5 冷停堆，要由操作員啟動。
- Meltdown-to-real-PC-shutdown is off by default and abortable when explicitly armed. · 熔毀觸發真實電腦關機預設關閉，明確啟用後仍然可以中止。
- Waste writes preserve a disk free-space floor and default 50 GB cap. · 廢料寫入要保留磁碟可用空間底線同預設 50 GB 上限。
- Real-world side effects remain opt-in and reversible. · 現實世界副作用要保持明確 opt-in 同可還原。
- The focused harness is `dotnet run --project tests/ReactorSim.Tests -c Debug`; its current contract is 67/67 and nonzero exit on any failure or exception. · 專項 harness 目前合約係 67/67，任何失敗或例外都要非零退出。

## Completion and Git · 完成同 Git

- Treat every bounded task as incomplete until its intentional bilingual commit has been pushed. · 每個有限任務都要等雙語 commit push 咗先算完成。
- Work on a temporary `codex/` branch, push it, merge completed work into `main`, push `main`, fetch, and prove task and branch tips are ancestors of `origin/main`. · 用暫時 `codex/` branch 工作；push、合併入 `main`、push `main`、fetch，再證明 task 同 branch tip 都係 `origin/main` ancestor。
- Confirm expected files on remote `main` before deleting only the task branches and worktrees proven merged. Never delete or overwrite unrelated user work, dirty worktrees, branches, or stashes. · 刪除前先確認 remote main 有預期檔案；只可刪已證明合併嘅 task branch／worktree，絕對唔可以刪或覆寫不相關使用者工作。
- Never force-push. If authentication, protection, hosted CI, or release publication prevents remote proof, report the exact blocker and keep recoverable state. · 絕不 force-push；如果認證、保護、hosted CI 或發佈阻礙遙距證明，要報告確實問題並保留可恢復狀態。

## Documentation and evidence · 文件同證據

- Update `README.md`, the relevant categorized docs, `ROADMAP.md`, `handoff-summary.md`, `docs/wiki/`, and Pages content under `design/content/` for every project-changing task. · 每次改 project 都要同步 README、分類文件、路線圖、handoff、wiki 同 Pages 內容。
- Generated feature and button references remain generated; use their repository generator rather than hand-editing generated output. · 自動產生嘅功能同按鈕參考要繼續由 generator 產生，唔好手改 generated output。
- Visual changes require a fresh inspected screenshot for every changed page. If capture is blocked, record the exact error, retain no invalid/stale replacement, and keep visual status separate from functional evidence. · 視覺改動要有最新已檢視截圖；擷取受阻時要記確實錯誤、唔保留無效或舊替代圖，並將視覺狀態同功能證據分開。
- Use LowLevel Computer Use MCP on a dedicated headless desktop when callable. Otherwise use the repository driver's process-owned, non-input Win32 desktop, state the exact tool blocker, and never claim unavailable evidence. A visible launch or focus-stealing fallback is prohibited. · 可呼叫時要喺專用 headless desktop 用 LowLevel MCP；否則用 repo driver 自家 process、非輸入 Win32 desktop，記低工具阻礙，唔好聲稱冇做過嘅證據。禁止任何可見 launch 或搶 focus 後備。
- Safe WinUI captures accept only bounded PNG paths on fixed/removable local drives, composite premultiplied pixels against the root's `ActualTheme`, and flush a unique same-directory partial file. Every live-tree and `PrintWindow` result reaches the requested filename only through write-through atomic promotion; no path writes directly to the final file. The driver removes stale output at attempt start, creates and closes one unique off-screen desktop, retains only the exact owned process/HWND, restores capture and automation-data environment variables, and removes only its validated unique temp data root plus `.winui-*` / `.driver-*` temporary files. It must never touch another WinForge process or the user's regular LocalAppData. · 安全 WinUI 擷取只接受 fixed／removable 本機 drive 上有限 PNG 路徑，按 root `ActualTheme` 合成 premultiplied pixels，先 flush 同目錄唯一 partial file。所有 live-tree 同 `PrintWindow` 結果只會經 write-through 原子升格到要求檔名，冇路徑會直接寫 final file。Driver 開始擷取就清走舊 output、建立並關閉唯一 off-screen desktop、只保留確實自家 process／HWND、還原 capture 同自動化資料環境變數，並只刪除已驗證嘅唯一 temp data root 同 `.winui-*`／`.driver-*` temp file。絕對唔可以掂另一個 WinForge process 或使用者正常 LocalAppData。

## Security and hygiene · 安全同整潔

- Never persist, log, copy, or screenshot secrets. Use DPAPI-backed stores already present in the application. · 絕對唔好保存、記錄、複製或截圖秘密；用 app 既有 DPAPI store。
- Keep destructive, financial, security, package, and external-integration actions explicit, reviewable, least-privileged, and reversible where possible. · 破壞性、財務、安全、套件同外部整合動作要明確、可檢視、最小權限，而且盡量可還原。
- Preserve unrelated working-tree changes and report verification honestly, including incomplete hosted or visual proof. · 保留不相關 working-tree 變更，如實報告驗證，包括未完成嘅 hosted 或視覺證明。
