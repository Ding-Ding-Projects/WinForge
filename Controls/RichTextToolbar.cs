using System;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Controls;

/// <summary>
/// 可重用嘅格式化工具列 · A reusable, per-instance rich-text formatting toolbar.
///
/// 同一個工具列嘅機制（字型、字級、粗／斜／刪除線、字色、主題、複製、存 TXT／PDF）原本喺
/// 首次啟動嘅條款閱讀器內聯實作；呢個控制項將佢抽成可重用、每個實例獨立狀態嘅控制項。
/// The same toolbar mechanics first built inline in the first-launch Terms reader, extracted into a
/// reusable control whose formatting state lives entirely in <b>instance fields</b> and is applied to a
/// <b>single</b> target supplied at construction.
///
/// <para><b>硬性要求 · Hard requirement:</b> 狀態係每個文字框獨立嘅 — 改一個框唔會影響另一個框或者
/// 成個 app。No global/<c>static</c> state; theme is scoped to this surface's own container via
/// <see cref="FrameworkElement.RequestedTheme"/>, never <c>App.SetTheme</c>.</para>
///
/// <para><b>容錯 · Error handling (handoff 54 §4b):</b> 每個事件處理器都包住（<see cref="Guard"/> /
/// <see cref="GuardAsync"/>），對話框經 <see cref="SafeShowAsync"/>，重活（PDF）走背景執行緒，
/// 出錯只記錄唔會整冧 app。Every handler is wrapped, long work is off the UI thread, failures fail open.</para>
/// </summary>
public sealed class RichTextToolbar : UserControl
{
    /// <summary>目標文字面嘅種類 · The kind of text surface the toolbar drives.</summary>
    public enum Mode
    {
        /// <summary>唯讀（<see cref="TextBlock"/>）· Read-only TextBlock.</summary>
        ReadOnly,
        /// <summary>可編輯純文字（<see cref="TextBox"/>）· Editable plain TextBox.</summary>
        Editable,
        /// <summary>可編輯富文字（<see cref="RichEditBox"/>，按選取套用）· Editable RichEditBox (per-selection).</summary>
        RichEditable,
    }

    // ── 每實例格式狀態（絕不 static）· Per-instance formatting state (never static) ──
    private readonly FrameworkElement _target;
    private readonly Mode _mode;
    private readonly FrameworkElement? _themeScope;
    private readonly Func<int, string?>? _languageProvider;

    private string _family;
    private double _size;
    private bool _bold, _italic, _strike;
    private Color? _colour;
    private ElementTheme _theme = ElementTheme.Default;

    // ── 工具列控件 · toolbar widgets we read back from ──
    private readonly ComboBox _fontBox;
    private readonly NumberBox _sizeBox;
    private readonly ToggleButton _boldBtn, _italicBtn, _strikeBtn;

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    /// <summary>
    /// 建構工具列 · Construct a toolbar bound to <paramref name="target"/>.
    /// </summary>
    /// <param name="target">要格式化嘅文字面 · the text surface to format (TextBlock / TextBox / RichEditBox).</param>
    /// <param name="mode">面嘅種類 · the surface kind.</param>
    /// <param name="themeScope">
    /// 主題只套用喺呢個容器（連同 target 同工具列嘅最細外層）· the smallest container wrapping just this
    /// surface + toolbar; <c>RequestedTheme</c> is written here only. If null, falls back to this control's
    /// own parent at apply-time (still scoped, never app-wide).
    /// </param>
    /// <param name="languageProvider">
    /// 可選：對於有雙語原文嘅面，回傳對應語言嘅文字（index：0=En,1=Zh,2=雙語）· optional hook for
    /// surfaces with bilingual source text; most surfaces leave this null.
    /// </param>
    public RichTextToolbar(FrameworkElement target, Mode mode,
        FrameworkElement? themeScope = null, Func<int, string?>? languageProvider = null)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _mode = mode;
        _themeScope = themeScope;
        _languageProvider = languageProvider;

        // 由目標目前值初始化狀態 · seed state from the target's current values.
        _family = FamilyOf(target) ?? TextExportService.FontChoices[0];
        _size = SizeOf(target);
        _bold = false; _italic = false; _strike = false;
        _colour = null;

        // ── Row 1 : font / size / B I S / colour ──
        _fontBox = new ComboBox { MinWidth = 170 };
        foreach (var f in TextExportService.FontChoices)
            _fontBox.Items.Add(new ComboBoxItem { Content = f, Tag = f });
        SelectFamily(_family);
        ToolTipService.SetToolTip(_fontBox, P("Font", "字型"));
        _fontBox.SelectionChanged += (_, _) => Guard("font", () =>
        {
            if (_fontBox.SelectedItem is ComboBoxItem { Tag: string fam }) { _family = fam; Apply(); }
        });

        _sizeBox = new NumberBox
        {
            Minimum = 9, Maximum = 72, Value = _size,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, MinWidth = 116,
        };
        ToolTipService.SetToolTip(_sizeBox, P("Font size", "字級"));
        _sizeBox.ValueChanged += (_, _) => Guard("size", () =>
        {
            if (!double.IsNaN(_sizeBox.Value) && _sizeBox.Value >= 9) { _size = _sizeBox.Value; Apply(); }
        });

        _boldBtn = new ToggleButton { Content = new TextBlock { Text = "B", FontWeight = FontWeights.Bold, FontSize = 16 } };
        ToolTipService.SetToolTip(_boldBtn, P("Bold", "粗體"));
        _italicBtn = new ToggleButton { Content = new TextBlock { Text = "I", FontStyle = Windows.UI.Text.FontStyle.Italic, FontSize = 16, FontFamily = new FontFamily("Georgia") } };
        ToolTipService.SetToolTip(_italicBtn, P("Italic", "斜體"));
        _strikeBtn = new ToggleButton { Content = new TextBlock { Text = "S", FontSize = 16, TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough } };
        ToolTipService.SetToolTip(_strikeBtn, P("Strikethrough", "刪除線"));

        _boldBtn.Click += (_, _) => Guard("bold", () => { _bold = _boldBtn.IsChecked == true; Apply(); });
        _italicBtn.Click += (_, _) => Guard("italic", () => { _italic = _italicBtn.IsChecked == true; Apply(); });
        _strikeBtn.Click += (_, _) => Guard("strike", () => { _strike = _strikeBtn.IsChecked == true; Apply(); });

        var picker = new ColorPicker { IsAlphaEnabled = false, IsMoreButtonVisible = true, Color = Colors.SteelBlue, Width = 300 };
        var applyColour = new Button { Content = P("Apply", "套用"), Margin = new Thickness(0, 8, 0, 0) };
        var colourFlyout = new Flyout { Content = new StackPanel { Children = { picker, applyColour } } };
        applyColour.Click += (_, _) => Guard("colour.apply", () => { _colour = picker.Color; Apply(); colourFlyout.Hide(); });
        var colourBtn = new Button { Content = new FontIcon { Glyph = "" }, Flyout = colourFlyout };
        ToolTipService.SetToolTip(colourBtn, P("Font colour", "字體顏色"));
        var resetColour = new Button { Content = new FontIcon { Glyph = "" } };
        ToolTipService.SetToolTip(resetColour, P("Reset colour", "重設顏色"));
        resetColour.Click += (_, _) => Guard("colour.reset", () => { _colour = null; Apply(); });

        // ── Row 2 : theme / copy / save txt / save pdf (+ optional language) ──
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
        ToolTipService.SetToolTip(themeBox, P("Theme (this surface only)", "主題（只影響呢個面）"));
        themeBox.SelectionChanged += (_, _) => Guard("theme", () =>
        {
            if (themeBox.SelectedItem is ComboBoxItem { Tag: ElementTheme t }) { _theme = t; ApplyTheme(); }
        });

        ComboBox? langBox = null;
        if (_languageProvider is not null)
        {
            langBox = new ComboBox
            {
                MinWidth = 140,
                Items =
                {
                    new ComboBoxItem { Content = "English", Tag = 0 },
                    new ComboBoxItem { Content = "繁中・粵語", Tag = 1 },
                    new ComboBoxItem { Content = "Bilingual 雙語", Tag = 2 },
                },
                SelectedIndex = 2,
            };
            langBox.SelectionChanged += (_, _) => Guard("lang", () =>
            {
                if (langBox!.SelectedItem is ComboBoxItem { Tag: int idx })
                {
                    var t = _languageProvider!(idx);
                    if (t is not null) SetText(t);
                }
            });
        }

        var copyBtn = ActionButton("", P("Copy", "複製"));
        copyBtn.Click += (_, _) => Guard("copy", () =>
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(GetPlainText());
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        });

        var txtBtn = ActionButton("", P("Save TXT", "存 TXT"));
        txtBtn.Click += (_, _) => GuardAsync("save.txt", async () =>
        {
            var path = await FileDialogs.SaveFileAsync("WinForge-Text", ".txt");
            if (string.IsNullOrEmpty(path)) return;
            string text = GetPlainText();
            await System.IO.File.WriteAllTextAsync(path, text);
        });

        var pdfBtn = ActionButton("", P("Save PDF", "存 PDF"));
        pdfBtn.Click += (_, _) => GuardAsync("save.pdf", async () =>
        {
            var path = await FileDialogs.SaveFileAsync("WinForge-Text", ".pdf");
            if (string.IsNullOrEmpty(path)) return;
            // 快照即時格式，再喺背景執行緒渲染 · snapshot live formatting, render off the UI thread.
            string text = GetPlainText();
            string family = _family;
            double sz = _size;
            bool b = _bold, i = _italic, s = _strike;
            Color? col = _colour;
            await Task.Run(() => TextExportService.RenderPdf(path!, text, family, sz, b, i, s, col));
        });

        // ── Layout : ≤ 2 rows so nothing scrolls off ──
        var row1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row1.Children.Add(_fontBox);
        row1.Children.Add(_sizeBox);
        row1.Children.Add(_boldBtn);
        row1.Children.Add(_italicBtn);
        row1.Children.Add(_strikeBtn);
        row1.Children.Add(colourBtn);
        row1.Children.Add(resetColour);

        var row2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (langBox is not null) row2.Children.Add(langBox);
        row2.Children.Add(themeBox);
        row2.Children.Add(copyBtn);
        row2.Children.Add(txtBtn);
        row2.Children.Add(pdfBtn);

        Content = new StackPanel { Spacing = 6, Children = { row1, row2 } };

        // 套用初始狀態（粗斜刪除線初始為關，唔需要寫 family/size 因為已由 target 種子）·
        // No initial Apply needed — fields mirror the target; user actions drive changes.
    }

    // ─────────────────────────────── apply ───────────────────────────────

    /// <summary>將即時格式寫落目標（淨係目標）· Write live formatting onto the target only.</summary>
    private void Apply() => Guard("apply", () =>
    {
        switch (_mode)
        {
            case Mode.RichEditable when _target is RichEditBox reb:
                ApplyToRichEdit(reb);
                break;
            case Mode.Editable when _target is TextBox tb:
                ApplyToTextLike(tb,
                    f => tb.FontFamily = f, sz => tb.FontSize = sz,
                    w => tb.FontWeight = w, st => tb.FontStyle = st,
                    br => tb.Foreground = br, () => tb.ClearValue(TextBox.ForegroundProperty));
                // TextBox has no TextDecorations — strikethrough is a no-op visually (still tracked for PDF).
                break;
            default:
                if (_target is TextBlock blk) ApplyToTextBlock(blk);
                break;
        }
    });

    private void ApplyToTextBlock(TextBlock blk)
    {
        blk.FontFamily = new FontFamily(_family);
        blk.FontSize = _size;
        blk.LineHeight = _size * 1.45;
        blk.FontWeight = _bold ? FontWeights.Bold : FontWeights.Normal;
        blk.FontStyle = _italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal;
        blk.TextDecorations = _strike ? Windows.UI.Text.TextDecorations.Strikethrough : Windows.UI.Text.TextDecorations.None;
        if (_colour is { } c) blk.Foreground = new SolidColorBrush(c);
        else blk.ClearValue(TextBlock.ForegroundProperty);
    }

    private void ApplyToTextLike(TextBox tb, Action<FontFamily> fam, Action<double> size,
        Action<Windows.UI.Text.FontWeight> weight, Action<Windows.UI.Text.FontStyle> style,
        Action<Brush> fg, Action clearFg)
    {
        fam(new FontFamily(_family));
        size(_size);
        weight(_bold ? FontWeights.Bold : FontWeights.Normal);
        style(_italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal);
        if (_colour is { } c) fg(new SolidColorBrush(c));
        else clearFg();
    }

    /// <summary>RichEditBox：套用到目前選取（冇選取就成份文件）· apply to the current selection (or whole doc).</summary>
    private void ApplyToRichEdit(RichEditBox reb)
    {
        var sel = reb.Document.Selection;
        ITextRange range = sel;
        if (sel is null || sel.Length == 0)
        {
            range = reb.Document.GetRange(0, int.MaxValue);   // whole document when nothing selected
        }
        var cf = range.CharacterFormat;
        cf.Name = _family;
        cf.Size = (float)_size;
        cf.Bold = _bold ? FormatEffect.On : FormatEffect.Off;
        cf.Italic = _italic ? FormatEffect.On : FormatEffect.Off;
        cf.Strikethrough = _strike ? FormatEffect.On : FormatEffect.Off;
        if (_colour is { } c) cf.ForegroundColor = c;
        range.CharacterFormat = cf;
    }

    /// <summary>主題只套用喺呢個面嘅容器，永不 App.SetTheme · scope theme to this surface's container only.</summary>
    private void ApplyTheme() => Guard("theme.apply", () =>
    {
        var scope = _themeScope ?? (this.Parent as FrameworkElement) ?? _target;
        scope.RequestedTheme = _theme;
    });

    // ─────────────────────────────── text I/O ───────────────────────────────

    /// <summary>抽出純文字（按模式）· Pull plain text from the target according to its mode.</summary>
    public string GetPlainText()
    {
        try
        {
            return _mode switch
            {
                Mode.RichEditable when _target is RichEditBox reb =>
                    GetRichText(reb),
                Mode.Editable when _target is TextBox tb => tb.Text ?? "",
                _ => (_target as TextBlock)?.Text ?? "",
            };
        }
        catch (Exception ex) { CrashLogger.Log("richtoolbar:getplaintext", ex); return ""; }
    }

    private static string GetRichText(RichEditBox reb)
    {
        reb.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out var s);
        return s ?? "";
    }

    private void SetText(string text) => Guard("settext", () =>
    {
        switch (_mode)
        {
            case Mode.RichEditable when _target is RichEditBox reb:
                reb.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, text);
                break;
            case Mode.Editable when _target is TextBox tb:
                tb.Text = text;
                break;
            default:
                if (_target is TextBlock blk) blk.Text = text;
                break;
        }
    });

    // ─────────────────────────────── helpers ───────────────────────────────

    private void SelectFamily(string family)
    {
        for (int i = 0; i < _fontBox.Items.Count; i++)
            if (_fontBox.Items[i] is ComboBoxItem { Tag: string f } && string.Equals(f, family, StringComparison.OrdinalIgnoreCase))
            { _fontBox.SelectedIndex = i; return; }
        _fontBox.SelectedIndex = 0;
    }

    private static Button ActionButton(string glyph, string label) => new()
    {
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 6,
            Children = { new FontIcon { Glyph = glyph, FontSize = 14 }, new TextBlock { Text = label } },
        },
    };

    private static string? FamilyOf(FrameworkElement el) => el switch
    {
        TextBlock b => b.FontFamily?.Source,
        TextBox t => t.FontFamily?.Source,
        RichEditBox r => r.FontFamily?.Source,
        Control c => c.FontFamily?.Source,
        _ => null,
    };

    private static double SizeOf(FrameworkElement el) => el switch
    {
        TextBlock b when b.FontSize >= 9 => b.FontSize,
        TextBox t when t.FontSize >= 9 => t.FontSize,
        RichEditBox r when r.FontSize >= 9 => r.FontSize,
        Control c when c.FontSize >= 9 => c.FontSize,
        _ => 14,
    };

    // ── 防護 · Safety wrappers (handoff 54 §4b) ──

    private static void Guard(string src, Action body)
    {
        try { body(); }
        catch (Exception ex) { CrashLogger.Log("richtoolbar:" + src, ex); }
    }

    private static async void GuardAsync(string src, Func<Task> body)
    {
        try { await body(); }
        catch (Exception ex) { CrashLogger.Log("richtoolbar:" + src, ex); }
    }

    /// <summary>顯示對話框且永不拋例外 · Show a dialog that never throws; null on failure.</summary>
    public static async Task<ContentDialogResult?> SafeShowAsync(ContentDialog dlg)
    {
        try { return await dlg.ShowAsync(); }
        catch (Exception ex) { CrashLogger.Log("richtoolbar:ShowAsync", ex); return null; }
    }
}
