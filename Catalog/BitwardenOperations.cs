using System.Collections.Generic;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// Bitwarden 操作目錄 · Catalog of bw maintenance operations rendered as TweakCards
/// (sync / lock / logout / status / version). 機密操作（解鎖、複製、產生）喺主頁原生控制度做，
/// 唔放喺呢度。Sensitive flows (unlock, copy, generate) live in the page's native controls, not here.
/// </summary>
public static class BitwardenOperations
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.Action("bw.sync", "Sync vault", "同步密碼庫",
            "Pull the latest vault data from the Bitwarden server.",
            "由 Bitwarden 伺服器拉取最新嘅密碼庫資料。",
            "Sync", "同步",
            async ct => await BitwardenService.SyncAsync(ct),
            keywords: "sync refresh pull 同步 更新"),

        Tweak.Action("bw.lock", "Lock vault", "鎖定密碼庫",
            "Lock the vault and wipe the in-memory session key.",
            "鎖定密碼庫並清除記憶體中嘅工作階段金鑰。",
            "Lock", "鎖定",
            async ct => await BitwardenService.LockAsync(ct),
            keywords: "lock 鎖定 session"),

        Tweak.Action("bw.logout", "Log out", "登出",
            "Log out of the account and wipe the session key.",
            "登出帳戶並清除工作階段金鑰。",
            "Log out", "登出",
            async ct => await BitwardenService.LogoutAsync(ct),
            destructive: true,
            keywords: "logout sign out 登出"),

        Tweak.Action("bw.status", "Vault status", "密碼庫狀態",
            "Show the raw bw status (authenticated / locked / unlocked, server, email).",
            "顯示原始 bw 狀態（已驗證／鎖定／解鎖、伺服器、電郵）。",
            "Check", "查睇",
            async ct => await BitwardenService.Run("status", ct),
            keywords: "status 狀態"),

        Tweak.Action("bw.version", "bw version", "bw 版本",
            "Show the installed bw CLI version.", "顯示已安裝嘅 bw CLI 版本。",
            "Check", "查睇",
            async ct => await BitwardenService.Run("--version", ct),
            keywords: "version 版本"),
    };
}
