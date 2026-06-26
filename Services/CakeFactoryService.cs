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
    public bool StageReadyForOperator { get; init; }
    public bool CanAdvanceStage { get; init; }
    public string OperatorPrompt { get; init; } = "";
    public string MissingIngredients { get; init; } = "";
    public bool CanHarvest { get; init; }
    public bool CanCollectDairy { get; init; }
    public bool CanMillWheat { get; init; }
    public bool CanRefineSugar { get; init; }
    public bool CanChurnButter { get; init; }
    public bool CanReceiveSupplies { get; init; }
    public bool CanProcessCocoa { get; init; }
    public bool CanRunSaltWorks { get; init; }
    public bool CanRunLeaveningPlant { get; init; }
    public double WheatGrowth { get; init; }
    public double BeetGrowth { get; init; }
    public double PastureHealth { get; init; }
    public double VanillaGrowth { get; init; }
    public double DairyReadyL { get; init; }
    public double EggsReady { get; init; }
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
    public double CocoaBeansKg { get; init; }
    public double CocoaKg { get; init; }
    public double BrineL { get; init; }
    public double SodaAshKg { get; init; }
    public double PhosphateKg { get; init; }
    public double StarchKg { get; init; }
    public double WheatSeedKg { get; init; }
    public double BeetSeedKg { get; init; }
    public double IrrigationWaterL { get; init; }
    public double FertilizerKg { get; init; }
    public double AnimalFeedKg { get; init; }
    public double PackagingUnits { get; init; }
    public string ResourceStatus { get; init; } = "";
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
    private bool _stageReadyForOperator;
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
    private double _dairyReadyL = 18;
    private double _eggsReady = 42;

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
    private double _cocoaBeansKg = 60;
    private double _cocoaKg = 20;
    private double _brineL = 900;
    private double _sodaAshKg = 28;
    private double _phosphateKg = 30;
    private double _starchKg = 24;
    private double _wheatSeedKg = 18;
    private double _beetSeedKg = 16;
    private double _irrigationWaterL = 16000;
    private double _fertilizerKg = 120;
    private double _animalFeedKg = 420;
    private double _packagingUnits = 160;
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
    public bool AutoHarvest { get; set; }
    public bool AutoBatch { get; set; }
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
        if (_lastPowerAvailability < 0.15)
            return "Harvesters are locked out until the reactor bus is energized.";

        if (_wheatGrowth < 25 && _beetGrowth < 25 && _vanillaGrowth < 25)
            return "Fields are still immature; keep irrigation and lighting powered.";

        double wheat = _wheatGrowth >= 25 ? 3.8 * _wheatGrowth : 0;
        double beet = _beetGrowth >= 25 ? 7.2 * _beetGrowth : 0;
        double vanilla = _vanillaGrowth >= 25 ? 0.018 * _vanillaGrowth : 0;
        _wheatKg += wheat;
        _sugarCropKg += beet;
        _vanillaL += vanilla;
        if (wheat > 0) _wheatGrowth = 12 + _rng.NextDouble() * 8;
        if (beet > 0) _beetGrowth = 10 + _rng.NextDouble() * 8;
        if (vanilla > 0) _vanillaGrowth = 8 + _rng.NextDouble() * 5;
        _pastureHealth = Math.Min(100, _pastureHealth + 8);
        return $"Harvested {wheat:0} kg wheat, {beet:0} kg sugar crop, {vanilla:0.00} L vanilla extract equivalent.";
    }

    public string CollectDairyAndEggs()
    {
        if (_lastPowerAvailability < 0.12)
            return "Milking parlor and egg grading line need reactor bus power.";

        if (_dairyReadyL < 1 && _eggsReady < 1)
            return "Barn and nest buffers are not ready yet.";

        double milk = Math.Min(_dairyReadyL, 36);
        double eggs = Math.Min(_eggsReady, 96);
        _dairyReadyL -= milk;
        _eggsReady -= eggs;
        _milkL += milk;
        _eggs += eggs;
        return $"Collected {milk:0.0} L milk and {eggs:0} graded eggs.";
    }

    public string MillWheat()
    {
        if (_lastPowerAvailability < 0.2)
            return "The roller mill requires reactor power before wheat can be milled.";

        double wheat = Math.Min(_wheatKg, 90);
        if (wheat < 5)
            return "Not enough harvested wheat is available for a mill run.";

        _wheatKg -= wheat;
        _flourKg += wheat * 0.76;
        _wasteKg += wheat * 0.04;
        return $"Milled {wheat:0} kg wheat into {wheat * 0.76:0.0} kg cake flour.";
    }

    public string RefineSugar()
    {
        if (_lastPowerAvailability < 0.2)
            return "Sugar washing, extraction and evaporation need reactor power.";

        double crop = Math.Min(_sugarCropKg, 160);
        if (crop < 10)
            return "Not enough sugar crop is available for refining.";

        _sugarCropKg -= crop;
        _sugarKg += crop * 0.155;
        _wasteKg += crop * 0.025;
        return $"Refined {crop:0} kg sugar crop into {crop * 0.155:0.0} kg sugar.";
    }

    public string ChurnButter()
    {
        if (_lastPowerAvailability < 0.2)
            return "Cream separator and churn need reactor power.";

        double milk = Math.Min(Math.Max(0, _milkL - 30), 54);
        if (milk < 5)
            return "Keep at least 30 L milk in cold storage before churning butter.";

        _milkL -= milk;
        _butterKg += milk / 22.0;
        return $"Churned {milk:0.0} L milk/cream into {milk / 22.0:0.0} kg butter.";
    }

    public string ReceiveSupplies()
    {
        if (_lastPowerAvailability < 0.1)
            return "Receiving dock, cold room and barcode scales need reactor bus power.";

        _wheatSeedKg += 28;
        _beetSeedKg += 24;
        _irrigationWaterL += 24000;
        _fertilizerKg += 150;
        _animalFeedKg += 640;
        _brineL += 1600;
        _sodaAshKg += 42;
        _phosphateKg += 48;
        _starchKg += 36;
        _packagingUnits += 180;
        _cocoaBeansKg += 90;
        return "Received audited supplies: seed, irrigation water, fertilizer, animal feed, brine, soda ash, phosphate, starch, cartons and cocoa beans.";
    }

    public string ProcessCocoa()
    {
        if (_lastPowerAvailability < 0.2)
            return "Cocoa roaster and grinder need reactor power.";

        double beans = Math.Min(_cocoaBeansKg, 45);
        if (beans < 5)
            return "Not enough cocoa beans are available for a roast/grind run.";

        _cocoaBeansKg -= beans;
        _cocoaKg += beans * 0.78;
        _wasteKg += beans * 0.05;
        return $"Roasted and ground {beans:0} kg cocoa beans into {beans * 0.78:0.0} kg cocoa.";
    }

    public string RunSaltWorks()
    {
        if (_lastPowerAvailability < 0.2)
            return "Salt evaporator and crystallizer need reactor power.";

        double brine = Math.Min(_brineL, 600);
        if (brine < 80)
            return "Not enough brine is available for a salt works run.";

        _brineL -= brine;
        double salt = brine * 0.026;
        _saltKg += salt;
        _wasteKg += brine * 0.001;
        return $"Evaporated {brine:0} L brine into {salt:0.0} kg baking-grade salt.";
    }

    public string RunLeaveningPlant()
    {
        if (_lastPowerAvailability < 0.2)
            return "Leavening plant blender needs reactor power.";

        if (_sodaAshKg < 3 || _phosphateKg < 3 || _starchKg < 2)
            return "Not enough soda ash, phosphate and starch are available for baking powder.";

        double scale = Math.Min(Math.Min(_sodaAshKg / 18.0, _phosphateKg / 18.0), _starchKg / 12.0);
        scale = Math.Min(1.0, scale);
        double soda = 18.0 * scale;
        double phosphate = 18.0 * scale;
        double starch = 12.0 * scale;
        double input = soda + phosphate + starch;

        _sodaAshKg -= soda;
        _phosphateKg -= phosphate;
        _starchKg -= starch;
        _bakingPowderKg += input * 0.92;
        _wasteKg += input * 0.02;
        return $"Blended {input * 0.92:0.0} kg baking powder from soda ash, phosphate and starch.";
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
        _stageReadyForOperator = false;
        _stageSeconds = 0;
        _batchInternalC = 22;
        _mixerSpecificGravity = 1.02;
        _batchQuality = Math.Clamp(86 + _sanitationScore * 0.11 + _rng.NextDouble() * 4, 70, 99);
        _batterKg += BatchIngredientMass(CurrentRecipe);
        message = $"Started {CurrentRecipe.Name} batch ({CurrentRecipe.BatchSize} cakes).";
        return true;
    }

    public bool TryAdvanceStage(out string message)
    {
        if (_stage == CakeBatchStage.Idle)
        {
            message = "No active batch is waiting for operator release.";
            return false;
        }

        if (!_stageReadyForOperator)
        {
            message = $"{StageLabel(_stage)} is still running; wait for the release gate.";
            return false;
        }

        if (_lastPowerAvailability < 0.2)
        {
            message = "Restore reactor bus power before releasing the next powered step.";
            return false;
        }

        if (!StageSafetyGateMet(_stage))
        {
            message = OperatorPrompt(1, MissingIngredients(CurrentRecipe));
            return false;
        }

        var finished = StageLabel(_stage);
        _stageReadyForOperator = false;
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

        message = _stage == CakeBatchStage.Idle
            ? $"{finished} released. Batch packed, coded and logged."
            : $"{finished} released. Next step: {StageLabel(_stage)}.";
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
        UpdateCleaning(seconds, power);
        UpdateBatch(seconds, power);

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
            StageReadyForOperator = _stageReadyForOperator,
            CanAdvanceStage = _stage != CakeBatchStage.Idle && _stageReadyForOperator && power >= 0.2 && StageSafetyGateMet(_stage),
            OperatorPrompt = OperatorPrompt(power, missing),
            MissingIngredients = missing,
            CanHarvest = power >= 0.15 && (_wheatGrowth >= 25 || _beetGrowth >= 25 || _vanillaGrowth >= 25),
            CanCollectDairy = power >= 0.12 && (_dairyReadyL >= 1 || _eggsReady >= 1),
            CanMillWheat = power >= 0.2 && _wheatKg >= 5,
            CanRefineSugar = power >= 0.2 && _sugarCropKg >= 10,
            CanChurnButter = power >= 0.2 && _milkL > 35,
            CanReceiveSupplies = power >= 0.1,
            CanProcessCocoa = power >= 0.2 && _cocoaBeansKg >= 5,
            CanRunSaltWorks = power >= 0.2 && _brineL >= 80,
            CanRunLeaveningPlant = power >= 0.2 && _sodaAshKg >= 3 && _phosphateKg >= 3 && _starchKg >= 2,
            WheatGrowth = _wheatGrowth,
            BeetGrowth = _beetGrowth,
            PastureHealth = _pastureHealth,
            VanillaGrowth = _vanillaGrowth,
            DairyReadyL = _dairyReadyL,
            EggsReady = _eggsReady,
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
            CocoaBeansKg = _cocoaBeansKg,
            CocoaKg = _cocoaKg,
            BrineL = _brineL,
            SodaAshKg = _sodaAshKg,
            PhosphateKg = _phosphateKg,
            StarchKg = _starchKg,
            WheatSeedKg = _wheatSeedKg,
            BeetSeedKg = _beetSeedKg,
            IrrigationWaterL = _irrigationWaterL,
            FertilizerKg = _fertilizerKg,
            AnimalFeedKg = _animalFeedKg,
            PackagingUnits = _packagingUnits,
            ResourceStatus = ResourceStatus(power),
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
        double fieldDemand = Math.Max(0, FarmIntensity * power);
        double fieldWaterNeed = seconds * fieldDemand * 18.0;
        double fertilizerNeed = seconds * fieldDemand * 0.035;
        double wheatSeedNeed = seconds * fieldDemand * 0.0028;
        double beetSeedNeed = seconds * fieldDemand * 0.0024;
        double fieldInputFactor = SupplyFactor(
            (_irrigationWaterL, fieldWaterNeed),
            (_fertilizerKg, fertilizerNeed),
            (_wheatSeedKg, wheatSeedNeed),
            (_beetSeedKg, beetSeedNeed));

        if (fieldInputFactor > 0)
        {
            _irrigationWaterL = Math.Max(0, _irrigationWaterL - fieldWaterNeed * fieldInputFactor);
            _fertilizerKg = Math.Max(0, _fertilizerKg - fertilizerNeed * fieldInputFactor);
            _wheatSeedKg = Math.Max(0, _wheatSeedKg - wheatSeedNeed * fieldInputFactor);
            _beetSeedKg = Math.Max(0, _beetSeedKg - beetSeedNeed * fieldInputFactor);
        }

        double fieldEffect = FarmIntensity * (0.12 + 0.88 * power) * fieldInputFactor;
        _wheatGrowth = Math.Min(100, _wheatGrowth + seconds * 0.16 * fieldEffect);
        _beetGrowth = Math.Min(100, _beetGrowth + seconds * 0.13 * fieldEffect);
        _pastureHealth = Math.Clamp(_pastureHealth + seconds * (0.09 * fieldEffect - 0.015), 10, 100);
        _vanillaGrowth = Math.Min(100, _vanillaGrowth + seconds * 0.045 * fieldEffect);

        if (AutoHarvest && _lastPowerAvailability >= 0.15 && _wheatGrowth >= 100) _wheatKg += HarvestCrop(ref _wheatGrowth, 390);
        if (AutoHarvest && _lastPowerAvailability >= 0.15 && _beetGrowth >= 100) _sugarCropKg += HarvestCrop(ref _beetGrowth, 760);
        if (AutoHarvest && _lastPowerAvailability >= 0.15 && _vanillaGrowth >= 100)
        {
            _vanillaL += 2.2 + _rng.NextDouble() * 0.6;
            _vanillaGrowth = 9 + _rng.NextDouble() * 6;
        }

        double livestockPower = FarmIntensity * power;
        double feedNeed = seconds * livestockPower * 0.22;
        double barnWaterNeed = seconds * livestockPower * 9.0;
        double livestockInputFactor = SupplyFactor((_animalFeedKg, feedNeed), (_irrigationWaterL, barnWaterNeed));
        if (livestockInputFactor > 0)
        {
            _animalFeedKg = Math.Max(0, _animalFeedKg - feedNeed * livestockInputFactor);
            _irrigationWaterL = Math.Max(0, _irrigationWaterL - barnWaterNeed * livestockInputFactor);
        }
        livestockPower *= livestockInputFactor;
        _dairyReadyL = Math.Min(140, _dairyReadyL + seconds * (0.9 + _pastureHealth / 120.0) * livestockPower);
        _eggsReady = Math.Min(420, _eggsReady + seconds * (0.75 + FarmIntensity * 0.55) * livestockPower);
        if (livestockInputFactor <= 0)
            _pastureHealth = Math.Max(10, _pastureHealth - seconds * 0.035);
    }

    private static double SupplyFactor(params (double available, double needed)[] supplies)
    {
        double factor = 1.0;
        foreach (var (available, needed) in supplies)
        {
            if (needed <= 0) continue;
            factor = Math.Min(factor, Math.Clamp(available / needed, 0, 1));
        }
        return factor;
    }

    private double HarvestCrop(ref double growth, double baseYield)
    {
        double yield = baseYield * (0.92 + _rng.NextDouble() * 0.16);
        growth = 10 + _rng.NextDouble() * 7;
        return yield;
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

        if (_stageReadyForOperator)
        {
            double holdPenalty = _stage == CakeBatchStage.Baking ? 0.18 : 0.035;
            _batchQuality = Math.Max(50, _batchQuality - seconds * holdPenalty);
            return;
        }

        double duration = StageDuration(CurrentRecipe, _stage);
        _stageSeconds = Math.Min(duration, _stageSeconds + seconds * stageRate);
        if (_stageSeconds >= duration && StageSafetyGateMet(_stage))
            _stageReadyForOperator = true;
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
        _packagingUnits = Math.Max(0, _packagingUnits - recipe.BatchSize);
        _wasteKg += rejected * 0.42;
        _batterKg = Math.Max(0, _batterKg - BatchIngredientMass(recipe));
        _sanitationScore = Math.Max(0, _sanitationScore - 2.4);
        _batchInternalC = 28;
        _stageReadyForOperator = false;
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

    private bool StageSafetyGateMet(CakeBatchStage stage) => stage switch
    {
        CakeBatchStage.Baking => _batchInternalC >= 71,
        CakeBatchStage.Cooling => _batchInternalC <= 35,
        CakeBatchStage.Icing => _sanitationScore >= 62,
        CakeBatchStage.Packaging => _sanitationScore >= 58,
        _ => true,
    };

    private string OperatorPrompt(double power, string missing)
    {
        if (CipActive)
            return $"CIP sanitation loop active ({(1.0 - _cipSeconds / 24.0) * 100:0}%). Wait for wash, rinse and drain.";

        if (power < 0.2)
            return "Start or recover the nuclear reactor; farm equipment and bakery drives are waiting for bus power.";

        if (_stage == CakeBatchStage.Idle)
            return missing.Length == 0
                ? "Manual mode: harvest, collect, mill/refine/churn as needed, then start a batch."
                : "Manual mode: prep ingredients before batching. Missing: " + missing;

        if (!_stageReadyForOperator)
            return _stage switch
            {
                CakeBatchStage.Scaling => "Operator weighing ingredients; wait for scale verification.",
                CakeBatchStage.Mixing => "Mixer is running. Watch batter specific gravity before release.",
                CakeBatchStage.Depositing => "Depositor is filling pans. Release when pan weights settle.",
                CakeBatchStage.Baking => "Tunnel oven running. Hold until core reaches the kill step.",
                CakeBatchStage.Cooling => "Spiral cooler running. Hold until product is safe for icing.",
                CakeBatchStage.Icing => "Decorating head running. Release after icing coverage is complete.",
                CakeBatchStage.Packaging => "Packer is coding and sealing cartons. Release for QA count.",
                _ => "Line is running.",
            };

        if (!StageSafetyGateMet(_stage))
            return _stage switch
            {
                CakeBatchStage.Baking => $"Do not unload: core is {_batchInternalC:0} degC; wait for at least 71 degC.",
                CakeBatchStage.Cooling => $"Do not ice: cake core is {_batchInternalC:0} degC; cool below 35 degC.",
                CakeBatchStage.Icing => $"Clean or slow down: sanitation is {_sanitationScore:0}%.",
                CakeBatchStage.Packaging => $"Packaging QA hold: sanitation is {_sanitationScore:0}%.",
                _ => "Safety gate is holding the step.",
            };

        return _stage switch
        {
            CakeBatchStage.Scaling => "Scale check complete. Release to mixer.",
            CakeBatchStage.Mixing => "Specific gravity in range. Release to depositor.",
            CakeBatchStage.Depositing => "Pan weights stable. Release to tunnel oven.",
            CakeBatchStage.Baking => "Kill step reached. Release to spiral cooler.",
            CakeBatchStage.Cooling => "Core temperature is icing-safe. Release to decorating.",
            CakeBatchStage.Icing => "Decorating complete. Release to packaging.",
            CakeBatchStage.Packaging => "Cartons coded and counted. Release finished batch.",
            _ => "Release next step.",
        };
    }

    private string HaccpStatus()
    {
        if (_stage == CakeBatchStage.Idle) return _sanitationScore >= 80 ? "Ready - clean line" : "Clean recommended";
        if (_stage is CakeBatchStage.Scaling or CakeBatchStage.Mixing or CakeBatchStage.Depositing) return "Raw flour/egg controls";
        if (_stage == CakeBatchStage.Baking && _batchInternalC >= 71) return "Kill step reached";
        if (_stage == CakeBatchStage.Baking) return "Kill step pending";
        return _batchInternalC >= 35 ? "Cooling below hot zone" : "Post-bake protected";
    }

    private string ResourceStatus(double power)
    {
        if (power < 0.2) return "Reactor bus required";
        var low = new List<string>();
        if (_irrigationWaterL < 800) low.Add("water");
        if (_fertilizerKg < 8) low.Add("fertilizer");
        if (_wheatSeedKg < 1) low.Add("wheat seed");
        if (_beetSeedKg < 1) low.Add("beet seed");
        if (_animalFeedKg < 20) low.Add("feed");
        if (_packagingUnits < CurrentRecipe.BatchSize) low.Add("cartons");
        if (_cocoaBeansKg < 5 && _cocoaKg < CurrentRecipe.CocoaKg * CurrentRecipe.BatchSize) low.Add("cocoa beans");
        if (_brineL < 80 && _saltKg < CurrentRecipe.SaltKg * CurrentRecipe.BatchSize) low.Add("brine");
        if ((_sodaAshKg < 3 || _phosphateKg < 3 || _starchKg < 2)
            && _bakingPowderKg < CurrentRecipe.BakingPowderKg * CurrentRecipe.BatchSize)
            low.Add("leavening feedstocks");
        return low.Count == 0 ? "Inputs stocked" : "Low: " + string.Join(", ", low);
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
        if (_packagingUnits < n) missing.Add("cartons/labels");
        return string.Join(", ", missing);
    }

    private double BatchIngredientMass(CakeRecipe r)
    {
        double n = r.BatchSize;
        return n * (r.FlourKg + r.SugarKg + r.ButterKg + r.MilkL + r.BakingPowderKg + r.SaltKg + r.VanillaL + r.CocoaKg + r.EggCount * 0.052);
    }
}
