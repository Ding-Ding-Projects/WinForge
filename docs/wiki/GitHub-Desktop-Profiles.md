# GitHub Desktop Profiles · GitHub Desktop 多帳戶設定檔

**EN —** The GitHub Desktop Profiles module gives any number of GitHub identities separate GitHub Desktop application data and global Git configuration while sharing one [official, auto-updating GitHub Desktop](https://desktop.github.com/) installation. It also includes a separate manager for accounts already stored by the official GitHub CLI (`gh`).

**粵語 —** GitHub Desktop 多帳戶設定檔模組畀任意數量嘅 GitHub 身份各自用獨立嘅 GitHub Desktop app 資料同全域 Git 設定，同時共用一份官方、可以自動更新嘅 GitHub Desktop 安裝。模組亦有一個獨立管理員，管理官方 GitHub CLI（`gh`）已儲存嘅帳戶。

## Initial profiles · 初始設定檔

| Profile · 設定檔 | Role · 用途 |
|---|---|
| **CodingMachineEdge** | Preserves the existing/default GitHub Desktop data and the user's normal global `.gitconfig` · 保留現有／預設 GitHub Desktop 資料同用戶原本嘅全域 `.gitconfig` |
| **CafePromenade** | Uses separate application data and a separate global Git configuration · 用獨立 app 資料同獨立全域 Git 設定 |
| **INFTGroup7** | Uses separate application data and a separate global Git configuration · 用獨立 app 資料同獨立全域 Git 設定 |

These are the initial names, not a three-profile limit. Add as many profiles as required. Every profile has a stable internal identifier that remains the same when its display name changes. Existing schema-1 configuration and the earlier default, `Profile 2`, and `Profile 3` data folders can be adopted instead of reset.

上面只係初始名稱，唔係最多三個設定檔。你可以按需要新增任意數量。每個設定檔都有穩定內部識別碼，改顯示名稱唔會改身份。現有 schema-1 設定，同之前嘅預設、`Profile 2` 同 `Profile 3` 資料夾都可以沿用，唔使重新開始。

## Requirement · 使用要求

Install the official GitHub Desktop once. WinForge then creates one isolated launch path per profile; it does not install or maintain separate application binaries. If GitHub Desktop is missing, the module offers the verified `GitHub.GitHubDesktop` winget package.

只需要安裝一次官方 GitHub Desktop。之後 WinForge 會為每個設定檔建立一條隔離啟動路徑；佢唔會安裝或者維護多份程式二進位檔。如果未有 GitHub Desktop，模組會提供經核實嘅 `GitHub.GitHubDesktop` winget 套件。

GitHub CLI is optional and separately installed as the verified `GitHub.cli` winget package. GitHub Desktop does not require `gh`, and the CLI account manager does not require a Desktop profile with the same name.

GitHub CLI 係可選而且獨立安裝，使用經核實嘅 `GitHub.cli` winget 套件。GitHub Desktop 唔需要 `gh`；CLI 帳戶管理員亦唔要求有同名 Desktop 設定檔。

## What the module manages · 模組管理內容

- Set up or repair every profile and recreate its Start Menu and optional desktop shortcuts. · 建立或者修復所有設定檔，並重新建立「開始」選單同可選桌面捷徑。
- Add, edit, or remove individual profiles at any time. · 隨時新增、編輯或者移除個別設定檔。
- Activate and launch a profile or open its data folder. · 啟用同開啟設定檔，或者開啟佢嘅資料夾。
- Remove the complete managed configuration while preserving profile data by default. · 移除整套受管理設定時，預設會保留設定檔資料。
- Reuse the signed GitHub Desktop executable rather than copying application binaries. · 重用已簽署嘅 GitHub Desktop 執行檔，唔會複製程式二進位檔。

WinForge shortcuts start `WinForgeLauncher` with a stable profile identifier. Child GitHub Desktop processes receive the matching application-data directory, while only the additional profiles receive their own `GIT_CONFIG_GLOBAL` file.

WinForge 捷徑會用穩定設定檔識別碼啟動 `WinForgeLauncher`。由佢開出嚟嘅 GitHub Desktop 子程序會收到對應 app 資料目錄，而只有額外設定檔先會收到各自嘅 `GIT_CONFIG_GLOBAL` 檔案。

## Add, edit, and remove profiles · 新增、編輯同移除設定檔

1. Open **GitHub Desktop Profiles** and leave **Desktop shortcuts** on if each profile should also appear on the desktop. **Configure profiles** creates or adopts the current list; **Repair routing** rebuilds its shortcuts and callback handlers. · 開 **GitHub Desktop 多帳戶設定檔**；如果每個設定檔都要有桌面捷徑，就保持 **桌面捷徑** 開啟。**設定帳戶**會建立或者接管目前清單；**修復路由**會重建捷徑同 callback handler。
2. Select **Add profile**, enter a unique display name, and confirm. WinForge creates a stable profile identifier, isolated app-data path, separate global Git configuration, and named shortcuts without changing existing profiles. · 揀 **新增設定檔**，輸入唯一顯示名稱再確認。WinForge 會建立穩定識別碼、隔離 app 資料路徑、獨立全域 Git 設定同具名捷徑，唔會改動現有設定檔。
3. To rename one, edit its name box and leave the field. The stable identifier and data path stay attached to that profile while its shortcuts are renamed. Empty or duplicate names are rejected. · 要改名，就編輯名稱欄再離開欄位。穩定識別碼同資料路徑仍然屬於同一個設定檔，捷徑會一併改名；空白或者重複名稱會被拒絕。
4. To remove an additional profile, select **Remove** on its row and confirm. WinForge removes it from the launcher and deletes only the shortcuts it owns; the existing app-data folder is retained. The default profile keeps the user's original data and `.gitconfig`, so it can be renamed but not removed. · 要移除額外設定檔，就喺該行揀 **移除**再確認。WinForge 會由啟動器移除佢，只刪除自己建立嘅捷徑，現有 app 資料夾會保留。預設設定檔保留用戶原本資料同 `.gitconfig`，所以可以改名但唔可以移除。
5. **Launch** opens the selected isolated instance without closing other instances. **Activate** selects which profile receives the next GitHub Desktop browser callback. **Folder** opens its app-data directory. · **開啟**會啟動所選隔離實例，唔會關閉其他實例；**設為使用中**會指定下一個 GitHub Desktop 瀏覽器 callback 交畀邊個設定檔；**資料夾**會開佢嘅 app 資料目錄。

The page summary shows how many data folders exist. A missing shortcut does not mean the data was deleted; run **Repair routing** to recreate managed shortcuts and handlers.

頁面摘要會顯示有幾多個資料夾已存在。捷徑唔見咗唔代表資料被刪；執行 **修復路由**就可以重建受管理捷徑同 handler。

## GitHub CLI account manager · GitHub CLI 帳戶管理員

The GitHub CLI card operates the official `gh` account store. **Refresh accounts** checks the installed version and runs safe status discovery for `github.com`; the account picker shows login, host, active state, authentication state, scopes, token source, Git protocol, and any non-secret error. It never displays the token value.

GitHub CLI 卡會操作官方 `gh` 帳戶儲存。**重新整理帳戶**會檢查已安裝版本，並為 `github.com` 執行安全狀態查詢；帳戶選擇器會顯示登入名稱、host、使用中狀態、驗證狀態、scope、憑證來源、Git 協定同非機密錯誤，永遠唔會顯示 token 值。

- **Login in terminal** opens an interactive terminal running the official browser login flow. Complete the browser authorization yourself, then return and refresh. Login is deliberately not a silent or headless operation. · **喺終端登入**會開互動終端，執行官方瀏覽器登入流程。請由你本人完成瀏覽器授權，再返嚟重新整理；登入刻意唔會靜默或者無介面執行。
- **Switch** activates the selected stored login for later `gh` commands. It does not alter any GitHub Desktop profile. · **切換**會將所選已儲存登入設為之後 `gh` 指令使用中；佢唔會改任何 GitHub Desktop 設定檔。
- **Logout** asks for confirmation, then removes that login from the local GitHub CLI credential store. It neither deletes a Desktop profile nor revokes the OAuth grant on GitHub. · **登出**會先要求確認，再由本機 GitHub CLI 憑證儲存移除該登入；佢唔會刪除 Desktop 設定檔，亦唔會撤銷 GitHub 上面嘅 OAuth 授權。

If `GH_TOKEN` or `GITHUB_TOKEN` is set, that environment credential takes priority over stored `gh` accounts. WinForge reports the override and disables login, switching, and logout until it is cleared; this avoids changing stored accounts while terminal commands would actually use the environment token.

如果已設定 `GH_TOKEN` 或 `GITHUB_TOKEN`，環境憑證會優先過 `gh` 已儲存帳戶。WinForge 會顯示呢個覆寫，並停用登入、切換同登出直至清除為止，避免終端指令實際用緊環境 token 時改動已儲存帳戶。

## Desktop profiles and CLI accounts are separate · Desktop 設定檔同 CLI 帳戶互相獨立

GitHub Desktop and GitHub CLI use different state and credential flows. Matching names are only a human convention: activating `CafePromenade` in Desktop does not run `gh auth switch`, and switching `gh` does not change Desktop's active callback profile. Likewise, removing a Desktop profile does not log out `gh`, and logging out `gh` does not remove Desktop data. Select both explicitly when a workflow uses both applications.

GitHub Desktop 同 GitHub CLI 使用唔同狀態同憑證流程。同名只係方便人理解：喺 Desktop 啟用 `CafePromenade` 唔會執行 `gh auth switch`，切換 `gh` 亦唔會改 Desktop 使用中 callback 設定檔。同樣，移除 Desktop 設定檔唔會登出 `gh`，登出 `gh` 亦唔會移除 Desktop 資料。如果工作流程同時用兩者，請分別明確選擇。

## Browser sign-in and deep links · 瀏覽器登入同深層連結

The last profile activated through its shortcut owns browser callbacks. WinForge routes only these GitHub Desktop schemes to it:

最後一次經捷徑啟用嘅設定檔會接收瀏覽器 callback。WinForge 只會將以下 GitHub Desktop scheme 轉送畀佢：

- `x-github-client`
- `github-windows`
- `x-github-desktop-auth`

Open the intended profile shortcut immediately before signing in or choosing **Open in GitHub Desktop**. Merely focusing an already-open GitHub Desktop window does not change callback ownership.

登入或者揀 **Open in GitHub Desktop** 之前，請先開一次你想用嘅設定檔捷徑。淨係將已開嘅 GitHub Desktop 視窗拉到最前，唔會改變 callback 所屬設定檔。

## LowLevel MCP background workflow · LowLevel MCP 背景工作流程

WinForge does not add specialized GitHub MCP tools. An agent can use the existing generic [LowLevel Computer Use MCP](https://github.com/codingmachineedge/lowlevel-computer-use-mcp) catalog to inspect or drive a normal-integrity GitHub Desktop window on an off-screen Windows desktop:

WinForge 唔會新增專用 GitHub MCP 工具。Agent 可以使用現有通用 [LowLevel Computer Use MCP](https://github.com/codingmachineedge/lowlevel-computer-use-mcp) catalog，喺離屏 Windows desktop 檢查或者驅動普通權限 GitHub Desktop 視窗：

1. `create_headless_desktop` — create a named off-screen desktop. · 建立具名離屏 desktop。
2. `launch_on_headless_desktop` — launch the selected profile's generated shortcut target or `WinForgeLauncher` profile command on that desktop. · 喺該 desktop 啟動所選設定檔捷徑目標或者 `WinForgeLauncher` 設定檔指令。
3. `list_headless_windows` — resolve the current window handle; never reuse a handle from an earlier run. · 取得目前視窗 handle；唔好重用上次運行嘅 handle。
4. `screenshot` with `hwnd` and, when needed, `output_path` — verify the off-screen window without moving it onto the visible desktop. · 用 `hwnd` 同按需要提供 `output_path` 擷取畫面，唔使將視窗搬去可見 desktop 都可以驗證。
5. Close the app/window, then call `close_headless_desktop` to release the desktop handle. · 關閉程式／視窗，再呼叫 `close_headless_desktop` 釋放 desktop handle。

For a non-GUI health check, use the generic `run_command` tool to invoke the installed reusable manager under the same Windows user:

如果只做無介面健康檢查，可以用通用 `run_command` 工具，以同一個 Windows 用戶執行已安裝嘅可重用管理器：

```jsonc
run_command {
  "params": {
    "command": "powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Command \"& (Join-Path $env:LOCALAPPDATA 'GitHubDesktopProfiles\\Manage-GitHubCliAccounts.ps1') -Action Status -Json\""
  }
}
```

The current LowLevel MCP schemas wrap each tool's arguments inside `params`; always inspect `list_tools` when replaying this flow against a newer server.

目前 LowLevel MCP schema 會將每個工具嘅參數包喺 `params` 入面；用新版 server 重播流程時，請先檢查 `list_tools`。

`Status` returns machine-readable, allowlisted account state and never returns token material. The reusable script also supports headless `List`, `Switch`, and `Logout` with `-Json`; state changes should name the exact login and use the controller's normal confirmation policy. `Login` always remains an interactive, visible browser flow and must not be simulated by typing credentials or tokens through MCP.

`Status` 會回傳機器可讀、經 allowlist 過濾嘅帳戶狀態，永遠唔會回傳 token 內容。可重用 script 亦支援用 `-Json` 無介面執行 `List`、`Switch` 同 `Logout`；改動狀態時要指定準確登入名稱，並跟隨控制器正常確認政策。`Login` 永遠係互動、可見嘅瀏覽器流程，唔可以經 MCP 模擬輸入憑證或者 token。

Run WinForge, `WinForgeLauncher`, GitHub Desktop, `gh`, and the LowLevel MCP host at normal integrity under the same signed-in Windows user. Do not substitute `run_command_as_admin`: elevated processes use a different security boundary and the profile/callback operations intentionally fail closed.

WinForge、`WinForgeLauncher`、GitHub Desktop、`gh` 同 LowLevel MCP host 必須用同一個已登入 Windows 用戶，以普通權限執行。唔好改用 `run_command_as_admin`：系統管理員程序屬於另一個安全邊界，而設定檔／callback 操作會刻意拒絕執行。

## Safety and isolation boundary · 安全同隔離範圍

This feature provides application-profile convenience isolation, not separate Windows security boundaries. Windows Credential Manager, SSH and GPG agents, system Git configuration, filesystem permissions, and repository folders remain shared. Use a different clone directory for each account, and use separate Windows accounts or virtual machines when credentials and files must be completely isolated.

呢個功能提供方便嘅 app 設定檔隔離，唔等於獨立 Windows 安全邊界。Windows 認證管理員、SSH／GPG agent、系統 Git 設定、檔案權限同儲存庫資料夾仍然共用。每個帳戶應該用唔同 clone 目錄；如果認證同檔案一定要完全隔離，請改用獨立 Windows 帳戶或者虛擬機器。

WinForge never reads, copies, prints, or stores GitHub access tokens for this feature. Setup, repair, launch, and protocol handling fail closed while WinForge or its launcher is elevated, because per-user desktop applications and callback handlers must run at normal integrity.

WinForge 唔會為呢個功能讀取、複製、列印或者儲存 GitHub access token。當 WinForge 或者啟動器以系統管理員身份執行時，建立、修復、啟動同 protocol 處理都會拒絕執行，因為每用戶桌面程式同 callback handler 必須以普通權限運行。

The WinForge CLI card refuses login, switching, and logout while `GH_TOKEN` or `GITHUB_TOKEN` overrides stored credentials. The reusable PowerShell manager follows the same precedence for the relevant GitHub or GitHub Enterprise variables: `Status` and `List` honor the override so their result matches the account `gh` would actually use, but expose only `EnvironmentOverride: true|false`, never the variable name or value. `Login`, `Switch`, and `Logout` fail clearly until the relevant override is cleared. `GH_DEBUG` is removed only from the child process. Neither path accepts a token parameter or calls token-reveal commands.

當 `GH_TOKEN` 或 `GITHUB_TOKEN` 蓋過已儲存憑證時，WinForge CLI 卡會拒絕登入、切換同登出。可重用 PowerShell 管理器會對相關 GitHub 或 GitHub Enterprise 變數跟隨同一優先次序：`Status` 同 `List` 會尊重覆寫，確保結果同 `gh` 實際使用帳戶一致，但只會顯示 `EnvironmentOverride: true|false`，永遠唔會顯示變數名稱或者值。相關覆寫清除之前，`Login`、`Switch` 同 `Logout` 會清楚拒絕執行；`GH_DEBUG` 只會喺子程序移除。兩條路徑都唔接受 token 參數，亦唔會呼叫顯示 token 嘅指令。

## Related · 相關

- [Git & GitHub](Git-and-GitHub.md) · Git 與 GitHub
- [Module Categories](Module-Categories.md) · 模組分類
- [Reusable profile scripts](https://github.com/codingmachineedge/github-desktop-profiles) · 可重用設定檔 script
