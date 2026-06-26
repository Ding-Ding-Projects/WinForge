using System;
using System.Collections.Generic;

namespace WinForge.Services;

public enum CakeBatchStage
{
    Idle,
    Scaling,
    Mixing,
    Depositing,
    Baking,
    Cooling,
    Icing,
    Packaging,
}

public sealed record CakeRecipe(
    string Key,
    string Name,
    string NameZh,
    int BatchSize,
    double FlourKg,
    double SugarKg,
    double EggCount,
    double ButterKg,
    double MilkL,
    double BakingPowderKg,
    double SaltKg,
    double VanillaL,
    double CocoaKg,
    double MixSeconds,
    double BakeSeconds,
    double OvenSetpointC,
    double TargetSpecificGravity);

public sealed class CakeFactorySnapshot
{
    public CakeRecipe Recipe { get; init; } = CakeFactoryService.Recipes[0];
    public bool ReactorOnline { get; init; }
    public string ReactorMode { get; init; } = "Offline";
    public double ReactorElectricMW { get; init; }
    public double PowerDemandMW { get; init; }
    public double PowerAvailability { get; init; }
    public string PowerStatus { get; init; } = "";
    public CakeBatchStage Stage { get; init; }
    public string StageName { get; init; } = "";
    public double StageProgress { get; init; }
    public double MixerSpecificGravity { get; init; }
    public double OvenTemperatureC { get; init; }
    public double ProductInternalC { get; init; }
    public double QualityScore { get; init; }
    public double SanitationScore { get; init; }
    public string HaccpStatus { get; init; } = "";
    public bool CipActive { get; init; }
    public double CipProgress { get; init; }
    public bool CanStartBatch { get; init; }
    public string MissingIngredients { get; init; } = "";
    public double WheatGrowth { get; init; }
    public double BeetGrowth { get; init; }
    public double PastureHealth { get; init; }
    public double VanillaGrowth { get; init; }
    public double WheatKg { get; init; }
    public double SugarCropKg { get; init; }
    public double FlourKg { get; init; }
    public double SugarKg { get; init; }
    public double Eggs { get; init; }
    public double MilkL { get; init; }
    public double ButterKg { get; init; }
    public double BakingPowderKg { get; init; }
    public double SaltKg { get; init; }
    public double VanillaL { get; init; }
    public double CocoaKg { get; init; }
    public double BatterKg { get; init; }
    public int CakesBaked { get; init; }
    public int CakesPacked { get; init; }
    public int CakesRejected { get; init; }
    public double WasteKg { get; init; }
    public double ConveyorPhase { get; init; }
    public double MixerAngle { get; init; }
    public double TractorPhase { get; init; }
    public double OvenGlow { get; init; }
}

/// <summary>
/// Research-backed cake supply-chain simulator: farm production, ingredient conversion, bakery
/// stages, sanitation, HACCP-style safety status, and a hard dependency on the live reactor bus.
/// </summary>
public sealed class CakeFactoryService
{
    public static IReadOnlyList<CakeRecipe> Recipes { get; } = new[]
    {
        new CakeRecipe(
            "white-layer", "White layer cake", "白色忌廉蛋糕", 12,
            FlourKg: 0.244, SugarKg: 0.350, EggCount: 4.0, ButterKg: 0.170, MilkL: 0.240,
            BakingPowderKg: 0.016, SaltKg: 0.006, VanillaL: 0.005, CocoaKg: 0,
            MixSeconds: 18, BakeSeconds: 42, OvenSetpointC: 176, TargetSpecificGravity: 0.82),
        new CakeRecipe(
            "butter-pound", "Butter pound cake", "牛油磅蛋糕", 10,
            FlourKg: 0.250, SugarKg: 0.250, EggCount: 3.4, ButterKg: 0.250, MilkL: 0.070,
            BakingPowderKg: 0.008, SaltKg: 0.004, VanillaL: 0.004, CocoaKg: 0,
            MixSeconds: 22, BakeSeconds: 52, OvenSetpointC: 168, TargetSpecificGravity: 0.92),
        new CakeRecipe(
            "chocolate", "Chocolate layer cake", "朱古力蛋糕", 12,
            FlourKg: 0.225, SugarKg: 0.310, EggCount: 3.0, ButterKg: 0.120, MilkL: 0.260,
            BakingPowderKg: 0.013, SaltKg: 0.005, VanillaL: 0.004, CocoaKg: 0.045,
            MixSeconds: 20, BakeSeconds: 40, OvenSetpointC: 176, TargetSpecificGravity: 0.86),
    };

    private readonly Random _rng = new(1204);
    private int _recipeIndex;
    private CakeBatchStage _stage = CakeBatchStage.Idle;
    private double _stageSeconds;
    private double _batchInternalC = 22;
    private double _batchQuality = 96;
    private double _mixerSpecificGravity = 1.0;
    private double _ovenTemperatureC = 24;
    private double _sanitationScore = 94;
    private double _cipSeconds;
    private double _lastPowerAvailability;

    private double _wheatGrowth = 64;
    private double _beetGrowth = 58;
    private double _pastureHealth = 76;
    private double _vanillaGrowth = 34;

    private double _wheatKg = 260;
    private double _sugarCropKg = 380;
    private double _flourKg = 120;
    private double _sugarKg = 92;
    private double _eggs = 240;
    private double _milkL = 140;
    private double _butterKg = 28;
    private double _bakingPowderKg = 14;
    private double _saltKg = 18;
    private double _vanillaL = 2.8;
    private double _cocoaKg = 20;
    private double _batterKg;
    private double _wasteKg;

    private int _cakesBaked;
    private int _cakesPacked;
    private int _cakesRejected;

    private double _conveyorPhase;
    private double _mixerAngle;
    private double _tractorPhase;

    public double FarmIntensity { get; set; } = 0.78;
    public double LineSpeed { get; set; } = 0.72;
    public bool AutoHarvest { get; set; } = true;
    public bool AutoBatch { get; set; } = true;
    public bool CipActive => _cipSeconds > 0;
    public int RecipeIndex => _recipeIndex;
    public CakeRecipe CurrentRecipe => Recipes[Math.Clamp(_recipeIndex, 0, Recipes.Count - 1)];
    public CakeFactorySnapshot Snapshot { get; private set; } = new();

    public void SelectRecipe(int index)
    {
        if (index >= 0 && index < Recipes.Count) _recipeIndex = index;
    }

    public string HarvestNow()
    {
        double wheat = 3.8 * _wheatGrowth;
        double beet = 7.2 * _beetGrowth;
        double vanilla = 0.018 * _vanillaGrowth;
        _wheatKg += wheat;
        _sugarCropKg += beet;
        _vanillaL += vanilla;
        _wheatGrowth = 12 + _rng.NextDouble() * 8;
        _beetGrowth = 10 + _rng.NextDouble() * 8;
        _vanillaGrowth = 8 + _rng.NextDouble() * 5;
        _pastureHealth = Math.Min(100, _pastureHealth + 8);
        return $"Harvested {wheat:0} kg wheat, {beet:0} kg sugar crop, {vanilla:0.00} L vanilla extract equivalent.";
    }

    public void StartClean()
    {
        _cipSeconds = Math.Max(_cipSeconds, 24);
        if (_stage == CakeBatchStage.Idle) _stageSeconds = 0;
    }

    public bool TryStartBatch(out string message)
    {
        if (_stage != CakeBatchStage.Idle)
        {
            message = "A batch is already on the line.";
            return false;
        }
        if (CipActive)
        {
            message = "CIP sanitation cycle is active.";
            return false;
        }
        if (_lastPowerAvailability < 0.2)
        {
            message = "Nuclear reactor power is required before the factory can start a batch.";
            return false;
        }
        var missing = MissingIngredients(CurrentRecipe);
        if (missing.Length > 0)
        {
            message = "Missing: " + missing;
            return false;
        }

        ConsumeIngredients(CurrentRecipe);
        _stage = CakeBatchStage.Scaling;
        _stageSeconds = 0;
        _batchInternalC = 22;
        _mixerSpecificGravity = 1.02;
        _batchQuality = Math.Clamp(86 + _sanitationScore * 0.11 + _rng.NextDouble() * 4, 70, 99);
        _batterKg += BatchIngredientMass(CurrentRecipe);
        message = $"Started {CurrentRecipe.Name} batch ({CurrentRecipe.BatchSize} cakes).";
        return true;
    }

    public void Tick(double seconds, ReactorStatusSnapshot reactor)
    {
        seconds = Math.Clamp(seconds, 0.016, 0.25);
        var recipe = CurrentRecipe;
        double farmDemand = 0.7 + FarmIntensity * 3.8;
        double factoryDemand = 2.4 + LineSpeed * 28.0 + (CipActive ? 2.8 : 0);
        double demand = farmDemand + factoryDemand;
        bool reactorOnline = reactor.IsGenerating && reactor.ElectricMW > 1 && !reactor.IsMeltdown;
        double power = reactorOnline ? Math.Clamp(reactor.ElectricMW / Math.Max(1, demand), 0, 1) : 0;
        _lastPowerAvailability = power;

        UpdateAnimation(seconds, power);
        UpdateFarm(seconds, power);
        UpdateIngredientConversion(seconds, power);
        UpdateCleaning(seconds, power);
        UpdateBatch(seconds, power);

        if (AutoBatch && _stage == CakeBatchStage.Idle && !CipActive && power > 0.65)
            _ = TryStartBatch(out _);

        _sanitationScore = Math.Clamp(_sanitationScore - seconds * (0.003 + (_stage == CakeBatchStage.Idle ? 0 : 0.018 * LineSpeed)), 0, 100);

        var missing = MissingIngredients(recipe);
        Snapshot = new CakeFactorySnapshot
        {
            Recipe = recipe,
            ReactorOnline = reactorOnline,
            ReactorMode = reactor.Mode ?? "Offline",
            ReactorElectricMW = reactor.ElectricMW,
            PowerDemandMW = demand,
            PowerAvailability = power,
            PowerStatus = reactorOnline
                ? (power >= 0.98 ? "Reactor bus nominal" : "Reactor output limiting the line")
                : "Waiting for nuclear reactor generation",
            Stage = _stage,
            StageName = StageLabel(_stage),
            StageProgress = StageProgress(recipe),
            MixerSpecificGravity = _mixerSpecificGravity,
            OvenTemperatureC = _ovenTemperatureC,
            ProductInternalC = _batchInternalC,
            QualityScore = LiveQuality(power),
            SanitationScore = _sanitationScore,
            HaccpStatus = HaccpStatus(),
            CipActive = CipActive,
            CipProgress = CipActive ? 1.0 - _cipSeconds / 24.0 : 1.0,
            CanStartBatch = _stage == CakeBatchStage.Idle && !CipActive && missing.Length == 0 && power >= 0.2,
            MissingIngredients = missing,
            WheatGrowth = _wheatGrowth,
            BeetGrowth = _beetGrowth,
            PastureHealth = _pastureHealth,
            VanillaGrowth = _vanillaGrowth,
            WheatKg = _wheatKg,
            SugarCropKg = _sugarCropKg,
            FlourKg = _flourKg,
            SugarKg = _sugarKg,
            Eggs = _eggs,
            MilkL = _milkL,
            ButterKg = _butterKg,
            BakingPowderKg = _bakingPowderKg,
            SaltKg = _saltKg,
            VanillaL = _vanillaL,
            CocoaKg = _cocoaKg,
            BatterKg = _batterKg,
            CakesBaked = _cakesBaked,
            CakesPacked = _cakesPacked,
            CakesRejected = _cakesRejected,
            WasteKg = _wasteKg,
            ConveyorPhase = _conveyorPhase,
            MixerAngle = _mixerAngle,
            TractorPhase = _tractorPhase,
            OvenGlow = Math.Clamp((_ovenTemperatureC - 80) / 110.0, 0, 1) * (0.3 + power * 0.7),
        };
    }

    private void UpdateAnimation(double seconds, double power)
    {
        double motion = 0.15 + power * Math.Max(0.05, LineSpeed);
        _conveyorPhase = (_conveyorPhase + seconds * motion * 90) % 80;
        _mixerAngle = (_mixerAngle + seconds * motion * 260) % 360;
        _tractorPhase = (_tractorPhase + seconds * Math.Max(0.02, FarmIntensity * power) * 0.05) % 1;
    }

    private void UpdateFarm(double seconds, double power)
    {
        double fieldEffect = FarmIntensity * (0.12 + 0.88 * power);
        _wheatGrowth = Math.Min(100, _wheatGrowth + seconds * 0.16 * fieldEffect);
        _beetGrowth = Math.Min(100, _beetGrowth + seconds * 0.13 * fieldEffect);
        _pastureHealth = Math.Clamp(_pastureHealth + seconds * (0.09 * fieldEffect - 0.015), 10, 100);
        _vanillaGrowth = Math.Min(100, _vanillaGrowth + seconds * 0.045 * fieldEffect);

        if (AutoHarvest && _wheatGrowth >= 100) _wheatKg += HarvestCrop(ref _wheatGrowth, 390);
        if (AutoHarvest && _beetGrowth >= 100) _sugarCropKg += HarvestCrop(ref _beetGrowth, 760);
        if (AutoHarvest && _vanillaGrowth >= 100)
        {
            _vanillaL += 2.2 + _rng.NextDouble() * 0.6;
            _vanillaGrowth = 9 + _rng.NextDouble() * 6;
        }

        double livestockPower = FarmIntensity * power;
        _milkL += seconds * (0.9 + _pastureHealth / 120.0) * livestockPower;
        _eggs += seconds * (0.75 + FarmIntensity * 0.55) * power;
        _cocoaKg += seconds * 0.03 * fieldEffect;
    }

    private double HarvestCrop(ref double growth, double baseYield)
    {
        double yield = baseYield * (0.92 + _rng.NextDouble() * 0.16);
        growth = 10 + _rng.NextDouble() * 7;
        return yield;
    }

    private void UpdateIngredientConversion(double seconds, double power)
    {
        double rate = seconds * LineSpeed * power;
        if (rate <= 0) return;

        double wheat = Math.Min(_wheatKg, rate * 22.0);
        _wheatKg -= wheat;
        _flourKg += wheat * 0.76;
        _wasteKg += wheat * 0.04;

        double beet = Math.Min(_sugarCropKg, rate * 34.0);
        _sugarCropKg -= beet;
        _sugarKg += beet * 0.155;
        _wasteKg += beet * 0.025;

        double churnMilk = Math.Min(Math.Max(0, _milkL - 40), rate * 7.5);
        _milkL -= churnMilk;
        _butterKg += churnMilk / 22.0;
    }

    private void UpdateCleaning(double seconds, double power)
    {
        if (!CipActive) return;
        double cleanRate = seconds * Math.Max(0.1, power);
        _cipSeconds = Math.Max(0, _cipSeconds - cleanRate);
        _sanitationScore = Math.Min(100, _sanitationScore + cleanRate * 4.2);
    }

    private void UpdateBatch(double seconds, double power)
    {
        double idleTarget = power > 0 ? 82 : 24;
        if (_stage != CakeBatchStage.Baking)
            _ovenTemperatureC += (idleTarget - _ovenTemperatureC) * Math.Min(1, seconds / 16.0);

        if (_stage == CakeBatchStage.Idle) return;

        double stageRate = power * Math.Clamp(LineSpeed, 0.15, 1.25);
        if (_stage == CakeBatchStage.Baking)
        {
            _ovenTemperatureC += (CurrentRecipe.OvenSetpointC - _ovenTemperatureC) * Math.Min(1, seconds * power / 10.0);
            double heatTransfer = Math.Max(0, (_ovenTemperatureC - _batchInternalC) / Math.Max(40, CurrentRecipe.OvenSetpointC));
            _batchInternalC += seconds * heatTransfer * 7.0 * power;
            if (_ovenTemperatureC < CurrentRecipe.OvenSetpointC - 25) stageRate *= 0.35;
        }
        else if (_stage == CakeBatchStage.Mixing)
        {
            _mixerSpecificGravity += (CurrentRecipe.TargetSpecificGravity - _mixerSpecificGravity) * Math.Min(1, seconds * stageRate / 6.0);
        }
        else if (_stage == CakeBatchStage.Cooling)
        {
            _batchInternalC += (32 - _batchInternalC) * Math.Min(1, seconds * stageRate / 16.0);
        }

        if (stageRate <= 0.01)
        {
            _batchQuality = Math.Max(55, _batchQuality - seconds * 0.08);
            return;
        }

        _stageSeconds += seconds * stageRate;
        if (_stageSeconds < StageDuration(CurrentRecipe, _stage)) return;

        _stageSeconds = 0;
        _stage = _stage switch
        {
            CakeBatchStage.Scaling => CakeBatchStage.Mixing,
            CakeBatchStage.Mixing => CakeBatchStage.Depositing,
            CakeBatchStage.Depositing => CakeBatchStage.Baking,
            CakeBatchStage.Baking => CakeBatchStage.Cooling,
            CakeBatchStage.Cooling => CakeBatchStage.Icing,
            CakeBatchStage.Icing => CakeBatchStage.Packaging,
            CakeBatchStage.Packaging => CompleteBatch(),
            _ => CakeBatchStage.Idle,
        };
    }

    private CakeBatchStage CompleteBatch()
    {
        var recipe = CurrentRecipe;
        double quality = LiveQuality(_lastPowerAvailability);
        double pass = Math.Clamp((quality - 62) / 34.0, 0.15, 0.995);
        int packed = (int)Math.Round(recipe.BatchSize * pass);
        int rejected = recipe.BatchSize - packed;
        _cakesBaked += recipe.BatchSize;
        _cakesPacked += packed;
        _cakesRejected += rejected;
        _wasteKg += rejected * 0.42;
        _batterKg = Math.Max(0, _batterKg - BatchIngredientMass(recipe));
        _sanitationScore = Math.Max(0, _sanitationScore - 2.4);
        _batchInternalC = 28;
        return CakeBatchStage.Idle;
    }

    private double StageDuration(CakeRecipe recipe, CakeBatchStage stage) => stage switch
    {
        CakeBatchStage.Scaling => 8,
        CakeBatchStage.Mixing => recipe.MixSeconds,
        CakeBatchStage.Depositing => 7,
        CakeBatchStage.Baking => recipe.BakeSeconds,
        CakeBatchStage.Cooling => 18,
        CakeBatchStage.Icing => 15,
        CakeBatchStage.Packaging => 9,
        _ => 1,
    };

    private double StageProgress(CakeRecipe recipe)
    {
        if (_stage == CakeBatchStage.Idle) return 0;
        return Math.Clamp(_stageSeconds / StageDuration(recipe, _stage), 0, 1);
    }

    private double LiveQuality(double power)
    {
        double ovenPenalty = _stage == CakeBatchStage.Baking ? Math.Max(0, CurrentRecipe.OvenSetpointC - _ovenTemperatureC) * 0.11 : 0;
        double sanitationPenalty = Math.Max(0, 72 - _sanitationScore) * 0.28;
        double gravityPenalty = (int)_stage >= (int)CakeBatchStage.Mixing
            ? Math.Abs(_mixerSpecificGravity - CurrentRecipe.TargetSpecificGravity) * 45
            : 0;
        return Math.Clamp(_batchQuality + power * 3.5 - ovenPenalty - sanitationPenalty - gravityPenalty, 0, 100);
    }

    private string HaccpStatus()
    {
        if (_stage == CakeBatchStage.Idle) return _sanitationScore >= 80 ? "Ready - clean line" : "Clean recommended";
        if (_stage is CakeBatchStage.Scaling or CakeBatchStage.Mixing or CakeBatchStage.Depositing) return "Raw flour/egg controls";
        if (_stage == CakeBatchStage.Baking && _batchInternalC >= 71) return "Kill step reached";
        if (_stage == CakeBatchStage.Baking) return "Kill step pending";
        return _batchInternalC >= 35 ? "Cooling below hot zone" : "Post-bake protected";
    }

    private string StageLabel(CakeBatchStage stage) => stage switch
    {
        CakeBatchStage.Idle => "Idle",
        CakeBatchStage.Scaling => "Weighing + scaling",
        CakeBatchStage.Mixing => "Planetary mixing",
        CakeBatchStage.Depositing => "Depositing batter",
        CakeBatchStage.Baking => "Tunnel oven bake",
        CakeBatchStage.Cooling => "Spiral cooling",
        CakeBatchStage.Icing => "Icing + decorating",
        CakeBatchStage.Packaging => "Packaging + coding",
        _ => stage.ToString(),
    };

    private void ConsumeIngredients(CakeRecipe r)
    {
        double n = r.BatchSize;
        _flourKg -= r.FlourKg * n;
        _sugarKg -= r.SugarKg * n;
        _eggs -= r.EggCount * n;
        _butterKg -= r.ButterKg * n;
        _milkL -= r.MilkL * n;
        _bakingPowderKg -= r.BakingPowderKg * n;
        _saltKg -= r.SaltKg * n;
        _vanillaL -= r.VanillaL * n;
        _cocoaKg -= r.CocoaKg * n;
    }

    private string MissingIngredients(CakeRecipe r)
    {
        var missing = new List<string>();
        double n = r.BatchSize;
        if (_flourKg < r.FlourKg * n) missing.Add("flour");
        if (_sugarKg < r.SugarKg * n) missing.Add("sugar");
        if (_eggs < r.EggCount * n) missing.Add("eggs");
        if (_butterKg < r.ButterKg * n) missing.Add("butter");
        if (_milkL < r.MilkL * n) missing.Add("milk");
        if (_bakingPowderKg < r.BakingPowderKg * n) missing.Add("baking powder");
        if (_saltKg < r.SaltKg * n) missing.Add("salt");
        if (_vanillaL < r.VanillaL * n) missing.Add("vanilla");
        if (_cocoaKg < r.CocoaKg * n) missing.Add("cocoa");
        return string.Join(", ", missing);
    }

    private double BatchIngredientMass(CakeRecipe r)
    {
        double n = r.BatchSize;
        return n * (r.FlourKg + r.SugarKg + r.ButterKg + r.MilkL + r.BakingPowderKg + r.SaltKg + r.VanillaL + r.CocoaKg + r.EggCount * 0.052);
    }
}
