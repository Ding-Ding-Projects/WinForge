using System.Collections.Generic;

namespace WinForge.Services;

// 程式、開發與雲端章節（第二部分）· Apps, dev & cloud section (part 2).
public static partial class ManualContent
{
    private static List<ManualEntry> AppsEntriesPart2() => new()
    {
        new ManualEntry
        {
            Tag = "module.git", Glyph = "",
            TitleEn = "Git & GitHub", TitleZh = "Git 與 GitHub",
            SummaryEn = "A full Git client — stage, commit, push, branch, open pull requests and run gh commands — without leaving WinForge.",
            SummaryZh = "一個齊全嘅 Git 客戶端 — staging、commit、push、開分支、開 pull request 同跑 gh 指令，全部喺 WinForge 入面搞掂。",
            StepsEn = new[]
            {
                "On the left, click Add Repo or Scan Repos to register a local repository (or paste a URL and use Clone).",
                "In the Changes tab, review the diff, tick files to stage, type a summary, then click Commit or Commit & Push.",
                "Use the Branches tab to switch, merge or create branches, and to open a pull request (fills title and body).",
                "Open the Tools tab for the chunked uploader and a git / gh command runner with a searchable operations library.",
            },
            StepsZh = new[]
            {
                "喺左邊撳 Add Repo 或者 Scan Repos 登記本機儲存庫（或者貼個 URL 用 Clone 複製）。",
                "喺 Changes 分頁睇 diff、揀檔案 stage、打個 summary，然後撳 Commit 或者 Commit & Push。",
                "喺 Branches 分頁切換、merge 或者開新分支，亦可以開 pull request（會幫你填標題同內容）。",
                "想用分塊上載器同 git / gh 指令工具，去 Tools 分頁，嗰度有可搜尋嘅操作庫。",
            },
            TipEn = "Needs the git CLI; GitHub features use the gh CLI and your existing gh auth login (GitHub Desktop works as a fallback).",
            TipZh = "要安裝 git CLI；GitHub 功能要用 gh CLI 同你登咗入嘅 gh auth（冇咗都可以用 GitHub Desktop 頂住）。",
            Keywords = "git github commit push pull branch merge pull request pr gh clone diff stage uploader 版本控制 儲存庫 分支 提交",
        },
        new ManualEntry
        {
            Tag = "module.vscode", Glyph = "",
            TitleEn = "VS Code", TitleZh = "VS Code 編輯器",
            SummaryEn = "Drive Visual Studio Code from WinForge — open files and folders, diff, jump to a line, and manage extensions and profiles.",
            SummaryZh = "喺 WinForge 操控 VS Code — 開檔案同資料夾、比對、跳去指定行，同管理擴充功能同設定檔。",
            StepsEn = new[]
            {
                "Check the Status row for the detected VS Code version; flip the Insiders toggle if you use Insiders.",
                "Pick file, folder or workspace mode, then click an open button to launch it in VS Code.",
                "Use Compare/Diff to pick two files and open a diff, or Goto to open a file at a given line and column.",
                "In Extensions, search installed add-ons, export or import your list, or install one by ID.",
            },
            StepsZh = new[]
            {
                "喺 Status 列睇下偵測到嘅 VS Code 版本；如果你用 Insiders 就撳開 Insiders 開關。",
                "揀檔案、資料夾或者 workspace 模式，再撳開啟掣喺 VS Code 打開。",
                "用 Compare/Diff 揀兩個檔案開比對，或者用 Goto 開檔案直接跳去指定行同列。",
                "喺 Extensions 搜尋已安裝嘅擴充功能、匯出匯入清單，或者打 ID 直接安裝。",
            },
            TipEn = "Requires the code CLI on PATH — install VS Code (Microsoft.VisualStudioCode) via winget if it isn't found.",
            TipZh = "要 PATH 上有 code CLI — 搵唔到嘅話用 winget 裝 VS Code（Microsoft.VisualStudioCode）。",
            Keywords = "vscode vs code editor diff compare goto line extension install profile tunnel insiders 編輯器 擴充功能 比對 設定",
        },
        new ManualEntry
        {
            Tag = "module.aiagents", Glyph = "",
            TitleEn = "AI Agents", TitleZh = "AI 代理",
            SummaryEn = "Edit the config files of coding agents like Claude Code, Codex and opencode — model, provider, API key and instructions — in one place.",
            SummaryZh = "喺一個地方編輯 Claude Code、Codex、opencode 等編程代理嘅設定檔 — 模型、供應商、API key 同指示。",
            StepsEn = new[]
            {
                "Check the Node.js bar at the top — some agents need Node.js to run.",
                "Pick a working directory so project-level agent files (like CLAUDE.md or AGENTS.md) resolve correctly.",
                "Expand an agent card to load its config (settings.json, config.toml, opencode.json) into the editor.",
                "Fill the model, provider and API key fields or edit the raw text, then save.",
            },
            StepsZh = new[]
            {
                "睇下頂部嘅 Node.js 列 — 部分代理要 Node.js 先行到。",
                "揀個工作目錄，咁專案層級嘅代理檔案（例如 CLAUDE.md 或 AGENTS.md）先搵得到。",
                "撳開某個代理卡片，將佢嘅設定檔（settings.json、config.toml、opencode.json）載入編輯器。",
                "填模型、供應商同 API key 欄位，或者直接改原始文字，然後儲存。",
            },
            TipEn = "Your API keys live in each agent's own config file under your home folder — keep those files private.",
            TipZh = "你嘅 API key 係儲喺各個代理喺 home 資料夾自己嘅設定檔入面 — 記住唔好亂咁畀人睇。",
            Keywords = "ai agent claude code codex opencode config api key model provider node settings.json 代理 編程 設定 安裝",
        },
        new ManualEntry
        {
            Tag = "module.resume", Glyph = "",
            TitleEn = "Resume Writer", TitleZh = "履歷與求職信寫手",
            SummaryEn = "Tailor your resume and cover letter to a job description using an AI coding agent, then export the results.",
            SummaryZh = "用 AI 代理將你嘅履歷同求職信度身改成切合某份職位描述，然後匯出。",
            StepsEn = new[]
            {
                "Pick or create a base resume in the library, then paste or edit your base resume text.",
                "Paste the job description, choose an AI agent and a tone.",
                "Click Generate; watch the progress ring, then read the tailored resume and cover letter side by side.",
                "Edit either pane, then use Save History, Export Resume or Export Cover Letter (.md / .txt).",
            },
            StepsZh = new[]
            {
                "喺資料庫揀或者新增一份底稿履歷，再貼上或者改你嘅履歷文字。",
                "貼上職位描述，揀個 AI 代理同語氣。",
                "撳 Generate；睇住個進度圈，跟住左右對照睇度身改好嘅履歷同求職信。",
                "兩邊都改得，改好就用 Save History、Export Resume 或者 Export Cover Letter 匯出（.md／.txt）。",
            },
            TipEn = "Generation uses the AI agent you set up in AI Agents — make sure that agent has a working API key first.",
            TipZh = "生成係用你喺 AI 代理度設定好嘅代理 — 記住嗰個代理要有個用得嘅 API key 先。",
            Keywords = "resume cv cover letter job tailor ai generate export docx markdown history tone 履歷 求職信 應徵 度身 生成 匯出",
        },
        new ManualEntry
        {
            Tag = "module.ollama", Glyph = "",
            TitleEn = "Ollama", TitleZh = "本地大模型",
            SummaryEn = "Manage and chat with local LLMs through Ollama — pull models, see what's running, and tune sampling.",
            SummaryZh = "經 Ollama 管理同同本地大模型傾偈 — 下載模型、睇邊個 model 跑緊，同調校取樣參數。",
            StepsEn = new[]
            {
                "Confirm the connection bar points at your Ollama server (default http://localhost:11434), then Refresh.",
                "In the Pull tab, type a model name (e.g. llama3, qwen2) and click Pull to download it.",
                "Use the Models tab to list or delete models, and the Running tab to see and unload loaded models.",
                "Open the Chat tab, pick a model, and adjust system prompt, temperature, top_p and context size on the right.",
            },
            StepsZh = new[]
            {
                "確認連線列指住你嘅 Ollama 伺服器（預設 http://localhost:11434），然後撳 Refresh。",
                "喺 Pull 分頁打個模型名（例如 llama3、qwen2）再撳 Pull 下載。",
                "用 Models 分頁列出或者刪除模型，Running 分頁睇住同卸載已載入嘅模型。",
                "開 Chat 分頁揀個模型，喺右邊調 system prompt、temperature、top_p 同 context size。",
            },
            TipEn = "Requires Ollama installed and running — get it with winget install Ollama.Ollama if the connection fails.",
            TipZh = "要裝咗 Ollama 而且行緊先得 — 連唔到嘅話用 winget install Ollama.Ollama 裝。",
            Keywords = "ollama llm local model pull serve chat temperature top_p context llama qwen gemma 本地 模型 下載 大模型 聊天",
        },
        new ManualEntry
        {
            Tag = "module.aichat", Glyph = "",
            TitleEn = "AI Chat", TitleZh = "AI 聊天",
            SummaryEn = "A multi-provider chat workspace — keep conversations, switch models, and tune the system prompt and sampling.",
            SummaryZh = "一個多供應商嘅聊天工作區 — 儲對話、切換模型，同調 system prompt 同取樣設定。",
            StepsEn = new[]
            {
                "Click New Chat, then pick a provider and model from the dropdowns in the header.",
                "Click the gear to set a system prompt, temperature and max tokens.",
                "Type in the composer (Enter sends, Shift+Enter adds a line); attach files if needed.",
                "Switch between saved conversations in the left sidebar, or search them from the search box.",
            },
            StepsZh = new[]
            {
                "撳 New Chat，然後喺頂部嘅下拉揀供應商同模型。",
                "撳齒輪設定 system prompt、temperature 同 max tokens。",
                "喺輸入框打字（Enter 送出，Shift+Enter 換行）；需要嘅話可以夾檔案。",
                "喺左邊側欄切換已儲存嘅對話，或者用搜尋框搵返。",
            },
            TipEn = "Cloud providers need an API key (set up via AI Agents / In-App Login); local models route through Ollama.",
            TipZh = "雲端供應商要 API key（喺 AI 代理／內置登入度設定）；本地模型就行 Ollama。",
            Keywords = "ai chat llm openai openrouter ollama lm studio model system prompt temperature conversation 聊天 對話 本機模型 提示",
        },
        new ManualEntry
        {
            Tag = "module.cloudflare", Glyph = "",
            TitleEn = "Cloudflare & Tunnel", TitleZh = "Cloudflare 與 Tunnel",
            SummaryEn = "Run cloudflared from a friendly panel — spin up quick tunnels, manage Access and DNS-over-HTTPS, and run common operations.",
            SummaryZh = "用一個友善嘅面板跑 cloudflared — 開 quick tunnel、管理 Access 同 DNS-over-HTTPS，同跑常用操作。",
            StepsEn = new[]
            {
                "If the engine bar warns cloudflared is missing, install it first.",
                "Use the Quick actions buttons for common jobs like starting a quick tunnel.",
                "Search the operations list to find and run a specific cloudflared command.",
                "Read the command output in the collapsible output pane below.",
            },
            StepsZh = new[]
            {
                "如果引擎列提示搵唔到 cloudflared，先裝咗佢。",
                "用 Quick actions 嘅掣做常見工作，例如開 quick tunnel。",
                "喺操作清單搜尋，搵到就跑指定嘅 cloudflared 指令。",
                "喺下面可摺疊嘅輸出區睇指令結果。",
            },
            TipEn = "Account-level actions need you signed in to Cloudflare; sign in via In-App Login if prompted.",
            TipZh = "帳戶層面嘅操作要你登咗入 Cloudflare；有提示嘅話喺內置登入度登。",
            Keywords = "cloudflare cloudflared tunnel quick tunnel trycloudflare access warp dns over https doh zero trust 隧道 連線",
        },
        new ManualEntry
        {
            Tag = "module.weblogin", Glyph = "",
            TitleEn = "In-App Login", TitleZh = "內置登入",
            SummaryEn = "An embedded browser that signs you in to services and captures the resulting token or cookies into a profile.",
            SummaryZh = "一個內置瀏覽器，幫你登入各種服務，並將拎到嘅權杖或者 cookie 擷取入一個 profile。",
            StepsEn = new[]
            {
                "Type or pick the service address in the toolbar and click Go to load its sign-in page.",
                "Complete the normal login inside the embedded browser.",
                "Pick the matching Provider, name a Profile, then click Capture to save the session.",
                "Use Clear to wipe a profile's data when you want to log out.",
            },
            StepsZh = new[]
            {
                "喺工具列打或者揀服務嘅網址，撳 Go 載入登入頁。",
                "喺內置瀏覽器入面正常咁登入。",
                "揀返啱嘅 Provider、改個 Profile 名，再撳 Capture 儲低個 session。",
                "想登出嘅話撳 Clear 清走嗰個 profile 嘅資料。",
            },
            TipEn = "Each profile is isolated under LocalAppData\\WinForge\\WebView2; captured secrets are never logged. Needs the WebView2 Runtime (ships with Windows 11).",
            TipZh = "每個 profile 都獨立儲喺 LocalAppData\\WinForge\\WebView2，擷取到嘅機密唔會寫入記錄。要有 WebView2 Runtime（Windows 11 已內置）。",
            Keywords = "login sign in oauth webview2 browser token cookie session capture profile provider 登入 內置 瀏覽器 認證 憑證 權杖",
        },
        new ManualEntry
        {
            Tag = "module.ssh", Glyph = "",
            TitleEn = "SSH Toolset", TitleZh = "SSH 工具",
            SummaryEn = "Save SSH connection profiles, open a live terminal, generate keys, deploy them for passwordless login, and browse files over SFTP.",
            SummaryZh = "儲 SSH 連線設定檔、開即時終端機、產生金鑰、部署做免密碼登入，同經 SFTP 瀏覽檔案。",
            StepsEn = new[]
            {
                "In Profiles, click New, fill Host, Port, User and an auth method (password or private key), then Save.",
                "Use Connect to open a session, or the Live Terminal tab for a full ConPTY terminal.",
                "In Keys, generate an ed25519 or RSA key, then use Deploy on a profile for passwordless login.",
                "Open the SFTP tab to browse, upload, download and manage remote files.",
            },
            StepsZh = new[]
            {
                "喺 Profiles 撳 New，填 Host、Port、User 同認證方式（密碼或者私鑰），再撳 Save。",
                "撳 Connect 開 session，或者去 Live Terminal 分頁用完整嘅 ConPTY 終端機。",
                "喺 Keys 產生 ed25519 或者 RSA 金鑰，再喺某個 profile 撳 Deploy 做免密碼登入。",
                "開 SFTP 分頁瀏覽、上載、下載同管理遠端檔案。",
            },
            TipEn = "Passwords and key passphrases are stored DPAPI-encrypted (current-user). OpenSSH ships with Windows 11, so no install is usually needed.",
            TipZh = "密碼同金鑰 passphrase 會用 DPAPI 加密（目前用戶）儲存。OpenSSH 喺 Windows 11 已內置，通常唔使另外裝。",
            Keywords = "ssh sftp scp terminal key keygen ed25519 rsa passwordless deploy profile conpty dpapi 終端機 遠端 金鑰 免密碼 連線",
        },
        new ManualEntry
        {
            Tag = "module.packer", Glyph = "",
            TitleEn = "Packer (Image Builder)", TitleZh = "Packer（映像建置器）",
            SummaryEn = "A front end for HashiCorp Packer — pick a template, set variables, and run init, validate, fmt and build with live console output.",
            SummaryZh = "HashiCorp Packer 嘅前端 — 揀範本、設定變數，跑 init、validate、fmt 同 build，仲有即時 console 輸出。",
            StepsEn = new[]
            {
                "Pick your working folder so WinForge can list templates and var-files.",
                "Select a template and the build targets you want.",
                "Add variables and var-files in the Variables editor.",
                "Run Init, Validate, Fmt then Build; watch the console and use Save Log to keep the output.",
            },
            StepsZh = new[]
            {
                "揀你嘅工作資料夾，咁 WinForge 先列得到範本同 var-file。",
                "揀一個範本同你想 build 嘅 target。",
                "喺 Variables 編輯器加變數同 var-file。",
                "順序跑 Init、Validate、Fmt 再 Build；睇住 console，用 Save Log 留低輸出。",
            },
            TipEn = "Requires the Packer CLI — install it with winget install Hashicorp.Packer if the engine bar says it's missing.",
            TipZh = "要 Packer CLI — 引擎列話搵唔到嘅話用 winget install Hashicorp.Packer 裝。",
            Keywords = "packer hashicorp image builder template hcl init validate fmt build variables var-file provisioner 映像 範本 建置 變數",
        },
        new ManualEntry
        {
            Tag = "module.worldmonitor", Glyph = "",
            TitleEn = "World Monitor", TitleZh = "世界監察",
            SummaryEn = "An embedded live dashboard of world news, geopolitics, finance, commodities and energy, with several variants.",
            SummaryZh = "一個內置嘅即時儀表板，睇世界新聞、地緣政治、金融、商品同能源，仲有幾個版本可揀。",
            StepsEn = new[]
            {
                "Pick a variant from the dropdown (world, tech, finance, commodity, energy or happy).",
                "Use Back, Forward, Home and Reload to move around the dashboard.",
                "Zoom in or out with the zoom controls to fit your screen.",
                "Click Open in browser to pop the dashboard out into your default browser.",
            },
            StepsZh = new[]
            {
                "喺下拉揀一個版本（world、tech、finance、commodity、energy 或者 happy）。",
                "用 Back、Forward、Home 同 Reload 喺儀表板度走嚟走去。",
                "用縮放掣放大縮細，啱返你個螢幕。",
                "撳 Open in browser 將儀表板彈去你預設嘅瀏覽器。",
            },
            TipEn = "It's a web embed using the WebView2 Runtime (built into Windows 11); no install or login is needed.",
            TipZh = "佢係用 WebView2 Runtime（Windows 11 內置）嵌入嘅網頁；唔使裝嘢又唔使登入。",
            Keywords = "world monitor news geopolitics finance commodity energy dashboard globe webview variant 世界 監察 新聞 金融 儀表板",
        },
        new ManualEntry
        {
            Tag = "module.webcloner", Glyph = "",
            TitleEn = "Website Cloner", TitleZh = "網站複製器",
            SummaryEn = "Download and rebuild a website locally, either with a native scraper or with an AI agent that reverse-engineers the design.",
            SummaryZh = "將一個網站下載並喺本機重建，可以用原生抓取器，又或者用 AI 代理逆向重現個設計。",
            StepsEn = new[]
            {
                "Read the Terms-of-Service disclaimer, then paste the URL and pick a destination folder.",
                "Set options like Include assets and Rendered HTML, and choose Native or AI mode.",
                "In AI mode, pick an AI agent; then click Clone and watch the progress log.",
                "When done, use Open folder to see the files, or check the design-token summary card.",
            },
            StepsZh = new[]
            {
                "睇咗條款免責聲明，然後貼個 URL 同揀個目的地資料夾。",
                "設定選項，例如 Include assets 同 Rendered HTML，再揀 Native 或者 AI 模式。",
                "AI 模式要揀個 AI 代理；之後撳 Clone，睇住進度記錄。",
                "完成後撳 Open folder 睇檔案，或者睇下 design-token 摘要卡。",
            },
            TipEn = "AI mode needs a coding agent (e.g. opencode / Claude CLI) set up in AI Agents; native mode works on its own.",
            TipZh = "AI 模式要喺 AI 代理度設定好編程代理（例如 opencode／Claude CLI）；Native 模式自己就用得。",
            Keywords = "website cloner clone copy scrape download assets html css mirror rebuild ai agent design tokens 網站 複製 抓取 下載 重建",
        },
        new ManualEntry
        {
            Tag = "module.pgadmin", Glyph = "",
            TitleEn = "Postgres Tool", TitleZh = "Postgres 工具 / pgAdmin",
            SummaryEn = "Connect to PostgreSQL, browse the schema tree, and run SQL with a results grid — or launch the full pgAdmin app.",
            SummaryZh = "連接 PostgreSQL、瀏覽結構樹，同用結果格跑 SQL — 又或者啟動完整嘅 pgAdmin 應用程式。",
            StepsEn = new[]
            {
                "Click New connection, fill Host, Port (5432), Database and User, then Test and Connect.",
                "Browse Databases, Schemas and Tables in the left object tree.",
                "Type SQL in the editor and click Run; results appear in the grid below.",
                "Use Export CSV to save results, or Launch pgAdmin for advanced administration.",
            },
            StepsZh = new[]
            {
                "撳 New connection，填 Host、Port（5432）、Database 同 User，再撳 Test 同 Connect。",
                "喺左邊物件樹瀏覽 Databases、Schemas 同 Tables。",
                "喺編輯器打 SQL 再撳 Run；結果會喺下面個格顯示。",
                "用 Export CSV 儲結果，或者撳 Launch pgAdmin 做進階管理。",
            },
            TipEn = "Saved passwords are stored DPAPI-encrypted; untick Save password to be prompted each time.",
            TipZh = "儲低嘅密碼會用 DPAPI 加密；唔想儲就唔好剔 Save password，每次叫你輸入。",
            Keywords = "postgres postgresql pgadmin sql database query connection schema table npgsql csv 資料庫 數據庫 查詢 表 連線",
        },
        new ManualEntry
        {
            Tag = "module.filezilla", Glyph = "",
            TitleEn = "FTP / SFTP", TitleZh = "FTP／SFTP 檔案傳輸",
            SummaryEn = "A dual-pane FTP, FTPS and SFTP client — quickconnect or save sites, then transfer files with a resumable queue.",
            SummaryZh = "一個雙窗格嘅 FTP、FTPS 同 SFTP 客戶端 — quickconnect 或者儲站台，然後用可續傳佇列傳檔案。",
            StepsEn = new[]
            {
                "Use the Quickconnect bar (protocol, host, port, user, password) or save a site in Site Manager.",
                "Click Connect; the local pane is on the left, the remote pane on the right.",
                "Drag, or use the transfer arrows / right-click Upload and Download to move files.",
                "Watch the transfer queue at the bottom; tick Resume to continue interrupted transfers.",
            },
            StepsZh = new[]
            {
                "用 Quickconnect 列（協定、host、port、user、密碼），或者喺 Site Manager 儲低個站台。",
                "撳 Connect；左邊係本機窗格，右邊係遠端窗格。",
                "拖拉，或者用傳輸箭咀／右擊 Upload 同 Download 嚟搬檔案。",
                "睇住底部嘅傳輸佇列；剔 Resume 可以續傳斷咗嘅傳輸。",
            },
            TipEn = "Saved site credentials and SFTP key paths are stored DPAPI-encrypted; leave the password blank to be prompted on connect.",
            TipZh = "儲低嘅站台憑證同 SFTP 金鑰路徑會用 DPAPI 加密；密碼留空就連線嗰陣先叫你輸入。",
            Keywords = "ftp sftp ftps filezilla file transfer site manager upload download queue resume tls private key 檔案傳輸 上載 下載 站台 續傳",
        },
        new ManualEntry
        {
            Tag = "module.bitwarden", Glyph = "",
            TitleEn = "Bitwarden Vault", TitleZh = "Bitwarden 密碼庫",
            SummaryEn = "Unlock your Bitwarden vault inside WinForge to search logins, copy passwords and TOTP codes, and generate new passwords.",
            SummaryZh = "喺 WinForge 入面解鎖你嘅 Bitwarden 密碼庫，搜尋登入、複製密碼同 TOTP 驗證碼，同產生新密碼。",
            StepsEn = new[]
            {
                "Optionally set a self-hosted server URL, then enter your email and master password (plus 2FA) and click Auth.",
                "Search the vault and click an item to see its username, password, TOTP and notes.",
                "Use the copy and reveal buttons to grab a secret; the clipboard auto-clears after a timeout.",
                "Click Generate for a new password or passphrase; use Lock or Logout when you're done.",
            },
            StepsZh = new[]
            {
                "需要嘅話填自架伺服器 URL，再輸入 email 同主密碼（連 2FA）撳 Auth。",
                "搜尋密碼庫，撳一項就睇到佢嘅帳號、密碼、TOTP 同備註。",
                "用複製同顯示掣攞機密；剪貼簿過咗時限會自動清走。",
                "撳 Generate 整個新密碼或者 passphrase；用完撳 Lock 或者 Logout。",
            },
            TipEn = "Needs the Bitwarden CLI (winget install Bitwarden.CLI). The session key is kept in memory only and never written to disk.",
            TipZh = "要 Bitwarden CLI（winget install Bitwarden.CLI）。session key 淨係留喺記憶體，唔會寫落硬碟。",
            Keywords = "bitwarden vault password manager unlock master password totp 2fa generate passphrase self-hosted vaultwarden 密碼庫 解鎖 主密碼 驗證碼",
        },
        new ManualEntry
        {
            Tag = "module.quicktype", Glyph = "",
            TitleEn = "quicktype", TitleZh = "JSON 轉型別",
            SummaryEn = "Turn JSON, CSV or XML samples into typed code — TypeScript, C#, Python, Go and many more.",
            SummaryZh = "將 JSON、CSV 或者 XML 樣本轉成有型別嘅程式碼 — TypeScript、C#、Python、Go 等等。",
            StepsEn = new[]
            {
                "Paste your sample into the input pane (or Load File) and pick the input kind.",
                "Choose a target language, set a top-level type name, and tick Just Types if you only want the types.",
                "For C#, set the namespace and framework option if needed, then click Generate.",
                "Copy or Save the generated code from the output pane.",
            },
            StepsZh = new[]
            {
                "將樣本貼入輸入區（或者 Load File），再揀輸入種類。",
                "揀目標語言、改個 top-level 型別名，淨係要型別嘅話剔 Just Types。",
                "如果係 C#，需要嘅話設定 namespace 同 framework，然後撳 Generate。",
                "喺輸出區 Copy 或者 Save 產生出嚟嘅程式碼。",
            },
            TipEn = "Needs Node.js and the quicktype CLI — use the Install button in the CLI bar (npm install -g quicktype) if it's missing.",
            TipZh = "要 Node.js 同 quicktype CLI — 搵唔到嘅話撳 CLI 列嘅 Install 掣（npm install -g quicktype）。",
            Keywords = "quicktype json schema csv xml typescript csharp python go rust java code generator types namespace 程式碼產生 型別 轉換",
        },
        new ManualEntry
        {
            Tag = "module.aws", Glyph = "",
            TitleEn = "AWS Manager", TitleZh = "AWS 管理中心",
            SummaryEn = "An AWS Console-style manager with isolated account context, resource discovery, a 149-service catalog, native S3 and EC2 controls, Cloud Control lifecycle APIs, and an optional CLI workbench.",
            SummaryZh = "AWS Console 式管理中心，整合隔離帳戶情境、資源探索、149 個服務目錄、原生 S3 同 EC2 控制、Cloud Control 生命週期 API，同選用 CLI 工作台。",
            StepsEn = new[]
            {
                "Choose a shared AWS Profile and Region in the top context bar, then use Who am I to verify the account before changing resources.",
                "Use Console home and All resources to search Resource Explorer inventory; if it is unavailable, WinForge labels the narrower tag-inventory fallback.",
                "Open All services to browse 149 curated services plus live services discovered from the installed AWS CLI. Most services currently use the shared resource workspace.",
                "Use the native S3 workspace for buckets, objects, transfers, versioning, encryption, public-access settings, policies, lifecycle, CORS and tags.",
                "Use the native EC2 workspace to filter and inspect instances in the selected Region. Start, stop and reboot require review; termination requires the exact instance ID.",
                "Open CLI workbench only when you need exact command coverage, streaming output, generated forms, history or favorites.",
            },
            StepsZh = new[]
            {
                "喺頂部情境列揀 shared AWS Profile 同 Region，再用「我係邊個」核對帳戶，先至改資源。",
                "用 Console 首頁同所有資源搜尋 Resource Explorer 清單；如果用唔到，WinForge 會清楚標示範圍較窄嘅標籤清單後備。",
                "開所有服務，瀏覽 149 個精選服務同由已安裝 AWS CLI 即時探索嘅服務。其他大部分服務目前會用共用資源工作區。",
                "用原生 S3 工作區管理儲存桶、物件、傳輸、版本控制、加密、公開存取設定、政策、生命週期、CORS 同標籤。",
                "用原生 EC2 工作區篩選同檢視所選 Region 嘅執行個體。啟動、停止同重新啟動要先覆核；終止就要輸入完整執行個體 ID。",
                "只有需要精確指令覆蓋、即時輸出、生成表單、歷史或者收藏時，先開 CLI 工作台。",
            },
            TipEn = "The primary manager uses AWS SDK for .NET v4 and does not require aws.exe. WinForge stores only non-secret context; credentials remain with standard AWS providers. IAM permissions still apply, and raw CLI commands may be destructive.",
            TipZh = "主要管理中心用 AWS SDK for .NET v4，唔需要 aws.exe。WinForge 只保存非機密情境；憑證會留喺標準 AWS provider。IAM 權限仍然生效，而原始 CLI 指令可能有破壞性。",
            Keywords = "aws amazon manager console sdk cli s3 ec2 instance start stop reboot terminate resource explorer cloud control crudl services profile credentials region sso configure favorites history 雲端 管理中心 命令列 設定檔 憑證 區域 資源 執行個體",
        },
        new ManualEntry
        {
            Tag = "module.cmdnotfound", Glyph = "",
            TitleEn = "Command Not Found", TitleZh = "搵唔到指令",
            SummaryEn = "Set up PowerShell's WinGet Command Not Found suggestions, so a mistyped command suggests the package that provides it.",
            SummaryZh = "設定 PowerShell 嘅 WinGet「搵唔到指令」建議，咁打錯指令就會提你邊個套件有得裝。",
            StepsEn = new[]
            {
                "Check the status card for pwsh, the CommandNotFound module and the profile hook.",
                "Click Install or Enable on any row that isn't ready yet.",
                "Type a command (e.g. pyton) and click Test to see the suggestion in the console.",
                "Use Winget lookup to search for a package, or Open Editor to view your PowerShell profile.",
            },
            StepsZh = new[]
            {
                "喺狀態卡睇 pwsh、CommandNotFound 模組同設定檔掛鈎嘅狀態。",
                "邊行未準備好就撳嗰行嘅 Install 或者 Enable。",
                "打個指令（例如 pyton）撳 Test，喺 console 睇建議。",
                "用 Winget lookup 搜尋套件，或者撳 Open Editor 睇你嘅 PowerShell 設定檔。",
            },
            TipEn = "Requires PowerShell 7+ and the Microsoft.WinGet.CommandNotFound module; the feature is wired into your PowerShell profile.",
            TipZh = "要 PowerShell 7+ 同 Microsoft.WinGet.CommandNotFound 模組；個功能會駁入你嘅 PowerShell 設定檔。",
            Keywords = "command not found cmdnotfound winget suggest powershell pwsh profile module enable disable hook package 搵唔到指令 建議 套件 設定檔 啟用",
        },
        new ManualEntry
        {
            Tag = "module.configbackup", Glyph = "",
            TitleEn = "Config & Backup", TitleZh = "設定與備份",
            SummaryEn = "Snapshot and back up your WinForge settings to a local Git history, export portable bundles, and auto-sync on a schedule.",
            SummaryZh = "將你嘅 WinForge 設定快照同備份去本機 Git 歷史、匯出可攜帶捆綁，同按排程自動同步。",
            StepsEn = new[]
            {
                "In Config snapshots, type a message and click Take Snapshot to commit a point you can restore.",
                "Use Export to make a portable bundle; tick include secrets and set a password to encrypt them.",
                "Capture extras with Export Registry, Export Winget or Backup Taskbar.",
                "Set up Auto-sync (every N minutes/hours/days), optionally add a remote URL, and use Push Now or Sync Now.",
            },
            StepsZh = new[]
            {
                "喺 Config snapshots 打個訊息撳 Take Snapshot，commit 一個可以還原嘅點。",
                "用 Export 整個可攜帶捆綁；剔包含機密再設個密碼將佢哋加密。",
                "用 Export Registry、Export Winget 或者 Backup Taskbar 擷取額外嘅嘢。",
                "設定 Auto-sync（每 N 分鐘／小時／日），需要嘅話加個遠端 URL，再用 Push Now 或者 Sync Now。",
            },
            TipEn = "Needs the git CLI; snapshots live in a local repo under %LOCALAPPDATA%\\WinForge\\snapshots and bundled secrets are password-encrypted.",
            TipZh = "要 git CLI；快照儲喺 %LOCALAPPDATA%\\WinForge\\snapshots 嘅本機 repo，捆綁入面嘅機密會用密碼加密。",
            Keywords = "config backup snapshot restore export import bundle git schedule auto-sync mirror registry winget secrets encrypt 設定 備份 快照 還原 排程 加密",
        },
    };
}
