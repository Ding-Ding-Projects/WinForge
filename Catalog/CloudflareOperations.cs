using System.Collections.Generic;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// Cloudflare 操作目錄 · Catalog of cloudflared / Tunnel / Access / WARP / DoH operations.
/// 快速指令用 Tweak.Shell 直接執行並擷取輸出；長時間運行嘅（tunnel run、quick tunnel、proxy-dns）
/// 會喺終端機開出嚟。Quick commands capture output; long-running ones open in a terminal window.
/// </summary>
public static class CloudflareOperations
{
    private static TweakDefinition Long(string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string fileName, string args, string? keywords = null)
        => Tweak.Action(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            _ => Task.FromResult(CloudflareService.LaunchInTerminal(fileName, args)), keywords: keywords);

    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // ===== basics =====
        Tweak.Shell("cf.version", "cloudflared version", "cloudflared 版本",
            "Show the installed cloudflared version.", "顯示已安裝嘅 cloudflared 版本。",
            "Check", "查睇", "cloudflared", "--version", keywords: "version cloudflared 版本"),
        Tweak.Shell("cf.update", "Update cloudflared", "更新 cloudflared",
            "Update cloudflared to the latest release.", "將 cloudflared 更新到最新版本。",
            "Update", "更新", "cloudflared", "update", keywords: "update 更新"),
        Tweak.Shell("cf.help", "cloudflared help", "cloudflared 說明",
            "Show the cloudflared top-level help.", "顯示 cloudflared 頂層說明。",
            "Show", "顯示", "cloudflared", "help", keywords: "help 說明"),

        // ===== auth =====
        Long("cf.login", "Tunnel login", "Tunnel 登入",
            "Open a browser to authorise this machine with your Cloudflare account (creates a cert.pem).",
            "開瀏覽器，將呢部機授權畀你嘅 Cloudflare 帳戶（會產生 cert.pem）。",
            "Login", "登入", "cloudflared", "tunnel login", keywords: "login auth 登入 授權"),

        // ===== named tunnels =====
        Tweak.Shell("cf.tunnel-list", "List tunnels", "列出 Tunnel",
            "List all named tunnels on your account.", "列出你帳戶上所有具名 tunnel。",
            "List", "列出", "cloudflared", "tunnel list", keywords: "tunnel list 列出"),
        Tweak.Shell("cf.tunnel-create", "Create tunnel", "建立 Tunnel",
            "Create a named tunnel called MYTUNNEL (edit the name).", "建立一個叫 MYTUNNEL 嘅具名 tunnel（自行改名）。",
            "Create", "建立", "cloudflared", "tunnel create MYTUNNEL", keywords: "tunnel create 建立"),
        Tweak.Shell("cf.tunnel-info", "Tunnel info", "Tunnel 資料",
            "Show details and connections for MYTUNNEL.", "顯示 MYTUNNEL 嘅詳情同連線。",
            "Info", "查睇", "cloudflared", "tunnel info MYTUNNEL", keywords: "tunnel info 資料"),
        Tweak.Shell("cf.tunnel-token", "Tunnel token", "Tunnel 權杖",
            "Print the connector token for MYTUNNEL.", "印出 MYTUNNEL 嘅連接器權杖。",
            "Token", "權杖", "cloudflared", "tunnel token MYTUNNEL", keywords: "tunnel token 權杖"),
        Tweak.Shell("cf.route-dns", "Route DNS to tunnel", "DNS 路由到 Tunnel",
            "Point app.example.com at MYTUNNEL via a DNS record.", "用 DNS 記錄將 app.example.com 指去 MYTUNNEL。",
            "Route", "路由", "cloudflared", "tunnel route dns MYTUNNEL app.example.com", keywords: "route dns 路由"),
        Tweak.Shell("cf.route-ip-add", "Route IP range", "路由 IP 範圍",
            "Route a private IP range (10.0.0.0/24) through MYTUNNEL (WARP-to-Tunnel).",
            "將私有 IP 範圍（10.0.0.0/24）經 MYTUNNEL 路由。",
            "Route", "路由", "cloudflared", "tunnel route ip add 10.0.0.0/24 MYTUNNEL", keywords: "route ip 路由"),
        Tweak.Shell("cf.route-ip-list", "List routed IPs", "列出已路由 IP",
            "List the private IP routes for your tunnels.", "列出你 tunnel 嘅私有 IP 路由。",
            "List", "列出", "cloudflared", "tunnel route ip list", keywords: "route ip list 路由"),
        Long("cf.tunnel-run", "Run tunnel", "執行 Tunnel",
            "Run MYTUNNEL in a terminal (stays open until you close it).", "喺終端機執行 MYTUNNEL（會一直開住）。",
            "Run", "執行", "cloudflared", "tunnel run MYTUNNEL", keywords: "tunnel run 執行"),
        Tweak.Shell("cf.tunnel-cleanup", "Clean up tunnel", "清理 Tunnel",
            "Clean up stale connections for MYTUNNEL.", "清理 MYTUNNEL 殘留嘅連線。",
            "Clean", "清理", "cloudflared", "tunnel cleanup MYTUNNEL", destructive: true, keywords: "tunnel cleanup 清理"),
        Tweak.Shell("cf.tunnel-delete", "Delete tunnel", "刪除 Tunnel",
            "Delete the named tunnel MYTUNNEL.", "刪除具名 tunnel MYTUNNEL。",
            "Delete", "刪除", "cloudflared", "tunnel delete MYTUNNEL", destructive: true, keywords: "tunnel delete 刪除"),

        // ===== quick tunnel (free trycloudflare) =====
        Long("cf.quick-tunnel", "Quick tunnel (try Cloudflare)", "快速 Tunnel（免費試）",
            "Expose http://localhost:8080 over a free *.trycloudflare.com URL (no account). Edit the URL.",
            "用免費 *.trycloudflare.com 網址公開 http://localhost:8080（唔使帳戶）。自行改網址。",
            "Start", "開始", "cloudflared", "tunnel --url http://localhost:8080", keywords: "quick tunnel trycloudflare 快速"),

        // ===== service =====
        Tweak.Shell("cf.service-install", "Install service", "安裝服務",
            "Install cloudflared as a Windows service (run as admin).", "將 cloudflared 安裝做 Windows 服務（需管理員）。",
            "Install", "安裝", "cloudflared", "service install", requiresAdmin: true, keywords: "service install 服務"),
        Tweak.Shell("cf.service-uninstall", "Uninstall service", "移除服務",
            "Remove the cloudflared Windows service.", "移除 cloudflared Windows 服務。",
            "Uninstall", "移除", "cloudflared", "service uninstall", requiresAdmin: true, destructive: true, keywords: "service uninstall 服務"),

        // ===== Cloudflare Access =====
        Long("cf.access-login", "Access login", "Access 登入",
            "Authenticate to a Cloudflare Access app at https://app.example.com.", "登入 Cloudflare Access 應用 https://app.example.com。",
            "Login", "登入", "cloudflared", "access login https://app.example.com", keywords: "access login 登入"),
        Tweak.Shell("cf.access-curl", "Access curl", "Access curl",
            "Make an authenticated request to a protected URL.", "向受保護網址發出已驗證請求。",
            "Run", "執行", "cloudflared", "access curl https://app.example.com", keywords: "access curl"),
        Long("cf.access-tcp", "Access TCP bind", "Access TCP 綁定",
            "Bind a protected TCP service (e.g. SSH) to a local port. Stays open.",
            "將受保護 TCP 服務（例如 SSH）綁到本機連接埠。會一直開住。",
            "Bind", "綁定", "cloudflared", "access tcp --hostname ssh.example.com --url localhost:2222", keywords: "access tcp ssh"),
        Tweak.Shell("cf.access-ssh", "Access SSH config", "Access SSH 設定",
            "Print SSH ProxyCommand config for a protected host.", "印出受保護主機嘅 SSH ProxyCommand 設定。",
            "Show", "顯示", "cloudflared", "access ssh-config --hostname ssh.example.com", keywords: "access ssh"),

        // ===== DNS over HTTPS =====
        Long("cf.doh", "DNS over HTTPS proxy", "DNS over HTTPS 代理",
            "Run a local DNS-over-HTTPS proxy on port 5053 → https://1.1.1.1/dns-query. Stays open.",
            "喺連接埠 5053 執行本機 DNS-over-HTTPS 代理 → https://1.1.1.1/dns-query。會一直開住。",
            "Start", "開始", "cloudflared", "proxy-dns --port 5053 --upstream https://1.1.1.1/dns-query", keywords: "doh proxy-dns dns 加密"),

        // ===== WARP (warp-cli) =====
        Tweak.Shell("cf.warp-version", "WARP version", "WARP 版本",
            "Show the installed WARP client version.", "顯示已安裝嘅 WARP 用戶端版本。",
            "Check", "查睇", "warp-cli", "--version", keywords: "warp version 版本"),
        Tweak.Shell("cf.warp-register", "WARP register", "WARP 註冊",
            "Register this device with WARP.", "將呢部裝置註冊到 WARP。",
            "Register", "註冊", "warp-cli", "registration new", keywords: "warp register 註冊"),
        Tweak.Shell("cf.warp-connect", "WARP connect", "WARP 連線",
            "Connect the WARP tunnel.", "連線 WARP tunnel。",
            "Connect", "連線", "warp-cli", "connect", keywords: "warp connect 連線"),
        Tweak.Shell("cf.warp-disconnect", "WARP disconnect", "WARP 斷線",
            "Disconnect the WARP tunnel.", "斷開 WARP tunnel。",
            "Disconnect", "斷線", "warp-cli", "disconnect", keywords: "warp disconnect 斷線"),
        Tweak.Shell("cf.warp-status", "WARP status", "WARP 狀態",
            "Show the WARP connection status.", "顯示 WARP 連線狀態。",
            "Status", "狀態", "warp-cli", "status", keywords: "warp status 狀態"),
        Tweak.Shell("cf.warp-settings", "WARP settings", "WARP 設定",
            "Show current WARP settings.", "顯示目前 WARP 設定。",
            "Show", "顯示", "warp-cli", "settings", keywords: "warp settings 設定"),
        Tweak.Shell("cf.warp-mode-warp", "WARP mode: WARP", "WARP 模式：WARP",
            "Set the client mode to full WARP (encrypted tunnel).", "將用戶端模式設為完整 WARP（加密通道）。",
            "Set", "設定", "warp-cli", "mode warp", keywords: "warp mode 模式"),
        Tweak.Shell("cf.warp-mode-doh", "WARP mode: DoH", "WARP 模式：DoH",
            "Set the client mode to DNS-only (1.1.1.1 over HTTPS).", "將用戶端模式設為只用 DNS（1.1.1.1 over HTTPS）。",
            "Set", "設定", "warp-cli", "mode doh", keywords: "warp mode doh"),
        Tweak.Shell("cf.warp-account", "WARP account", "WARP 帳戶",
            "Show the WARP account / registration details.", "顯示 WARP 帳戶／註冊詳情。",
            "Show", "顯示", "warp-cli", "account", keywords: "warp account 帳戶"),
    };
}
