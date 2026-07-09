using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 模組頁首動作按鈕工廠 · Shared factory for module-header action buttons (set via
/// <c>Header.ActionContent</c>). Two flavours: "Open full features" opens the WinForge COMPANION app —
/// written in the upstream app's literal language (Monaco/JS web popup, or compiled-on-demand C++) — and
/// "Native window" opens the external-app launcher popup (auto-install + launch the upstream product).
/// Rebuild on every Render() call so language changes re-label naturally.
/// </summary>
public static class HeaderActions
{
    private static string P(string en, string zh) => Loc.I.Pick(en, zh);

    /// <summary>「開啟完整功能」→ WinForge 隨附 app · Full-features button → the WinForge companion app.</summary>
    public static Button FullFeaturesButton(string companionId)
    {
        var btn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = ((char)0xE740).ToString(), FontSize = 13 },   // full-screen glyph
                    new TextBlock { Text = P("Open full features", "開啟完整功能") },
                },
            },
        };
        ToolTipService.SetToolTip(btn, P(
            "Open the full-featured WinForge companion app (written in the upstream app's own language) in a popup.",
            "以彈窗開啟功能完整嘅 WinForge 隨附 app（以上游 app 嘅原語言寫成）。"));
        btn.Click += (_, _) => CompanionLauncher.Open(companionId);
        return btn;
    }

    /// <summary>「原生視窗」→ 外部 app 啟動彈窗 · Native-window button → the external-app launcher popup.</summary>
    public static Button NativeWindowButton(string externalAppId)
    {
        var btn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = ((char)0xE8A7).ToString(), FontSize = 13 },
                    new TextBlock { Text = P("Native window", "原生視窗") },
                },
            },
        };
        ToolTipService.SetToolTip(btn, P("Install everything and launch the real app in its own window.",
            "全部自動安裝，並以原生視窗啟動真正嘅 app。"));
        btn.Click += (_, _) => AppLauncherWindow.Show(externalAppId);
        return btn;
    }

    /// <summary>組合多個頁首按鈕 · Combine several header buttons into one ActionContent panel.</summary>
    public static StackPanel Combine(params FrameworkElement[] items)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var i in items) panel.Children.Add(i);
        return panel;
    }
}
