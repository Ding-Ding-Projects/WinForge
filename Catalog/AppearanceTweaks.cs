using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 外觀與個人化 · Appearance &amp; personalisation tweaks.
/// 全部用真實嘅 Windows 11 登錄檔路徑同數值。
/// Every entry uses real, documented Windows 11 registry paths and values.
/// </summary>
public static class AppearanceTweaks
{
    private const string Advanced =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string Personalize =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // 1) Dark mode (apps + system) — needs two values, so CustomToggle.
        //    Rich: live mini-window preview reflecting dark/light + accent-on-chrome + transparency.
        //    豐富化：活動迷你視窗預覽，反映深淺色、主題色上框同透明設定（套用後即時重畫）。
        DarkMode(),

        // 2) Transparency effects.
        Tweak.RegToggle("appearance.transparency", "Transparency effects", "透明效果",
            "Enable the translucent acrylic look on Start, taskbar and surfaces.",
            "喺開始功能表、工作列同視窗開透明亞克力效果。",
            RegRoot.HKCU, Personalize, "EnableTransparency",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer,
            keywords: "transparency,acrylic,透明,亞克力"),

        // 3) Accent colour on Start & taskbar.
        Tweak.RegToggle("appearance.accent-start-taskbar", "Accent colour on Start & taskbar", "開始與工作列顯示主題色",
            "Tint the Start menu and taskbar with your accent colour.",
            "用主題色為開始功能表同工作列上色。",
            RegRoot.HKCU, Personalize, "ColorPrevalence",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer,
            keywords: "accent,colour,color,主題色,工作列"),

        // 4) Accent colour on title bars & window borders.
        Tweak.RegToggle("appearance.accent-titlebars", "Accent colour on title bars & borders", "標題列與邊框顯示主題色",
            "Show your accent colour on window title bars and borders.",
            "喺視窗標題列同邊框顯示主題色。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\DWM", "ColorPrevalence",
            onValue: 1, offValue: 0,
            keywords: "accent,titlebar,border,標題列,邊框"),

        // 5) Show seconds in the system-clock.
        Tweak.RegToggle("appearance.clock-seconds", "Show seconds in clock", "時鐘顯示秒數",
            "Display seconds on the taskbar clock.",
            "喺工作列嘅時鐘顯示秒數。",
            RegRoot.HKCU, Advanced, "ShowSecondsInSystemClock",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer,
            keywords: "clock,seconds,時鐘,秒"),

        // 6) Taskbar animations.
        Tweak.RegToggle("appearance.taskbar-animations", "Taskbar animations", "工作列動畫",
            "Animate taskbar buttons and previews.",
            "為工作列按鈕同預覽加上動畫。",
            RegRoot.HKCU, Advanced, "TaskbarAnimations",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer,
            keywords: "animation,taskbar,動畫,工作列"),

        // 7) Window minimise/maximise animation (REG_SZ "1"/"0").
        Tweak.RegToggle("appearance.window-animations", "Window min/max animations", "視窗縮放動畫",
            "Animate windows when minimising and maximising.",
            "縮細同放大視窗嗰陣播放動畫。",
            RegRoot.HKCU, @"Control Panel\Desktop\WindowMetrics", "MinAnimate",
            onValue: "1", offValue: "0", kind: RegistryValueKind.String,
            restart: RestartScope.SignOut, keywords: "animation,window,minimize,動畫,視窗"),

        // 8) Snap layout flyout on hover over the maximise button.
        Tweak.RegToggle("appearance.snap-flyout", "Snap layout flyout on hover", "懸停顯示貼齊版面",
            "Show the snap layouts flyout when hovering the maximise button.",
            "將滑鼠移到最大化掣上面就彈出貼齊版面。",
            RegRoot.HKCU, Advanced, "EnableSnapAssistFlyout",
            onValue: 1, offValue: 0, keywords: "snap,layout,貼齊,版面"),

        // 9) Translucent selection rectangle in Explorer.
        Tweak.RegToggle("appearance.alpha-select", "Translucent selection rectangle", "半透明選取框",
            "Use the translucent rubber-band selection box in File Explorer.",
            "喺檔案總管用半透明嘅拖曳選取框。",
            RegRoot.HKCU, Advanced, "ListviewAlphaSelect",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer,
            keywords: "selection,translucent,選取,透明"),

        // 10) Drop shadows for desktop icon labels.
        Tweak.RegToggle("appearance.icon-shadow", "Drop shadows for icon labels", "圖示標籤陰影",
            "Add drop shadows behind desktop icon text.",
            "為桌面圖示文字加上投影陰影。",
            RegRoot.HKCU, Advanced, "ListviewShadow",
            onValue: 1, offValue: 0, restart: RestartScope.Explorer,
            keywords: "shadow,icon,陰影,圖示"),

        // 11) Show window contents while dragging (REG_SZ "1"/"0").
        Tweak.RegToggle("appearance.drag-full-windows", "Show window contents while dragging", "拖曳時顯示視窗內容",
            "Show the whole window instead of an outline while dragging.",
            "拖曳視窗嗰陣顯示成個視窗內容，而唔係淨係框線。",
            RegRoot.HKCU, @"Control Panel\Desktop", "DragFullWindows",
            onValue: "1", offValue: "0", kind: RegistryValueKind.String,
            restart: RestartScope.SignOut, keywords: "drag,window,拖曳,視窗"),

        // 12) Start menu layout (more pins vs more recommendations).
        // Three mutually-exclusive named options read better as radio buttons than a dropdown.
        // 三個互斥嘅命名選項用單選按鈕比下拉選單更清楚；Id／登錄檔值／重啟範圍全部不變。
        RegRadio("appearance.start-layout", "Start menu layout", "開始功能表版面",
            "Balance pinned apps against recommended items in the Start menu.",
            "喺開始功能表平衡釘選應用程式同建議項目。",
            Advanced, "Start_Layout",
            new (string en, string zh, int value)[]
            {
                ("Default", "預設", 0),
                ("More pins", "更多釘選", 1),
                ("More recommendations", "更多建議", 2),
            },
            restart: RestartScope.Explorer, keywords: "start,layout,開始,版面,釘選"),

        // 13) Combine taskbar buttons & hide labels (real Win11 Advanced value).
        // Three mutually-exclusive named options — clearer as radio buttons.
        // 三個互斥選項用單選按鈕更清楚；行為／登錄檔完全一樣。
        RegRadio("appearance.taskbar-glom", "Combine taskbar buttons", "合併工作列按鈕",
            "Choose when taskbar buttons are combined and labels hidden.",
            "揀幾時合併工作列按鈕同收埋文字標籤。",
            Advanced, "TaskbarGlomLevel",
            new (string en, string zh, int value)[]
            {
                ("Always combine, hide labels", "永遠合併、收埋標籤", 0),
                ("Combine when taskbar is full", "工作列滿先合併", 1),
                ("Never combine", "永遠唔合併", 2),
            },
            restart: RestartScope.Explorer, keywords: "taskbar,combine,labels,工作列,合併,標籤"),

        // 14) Personalisation: open the Colours settings page (real ms-settings command).
        Tweak.Shell("appearance.open-colors", "Open colour settings", "開啟色彩設定",
            "Launch the Windows Personalisation > Colours settings page.",
            "開啟 Windows「個人化 > 色彩」設定頁。",
            "Open", "開啟", "explorer.exe", "ms-settings:colors",
            keywords: "settings,colours,personalisation,色彩,個人化"),

        // 14b) Accent colour picker with a live swatch + tint/shade strip.
        //      Writes the documented DWM AccentColor + Explorer accent palette as ABGR DWords, then
        //      nudges Explorer so chrome picks it up. Rich: code-drawn swatch preview (live).
        //      主題色揀選器，附活動色板同色階帶；寫入 DWM／Explorer 主題色（ABGR DWord）。
        AccentColour(),

        // 15) Disable JPEG wallpaper compression (HKCU\Control Panel\Desktop\JPEGImportQuality=100).
        // Writes the value, then re-applies the current wallpaper so the new quality takes effect.
        Tweak.CustomToggle("appearance.wallpaper-quality", "Disable wallpaper JPEG compression", "停用桌布 JPEG 壓縮",
            "Set wallpaper import quality to 100 so Windows stops recompressing JPG wallpapers (re-applies the current wallpaper).",
            "將桌布匯入品質設為 100，唔再壓縮 JPG 桌布（會即時重新套用目前桌布）。",
            getIsOn: () => RegistryHelper.ValueEquals(RegRoot.HKCU, @"Control Panel\Desktop", "JPEGImportQuality", 100),
            setIsOn: on =>
            {
                if (on) RegistryHelper.SetValue(RegRoot.HKCU, @"Control Panel\Desktop", "JPEGImportQuality", 100, RegistryValueKind.DWord);
                else RegistryHelper.DeleteValue(RegRoot.HKCU, @"Control Panel\Desktop", "JPEGImportQuality");
                // Re-apply the current wallpaper so the new quality takes effect immediately.
                WallpaperHelper.ReapplyCurrentWallpaper();
            },
            keywords: "wallpaper,jpeg,quality,compression,桌布,壓縮,品質"),
    };

    /// <summary>
    /// 深色模式開關 · Dark-mode toggle (apps + shell), behaviour identical to the original
    /// CustomToggle but with a live mini-window <see cref="TweakVisuals.ThemePreview"/>.
    /// </summary>
    private static TweakDefinition DarkMode()
    {
        bool IsDark() => RegistryHelper.ValueEquals(RegRoot.HKCU, Personalize, "AppsUseLightTheme", 0);
        bool AccentChrome() => RegistryHelper.ValueEquals(RegRoot.HKCU, Personalize, "ColorPrevalence", 1);
        bool Transparency() => RegistryHelper.ValueEquals(RegRoot.HKCU, Personalize, "EnableTransparency", 1);

        return new TweakDefinition
        {
            Id = "appearance.dark-mode",
            Title = new("Dark mode", "深色模式"),
            Description = new("Use the dark theme for both apps and the Windows shell.",
                "應用程式同 Windows 介面都用深色主題。"),
            Kind = TweakKind.Toggle,
            Restart = RestartScope.Explorer,
            Keywords = new[] { "dark", "theme", "深色", "主題", "黑色" },
            GetIsOn = IsDark,
            SetIsOn = on =>
            {
                int v = on ? 0 : 1; // light theme value is 1; dark is 0
                RegistryHelper.SetValue(RegRoot.HKCU, Personalize, "AppsUseLightTheme", v, RegistryValueKind.DWord);
                RegistryHelper.SetValue(RegRoot.HKCU, Personalize, "SystemUsesLightTheme", v, RegistryValueKind.DWord);
            },
            VisualLiveUpdate = true,
            VisualBuilder = _ => TweakVisuals.ThemePreview(IsDark, AccentChrome, Transparency),
        };
    }

    /// <summary>
    /// 主題色揀選器 · Accent-colour picker.
    /// 讀寫 DWM/Explorer 主題色（以 ABGR DWord 儲存），並附活動色板預覽 ·
    /// reads/writes the accent colour (stored as an ABGR DWord) with a live swatch preview.
    /// </summary>
    private static TweakDefinition AccentColour()
    {
        const string Dwm = @"Software\Microsoft\Windows\DWM";
        const string Accent = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";

        string GetHex()
        {
            // DWM AccentColor is an ABGR DWord (0xAABBGGRR). Fall back to a sensible default.
            var raw = RegistryHelper.GetValue(RegRoot.HKCU, Dwm, "AccentColor");
            if (raw is int dw)
            {
                byte r = (byte)(dw & 0xFF);
                byte g = (byte)((dw >> 8) & 0xFF);
                byte b = (byte)((dw >> 16) & 0xFF);
                return $"#{r:X2}{g:X2}{b:X2}";
            }
            return "#0078D7";
        }

        void SetHex(string hex)
        {
            var s = hex.Trim().TrimStart('#');
            byte r = Convert.ToByte(s.Substring(0, 2), 16);
            byte g = Convert.ToByte(s.Substring(2, 2), 16);
            byte b = Convert.ToByte(s.Substring(4, 2), 16);
            int abgr = (0xFF << 24) | (b << 16) | (g << 8) | r;       // 0xAABBGGRR for DWM
            int agbr = (0xC4 << 24) | (b << 16) | (g << 8) | r;       // Explorer accent palette entry
            RegistryHelper.SetValue(RegRoot.HKCU, Dwm, "AccentColor", abgr, RegistryValueKind.DWord);
            RegistryHelper.SetValue(RegRoot.HKCU, Dwm, "ColorizationColor", abgr, RegistryValueKind.DWord);
            RegistryHelper.SetValue(RegRoot.HKCU, Dwm, "ColorizationAfterglow", abgr, RegistryValueKind.DWord);
            RegistryHelper.SetValue(RegRoot.HKCU, Accent, "AccentColorMenu", agbr, RegistryValueKind.DWord);
        }

        return new TweakDefinition
        {
            Id = "appearance.accent-colour",
            Title = new("Accent colour", "主題色"),
            Description = new("Pick the Windows accent colour; the swatch below previews tints and shades.",
                "揀 Windows 主題色；下面色板會預覽佢嘅深淺色階。"),
            Kind = TweakKind.Color,
            Restart = RestartScope.Explorer,
            Keywords = new[] { "accent", "colour", "color", "主題色", "顏色" },
            GetHex = GetHex,
            SetHex = SetHex,
            VisualLiveUpdate = true,
            VisualBuilder = _ => TweakVisuals.ColorSwatch(GetHex),
        };
    }

    /// <summary>
    /// 由單一 HKCU DWord 登錄檔值支援嘅單選按鈕組 · A RadioButtons group backed by a single HKCU DWord value.
    /// 讀寫語意同 <see cref="Tweak.RegChoice"/> 一模一樣（用 ValueEquals 比對、寫入整數 DWord），
    /// 只係改用單選按鈕呈現。Same read/write semantics as RegChoice (ValueEquals match, writes an int DWord);
    /// only the presentation differs.
    /// </summary>
    private static TweakDefinition RegRadio(
        string id, string enT, string zhT, string enD, string zhD,
        string path, string name,
        (string en, string zh, int value)[] options,
        RestartScope restart = RestartScope.None, string? keywords = null)
        => Tweak.RadioGroup(id, enT, zhT, enD, zhD,
            Array.ConvertAll(options, o => (o.en, o.zh, o.value.ToString())),
            getCurrent: () =>
            {
                foreach (var o in options)
                    if (RegistryHelper.ValueEquals(RegRoot.HKCU, path, name, o.value))
                        return o.value.ToString();
                return null;
            },
            setChoice: val =>
            {
                foreach (var o in options)
                    if (string.Equals(o.value.ToString(), val, StringComparison.OrdinalIgnoreCase))
                    {
                        RegistryHelper.SetValue(RegRoot.HKCU, path, name, o.value, RegistryValueKind.DWord);
                        return;
                    }
            },
            restart: restart, keywords: keywords);
}