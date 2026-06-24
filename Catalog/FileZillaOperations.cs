using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// FTP／SFTP 操作目錄 · Catalog of helper operations for the native FTP/FTPS/SFTP client.
/// 真正嘅雙窗瀏覽器同傳輸佇列住喺 FileZillaModule 頁面；呢度係畀主搜尋同快速跳轉用嘅卡片。
/// The real dual-pane browser + transfer queue live in the FileZilla page; these cards exist for
/// master-search discoverability and quick jumps, plus the optional "launch the real FileZilla" hatch.
/// </summary>
public static class FileZillaOperations
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // 行為唔變嘅 Info 卡（同 Tweak.Info 一樣嘅 Id／種類／文字），淨係加埋一粒「已儲存站台數量」彩色藥丸。
        // Behaviour-identical Info card (same Id, Info kind, GetInfo text as Tweak.Info) — we only add a
        // coloured pill showing how many sites are saved in the DPAPI-backed Site Manager (a cheap, cached
        // synchronous read via FtpSiteStore.All()). Green when ≥1 site is saved, grey when none yet.
        new TweakDefinition
        {
            Id = "fz.about",
            Title = new("Native FTP / SFTP client", "原生 FTP／SFTP 客戶端"),
            Description = new(
                "FileZilla-style transfers built into WinForge — FTP, FTPS (explicit TLS) and SFTP via FluentFTP + SSH.NET. No external binary needed.",
                "WinForge 內建嘅 FileZilla 式傳輸 — 用 FluentFTP + SSH.NET 支援 FTP、FTPS（明示 TLS）同 SFTP，唔使另外裝程式。"),
            Kind = TweakKind.Info,
            // 同 Tweak.Info 嘅關鍵字儲存方式一致（Tweak.Keys 只用 ,／; 分割，所以空格字串維持單一元素）。
            // Identical to how Tweak.Info stores keywords (Tweak.Keys splits on , / ; only, so a space-separated
            // string stays a single element) — search haystack and docs output are byte-for-byte unchanged.
            Keywords = new[] { "ftp sftp ftps filezilla file transfer 檔案傳輸 上載 下載" },
            GetInfo = () => "FTP · FTPS · SFTP",
            ColoredStatus = () =>
            {
                int n = FtpSiteStore.All().Count;
                return n > 0
                    ? ($"{n} saved site{(n == 1 ? "" : "s")}", $"{n} 個已儲存站台", StatusColor.Good)
                    : ("No sites yet", "未有站台", StatusColor.Neutral);
            },
        },

        Tweak.Action("fz.open", "Open FTP / SFTP client", "開啟 FTP／SFTP 客戶端",
            "Open the dual-pane file-transfer client (Site Manager, local/remote browser, transfer queue).",
            "開啟雙窗檔案傳輸客戶端（站台管理、本機／遠端瀏覽、傳輸佇列）。",
            "Open", "開啟",
            _ => { Navigator.GoToModule?.Invoke("module.filezilla"); return Task.FromResult(TweakResult.Ok("Opened.", "已開啟。")); },
            keywords: "open ftp sftp client filezilla 開啟 客戶端"),

        Tweak.Info("fz.protocols", "Supported protocols", "支援嘅協定",
            "FTP (plain), FTPS (FTP over explicit TLS) and SFTP (SSH). Resume is supported on all three: FTP via REST, SFTP via byte-offset append.",
            "FTP（純）、FTPS（FTP over 明示 TLS）同 SFTP（SSH）。三者都支援續傳：FTP 用 REST，SFTP 用位元組偏移 append。",
            () => "FTP/FTPS port 21 · SFTP port 22",
            keywords: "protocol ftp ftps sftp tls ssh resume rest 續傳 協定"),

        // 行為唔變嘅 Info 卡，淨係加埋一粒反映 TOFU 信任狀態嘅彩色藥丸。
        // Behaviour-identical Info card (same Id, Info kind, GetInfo text as Tweak.Info) — we only add a
        // coloured pill summarising the trust-on-first-use state across saved sites: how many already carry
        // a remembered host-key / cert fingerprint. Cheap cached synchronous read via FtpSiteStore.All().
        new TweakDefinition
        {
            Id = "fz.security",
            Title = new("Encrypted credentials", "加密憑證"),
            Description = new(
                "Saved-site passwords and key passphrases are encrypted at rest with Windows DPAPI (current-user scope). Unknown SFTP host keys / FTPS certificates prompt for trust on first use (TOFU).",
                "已儲存站台嘅密碼同金鑰密語會用 Windows DPAPI（目前使用者範圍）加密儲存。未知嘅 SFTP 主機金鑰／FTPS 憑證會喺首次連線時提示信任（TOFU）。"),
            Kind = TweakKind.Info,
            // 同 Tweak.Info 嘅關鍵字儲存方式一致（單一空格字串元素，唔影響搜尋／文件輸出）。
            // Identical keyword storage to Tweak.Info (one space-separated element) — search & docs unchanged.
            Keywords = new[] { "dpapi encrypt password host key fingerprint tofu trust 加密 信任 指紋" },
            GetInfo = () => "DPAPI · TOFU",
            ColoredStatus = () =>
            {
                var sites = FtpSiteStore.All();
                if (sites.Count == 0)
                    return ("DPAPI · TOFU", "DPAPI · TOFU", StatusColor.Neutral);
                int trusted = sites.Count(s => !string.IsNullOrEmpty(s.TrustedFingerprint));
                return trusted == sites.Count
                    ? ("All hosts trusted", "全部主機已信任", StatusColor.Good)
                    : ($"{trusted}/{sites.Count} hosts trusted", $"{trusted}/{sites.Count} 主機已信任", StatusColor.Warn);
            },
        },
    };
}
