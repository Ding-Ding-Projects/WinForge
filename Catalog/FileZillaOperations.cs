using System.Collections.Generic;
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
        Tweak.Info("fz.about", "Native FTP / SFTP client", "原生 FTP／SFTP 客戶端",
            "FileZilla-style transfers built into WinForge — FTP, FTPS (explicit TLS) and SFTP via FluentFTP + SSH.NET. No external binary needed.",
            "WinForge 內建嘅 FileZilla 式傳輸 — 用 FluentFTP + SSH.NET 支援 FTP、FTPS（明示 TLS）同 SFTP，唔使另外裝程式。",
            () => "FTP · FTPS · SFTP",
            keywords: "ftp sftp ftps filezilla file transfer 檔案傳輸 上載 下載"),

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

        Tweak.Info("fz.security", "Encrypted credentials", "加密憑證",
            "Saved-site passwords and key passphrases are encrypted at rest with Windows DPAPI (current-user scope). Unknown SFTP host keys / FTPS certificates prompt for trust on first use (TOFU).",
            "已儲存站台嘅密碼同金鑰密語會用 Windows DPAPI（目前使用者範圍）加密儲存。未知嘅 SFTP 主機金鑰／FTPS 憑證會喺首次連線時提示信任（TOFU）。",
            () => "DPAPI · TOFU",
            keywords: "dpapi encrypt password host key fingerprint tofu trust 加密 信任 指紋"),
    };
}
