using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

public static class DevTerminalTweaks
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // --- winget (20) ---
        Tweak.Cmd("dev.winget.search", "Search a package", "搜尋套件",
            "Search the winget repositories for a package by name.", "用 winget 喺套件庫度按名搜尋一個套件。",
            "Search", "搜尋", "winget search 7zip --accept-source-agreements",
            keywords: "winget,search,搜尋,find"),
        
        Tweak.Cmd("dev.winget.install", "Install by ID", "按 ID 安裝",
            "Install a specific package by its winget package identifier.", "用 winget 套件識別碼安裝指定套件。",
            "Install", "安裝", "winget install --id 7zip.7zip --exact --accept-source-agreements --accept-package-agreements",
            keywords: "winget,install,安裝"),
        
        Tweak.Cmd("dev.winget.upgrade-all", "Upgrade all apps", "更新所有應用程式",
            "Upgrade every installed package that has an update available.", "更新所有有新版本嘅已安裝套件。",
            "Upgrade", "更新", "winget upgrade --all --include-unknown --accept-source-agreements --accept-package-agreements",
            requiresAdmin: true, keywords: "winget,upgrade,update,更新"),
        
        Tweak.Cmd("dev.winget.list", "List installed", "列出已安裝",
            "List every package winget knows is installed on this machine.", "列出 winget 喺呢部機認得嘅所有已安裝套件。",
            "List", "列出", "winget list --accept-source-agreements",
            keywords: "winget,list,installed,已安裝"),
        
        Tweak.Cmd("dev.winget.upgradable", "List upgradable", "列出可更新",
            "Show only the installed packages that have an upgrade available.", "淨係顯示有新版本可以更新嘅已安裝套件。",
            "Check", "檢查", "winget upgrade --include-unknown --accept-source-agreements",
            keywords: "winget,upgrade,outdated,可更新"),
        
        Tweak.Cmd("dev.winget.export", "Export installed list", "匯出已安裝清單",
            "Export the list of installed packages to a JSON file on your desktop.", "將已安裝套件清單匯出做 JSON 檔到桌面。",
            "Export", "匯出", "winget export --output \"%USERPROFILE%\\Desktop\\winget-packages.json\" --accept-source-agreements",
            keywords: "winget,export,backup,匯出"),
        
        Tweak.Cmd("dev.winget.import", "Import package list", "匯入套件清單",
            "Install every package listed in an exported JSON file from your desktop.", "安裝桌面上匯出 JSON 檔入面列嘅所有套件。",
            "Import", "匯入", "winget import --import-file \"%USERPROFILE%\\Desktop\\winget-packages.json\" --accept-source-agreements --accept-package-agreements --ignore-unavailable",
            requiresAdmin: true, keywords: "winget,import,restore,匯入"),
        
        Tweak.Cmd("dev.winget.uninstall", "Uninstall a package", "解除安裝套件",
            "Uninstall an example package (7-Zip) by its identifier.", "用識別碼解除安裝示範套件（7-Zip）。",
            "Uninstall", "解除安裝", "winget uninstall --id 7zip.7zip --exact",
            destructive: true, keywords: "winget,uninstall,remove,解除安裝"),
        
        Tweak.Cmd("dev.winget.show", "Show package info", "顯示套件資料",
            "Show detailed metadata for a package, including version and homepage.", "顯示套件嘅詳細資料，包括版本同主頁。",
            "Show", "顯示", "winget show --id Microsoft.PowerToys --accept-source-agreements",
            keywords: "winget,show,info,資料"),
        
        Tweak.Cmd("dev.winget.sources", "List sources", "列出來源",
            "List all configured package sources winget pulls from.", "列出 winget 用緊嘅所有套件來源。",
            "Sources", "來源", "winget source list",
            keywords: "winget,source,sources,來源"),
        
        Tweak.Cmd("dev.winget.update-sources", "Update sources", "更新來源",
            "Refresh the local cache of all winget package sources.", "重新整理所有 winget 套件來源嘅本機快取。",
            "Update", "更新", "winget source update",
            keywords: "winget,source,update,refresh,更新來源"),
        
        Tweak.Cmd("dev.winget.pin", "Pin a package", "釘選套件",
            "Pin an example package so winget skips it during upgrades.", "釘選示範套件，等 winget 更新時跳過佢。",
            "Pin", "釘選", "winget pin add --id Microsoft.PowerToys --accept-source-agreements",
            keywords: "winget,pin,hold,釘選"),
        
        Tweak.Cmd("dev.winget.list-pins", "List pins", "列出釘選",
            "List every package currently pinned from upgrades.", "列出而家所有被釘選、唔會更新嘅套件。",
            "List Pins", "列出釘選", "winget pin list",
            keywords: "winget,pin,list,釘選"),
        
        Tweak.Cmd("dev.winget.self-check", "Check winget version", "檢查 winget 版本",
            "Show the installed winget client version to confirm it is present.", "顯示已安裝嘅 winget 用戶端版本，確認佢存在。",
            "Version", "版本", "winget --version",
            keywords: "winget,version,self,版本"),
        
        Tweak.Cmd("dev.winget.repair", "Repair a package", "修復套件",
            "Attempt to repair an installed example package using its installer.", "用安裝程式嘗試修復一個已安裝嘅示範套件。",
            "Repair", "修復", "winget repair --id Microsoft.PowerToys --accept-source-agreements",
            requiresAdmin: true, keywords: "winget,repair,fix,修復"),
        
        Tweak.Cmd("dev.winget.validate", "Validate a manifest", "驗證資訊清單",
            "Validate a winget manifest file for syntax and schema errors.", "驗證 winget 資訊清單檔嘅語法同結構錯誤。",
            "Validate", "驗證", "winget validate --manifest \"%USERPROFILE%\\Desktop\\manifest.yaml\"",
            keywords: "winget,validate,manifest,驗證"),
        
        Tweak.Cmd("dev.winget.download", "Download only", "只下載",
            "Download a package installer without installing it, saving to your desktop.", "只下載套件安裝程式而唔安裝，存去桌面。",
            "Download", "下載", "winget download --id Microsoft.PowerToys --download-directory \"%USERPROFILE%\\Desktop\" --accept-source-agreements --accept-package-agreements",
            keywords: "winget,download,offline,下載"),
        
        Tweak.Cmd("dev.winget.list-versions", "List with versions", "列出連版本",
            "List installed packages that have an upgrade, including their versions.", "列出有更新嘅已安裝套件連版本，方便睇邊個有更新。",
            "List", "列出", "winget list --upgrade-available --include-unknown --accept-source-agreements",
            keywords: "winget,list,version,版本"),
        
        Tweak.Cmd("dev.winget.search-tag", "Search by tag", "按標籤搜尋",
            "Search the repositories for packages that carry a given tag.", "喺套件庫度搜尋帶有指定標籤嘅套件。",
            "Search Tag", "搜尋標籤", "winget search --tag editor --accept-source-agreements",
            keywords: "winget,search,tag,標籤"),
        
        Tweak.Cmd("dev.winget.show-versions", "Show package versions", "顯示套件版本",
            "List every available version of a specific package.", "列出指定套件所有可用嘅版本。",
            "Versions", "版本", "winget show --id Microsoft.PowerToys --versions --accept-source-agreements",
            keywords: "winget,show,versions,版本"),
        
        Tweak.Cmd("dev.winget.configure-show", "Show configuration", "顯示組態",
            "Display the details of a WinGet configuration file without applying it.", "顯示 WinGet 組態檔嘅內容而唔套用佢。",
            "Show Config", "顯示組態", "winget configure show --file \"%USERPROFILE%\\Desktop\\configuration.dsc.yaml\" --accept-configuration-agreements",
            keywords: "winget,configure,dsc,組態"),

        // --- docker (20) ---
        Tweak.Cmd("dev.docker.ps", "List running containers", "列出執行緊嘅容器",
            "Show all currently running Docker containers.", "顯示所有而家執行緊嘅 Docker 容器。",
            "List", "列出", "docker ps",
            keywords: "docker,ps,containers,容器"),
        
        Tweak.Cmd("dev.docker.ps-all", "List all containers", "列出所有容器",
            "Show every container including stopped ones.", "顯示所有容器，包括停咗嘅。",
            "List all", "全部列出", "docker ps -a",
            keywords: "docker,ps,all,stopped,容器"),
        
        Tweak.Cmd("dev.docker.images", "List images", "列出映像檔",
            "Show all locally stored Docker images.", "顯示所有本機儲存嘅 Docker 映像檔。",
            "List", "列出", "docker images",
            keywords: "docker,images,映像,鏡像"),
        
        Tweak.Cmd("dev.docker.system-df", "Disk usage", "磁碟用量",
            "Show how much disk space Docker is using for images, containers and volumes.", "顯示 Docker 喺映像檔、容器同卷度用咗幾多磁碟空間。",
            "Show usage", "睇用量", "docker system df",
            keywords: "docker,disk,df,usage,磁碟"),
        
        Tweak.Cmd("dev.docker.system-prune", "Prune system", "清理系統",
            "Remove all stopped containers, unused networks, dangling images and build cache.", "移除所有停咗嘅容器、未用嘅網路、懸空映像檔同建置快取。",
            "Prune", "清理", "docker system prune -f",
            destructive: true, keywords: "docker,prune,system,clean,清理"),
        
        Tweak.Cmd("dev.docker.container-prune", "Prune containers", "清理容器",
            "Remove all stopped containers.", "移除所有停咗嘅容器。",
            "Prune", "清理", "docker container prune -f",
            destructive: true, keywords: "docker,container,prune,清理,容器"),
        
        Tweak.Cmd("dev.docker.image-prune", "Prune images", "清理映像檔",
            "Remove dangling (untagged) images to free space.", "移除懸空（無標籤）嘅映像檔嚟釋放空間。",
            "Prune", "清理", "docker image prune -f",
            destructive: true, keywords: "docker,image,prune,清理,映像"),
        
        Tweak.Cmd("dev.docker.volume-ls", "List volumes", "列出卷",
            "Show all Docker volumes on this host.", "顯示呢部主機上面所有嘅 Docker 卷。",
            "List", "列出", "docker volume ls",
            keywords: "docker,volume,ls,卷"),
        
        Tweak.Cmd("dev.docker.network-ls", "List networks", "列出網路",
            "Show all Docker networks.", "顯示所有 Docker 網路。",
            "List", "列出", "docker network ls",
            keywords: "docker,network,ls,網路"),
        
        Tweak.Cmd("dev.docker.stats", "Container stats", "容器統計",
            "Show a one-shot snapshot of CPU, memory and I/O usage per container.", "顯示每個容器嘅 CPU、記憶體同 I/O 用量一次性快照。",
            "Show stats", "睇統計", "docker stats --no-stream",
            keywords: "docker,stats,cpu,memory,統計"),
        
        Tweak.Cmd("dev.docker.version", "Docker version", "Docker 版本",
            "Show the Docker client and server version details.", "顯示 Docker 客戶端同伺服器嘅版本資料。",
            "Version", "版本", "docker version",
            keywords: "docker,version,版本"),
        
        Tweak.Cmd("dev.docker.info", "Docker info", "Docker 資訊",
            "Show system-wide Docker information including driver, root dir and container counts.", "顯示全系統嘅 Docker 資訊，包括驅動程式、根目錄同容器數目。",
            "Info", "資訊", "docker info",
            keywords: "docker,info,system,資訊"),
        
        Tweak.Cmd("dev.docker.compose-ps", "Compose services", "Compose 服務",
            "List the services and their status for the Compose project in the current directory.", "列出目前目錄下 Compose 專案嘅服務同佢哋嘅狀態。",
            "List", "列出", "docker compose ps",
            keywords: "docker,compose,ps,services,服務"),
        
        Tweak.Cmd("dev.docker.logs-note", "Container logs help", "容器日誌說明",
            "Print docker logs usage; pass a container name or ID to view its logs.", "印出 docker logs 用法；俾個容器名或者 ID 就可以睇佢嘅日誌。",
            "Show help", "睇說明", "docker logs --help",
            keywords: "docker,logs,日誌,help"),
        
        Tweak.Cmd("dev.docker.top", "Container processes help", "容器程序說明",
            "Print docker top usage; pass a container name to list the processes running inside it.", "印出 docker top 用法；俾個容器名就可以列出佢入面執行緊嘅程序。",
            "Show help", "睇說明", "docker top --help",
            keywords: "docker,top,processes,程序"),
        
        Tweak.Powershell("dev.docker.restart-desktop", "Restart Docker Desktop", "重啟 Docker Desktop",
            "Stop any running Docker Desktop, then relaunch it to recover from a hung engine.", "停咗執行緊嘅 Docker Desktop，然後重新啟動嚟救返卡住嘅引擎。",
            "Restart", "重啟", "Get-Process 'Docker Desktop' -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Process \"$env:ProgramFiles\\Docker\\Docker\\Docker Desktop.exe\"",
            destructive: true, keywords: "docker,desktop,restart,重啟"),
        
        Tweak.Cmd("dev.docker.builder-prune", "Prune build cache", "清理建置快取",
            "Remove the BuildKit build cache to free disk space.", "移除 BuildKit 嘅建置快取嚟釋放磁碟空間。",
            "Prune", "清理", "docker builder prune -f",
            destructive: true, keywords: "docker,builder,prune,cache,快取"),
        
        Tweak.Cmd("dev.docker.context-ls", "List contexts", "列出 context",
            "Show all Docker contexts and which one is currently active.", "顯示所有 Docker context，同埋而家用緊邊個。",
            "List", "列出", "docker context ls",
            keywords: "docker,context,ls,情境"),
        
        Tweak.Cmd("dev.docker.volume-prune", "Prune volumes", "清理卷",
            "Remove all unused local volumes; this deletes their data permanently.", "移除所有未用嘅本機卷；會永久刪除佢哋嘅資料。",
            "Prune", "清理", "docker volume prune -f",
            destructive: true, keywords: "docker,volume,prune,清理,卷"),
        
        Tweak.Cmd("dev.docker.history-note", "Image history help", "映像檔歷史說明",
            "Print docker history usage; pass an image name to see its layers and how it was built.", "印出 docker history 用法；俾個映像檔名就可以睇佢嘅層同點建置出嚟。",
            "Show help", "睇說明", "docker history --help",
            keywords: "docker,history,layers,映像,層"),

        // --- runtimes (20) ---
        Tweak.Cmd("dev.runtimes.node-version", "Node.js version", "Node.js 版本",
            "Show the installed Node.js runtime version.", "顯示已安裝嘅 Node.js 執行階段版本。",
            "Check", "查看", "node --version",
            keywords: "node,nodejs,version,版本"),
        
        Tweak.Cmd("dev.runtimes.npm-version", "npm version", "npm 版本",
            "Show the installed npm package manager version.", "顯示已安裝嘅 npm 套件管理員版本。",
            "Check", "查看", "npm --version",
            keywords: "npm,version,版本"),
        
        Tweak.Cmd("dev.runtimes.npm-list-global", "Global npm packages", "全域 npm 套件",
            "List globally installed npm packages at top level only.", "列出全域安裝嘅 npm 套件（只列頂層）。",
            "List", "列出", "npm list -g --depth=0",
            keywords: "npm,global,list,套件"),
        
        Tweak.Cmd("dev.runtimes.npm-cache-verify", "Verify npm cache", "驗證 npm 快取",
            "Check the integrity of the npm cache contents.", "檢查 npm 快取內容嘅完整性。",
            "Verify", "驗證", "npm cache verify",
            keywords: "npm,cache,verify,快取"),
        
        Tweak.Cmd("dev.runtimes.npm-outdated-global", "Outdated global npm", "過時嘅全域 npm",
            "List global npm packages that have newer versions available.", "列出有新版本可用嘅全域 npm 套件。",
            "Check", "查看", "npm outdated -g",
            keywords: "npm,outdated,global,過時"),
        
        Tweak.Cmd("dev.runtimes.npm-cache-clean", "Clear npm cache", "清除 npm 快取",
            "Force-clear the entire npm cache directory.", "強制清除整個 npm 快取目錄。",
            "Clear", "清除", "npm cache clean --force",
            destructive: true, keywords: "npm,cache,clean,清除,快取"),
        
        Tweak.Cmd("dev.runtimes.python-version", "Python version", "Python 版本",
            "Show the version of the default Python interpreter.", "顯示預設 Python 直譯器嘅版本。",
            "Check", "查看", "python --version",
            keywords: "python,version,版本"),
        
        Tweak.Cmd("dev.runtimes.pip-version", "pip version", "pip 版本",
            "Show the installed pip package manager version.", "顯示已安裝嘅 pip 套件管理員版本。",
            "Check", "查看", "pip --version",
            keywords: "pip,python,version,版本"),
        
        Tweak.Cmd("dev.runtimes.pip-list", "Installed pip packages", "已安裝嘅 pip 套件",
            "List all installed Python packages and their versions.", "列出所有已安裝嘅 Python 套件同版本。",
            "List", "列出", "pip list",
            keywords: "pip,python,list,套件"),
        
        Tweak.Cmd("dev.runtimes.pip-outdated", "Outdated pip packages", "過時嘅 pip 套件",
            "List installed Python packages that have newer versions.", "列出有新版本嘅已安裝 Python 套件。",
            "Check", "查看", "pip list --outdated",
            keywords: "pip,python,outdated,過時"),
        
        Tweak.Cmd("dev.runtimes.py-list", "List Python installs", "列出 Python 安裝",
            "Use the py launcher to list all installed Python versions.", "用 py 啟動器列出所有已安裝嘅 Python 版本。",
            "List", "列出", "py -0",
            keywords: "python,py,launcher,version,版本"),
        
        Tweak.Cmd("dev.runtimes.dotnet-info", ".NET info", ".NET 資訊",
            "Show detailed information about the installed .NET SDK and environment.", "顯示已安裝 .NET SDK 同環境嘅詳細資訊。",
            "Show", "顯示", "dotnet --info",
            keywords: "dotnet,.net,info,資訊"),
        
        Tweak.Cmd("dev.runtimes.dotnet-list-sdks", ".NET SDKs", ".NET SDK 清單",
            "List all installed .NET SDK versions.", "列出所有已安裝嘅 .NET SDK 版本。",
            "List", "列出", "dotnet --list-sdks",
            keywords: "dotnet,.net,sdk,list"),
        
        Tweak.Cmd("dev.runtimes.dotnet-list-runtimes", ".NET runtimes", ".NET 執行階段",
            "List all installed .NET runtime versions.", "列出所有已安裝嘅 .NET 執行階段版本。",
            "List", "列出", "dotnet --list-runtimes",
            keywords: "dotnet,.net,runtime,執行階段"),
        
        Tweak.Cmd("dev.runtimes.dotnet-nuget-locals", "NuGet cache folders", "NuGet 快取資料夾",
            "List the local NuGet cache folders for all caches.", "列出所有 NuGet 本機快取資料夾。",
            "List", "列出", "dotnet nuget locals all --list",
            keywords: "dotnet,nuget,cache,快取"),
        
        Tweak.Cmd("dev.runtimes.java-version", "Java version", "Java 版本",
            "Show the installed Java runtime version.", "顯示已安裝嘅 Java 執行階段版本。",
            "Check", "查看", "java -version",
            keywords: "java,jdk,jre,version,版本"),
        
        Tweak.Cmd("dev.runtimes.go-version", "Go version", "Go 版本",
            "Show the installed Go toolchain version (reports not found if Go is absent).", "顯示已安裝嘅 Go 工具鏈版本（如未安裝會顯示搵唔到）。",
            "Check", "查看", "go version",
            keywords: "go,golang,version,版本"),
        
        Tweak.Cmd("dev.runtimes.rustc-version", "Rust compiler version", "Rust 編譯器版本",
            "Show the installed Rust compiler version (reports not found if Rust is absent).", "顯示已安裝嘅 Rust 編譯器版本（如未安裝會顯示搵唔到）。",
            "Check", "查看", "rustc --version",
            keywords: "rust,rustc,cargo,version,版本"),
        
        Tweak.Cmd("dev.runtimes.deno-version", "Deno version", "Deno 版本",
            "Show the installed Deno runtime version (reports not found if Deno is absent).", "顯示已安裝嘅 Deno 執行階段版本（如未安裝會顯示搵唔到）。",
            "Check", "查看", "deno --version",
            keywords: "deno,javascript,typescript,version,版本"),
        
        Tweak.Cmd("dev.runtimes.where-node-python", "Locate node and python", "搵 node 同 python",
            "Show the filesystem locations of the node and python executables.", "顯示 node 同 python 可執行檔嘅檔案系統位置。",
            "Locate", "定位", "where node & where python",
            keywords: "where,node,python,path,位置"),

        // --- envports (20) ---
        Tweak.Powershell("dev.envports.list-env", "List environment variables", "列出環境變數",
            "Show all user and process environment variables.", "顯示所有用戶同進程嘅環境變數。",
            "List", "列出", "Get-ChildItem Env: | Sort-Object Name | Format-Table -AutoSize Name,Value",
            keywords: "env,environment,variables,環境變數"),
        
        Tweak.Powershell("dev.envports.path-split", "Show PATH per-line", "逐行顯示 PATH",
            "Split the PATH variable into one entry per line for easy reading.", "將 PATH 變數逐行拆開,方便睇。",
            "Show", "顯示", "$env:Path -split ';' | Where-Object { $_ }",
            keywords: "path,環境,逐行,split"),
        
        Tweak.Powershell("dev.envports.listening-ports", "List listening TCP ports", "列出監聽中嘅 TCP 端口",
            "Show all TCP ports currently in the Listen state with their owning process.", "顯示所有處於監聽狀態嘅 TCP 端口同埋擁有嘅進程。",
            "List", "列出", "Get-NetTCPConnection -State Listen | Select-Object LocalAddress,LocalPort,OwningProcess | Sort-Object LocalPort | Format-Table -AutoSize",
            keywords: "ports,tcp,listen,端口,監聽"),
        
        Tweak.Powershell("dev.envports.find-by-port", "Find process by port", "用端口搵進程",
            "Show which process owns TCP port 8080 (edit the port in the command as needed).", "顯示邊個進程佔用 TCP 端口 8080(可自行改端口)。",
            "Find", "搵", "$port=8080; Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue | Select-Object LocalPort,State,OwningProcess,@{N='Process';E={(Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).ProcessName}} | Format-Table -AutoSize",
            keywords: "port,process,find,端口,進程,8080"),
        
        Tweak.Cmd("dev.envports.kill-by-pid", "Kill process by PID", "用 PID 終止進程",
            "Force-terminate the process with PID 1234 (edit the PID before running).", "強制終止 PID 1234 嘅進程(執行前請改 PID)。",
            "Kill", "終止", "taskkill /PID 1234 /F",
            destructive: true, keywords: "kill,taskkill,pid,終止,進程"),
        
        Tweak.Cmd("dev.envports.kill-by-name", "Kill process by name", "用名稱終止進程",
            "Force-terminate all processes named notepad.exe (edit the image name first).", "強制終止所有名為 notepad.exe 嘅進程(請先改程式名)。",
            "Kill", "終止", "taskkill /IM notepad.exe /F",
            destructive: true, keywords: "kill,taskkill,name,名稱,終止"),
        
        Tweak.Cmd("dev.envports.hostname", "Show hostname", "顯示主機名",
            "Print this computer's network hostname.", "顯示呢部電腦嘅網絡主機名。",
            "Show", "顯示", "hostname",
            keywords: "hostname,主機名,computer"),
        
        Tweak.Cmd("dev.envports.ipconfig", "Show IP configuration", "顯示 IP 設定",
            "Display full network adapter configuration via ipconfig /all.", "用 ipconfig /all 顯示完整嘅網絡介面卡設定。",
            "Show", "顯示", "ipconfig /all",
            keywords: "ipconfig,ip,network,網絡,設定"),
        
        Tweak.Cmd("dev.envports.whoami-all", "Show whoami /all", "顯示 whoami /all",
            "Dump the current user, groups, and privileges with whoami /all.", "用 whoami /all 顯示目前用戶、群組同權限。",
            "Show", "顯示", "whoami /all",
            keywords: "whoami,user,groups,privileges,用戶,權限"),
        
        Tweak.Powershell("dev.envports.top-cpu", "Top processes by CPU", "按 CPU 排名嘅進程",
            "List the top 15 running processes ranked by CPU time.", "列出按 CPU 時間排名嘅頭 15 個進程。",
            "Show", "顯示", "Get-Process | Sort-Object CPU -Descending | Select-Object -First 15 Name,Id,CPU,@{N='WS(MB)';E={[math]::Round($_.WorkingSet64/1MB,1)}} | Format-Table -AutoSize",
            keywords: "process,cpu,top,進程,排名"),
        
        Tweak.Powershell("dev.envports.uptime", "Show system uptime", "顯示系統運行時間",
            "Show how long since the last boot using the last boot-up time.", "用上次開機時間計算系統已運行幾耐。",
            "Show", "顯示", "$b=(Get-CimInstance Win32_OperatingSystem).LastBootUpTime; $u=(Get-Date)-$b; Write-Output (\"Last boot: {0}\" -f $b); Write-Output (\"Uptime: {0}d {1}h {2}m\" -f $u.Days,$u.Hours,$u.Minutes)",
            keywords: "uptime,boot,運行時間,開機"),
        
        Tweak.Shell("dev.envports.edit-env-gui", "Open Environment Variables editor", "開啟環境變數編輯器",
            "Launch the Windows Environment Variables dialog to edit user and system variables.", "開啟 Windows 環境變數對話框,編輯用戶同系統變數。",
            "rundll32.exe", "sysdm.cpl,EditEnvironmentVariables",
            keywords: "environment,editor,gui,環境變數,編輯"),
        
        Tweak.Powershell("dev.envports.refresh-env", "Refresh environment", "重新整理環境變數",
            "Reload PATH for the current session from the registry without rebooting.", "唔使重啟,從登錄檔重新載入目前工作階段嘅 PATH。",
            "Refresh", "重新整理", "$env:Path = [System.Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [System.Environment]::GetEnvironmentVariable('Path','User'); Write-Output 'PATH refreshed for this session.'; $env:Path -split ';' | Where-Object { $_ }",
            keywords: "refresh,environment,path,重新整理,環境"),
        
        Tweak.Powershell("dev.envports.echo-var", "Echo a specific env var", "顯示指定環境變數",
            "Print the value of the USERPROFILE variable (edit the name to inspect another).", "顯示 USERPROFILE 變數嘅值(可改名睇其他變數)。",
            "Echo", "顯示", "$name='USERPROFILE'; Write-Output (\"{0} = {1}\" -f $name, [System.Environment]::GetEnvironmentVariable($name))",
            keywords: "echo,env,variable,變數,userprofile"),
        
        Tweak.Powershell("dev.envports.scheduled-reboots", "List scheduled reboot tasks", "列出排程重啟工作",
            "Search scheduled tasks whose action runs shutdown.exe.", "搜尋動作執行 shutdown.exe 嘅排程工作。",
            "List", "列出", "Get-ScheduledTask | Where-Object { $_.Actions.Execute -match 'shutdown' } | Select-Object TaskName,TaskPath,State | Format-Table -AutoSize",
            keywords: "scheduled,reboot,shutdown,排程,重啟"),
        
        Tweak.Cmd("dev.envports.user-sid", "Show current user SID", "顯示目前用戶 SID",
            "Print the security identifier (SID) of the current user.", "顯示目前用戶嘅安全識別碼(SID)。",
            "Show", "顯示", "whoami /user",
            keywords: "sid,user,whoami,用戶,識別碼"),
        
        Tweak.Cmd("dev.envports.local-admins", "List local administrators", "列出本機管理員",
            "Show members of the local Administrators group.", "顯示本機 Administrators 群組嘅成員。",
            "List", "列出", "net localgroup administrators",
            keywords: "administrators,localgroup,admins,管理員,群組"),
        
        Tweak.Shell("dev.envports.system-properties", "Open System Properties (Advanced)", "開啟系統內容(進階)",
            "Launch the Advanced tab of the System Properties dialog.", "開啟系統內容對話框嘅進階分頁。",
            "SystemPropertiesAdvanced.exe", "",
            keywords: "system,properties,advanced,系統內容,進階"),
        
        Tweak.Powershell("dev.envports.dns-cache", "Show DNS client cache", "顯示 DNS 客戶端快取",
            "Display the resolver cache of recently looked-up DNS entries.", "顯示最近查詢過嘅 DNS 解析快取項目。",
            "Show", "顯示", "Get-DnsClientCache | Select-Object Entry,RecordType,Data,TimeToLive | Format-Table -AutoSize",
            keywords: "dns,cache,resolver,快取"),
        
        Tweak.Powershell("dev.envports.active-connections", "List active TCP connections", "列出已建立嘅 TCP 連線",
            "Show established outbound and inbound TCP connections with their processes.", "顯示已建立嘅入站同出站 TCP 連線同對應進程。",
            "List", "列出", "Get-NetTCPConnection -State Established | Select-Object LocalAddress,LocalPort,RemoteAddress,RemotePort,OwningProcess,@{N='Process';E={(Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).ProcessName}} | Format-Table -AutoSize",
            keywords: "tcp,connections,established,連線,網絡"),

        // --- clis (20) ---
        Tweak.Cmd("dev.clis.claude-version", "Claude CLI version", "Claude CLI 版本",
            "Print the installed Claude Code CLI version.", "顯示已安裝嘅 Claude Code CLI 版本。",
            "Check", "查版本", "claude --version", keywords: "claude,version,版本,cli"),
        
        Tweak.Cmd("dev.clis.codex-version", "Codex CLI version", "Codex CLI 版本",
            "Print the installed Codex CLI version.", "顯示已安裝嘅 Codex CLI 版本。",
            "Check", "查版本", "codex --version", keywords: "codex,version,版本,cli"),
        
        Tweak.Cmd("dev.clis.opencode-version", "OpenCode CLI version", "OpenCode CLI 版本",
            "Print the installed OpenCode CLI version.", "顯示已安裝嘅 OpenCode CLI 版本。",
            "Check", "查版本", "opencode --version", keywords: "opencode,version,版本,cli"),
        
        Tweak.Cmd("dev.clis.gh-version", "GitHub CLI version", "GitHub CLI 版本",
            "Print the installed GitHub CLI (gh) version.", "顯示已安裝嘅 GitHub CLI（gh）版本。",
            "Check", "查版本", "gh --version", keywords: "gh,github,version,版本,cli"),
        
        Tweak.Cmd("dev.clis.gh-auth-status", "GitHub auth status", "GitHub 登入狀態",
            "Show your current GitHub CLI authentication status and logged-in account.", "顯示你而家 GitHub CLI 嘅登入狀態同帳戶。",
            "Check", "查登入", "gh auth status", keywords: "gh,github,auth,login,登入,狀態"),
        
        Tweak.Cmd("dev.clis.git-version", "Git version", "Git 版本",
            "Print the installed Git version.", "顯示已安裝嘅 Git 版本。",
            "Check", "查版本", "git --version", keywords: "git,version,版本,cli"),
        
        Tweak.Cmd("dev.clis.git-config-list", "List global Git config", "列出全域 Git 設定",
            "Show every entry in your global Git configuration.", "顯示你全域 Git 設定入面嘅每一項。",
            "List", "列出", "git config --global --list", keywords: "git,config,設定,global,全域"),
        
        Tweak.Cmd("dev.clis.ssh-keygen-note", "Generate SSH key (Ed25519)", "產生 SSH 金鑰（Ed25519）",
            "Create a new Ed25519 SSH key pair in your .ssh folder for Git and remote logins.", "喺你 .ssh 資料夾整一對新嘅 Ed25519 SSH 金鑰，畀 Git 同遠端登入用。",
            "Generate", "產生", "ssh-keygen -t ed25519 -C \"%USERNAME%@%COMPUTERNAME%\" -f \"%USERPROFILE%\\.ssh\\id_ed25519\"", keywords: "ssh,ssh-keygen,key,金鑰,ed25519"),
        
        Tweak.Powershell("dev.clis.new-guid", "New GUID", "產生新 GUID",
            "Generate a fresh random GUID you can copy into code or configs.", "產生一個全新隨機嘅 GUID，可以複製去程式碼或者設定檔。",
            "Generate", "產生", "[guid]::NewGuid().ToString()", keywords: "guid,uuid,new,產生,隨機"),
        
        Tweak.Powershell("dev.clis.base64-encode", "Base64 encode text", "Base64 編碼文字",
            "Base64-encode a sample UTF-8 string; edit the text to encode your own.", "將一段 UTF-8 文字做 Base64 編碼；改個字串就可以編碼你自己嘅。",
            "Encode", "編碼", "[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('Hello, world'))", keywords: "base64,encode,編碼,convert"),
        
        Tweak.Powershell("dev.clis.sha256-clipboard", "SHA256 of clipboard text", "剪貼簿文字 SHA256",
            "Compute the SHA256 hash of whatever text is on your clipboard.", "計算你剪貼簿入面文字嘅 SHA256 雜湊值。",
            "Hash", "計算", "$t=Get-Clipboard -Raw; $s=[IO.MemoryStream]::new([Text.Encoding]::UTF8.GetBytes($t)); (Get-FileHash -InputStream $s -Algorithm SHA256).Hash", keywords: "sha256,hash,clipboard,剪貼簿,雜湊"),
        
        Tweak.Cmd("dev.clis.open-vscode", "Open VS Code here", "喺呢度開 VS Code",
            "Launch Visual Studio Code in your user profile folder.", "喺你嘅使用者資料夾度開 Visual Studio Code。",
            "Open", "開啟", "code \"%USERPROFILE%\"", keywords: "vscode,code,editor,編輯器,開啟"),
        
        Tweak.Cmd("dev.clis.open-terminal", "Open Windows Terminal", "開 Windows Terminal",
            "Launch a new Windows Terminal window.", "開一個新嘅 Windows Terminal 視窗。",
            "Open", "開啟", "start \"\" wt", keywords: "wt,terminal,windows terminal,終端機,開啟"),
        
        Tweak.Cmd("dev.clis.wsl-list-verbose", "List WSL distros", "列出 WSL 發行版",
            "Show all installed WSL distributions with their version and running state.", "顯示所有已安裝嘅 WSL 發行版、版本同運行狀態。",
            "List", "列出", "wsl --list --verbose", keywords: "wsl,linux,distro,發行版,列出"),
        
        Tweak.Cmd("dev.clis.wsl-status", "WSL status", "WSL 狀態",
            "Show the current WSL configuration, default distro and kernel version.", "顯示而家 WSL 嘅設定、預設發行版同核心版本。",
            "Check", "查狀態", "wsl --status", keywords: "wsl,status,狀態,linux"),
        
        Tweak.Cmd("dev.clis.open-git-bash", "Open Git Bash", "開 Git Bash",
            "Launch the Git Bash shell from your Git installation.", "由你嘅 Git 安裝度開啟 Git Bash 殼層。",
            "Open", "開啟", "start \"\" \"%PROGRAMFILES%\\Git\\git-bash.exe\"", keywords: "git,bash,git bash,shell,殼層,開啟"),
        
        Tweak.Cmd("dev.clis.git-aliases", "List Git aliases", "列出 Git 別名",
            "Show all configured global Git aliases.", "顯示所有已設定嘅全域 Git 別名。",
            "List", "列出", "git config --global --get-regexp \"^alias\\.\"", keywords: "git,alias,別名,config,設定"),
        
        Tweak.Cmd("dev.clis.curl-url", "Curl a URL", "用 curl 抓網址",
            "Fetch response headers from example.com to test connectivity with curl.", "用 curl 由 example.com 攞返回標頭，測試連線。",
            "Fetch", "抓取", "curl -sI https://example.com", keywords: "curl,http,url,網址,fetch,抓取"),
        
        Tweak.Cmd("dev.clis.jq-version", "jq version", "jq 版本",
            "Print the installed jq JSON processor version.", "顯示已安裝嘅 jq JSON 處理器版本。",
            "Check", "查版本", "jq --version", keywords: "jq,json,version,版本,cli"),
        
        Tweak.Cmd("dev.clis.openssl-version", "OpenSSL version", "OpenSSL 版本",
            "Print the installed OpenSSL version.", "顯示已安裝嘅 OpenSSL 版本。",
            "Check", "查版本", "openssl version", keywords: "openssl,ssl,version,版本,crypto"),
    };
}
