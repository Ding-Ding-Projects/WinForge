using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 健康計算器 · Health calculators — BMI, BMR (Mifflin–St Jeor), daily calorie needs (TDEE)
/// and US-Navy body-fat %. Metric/imperial toggle; live compute; all conversions & math in
/// <see cref="BmiService"/>. Never throws — invalid input shows a bilingual status. Bilingual (粵語).
/// Estimates only, not medical advice.
/// </summary>
public sealed partial class BmiModule : Page
{
    private bool _ready;

    public BmiModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        _ready = true;
        Recompute();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private bool Metric => UnitSwitch is null || !UnitSwitch.IsOn;

    // ---- rendering / localization ------------------------------------------
    private void Render()
    {
        try
        {
            Header.Title = "Health Calculators · 健康計算器";
            HeaderBlurb.Text = P(
                "Quick health estimates — BMI, basal metabolic rate, daily calorie needs and body-fat %. Everything updates live as you type.",
                "快速健康估算 — BMI、基礎代謝率、每日熱量需要同體脂率。你打字嘅時候即時計算。");

            UnitsTitle.Text = P("Units", "單位");
            UnitsHint.Text = Metric
                ? P("Metric — cm / kg", "公制 — 厘米 / 公斤")
                : P("Imperial — inches / lb", "英制 — 英寸 / 磅");

            // BMI
            BmiTitle.Text = P("Body Mass Index (BMI)", "身體質量指數（BMI）");
            BmiHeightLabel.Text = HeightLabel();
            BmiWeightLabel.Text = WeightLabel();

            // BMR + calories
            BmrTitle.Text = P("Metabolic rate & daily calories", "代謝率同每日熱量");
            BmrSexLabel.Text = P("Sex", "性別");
            BmrAgeLabel.Text = P("Age (years)", "年齡（歲）");
            BmrHeightLabel.Text = HeightLabel();
            BmrWeightLabel.Text = WeightLabel();
            ActivityLabel.Text = P("Activity level", "活動量");
            FillSexCombo(BmrSex);
            FillActivityCombo();

            // Body fat
            BfTitle.Text = P("Body-fat % (US Navy method)", "體脂率（美國海軍法）");
            BfSexLabel.Text = P("Sex", "性別");
            BfHeightLabel.Text = HeightLabel();
            BfNeckLabel.Text = P("Neck", "頸圍") + LenUnit();
            BfWaistLabel.Text = P("Waist", "腰圍") + LenUnit();
            BfHipsLabel.Text = P("Hips", "臀圍") + LenUnit();
            FillSexCombo(BfSex);

            Disclaimer.Text = P(
                "Estimates only — not medical advice. Consult a healthcare professional for anything that matters.",
                "只係估算 — 唔係醫療建議。有需要請諮詢專業醫護人員。");

            Recompute();
        }
        catch { /* never throw from render */ }
    }

    private string HeightLabel() => P("Height", "身高") + LenUnit();
    private string WeightLabel() => P("Weight", "體重") + (Metric ? " (kg)" : " (lb)");
    private string LenUnit() => Metric ? " (cm)" : " (in)";

    private void FillSexCombo(ComboBox box)
    {
        int sel = box.SelectedIndex < 0 ? 0 : box.SelectedIndex;
        box.Items.Clear();
        box.Items.Add(P("Male", "男"));
        box.Items.Add(P("Female", "女"));
        box.SelectedIndex = sel;
    }

    private void FillActivityCombo()
    {
        int sel = ActivityBox.SelectedIndex < 0 ? 0 : ActivityBox.SelectedIndex;
        ActivityBox.Items.Clear();
        foreach (var lvl in BmiService.ActivityLevels)
            ActivityBox.Items.Add(P(lvl.En, lvl.Zh));
        ActivityBox.SelectedIndex = sel;
    }

    // ---- event handlers -----------------------------------------------------
    private void Units_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        Render(); // relabels units + recomputes
    }

    private void Any_Changed(NumberBox sender, NumberBoxValueChangedEventArgs e) => Recompute();

    private void Sel_Changed(object sender, SelectionChangedEventArgs e) => Recompute();

    // ---- compute ------------------------------------------------------------
    private static double V(NumberBox box) => double.IsNaN(box.Value) ? 0 : box.Value;

    private void Recompute()
    {
        if (!_ready) return;
        try
        {
            ComputeBmi();
            ComputeBmr();
            ComputeBodyFat();
        }
        catch { /* never throw */ }
    }

    private void ComputeBmi()
    {
        double h = BmiService.LengthToCm(V(BmiHeight), Metric);
        double w = BmiService.MassToKg(V(BmiWeight), Metric);
        double? bmi = BmiService.Bmi(h, w);
        if (bmi is not double b)
        {
            BmiResult.Text = P("Enter a valid height and weight.", "請輸入有效嘅身高同體重。");
            return;
        }
        var cat = BmiService.BmiCategory(b);
        BmiResult.Text = P($"BMI {b:0.0} — {cat.En}", $"BMI {b:0.0} — {cat.Zh}");
    }

    private void ComputeBmr()
    {
        var sex = BmrSex.SelectedIndex == 1 ? BmiService.Sex.Female : BmiService.Sex.Male;
        int age = (int)Math.Round(V(BmrAge));
        double h = BmiService.LengthToCm(V(BmrHeight), Metric);
        double w = BmiService.MassToKg(V(BmrWeight), Metric);
        double? bmr = BmiService.Bmr(sex, age, h, w);
        if (bmr is not double b)
        {
            BmrResult.Text = P("Enter a valid age, height and weight.", "請輸入有效嘅年齡、身高同體重。");
            return;
        }

        int ai = ActivityBox.SelectedIndex;
        if (ai < 0 || ai >= BmiService.ActivityLevels.Length) ai = 0;
        double factor = BmiService.ActivityLevels[ai].Factor;
        double? tdee = BmiService.Tdee(b, factor);

        string tdeeText = tdee is double t
            ? P($" · about {t:0} kcal/day to maintain", $" · 大約每日 {t:0} 卡路里維持體重")
            : "";
        BmrResult.Text = P($"BMR {b:0} kcal/day{tdeeText}", $"基礎代謝 {b:0} 卡路里/日{tdeeText}");
    }

    private void ComputeBodyFat()
    {
        var sex = BfSex.SelectedIndex == 1 ? BmiService.Sex.Female : BmiService.Sex.Male;
        bool female = sex == BmiService.Sex.Female;
        BfHipsPanel.Visibility = female ? Visibility.Visible : Visibility.Collapsed;

        double h = BmiService.LengthToCm(V(BfHeight), Metric);
        double neck = BmiService.LengthToCm(V(BfNeck), Metric);
        double waist = BmiService.LengthToCm(V(BfWaist), Metric);
        double hips = BmiService.LengthToCm(V(BfHips), Metric);

        double? bf = BmiService.BodyFatNavy(sex, h, neck, waist, hips);
        if (bf is not double v)
        {
            BfResult.Text = female
                ? P("Enter valid height, neck, waist and hips (waist + hips must exceed neck).",
                    "請輸入有效嘅身高、頸圍、腰圍同臀圍（腰＋臀要大過頸）。")
                : P("Enter valid height, neck and waist (waist must exceed neck).",
                    "請輸入有效嘅身高、頸圍同腰圍（腰要大過頸）。");
            return;
        }
        BfResult.Text = P($"Body fat ≈ {v:0.0}%", $"體脂率約 {v:0.0}%");
    }
}
