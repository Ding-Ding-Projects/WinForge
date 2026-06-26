using System;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Nuclear-powered cake factory and farm simulator. The farm, mill, bakery, packaging line,
/// sanitation state, and HACCP-style status all run from the live reactor status bus.
/// </summary>
public sealed partial class CakeFactoryModule : Page
{
    private readonly CakeFactoryService _sim = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private DateTime _lastTick = DateTime.UtcNow;
    private bool _ready;

    public CakeFactoryModule()
    {
        InitializeComponent();
        _timer.Tick += OnTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Loc.I.LanguageChanged += (_, _) => RenderText();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ReactorStatusApiService.I.Start();
        PopulateRecipes();
        RenderText();
        _ready = true;
        _lastTick = DateTime.UtcNow;
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
    }

    private void PopulateRecipes()
    {
        if (RecipeBox.Items.Count > 0) return;
        for (int i = 0; i < CakeFactoryService.Recipes.Count; i++)
        {
            var r = CakeFactoryService.Recipes[i];
            RecipeBox.Items.Add(new ComboBoxItem { Content = $"{r.Name} · {r.NameZh}", Tag = i });
        }
        RecipeBox.SelectedIndex = _sim.RecipeIndex;
    }

    private void RenderText()
    {
        HeaderTitle.Text = "Nuclear Cake Factory & Farm · 核能蛋糕工廠與農場";
        HeaderBlurb.Text = P(
            "Live reactor bus, working ingredient farm, milling, mixing, tunnel baking, cooling, decorating, packaging, QA and sanitation.",
            "即時反應堆供電、原料農場、磨粉、攪拌、隧道焗爐、冷卻、裝飾、包裝、品檢同清潔。");
        OpenReactorText.Text = P("Open reactor", "開反應堆");

        FarmHeader.Text = P("Farm production", "農場生產");
        FactoryHeader.Text = P("Factory line", "工廠生產線");
        FoodSafetyHeader.Text = P("Quality + food safety", "品質與食物安全");
        ControlsHeader.Text = P("Line controls", "生產線控制");
        PowerHeader.Text = P("Reactor power tie", "反應堆供電");
        InventoryHeader.Text = P("Ingredient inventory", "原料庫存");

        FarmSliderLabel.Text = $"{P("Farm intensity", "農場強度")}: {FarmSlider.Value:0}%";
        LineSliderLabel.Text = $"{P("Line speed", "生產速度")}: {LineSlider.Value:0}%";
        AutoHarvestSwitch.Header = P("Auto harvest mature fields", "自動收成成熟農地");
        AutoBatchSwitch.Header = P("Auto start batches when ready", "材料齊備時自動開批");
        StartBatchBtn.Content = P("Start batch", "開批");
        CleanBtn.Content = P("CIP clean", "CIP 清潔");
        HarvestBtn.Content = P("Harvest now", "即時收成");

        FlourLbl.Text = P("Cake flour", "蛋糕粉");
        SugarLbl.Text = P("Sugar", "糖");
        EggLbl.Text = P("Eggs", "雞蛋");
        MilkLbl.Text = P("Milk", "牛奶");
        ButterLbl.Text = P("Butter", "牛油");
        VanillaLbl.Text = P("Vanilla", "雲呢拿");
        CocoaLbl.Text = P("Cocoa", "可可");
        BakingLbl.Text = P("Leavening + salt", "膨脹劑與鹽");
        PackedLbl.Text = P("Packed cakes", "已包裝蛋糕");
        WasteLbl.Text = P("Waste/rejects", "廢料/退件");
    }

    private void OnTick(object? sender, object e)
    {
        var now = DateTime.UtcNow;
        double dt = Math.Clamp((now - _lastTick).TotalSeconds, 0.016, 0.25);
        _lastTick = now;

        _sim.Tick(dt, ReactorStatusApiService.I.LastSnapshot);
        RenderSnapshot(_sim.Snapshot);
        DrawScene(_sim.Snapshot);
    }

    private void RenderSnapshot(CakeFactorySnapshot s)
    {
        PowerInfo.Severity = !s.ReactorOnline
            ? InfoBarSeverity.Warning
            : s.PowerAvailability < 0.98
                ? InfoBarSeverity.Warning
                : InfoBarSeverity.Success;
        PowerInfo.Title = s.PowerStatus;
        PowerInfo.Message = P(
            $"Reactor {s.ReactorMode} · {s.ReactorElectricMW:0.0} MWe available · plant demand {s.PowerDemandMW:0.0} MW",
            $"反應堆 {s.ReactorMode} · 可用 {s.ReactorElectricMW:0.0} MWe · 廠房需求 {s.PowerDemandMW:0.0} MW");

        WheatBar.Value = s.WheatGrowth;
        BeetBar.Value = s.BeetGrowth;
        VanillaBar.Value = s.VanillaGrowth;
        WheatText.Text = P($"Wheat {s.WheatGrowth:0}% · raw wheat {s.WheatKg:0} kg", $"小麥 {s.WheatGrowth:0}% · 原麥 {s.WheatKg:0} kg");
        BeetText.Text = P($"Sugar crop {s.BeetGrowth:0}% · beet/cane {s.SugarCropKg:0} kg", $"糖料作物 {s.BeetGrowth:0}% · 甜菜/蔗 {s.SugarCropKg:0} kg");
        VanillaText.Text = P($"Vanilla greenhouse {s.VanillaGrowth:0}% · pasture {s.PastureHealth:0}%", $"雲呢拿溫室 {s.VanillaGrowth:0}% · 牧草 {s.PastureHealth:0}%");

        StageText.Text = $"{s.StageName} · {s.Recipe.Name}";
        StageBar.Value = s.StageProgress * 100;
        OvenText.Text = P($"Oven {s.OvenTemperatureC:0} degC · product core {s.ProductInternalC:0} degC", $"焗爐 {s.OvenTemperatureC:0} degC · 蛋糕中心 {s.ProductInternalC:0} degC");
        GravityText.Text = P($"Batter specific gravity {s.MixerSpecificGravity:0.00} · target {s.Recipe.TargetSpecificGravity:0.00}", $"麵糊比重 {s.MixerSpecificGravity:0.00} · 目標 {s.Recipe.TargetSpecificGravity:0.00}");

        HaccpText.Text = s.HaccpStatus;
        QualityBar.Value = s.QualityScore;
        QualityText.Text = P($"Quality {s.QualityScore:0}% · baked {s.CakesBaked}", $"品質 {s.QualityScore:0}% · 已焗 {s.CakesBaked}");
        SanitationBar.Value = s.SanitationScore;
        SanitationText.Text = s.CipActive
            ? P($"CIP active {s.CipProgress * 100:0}% · sanitation {s.SanitationScore:0}%", $"CIP 清潔 {s.CipProgress * 100:0}% · 衛生 {s.SanitationScore:0}%")
            : P($"Sanitation {s.SanitationScore:0}%", $"衛生 {s.SanitationScore:0}%");

        PowerBar.Value = s.PowerAvailability * 100;
        PowerText.Text = $"Reactor: {s.ReactorElectricMW,7:0.0} MWe  Bus: {s.PowerAvailability * 100,5:0}%";
        DemandText.Text = $"Demand:  {s.PowerDemandMW,7:0.0} MW   Stage: {s.StageName}";

        FlourVal.Text = $"{s.FlourKg:0.0} kg";
        SugarVal.Text = $"{s.SugarKg:0.0} kg";
        EggVal.Text = $"{s.Eggs:0}";
        MilkVal.Text = $"{s.MilkL:0.0} L";
        ButterVal.Text = $"{s.ButterKg:0.0} kg";
        VanillaVal.Text = $"{s.VanillaL:0.00} L";
        CocoaVal.Text = $"{s.CocoaKg:0.0} kg";
        BakingVal.Text = $"{s.BakingPowderKg + s.SaltKg:0.0} kg";
        PackedVal.Text = $"{s.CakesPacked} ({P("rejects", "退件")} {s.CakesRejected})";
        WasteVal.Text = $"{s.WasteKg:0.0} kg";
        MissingText.Text = s.MissingIngredients.Length == 0 ? "" : P("Missing: ", "缺少：") + s.MissingIngredients;

        StartBatchBtn.IsEnabled = s.CanStartBatch;
        CleanBtn.IsEnabled = !s.CipActive;
    }

    private void RecipeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        if ((RecipeBox.SelectedItem as ComboBoxItem)?.Tag is int index)
            _sim.SelectRecipe(index);
    }

    private void FarmSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _sim.FarmIntensity = Math.Clamp(e.NewValue / 100.0, 0, 1);
        if (FarmSliderLabel is not null)
            FarmSliderLabel.Text = $"{P("Farm intensity", "農場強度")}: {e.NewValue:0}%";
    }

    private void LineSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _sim.LineSpeed = Math.Clamp(e.NewValue / 100.0, 0, 1.2);
        if (LineSliderLabel is not null)
            LineSliderLabel.Text = $"{P("Line speed", "生產速度")}: {e.NewValue:0}%";
    }

    private void AutoHarvestSwitch_Toggled(object sender, RoutedEventArgs e) => _sim.AutoHarvest = AutoHarvestSwitch.IsOn;
    private void AutoBatchSwitch_Toggled(object sender, RoutedEventArgs e) => _sim.AutoBatch = AutoBatchSwitch.IsOn;

    private void StartBatch_Click(object sender, RoutedEventArgs e)
    {
        bool ok = _sim.TryStartBatch(out var message);
        Notify(ok ? InfoBarSeverity.Success : InfoBarSeverity.Warning, ok ? P("Batch started", "已開批") : P("Cannot start", "未能開批"), message);
    }

    private void Clean_Click(object sender, RoutedEventArgs e)
    {
        _sim.StartClean();
        Notify(InfoBarSeverity.Informational, P("CIP started", "CIP 已開始"), P("Sanitation loop is washing mixer, depositor, oven belt, icing head and packer.", "清潔迴路正沖洗攪拌機、落模機、爐帶、唧花頭同包裝機。"));
    }

    private void Harvest_Click(object sender, RoutedEventArgs e)
    {
        Notify(InfoBarSeverity.Success, P("Harvest complete", "收成完成"), _sim.HarvestNow());
    }

    private void OpenReactor_Click(object sender, RoutedEventArgs e) => Navigator.GoToModule?.Invoke("module.reactor");

    private void Notify(InfoBarSeverity severity, string title, string message)
    {
        ActionInfo.Severity = severity;
        ActionInfo.Title = title;
        ActionInfo.Message = message;
        ActionInfo.IsOpen = true;
    }

    private void DrawScene(CakeFactorySnapshot s)
    {
        var c = SceneCanvas;
        c.Children.Clear();

        AddRect(c, 0, 0, 1180, 520, C(255, 18, 24, 29));
        AddRect(c, 16, 16, 1148, 488, C(255, 28, 34, 39), 18, 1, C(255, 62, 70, 75));
        DrawPowerBus(c, s);
        DrawFarm(c, s);
        DrawFactory(c, s);
        DrawTelemetry(c, s);
    }

    private void DrawPowerBus(Canvas c, CakeFactorySnapshot s)
    {
        var bus = s.ReactorOnline ? C(255, 70, 174, 255) : C(255, 118, 95, 72);
        AddRect(c, 38, 34, 150, 64, C(255, 19, 42, 56), 10, 1, bus);
        AddText(c, 54, 46, "REACTOR BUS", 14, bus, FontWeights.Bold);
        AddText(c, 54, 68, $"{s.ReactorElectricMW:0.0} MWe", 18, C(255, 245, 250, 255), FontWeights.SemiBold);
        AddLine(c, 188, 66, 1098, 66, bus, 6, 0.72 + s.PowerAvailability * 0.28);
        AddLine(c, 360, 66, 360, 126, bus, 4, 0.75);
        AddLine(c, 782, 66, 782, 126, bus, 4, 0.75);

        for (int i = 0; i < 8; i++)
        {
            double x = 205 + ((s.ConveyorPhase * 4 + i * 118) % 875);
            AddEllipse(c, x, 61, 10, 10, bus, 0.35 + s.PowerAvailability * 0.55);
        }

        AddRect(c, 964, 34, 136, 64, C(255, 45, 38, 27), 10, 1, C(255, 222, 169, 82));
        AddText(c, 980, 46, "PLANT LOAD", 14, C(255, 246, 213, 155), FontWeights.Bold);
        AddText(c, 980, 68, $"{s.PowerDemandMW:0.0} MW", 18, C(255, 255, 245, 223), FontWeights.SemiBold);
    }

    private void DrawFarm(Canvas c, CakeFactorySnapshot s)
    {
        AddText(c, 44, 118, "INGREDIENT FARM", 20, C(255, 235, 242, 230), FontWeights.Bold);
        AddRect(c, 38, 148, 450, 314, C(255, 34, 50, 40), 14, 1, C(255, 73, 110, 76));

        DrawField(c, 58, 172, 185, 92, C(255, 118, 87, 47), C(255, 109, 186, 77), s.WheatGrowth, "WHEAT");
        DrawField(c, 272, 172, 185, 92, C(255, 91, 57, 53), C(255, 158, 73, 80), s.BeetGrowth, "SUGAR CROP");
        DrawPasture(c, 58, 292, s);
        DrawGreenhouse(c, 272, 292, s);

        double tx = 62 + s.TractorPhase * 360;
        AddRect(c, tx, 252, 46, 20, C(255, 210, 55, 44), 4);
        AddRect(c, tx + 10, 237, 22, 18, C(255, 232, 93, 56), 3);
        AddEllipse(c, tx + 2, 265, 15, 15, C(255, 20, 24, 26));
        AddEllipse(c, tx + 30, 265, 17, 17, C(255, 20, 24, 26));
        AddLine(c, tx + 46, 262, tx + 86, 248, C(150, 180, 218, 255), 2, s.PowerAvailability);

        DrawTruck(c, 494, 328, s.ConveyorPhase, C(255, 220, 226, 231));
        AddLine(c, 520, 342, 635, 342, C(255, 108, 174, 120), 3, 0.8);
    }

    private void DrawField(Canvas c, double x, double y, double w, double h, Color soil, Color crop, double pct, string label)
    {
        AddRect(c, x, y, w, h, soil, 8);
        for (int row = 0; row < 7; row++)
        {
            double yy = y + 14 + row * 10;
            AddLine(c, x + 12, yy, x + w - 12, yy, C(150, 45, 38, 28), 3, 0.8);
            double grow = (w - 26) * Math.Clamp(pct / 100.0, 0, 1);
            AddLine(c, x + 13, yy - 2, x + 13 + grow, yy - 2, crop, 4, 0.4 + pct / 180.0);
        }
        AddText(c, x + 12, y + h - 24, $"{label} {pct:0}%", 12, C(255, 245, 245, 230), FontWeights.SemiBold);
    }

    private void DrawPasture(Canvas c, double x, double y, CakeFactorySnapshot s)
    {
        AddRect(c, x, y, 185, 126, C(255, 45, 88, 52), 8);
        for (int i = 0; i < 18; i++)
        {
            double gx = x + 10 + (i * 29) % 165;
            double gy = y + 15 + (i * 17) % 95;
            AddLine(c, gx, gy + 8, gx + 4, gy, C(180, 130, 215, 108), 2, s.PastureHealth / 100.0);
        }
        DrawCow(c, x + 28, y + 45, s.PowerAvailability);
        DrawCow(c, x + 96, y + 62, s.PowerAvailability);
        DrawHen(c, x + 136, y + 35);
        DrawHen(c, x + 152, y + 55);
        AddText(c, x + 12, y + 98, $"DAIRY + EGGS {s.PastureHealth:0}%", 12, C(255, 239, 248, 235), FontWeights.SemiBold);
    }

    private void DrawGreenhouse(Canvas c, double x, double y, CakeFactorySnapshot s)
    {
        AddRect(c, x, y + 22, 185, 104, C(255, 29, 67, 72), 8, 1, C(255, 122, 210, 218));
        AddLine(c, x + 14, y + 22, x + 92, y - 8, C(255, 122, 210, 218), 3);
        AddLine(c, x + 171, y + 22, x + 92, y - 8, C(255, 122, 210, 218), 3);
        for (int i = 0; i < 6; i++)
        {
            double vx = x + 25 + i * 24;
            AddLine(c, vx, y + 92, vx + Math.Sin(i) * 8, y + 40, C(255, 82, 170, 96), 3);
            AddEllipse(c, vx + 6, y + 48 + i % 2 * 8, 10, 6, C(255, 230, 224, 178), 0.9);
        }
        AddText(c, x + 12, y + 98, $"VANILLA {s.VanillaGrowth:0}%", 12, C(255, 222, 250, 247), FontWeights.SemiBold);
    }

    private void DrawFactory(Canvas c, CakeFactorySnapshot s)
    {
        AddText(c, 620, 118, "CAKE FACTORY", 20, C(255, 236, 240, 242), FontWeights.Bold);
        AddRect(c, 610, 148, 512, 314, C(255, 42, 47, 53), 14, 1, C(255, 88, 96, 103));
        AddRect(c, 622, 166, 486, 278, C(255, 30, 35, 40), 10);

        DrawHopper(c, 642, 182, "FLOUR", C(255, 220, 226, 220));
        DrawHopper(c, 702, 182, "SUGAR", C(255, 238, 232, 200));
        DrawHopper(c, 762, 182, "EGG/MILK", C(255, 248, 230, 160));
        DrawMill(c, 644, 278, s);
        DrawMixer(c, 760, 282, s);
        DrawDepositor(c, 878, 278, s);
        DrawOven(c, 620, 370, s);
        DrawCooler(c, 828, 372, s);
        DrawIcer(c, 948, 292, s);
        DrawPacker(c, 1018, 372, s);

        DrawConveyor(c, 646, 338, 398, s);
        DrawConveyor(c, 646, 430, 420, s);
        DrawCakesOnLine(c, s);
    }

    private void DrawHopper(Canvas c, double x, double y, string label, Color fill)
    {
        AddPolygon(c, new[] { (x, y), (x + 44, y), (x + 34, y + 54), (x + 10, y + 54) }, C(255, 78, 86, 94), 1, C(255, 132, 142, 150));
        AddRect(c, x + 8, y + 8, 28, 20, fill, 2, 0.85);
        AddText(c, x + 4, y + 60, label, 10, C(255, 218, 224, 229), FontWeights.SemiBold);
    }

    private void DrawMill(Canvas c, double x, double y, CakeFactorySnapshot s)
    {
        bool active = s.PowerAvailability > 0.2;
        AddRect(c, x, y, 72, 50, C(255, 71, 76, 84), 8, 1, C(255, 130, 138, 146));
        AddEllipse(c, x + 12, y + 10, 24, 24, active ? C(255, 180, 193, 207) : C(255, 92, 98, 104));
        AddEllipse(c, x + 36, y + 10, 24, 24, active ? C(255, 180, 193, 207) : C(255, 92, 98, 104));
        AddText(c, x + 10, y + 58, "MILL", 11, C(255, 214, 220, 226), FontWeights.SemiBold);
    }

    private void DrawMixer(Canvas c, double x, double y, CakeFactorySnapshot s)
    {
        bool active = s.Stage == CakeBatchStage.Mixing;
        AddRect(c, x, y + 8, 88, 58, C(255, 64, 72, 82), 12, 1, C(255, 135, 147, 156));
        AddEllipse(c, x + 12, y + 20, 64, 34, active ? C(255, 230, 204, 136) : C(255, 114, 94, 65), 0.92);
        double cx = x + 44;
        double cy = y + 37;
        double a = s.MixerAngle * Math.PI / 180.0;
        AddLine(c, cx, cy, cx + Math.Cos(a) * 28, cy + Math.Sin(a) * 16, C(255, 235, 241, 245), 4);
        AddLine(c, cx, cy, cx + Math.Cos(a + Math.PI) * 28, cy + Math.Sin(a + Math.PI) * 16, C(255, 235, 241, 245), 4);
        AddText(c, x + 16, y + 74, "MIXER", 11, C(255, 214, 220, 226), FontWeights.SemiBold);
    }

    private void DrawDepositor(Canvas c, double x, double y, CakeFactorySnapshot s)
    {
        AddRect(c, x, y, 62, 62, C(255, 65, 83, 93), 8, 1, C(255, 120, 164, 180));
        AddRect(c, x + 18, y + 60, 26, 30, C(255, 115, 128, 136), 4);
        if (s.Stage == CakeBatchStage.Depositing)
            AddEllipse(c, x + 20, y + 88 + Math.Sin(s.ConveyorPhase / 8) * 4, 22, 12, C(255, 222, 182, 102), 0.85);
        AddText(c, x + 2, y + 96, "DEPOSIT", 11, C(255, 214, 220, 226), FontWeights.SemiBold);
    }

    private void DrawOven(Canvas c, double x, double y, CakeFactorySnapshot s)
    {
        AddRect(c, x, y, 188, 62, C(255, 65, 55, 46), 8, 1, C(255, 154, 116, 75));
        AddRect(c, x + 16, y + 14, 156, 34, C(255, 82, 46, 28), 5);
        AddRect(c, x + 16, y + 14, 156, 34, C((byte)(60 + s.OvenGlow * 140), 255, 122, 35), 5);
        for (int i = 0; i < 5; i++)
        {
            double fx = x + 28 + i * 28;
            double flame = Math.Sin((s.ConveyorPhase + i * 18) / 12.0) * 8;
            AddPolygon(c, new[] { (fx, y + 46), (fx + 8, y + 26 + flame), (fx + 16, y + 46) }, C((byte)(90 + s.OvenGlow * 150), 255, 184, 70));
        }
        AddText(c, x + 52, y + 74, $"TUNNEL OVEN {s.OvenTemperatureC:0}C", 11, C(255, 236, 225, 208), FontWeights.SemiBold);
    }

    private void DrawCooler(Canvas c, double x, double y, CakeFactorySnapshot s)
    {
        AddRect(c, x, y, 92, 62, C(255, 43, 68, 75), 8, 1, C(255, 94, 180, 195));
        for (int i = 0; i < 4; i++)
        {
            double r = 16 + i * 6;
            AddEllipse(c, x + 46 - r / 2, y + 31 - r / 2, r, r, C(0, 0, 0, 0), 1, C(255, 112, 206, 218));
        }
        AddText(c, x + 16, y + 74, "COOLER", 11, C(255, 214, 235, 238), FontWeights.SemiBold);
    }

    private void DrawIcer(Canvas c, double x, double y, CakeFactorySnapshot s)
    {
        AddRect(c, x, y, 50, 100, C(255, 62, 70, 88), 8, 1, C(255, 150, 160, 185));
        AddLine(c, x + 25, y + 12, x + 25, y + 88, C(255, 170, 180, 196), 5);
        AddEllipse(c, x + 15, y + 84, 20, 20, s.Stage == CakeBatchStage.Icing ? C(255, 245, 236, 248) : C(255, 130, 132, 142));
        AddText(c, x + 6, y + 108, "ICING", 11, C(255, 226, 224, 238), FontWeights.SemiBold);
    }

    private void DrawPacker(Canvas c, double x, double y, CakeFactorySnapshot s)
    {
        AddRect(c, x, y, 70, 62, C(255, 72, 65, 53), 8, 1, C(255, 192, 167, 108));
        AddRect(c, x + 12, y + 16, 46, 28, C(255, 212, 184, 104), 4, s.Stage == CakeBatchStage.Packaging ? 1 : 0.45);
        AddText(c, x + 12, y + 74, "PACK", 11, C(255, 238, 229, 204), FontWeights.SemiBold);
    }

    private void DrawConveyor(Canvas c, double x, double y, double w, CakeFactorySnapshot s)
    {
        AddRect(c, x, y, w, 18, C(255, 55, 59, 65), 8, 1, C(255, 96, 104, 112));
        for (int i = 0; i < 25; i++)
        {
            double xx = x + ((i * 36 + s.ConveyorPhase) % w);
            AddLine(c, xx, y + 3, xx + 18, y + 15, C(160, 120, 130, 140), 2);
        }
    }

    private void DrawCakesOnLine(Canvas c, CakeFactorySnapshot s)
    {
        int count = s.Stage == CakeBatchStage.Idle ? 3 : 8;
        for (int i = 0; i < count; i++)
        {
            double p = ((s.ConveyorPhase / 80.0) + i / (double)count) % 1.0;
            double x = 660 + p * 376;
            double y = s.Stage is CakeBatchStage.Baking or CakeBatchStage.Cooling or CakeBatchStage.Icing or CakeBatchStage.Packaging ? 427 : 334;
            Color cake = (int)s.Stage >= (int)CakeBatchStage.Baking ? C(255, 170, 112, 58) : C(255, 222, 182, 102);
            AddEllipse(c, x, y - 10, 24, 15, cake, 0.95);
            if (s.Stage is CakeBatchStage.Icing or CakeBatchStage.Packaging)
                AddEllipse(c, x + 3, y - 13, 18, 8, C(255, 246, 236, 246), 0.9);
        }
    }

    private void DrawTelemetry(Canvas c, CakeFactorySnapshot s)
    {
        AddRect(c, 44, 474, 1078, 20, C(255, 18, 22, 25), 8, 1, C(255, 58, 64, 70));
        double powerW = 360 * s.PowerAvailability;
        AddRect(c, 52, 480, powerW, 8, s.PowerAvailability > 0.95 ? C(255, 75, 205, 122) : C(255, 222, 165, 72), 4);
        AddText(c, 430, 476, $"POWER {s.PowerAvailability * 100:0}%   {s.StageName}   HACCP {s.HaccpStatus}   QA {s.QualityScore:0}%", 12, C(255, 224, 230, 235), FontWeights.SemiBold);
    }

    private void DrawCow(Canvas c, double x, double y, double power)
    {
        AddEllipse(c, x, y, 42, 24, C(255, 238, 238, 232));
        AddEllipse(c, x + 28, y - 6, 18, 18, C(255, 238, 238, 232));
        AddEllipse(c, x + 8, y + 4, 8, 6, C(255, 36, 38, 40));
        AddEllipse(c, x + 22, y + 10, 10, 7, C(255, 36, 38, 40));
        AddLine(c, x + 8, y + 22, x + 8, y + 32, C(255, 230, 230, 224), 3);
        AddLine(c, x + 30, y + 22, x + 30, y + 32, C(255, 230, 230, 224), 3);
        if (power > 0.5) AddLine(c, x + 42, y + 2, x + 52, y - 8, C(180, 210, 230, 255), 2);
    }

    private void DrawHen(Canvas c, double x, double y)
    {
        AddEllipse(c, x, y, 16, 12, C(255, 239, 226, 181));
        AddEllipse(c, x + 10, y - 4, 8, 8, C(255, 240, 218, 170));
        AddPolygon(c, new[] { (x + 18, y), (x + 25, y + 3), (x + 18, y + 6) }, C(255, 232, 136, 45));
    }

    private void DrawTruck(Canvas c, double x, double y, double phase, Color fill)
    {
        double bob = Math.Sin(phase / 10.0) * 2;
        AddRect(c, x, y + bob, 70, 26, fill, 4, 1, C(255, 90, 98, 104));
        AddRect(c, x + 48, y + 8 + bob, 34, 18, C(255, 84, 110, 132), 4);
        AddEllipse(c, x + 10, y + 22 + bob, 15, 15, C(255, 20, 24, 26));
        AddEllipse(c, x + 58, y + 22 + bob, 15, 15, C(255, 20, 24, 26));
    }

    private static SolidColorBrush B(Color c, double opacity = 1) => new(c) { Opacity = opacity };
    private static Color C(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);

    private static void AddRect(Canvas canvas, double x, double y, double w, double h, Color fill, double radius = 0, double opacity = 1, Color? stroke = null)
    {
        var r = new Rectangle
        {
            Width = w,
            Height = h,
            RadiusX = radius,
            RadiusY = radius,
            Fill = B(fill, opacity),
            Stroke = stroke is null ? null : B(stroke.Value),
            StrokeThickness = stroke is null ? 0 : 1,
        };
        Canvas.SetLeft(r, x);
        Canvas.SetTop(r, y);
        canvas.Children.Add(r);
    }

    private static void AddEllipse(Canvas canvas, double x, double y, double w, double h, Color fill, double opacity = 1, Color? stroke = null)
    {
        var e = new Ellipse
        {
            Width = w,
            Height = h,
            Fill = fill.A == 0 ? null : B(fill, opacity),
            Stroke = stroke is null ? null : B(stroke.Value),
            StrokeThickness = stroke is null ? 0 : 1,
        };
        Canvas.SetLeft(e, x);
        Canvas.SetTop(e, y);
        canvas.Children.Add(e);
    }

    private static void AddLine(Canvas canvas, double x1, double y1, double x2, double y2, Color color, double thickness = 1, double opacity = 1)
    {
        canvas.Children.Add(new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = B(color, opacity),
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        });
    }

    private static void AddPolygon(Canvas canvas, (double x, double y)[] points, Color fill, double opacity = 1, Color? stroke = null)
    {
        var p = new Polygon
        {
            Fill = B(fill, opacity),
            Stroke = stroke is null ? null : B(stroke.Value),
            StrokeThickness = stroke is null ? 0 : 1,
        };
        foreach (var (x, y) in points)
            p.Points.Add(new Windows.Foundation.Point(x, y));
        canvas.Children.Add(p);
    }

    private static void AddText(Canvas canvas, double x, double y, string text, double size, Color color, Windows.UI.Text.FontWeight? weight = null)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = size,
            Foreground = B(color),
            FontWeight = weight ?? FontWeights.Normal,
        };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        canvas.Children.Add(tb);
    }
}
