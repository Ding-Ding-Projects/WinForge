using WinForge.Models;

namespace WinForge.Catalog;

/// <summary>
/// Explicitly tone-variable, non-safety copy. Keeping this catalog separate from UI logic
/// makes the reviewed boundary visible and prevents automatic rewriting of operational text.
/// </summary>
public static class PlayfulCopy
{
    public static PlayfulText DashboardHero { get; } = new(
        en1: "A bilingual Windows 11 control center.",
        en2: "A practical, fully bilingual control center for Windows 11.",
        en3: "An all-in-one, fully bilingual control center that genuinely tunes Windows 11.",
        en4: "Your bilingual Windows 11 control room — tune the system without the scavenger hunt.",
        en5: "Windows 11 tuning, bilingual and fully loaded — fewer maze-like menus, more useful buttons.",
        zh1: "雙語 Windows 11 控制中心。",
        zh2: "實用嘅全雙語 Windows 11 控制中心。",
        zh3: "全方位、全雙語嘅控制中心，真係會幫你調校 Windows 11。",
        zh4: "你嘅雙語 Windows 11 控制室 — 唔使再喺設定迷宮兜圈。",
        zh5: "Windows 11 調校大本營 — 少啲迷宮式選單，多啲一撳就有用嘅掣。"
    );
}
