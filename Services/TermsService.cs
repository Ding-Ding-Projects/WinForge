using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 首次啟動條款同細則閘 · First-launch Terms &amp; Conditions gate.
/// 使用者必須讀完條款，並且喺 5 題小測驗攞到 5/5 先可以接受同繼續。
/// The user must read the terms, then score 5/5 on a short quiz before they can accept and continue.
/// 條款閱讀器支援語言選擇、自訂字型／字級／顏色、粗體／斜體／刪除線、變更視窗主題、複製、另存 TXT／PDF。
/// The reader supports language selection, custom font/size/colour, bold/italic/strikethrough, window
/// theme switching, copy, and Save as TXT/PDF. Once accepted the answer is persisted via a dedicated
/// marker file (settings.json can be clobbered by other instances) so the gate never shows again.
/// </summary>
public static class TermsService
{
    private const string AcceptedKey = "terms.accepted.v1";
    private const int PassMark = 5;

    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge");

    /// <summary>專用標記檔，唔受 settings.json 被其他實例覆寫影響 · Dedicated marker file, immune to settings.json clobbering.</summary>
    private static readonly string MarkerPath = Path.Combine(Dir, "terms-accepted.v1");

    public static bool HasAccepted =>
        File.Exists(MarkerPath) ||
        string.Equals(SettingsStore.Get(AcceptedKey, "false"), "true", StringComparison.OrdinalIgnoreCase);

    private static void MarkAccepted()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("o"));
        }
        catch { /* best effort — settings key below is the fallback */ }
        SettingsStore.Set(AcceptedKey, "true");
        SettingsStore.Set("terms.accepted.utc", DateTime.UtcNow.ToString("o"));
    }

    private static string P(string en, string zh) => Loc.I.Pick(en, zh);

    // ─────────────────────────────────────────────────────────────────────────
    // 防護 · Safety wrappers
    // 任何事件處理器拋例外都會冧 app（同步：未捕捉派發例外；async void：未觀察例外）。
    // 全部 UI 事件都包住，PDF／檔案／剪貼簿都唔會整冧 app。對話框顯示亦包住，避免
    // 「同一時間只可開一個 ContentDialog」會擲出而令啟動崩潰或卡死。
    // Any throwing event handler crashes the app (sync handlers throw on the dispatcher; async void
    // handlers raise unobserved exceptions). Wrap every handler. Also wrap ShowAsync, because WinUI
    // throws "only a single ContentDialog can be open" — that exception at startup was crashing/freezing.
    // ─────────────────────────────────────────────────────────────────────────

    private static void Guard(string src, Action body)
    {
        try { body(); }
        catch (Exception ex) { CrashLogger.Log("terms:" + src, ex); }
    }

    private static async void GuardAsync(string src, Func<Task> body)
    {
        try { await body(); }
        catch (Exception ex) { CrashLogger.Log("terms:" + src, ex); }
    }

    /// <summary>顯示對話框且永不拋例外；出錯回 null（當作非主要按鈕）· Show a dialog that never throws; null on error.</summary>
    private static async Task<ContentDialogResult?> SafeShowAsync(ContentDialog dlg)
    {
        try { return await dlg.ShowAsync(); }
        catch (Exception ex) { CrashLogger.Log("terms:ShowAsync", ex); return null; }
    }

    public static async Task<bool> EnsureAcceptedAsync(XamlRoot xamlRoot)
    {
        if (HasAccepted) return true;
        if (xamlRoot is null) return true;     // 無 UI 根（無頭模式）就唔阻塞 · no root (headless) → don't block

        // 整個閘包一層：任何未預期錯誤都「容錯放行」（唔接受、唔退出、唔卡死），下次啟動再彈。
        // The whole gate is wrapped: any unexpected error fails OPEN — we neither mark accepted nor exit
        // the app nor block; the gate simply reappears next launch. Returning true here means "don't exit".
        try
        {
            // null = 顯示失敗（例如對話框衝突）→ 容錯放行，唔好退出 app。
            // null = the dialog could not be shown (e.g. dialog conflict) → fail open, do not exit.
            var read = await ShowTermsAsync(xamlRoot);
            if (read is null) return true;          // couldn't show → don't exit
            if (read == false) return false;        // user declined → caller exits

            while (true)
            {
                int score = await RunQuizAsync(xamlRoot);
                if (score == QuizError) return true;    // couldn't show quiz → fail open
                if (score < 0) return false;            // user declined
                if (score >= PassMark)
                {
                    MarkAccepted();
                    await ShowInfoAsync(xamlRoot,
                        P("Welcome to WinForge", "歡迎使用 WinForge"),
                        P("You scored 5/5. Terms accepted — enjoy WinForge!",
                          "你考到 5/5。條款已接受 — 盡情使用 WinForge！"));
                    return true;
                }

                var again = await ShowRetryAsync(xamlRoot, score);
                if (again is null) return true;     // couldn't show → fail open
                if (again == false) return false;   // user declined
            }
        }
        catch (Exception ex)
        {
            CrashLogger.Log("terms:gate (failing open)", ex);
            return true;        // never crash / exit / block the app because of the gate
        }
    }

    private const int QuizError = -2;     // RunQuizAsync sentinel: dialog could not be shown

    // ─────────────────────────────────────────────────────────────────────────
    // 條款閱讀器 · The rich terms reader
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task<bool?> ShowTermsAsync(XamlRoot xamlRoot)
    {
        // 條款本身嘅語言（獨立於 app 語言）· The language the terms are shown in (independent of the app language).
        var termsLang = Loc.I.Language;

        // 即時格式狀態 · Live formatting state.
        var body = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            FontSize = 15,
            Text = TermsTextFor(termsLang),
            LineHeight = 22,
        };

        var scroller = new ScrollViewer
        {
            Content = new Border { Child = body, Padding = new Thickness(4, 4, 14, 4) },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 360,
        };

        // —— 工具列 · Toolbar ——
        var langBox = new ComboBox
        {
            MinWidth = 150,
            Items =
            {
                new ComboBoxItem { Content = "English",  Tag = AppLanguage.English },
                new ComboBoxItem { Content = "繁中・粵語", Tag = AppLanguage.Cantonese },
                new ComboBoxItem { Content = "Bilingual 雙語", Tag = AppLanguage.Bilingual },
            },
        };
        langBox.SelectedIndex = termsLang switch
        {
            AppLanguage.English => 0,
            AppLanguage.Cantonese => 1,
            _ => 2,
        };
        langBox.SelectionChanged += (_, _) => Guard("lang", () =>
        {
            if (langBox.SelectedItem is ComboBoxItem { Tag: AppLanguage l })
            {
                termsLang = l;
                body.Text = TermsTextFor(l);
            }
        });

        var fontBox = new ComboBox { MinWidth = 170 };
        foreach (var f in FontChoices) fontBox.Items.Add(new ComboBoxItem { Content = f, Tag = f });
        fontBox.SelectedIndex = 0;
        fontBox.SelectionChanged += (_, _) => Guard("font", () =>
        {
            if (fontBox.SelectedItem is ComboBoxItem { Tag: string fam })
                body.FontFamily = new FontFamily(fam);
        });

        var sizeBox = new NumberBox
        {
            Minimum = 9, Maximum = 40, Value = 15, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            MinWidth = 120,
        };
        sizeBox.ValueChanged += (_, _) => Guard("size", () =>
        {
            if (!double.IsNaN(sizeBox.Value) && sizeBox.Value >= 9)
            {
                body.FontSize = sizeBox.Value;
                body.LineHeight = sizeBox.Value * 1.45;
            }
        });

        var bold = new ToggleButton { Content = new FontIcon { Glyph = "" } };
        ToolTipService.SetToolTip(bold, P("Bold", "粗體"));
        var italic = new ToggleButton { Content = new FontIcon { Glyph = "" } };
        ToolTipService.SetToolTip(italic, P("Italic", "斜體"));
        var strike = new ToggleButton { Content = new FontIcon { Glyph = "" } };
        ToolTipService.SetToolTip(strike, P("Strikethrough", "刪除線"));

        // 用文字圖示避免 glyph 字碼含糊（例如刪除線睇落似底線）· Use text glyphs so the icons are unambiguous.
        bold.Content = new TextBlock { Text = "B", FontWeight = FontWeights.Bold, FontSize = 16 };
        italic.Content = new TextBlock { Text = "I", FontStyle = Windows.UI.Text.FontStyle.Italic, FontSize = 16, FontFamily = new FontFamily("Georgia") };
        strike.Content = new TextBlock { Text = "S", FontSize = 16, TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough };

        void ApplyStyle()
        {
            body.FontWeight = (bold.IsChecked == true) ? FontWeights.Bold : FontWeights.Normal;
            body.FontStyle = (italic.IsChecked == true) ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal;
            body.TextDecorations = (strike.IsChecked == true)
                ? Windows.UI.Text.TextDecorations.Strikethrough
                : Windows.UI.Text.TextDecorations.None;
        }
        bold.Click += (_, _) => Guard("bold", ApplyStyle);
        italic.Click += (_, _) => Guard("italic", ApplyStyle);
        strike.Click += (_, _) => Guard("strike", ApplyStyle);

        // 字體顏色（ColorPicker 喺 flyout）· Font colour via a ColorPicker flyout.
        Color? chosenColor = null;
        var picker = new ColorPicker
        {
            IsAlphaEnabled = false,
            IsMoreButtonVisible = true,
            Color = Colors.SteelBlue,
            Width = 300,
        };
        var applyColor = new Button { Content = P("Apply", "套用"), Margin = new Thickness(0, 8, 0, 0) };
        var colorFlyout = new Flyout { Content = new StackPanel { Children = { picker, applyColor } } };
        applyColor.Click += (_, _) => Guard("color.apply", () =>
        {
            chosenColor = picker.Color;
            body.Foreground = new SolidColorBrush(picker.Color);
            colorFlyout.Hide();
        });
        var colorBtn = new Button { Content = new FontIcon { Glyph = "" }, Flyout = colorFlyout };
        ToolTipService.SetToolTip(colorBtn, P("Font colour", "字體顏色"));
        var resetColor = new Button { Content = new FontIcon { Glyph = "" } };
        ToolTipService.SetToolTip(resetColor, P("Reset colour", "重設顏色"));
        resetColor.Click += (_, _) => Guard("color.reset", () => { chosenColor = null; body.ClearValue(TextBlock.ForegroundProperty); });

        // 主題（套用到整個視窗根）· Theme applied to the whole window root.
        var themeBox = new ComboBox
        {
            MinWidth = 130,
            Items =
            {
                new ComboBoxItem { Content = P("System theme", "系統主題"), Tag = ElementTheme.Default },
                new ComboBoxItem { Content = P("Light", "淺色"), Tag = ElementTheme.Light },
                new ComboBoxItem { Content = P("Dark", "深色"), Tag = ElementTheme.Dark },
            },
            SelectedIndex = 0,
        };
        themeBox.SelectionChanged += (_, _) => Guard("theme", () =>
        {
            if (themeBox.SelectedItem is ComboBoxItem { Tag: ElementTheme t })
                App.SetTheme(t);
        });

        // 動作：複製 / 另存 TXT / 另存 PDF · Actions: copy / save TXT / save PDF.
        var copyBtn = new Button { Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6,
            Children = { new FontIcon { Glyph = "" }, new TextBlock { Text = P("Copy", "複製") } } } };
        copyBtn.Click += (_, _) => Guard("copy", () =>
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(body.Text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        });

        var txtBtn = new Button { Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6,
            Children = { new FontIcon { Glyph = "" }, new TextBlock { Text = P("Save TXT", "存 TXT") } } } };
        txtBtn.Click += (_, _) => GuardAsync("save.txt", async () =>
        {
            var path = await FileDialogs.SaveFileAsync("WinForge-Terms", ".txt");
            if (!string.IsNullOrEmpty(path))
                File.WriteAllText(path, body.Text);
        });

        var pdfBtn = new Button { Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6,
            Children = { new FontIcon { Glyph = "" }, new TextBlock { Text = P("Save PDF", "存 PDF") } } } };
        pdfBtn.Click += (_, _) => GuardAsync("save.pdf", async () =>
        {
            var path = await FileDialogs.SaveFileAsync("WinForge-Terms", ".pdf");
            if (string.IsNullOrEmpty(path)) return;
            var family = (fontBox.SelectedItem as ComboBoxItem)?.Tag as string ?? FontChoices[0];
            double sz = (sizeBox.Value >= 9 && !double.IsNaN(sizeBox.Value)) ? sizeBox.Value : 15;
            var col = chosenColor;
            string text = body.Text;
            bool b = bold.IsChecked == true, i = italic.IsChecked == true, s = strike.IsChecked == true;
            await Task.Run(() => RenderPdf(path!, text, family, sz, b, i, s, col));
        });

        // 工具列分兩行排，確保全部按鈕都見得到（唔使橫向捲）· Two rows so every control is visible (no horizontal scrolling).
        TextBlock Label(string en, string zh) => new()
        {
            Text = P(en, zh), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0),
        };

        var row1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row1.Children.Add(Label("Language", "語言"));
        row1.Children.Add(langBox);
        row1.Children.Add(Label("Font", "字型"));
        row1.Children.Add(fontBox);
        row1.Children.Add(sizeBox);
        row1.Children.Add(bold);
        row1.Children.Add(italic);
        row1.Children.Add(strike);
        row1.Children.Add(colorBtn);
        row1.Children.Add(resetColor);

        var row2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row2.Children.Add(Label("Theme", "主題"));
        row2.Children.Add(themeBox);
        row2.Children.Add(copyBtn);
        row2.Children.Add(txtBtn);
        row2.Children.Add(pdfBtn);

        var toolbar = new StackPanel { Spacing = 8, Children = { row1, row2 } };

        var layout = new StackPanel { Spacing = 12, Width = 860 };
        layout.Children.Add(toolbar);
        layout.Children.Add(scroller);
        layout.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
            Text = P("To continue you must pass a short 5-question quiz with a perfect 5/5 score.",
                     "要繼續，你必須喺一個 5 題嘅小測驗攞到滿分 5/5。"),
        });

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = P("WinForge — Terms & Conditions", "WinForge — 條款及細則"),
            Content = layout,
            PrimaryButtonText = P("I have read — continue to quiz", "我已閱讀 — 繼續測驗"),
            CloseButtonText = P("Decline & Exit", "拒絕並退出"),
            DefaultButton = ContentDialogButton.Primary,
        };
        dlg.Resources["ContentDialogMaxWidth"] = 1200.0;

        var r = await SafeShowAsync(dlg);
        if (r is null) return null;                         // couldn't show → caller fails open
        return r == ContentDialogResult.Primary;
    }

    /// <summary>用 PdfSharp 將條款渲染成 PDF，保留字型／字級／粗斜／刪除線／顏色 · Render the terms to PDF honouring the chosen formatting.</summary>
    private static void RenderPdf(string path, string text, string family, double size,
        bool bold, bool italic, bool strike, Color? color)
    {
        var style = XFontStyleEx.Regular;
        if (bold) style |= XFontStyleEx.Bold;
        if (italic) style |= XFontStyleEx.Italic;
        if (strike) style |= XFontStyleEx.Strikeout;

        XFont font;
        try { font = new XFont(family, size, style); }
        catch { font = new XFont("Microsoft JhengHei", size, style); }   // CJK-safe fallback

        var brush = color is { } c ? new XSolidBrush(XColor.FromArgb(c.A, c.R, c.G, c.B)) : XBrushes.Black;

        using var doc = new PdfDocument();
        const double margin = 56;
        double lineHeight = size * 1.45;

        PdfPage page = doc.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        double maxWidth = page.Width.Point - margin * 2;
        double y = margin;

        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            // 逐字／逐詞自動換行 · word-wrap each paragraph line to the page width.
            foreach (var visualLine in WrapLine(gfx, rawLine, font, maxWidth))
            {
                if (y + lineHeight > page.Height.Point - margin)
                {
                    gfx.Dispose();
                    page = doc.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = margin;
                }
                if (visualLine.Length > 0)
                    gfx.DrawString(visualLine, font, brush, new XPoint(margin, y));
                y += lineHeight;
            }
        }
        gfx.Dispose();
        doc.Save(path);
    }

    private static IEnumerable<string> WrapLine(XGraphics gfx, string line, XFont font, double maxWidth)
    {
        if (line.Length == 0) { yield return ""; yield break; }

        var current = new System.Text.StringBuilder();
        foreach (var ch in line)
        {
            var trial = current.ToString() + ch;
            if (gfx.MeasureString(trial, font).Width > maxWidth && current.Length > 0)
            {
                // 喺最近嘅空格切（西文），冇就硬切（中文）· break at the last space (Latin), else hard-break (CJK).
                string s = current.ToString();
                int sp = s.LastIndexOf(' ');
                if (sp > 0)
                {
                    yield return s.Substring(0, sp);
                    current.Clear();
                    current.Append(s.Substring(sp + 1));
                }
                else
                {
                    yield return s;
                    current.Clear();
                }
            }
            current.Append(ch);
        }
        if (current.Length > 0) yield return current.ToString();
    }

    private static readonly string[] FontChoices =
    {
        "Segoe UI", "Segoe UI Variable", "Microsoft JhengHei UI", "Microsoft JhengHei",
        "PMingLiU", "DFKai-SB", "Cambria", "Georgia", "Times New Roman",
        "Consolas", "Cascadia Mono", "Arial", "Verdana", "Calibri",
    };

    // ─────────────────────────────────────────────────────────────────────────
    // 測驗 · Quiz
    // ─────────────────────────────────────────────────────────────────────────

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
            var rb = new RadioButtons { Header = $"{n}. {q.Prompt}", ItemsSource = q.Options.ToList() };
            groups.Add(rb);
            panel.Children.Add(rb);
            n++;
        }

        var scroller = new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 440,
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

        var r = await SafeShowAsync(dlg);
        if (r is null) return QuizError;                    // couldn't show → fail open
        if (r != ContentDialogResult.Primary) return -1;    // declined

        int score = 0;
        for (int i = 0; i < Questions.Length; i++)
            if (groups[i].SelectedIndex == Questions[i].CorrectIndex) score++;
        return score;
    }

    private static async Task<bool?> ShowRetryAsync(XamlRoot xamlRoot, int score)
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
        var r = await SafeShowAsync(dlg);
        if (r is null) return null;
        return r == ContentDialogResult.Primary;
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
        await SafeShowAsync(dlg);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 條款本文 · Terms body
    // ─────────────────────────────────────────────────────────────────────────

    private static string TermsTextFor(AppLanguage lang) => lang switch
    {
        AppLanguage.English => TermsEn,
        AppLanguage.Cantonese => TermsZh,
        _ => TermsEn + "\n\n———\n\n" + TermsZh,
    };

    private const string TermsEn =
        "WinForge — Terms & Conditions\nLast updated: 28 June 2026\n\n" +
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
        "To confirm you understand these Terms, you must pass a short 5-question quiz with a perfect score.";

    private const string TermsZh =
        "WinForge — 條款及細則\n最後更新：2026 年 6 月 28 日\n\n" +
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
        "為確認你明白本條款，你必須喺一個 5 題嘅小測驗攞到滿分。";

    private sealed record Quiz(string Prompt, string[] Options, int CorrectIndex);

    private static Quiz[] Questions => _questions ??= BuildQuestions();
    private static Quiz[]? _questions;

    private static Quiz[] BuildQuestions() => new[]
    {
        new Quiz(
            P("What is the WinForge nuclear reactor?", "WinForge 嘅核反應堆係咩嚟？"),
            new[]
            {
                P("A real reactor controlled over the internet", "一個透過互聯網控制嘅真實反應堆"),
                P("A simulation for education and entertainment only", "一個只供教育同娛樂嘅模擬"),
                P("A cryptocurrency miner", "一個加密貨幣挖礦程式"),
            }, 1),

        new Quiz(
            P("Before applying system tweaks, you are responsible for…", "套用系統調校之前，你有責任…"),
            new[]
            {
                P("Creating backups / restore points", "建立備份／還原點"),
                P("Nothing — WinForge guarantees safety", "乜都唔使做 — WinForge 保證安全"),
                P("Disabling Windows Update permanently", "永久停用 Windows Update"),
            }, 0),

        new Quiz(
            P("Features with real-world side-effects (e.g. real shutdown) are…", "會產生真實對外影響嘅功能（例如真實關機）係…"),
            new[]
            {
                P("Always on and cannot be turned off", "永遠開啟，無得關"),
                P("Off by default, opt-in, and reversible", "預設關閉、須自行開啟、可還原"),
                P("Triggered randomly", "隨機觸發"),
            }, 1),

        new Quiz(
            P("What warranty does WinForge provide?", "WinForge 提供咩保證？"),
            new[]
            {
                P("A lifetime money-back guarantee", "終身退款保證"),
                P("None — the software is provided \"AS IS\"", "無 — 軟件按「現狀」提供"),
                P("A guarantee against all data loss", "保證唔會有任何資料遺失"),
            }, 1),

        new Quiz(
            P("Where are your settings and secrets stored?", "你嘅設定同密鑰儲存喺邊度？"),
            new[]
            {
                P("Sold to advertisers", "賣畀廣告商"),
                P("On a public cloud server", "公共雲端伺服器"),
                P("Locally on your device (secrets via Windows DPAPI)", "你部裝置本機（密鑰以 Windows DPAPI 保護）"),
            }, 2),
    };
}
