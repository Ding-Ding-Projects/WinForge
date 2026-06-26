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
    public int DairyCowCount { get; init; }
    public int LactatingCowCount { get; init; }
    public double CowComfort { get; init; }
    public double MilkProductionLPerHour { get; init; }
    public double MilkParlorThroughputLPerHour { get; init; }
    public string MilkSourceStatus { get; init; } = "";
    public double BulkMilkTankC { get; init; }
    public double MilkBacteriaCfuPerMl { get; init; }
    public double MilkSomaticCellCountKPerMl { get; init; }
    public double MilkFatPct { get; init; }
    public double MilkProteinPct { get; init; }
    public double MilkingVacuumKPa { get; init; }
    public string MilkQaStatus { get; init; } = "";
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
    public double ProcessWaterL { get; init; }
    public double CulinarySteamKg { get; init; }
    public double CompressedAirNm3 { get; init; }
    public double FilterMediaPct { get; init; }
    public string FactoryUtilityStatus { get; init; } = "";
    public string FactoryStatus { get; init; } = "";
    public bool FactoryRunActive { get; init; }
    public string ActiveFactoryName { get; init; } = "";
    public string ActiveFactoryPhase { get; init; } = "";
    public double FactoryProgress { get; init; }
    public double FactoryRunPowerMW { get; init; }
    public double FactoryRunSecondsRemaining { get; init; }
    public double FactoryRunQualityPct { get; init; }
    public bool CanServiceFactories { get; init; }
    public string FactoryMaintenanceStatus { get; init; } = "";
    public double ActiveFactoryConditionPct { get; init; }
    public double ActiveFactoryCalibrationPct { get; init; }
    public double ActiveFactoryBearingTemperatureC { get; init; }
    public double ActiveFactoryVibrationMmS { get; init; }
    public double MillConditionPct { get; init; }
    public double MillCalibrationPct { get; init; }
    public double SugarConditionPct { get; init; }
    public double SugarCalibrationPct { get; init; }
    public double ButterConditionPct { get; init; }
    public double ButterCalibrationPct { get; init; }
    public double CocoaConditionPct { get; init; }
    public double CocoaCalibrationPct { get; init; }
    public double SaltConditionPct { get; init; }
    public double SaltCalibrationPct { get; init; }
    public double LeaveningConditionPct { get; init; }
    public double LeaveningCalibrationPct { get; init; }
    public double MillRollGapMm { get; init; }
    public double FlourExtractionPct { get; init; }
    public double SugarJuiceBrix { get; init; }
    public double SugarEvaporatorTemperatureC { get; init; }
    public double CreamSeparatorRpm { get; init; }
    public double ButterFatPct { get; init; }
    public double CocoaRoasterTemperatureC { get; init; }
    public double CocoaGrindMicrons { get; init; }
    public double BrineSalinityPct { get; init; }
    public double SaltCrystallizerTemperatureC { get; init; }
    public double LeaveningMixerRpm { get; init; }
    public double LeaveningHomogeneityPct { get; init; }
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
    private enum IngredientFactoryKind
    {
        Mill,
        Sugar,
        Butter,
        Cocoa,
        Salt,
        Leavening,
    }

    private sealed class IngredientFactoryRun
    {
        public required IngredientFactoryKind Kind { get; init; }
        public required string Name { get; init; }
        public required string StartedMessage { get; init; }
        public double DurationSeconds { get; init; }
        public double ElapsedSeconds { get; set; }
        public double PowerDemandMW { get; init; }
        public double PrimaryInput { get; init; }
        public double SecondaryInput { get; init; }
        public double TertiaryInput { get; init; }
        public double QuaternaryInput { get; init; }
        public double Product { get; init; }
        public double Waste { get; init; }
        public double ProcessWaterL { get; init; }
        public double CulinarySteamKg { get; init; }
        public double CompressedAirNm3 { get; init; }
        public double FilterMediaPct { get; init; }
        public double WearPct { get; init; }
        public double CalibrationDriftPct { get; init; }
        public double EquipmentConditionAtStart { get; set; }
        public double EquipmentCalibrationAtStart { get; set; }
        public double Progress => DurationSeconds <= 0 ? 1 : Math.Clamp(ElapsedSeconds / DurationSeconds, 0, 1);
        public double RemainingSeconds => Math.Max(0, DurationSeconds - ElapsedSeconds);
    }

    private sealed class IngredientFactoryEquipment
    {
        public IngredientFactoryEquipment(double conditionPct, double calibrationPct, double bearingTemperatureC, double vibrationMmS)
        {
            ConditionPct = conditionPct;
            CalibrationPct = calibrationPct;
            BearingTemperatureC = bearingTemperatureC;
            VibrationMmS = vibrationMmS;
        }

        public double ConditionPct { get; set; }
        public double CalibrationPct { get; set; }
        public double BearingTemperatureC { get; set; }
        public double VibrationMmS { get; set; }
    }

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
    private int _dairyCowCount = 18;
    private int _lactatingCowCount = 14;
    private int _layingHenCount = 72;
    private double _cowComfort = 82;
    private double _milkProductionLPerHour;
    private double _milkParlorThroughputLPerHour = 720;
    private string _milkSourceStatus = "Milk comes from the lactating cow herd after feed, water, pasture and powered milking.";
    private double _bulkMilkTankC = 3.6;
    private double _milkBacteriaCfuPerMl = 8500;
    private double _milkSomaticCellCountKPerMl = 145;
    private double _milkFatPct = 3.8;
    private double _milkProteinPct = 3.25;
    private double _milkingVacuumKPa = 42;

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
    private double _processWaterL = 6000;
    private double _culinarySteamKg = 2600;
    private double _compressedAirNm3 = 900;
    private double _filterMediaPct = 100;
    private string _factoryStatus = "Ingredient factories idle.";
    private string _factoryMaintenanceStatus = "Preventive maintenance normal: all ingredient plants within limits.";
    private readonly Dictionary<IngredientFactoryKind, IngredientFactoryEquipment> _factoryEquipment = new()
    {
        [IngredientFactoryKind.Mill] = new(93, 96, 36, 1.8),
        [IngredientFactoryKind.Sugar] = new(91, 94, 39, 2.0),
        [IngredientFactoryKind.Butter] = new(95, 97, 34, 1.4),
        [IngredientFactoryKind.Cocoa] = new(90, 95, 41, 2.1),
        [IngredientFactoryKind.Salt] = new(92, 93, 38, 1.9),
        [IngredientFactoryKind.Leavening] = new(94, 96, 33, 1.3),
    };
    private double _millRollGapMm = 0.32;
    private double _flourExtractionPct = 76;
    private double _sugarJuiceBrix = 0;
    private double _sugarEvaporatorTemperatureC = 24;
    private double _creamSeparatorRpm = 0;
    private double _butterFatPct = 0;
    private double _cocoaRoasterTemperatureC = 24;
    private double _cocoaGrindMicrons = 0;
    private double _brineSalinityPct = 2.6;
    private double _saltCrystallizerTemperatureC = 24;
    private double _leaveningMixerRpm = 0;
    private double _leaveningHomogeneityPct = 0;
    private double _factoryRunQualityPct = 100;
    private IngredientFactoryRun? _factoryRun;
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
        _milkParlorThroughputLPerHour = 680 + _rng.NextDouble() * 80;
        _milkingVacuumKPa = 40.5 + _rng.NextDouble() * 3.0;
        _milkFatPct = 3.55 + _cowComfort / 100.0 * 0.55 + _rng.NextDouble() * 0.12;
        _milkProteinPct = 3.05 + _pastureHealth / 100.0 * 0.28 + _rng.NextDouble() * 0.06;
        _milkSomaticCellCountKPerMl = Math.Clamp(250 - _cowComfort * 1.25 + _rng.NextDouble() * 25, 80, 420);
        _milkBacteriaCfuPerMl = Math.Clamp(_milkBacteriaCfuPerMl + milk * 28 + (100 - _sanitationScore) * 35, 1200, 60000);
        _bulkMilkTankC = Math.Min(6.0, (_bulkMilkTankC * Math.Max(0, _milkL - milk) + 37.0 * milk) / Math.Max(1, _milkL));
        _milkSourceStatus = $"Transferred {milk:0.0} L raw milk from {_lactatingCowCount} lactating cows through the milking parlor to cold storage.";
        return $"Collected {milk:0.0} L cow milk and {eggs:0} graded eggs; bulk tank {_bulkMilkTankC:0.0} degC, bacteria {_milkBacteriaCfuPerMl:0} CFU/mL.";
    }

    public string MillWheat()
    {
        if (_lastPowerAvailability < 0.2)
            return "The roller mill requires reactor power before wheat can be milled.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        double wheat = Math.Min(_wheatKg, 90);
        if (wheat < 5)
            return "Not enough harvested wheat is available for a mill run.";

        _millRollGapMm = 0.28 + _rng.NextDouble() * 0.05;
        _flourExtractionPct = 0;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Mill,
            Name = "Roller mill",
            StartedMessage = $"Started milling {wheat:0} kg wheat through break rolls, sifters and purifier.",
            DurationSeconds = 8.0,
            PowerDemandMW = 1.8,
            PrimaryInput = wheat,
            Product = wheat * 0.76,
            Waste = wheat * 0.04,
            ProcessWaterL = 20,
            CompressedAirNm3 = 38,
            FilterMediaPct = 0.6,
            WearPct = 1.8,
            CalibrationDriftPct = 0.55,
        };
        return StartFactoryRun(run, () => _wheatKg -= wheat);
    }

    public string RefineSugar()
    {
        if (_lastPowerAvailability < 0.2)
            return "Sugar washing, extraction and evaporation need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        double crop = Math.Min(_sugarCropKg, 160);
        if (crop < 10)
            return "Not enough sugar crop is available for refining.";

        _sugarJuiceBrix = 12.0;
        _sugarEvaporatorTemperatureC = 45.0;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Sugar,
            Name = "Sugar diffuser and evaporator",
            StartedMessage = $"Started washing, slicing, diffusing and evaporating {crop:0} kg sugar crop.",
            DurationSeconds = 10.0,
            PowerDemandMW = 2.2,
            PrimaryInput = crop,
            Product = crop * 0.155,
            Waste = crop * 0.025,
            ProcessWaterL = 320,
            CulinarySteamKg = 520,
            CompressedAirNm3 = 22,
            FilterMediaPct = 1.2,
            WearPct = 2.35,
            CalibrationDriftPct = 0.75,
        };
        return StartFactoryRun(run, () => _sugarCropKg -= crop);
    }

    public string ChurnButter()
    {
        if (_lastPowerAvailability < 0.2)
            return "Cream separator and churn need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        double milk = Math.Min(Math.Max(0, _milkL - 30), 54);
        if (milk < 5)
            return "Keep at least 30 L milk in cold storage before churning butter.";

        _creamSeparatorRpm = 0;
        _butterFatPct = 0;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Butter,
            Name = "Cream separator and butter churn",
            StartedMessage = $"Started separating cream and churning {milk:0.0} L cow milk.",
            DurationSeconds = 7.0,
            PowerDemandMW = 1.4,
            PrimaryInput = milk,
            Product = milk / 22.0,
            ProcessWaterL = 110,
            CulinarySteamKg = 140,
            CompressedAirNm3 = 15,
            FilterMediaPct = 0.8,
            WearPct = 1.45,
            CalibrationDriftPct = 0.45,
        };
        return StartFactoryRun(run, () => _milkL -= milk);
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
        _processWaterL += 9000;
        _culinarySteamKg += 2200;
        _compressedAirNm3 += 720;
        _filterMediaPct = Math.Min(100, _filterMediaPct + 45);
        return "Received audited supplies: seed, irrigation water, fertilizer, animal feed, brine, soda ash, phosphate, starch, cartons, cocoa beans, process water, culinary steam, compressed air and filter media.";
    }

    public string ProcessCocoa()
    {
        if (_lastPowerAvailability < 0.2)
            return "Cocoa roaster and grinder need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        double beans = Math.Min(_cocoaBeansKg, 45);
        if (beans < 5)
            return "Not enough cocoa beans are available for a roast/grind run.";

        _cocoaRoasterTemperatureC = 24;
        _cocoaGrindMicrons = 0;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Cocoa,
            Name = "Cocoa roaster and grinder",
            StartedMessage = $"Started roasting, winnowing and grinding {beans:0} kg cocoa beans.",
            DurationSeconds = 11.0,
            PowerDemandMW = 1.9,
            PrimaryInput = beans,
            Product = beans * 0.78,
            Waste = beans * 0.05,
            ProcessWaterL = 25,
            CompressedAirNm3 = 45,
            FilterMediaPct = 0.7,
            WearPct = 2.15,
            CalibrationDriftPct = 0.65,
        };
        return StartFactoryRun(run, () => _cocoaBeansKg -= beans);
    }

    public string RunSaltWorks()
    {
        if (_lastPowerAvailability < 0.2)
            return "Salt evaporator and crystallizer need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        double brine = Math.Min(_brineL, 600);
        if (brine < 80)
            return "Not enough brine is available for a salt works run.";

        double salt = brine * 0.026;
        _brineSalinityPct = 2.5 + _rng.NextDouble() * 0.4;
        _saltCrystallizerTemperatureC = 28;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Salt,
            Name = "Salt evaporator and crystallizer",
            StartedMessage = $"Started evaporating {brine:0} L brine into baking-grade salt crystals.",
            DurationSeconds = 9.0,
            PowerDemandMW = 1.6,
            PrimaryInput = brine,
            Product = salt,
            Waste = brine * 0.001,
            CulinarySteamKg = 700,
            CompressedAirNm3 = 30,
            FilterMediaPct = 0.4,
            WearPct = 1.95,
            CalibrationDriftPct = 0.50,
        };
        return StartFactoryRun(run, () => _brineL -= brine);
    }

    public string RunLeaveningPlant()
    {
        if (_lastPowerAvailability < 0.2)
            return "Leavening plant blender needs reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        if (_sodaAshKg < 3 || _phosphateKg < 3 || _starchKg < 2)
            return "Not enough soda ash, phosphate and starch are available for baking powder.";

        double scale = Math.Min(Math.Min(_sodaAshKg / 18.0, _phosphateKg / 18.0), _starchKg / 12.0);
        scale = Math.Min(1.0, scale);
        double soda = 18.0 * scale;
        double phosphate = 18.0 * scale;
        double starch = 12.0 * scale;
        double input = soda + phosphate + starch;

        _leaveningMixerRpm = 0;
        _leaveningHomogeneityPct = 0;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Leavening,
            Name = "Leavening weigh-blend plant",
            StartedMessage = $"Started weighing and blending {input:0.0} kg soda ash, phosphate and starch carrier.",
            DurationSeconds = 6.0,
            PowerDemandMW = 1.1,
            PrimaryInput = soda,
            SecondaryInput = phosphate,
            TertiaryInput = starch,
            Product = input * 0.92,
            Waste = input * 0.02,
            CompressedAirNm3 = 50,
            FilterMediaPct = 1.0,
            WearPct = 1.25,
            CalibrationDriftPct = 0.85,
        };
        return StartFactoryRun(run, () =>
        {
            _sodaAshKg -= soda;
            _phosphateKg -= phosphate;
            _starchKg -= starch;
        });
    }

    private string StartFactoryRun(IngredientFactoryRun run, Action consumeInputs)
    {
        string missingUtilities = MissingFactoryUtilities(run);
        if (missingUtilities.Length > 0)
            return $"{run.Name} cannot start; missing factory utilities: {missingUtilities}.";

        var equipment = EquipmentFor(run.Kind);
        if (equipment.ConditionPct < 42)
            return $"{run.Name} is locked out for maintenance; equipment condition is {equipment.ConditionPct:0}%.";
        if (equipment.CalibrationPct < 58)
            return $"{run.Name} needs calibration before release; calibration is {equipment.CalibrationPct:0}%.";

        run.EquipmentConditionAtStart = equipment.ConditionPct;
        run.EquipmentCalibrationAtStart = equipment.CalibrationPct;
        consumeInputs();
        ConsumeFactoryUtilities(run);
        _factoryRun = run;
        _factoryRunQualityPct = Math.Clamp(72 - FactoryEquipmentPenalty(run.Kind), 0, 100);
        _factoryStatus = $"{run.Name} running: {FactoryPhase(run)} at 0% complete, {run.PowerDemandMW:0.0} MW load, condition {equipment.ConditionPct:0}% and calibration {equipment.CalibrationPct:0}%.";
        _factoryMaintenanceStatus = BuildFactoryMaintenanceStatus();
        return run.StartedMessage;
    }

    private string MissingFactoryUtilities(IngredientFactoryRun run)
    {
        var missing = new List<string>();
        if (_processWaterL < run.ProcessWaterL) missing.Add("process water");
        if (_culinarySteamKg < run.CulinarySteamKg) missing.Add("culinary steam");
        if (_compressedAirNm3 < run.CompressedAirNm3) missing.Add("compressed air");
        if (_filterMediaPct < run.FilterMediaPct) missing.Add("filter media");
        return string.Join(", ", missing);
    }

    private void ConsumeFactoryUtilities(IngredientFactoryRun run)
    {
        _processWaterL = Math.Max(0, _processWaterL - run.ProcessWaterL);
        _culinarySteamKg = Math.Max(0, _culinarySteamKg - run.CulinarySteamKg);
        _compressedAirNm3 = Math.Max(0, _compressedAirNm3 - run.CompressedAirNm3);
        _filterMediaPct = Math.Max(0, _filterMediaPct - run.FilterMediaPct);
    }

    private bool HasFactoryUtilities(double processWaterL, double culinarySteamKg, double compressedAirNm3, double filterMediaPct) =>
        _processWaterL >= processWaterL
        && _culinarySteamKg >= culinarySteamKg
        && _compressedAirNm3 >= compressedAirNm3
        && _filterMediaPct >= filterMediaPct;

    public string ServiceIngredientFactories()
    {
        if (_lastPowerAvailability < 0.2)
            return "Maintenance crews need reactor bus power before servicing ingredient factories.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is running; service crews cannot enter until the plant is isolated.";

        const double processWaterL = 120;
        const double culinarySteamKg = 80;
        const double compressedAirNm3 = 35;
        const double filterMediaPct = 1.5;
        if (!HasFactoryUtilities(processWaterL, culinarySteamKg, compressedAirNm3, filterMediaPct))
            return "Factory service cannot start; maintenance needs process water, culinary steam, compressed air and filter media.";

        _processWaterL = Math.Max(0, _processWaterL - processWaterL);
        _culinarySteamKg = Math.Max(0, _culinarySteamKg - culinarySteamKg);
        _compressedAirNm3 = Math.Max(0, _compressedAirNm3 - compressedAirNm3);
        _filterMediaPct = Math.Max(0, _filterMediaPct - filterMediaPct);

        foreach (var equipment in _factoryEquipment.Values)
        {
            equipment.ConditionPct = Math.Min(100, equipment.ConditionPct + 8.0);
            equipment.CalibrationPct = Math.Min(100, equipment.CalibrationPct + 6.0);
            equipment.BearingTemperatureC += (32 - equipment.BearingTemperatureC) * 0.65;
            equipment.VibrationMmS = Math.Max(0.8, equipment.VibrationMmS - 0.7);
        }

        _sanitationScore = Math.Min(100, _sanitationScore + 1.4);
        _factoryMaintenanceStatus = BuildFactoryMaintenanceStatus();
        _factoryStatus = "Maintenance crew serviced roller mill, sugar house, butter room, cocoa line, salt works and leavening blender.";
        return "Serviced all ingredient factories: lubricated bearings, verified guards, replaced filters, calibrated scales and sensors, and signed the maintenance log.";
    }

    private void UpdateFactoryRun(double seconds, double power)
    {
        if (_factoryRun is not { } run) return;

        if (power < 0.2)
        {
            _factoryStatus = $"{run.Name} paused during {FactoryPhase(run)} at {run.Progress:P0}; restore reactor bus power.";
            return;
        }

        double throughput = FactoryThroughputFactor(run.Kind);
        run.ElapsedSeconds = Math.Min(run.DurationSeconds, run.ElapsedSeconds + seconds * Math.Clamp(power, 0, 1) * throughput);
        UpdateFactoryTelemetry(run, power, seconds);

        if (run.Progress >= 1)
        {
            CompleteFactoryRun(run);
            _factoryRun = null;
        }
        else
        {
            _factoryStatus = $"{run.Name} running: {FactoryPhase(run)}, {run.Progress:P0} complete, {run.RemainingSeconds:0.0}s remaining.";
        }
    }

    private void UpdateFactoryTelemetry(IngredientFactoryRun run, double power, double seconds)
    {
        double p = run.Progress;
        var equipment = EquipmentFor(run.Kind);
        double bearingTarget = 31 + run.PowerDemandMW * 3.4 + p * 7.0 + Math.Max(0, 88 - equipment.ConditionPct) * 0.12;
        equipment.BearingTemperatureC += (bearingTarget - equipment.BearingTemperatureC) * Math.Min(1, seconds / 10.0);
        equipment.VibrationMmS = Math.Clamp(equipment.VibrationMmS + Math.Sin(p * Math.PI * 6) * 0.012 + Math.Max(0, 92 - equipment.ConditionPct) * 0.0008 * seconds, 0.6, 12.0);

        _factoryRunQualityPct = Math.Clamp(72 + p * 24 + power * 4 - FactoryEquipmentPenalty(run.Kind), 0, 100);
        switch (run.Kind)
        {
            case IngredientFactoryKind.Mill:
                _flourExtractionPct = 76.0 * p;
                if (p > 0.35) _factoryRunQualityPct -= Math.Abs(_millRollGapMm - 0.30) * 120;
                break;
            case IngredientFactoryKind.Sugar:
                _sugarJuiceBrix = 12.0 + p * 57.0;
                _sugarEvaporatorTemperatureC = 45.0 + p * 61.0;
                if (p > 0.5) _factoryRunQualityPct -= Math.Abs(_sugarEvaporatorTemperatureC - 104.0) * 0.15;
                break;
            case IngredientFactoryKind.Butter:
                _creamSeparatorRpm = p < 0.15 ? 6400 * p / 0.15 : 6400 + Math.Sin(p * Math.PI * 3) * 180;
                _butterFatPct = 35.0 + p * 47.0;
                if (p > 0.4) _factoryRunQualityPct -= Math.Abs(_butterFatPct - 82.0) * 0.12;
                break;
            case IngredientFactoryKind.Cocoa:
                _cocoaRoasterTemperatureC = 24.0 + p * 112.0;
                _cocoaGrindMicrons = p < 0.45 ? 0 : 140.0 - (p - 0.45) / 0.55 * 66.0;
                if (p > 0.5) _factoryRunQualityPct -= Math.Abs(_cocoaRoasterTemperatureC - 134.0) * 0.08;
                break;
            case IngredientFactoryKind.Salt:
                _saltCrystallizerTemperatureC = 28.0 + p * 38.0;
                if (p > 0.45) _factoryRunQualityPct -= Math.Abs(_brineSalinityPct - 2.7) * 8.0;
                break;
            case IngredientFactoryKind.Leavening:
                _leaveningMixerRpm = p < 0.12 ? 90.0 * p / 0.12 : 90.0 + Math.Sin(p * Math.PI * 4) * 12.0;
                _leaveningHomogeneityPct = 52.0 + p * 46.0;
                _factoryRunQualityPct = Math.Min(_factoryRunQualityPct, _leaveningHomogeneityPct);
                break;
        }
        _factoryRunQualityPct = Math.Clamp(_factoryRunQualityPct, 0, 100);
    }

    private static string FactoryPhase(IngredientFactoryRun run)
    {
        double p = run.Progress;
        return run.Kind switch
        {
            IngredientFactoryKind.Mill when p < 0.22 => "magnet check and wheat feed",
            IngredientFactoryKind.Mill when p < 0.48 => "break rolling",
            IngredientFactoryKind.Mill when p < 0.76 => "plansifter separation",
            IngredientFactoryKind.Mill => "purifier and flour bin transfer",
            IngredientFactoryKind.Sugar when p < 0.18 => "wash and slice",
            IngredientFactoryKind.Sugar when p < 0.42 => "diffusion",
            IngredientFactoryKind.Sugar when p < 0.74 => "evaporation",
            IngredientFactoryKind.Sugar => "crystallization and drying",
            IngredientFactoryKind.Butter when p < 0.25 => "cream separation",
            IngredientFactoryKind.Butter when p < 0.58 => "pasteurization hold",
            IngredientFactoryKind.Butter when p < 0.84 => "churning",
            IngredientFactoryKind.Butter => "working and cold-room transfer",
            IngredientFactoryKind.Cocoa when p < 0.28 => "roast ramp",
            IngredientFactoryKind.Cocoa when p < 0.48 => "roast hold",
            IngredientFactoryKind.Cocoa when p < 0.64 => "winnowing",
            IngredientFactoryKind.Cocoa => "pin milling",
            IngredientFactoryKind.Salt when p < 0.30 => "brine preheat",
            IngredientFactoryKind.Salt when p < 0.70 => "vacuum evaporation",
            IngredientFactoryKind.Salt => "crystallizer and centrifuge",
            IngredientFactoryKind.Leavening when p < 0.20 => "ingredient weigh-up",
            IngredientFactoryKind.Leavening when p < 0.55 => "ribbon blending",
            IngredientFactoryKind.Leavening when p < 0.82 => "screening",
            IngredientFactoryKind.Leavening => "lot QA and bin discharge",
            _ => "processing",
        };
    }

    private IngredientFactoryEquipment EquipmentFor(IngredientFactoryKind kind) => _factoryEquipment[kind];

    private static string FactoryName(IngredientFactoryKind kind) => kind switch
    {
        IngredientFactoryKind.Mill => "roller mill",
        IngredientFactoryKind.Sugar => "sugar house",
        IngredientFactoryKind.Butter => "butter room",
        IngredientFactoryKind.Cocoa => "cocoa line",
        IngredientFactoryKind.Salt => "salt works",
        IngredientFactoryKind.Leavening => "leavening plant",
        _ => "ingredient plant",
    };

    private double FactoryThroughputFactor(IngredientFactoryKind kind)
    {
        var equipment = EquipmentFor(kind);
        return Math.Clamp(0.48 + equipment.ConditionPct / 170.0 + equipment.CalibrationPct / 420.0, 0.45, 1.08);
    }

    private double FactoryEquipmentPenalty(IngredientFactoryKind kind)
    {
        var equipment = EquipmentFor(kind);
        double conditionPenalty = Math.Max(0, 96 - equipment.ConditionPct) * 0.38;
        double calibrationPenalty = Math.Max(0, 98 - equipment.CalibrationPct) * 0.30;
        double vibrationPenalty = Math.Max(0, equipment.VibrationMmS - 3.2) * 1.8;
        double bearingPenalty = Math.Max(0, equipment.BearingTemperatureC - 58) * 0.28;
        return conditionPenalty + calibrationPenalty + vibrationPenalty + bearingPenalty;
    }

    private double FactoryYieldFactor(IngredientFactoryKind kind)
    {
        var equipment = EquipmentFor(kind);
        return Math.Clamp(0.82 + equipment.ConditionPct / 620.0 + equipment.CalibrationPct / 980.0, 0.82, 1.0);
    }

    private void ApplyFactoryWear(IngredientFactoryRun run)
    {
        var equipment = EquipmentFor(run.Kind);
        equipment.ConditionPct = Math.Clamp(equipment.ConditionPct - run.WearPct, 0, 100);
        equipment.CalibrationPct = Math.Clamp(equipment.CalibrationPct - run.CalibrationDriftPct, 0, 100);
        equipment.BearingTemperatureC = Math.Clamp(equipment.BearingTemperatureC + run.WearPct * 0.55, 20, 90);
        equipment.VibrationMmS = Math.Clamp(equipment.VibrationMmS + run.WearPct * 0.12, 0.6, 12.0);
    }

    private bool NeedsFactoryService()
    {
        foreach (var equipment in _factoryEquipment.Values)
        {
            if (equipment.ConditionPct < 98 || equipment.CalibrationPct < 99 || equipment.VibrationMmS > 1.0 || equipment.BearingTemperatureC > 33)
                return true;
        }
        return false;
    }

    private IngredientFactoryEquipment LowestConditionEquipment()
    {
        IngredientFactoryEquipment? lowest = null;
        double score = double.MaxValue;
        foreach (var equipment in _factoryEquipment.Values)
        {
            double candidate = Math.Min(equipment.ConditionPct, equipment.CalibrationPct);
            if (candidate < score)
            {
                score = candidate;
                lowest = equipment;
            }
        }
        return lowest ?? EquipmentFor(IngredientFactoryKind.Mill);
    }

    private string BuildFactoryMaintenanceStatus()
    {
        IngredientFactoryKind worstKind = IngredientFactoryKind.Mill;
        IngredientFactoryEquipment worst = EquipmentFor(worstKind);
        double worstScore = Math.Min(worst.ConditionPct, worst.CalibrationPct);
        foreach (var (kind, equipment) in _factoryEquipment)
        {
            double score = Math.Min(equipment.ConditionPct, equipment.CalibrationPct);
            if (score < worstScore)
            {
                worstKind = kind;
                worst = equipment;
                worstScore = score;
            }
        }

        string name = FactoryName(worstKind);
        if (worst.ConditionPct < 55 || worst.CalibrationPct < 65)
            return $"Maintenance lockout risk: {name} at {worst.ConditionPct:0}% condition / {worst.CalibrationPct:0}% calibration.";
        if (worst.ConditionPct < 76 || worst.CalibrationPct < 82 || worst.VibrationMmS > 5.0)
            return $"Maintenance due soon: {name} at {worst.ConditionPct:0}% condition, {worst.CalibrationPct:0}% calibration, {worst.VibrationMmS:0.0} mm/s vibration.";
        return $"Preventive maintenance normal: lowest asset is {name} at {worst.ConditionPct:0}% condition / {worst.CalibrationPct:0}% calibration.";
    }

    private void CompleteFactoryRun(IngredientFactoryRun run)
    {
        double output = run.Product * FactoryYieldFactor(run.Kind);
        double waste = run.Waste + Math.Max(0, run.Product - output);
        switch (run.Kind)
        {
            case IngredientFactoryKind.Mill:
                _flourKg += output;
                _wasteKg += waste;
                _flourExtractionPct = 75.0 + _rng.NextDouble() * 2.0;
                _factoryRunQualityPct = Math.Clamp(96 - Math.Abs(_millRollGapMm - 0.30) * 120 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Roller mill completed: {output:0.0} kg cake flour, {waste:0.0} kg bran/waste, {_millRollGapMm:0.00} mm roll gap, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Sugar:
                _sugarKg += output;
                _wasteKg += waste;
                _sugarJuiceBrix = 67.0 + _rng.NextDouble() * 3.0;
                _sugarEvaporatorTemperatureC = 103.0 + _rng.NextDouble() * 4.0;
                _factoryRunQualityPct = Math.Clamp(98 - Math.Abs(_sugarJuiceBrix - 68.0) * 1.5 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Sugar house completed: {output:0.0} kg sugar at {_sugarJuiceBrix:0.0} Brix and {_sugarEvaporatorTemperatureC:0} degC, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Butter:
                _butterKg += output;
                _creamSeparatorRpm = 6400 + _rng.NextDouble() * 420;
                _butterFatPct = 81.0 + _rng.NextDouble() * 2.5;
                _wasteKg += waste;
                _factoryRunQualityPct = Math.Clamp(98 - Math.Abs(_butterFatPct - 82.0) * 1.4 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Butter room completed: {output:0.0} kg butter at {_butterFatPct:0.0}% butterfat, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Cocoa:
                _cocoaKg += output;
                _wasteKg += waste;
                _cocoaRoasterTemperatureC = 130 + _rng.NextDouble() * 12;
                _cocoaGrindMicrons = 68 + _rng.NextDouble() * 18;
                _factoryRunQualityPct = Math.Clamp(98 - Math.Abs(_cocoaGrindMicrons - 75.0) * 0.35 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Cocoa line completed: {output:0.0} kg cocoa at {_cocoaRoasterTemperatureC:0} degC roast and {_cocoaGrindMicrons:0} micron grind, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Salt:
                _saltKg += output;
                _wasteKg += waste;
                _saltCrystallizerTemperatureC = 62 + _rng.NextDouble() * 8;
                _factoryRunQualityPct = Math.Clamp(97 - Math.Abs(_brineSalinityPct - 2.7) * 5.0 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Salt works completed: {output:0.0} kg baking-grade salt from {_brineSalinityPct:0.0}% brine, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Leavening:
                _bakingPowderKg += output;
                _wasteKg += waste;
                _leaveningMixerRpm = 72 + _rng.NextDouble() * 36;
                _leaveningHomogeneityPct = 96.5 + _rng.NextDouble() * 2.6;
                _factoryRunQualityPct = Math.Clamp(_leaveningHomogeneityPct - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Leavening plant completed: {output:0.0} kg baking powder at {_leaveningHomogeneityPct:0.0}% homogeneity, QA {_factoryRunQualityPct:0}%.";
                break;
        }

        ApplyFactoryWear(run);
        var equipment = EquipmentFor(run.Kind);
        _factoryMaintenanceStatus = BuildFactoryMaintenanceStatus();
        _factoryStatus += $" Equipment now {equipment.ConditionPct:0}% condition, {equipment.CalibrationPct:0}% calibration, {equipment.VibrationMmS:0.0} mm/s vibration.";
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
        double ingredientFactoryDemand = _factoryRun?.PowerDemandMW ?? 0;
        double factoryDemand = 2.4 + LineSpeed * 28.0 + (CipActive ? 2.8 : 0) + ingredientFactoryDemand;
        double demand = farmDemand + factoryDemand;
        bool reactorOnline = reactor.IsGenerating && reactor.ElectricMW > 1 && !reactor.IsMeltdown;
        double power = reactorOnline ? Math.Clamp(reactor.ElectricMW / Math.Max(1, demand), 0, 1) : 0;
        _lastPowerAvailability = power;

        UpdateAnimation(seconds, power);
        UpdateFarm(seconds, power);
        UpdateMilkColdChain(seconds, power);
        UpdateCleaning(seconds, power);
        UpdateFactoryRun(seconds, power);
        UpdateBatch(seconds, power);

        _sanitationScore = Math.Clamp(_sanitationScore - seconds * (0.003 + (_stage == CakeBatchStage.Idle ? 0 : 0.018 * LineSpeed)), 0, 100);

        var missing = MissingIngredients(recipe);
        var displayedEquipment = _factoryRun is null ? LowestConditionEquipment() : EquipmentFor(_factoryRun.Kind);
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
            CanMillWheat = power >= 0.2 && _factoryRun is null && _wheatKg >= 5 && HasFactoryUtilities(20, 0, 38, 0.6),
            CanRefineSugar = power >= 0.2 && _factoryRun is null && _sugarCropKg >= 10 && HasFactoryUtilities(320, 520, 22, 1.2),
            CanChurnButter = power >= 0.2 && _factoryRun is null && _milkL > 35 && HasFactoryUtilities(110, 140, 15, 0.8),
            CanReceiveSupplies = power >= 0.1,
            CanProcessCocoa = power >= 0.2 && _factoryRun is null && _cocoaBeansKg >= 5 && HasFactoryUtilities(25, 0, 45, 0.7),
            CanRunSaltWorks = power >= 0.2 && _factoryRun is null && _brineL >= 80 && HasFactoryUtilities(0, 700, 30, 0.4),
            CanRunLeaveningPlant = power >= 0.2 && _factoryRun is null && _sodaAshKg >= 3 && _phosphateKg >= 3 && _starchKg >= 2 && HasFactoryUtilities(0, 0, 50, 1.0),
            CanServiceFactories = power >= 0.2 && _factoryRun is null && NeedsFactoryService() && HasFactoryUtilities(120, 80, 35, 1.5),
            WheatGrowth = _wheatGrowth,
            BeetGrowth = _beetGrowth,
            PastureHealth = _pastureHealth,
            VanillaGrowth = _vanillaGrowth,
            DairyReadyL = _dairyReadyL,
            EggsReady = _eggsReady,
            DairyCowCount = _dairyCowCount,
            LactatingCowCount = _lactatingCowCount,
            CowComfort = _cowComfort,
            MilkProductionLPerHour = _milkProductionLPerHour,
            MilkParlorThroughputLPerHour = _milkParlorThroughputLPerHour,
            MilkSourceStatus = _milkSourceStatus,
            BulkMilkTankC = _bulkMilkTankC,
            MilkBacteriaCfuPerMl = _milkBacteriaCfuPerMl,
            MilkSomaticCellCountKPerMl = _milkSomaticCellCountKPerMl,
            MilkFatPct = _milkFatPct,
            MilkProteinPct = _milkProteinPct,
            MilkingVacuumKPa = _milkingVacuumKPa,
            MilkQaStatus = MilkQaStatus(),
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
            ProcessWaterL = _processWaterL,
            CulinarySteamKg = _culinarySteamKg,
            CompressedAirNm3 = _compressedAirNm3,
            FilterMediaPct = _filterMediaPct,
            FactoryUtilityStatus = FactoryUtilityStatus(),
            FactoryStatus = _factoryStatus,
            FactoryRunActive = _factoryRun is not null,
            ActiveFactoryName = _factoryRun?.Name ?? "",
            ActiveFactoryPhase = _factoryRun is null ? "" : FactoryPhase(_factoryRun),
            FactoryProgress = _factoryRun?.Progress ?? 0,
            FactoryRunPowerMW = _factoryRun?.PowerDemandMW ?? 0,
            FactoryRunSecondsRemaining = _factoryRun?.RemainingSeconds ?? 0,
            FactoryRunQualityPct = _factoryRunQualityPct,
            FactoryMaintenanceStatus = _factoryMaintenanceStatus,
            ActiveFactoryConditionPct = displayedEquipment.ConditionPct,
            ActiveFactoryCalibrationPct = displayedEquipment.CalibrationPct,
            ActiveFactoryBearingTemperatureC = displayedEquipment.BearingTemperatureC,
            ActiveFactoryVibrationMmS = displayedEquipment.VibrationMmS,
            MillConditionPct = EquipmentFor(IngredientFactoryKind.Mill).ConditionPct,
            MillCalibrationPct = EquipmentFor(IngredientFactoryKind.Mill).CalibrationPct,
            SugarConditionPct = EquipmentFor(IngredientFactoryKind.Sugar).ConditionPct,
            SugarCalibrationPct = EquipmentFor(IngredientFactoryKind.Sugar).CalibrationPct,
            ButterConditionPct = EquipmentFor(IngredientFactoryKind.Butter).ConditionPct,
            ButterCalibrationPct = EquipmentFor(IngredientFactoryKind.Butter).CalibrationPct,
            CocoaConditionPct = EquipmentFor(IngredientFactoryKind.Cocoa).ConditionPct,
            CocoaCalibrationPct = EquipmentFor(IngredientFactoryKind.Cocoa).CalibrationPct,
            SaltConditionPct = EquipmentFor(IngredientFactoryKind.Salt).ConditionPct,
            SaltCalibrationPct = EquipmentFor(IngredientFactoryKind.Salt).CalibrationPct,
            LeaveningConditionPct = EquipmentFor(IngredientFactoryKind.Leavening).ConditionPct,
            LeaveningCalibrationPct = EquipmentFor(IngredientFactoryKind.Leavening).CalibrationPct,
            MillRollGapMm = _millRollGapMm,
            FlourExtractionPct = _flourExtractionPct,
            SugarJuiceBrix = _sugarJuiceBrix,
            SugarEvaporatorTemperatureC = _sugarEvaporatorTemperatureC,
            CreamSeparatorRpm = _creamSeparatorRpm,
            ButterFatPct = _butterFatPct,
            CocoaRoasterTemperatureC = _cocoaRoasterTemperatureC,
            CocoaGrindMicrons = _cocoaGrindMicrons,
            BrineSalinityPct = _brineSalinityPct,
            SaltCrystallizerTemperatureC = _saltCrystallizerTemperatureC,
            LeaveningMixerRpm = _leaveningMixerRpm,
            LeaveningHomogeneityPct = _leaveningHomogeneityPct,
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

    private void UpdateMilkColdChain(double seconds, double power)
    {
        double targetC = power >= 0.15 ? 3.4 : 12.0;
        _bulkMilkTankC += (targetC - _bulkMilkTankC) * Math.Min(1, seconds / (power >= 0.15 ? 18.0 : 42.0));
        double growthFactor = Math.Max(0, _bulkMilkTankC - 4.0) * 0.018 + Math.Max(0, 75 - _sanitationScore) * 0.002;
        _milkBacteriaCfuPerMl = Math.Clamp(_milkBacteriaCfuPerMl * (1.0 + growthFactor * seconds), 1200, 250000);
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
        double simHours = seconds * 0.05 * Math.Max(0.25, FarmIntensity);
        double feedNeed = _dairyCowCount * 1.10 * simHours * livestockPower;
        double barnWaterNeed = _dairyCowCount * 4.20 * simHours * livestockPower;
        double livestockInputFactor = SupplyFactor((_animalFeedKg, feedNeed), (_irrigationWaterL, barnWaterNeed));
        if (livestockInputFactor > 0)
        {
            _animalFeedKg = Math.Max(0, _animalFeedKg - feedNeed * livestockInputFactor);
            _irrigationWaterL = Math.Max(0, _irrigationWaterL - barnWaterNeed * livestockInputFactor);
        }
        double targetComfort = livestockInputFactor > 0
            ? Math.Clamp(46 + _pastureHealth * 0.36 + power * 18, 35, 98)
            : 24;
        _cowComfort += (targetComfort - _cowComfort) * Math.Min(1, seconds / 45.0);

        double comfortFactor = Math.Clamp(_cowComfort / 82.0, 0.25, 1.20);
        double pastureFactor = Math.Clamp(0.70 + _pastureHealth / 360.0, 0.60, 1.05);
        _milkProductionLPerHour = _lactatingCowCount * 1.15 * livestockInputFactor * livestockPower * comfortFactor * pastureFactor;
        _milkParlorThroughputLPerHour = power >= 0.12 ? 650 + 130 * Math.Clamp(power, 0, 1) : 0;

        _dairyReadyL = Math.Min(140, _dairyReadyL + _milkProductionLPerHour * simHours);
        _eggsReady = Math.Min(420, _eggsReady + _layingHenCount * 0.035 * simHours * livestockInputFactor * livestockPower);
        _milkSourceStatus = livestockInputFactor > 0 && livestockPower > 0
            ? $"Milk comes from {_lactatingCowCount} lactating cows; herd consumed {feedNeed * livestockInputFactor:0.0} kg feed and {barnWaterNeed * livestockInputFactor:0} L water this tick."
            : "Milk production stalled: cow herd needs feed, water, pasture health and powered milking systems.";
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
        if (_processWaterL < 200) low.Add("process water");
        if (_culinarySteamKg < 260) low.Add("culinary steam");
        if (_compressedAirNm3 < 60) low.Add("compressed air");
        if (_filterMediaPct < 3) low.Add("filter media");
        return low.Count == 0 ? "Inputs stocked" : "Low: " + string.Join(", ", low);
    }

    private bool MilkQaInSpec() =>
        _milkL <= 0
        || (_bulkMilkTankC <= 7.0
            && _milkBacteriaCfuPerMl <= 100000
            && _milkSomaticCellCountKPerMl <= 400
            && _milkFatPct >= 3.0
            && _milkProteinPct >= 2.9);

    private string MilkQaStatus()
    {
        var issues = new List<string>();
        if (_bulkMilkTankC > 7.0) issues.Add("bulk tank warm");
        if (_milkBacteriaCfuPerMl > 100000) issues.Add("bacteria high");
        if (_milkSomaticCellCountKPerMl > 400) issues.Add("somatic cells high");
        if (_milkFatPct < 3.0) issues.Add("low fat");
        if (_milkProteinPct < 2.9) issues.Add("low protein");
        return issues.Count == 0 ? "Milk QA in spec" : "Milk QA hold: " + string.Join(", ", issues);
    }

    private string FactoryUtilityStatus()
    {
        var low = new List<string>();
        if (_processWaterL < 200) low.Add("process water");
        if (_culinarySteamKg < 260) low.Add("culinary steam");
        if (_compressedAirNm3 < 60) low.Add("compressed air");
        if (_filterMediaPct < 3) low.Add("filter media");
        return low.Count == 0 ? "Utilities ready" : "Low utilities: " + string.Join(", ", low);
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
        else if (r.MilkL > 0 && !MilkQaInSpec()) missing.Add("milk QA");
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
