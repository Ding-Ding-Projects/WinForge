using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace WinForge.Services;

/// <summary>
/// 首次啟動條款同細則閘 · First-launch Terms &amp; Conditions gate.
/// 使用者必須讀完條款，並且喺 5 題小測驗攞到 5/5 先可以接受同繼續。
/// The user must read the terms, then score 5/5 on a short quiz before they can accept and continue.
/// 接受後寫入設定，之後唔再彈出。Once accepted the answer is persisted and the gate never shows again.
/// </summary>
public static class TermsService
{
    /// <summary>設定鍵（帶版本，將來條款更新時可以 bump 重新要求接受）· Settings key (versioned so terms revisions can re-prompt).</summary>
    private const string AcceptedKey = "terms.accepted.v1";

    private const int PassMark = 5;     // 必須全對 · must answer all five correctly

    public static bool HasAccepted => string.Equals(SettingsStore.Get(AcceptedKey, "false"), "true", StringComparison.OrdinalIgnoreCase);

    private static string P(string en, string zh) => Loc.I.Pick(en, zh);

    /// <summary>
    /// 喺第一次啟動顯示條款閘。已接受就即刻回 true。
    /// Show the gate on first launch. Returns true immediately if already accepted.
    /// 回傳 false 代表使用者拒絕或測驗不及格 — 呼叫方應該結束 app。
    /// Returns false when the user declines or fails the quiz — the caller should exit the app.
    /// </summary>
    public static async Task<bool> EnsureAcceptedAsync(XamlRoot xamlRoot)
    {
        if (HasAccepted) return true;
        if (xamlRoot is null) return true;     // 無 UI 根（無頭模式）就唔阻塞 · no root (headless) → don't block

        // 1) 顯示條款本文 · Present the terms text.
        if (!await ShowTermsAsync(xamlRoot)) return false;

        // 2) 必須喺測驗攞 5/5；唔夠分可以重試 · Must score 5/5 on the quiz; retry allowed.
        while (true)
        {
            int score = await RunQuizAsync(xamlRoot);
            if (score < 0) return false;        // 使用者撳「拒絕並退出」· user chose to decline
            if (score >= PassMark)
            {
                SettingsStore.Set(AcceptedKey, "true");
                SettingsStore.Set("terms.accepted.utc", DateTime.UtcNow.ToString("o"));
                await ShowInfoAsync(xamlRoot,
                    P("Welcome to WinForge", "歡迎使用 WinForge"),
                    P("You scored 5/5. Terms accepted — enjoy WinForge!",
                      "你考到 5/5。條款已接受 — 盡情使用 WinForge！"));
                return true;
            }

            bool retry = await ShowRetryAsync(xamlRoot, score);
            if (!retry) return false;
        }
    }

    private static async Task<bool> ShowTermsAsync(XamlRoot xamlRoot)
    {
        var body = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = TermsBody(),
        };
        var scroller = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 380,
            Padding = new Thickness(0, 0, 12, 0),
        };

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = P("WinForge — Terms & Conditions", "WinForge — 條款及細則"),
            Content = scroller,
            PrimaryButtonText = P("I have read — continue to quiz", "我已閱讀 — 繼續測驗"),
            CloseButtonText = P("Decline & Exit", "拒絕並退出"),
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>顯示測驗，回傳得分（0–5）；-1 代表使用者拒絕 · Show the quiz; returns score 0–5, or -1 if declined.</summary>
    private static async Task<int> RunQuizAsync(XamlRoot xamlRoot)
    {
        var groups = new List<RadioButtons>();
        var panel = new StackPanel { Spacing = 18 };

        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = P("Answer all 5 questions. You must score 5/5 to accept the terms and continue.",
                     "回答全部 5 條題目。你必須攞到 5/5 先可以接受條款並繼續。"),
        });

        int n = 1;
        foreach (var q in Questions)
        {
            var rb = new RadioButtons
            {
                Header = $"{n}. {q.Prompt}",
                ItemsSource = q.Options.ToList(),
            };
            groups.Add(rb);
            panel.Children.Add(rb);
            n++;
        }

        var scroller = new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 420,
            Padding = new Thickness(0, 0, 12, 0),
        };

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = P("Terms & Conditions — Quiz", "條款及細則 — 測驗"),
            Content = scroller,
            PrimaryButtonText = P("Submit answers", "提交答案"),
            CloseButtonText = P("Decline & Exit", "拒絕並退出"),
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return -1;

        int score = 0;
        for (int i = 0; i < Questions.Length; i++)
        {
            if (groups[i].SelectedIndex == Questions[i].CorrectIndex) score++;
        }
        return score;
    }

    private static async Task<bool> ShowRetryAsync(XamlRoot xamlRoot, int score)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = P("Not quite — try again", "未夠分 — 再試一次"),
            Content = P($"You scored {score}/5. A perfect 5/5 is required to accept the terms. Please review and retry.",
                        $"你考到 {score}/5。必須 5/5 先可以接受條款。請重溫並再試。"),
            PrimaryButtonText = P("Retry quiz", "重新測驗"),
            CloseButtonText = P("Decline & Exit", "拒絕並退出"),
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    private static async Task ShowInfoAsync(XamlRoot xamlRoot, string title, string body)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = body,
            CloseButtonText = P("Get started", "開始使用"),
            DefaultButton = ContentDialogButton.Close,
        };
        await dlg.ShowAsync();
    }

    private static string TermsBody() => P(
        // English
        "Last updated: 28 June 2026\n\n" +
        "Please read these Terms & Conditions (\"Terms\") carefully before using WinForge (視窗調校). " +
        "By accepting, you agree to be bound by them.\n\n" +
        "1. NATURE OF THE SOFTWARE. WinForge is a Windows 11 control center containing system-tweak " +
        "tools and a hyper-realistic nuclear-reactor SIMULATOR. The reactor is a SIMULATION for " +
        "education and entertainment only; it does not control any real reactor or physical plant.\n\n" +
        "2. SYSTEM CHANGES. Some modules modify Windows settings, the registry, services and files. " +
        "Such changes can affect system behaviour. You are responsible for creating backups/restore " +
        "points before applying tweaks.\n\n" +
        "3. REAL-WORLD SIDE-EFFECTS ARE OPT-IN. Any feature with effects outside the app (e.g. the " +
        "reactor's meltdown-to-real-shutdown action, smart-home mirroring, scheduled tasks) is OFF by " +
        "default, must be enabled by you, and is reversible.\n\n" +
        "4. NO WARRANTY. The software is provided \"AS IS\", without warranty of any kind. The authors " +
        "are not liable for any data loss, damage, or disruption arising from its use.\n\n" +
        "5. YOUR RESPONSIBILITY. You will use WinForge lawfully and only on systems you are authorised " +
        "to administer. You accept full responsibility for the outcome of any change you apply.\n\n" +
        "6. PRIVACY. Settings and secrets are stored locally on your device (secrets protected via " +
        "Windows DPAPI). WinForge does not sell your data.\n\n" +
        "To confirm you understand these Terms, you must pass a short 5-question quiz with a perfect score.",
        // 繁體中文／粵語
        "最後更新：2026 年 6 月 28 日\n\n" +
        "使用 WinForge（視窗調校）之前，請細心閱讀本條款及細則（「條款」）。一經接受，即代表你同意受其約束。\n\n" +
        "1. 軟件性質。WinForge 係一個 Windows 11 控制中心，內含系統調校工具同一個超寫實核反應堆「模擬器」。" +
        "個反應堆只係用嚟教育同娛樂嘅「模擬」，唔會控制任何真實反應堆或者實體設施。\n\n" +
        "2. 系統更改。部分模組會改動 Windows 設定、登錄檔、服務同檔案，可能影響系統行為。" +
        "套用調校之前，你有責任自行建立備份／還原點。\n\n" +
        "3. 對外影響須自行開啟。任何會影響 app 以外嘅功能（例如反應堆「熔毀觸發真實關機」、智能家居鏡像、排程工作）" +
        "預設一律「關閉」，必須由你親自開啟，而且可以還原。\n\n" +
        "4. 不作保證。本軟件按「現狀」提供，不附帶任何形式嘅保證。作者對使用過程中引致嘅任何資料遺失、" +
        "損壞或中斷概不負責。\n\n" +
        "5. 你嘅責任。你會合法使用 WinForge，而且只喺你有權管理嘅系統上使用。你須為所套用嘅任何更改之後果負全責。\n\n" +
        "6. 私隱。設定同密鑰只會儲存喺你部裝置本機（密鑰以 Windows DPAPI 保護）。WinForge 唔會出售你嘅資料。\n\n" +
        "為確認你明白本條款，你必須喺一個 5 題嘅小測驗攞到滿分。");

    private sealed record Quiz(string Prompt, string[] Options, int CorrectIndex);

    private static Quiz[] Questions => _questions ??= BuildQuestions();
    private static Quiz[]? _questions;

    private static Quiz[] BuildQuestions() => new[]
    {
        new Quiz(
            P("What is the WinForge nuclear reactor?",
              "WinForge 嘅核反應堆係咩嚟？"),
            new[]
            {
                P("A real reactor controlled over the internet", "一個透過互聯網控制嘅真實反應堆"),
                P("A simulation for education and entertainment only", "一個只供教育同娛樂嘅模擬"),
                P("A cryptocurrency miner", "一個加密貨幣挖礦程式"),
            },
            1),

        new Quiz(
            P("Before applying system tweaks, you are responsible for…",
              "套用系統調校之前，你有責任…"),
            new[]
            {
                P("Creating backups / restore points", "建立備份／還原點"),
                P("Nothing — WinForge guarantees safety", "乜都唔使做 — WinForge 保證安全"),
                P("Disabling Windows Update permanently", "永久停用 Windows Update"),
            },
            0),

        new Quiz(
            P("Features with real-world side-effects (e.g. real shutdown) are…",
              "會產生真實對外影響嘅功能（例如真實關機）係…"),
            new[]
            {
                P("Always on and cannot be turned off", "永遠開啟，無得關"),
                P("Off by default, opt-in, and reversible", "預設關閉、須自行開啟、可還原"),
                P("Triggered randomly", "隨機觸發"),
            },
            1),

        new Quiz(
            P("What warranty does WinForge provide?",
              "WinForge 提供咩保證？"),
            new[]
            {
                P("A lifetime money-back guarantee", "終身退款保證"),
                P("None — the software is provided \"AS IS\"", "無 — 軟件按「現狀」提供"),
                P("A guarantee against all data loss", "保證唔會有任何資料遺失"),
            },
            1),

        new Quiz(
            P("Where are your settings and secrets stored?",
              "你嘅設定同密鑰儲存喺邊度？"),
            new[]
            {
                P("Sold to advertisers", "賣畀廣告商"),
                P("On a public cloud server", "公共雲端伺服器"),
                P("Locally on your device (secrets via Windows DPAPI)", "你部裝置本機（密鑰以 Windows DPAPI 保護）"),
            },
            2),
    };
}
