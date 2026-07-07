using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 科學／工程記數法 · Scientific / engineering notation converter. Input a number (plain, E-notation
/// or "a×10^b"-style) and see standard decimal, scientific, engineering, E-notation and SI-prefix
/// forms, rounded to a chosen number of significant figures. Copy each form. Bilingual. Never throws.
/// </summary>
public sealed partial class SciNotationModule : Page
{
    private SciNotationService.Result _last = new();

    public SciNotationModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) { Render(); Recompute(); }
    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;
    private void OnLang(object? sender, EventArgs e) { Render(); Recompute(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Scientific Notation · 科學記數法";
        HeaderBlurb.Text = P("Turn any number into standard, scientific, engineering, E-notation and SI-prefix forms. Type a plain number, E-notation or an a×10^b expression.",
            "把任何數字轉成標準、科學、工程、E 記數法及 SI 前綴形式。可以打普通數字、E 記數法或者 a×10^b 式。");
        InputLabel.Text = P("Number", "數字");
        InputHint.Text = P("Accepts e.g. 12345.678, 1.2345e4, or 1.2×10^4.",
            "接受例如 12345.678、1.2345e4 或 1.2×10^4。");
        SigLabel.Text = P("Significant figures (1–15)", "有效數字（1–15）");
        StdLabel.Text = P("Standard", "標準");
        SciLabel.Text = P("Scientific", "科學");
        EngLabel.Text = P("Engineering", "工程");
        ENotLabel.Text = P("E-notation", "E 記數法");
        SiLabel.Text = P("SI prefix", "SI 前綴");
        UpdateStatusEmpty();
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) => Recompute();

    private void Sig_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) => Recompute();

    private void Recompute()
    {
        try
        {
            string text = InputBox?.Text ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                _last = new SciNotationService.Result();
                ClearOutputs();
                UpdateStatusEmpty();
                return;
            }

            int sig = 6;
            if (SigBox != null && !double.IsNaN(SigBox.Value))
                sig = (int)Math.Round(SigBox.Value);

            var r = SciNotationService.Convert(text, sig);
            _last = r;

            if (!r.Ok)
            {
                ClearOutputs();
                StatusText.Text = P("Could not read that number. Try 12345.678, 1.2345e4 or 1.2×10^4.",
                    "讀不到那個數字。試下 12345.678、1.2345e4 或 1.2×10^4。");
                return;
            }

            StdValue.Text = r.Standard;
            SciValue.Text = r.Scientific;
            EngValue.Text = r.Engineering;
            ENotValue.Text = r.ENotation;
            SiValue.Text = r.SiPrefix;
            StatusText.Text = P($"Rounded to {sig} significant figure(s).", $"已四捨五入到 {sig} 個有效數字。");
        }
        catch (Exception ex)
        {
            ClearOutputs();
            StatusText.Text = P("Something went wrong: " + ex.Message, "出咗點問題：" + ex.Message);
        }
    }

    private void ClearOutputs()
    {
        StdValue.Text = "—";
        SciValue.Text = "—";
        EngValue.Text = "—";
        ENotValue.Text = "—";
        SiValue.Text = "—";
    }

    private void UpdateStatusEmpty()
    {
        if (_last == null || !_last.Ok)
            StatusText.Text = P("Type a number above to see every notation.", "在上面打個數字，就看到每種記數法。");
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_last == null || !_last.Ok) return;
            if (sender is not Button b || b.Tag is not string tag) return;

            string value = tag switch
            {
                "std" => _last.Standard,
                "sci" => _last.Scientific,
                "eng" => _last.Engineering,
                "enot" => _last.ENotation,
                "si" => _last.SiPrefix,
                _ => "",
            };
            if (string.IsNullOrEmpty(value)) return;

            var pkg = new DataPackage();
            pkg.SetText(value);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Copied: " + value, "已複製：" + value);
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Copy failed: " + ex.Message, "複製失敗：" + ex.Message);
        }
    }
}
