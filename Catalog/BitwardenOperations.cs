using System.Collections.Generic;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// Bitwarden 維護操作目錄 · Catalog of native Bitwarden maintenance operations rendered as TweakCards
/// (sync / lock / logout). 全部行喺原生用戶端（無 bw CLI）· All run through the native managed client
/// (no bw CLI). 機密流程（解鎖、複製、產生）喺主頁原生控制度做 · Sensitive flows live in the page itself.
/// </summary>
public static class BitwardenOperations
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.Action("bw.sync", "Sync vault", "同步保險庫",
            "Pull and decrypt the latest vault from the Bitwarden server.",
            "由 Bitwarden 伺服器拉取並解密最新嘅保險庫。",
            "Sync", "同步",
            async ct => await BitwardenService.Shared.SyncAsync(ct),
            keywords: "sync refresh pull 同步 更新"),

        Tweak.Action("bw.lock", "Lock vault", "鎖定保險庫",
            "Lock the vault and wipe all key material from memory.",
            "鎖定保險庫並清除記憶體中所有金鑰物料。",
            "Lock", "鎖定",
            ct => { BitwardenService.Shared.Lock(); return Task.FromResult(TweakResult.Ok("Vault locked.", "保險庫已鎖定。")); },
            keywords: "lock 鎖定 session key"),

        Tweak.Action("bw.logout", "Log out", "登出",
            "Log out of the account and wipe keys and stored tokens.",
            "登出帳戶並清除金鑰同已儲存嘅權杖。",
            "Log out", "登出",
            ct => { BitwardenService.Shared.Logout(); return Task.FromResult(TweakResult.Ok("Logged out.", "已登出。")); },
            destructive: true,
            keywords: "logout sign out 登出"),
    };
}
