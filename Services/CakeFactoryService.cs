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
    public bool CanMixDairyRation { get; init; }
    public bool CanWashDairyParlor { get; init; }
    public bool CanWashPoultryHouse { get; init; }
    public bool CanMillWheat { get; init; }
    public bool CanRefineSugar { get; init; }
    public bool CanChurnButter { get; init; }
    public bool CanExtractVanilla { get; init; }
    public bool CanReceiveSupplies { get; init; }
    public bool CanOrderSupplyDelivery { get; init; }
    public bool CanUnloadSupplyDelivery { get; init; }
    public bool SupplyTruckEnRoute { get; init; }
    public bool SupplyTruckArrived { get; init; }
    public double SupplyTruckEtaSeconds { get; init; }
    public double SupplyOrderCost { get; init; }
    public string InboundSupplyManifestId { get; init; } = "";
    public string SupplyOrderStatus { get; init; } = "";
    public bool CanProcessCocoa { get; init; }
    public bool CanRunSaltWorks { get; init; }
    public bool CanRunLeaveningPlant { get; init; }
    public bool CanRunPackagingPlant { get; init; }
    public bool CanPrepareIcing { get; init; }
    public bool CanReleaseLabLot { get; init; }
    public bool CanStageBatchKit { get; init; }
    public double WheatGrowth { get; init; }
    public double BeetGrowth { get; init; }
    public double PastureHealth { get; init; }
    public double VanillaGrowth { get; init; }
    public double DairyReadyL { get; init; }
    public double EggsReady { get; init; }
    public int DairyCowCount { get; init; }
    public int LactatingCowCount { get; init; }
    public int LayingHenCount { get; init; }
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
    public double EggProductionPerHour { get; init; }
    public double HenHouseHygienePct { get; init; }
    public double PoultryManureKg { get; init; }
    public double EggShellQualityPct { get; init; }
    public double EggWasherTemperatureC { get; init; }
    public string EggSourceStatus { get; init; } = "";
    public string EggQaStatus { get; init; } = "";
    public double ForageKg { get; init; }
    public double GrainKg { get; init; }
    public double DairyMineralKg { get; init; }
    public double MixedRationKg { get; init; }
    public double BeddingKg { get; init; }
    public double BarnLaborHours { get; init; }
    public double ManureKg { get; init; }
    public double DairyParlorHygienePct { get; init; }
    public double RationEnergyPct { get; init; }
    public double RationProteinPct { get; init; }
    public string RationStatus { get; init; } = "";
    public string MixedRationLotId { get; init; } = "";
    public string TraceabilityStatus { get; init; } = "";
    public double TraceabilityScorePct { get; init; }
    public string LastSupplyManifestId { get; init; } = "";
    public string CurrentBatchLotId { get; init; } = "";
    public string CurrentBatchTrace { get; init; } = "";
    public bool BatchKitStaged { get; init; }
    public string BatchKitLotId { get; init; } = "";
    public string BatchKitStatus { get; init; } = "";
    public double BatchKitMassKg { get; init; }
    public double ForkliftBatteryPct { get; init; }
    public double WarehousePalletSpacePct { get; init; }
    public string WarehouseStatus { get; init; } = "";
    public string IngredientLabStatus { get; init; } = "";
    public string PendingLabLotId { get; init; } = "";
    public string PendingLabProductName { get; init; } = "";
    public double PendingLabQualityPct { get; init; }
    public string WheatLotId { get; init; } = "";
    public string SugarCropLotId { get; init; } = "";
    public string VanillaBeanLotId { get; init; } = "";
    public string VanillaLotId { get; init; } = "";
    public string MilkLotId { get; init; } = "";
    public string EggLotId { get; init; } = "";
    public string FlourLotId { get; init; } = "";
    public string SugarLotId { get; init; } = "";
    public string ButterLotId { get; init; } = "";
    public string CocoaLotId { get; init; } = "";
    public string SaltLotId { get; init; } = "";
    public string LeaveningLotId { get; init; } = "";
    public string PackagingLotId { get; init; } = "";
    public string IcingLotId { get; init; } = "";
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
    public double VanillaBeansKg { get; init; }
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
    public double PaperboardKg { get; init; }
    public double LabelStockM { get; init; }
    public double PackagingInkL { get; init; }
    public double AdhesiveKg { get; init; }
    public double IcingKg { get; init; }
    public string ResourceStatus { get; init; } = "";
    public double ProcessWaterL { get; init; }
    public double CulinarySteamKg { get; init; }
    public double CompressedAirNm3 { get; init; }
    public double FilterMediaPct { get; init; }
    public string FactoryUtilityStatus { get; init; } = "";
    public bool CanRunUtilityPlant { get; init; }
    public bool UtilityPlantActive { get; init; }
    public string UtilityPlantPhase { get; init; } = "";
    public double UtilityPlantProgress { get; init; }
    public double UtilityPlantPowerMW { get; init; }
    public double UtilityPlantSecondsRemaining { get; init; }
    public string UtilityPlantStatus { get; init; } = "";
    public double ProcessWaterConductivityUsCm { get; init; }
    public double BoilerPressureBar { get; init; }
    public double AirHeaderPressureBar { get; init; }
    public string FactoryStatus { get; init; } = "";
    public bool FactoryRunActive { get; init; }
    public string ActiveFactoryName { get; init; } = "";
    public string ActiveFactoryPhase { get; init; } = "";
    public double FactoryProgress { get; init; }
    public double FactoryRunPowerMW { get; init; }
    public double FactoryRunSecondsRemaining { get; init; }
    public double FactoryRunQualityPct { get; init; }
    public bool CanServiceFactories { get; init; }
    public bool CanHaulByproducts { get; init; }
    public bool CanTreatFactoryEffluent { get; init; }
    public string FactoryMaintenanceStatus { get; init; } = "";
    public string WasteHandlingStatus { get; init; } = "";
    public double BranKg { get; init; }
    public double BeetPulpKg { get; init; }
    public double ButtermilkL { get; init; }
    public double VanillaPomaceKg { get; init; }
    public double CocoaShellKg { get; init; }
    public double BrineBlowdownL { get; init; }
    public double LeaveningDustKg { get; init; }
    public double FactoryEffluentL { get; init; }
    public double ByproductStoragePct { get; init; }
    public double EffluentTankPct { get; init; }
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
    public double VanillaConditionPct { get; init; }
    public double VanillaCalibrationPct { get; init; }
    public double CocoaConditionPct { get; init; }
    public double CocoaCalibrationPct { get; init; }
    public double SaltConditionPct { get; init; }
    public double SaltCalibrationPct { get; init; }
    public double LeaveningConditionPct { get; init; }
    public double LeaveningCalibrationPct { get; init; }
    public double PackagingConditionPct { get; init; }
    public double PackagingCalibrationPct { get; init; }
    public double IcingConditionPct { get; init; }
    public double IcingCalibrationPct { get; init; }
    public double MillRollGapMm { get; init; }
    public double FlourExtractionPct { get; init; }
    public double SugarJuiceBrix { get; init; }
    public double SugarEvaporatorTemperatureC { get; init; }
    public double CreamSeparatorRpm { get; init; }
    public double ButterFatPct { get; init; }
    public double VanillaExtractorTemperatureC { get; init; }
    public double VanillaExtractStrengthPct { get; init; }
    public double CocoaRoasterTemperatureC { get; init; }
    public double CocoaGrindMicrons { get; init; }
    public double BrineSalinityPct { get; init; }
    public double SaltCrystallizerTemperatureC { get; init; }
    public double LeaveningMixerRpm { get; init; }
    public double LeaveningHomogeneityPct { get; init; }
    public double CartonFormerSpeedCpm { get; init; }
    public double PrintRegistrationMm { get; init; }
    public double GluePotTemperatureC { get; init; }
    public bool IcingPrepActive { get; init; }
    public string IcingPrepPhase { get; init; } = "";
    public double IcingPrepProgress { get; init; }
    public double IcingPrepPowerMW { get; init; }
    public double IcingPrepSecondsRemaining { get; init; }
    public string IcingPrepStatus { get; init; } = "";
    public double IcingMixerRpm { get; init; }
    public double IcingTemperatureC { get; init; }
    public double IcingViscosityPaS { get; init; }
    public double BatterKg { get; init; }
    public int CakesBaked { get; init; }
    public int CakesPacked { get; init; }
    public int CakesRejected { get; init; }
    public int FinishedGoodsCakes { get; init; }
    public int OrdersFulfilled { get; init; }
    public string CurrentOrderId { get; init; } = "";
    public int OrderCakesRequired { get; init; }
    public int OrderCakesReady { get; init; }
    public double OrderSecondsRemaining { get; init; }
    public double OrderReward { get; init; }
    public double CashBalance { get; init; }
    public double ReputationPct { get; init; }
    public string OrderStatus { get; init; } = "";
    public bool CanDispatchOrder { get; init; }
    public double DispatchTruckChargePct { get; init; }
    public double DispatchColdChainC { get; init; }
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
        Vanilla,
        Cocoa,
        Salt,
        Leavening,
        Packaging,
        Icing,
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
        public string InputLotId { get; init; } = "";
        public string OutputLotId { get; init; } = "";
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
    private double _eggProductionPerHour;
    private double _henHouseHygienePct = 86;
    private double _poultryManureKg = 95;
    private double _eggShellQualityPct = 94;
    private double _eggWasherTemperatureC = 41;
    private string _eggSourceStatus = "Eggs come from laying hens after feed, water, bedding, nest labor and egg-room grading.";
    private double _milkProductionLPerHour;
    private double _milkParlorThroughputLPerHour = 720;
    private string _milkSourceStatus = "Milk comes from the lactating cow herd after feed, water, pasture and powered milking.";
    private double _bulkMilkTankC = 3.6;
    private double _milkBacteriaCfuPerMl = 8500;
    private double _milkSomaticCellCountKPerMl = 145;
    private double _milkFatPct = 3.8;
    private double _milkProteinPct = 3.25;
    private double _milkingVacuumKPa = 42;
    private double _forageKg = 260;
    private double _grainKg = 180;
    private double _dairyMineralKg = 35;
    private double _mixedRationKg = 90;
    private double _beddingKg = 160;
    private double _barnLaborHours = 32;
    private double _manureKg = 240;
    private double _dairyParlorHygienePct = 88;
    private double _rationEnergyPct = 94;
    private double _rationProteinPct = 92;
    private string _rationStatus = "Total mixed ration TMR-OPENING loaded for the lactating cow herd.";

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
    private double _vanillaBeansKg = 9.5;
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
    private double _paperboardKg = 96;
    private double _labelStockM = 360;
    private double _packagingInkL = 8.5;
    private double _adhesiveKg = 18;
    private double _icingKg = 8.0;
    private int _lotSequence = 2400;
    private string _lastSupplyManifestId = "RCV-OPENING-2400";
    private bool _supplyTruckEnRoute;
    private bool _supplyTruckArrived;
    private double _supplyTruckEtaSeconds;
    private double _supplyOrderCost = 260;
    private string _inboundSupplyManifestId = "";
    private string _supplyOrderStatus = "No supplier delivery scheduled.";
    private string _currentBatchLotId = "";
    private string _currentBatchTrace = "No batch has been started.";
    private string _traceabilityStatus = "Opening audited lots loaded for farm, dairy, ingredient and packaging inventory.";
    private bool _batchKitStaged;
    private string _batchKitRecipeKey = "";
    private string _batchKitLotId = "";
    private string _batchKitTrace = "No batch kit staged.";
    private double _batchKitMassKg;
    private int _batchKitPackagingUnits;
    private double _batchKitIcingKg;
    private string _batchKitIcingLotId = "";
    private double _activeBatchMassKg;
    private double _forkliftBatteryPct = 88;
    private double _warehousePalletSpacePct = 62;
    private string _warehouseStatus = "Warehouse ready: no staged kit on the line.";
    private string _wheatSeedLotId = "SEED-WHT-OPENING";
    private string _beetSeedLotId = "SEED-BEET-OPENING";
    private string _feedLotId = "FEED-OPENING";
    private string _wheatLotId = "WHEAT-OPENING";
    private string _sugarCropLotId = "SUGARCROP-OPENING";
    private string _vanillaBeanLotId = "VANILLABEAN-OPENING";
    private string _vanillaLotId = "VANILLA-OPENING";
    private string _milkLotId = "MILK-OPENING";
    private string _eggLotId = "EGG-OPENING";
    private string _forageLotId = "FORAGE-OPENING";
    private string _grainLotId = "GRAIN-OPENING";
    private string _dairyMineralLotId = "DAIRYMIN-OPENING";
    private string _mixedRationLotId = "TMR-OPENING";
    private string _beddingLotId = "BEDDING-OPENING";
    private string _flourLotId = "FLOUR-OPENING";
    private string _sugarLotId = "SUGAR-OPENING";
    private string _butterLotId = "BUTTER-OPENING";
    private string _cocoaBeansLotId = "COCOA-BEAN-OPENING";
    private string _cocoaLotId = "COCOA-OPENING";
    private string _brineLotId = "BRINE-OPENING";
    private string _saltLotId = "SALT-OPENING";
    private string _mineralLotId = "MINERAL-OPENING";
    private string _leaveningLotId = "LEAVEN-OPENING";
    private string _packagingLotId = "PACK-OPENING";
    private string _icingLotId = "ICING-OPENING";
    private string _paperboardLotId = "PAPERBOARD-OPENING";
    private string _labelStockLotId = "LABEL-OPENING";
    private string _packagingInkLotId = "INK-OPENING";
    private string _adhesiveLotId = "ADHESIVE-OPENING";
    private string _utilityLotId = "UTILITY-OPENING";
    private readonly HashSet<string> _releasedFactoryLots = new(StringComparer.OrdinalIgnoreCase)
    {
        "FLOUR-OPENING",
        "SUGAR-OPENING",
        "BUTTER-OPENING",
        "VANILLA-OPENING",
        "COCOA-OPENING",
        "SALT-OPENING",
        "LEAVEN-OPENING",
        "PACK-OPENING",
        "ICING-OPENING",
    };
    private string _ingredientLabStatus = "Factory QA lab released opening ingredient lots.";
    private IngredientFactoryKind _pendingLabKind = IngredientFactoryKind.Mill;
    private string _pendingLabLotId = "";
    private string _pendingLabProductName = "";
    private double _pendingLabQualityPct;
    private double _pendingLabQuantity;
    private double _processWaterL = 6000;
    private double _culinarySteamKg = 2600;
    private double _compressedAirNm3 = 900;
    private double _filterMediaPct = 100;
    private const double ProcessWaterCapacityL = 9000;
    private const double CulinarySteamCapacityKg = 4200;
    private const double CompressedAirCapacityNm3 = 1500;
    private const double UtilityPlantDurationSeconds = 9.5;
    private const double UtilityPlantPowerDemandMW = 2.2;
    private bool _utilityPlantActive;
    private double _utilityPlantElapsedSeconds;
    private string _utilityPlantStatus = "Utility plant idle: RO skid, clean-steam boiler and compressor stand ready.";
    private double _processWaterConductivityUsCm = 0.35;
    private double _boilerPressureBar = 3.2;
    private double _airHeaderPressureBar = 6.6;
    private string _factoryStatus = "Ingredient factories idle.";
    private string _factoryMaintenanceStatus = "Preventive maintenance normal: all ingredient plants within limits.";
    private readonly Dictionary<IngredientFactoryKind, IngredientFactoryEquipment> _factoryEquipment = new()
    {
        [IngredientFactoryKind.Mill] = new(93, 96, 36, 1.8),
        [IngredientFactoryKind.Sugar] = new(91, 94, 39, 2.0),
        [IngredientFactoryKind.Butter] = new(95, 97, 34, 1.4),
        [IngredientFactoryKind.Vanilla] = new(92, 95, 35, 1.5),
        [IngredientFactoryKind.Cocoa] = new(90, 95, 41, 2.1),
        [IngredientFactoryKind.Salt] = new(92, 93, 38, 1.9),
        [IngredientFactoryKind.Leavening] = new(94, 96, 33, 1.3),
        [IngredientFactoryKind.Packaging] = new(91, 94, 37, 1.7),
        [IngredientFactoryKind.Icing] = new(92, 95, 34, 1.5),
    };
    private double _millRollGapMm = 0.32;
    private double _flourExtractionPct = 76;
    private double _sugarJuiceBrix = 0;
    private double _sugarEvaporatorTemperatureC = 24;
    private double _creamSeparatorRpm = 0;
    private double _butterFatPct = 0;
    private double _vanillaExtractorTemperatureC = 24;
    private double _vanillaExtractStrengthPct = 0;
    private double _cocoaRoasterTemperatureC = 24;
    private double _cocoaGrindMicrons = 0;
    private double _brineSalinityPct = 2.6;
    private double _saltCrystallizerTemperatureC = 24;
    private double _leaveningMixerRpm = 0;
    private double _leaveningHomogeneityPct = 0;
    private double _cartonFormerSpeedCpm = 0;
    private double _printRegistrationMm = 0.18;
    private double _gluePotTemperatureC = 24;
    private double _icingMixerRpm = 0;
    private double _icingTemperatureC = 22;
    private double _icingViscosityPaS = 7.8;
    private double _factoryRunQualityPct = 100;
    private IngredientFactoryRun? _factoryRun;
    private double _batterKg;
    private double _wasteKg;
    private const double ByproductStorageCapacityKg = 900;
    private const double FactoryEffluentCapacityL = 3000;
    private double _branKg = 34;
    private double _beetPulpKg = 52;
    private double _buttermilkL = 18;
    private double _vanillaPomaceKg = 2.0;
    private double _cocoaShellKg = 6;
    private double _brineBlowdownL = 80;
    private double _leaveningDustKg = 1.2;
    private double _factoryEffluentL = 360;
    private string _wasteHandlingStatus = "Byproduct bins and effluent equalization tank ready.";

    private int _cakesBaked;
    private int _cakesPacked;
    private int _cakesRejected;
    private int _finishedGoodsCakes;
    private int _ordersFulfilled;
    private int _orderSequence = 5100;
    private string _currentOrderId = "ORD-005100";
    private int _orderCakesRequired = 12;
    private double _orderSecondsRemaining = 420;
    private double _orderReward = 240;
    private double _cashBalance = 500;
    private double _reputationPct = 84;
    private string _orderStatus = "Order ORD-005100 waiting for 12 packed cakes.";
    private double _dispatchTruckChargePct = 76;
    private double _dispatchColdChainC = 4.2;

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

    private string NewLotId(string prefix) => $"{prefix}-{++_lotSequence:000000}";

    private static void ConsumeTrackedStock(ref double quantity, double amount, ref string lotId)
    {
        quantity = Math.Max(0, quantity - amount);
        if (quantity <= 0.001) lotId = "";
    }

    private static bool HasLot(double quantity, string lotId) =>
        quantity <= 0.001 || !string.IsNullOrWhiteSpace(lotId);

    private bool FactoryLotReleased(double quantity, string lotId) =>
        quantity <= 0.001 || (!string.IsNullOrWhiteSpace(lotId) && _releasedFactoryLots.Contains(lotId));

    private bool RecipeLotDataPresent(CakeRecipe r)
    {
        double n = r.BatchSize;
        return HasLot(r.FlourKg * n, _flourLotId)
               && HasLot(r.SugarKg * n, _sugarLotId)
               && HasLot(r.EggCount * n, _eggLotId)
               && HasLot(r.ButterKg * n, _butterLotId)
               && HasLot(r.MilkL * n, _milkLotId)
               && HasLot(r.BakingPowderKg * n, _leaveningLotId)
               && HasLot(r.SaltKg * n, _saltLotId)
               && HasLot(r.VanillaL * n, _vanillaLotId)
               && HasLot(r.CocoaKg * n, _cocoaLotId)
               && HasLot(IcingNeedKg(r), _icingLotId)
               && HasLot(n, _packagingLotId);
    }

    private bool RecipeFactoryLotsReleased(CakeRecipe r)
    {
        double n = r.BatchSize;
        return FactoryLotReleased(r.FlourKg * n, _flourLotId)
               && FactoryLotReleased(r.SugarKg * n, _sugarLotId)
               && FactoryLotReleased(r.ButterKg * n, _butterLotId)
               && FactoryLotReleased(r.BakingPowderKg * n, _leaveningLotId)
               && FactoryLotReleased(r.SaltKg * n, _saltLotId)
               && FactoryLotReleased(r.VanillaL * n, _vanillaLotId)
               && FactoryLotReleased(r.CocoaKg * n, _cocoaLotId)
               && FactoryLotReleased(IcingNeedKg(r), _icingLotId)
               && FactoryLotReleased(n, _packagingLotId);
    }

    private bool RecipeLotsReady(CakeRecipe r) =>
        RecipeLotDataPresent(r) && RecipeFactoryLotsReleased(r);

    private static double IcingNeedKg(CakeRecipe r) =>
        r.BatchSize * (string.Equals(r.Key, "butter-pound", StringComparison.Ordinal) ? 0.075 : 0.12);

    private static (double SugarKg, double ButterKg, double MilkL, double VanillaL, double CocoaKg, double ProductKg) IcingFormula(CakeRecipe r)
    {
        double target = Math.Max(IcingNeedKg(r) * 1.25, 1.1);
        bool chocolate = r.CocoaKg > 0;
        double sugar = target * (chocolate ? 0.50 : 0.56);
        double butter = target * (string.Equals(r.Key, "butter-pound", StringComparison.Ordinal) ? 0.25 : 0.20);
        double milk = target * (string.Equals(r.Key, "butter-pound", StringComparison.Ordinal) ? 0.10 : 0.16);
        double vanilla = target * 0.015;
        double cocoa = chocolate ? target * 0.12 : 0;
        double product = (sugar + butter + milk + vanilla + cocoa) * 0.98;
        return (sugar, butter, milk, vanilla, cocoa, product);
    }

    private bool IcingInputsAvailable(CakeRecipe r)
    {
        var formula = IcingFormula(r);
        return _sugarKg >= formula.SugarKg
               && _butterKg >= formula.ButterKg
               && _milkL >= formula.MilkL
               && _vanillaL >= formula.VanillaL
               && _cocoaKg >= formula.CocoaKg
               && HasFactoryUtilities(38, 46, 12, 0.22)
               && MilkQaInSpec();
    }

    private bool IcingInputLotsReady(CakeRecipe r)
    {
        var formula = IcingFormula(r);
        return FactoryLotReleased(formula.SugarKg, _sugarLotId)
               && FactoryLotReleased(formula.ButterKg, _butterLotId)
               && HasLot(formula.MilkL, _milkLotId)
               && FactoryLotReleased(formula.VanillaL, _vanillaLotId)
               && FactoryLotReleased(formula.CocoaKg, _cocoaLotId);
    }

    private bool CanPrepareIcing(CakeRecipe r, double power, bool labClear, bool wasteReady) =>
        power >= 0.2
        && _factoryRun is null
        && labClear
        && wasteReady
        && IcingInputsAvailable(r)
        && IcingInputLotsReady(r);

    private string BuildBatchTrace(CakeRecipe r, string batchLotId)
    {
        return $"{batchLotId}: {r.Name} uses flour {_flourLotId}, sugar {_sugarLotId}, eggs {_eggLotId}, milk {_milkLotId}, butter {_butterLotId}, leavening {_leaveningLotId}, salt {_saltLotId}, vanilla {_vanillaLotId}, cocoa {(r.CocoaKg > 0 ? _cocoaLotId : "not required")}, prepared icing {_icingLotId} and packaging {_packagingLotId}.";
    }

    private void CreateNextOrder()
    {
        _currentOrderId = $"ORD-{++_orderSequence:000000}";
        _orderCakesRequired = CurrentRecipe.BatchSize + (_ordersFulfilled % 3) * 4;
        _orderSecondsRemaining = 360 + _orderCakesRequired * 8;
        _orderReward = 18.0 * _orderCakesRequired + Math.Max(0, _reputationPct - 70) * 1.5;
        _orderStatus = $"Order {_currentOrderId} waiting for {_orderCakesRequired} packed cakes.";
    }

    private bool DispatchReady(double power) =>
        power >= 0.15
        && _finishedGoodsCakes >= _orderCakesRequired
        && _dispatchTruckChargePct >= 18
        && _dispatchColdChainC <= 8.0;

    private double ByproductStorageLoadKg() =>
        _branKg
        + _beetPulpKg
        + _vanillaPomaceKg
        + _cocoaShellKg
        + _leaveningDustKg
        + _buttermilkL * 1.03
        + _brineBlowdownL * 1.05;

    private double ByproductStoragePctValue() =>
        Math.Clamp(ByproductStorageLoadKg() / ByproductStorageCapacityKg * 100.0, 0, 160);

    private double EffluentTankPctValue() =>
        Math.Clamp(_factoryEffluentL / FactoryEffluentCapacityL * 100.0, 0, 160);

    private static double ExpectedByproductLoadKg(IngredientFactoryRun run) => run.Kind switch
    {
        IngredientFactoryKind.Mill => run.PrimaryInput * 0.22,
        IngredientFactoryKind.Sugar => run.PrimaryInput * 0.34,
        IngredientFactoryKind.Butter => run.PrimaryInput * 0.78,
        IngredientFactoryKind.Vanilla => run.PrimaryInput * 0.32,
        IngredientFactoryKind.Cocoa => run.PrimaryInput * 0.14,
        IngredientFactoryKind.Salt => run.PrimaryInput * 0.16 * 1.05,
        IngredientFactoryKind.Leavening => run.Product * 0.025,
        IngredientFactoryKind.Packaging => 0,
        _ => run.Waste,
    };

    private static double ExpectedEffluentL(IngredientFactoryRun run) => run.Kind switch
    {
        IngredientFactoryKind.Sugar => run.ProcessWaterL * 0.72 + run.CulinarySteamKg * 0.08,
        IngredientFactoryKind.Butter => run.ProcessWaterL * 0.55 + run.PrimaryInput * 0.08,
        IngredientFactoryKind.Vanilla => run.ProcessWaterL * 0.62,
        IngredientFactoryKind.Salt => run.PrimaryInput * 0.14,
        IngredientFactoryKind.Cocoa => run.ProcessWaterL * 0.35,
        IngredientFactoryKind.Leavening => run.FilterMediaPct * 8.0,
        IngredientFactoryKind.Packaging => run.ProcessWaterL * 0.45 + run.QuaternaryInput * 0.7,
        _ => run.ProcessWaterL * 0.25,
    };

    private bool HasWasteHandlingCapacity(IngredientFactoryRun run, out string issue)
    {
        double nextByproductPct = (ByproductStorageLoadKg() + ExpectedByproductLoadKg(run)) / ByproductStorageCapacityKg * 100.0;
        if (nextByproductPct > 100)
        {
            issue = $"byproduct storage would reach {nextByproductPct:0}%";
            return false;
        }

        double nextEffluentPct = (_factoryEffluentL + ExpectedEffluentL(run)) / FactoryEffluentCapacityL * 100.0;
        if (nextEffluentPct > 100)
        {
            issue = $"factory effluent tank would reach {nextEffluentPct:0}%";
            return false;
        }

        issue = "";
        return true;
    }

    private bool WasteHandlingReady(IngredientFactoryRun run) =>
        HasWasteHandlingCapacity(run, out _);

    private string WasteHandlingStatus()
    {
        var issues = new List<string>();
        double byproductPct = ByproductStoragePctValue();
        double effluentPct = EffluentTankPctValue();
        if (byproductPct >= 90) issues.Add($"byproduct bins {byproductPct:0}%");
        if (effluentPct >= 90) issues.Add($"effluent tank {effluentPct:0}%");
        if (issues.Count > 0) return "Waste handling near capacity: " + string.Join(", ", issues);
        return _wasteHandlingStatus;
    }

    public string DispatchOrder()
    {
        if (_lastPowerAvailability < 0.15)
            return "Dispatch dock, scanner and reefer truck charger need reactor bus power.";
        if (_finishedGoodsCakes < _orderCakesRequired)
            return $"Order {_currentOrderId} needs {_orderCakesRequired} packed cakes; finished goods has {_finishedGoodsCakes}.";
        if (_dispatchTruckChargePct < 18)
            return $"Dispatch truck charge is low ({_dispatchTruckChargePct:0}%).";
        if (_dispatchColdChainC > 8.0)
            return $"Dispatch cold chain is too warm ({_dispatchColdChainC:0.0} degC).";

        double deadlineFactor = _orderSecondsRemaining >= 0 ? 1.0 : 0.72;
        double qualityFactor = Math.Clamp(Snapshot.QualityScore / 100.0, 0.55, 1.05);
        double paid = Math.Round(_orderReward * deadlineFactor * qualityFactor, 2);
        _finishedGoodsCakes -= _orderCakesRequired;
        _cashBalance += paid;
        _dispatchTruckChargePct = Math.Max(0, _dispatchTruckChargePct - 10 - _orderCakesRequired * 0.18);
        _reputationPct = Math.Clamp(_reputationPct + (_orderSecondsRemaining >= 0 ? 2.8 : -4.5) + (qualityFactor - 0.82) * 3.0, 0, 100);
        _ordersFulfilled++;
        string shipped = $"Dispatched {_currentOrderId}: {_orderCakesRequired} cakes, paid ${paid:0.00}, reputation {_reputationPct:0}%.";
        _orderStatus = shipped;
        CreateNextOrder();
        return shipped + " Next " + _orderStatus;
    }

    public string HarvestNow()
    {
        if (_lastPowerAvailability < 0.15)
            return "Harvesters are locked out until the reactor bus is energized.";

        if (_wheatGrowth < 25 && _beetGrowth < 25 && _vanillaGrowth < 25)
            return "Fields are still immature; keep irrigation and lighting powered.";

        double wheat = _wheatGrowth >= 25 ? 3.8 * _wheatGrowth : 0;
        double beet = _beetGrowth >= 25 ? 7.2 * _beetGrowth : 0;
        double vanillaBeans = _vanillaGrowth >= 25 ? 0.055 * _vanillaGrowth : 0;
        _wheatKg += wheat;
        _sugarCropKg += beet;
        _vanillaBeansKg += vanillaBeans;
        var lots = new List<string>();
        if (wheat > 0)
        {
            _wheatLotId = NewLotId("WHEAT");
            lots.Add($"wheat {_wheatLotId}");
        }
        if (beet > 0)
        {
            _sugarCropLotId = NewLotId("SUGARCROP");
            lots.Add($"sugar crop {_sugarCropLotId}");
        }
        if (vanillaBeans > 0)
        {
            _vanillaBeanLotId = NewLotId("VANILLABEAN");
            lots.Add($"vanilla beans {_vanillaBeanLotId}");
        }
        if (wheat > 0) _wheatGrowth = 12 + _rng.NextDouble() * 8;
        if (beet > 0) _beetGrowth = 10 + _rng.NextDouble() * 8;
        if (vanillaBeans > 0) _vanillaGrowth = 8 + _rng.NextDouble() * 5;
        _pastureHealth = Math.Min(100, _pastureHealth + 8);
        _traceabilityStatus = lots.Count == 0
            ? "Harvest completed with no new traceable lots."
            : "Harvest trace logged: " + string.Join(", ", lots) + ".";
        return $"Harvested {wheat:0} kg wheat, {beet:0} kg sugar crop, {vanillaBeans:0.0} kg green vanilla beans for curing/extraction.";
    }

    public string MixDairyRation()
    {
        if (_lastPowerAvailability < 0.15)
            return "The TMR mixer wagon and ration scales need reactor bus power.";

        const double forage = 48.0;
        const double grain = 22.0;
        const double mineral = 2.2;
        const double water = 38.0;
        const double labor = 0.8;
        if (_forageKg < forage || _grainKg < grain || _dairyMineralKg < mineral)
            return "Dairy ration cannot be mixed; forage, grain or mineral premix is low.";
        if (_irrigationWaterL < water || _barnLaborHours < labor)
            return "Dairy ration needs water and barn labor before the herd can be fed.";
        if (string.IsNullOrWhiteSpace(_forageLotId) || string.IsNullOrWhiteSpace(_grainLotId) || string.IsNullOrWhiteSpace(_dairyMineralLotId))
            return "Dairy ration cannot be mixed because a feed ingredient lot is missing from the ledger.";

        _forageKg -= forage;
        _grainKg -= grain;
        _dairyMineralKg -= mineral;
        _irrigationWaterL -= water;
        _barnLaborHours -= labor;
        double output = forage + grain + mineral + water * 0.18;
        _mixedRationKg += output;
        _mixedRationLotId = NewLotId("TMR");
        _rationEnergyPct = Math.Clamp(86 + grain / 22.0 * 8 + _pastureHealth / 100.0 * 3, 70, 102);
        _rationProteinPct = Math.Clamp(84 + forage / 48.0 * 5 + mineral / 2.2 * 5, 70, 101);
        _rationStatus = $"Mixed TMR lot {_mixedRationLotId}: {forage:0} kg forage, {grain:0} kg grain, {mineral:0.0} kg mineral premix, energy {_rationEnergyPct:0}% and protein {_rationProteinPct:0}%.";
        _traceabilityStatus = $"Dairy ration trace logged: {_mixedRationLotId} from forage {_forageLotId}, grain {_grainLotId} and mineral {_dairyMineralLotId}.";
        return _rationStatus;
    }

    public string WashDairyParlor()
    {
        if (_lastPowerAvailability < 0.15)
            return "Dairy washdown pumps, hot-water set and scraper alley need reactor bus power.";

        const double processWater = 180.0;
        const double steam = 60.0;
        const double air = 12.0;
        const double filter = 0.5;
        const double labor = 1.4;
        if (!HasFactoryUtilities(processWater, steam, air, filter) || _barnLaborHours < labor)
            return "Dairy washdown needs process water, culinary steam, compressed air, filter media and barn labor.";

        _processWaterL -= processWater;
        _culinarySteamKg -= steam;
        _compressedAirNm3 -= air;
        _filterMediaPct = Math.Max(0, _filterMediaPct - filter);
        _barnLaborHours -= labor;
        double manureRemoved = Math.Min(_manureKg, 340);
        _manureKg -= manureRemoved;
        _dairyParlorHygienePct = Math.Min(100, _dairyParlorHygienePct + 34);
        _cowComfort = Math.Min(100, _cowComfort + 4);
        _milkBacteriaCfuPerMl = Math.Max(1200, _milkBacteriaCfuPerMl * 0.78);
        _milkSomaticCellCountKPerMl = Math.Max(70, _milkSomaticCellCountKPerMl * 0.94);
        _milkSourceStatus = $"Parlor washed and alleys scraped; removed {manureRemoved:0} kg manure before the next milking.";
        return $"Washed dairy parlor: hygiene {_dairyParlorHygienePct:0}%, removed {manureRemoved:0} kg manure, labor {_barnLaborHours:0.0} h remaining.";
    }

    public string WashPoultryHouse()
    {
        if (_lastPowerAvailability < 0.15)
            return "Poultry-house washdown pumps and egg-room sanitizer need reactor bus power.";

        const double processWater = 90.0;
        const double steam = 30.0;
        const double air = 8.0;
        const double filter = 0.35;
        const double labor = 1.0;
        if (!HasFactoryUtilities(processWater, steam, air, filter) || _barnLaborHours < labor)
            return "Poultry washdown needs process water, culinary steam, compressed air, filter media and barn labor.";

        _processWaterL -= processWater;
        _culinarySteamKg -= steam;
        _compressedAirNm3 -= air;
        _filterMediaPct = Math.Max(0, _filterMediaPct - filter);
        _barnLaborHours -= labor;
        double manureRemoved = Math.Min(_poultryManureKg, 180);
        _poultryManureKg -= manureRemoved;
        _henHouseHygienePct = Math.Min(100, _henHouseHygienePct + 38);
        _eggShellQualityPct = Math.Min(100, _eggShellQualityPct + 4);
        _eggSourceStatus = $"Poultry house washed, nests rebedded and egg-room sanitizer verified for {_layingHenCount} laying hens.";
        _traceabilityStatus = $"Poultry sanitation logged before egg collection; current egg lot {_eggLotId}.";
        return $"Washed poultry house: hygiene {_henHouseHygienePct:0}%, removed {manureRemoved:0} kg poultry manure, labor {_barnLaborHours:0.0} h remaining.";
    }

    public string CollectDairyAndEggs()
    {
        if (_lastPowerAvailability < 0.12)
            return "Milking parlor and egg grading line need reactor bus power.";

        if (_dairyReadyL < 1 && _eggsReady < 1)
            return "Barn and nest buffers are not ready yet.";

        double milk = Math.Min(_dairyReadyL, 36);
        double eggs = Math.Min(_eggsReady, 96);
        if (eggs > 0 && !HasFactoryUtilities(8, 0, 2, 0.05))
            return "Egg washer and grading line need process water, compressed air and filter media before eggs can enter inventory.";

        _dairyReadyL -= milk;
        _eggsReady -= eggs;
        _milkL += milk;
        _eggs += eggs;
        if (eggs > 0)
        {
            _processWaterL = Math.Max(0, _processWaterL - 8);
            _compressedAirNm3 = Math.Max(0, _compressedAirNm3 - 2);
            _filterMediaPct = Math.Max(0, _filterMediaPct - 0.05);
            _eggWasherTemperatureC = 39.0 + _rng.NextDouble() * 4.0;
            double eggHygienePenalty = Math.Max(0, 84 - _henHouseHygienePct);
            _eggShellQualityPct = Math.Clamp(96 - eggHygienePenalty * 0.32 - Math.Max(0, _poultryManureKg - 320) * 0.015 + _rng.NextDouble() * 2.0, 55, 100);
            _henHouseHygienePct = Math.Max(0, _henHouseHygienePct - eggs * 0.018);
            _eggSourceStatus = $"Graded {eggs:0} eggs from {_layingHenCount} laying hens fed by lot {_feedLotId}; shell QA {_eggShellQualityPct:0}% and washer {_eggWasherTemperatureC:0.0} degC.";
        }
        if (milk > 0) _milkLotId = NewLotId("MILK");
        if (eggs > 0) _eggLotId = NewLotId("EGG");
        _milkParlorThroughputLPerHour = 680 + _rng.NextDouble() * 80;
        _milkingVacuumKPa = 40.5 + _rng.NextDouble() * 3.0;
        _milkFatPct = 3.55 + _cowComfort / 100.0 * 0.55 + _rng.NextDouble() * 0.12;
        _milkProteinPct = 3.05 + _pastureHealth / 100.0 * 0.28 + _rng.NextDouble() * 0.06;
        double hygienePenalty = Math.Max(0, 86 - _dairyParlorHygienePct);
        double manurePenalty = Math.Max(0, _manureKg - 650) * 0.025;
        _milkSomaticCellCountKPerMl = Math.Clamp(250 - _cowComfort * 1.25 + hygienePenalty * 3.0 + manurePenalty + _rng.NextDouble() * 25, 80, 520);
        _milkBacteriaCfuPerMl = Math.Clamp(_milkBacteriaCfuPerMl + milk * (24 + hygienePenalty * 1.8) + (100 - _sanitationScore) * 35, 1200, 85000);
        _bulkMilkTankC = Math.Min(6.0, (_bulkMilkTankC * Math.Max(0, _milkL - milk) + 37.0 * milk) / Math.Max(1, _milkL));
        _dairyParlorHygienePct = Math.Max(0, _dairyParlorHygienePct - milk * 0.045);
        _milkSourceStatus = $"Transferred {milk:0.0} L raw milk from {_lactatingCowCount} lactating cows fed by TMR lot {_mixedRationLotId} through the milking parlor to cold storage.";
        _traceabilityStatus = $"Dairy and poultry trace logged: milk lot {_milkLotId} from {_lactatingCowCount} lactating cows, ration {_mixedRationLotId}, egg lot {_eggLotId} from {_layingHenCount} hens using feed lot {_feedLotId}.";
        return $"Collected {milk:0.0} L cow milk and {eggs:0} graded eggs; parlor hygiene {_dairyParlorHygienePct:0}%, bulk tank {_bulkMilkTankC:0.0} degC, bacteria {_milkBacteriaCfuPerMl:0} CFU/mL.";
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
            InputLotId = _wheatLotId,
            OutputLotId = NewLotId("FLOUR"),
        };
        return StartFactoryRun(run, () => ConsumeTrackedStock(ref _wheatKg, wheat, ref _wheatLotId));
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
            InputLotId = _sugarCropLotId,
            OutputLotId = NewLotId("SUGAR"),
        };
        return StartFactoryRun(run, () => ConsumeTrackedStock(ref _sugarCropKg, crop, ref _sugarCropLotId));
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
            InputLotId = _milkLotId,
            OutputLotId = NewLotId("BUTTER"),
        };
        return StartFactoryRun(run, () => ConsumeTrackedStock(ref _milkL, milk, ref _milkLotId));
    }

    public string ExtractVanilla()
    {
        if (_lastPowerAvailability < 0.2)
            return "Vanilla curing room, extractor and polish filter need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        double beans = Math.Min(_vanillaBeansKg, 5.5);
        if (beans < 0.5)
            return "Not enough harvested vanilla beans are available for extraction.";
        if (string.IsNullOrWhiteSpace(_vanillaBeanLotId))
            return "Vanilla extraction cannot run because the vanilla bean lot is missing from the ledger.";

        _vanillaExtractorTemperatureC = 32;
        _vanillaExtractStrengthPct = 0;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Vanilla,
            Name = "Vanilla curing and extraction line",
            StartedMessage = $"Started blanching, conditioning and hot-steeping {beans:0.0} kg vanilla beans into vanilla extract.",
            DurationSeconds = 7.5,
            PowerDemandMW = 1.0,
            PrimaryInput = beans,
            Product = beans * 0.42,
            Waste = beans * 0.08,
            ProcessWaterL = 72,
            CulinarySteamKg = 85,
            CompressedAirNm3 = 10,
            FilterMediaPct = 0.35,
            WearPct = 1.35,
            CalibrationDriftPct = 0.50,
            InputLotId = _vanillaBeanLotId,
            OutputLotId = NewLotId("VANILLA"),
        };
        return StartFactoryRun(run, () => ConsumeTrackedStock(ref _vanillaBeansKg, beans, ref _vanillaBeanLotId));
    }

    public string ReceiveSupplies()
    {
        if (_supplyTruckEnRoute)
        {
            if (_supplyTruckArrived)
                return UnloadSupplyDelivery();

            return $"Supplier truck {_inboundSupplyManifestId} is still en route; ETA {_supplyTruckEtaSeconds:0}s. Supplies cannot enter inventory until dock unloading is complete.";
        }

        return OrderSupplyDelivery();
    }

    public string OrderSupplyDelivery()
    {
        if (_lastPowerAvailability < 0.1)
            return "Purchasing terminal and supplier EDI need reactor bus power.";
        if (_supplyTruckEnRoute)
            return $"Supplier truck {_inboundSupplyManifestId} is already scheduled; ETA {_supplyTruckEtaSeconds:0}s.";
        if (_cashBalance < _supplyOrderCost)
            return $"Cash balance ${_cashBalance:0.00} is below supplier order cost ${_supplyOrderCost:0.00}.";

        _cashBalance -= _supplyOrderCost;
        _inboundSupplyManifestId = NewLotId("PO");
        _supplyTruckEtaSeconds = 42 + _rng.NextDouble() * 18;
        _supplyTruckEnRoute = true;
        _supplyTruckArrived = false;
        _supplyOrderStatus = $"Purchase order {_inboundSupplyManifestId} placed with audited suppliers; refrigerated truck ETA {_supplyTruckEtaSeconds:0}s.";
        _warehouseStatus = _supplyOrderStatus;
        return _supplyOrderStatus;
    }

    public string UnloadSupplyDelivery()
    {
        if (_lastPowerAvailability < 0.1)
            return "Receiving dock, cold room and barcode scales need reactor bus power.";
        if (!_supplyTruckEnRoute)
            return "No supplier truck is scheduled.";
        if (!_supplyTruckArrived)
            return $"Supplier truck {_inboundSupplyManifestId} is still en route; ETA {_supplyTruckEtaSeconds:0}s.";
        if (_forkliftBatteryPct < 8 || _warehousePalletSpacePct < 8)
            return "Unloading needs forklift battery and open pallet space.";

        string manifest = _inboundSupplyManifestId.Replace("PO-", "RCV-");
        ApplySupplyManifest(manifest);
        _forkliftBatteryPct = Math.Max(0, _forkliftBatteryPct - 4.5);
        _warehousePalletSpacePct = Math.Max(0, _warehousePalletSpacePct - 2.0);
        _supplyTruckEnRoute = false;
        _supplyTruckArrived = false;
        _supplyTruckEtaSeconds = 0;
        _supplyOrderStatus = $"Supplier delivery {manifest} unloaded, weighed, temperature-checked and booked into inventory.";
        _warehouseStatus = $"{_supplyOrderStatus} Forklift battery {_forkliftBatteryPct:0}% and {_warehousePalletSpacePct:0}% pallet space free.";
        return _supplyOrderStatus;
    }

    private void ApplySupplyManifest(string manifestId)
    {
        _wheatSeedKg += 28;
        _beetSeedKg += 24;
        _irrigationWaterL += 24000;
        _fertilizerKg += 150;
        _animalFeedKg += 640;
        _forageKg += 380;
        _grainKg += 260;
        _dairyMineralKg += 40;
        _beddingKg += 220;
        _barnLaborHours = Math.Min(60, _barnLaborHours + 12);
        _brineL += 1600;
        _sodaAshKg += 42;
        _phosphateKg += 48;
        _starchKg += 36;
        _paperboardKg += 130;
        _labelStockM += 520;
        _packagingInkL += 14;
        _adhesiveKg += 26;
        _cocoaBeansKg += 90;
        _processWaterL += 9000;
        _culinarySteamKg += 2200;
        _compressedAirNm3 += 720;
        _filterMediaPct = Math.Min(100, _filterMediaPct + 45);
        _forkliftBatteryPct = Math.Min(100, _forkliftBatteryPct + 12);
        _warehousePalletSpacePct = Math.Min(90, _warehousePalletSpacePct + 4);
        _lastSupplyManifestId = manifestId;
        _wheatSeedLotId = NewLotId("SEED-WHT");
        _beetSeedLotId = NewLotId("SEED-BEET");
        _feedLotId = NewLotId("FEED");
        _forageLotId = NewLotId("FORAGE");
        _grainLotId = NewLotId("GRAIN");
        _dairyMineralLotId = NewLotId("DAIRYMIN");
        _beddingLotId = NewLotId("BEDDING");
        _cocoaBeansLotId = NewLotId("COCOABEAN");
        _brineLotId = NewLotId("BRINE");
        _mineralLotId = NewLotId("MINERAL");
        _paperboardLotId = NewLotId("PAPERBOARD");
        _labelStockLotId = NewLotId("LABEL");
        _packagingInkLotId = NewLotId("INK");
        _adhesiveLotId = NewLotId("ADHESIVE");
        _utilityLotId = NewLotId("UTILITY");
        _warehouseStatus = $"Receiving manifest {_lastSupplyManifestId} booked into warehouse; forklift battery {_forkliftBatteryPct:0}% and {_warehousePalletSpacePct:0}% pallet space free.";
        _traceabilityStatus = $"Receiving manifest {_lastSupplyManifestId} logged seed, dairy forage, grain, mineral, bedding, feed, cocoa, brine, leavening feedstocks, packaging feedstocks and utility lots.";
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
            InputLotId = _cocoaBeansLotId,
            OutputLotId = NewLotId("COCOA"),
        };
        return StartFactoryRun(run, () => ConsumeTrackedStock(ref _cocoaBeansKg, beans, ref _cocoaBeansLotId));
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
            InputLotId = _brineLotId,
            OutputLotId = NewLotId("SALT"),
        };
        return StartFactoryRun(run, () => ConsumeTrackedStock(ref _brineL, brine, ref _brineLotId));
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
            InputLotId = _mineralLotId,
            OutputLotId = NewLotId("LEAVEN"),
        };
        return StartFactoryRun(run, () =>
        {
            _sodaAshKg = Math.Max(0, _sodaAshKg - soda);
            _phosphateKg = Math.Max(0, _phosphateKg - phosphate);
            _starchKg = Math.Max(0, _starchKg - starch);
            if (_sodaAshKg <= 0.001 && _phosphateKg <= 0.001 && _starchKg <= 0.001) _mineralLotId = "";
        });
    }

    public string RunPackagingPlant()
    {
        if (_lastPowerAvailability < 0.2)
            return "Carton former, labeler and case coder need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        const double board = 42.0;
        const double labels = 140.0;
        const double ink = 2.4;
        const double adhesive = 6.0;
        if (_paperboardKg < board || _labelStockM < labels || _packagingInkL < ink || _adhesiveKg < adhesive)
            return "Packaging plant needs paperboard, label roll stock, ink and food-grade adhesive.";
        if (string.IsNullOrWhiteSpace(_paperboardLotId) || string.IsNullOrWhiteSpace(_labelStockLotId) || string.IsNullOrWhiteSpace(_packagingInkLotId) || string.IsNullOrWhiteSpace(_adhesiveLotId))
            return "Packaging plant cannot run because a packaging feedstock lot is missing from the ledger.";

        _cartonFormerSpeedCpm = 0;
        _printRegistrationMm = 0.65;
        _gluePotTemperatureC = 28;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Packaging,
            Name = "Carton former, labeler and coder",
            StartedMessage = $"Started converting {board:0} kg paperboard, {labels:0} m labels, {ink:0.0} L ink and {adhesive:0.0} kg adhesive into coded cake cartons.",
            DurationSeconds = 7.5,
            PowerDemandMW = 1.3,
            PrimaryInput = board,
            SecondaryInput = labels,
            TertiaryInput = ink,
            QuaternaryInput = adhesive,
            Product = 160,
            Waste = board * 0.055,
            ProcessWaterL = 18,
            CompressedAirNm3 = 42,
            FilterMediaPct = 0.45,
            WearPct = 1.55,
            CalibrationDriftPct = 0.70,
            InputLotId = $"{_paperboardLotId}/{_labelStockLotId}/{_packagingInkLotId}/{_adhesiveLotId}",
            OutputLotId = NewLotId("PACK"),
        };
        return StartFactoryRun(run, () =>
        {
            ConsumeTrackedStock(ref _paperboardKg, board, ref _paperboardLotId);
            ConsumeTrackedStock(ref _labelStockM, labels, ref _labelStockLotId);
            ConsumeTrackedStock(ref _packagingInkL, ink, ref _packagingInkLotId);
            ConsumeTrackedStock(ref _adhesiveKg, adhesive, ref _adhesiveLotId);
        });
    }

    public string PrepareIcing()
    {
        if (_lastPowerAvailability < 0.2)
            return "Icing kettle, tempering jacket and depositor hopper need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        var recipe = CurrentRecipe;
        var formula = IcingFormula(recipe);
        if (!IcingInputsAvailable(recipe))
            return "Icing kitchen needs released sugar, butter, cold milk, vanilla, cocoa when required, process water, culinary steam, compressed air, filter media and milk QA in spec.";
        if (!IcingInputLotsReady(recipe))
            return "Icing kitchen cannot run because sugar, butter, vanilla, cocoa or milk lot release is incomplete.";

        _icingMixerRpm = 0;
        _icingTemperatureC = 24;
        _icingViscosityPaS = 12.5;
        string cocoaLot = formula.CocoaKg > 0 ? "/" + _cocoaLotId : "";
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Icing,
            Name = "Icing tempering kitchen",
            StartedMessage = $"Started preparing {formula.ProductKg:0.0} kg icing for {recipe.Name}: sugar, butter, cow milk, vanilla{(formula.CocoaKg > 0 ? " and cocoa" : "")} are being weighed, cooked, cooled and tempered.",
            DurationSeconds = 6.8,
            PowerDemandMW = 0.9,
            PrimaryInput = formula.SugarKg,
            SecondaryInput = formula.ButterKg,
            TertiaryInput = formula.MilkL,
            QuaternaryInput = formula.VanillaL,
            Product = formula.ProductKg,
            Waste = formula.ProductKg * 0.018,
            ProcessWaterL = 38,
            CulinarySteamKg = 46,
            CompressedAirNm3 = 12,
            FilterMediaPct = 0.22,
            WearPct = 0.95,
            CalibrationDriftPct = 0.36,
            InputLotId = $"{_sugarLotId}/{_butterLotId}/{_milkLotId}/{_vanillaLotId}{cocoaLot}",
            OutputLotId = NewLotId("ICING"),
        };

        return StartFactoryRun(run, () =>
        {
            ConsumeTrackedStock(ref _sugarKg, formula.SugarKg, ref _sugarLotId);
            ConsumeTrackedStock(ref _butterKg, formula.ButterKg, ref _butterLotId);
            ConsumeTrackedStock(ref _milkL, formula.MilkL, ref _milkLotId);
            ConsumeTrackedStock(ref _vanillaL, formula.VanillaL, ref _vanillaLotId);
            ConsumeTrackedStock(ref _cocoaKg, formula.CocoaKg, ref _cocoaLotId);
        });
    }

    private string StartFactoryRun(IngredientFactoryRun run, Action consumeInputs)
    {
        string missingUtilities = MissingFactoryUtilities(run);
        if (missingUtilities.Length > 0)
            return $"{run.Name} cannot start; missing factory utilities: {missingUtilities}.";
        if (!string.IsNullOrWhiteSpace(_pendingLabLotId))
            return $"{run.Name} cannot start; QA lab must release {_pendingLabProductName} lot {_pendingLabLotId} first.";
        if (string.IsNullOrWhiteSpace(run.InputLotId))
            return $"{run.Name} cannot start; source ingredient lot is missing from the traceability ledger.";

        var equipment = EquipmentFor(run.Kind);
        if (equipment.ConditionPct < 42)
            return $"{run.Name} is locked out for maintenance; equipment condition is {equipment.ConditionPct:0}%.";
        if (equipment.CalibrationPct < 58)
            return $"{run.Name} needs calibration before release; calibration is {equipment.CalibrationPct:0}%.";
        if (!HasWasteHandlingCapacity(run, out var wasteIssue))
            return $"{run.Name} cannot start; {wasteIssue}. Haul byproducts or treat factory effluent first.";

        run.EquipmentConditionAtStart = equipment.ConditionPct;
        run.EquipmentCalibrationAtStart = equipment.CalibrationPct;
        consumeInputs();
        ConsumeFactoryUtilities(run);
        _factoryRun = run;
        _factoryRunQualityPct = Math.Clamp(72 - FactoryEquipmentPenalty(run.Kind), 0, 100);
        _factoryStatus = $"{run.Name} running: {FactoryPhase(run)} at 0% complete, {run.PowerDemandMW:0.0} MW load, input lot {run.InputLotId}, output lot {run.OutputLotId}, condition {equipment.ConditionPct:0}% and calibration {equipment.CalibrationPct:0}%.";
        _traceabilityStatus = $"{run.Name} is converting lot {run.InputLotId} into {run.OutputLotId}.";
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

    public string RunUtilityPlant()
    {
        if (_lastPowerAvailability < 0.18)
            return "Utility plant needs reactor bus power for the RO skid, clean-steam boiler and compressor.";
        if (_utilityPlantActive)
            return $"Utility plant already running: {UtilityPlantPhase()}, {UtilityPlantProgressValue() * 100:0}% complete.";
        if (_irrigationWaterL < 900 || _filterMediaPct < 0.8)
            return "Utility plant needs raw water inventory and filter media before it can make process utilities.";
        if (!UtilityPlantHasStorageRoom())
            return "Utility plant output tanks are full enough; consume process water, steam or compressed air first.";

        _irrigationWaterL = Math.Max(0, _irrigationWaterL - 900);
        _filterMediaPct = Math.Max(0, _filterMediaPct - 0.8);
        _utilityPlantElapsedSeconds = 0;
        _utilityPlantActive = true;
        _processWaterConductivityUsCm = 0.18 + _rng.NextDouble() * 0.12;
        _boilerPressureBar = 2.8;
        _airHeaderPressureBar = 4.2;
        _utilityPlantStatus = "Utility plant running: raw water drawn through RO prefilters, boiler feedwater heating and compressor dryer startup.";
        _traceabilityStatus = $"Utility run started from water manifest {_lastSupplyManifestId} using filter media lot {_utilityLotId}.";
        return "Started utility plant: raw water is being treated into process water, culinary steam and compressed air.";
    }

    private bool UtilityPlantHasStorageRoom() =>
        _processWaterL < ProcessWaterCapacityL - 120
        || _culinarySteamKg < CulinarySteamCapacityKg - 80
        || _compressedAirNm3 < CompressedAirCapacityNm3 - 40;

    private double UtilityPlantProgressValue() =>
        UtilityPlantDurationSeconds <= 0 ? 1 : Math.Clamp(_utilityPlantElapsedSeconds / UtilityPlantDurationSeconds, 0, 1);

    private double UtilityPlantSecondsRemainingValue() =>
        _utilityPlantActive ? Math.Max(0, UtilityPlantDurationSeconds - _utilityPlantElapsedSeconds) : 0;

    private string UtilityPlantPhase()
    {
        if (!_utilityPlantActive) return "";
        double p = UtilityPlantProgressValue();
        return p switch
        {
            < 0.22 => "raw-water prefilter and RO pressurization",
            < 0.48 => "RO permeate polishing",
            < 0.72 => "clean-steam boiler ramp",
            < 0.90 => "compressor dryer and air receiver fill",
            _ => "sample panel release and utility tank transfer",
        };
    }

    private void UpdateUtilityPlant(double seconds, double power)
    {
        if (!_utilityPlantActive)
            return;

        if (power < 0.15)
        {
            _utilityPlantStatus = $"Utility plant paused: reactor bus power is too low during {UtilityPlantPhase()}.";
            return;
        }

        _utilityPlantElapsedSeconds = Math.Min(UtilityPlantDurationSeconds, _utilityPlantElapsedSeconds + seconds * Math.Clamp(power, 0.35, 1.0));
        double p = UtilityPlantProgressValue();
        _processWaterConductivityUsCm = Math.Clamp(0.22 - p * 0.17 + _rng.NextDouble() * 0.015, 0.035, 0.30);
        _boilerPressureBar = Math.Clamp(2.8 + p * 3.4, 2.6, 6.5);
        _airHeaderPressureBar = Math.Clamp(4.2 + p * 2.8, 4.0, 7.2);
        _utilityPlantStatus = $"Utility plant running: {UtilityPlantPhase()}, {p * 100:0}% complete, RO conductivity {_processWaterConductivityUsCm:0.000} uS/cm, boiler {_boilerPressureBar:0.0} bar, air header {_airHeaderPressureBar:0.0} bar.";

        if (_utilityPlantElapsedSeconds < UtilityPlantDurationSeconds)
            return;

        double processWater = Math.Min(680, Math.Max(0, ProcessWaterCapacityL - _processWaterL));
        double culinarySteam = Math.Min(420, Math.Max(0, CulinarySteamCapacityKg - _culinarySteamKg));
        double compressedAir = Math.Min(260, Math.Max(0, CompressedAirCapacityNm3 - _compressedAirNm3));
        _processWaterL += processWater;
        _culinarySteamKg += culinarySteam;
        _compressedAirNm3 += compressedAir;
        _factoryEffluentL = Math.Min(FactoryEffluentCapacityL, _factoryEffluentL + 72);
        _utilityPlantActive = false;
        _utilityPlantElapsedSeconds = 0;
        _utilityPlantStatus = $"Utility plant completed: produced {processWater:0} L process water, {culinarySteam:0} kg culinary steam and {compressedAir:0} Nm3 compressed air; RO reject/blowdown added 72 L effluent.";
        _factoryStatus = _factoryRun is null ? _utilityPlantStatus : _factoryStatus;
        _traceabilityStatus = $"Utility lot {_utilityLotId} converted raw water into food-plant utilities for downstream factory runs.";
    }

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
        _factoryStatus = "Maintenance crew serviced roller mill, sugar house, butter room, vanilla extractor, cocoa line, salt works, leavening blender, packaging plant and icing tempering kitchen.";
        return "Serviced all ingredient factories: lubricated bearings, verified guards, replaced filters, calibrated scales, extraction temperature probes, packaging registration sensors, icing viscosity probes and safety interlocks, and signed the maintenance log.";
    }

    public string HaulFactoryByproducts()
    {
        if (_lastPowerAvailability < 0.12)
            return "Byproduct loading dock, scale and forklift charger need reactor bus power.";

        double load = ByproductStorageLoadKg();
        if (load < 10)
            return "Byproduct bins are already nearly empty.";
        if (_forkliftBatteryPct < 8 || _warehousePalletSpacePct < 5)
            return "Byproduct hauling needs forklift battery and dock pallet space.";

        double feedCredit = Math.Min(120, _branKg * 0.35 + _beetPulpKg * 0.18 + _buttermilkL * 0.08 + _vanillaPomaceKg * 0.04);
        double revenue = Math.Round(load * 0.045, 2);
        _animalFeedKg += feedCredit;
        _cashBalance += revenue;
        _forkliftBatteryPct = Math.Max(0, _forkliftBatteryPct - 5.5);
        _warehousePalletSpacePct = Math.Max(0, _warehousePalletSpacePct - 3.0);
        string detail = $"Hauled {load:0} kg-equivalent byproducts: bran {_branKg:0} kg, beet pulp {_beetPulpKg:0} kg, buttermilk {_buttermilkL:0} L, vanilla pomace {_vanillaPomaceKg:0.0} kg, cocoa shell {_cocoaShellKg:0} kg, brine blowdown {_brineBlowdownL:0} L and leavening dust {_leaveningDustKg:0.0} kg.";
        _branKg = 0;
        _beetPulpKg = 0;
        _buttermilkL = 0;
        _vanillaPomaceKg = 0;
        _cocoaShellKg = 0;
        _brineBlowdownL = 0;
        _leaveningDustKg = 0;
        _wasteHandlingStatus = $"{detail} Sold/repurposed for ${revenue:0.00} and recovered {feedCredit:0.0} kg animal feed equivalent.";
        return _wasteHandlingStatus;
    }

    public string TreatFactoryEffluent()
    {
        if (_lastPowerAvailability < 0.15)
            return "Effluent equalization pumps and dissolved-air flotation need reactor bus power.";

        if (_factoryEffluentL < 25)
            return "Factory effluent tank is already nearly empty.";

        const double compressedAir = 24;
        const double filterMedia = 0.8;
        if (_compressedAirNm3 < compressedAir || _filterMediaPct < filterMedia)
            return "Effluent treatment needs compressed air and filter media.";

        double treated = Math.Min(_factoryEffluentL, 950);
        _factoryEffluentL -= treated;
        _compressedAirNm3 = Math.Max(0, _compressedAirNm3 - compressedAir);
        _filterMediaPct = Math.Max(0, _filterMediaPct - filterMedia);
        double reclaimed = treated * 0.62;
        double sludge = treated * 0.018;
        _irrigationWaterL += reclaimed;
        _wasteKg += sludge;
        _wasteHandlingStatus = $"Treated {treated:0} L factory effluent; reclaimed {reclaimed:0} L utility water and sent {sludge:0.0} kg sludge to waste.";
        return _wasteHandlingStatus;
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
            case IngredientFactoryKind.Vanilla:
                _vanillaExtractorTemperatureC = 32.0 + p * 56.0;
                _vanillaExtractStrengthPct = Math.Clamp(18.0 + p * 77.0, 0, 100);
                if (p > 0.35) _factoryRunQualityPct -= Math.Abs(_vanillaExtractorTemperatureC - 82.0) * 0.10;
                if (p > 0.62) _factoryRunQualityPct -= Math.Max(0, 88.0 - _vanillaExtractStrengthPct) * 0.22;
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
            case IngredientFactoryKind.Packaging:
                _cartonFormerSpeedCpm = p < 0.18 ? 120.0 * p / 0.18 : 118.0 + Math.Sin(p * Math.PI * 5) * 9.0;
                _printRegistrationMm = Math.Max(0.04, 0.62 - p * 0.52 + Math.Sin(p * Math.PI * 3) * 0.015);
                _gluePotTemperatureC = 35.0 + p * 118.0;
                if (p > 0.45) _factoryRunQualityPct -= Math.Abs(_printRegistrationMm - 0.08) * 18.0;
                if (p > 0.55) _factoryRunQualityPct -= Math.Abs(_gluePotTemperatureC - 150.0) * 0.035;
                break;
            case IngredientFactoryKind.Icing:
                _icingMixerRpm = p < 0.16 ? 110.0 * p / 0.16 : 110.0 + Math.Sin(p * Math.PI * 5) * 18.0;
                _icingTemperatureC = p < 0.42 ? 24.0 + p * 92.0 : 62.0 - (p - 0.42) / 0.58 * 39.0;
                _icingViscosityPaS = Math.Clamp(12.5 - p * 5.9 + Math.Sin(p * Math.PI * 4) * 0.35, 5.8, 13.0);
                if (p > 0.45) _factoryRunQualityPct -= Math.Abs(_icingTemperatureC - 45.0) * 0.06;
                if (p > 0.60) _factoryRunQualityPct -= Math.Abs(_icingViscosityPaS - 6.6) * 1.2;
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
            IngredientFactoryKind.Vanilla when p < 0.18 => "bean grading and blanch",
            IngredientFactoryKind.Vanilla when p < 0.42 => "conditioning and chopping",
            IngredientFactoryKind.Vanilla when p < 0.72 => "hot extraction",
            IngredientFactoryKind.Vanilla when p < 0.90 => "polish filtration",
            IngredientFactoryKind.Vanilla => "extract strength sample and tank transfer",
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
            IngredientFactoryKind.Packaging when p < 0.18 => "paperboard unwind and scoring",
            IngredientFactoryKind.Packaging when p < 0.42 => "carton forming",
            IngredientFactoryKind.Packaging when p < 0.64 => "label print registration",
            IngredientFactoryKind.Packaging when p < 0.84 => "glue set and code verification",
            IngredientFactoryKind.Packaging => "case count and QA sample pull",
            IngredientFactoryKind.Icing when p < 0.18 => "micro scale weigh-up",
            IngredientFactoryKind.Icing when p < 0.42 => "sugar syrup cook and butter emulsification",
            IngredientFactoryKind.Icing when p < 0.70 => "tempering jacket cooldown",
            IngredientFactoryKind.Icing when p < 0.88 => "viscosity trim and cocoa blend",
            IngredientFactoryKind.Icing => "hopper transfer and QA sample pull",
            _ => "processing",
        };
    }

    private IngredientFactoryEquipment EquipmentFor(IngredientFactoryKind kind) => _factoryEquipment[kind];

    private static string FactoryName(IngredientFactoryKind kind) => kind switch
    {
        IngredientFactoryKind.Mill => "roller mill",
        IngredientFactoryKind.Sugar => "sugar house",
        IngredientFactoryKind.Butter => "butter room",
        IngredientFactoryKind.Vanilla => "vanilla extraction line",
        IngredientFactoryKind.Cocoa => "cocoa line",
        IngredientFactoryKind.Salt => "salt works",
        IngredientFactoryKind.Leavening => "leavening plant",
        IngredientFactoryKind.Packaging => "packaging plant",
        IngredientFactoryKind.Icing => "icing tempering kitchen",
        _ => "ingredient plant",
    };

    private static string FactoryProductName(IngredientFactoryKind kind) => kind switch
    {
        IngredientFactoryKind.Mill => "cake flour",
        IngredientFactoryKind.Sugar => "sugar",
        IngredientFactoryKind.Butter => "butter",
        IngredientFactoryKind.Vanilla => "vanilla extract",
        IngredientFactoryKind.Cocoa => "cocoa",
        IngredientFactoryKind.Salt => "baking salt",
        IngredientFactoryKind.Leavening => "baking powder",
        IngredientFactoryKind.Packaging => "cake cartons",
        IngredientFactoryKind.Icing => "prepared icing",
        _ => "ingredient",
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

    private void HoldFactoryLotForLab(IngredientFactoryRun run, double output)
    {
        _pendingLabKind = run.Kind;
        _pendingLabLotId = run.OutputLotId;
        _pendingLabProductName = FactoryProductName(run.Kind);
        _pendingLabQualityPct = _factoryRunQualityPct;
        _pendingLabQuantity = output;
        _ingredientLabStatus = $"QA lab hold: {_pendingLabProductName} lot {_pendingLabLotId} awaiting moisture, sieve, micro and label checks.";
    }

    private void RemoveRejectedFactoryLot(IngredientFactoryKind kind, double quantity)
    {
        switch (kind)
        {
            case IngredientFactoryKind.Mill:
                ConsumeTrackedStock(ref _flourKg, quantity, ref _flourLotId);
                break;
            case IngredientFactoryKind.Sugar:
                ConsumeTrackedStock(ref _sugarKg, quantity, ref _sugarLotId);
                break;
            case IngredientFactoryKind.Butter:
                ConsumeTrackedStock(ref _butterKg, quantity, ref _butterLotId);
                break;
            case IngredientFactoryKind.Vanilla:
                ConsumeTrackedStock(ref _vanillaL, quantity, ref _vanillaLotId);
                break;
            case IngredientFactoryKind.Cocoa:
                ConsumeTrackedStock(ref _cocoaKg, quantity, ref _cocoaLotId);
                break;
            case IngredientFactoryKind.Salt:
                ConsumeTrackedStock(ref _saltKg, quantity, ref _saltLotId);
                break;
            case IngredientFactoryKind.Leavening:
                ConsumeTrackedStock(ref _bakingPowderKg, quantity, ref _leaveningLotId);
                break;
            case IngredientFactoryKind.Packaging:
                ConsumeTrackedStock(ref _packagingUnits, quantity, ref _packagingLotId);
                break;
            case IngredientFactoryKind.Icing:
                ConsumeTrackedStock(ref _icingKg, quantity, ref _icingLotId);
                break;
        }
    }

    public string ReleaseIngredientLabLot()
    {
        if (_lastPowerAvailability < 0.15)
            return "QA lab instruments need reactor bus power before releasing an ingredient lot.";
        if (string.IsNullOrWhiteSpace(_pendingLabLotId))
            return "No ingredient lot is waiting in the QA lab.";
        if (!HasFactoryUtilities(12, 0, 4, 0.1))
            return "QA lab release needs process water, compressed instrument air and filter media.";

        _processWaterL = Math.Max(0, _processWaterL - 12);
        _compressedAirNm3 = Math.Max(0, _compressedAirNm3 - 4);
        _filterMediaPct = Math.Max(0, _filterMediaPct - 0.1);

        string lot = _pendingLabLotId;
        string product = _pendingLabProductName;
        double quality = _pendingLabQualityPct;
        if (quality < 62)
        {
            RemoveRejectedFactoryLot(_pendingLabKind, _pendingLabQuantity);
            _wasteKg += _pendingLabQuantity;
            _ingredientLabStatus = $"QA lab rejected {product} lot {lot} at {quality:0}% quality; lot was diverted to waste.";
            _traceabilityStatus = $"Lab rejected factory output lot {lot}; batch ledger cannot use it.";
        }
        else
        {
            _releasedFactoryLots.Add(lot);
            _ingredientLabStatus = $"QA lab released {product} lot {lot} at {quality:0}% quality.";
            _traceabilityStatus = $"Lab released factory output lot {lot} for batching.";
        }

        _pendingLabLotId = "";
        _pendingLabProductName = "";
        _pendingLabQualityPct = 0;
        _pendingLabQuantity = 0;
        return _ingredientLabStatus;
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

    private string RecordFactoryByproducts(IngredientFactoryRun run)
    {
        double effluent = ExpectedEffluentL(run);
        _factoryEffluentL = Math.Min(FactoryEffluentCapacityL, _factoryEffluentL + effluent);

        string detail;
        switch (run.Kind)
        {
            case IngredientFactoryKind.Mill:
                double bran = run.PrimaryInput * 0.22;
                _branKg += bran;
                detail = $"{bran:0.0} kg bran";
                break;
            case IngredientFactoryKind.Sugar:
                double pulp = run.PrimaryInput * 0.34;
                _beetPulpKg += pulp;
                detail = $"{pulp:0.0} kg beet pulp";
                break;
            case IngredientFactoryKind.Butter:
                double buttermilk = run.PrimaryInput * 0.78;
                _buttermilkL += buttermilk;
                detail = $"{buttermilk:0.0} L buttermilk";
                break;
            case IngredientFactoryKind.Vanilla:
                double pomace = run.PrimaryInput * 0.32;
                _vanillaPomaceKg += pomace;
                detail = $"{pomace:0.0} kg vanilla bean pomace";
                break;
            case IngredientFactoryKind.Cocoa:
                double shell = run.PrimaryInput * 0.14;
                _cocoaShellKg += shell;
                detail = $"{shell:0.0} kg cocoa shell";
                break;
            case IngredientFactoryKind.Salt:
                double blowdown = run.PrimaryInput * 0.16;
                _brineBlowdownL += blowdown;
                detail = $"{blowdown:0.0} L brine blowdown";
                break;
            case IngredientFactoryKind.Leavening:
                double dust = run.Product * 0.025;
                _leaveningDustKg += dust;
                detail = $"{dust:0.0} kg leavening dust";
                break;
            case IngredientFactoryKind.Packaging:
                double trim = run.PrimaryInput * 0.07;
                _wasteKg += trim;
                detail = $"{trim:0.0} kg carton trim and label matrix scrap";
                break;
            case IngredientFactoryKind.Icing:
                double kettleRinse = run.Product * 0.05;
                _factoryEffluentL = Math.Min(FactoryEffluentCapacityL, _factoryEffluentL + kettleRinse * 6.0);
                detail = $"{run.Waste:0.0} kg icing smear plus {kettleRinse * 6.0:0} L kettle rinse";
                break;
            default:
                detail = $"{run.Waste:0.0} kg residuals";
                break;
        }

        _wasteHandlingStatus = $"{run.Name} added {detail} and {effluent:0} L effluent. Byproduct bins {ByproductStoragePctValue():0}% full; effluent tank {EffluentTankPctValue():0}% full.";
        return _wasteHandlingStatus;
    }

    private void CompleteFactoryRun(IngredientFactoryRun run)
    {
        double output = run.Product * FactoryYieldFactor(run.Kind);
        double waste = run.Waste + Math.Max(0, run.Product - output);
        string byproductStatus = RecordFactoryByproducts(run);
        switch (run.Kind)
        {
            case IngredientFactoryKind.Mill:
                _flourKg += output;
                _flourLotId = run.OutputLotId;
                _wasteKg += waste;
                _flourExtractionPct = 75.0 + _rng.NextDouble() * 2.0;
                _factoryRunQualityPct = Math.Clamp(96 - Math.Abs(_millRollGapMm - 0.30) * 120 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Roller mill completed: {output:0.0} kg cake flour, {waste:0.0} kg bran/waste, {_millRollGapMm:0.00} mm roll gap, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Sugar:
                _sugarKg += output;
                _sugarLotId = run.OutputLotId;
                _wasteKg += waste;
                _sugarJuiceBrix = 67.0 + _rng.NextDouble() * 3.0;
                _sugarEvaporatorTemperatureC = 103.0 + _rng.NextDouble() * 4.0;
                _factoryRunQualityPct = Math.Clamp(98 - Math.Abs(_sugarJuiceBrix - 68.0) * 1.5 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Sugar house completed: {output:0.0} kg sugar at {_sugarJuiceBrix:0.0} Brix and {_sugarEvaporatorTemperatureC:0} degC, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Butter:
                _butterKg += output;
                _butterLotId = run.OutputLotId;
                _creamSeparatorRpm = 6400 + _rng.NextDouble() * 420;
                _butterFatPct = 81.0 + _rng.NextDouble() * 2.5;
                _wasteKg += waste;
                _factoryRunQualityPct = Math.Clamp(98 - Math.Abs(_butterFatPct - 82.0) * 1.4 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Butter room completed: {output:0.0} kg butter at {_butterFatPct:0.0}% butterfat, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Vanilla:
                _vanillaL += output;
                _vanillaLotId = run.OutputLotId;
                _wasteKg += waste;
                _vanillaExtractorTemperatureC = 80 + _rng.NextDouble() * 7;
                _vanillaExtractStrengthPct = 90 + _rng.NextDouble() * 6;
                _factoryRunQualityPct = Math.Clamp(98 - Math.Abs(_vanillaExtractorTemperatureC - 82.0) * 0.22 - Math.Max(0, 92.0 - _vanillaExtractStrengthPct) * 0.45 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Vanilla extraction completed: {output:0.00} L vanilla extract, strength {_vanillaExtractStrengthPct:0}%, kettle {_vanillaExtractorTemperatureC:0} degC, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Cocoa:
                _cocoaKg += output;
                _cocoaLotId = run.OutputLotId;
                _wasteKg += waste;
                _cocoaRoasterTemperatureC = 130 + _rng.NextDouble() * 12;
                _cocoaGrindMicrons = 68 + _rng.NextDouble() * 18;
                _factoryRunQualityPct = Math.Clamp(98 - Math.Abs(_cocoaGrindMicrons - 75.0) * 0.35 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Cocoa line completed: {output:0.0} kg cocoa at {_cocoaRoasterTemperatureC:0} degC roast and {_cocoaGrindMicrons:0} micron grind, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Salt:
                _saltKg += output;
                _saltLotId = run.OutputLotId;
                _wasteKg += waste;
                _saltCrystallizerTemperatureC = 62 + _rng.NextDouble() * 8;
                _factoryRunQualityPct = Math.Clamp(97 - Math.Abs(_brineSalinityPct - 2.7) * 5.0 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Salt works completed: {output:0.0} kg baking-grade salt from {_brineSalinityPct:0.0}% brine, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Leavening:
                _bakingPowderKg += output;
                _leaveningLotId = run.OutputLotId;
                _wasteKg += waste;
                _leaveningMixerRpm = 72 + _rng.NextDouble() * 36;
                _leaveningHomogeneityPct = 96.5 + _rng.NextDouble() * 2.6;
                _factoryRunQualityPct = Math.Clamp(_leaveningHomogeneityPct - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Leavening plant completed: {output:0.0} kg baking powder at {_leaveningHomogeneityPct:0.0}% homogeneity, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Packaging:
                _packagingUnits += Math.Floor(output);
                _packagingLotId = run.OutputLotId;
                _wasteKg += waste;
                _cartonFormerSpeedCpm = 118 + _rng.NextDouble() * 16;
                _printRegistrationMm = 0.05 + _rng.NextDouble() * 0.08;
                _gluePotTemperatureC = 148 + _rng.NextDouble() * 8;
                _factoryRunQualityPct = Math.Clamp(99 - Math.Abs(_printRegistrationMm - 0.08) * 20 - Math.Abs(_gluePotTemperatureC - 152.0) * 0.08 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Packaging plant completed: {Math.Floor(output):0} coded cartons, registration {_printRegistrationMm:0.00} mm, glue {_gluePotTemperatureC:0} degC, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Icing:
                _icingKg += output;
                _icingLotId = run.OutputLotId;
                _wasteKg += waste;
                _icingMixerRpm = 94 + _rng.NextDouble() * 30;
                _icingTemperatureC = 20 + _rng.NextDouble() * 4;
                _icingViscosityPaS = 6.1 + _rng.NextDouble() * 1.0;
                _factoryRunQualityPct = Math.Clamp(98 - Math.Abs(_icingViscosityPaS - 6.6) * 2.2 - Math.Abs(_icingTemperatureC - 22.0) * 0.25 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Icing kitchen completed: {output:0.0} kg prepared icing at {_icingTemperatureC:0} degC and {_icingViscosityPaS:0.0} Pa-s viscosity, QA {_factoryRunQualityPct:0}%.";
                break;
        }

        HoldFactoryLotForLab(run, output);
        ApplyFactoryWear(run);
        var equipment = EquipmentFor(run.Kind);
        _factoryMaintenanceStatus = BuildFactoryMaintenanceStatus();
        _traceabilityStatus = $"{run.Name} completed trace: source lot {run.InputLotId} -> output lot {run.OutputLotId}; QA lab release required.";
        _factoryStatus += $" {byproductStatus} Equipment now {equipment.ConditionPct:0}% condition, {equipment.CalibrationPct:0}% calibration, {equipment.VibrationMmS:0.0} mm/s vibration. QA lab is holding lot {run.OutputLotId}.";
    }

    public void StartClean()
    {
        _cipSeconds = Math.Max(_cipSeconds, 24);
        if (_stage == CakeBatchStage.Idle) _stageSeconds = 0;
    }

    public string StageBatchKit()
    {
        if (_stage != CakeBatchStage.Idle)
            return "Cannot stage a new kit while a batch is already on the line.";
        if (CipActive)
            return "Cannot stage a batch kit while CIP sanitation is active.";
        if (_lastPowerAvailability < 0.2)
            return "Warehouse pick scales, forklift chargers and line staging need reactor bus power.";
        if (_batchKitStaged)
            return $"Batch kit {_batchKitLotId} is already staged for the line.";
        if (_forkliftBatteryPct < 12)
            return $"Warehouse forklift battery is low ({_forkliftBatteryPct:0}%). Recharge before staging a kit.";
        if (_warehousePalletSpacePct < 18)
            return $"Line staging is congested ({_warehousePalletSpacePct:0}% pallet space free).";

        var missing = MissingIngredients(CurrentRecipe);
        if (missing.Length > 0)
            return "Cannot stage kit. Missing: " + missing;
        if (!RecipeLotsReady(CurrentRecipe))
            return "Cannot stage kit; ingredient lot ledger or lab release is incomplete.";

        _batchKitLotId = NewLotId("KIT");
        _batchKitRecipeKey = CurrentRecipe.Key;
        _batchKitTrace = BuildBatchTrace(CurrentRecipe, _batchKitLotId);
        double icingNeed = IcingNeedKg(CurrentRecipe);
        _batchKitMassKg = BatchIngredientMass(CurrentRecipe) + icingNeed;
        _batchKitPackagingUnits = CurrentRecipe.BatchSize;
        _batchKitIcingKg = icingNeed;
        _batchKitIcingLotId = _icingLotId;
        ConsumeIngredients(CurrentRecipe);
        ConsumeTrackedStock(ref _icingKg, icingNeed, ref _icingLotId);
        ConsumeTrackedStock(ref _packagingUnits, CurrentRecipe.BatchSize, ref _packagingLotId);
        _forkliftBatteryPct = Math.Max(0, _forkliftBatteryPct - Math.Clamp(5.0 + _batchKitMassKg * 0.10, 5.0, 14.0));
        _warehousePalletSpacePct = Math.Max(0, _warehousePalletSpacePct - 8);
        _batchKitStaged = true;
        _warehouseStatus = $"Batch kit {_batchKitLotId} staged at line scales: {_batchKitMassKg:0.0} kg ingredients, {_batchKitIcingKg:0.0} kg prepared icing lot {_batchKitIcingLotId} and {_batchKitPackagingUnits} cartons.";
        _traceabilityStatus = $"Warehouse staged kit {_batchKitLotId}; batch start can only use this picked kit.";
        return _warehouseStatus;
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
        if (!_batchKitStaged)
        {
            message = "Stage a warehouse batch kit before starting the line.";
            return false;
        }
        if (!string.Equals(_batchKitRecipeKey, CurrentRecipe.Key, StringComparison.Ordinal))
        {
            message = $"Staged kit {_batchKitLotId} is for a different recipe.";
            return false;
        }

        _currentBatchLotId = NewLotId("BATCH");
        _currentBatchTrace = $"{_currentBatchLotId}: started from staged warehouse kit {_batchKitLotId}. {_batchKitTrace}";
        _traceabilityStatus = $"Batch trace manifest {_currentBatchLotId} opened from staged kit {_batchKitLotId}.";
        _stage = CakeBatchStage.Scaling;
        _stageReadyForOperator = false;
        _stageSeconds = 0;
        _batchInternalC = 22;
        _mixerSpecificGravity = 1.02;
        _batchQuality = Math.Clamp(86 + _sanitationScore * 0.11 + _rng.NextDouble() * 4, 70, 99);
        _activeBatchMassKg = _batchKitMassKg;
        _batterKg += _batchKitMassKg;
        _warehousePalletSpacePct = Math.Min(100, _warehousePalletSpacePct + 6);
        _batchKitStaged = false;
        _batchKitRecipeKey = "";
        _batchKitLotId = "";
        _batchKitTrace = "No batch kit staged.";
        _batchKitMassKg = 0;
        _batchKitPackagingUnits = 0;
        _batchKitIcingKg = 0;
        _batchKitIcingLotId = "";
        _warehouseStatus = $"Batch {_currentBatchLotId} pulled staged kit into scaling; staging lane clear.";
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
        double utilityDemand = _utilityPlantActive ? UtilityPlantPowerDemandMW : 0;
        double factoryDemand = 2.4 + LineSpeed * 28.0 + (CipActive ? 2.8 : 0) + ingredientFactoryDemand + utilityDemand;
        double demand = farmDemand + factoryDemand;
        bool reactorOnline = reactor.IsGenerating && reactor.ElectricMW > 1 && !reactor.IsMeltdown;
        double power = reactorOnline ? Math.Clamp(reactor.ElectricMW / Math.Max(1, demand), 0, 1) : 0;
        _lastPowerAvailability = power;

        UpdateAnimation(seconds, power);
        UpdateFarm(seconds, power);
        UpdateMilkColdChain(seconds, power);
        UpdateWarehouse(seconds, power);
        UpdateSupplyDelivery(seconds, power);
        UpdateOrders(seconds, power);
        UpdateCleaning(seconds, power);
        UpdateUtilityPlant(seconds, power);
        UpdateFactoryRun(seconds, power);
        UpdateBatch(seconds, power);

        _sanitationScore = Math.Clamp(_sanitationScore - seconds * (0.003 + (_stage == CakeBatchStage.Idle ? 0 : 0.018 * LineSpeed)), 0, 100);

        bool kitMatchesRecipe = _batchKitStaged && string.Equals(_batchKitRecipeKey, recipe.Key, StringComparison.Ordinal);
        var rawMissing = MissingIngredients(recipe);
        var missing = kitMatchesRecipe ? "" : rawMissing;
        var displayedEquipment = _factoryRun is null ? LowestConditionEquipment() : EquipmentFor(_factoryRun.Kind);
        bool labClear = string.IsNullOrWhiteSpace(_pendingLabLotId);
        bool wasteReady = ByproductStoragePctValue() < 92 && EffluentTankPctValue() < 92;
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
            CanStartBatch = _stage == CakeBatchStage.Idle && !CipActive && kitMatchesRecipe && power >= 0.2,
            StageReadyForOperator = _stageReadyForOperator,
            CanAdvanceStage = _stage != CakeBatchStage.Idle && _stageReadyForOperator && power >= 0.2 && StageSafetyGateMet(_stage),
            OperatorPrompt = OperatorPrompt(power, missing),
            MissingIngredients = missing,
            CanHarvest = power >= 0.15 && (_wheatGrowth >= 25 || _beetGrowth >= 25 || _vanillaGrowth >= 25),
            CanCollectDairy = power >= 0.12 && (_dairyReadyL >= 1 || _eggsReady >= 1),
            CanMixDairyRation = power >= 0.15 && _forageKg >= 48 && _grainKg >= 22 && _dairyMineralKg >= 2.2 && _irrigationWaterL >= 38 && _barnLaborHours >= 0.8,
            CanWashDairyParlor = power >= 0.15 && HasFactoryUtilities(180, 60, 12, 0.5) && _barnLaborHours >= 1.4 && (_dairyParlorHygienePct < 96 || _manureKg > 80),
            CanWashPoultryHouse = power >= 0.15 && HasFactoryUtilities(90, 30, 8, 0.35) && _barnLaborHours >= 1.0 && (_henHouseHygienePct < 96 || _poultryManureKg > 80),
            CanMillWheat = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _wheatKg >= 5 && HasFactoryUtilities(20, 0, 38, 0.6),
            CanRefineSugar = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _sugarCropKg >= 10 && HasFactoryUtilities(320, 520, 22, 1.2),
            CanChurnButter = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _milkL > 35 && HasFactoryUtilities(110, 140, 15, 0.8),
            CanExtractVanilla = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _vanillaBeansKg >= 0.5 && HasFactoryUtilities(72, 85, 10, 0.35),
            CanReceiveSupplies = power >= 0.1 && (!_supplyTruckEnRoute || _supplyTruckArrived),
            CanOrderSupplyDelivery = power >= 0.1 && !_supplyTruckEnRoute && _cashBalance >= _supplyOrderCost,
            CanUnloadSupplyDelivery = power >= 0.1 && _supplyTruckEnRoute && _supplyTruckArrived && _forkliftBatteryPct >= 8 && _warehousePalletSpacePct >= 8,
            SupplyTruckEnRoute = _supplyTruckEnRoute,
            SupplyTruckArrived = _supplyTruckArrived,
            SupplyTruckEtaSeconds = _supplyTruckEtaSeconds,
            SupplyOrderCost = _supplyOrderCost,
            InboundSupplyManifestId = _inboundSupplyManifestId,
            SupplyOrderStatus = _supplyOrderStatus,
            CanProcessCocoa = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _cocoaBeansKg >= 5 && HasFactoryUtilities(25, 0, 45, 0.7),
            CanRunSaltWorks = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _brineL >= 80 && HasFactoryUtilities(0, 700, 30, 0.4),
            CanRunLeaveningPlant = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _sodaAshKg >= 3 && _phosphateKg >= 3 && _starchKg >= 2 && HasFactoryUtilities(0, 0, 50, 1.0),
            CanRunPackagingPlant = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _paperboardKg >= 42 && _labelStockM >= 140 && _packagingInkL >= 2.4 && _adhesiveKg >= 6 && HasFactoryUtilities(18, 0, 42, 0.45),
            CanPrepareIcing = CanPrepareIcing(recipe, power, labClear, wasteReady),
            CanReleaseLabLot = power >= 0.15 && _factoryRun is null && !labClear && HasFactoryUtilities(12, 0, 4, 0.1),
            CanStageBatchKit = _stage == CakeBatchStage.Idle && !CipActive && !_batchKitStaged && rawMissing.Length == 0 && power >= 0.2 && _forkliftBatteryPct >= 12 && _warehousePalletSpacePct >= 18,
            CanServiceFactories = power >= 0.2 && _factoryRun is null && NeedsFactoryService() && HasFactoryUtilities(120, 80, 35, 1.5),
            CanHaulByproducts = power >= 0.12 && ByproductStorageLoadKg() > 10 && _forkliftBatteryPct >= 8 && _warehousePalletSpacePct >= 5,
            CanTreatFactoryEffluent = power >= 0.15 && _factoryEffluentL >= 25 && _compressedAirNm3 >= 24 && _filterMediaPct >= 0.8,
            WheatGrowth = _wheatGrowth,
            BeetGrowth = _beetGrowth,
            PastureHealth = _pastureHealth,
            VanillaGrowth = _vanillaGrowth,
            DairyReadyL = _dairyReadyL,
            EggsReady = _eggsReady,
            DairyCowCount = _dairyCowCount,
            LactatingCowCount = _lactatingCowCount,
            LayingHenCount = _layingHenCount,
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
            EggProductionPerHour = _eggProductionPerHour,
            HenHouseHygienePct = _henHouseHygienePct,
            PoultryManureKg = _poultryManureKg,
            EggShellQualityPct = _eggShellQualityPct,
            EggWasherTemperatureC = _eggWasherTemperatureC,
            EggSourceStatus = _eggSourceStatus,
            EggQaStatus = EggQaStatus(),
            ForageKg = _forageKg,
            GrainKg = _grainKg,
            DairyMineralKg = _dairyMineralKg,
            MixedRationKg = _mixedRationKg,
            BeddingKg = _beddingKg,
            BarnLaborHours = _barnLaborHours,
            ManureKg = _manureKg,
            DairyParlorHygienePct = _dairyParlorHygienePct,
            RationEnergyPct = _rationEnergyPct,
            RationProteinPct = _rationProteinPct,
            RationStatus = _rationStatus,
            MixedRationLotId = _mixedRationLotId,
            TraceabilityStatus = TraceabilityStatus(),
            TraceabilityScorePct = TraceabilityScore(),
            LastSupplyManifestId = _lastSupplyManifestId,
            CurrentBatchLotId = _currentBatchLotId,
            CurrentBatchTrace = _currentBatchTrace,
            BatchKitStaged = _batchKitStaged,
            BatchKitLotId = _batchKitLotId,
            BatchKitStatus = _batchKitStaged ? _warehouseStatus : "No staged batch kit.",
            BatchKitMassKg = _batchKitMassKg,
            ForkliftBatteryPct = _forkliftBatteryPct,
            WarehousePalletSpacePct = _warehousePalletSpacePct,
            WarehouseStatus = _warehouseStatus,
            IngredientLabStatus = _ingredientLabStatus,
            PendingLabLotId = _pendingLabLotId,
            PendingLabProductName = _pendingLabProductName,
            PendingLabQualityPct = _pendingLabQualityPct,
            WheatLotId = _wheatLotId,
            SugarCropLotId = _sugarCropLotId,
            VanillaBeanLotId = _vanillaBeanLotId,
            VanillaLotId = _vanillaLotId,
            MilkLotId = _milkLotId,
            EggLotId = _eggLotId,
            FlourLotId = _flourLotId,
            SugarLotId = _sugarLotId,
            ButterLotId = _butterLotId,
            CocoaLotId = _cocoaLotId,
            SaltLotId = _saltLotId,
            LeaveningLotId = _leaveningLotId,
            PackagingLotId = _packagingLotId,
            IcingLotId = _icingLotId,
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
            VanillaBeansKg = _vanillaBeansKg,
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
            PaperboardKg = _paperboardKg,
            LabelStockM = _labelStockM,
            PackagingInkL = _packagingInkL,
            AdhesiveKg = _adhesiveKg,
            IcingKg = _icingKg,
            ResourceStatus = ResourceStatus(power),
            ProcessWaterL = _processWaterL,
            CulinarySteamKg = _culinarySteamKg,
            CompressedAirNm3 = _compressedAirNm3,
            FilterMediaPct = _filterMediaPct,
            FactoryUtilityStatus = FactoryUtilityStatus(),
            CanRunUtilityPlant = power >= 0.18 && !_utilityPlantActive && _irrigationWaterL >= 900 && _filterMediaPct >= 0.8 && UtilityPlantHasStorageRoom(),
            UtilityPlantActive = _utilityPlantActive,
            UtilityPlantPhase = UtilityPlantPhase(),
            UtilityPlantProgress = UtilityPlantProgressValue(),
            UtilityPlantPowerMW = _utilityPlantActive ? UtilityPlantPowerDemandMW : 0,
            UtilityPlantSecondsRemaining = UtilityPlantSecondsRemainingValue(),
            UtilityPlantStatus = _utilityPlantStatus,
            ProcessWaterConductivityUsCm = _processWaterConductivityUsCm,
            BoilerPressureBar = _boilerPressureBar,
            AirHeaderPressureBar = _airHeaderPressureBar,
            FactoryStatus = _factoryStatus,
            FactoryRunActive = _factoryRun is not null,
            ActiveFactoryName = _factoryRun?.Name ?? "",
            ActiveFactoryPhase = _factoryRun is null ? "" : FactoryPhase(_factoryRun),
            FactoryProgress = _factoryRun?.Progress ?? 0,
            FactoryRunPowerMW = _factoryRun?.PowerDemandMW ?? 0,
            FactoryRunSecondsRemaining = _factoryRun?.RemainingSeconds ?? 0,
            FactoryRunQualityPct = _factoryRunQualityPct,
            FactoryMaintenanceStatus = _factoryMaintenanceStatus,
            WasteHandlingStatus = WasteHandlingStatus(),
            BranKg = _branKg,
            BeetPulpKg = _beetPulpKg,
            ButtermilkL = _buttermilkL,
            VanillaPomaceKg = _vanillaPomaceKg,
            CocoaShellKg = _cocoaShellKg,
            BrineBlowdownL = _brineBlowdownL,
            LeaveningDustKg = _leaveningDustKg,
            FactoryEffluentL = _factoryEffluentL,
            ByproductStoragePct = ByproductStoragePctValue(),
            EffluentTankPct = EffluentTankPctValue(),
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
            VanillaConditionPct = EquipmentFor(IngredientFactoryKind.Vanilla).ConditionPct,
            VanillaCalibrationPct = EquipmentFor(IngredientFactoryKind.Vanilla).CalibrationPct,
            CocoaConditionPct = EquipmentFor(IngredientFactoryKind.Cocoa).ConditionPct,
            CocoaCalibrationPct = EquipmentFor(IngredientFactoryKind.Cocoa).CalibrationPct,
            SaltConditionPct = EquipmentFor(IngredientFactoryKind.Salt).ConditionPct,
            SaltCalibrationPct = EquipmentFor(IngredientFactoryKind.Salt).CalibrationPct,
            LeaveningConditionPct = EquipmentFor(IngredientFactoryKind.Leavening).ConditionPct,
            LeaveningCalibrationPct = EquipmentFor(IngredientFactoryKind.Leavening).CalibrationPct,
            PackagingConditionPct = EquipmentFor(IngredientFactoryKind.Packaging).ConditionPct,
            PackagingCalibrationPct = EquipmentFor(IngredientFactoryKind.Packaging).CalibrationPct,
            IcingConditionPct = EquipmentFor(IngredientFactoryKind.Icing).ConditionPct,
            IcingCalibrationPct = EquipmentFor(IngredientFactoryKind.Icing).CalibrationPct,
            MillRollGapMm = _millRollGapMm,
            FlourExtractionPct = _flourExtractionPct,
            SugarJuiceBrix = _sugarJuiceBrix,
            SugarEvaporatorTemperatureC = _sugarEvaporatorTemperatureC,
            CreamSeparatorRpm = _creamSeparatorRpm,
            ButterFatPct = _butterFatPct,
            VanillaExtractorTemperatureC = _vanillaExtractorTemperatureC,
            VanillaExtractStrengthPct = _vanillaExtractStrengthPct,
            CocoaRoasterTemperatureC = _cocoaRoasterTemperatureC,
            CocoaGrindMicrons = _cocoaGrindMicrons,
            BrineSalinityPct = _brineSalinityPct,
            SaltCrystallizerTemperatureC = _saltCrystallizerTemperatureC,
            LeaveningMixerRpm = _leaveningMixerRpm,
            LeaveningHomogeneityPct = _leaveningHomogeneityPct,
            CartonFormerSpeedCpm = _cartonFormerSpeedCpm,
            PrintRegistrationMm = _printRegistrationMm,
            GluePotTemperatureC = _gluePotTemperatureC,
            IcingPrepActive = _factoryRun?.Kind == IngredientFactoryKind.Icing,
            IcingPrepPhase = _factoryRun?.Kind == IngredientFactoryKind.Icing ? FactoryPhase(_factoryRun) : "",
            IcingPrepProgress = _factoryRun?.Kind == IngredientFactoryKind.Icing ? _factoryRun.Progress : 0,
            IcingPrepPowerMW = _factoryRun?.Kind == IngredientFactoryKind.Icing ? _factoryRun.PowerDemandMW : 0,
            IcingPrepSecondsRemaining = _factoryRun?.Kind == IngredientFactoryKind.Icing ? _factoryRun.RemainingSeconds : 0,
            IcingPrepStatus = _factoryRun?.Kind == IngredientFactoryKind.Icing
                ? _factoryStatus
                : $"Icing kitchen idle: {_icingKg:0.0} kg prepared icing in lot {(_icingLotId.Length > 0 ? _icingLotId : "none")}.",
            IcingMixerRpm = _icingMixerRpm,
            IcingTemperatureC = _icingTemperatureC,
            IcingViscosityPaS = _icingViscosityPaS,
            BatterKg = _batterKg,
            CakesBaked = _cakesBaked,
            CakesPacked = _cakesPacked,
            CakesRejected = _cakesRejected,
            FinishedGoodsCakes = _finishedGoodsCakes,
            OrdersFulfilled = _ordersFulfilled,
            CurrentOrderId = _currentOrderId,
            OrderCakesRequired = _orderCakesRequired,
            OrderCakesReady = _finishedGoodsCakes,
            OrderSecondsRemaining = _orderSecondsRemaining,
            OrderReward = _orderReward,
            CashBalance = _cashBalance,
            ReputationPct = _reputationPct,
            OrderStatus = _orderStatus,
            CanDispatchOrder = DispatchReady(power),
            DispatchTruckChargePct = _dispatchTruckChargePct,
            DispatchColdChainC = _dispatchColdChainC,
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
        double growthFactor = Math.Max(0, _bulkMilkTankC - 4.0) * 0.018
            + Math.Max(0, 75 - _sanitationScore) * 0.002
            + Math.Max(0, 82 - _dairyParlorHygienePct) * 0.0009;
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
            _vanillaBeansKg += 5.5 + _rng.NextDouble() * 1.4;
            _vanillaBeanLotId = NewLotId("VANILLABEAN");
            _vanillaGrowth = 9 + _rng.NextDouble() * 6;
        }

        double livestockPower = FarmIntensity * power;
        double simHours = seconds * 0.05 * Math.Max(0.25, FarmIntensity);
        double rationNeed = _dairyCowCount * 1.35 * simHours * livestockPower;
        double barnWaterNeed = _dairyCowCount * 4.20 * simHours * livestockPower;
        double beddingNeed = _dairyCowCount * 0.035 * simHours * livestockPower;
        double laborNeed = _dairyCowCount * 0.004 * simHours * livestockPower;
        double cowInputFactor = SupplyFactor(
            (_mixedRationKg, rationNeed),
            (_irrigationWaterL, barnWaterNeed),
            (_beddingKg, beddingNeed),
            (_barnLaborHours, laborNeed));
        double henFeedNeed = _layingHenCount * 0.026 * simHours * livestockPower;
        double henWaterNeed = _layingHenCount * 0.055 * simHours * livestockPower;
        double henBeddingNeed = _layingHenCount * 0.006 * simHours * livestockPower;
        double henLaborNeed = _layingHenCount * 0.0008 * simHours * livestockPower;
        double henInputFactor = SupplyFactor(
            (_animalFeedKg, henFeedNeed),
            (_irrigationWaterL, henWaterNeed),
            (_beddingKg, henBeddingNeed),
            (_barnLaborHours, henLaborNeed));

        if (cowInputFactor > 0)
        {
            ConsumeTrackedStock(ref _mixedRationKg, rationNeed * cowInputFactor, ref _mixedRationLotId);
            _irrigationWaterL = Math.Max(0, _irrigationWaterL - barnWaterNeed * cowInputFactor);
            _beddingKg = Math.Max(0, _beddingKg - beddingNeed * cowInputFactor);
            _barnLaborHours = Math.Max(0, _barnLaborHours - laborNeed * cowInputFactor);
            _manureKg = Math.Min(1800, _manureKg + _dairyCowCount * 0.42 * simHours * cowInputFactor);
            _dairyParlorHygienePct = Math.Max(0, _dairyParlorHygienePct - seconds * (0.006 + 0.006 * livestockPower) - Math.Max(0, _manureKg - 700) * 0.00018);
        }

        if (henInputFactor > 0)
        {
            _animalFeedKg = Math.Max(0, _animalFeedKg - henFeedNeed * henInputFactor);
            _irrigationWaterL = Math.Max(0, _irrigationWaterL - henWaterNeed * henInputFactor);
            _beddingKg = Math.Max(0, _beddingKg - henBeddingNeed * henInputFactor);
            _barnLaborHours = Math.Max(0, _barnLaborHours - henLaborNeed * henInputFactor);
            _poultryManureKg = Math.Min(900, _poultryManureKg + _layingHenCount * 0.045 * simHours * henInputFactor);
            _henHouseHygienePct = Math.Max(0, _henHouseHygienePct - seconds * (0.004 + 0.004 * livestockPower) - Math.Max(0, _poultryManureKg - 260) * 0.00022);
        }

        double hygieneFactor = Math.Clamp(_dairyParlorHygienePct / 88.0, 0.35, 1.10);
        double henHygieneFactor = Math.Clamp(_henHouseHygienePct / 86.0, 0.30, 1.08);
        double rationFactor = Math.Clamp((_rationEnergyPct * 0.55 + _rationProteinPct * 0.45) / 94.0, 0.45, 1.12);
        double manurePenalty = Math.Max(0, _manureKg - 850) / 32.0;
        double targetComfort = cowInputFactor > 0
            ? Math.Clamp(44 + _pastureHealth * 0.28 + power * 15 + hygieneFactor * 12 + rationFactor * 8 - manurePenalty, 25, 98)
            : 22;
        _cowComfort += (targetComfort - _cowComfort) * Math.Min(1, seconds / 45.0);

        double comfortFactor = Math.Clamp(_cowComfort / 82.0, 0.25, 1.20);
        double pastureFactor = Math.Clamp(0.70 + _pastureHealth / 360.0, 0.60, 1.05);
        _milkProductionLPerHour = _lactatingCowCount * 1.15 * cowInputFactor * livestockPower * comfortFactor * pastureFactor * rationFactor * hygieneFactor;
        _milkParlorThroughputLPerHour = power >= 0.12 ? (610 + 150 * Math.Clamp(power, 0, 1)) * hygieneFactor : 0;
        _eggProductionPerHour = _layingHenCount * 0.035 * henInputFactor * livestockPower * henHygieneFactor;

        _dairyReadyL = Math.Min(140, _dairyReadyL + _milkProductionLPerHour * simHours);
        _eggsReady = Math.Min(420, _eggsReady + _eggProductionPerHour * simHours);
        _milkSourceStatus = cowInputFactor > 0 && livestockPower > 0
            ? $"Milk comes from {_lactatingCowCount} lactating cows; herd consumed {rationNeed * cowInputFactor:0.0} kg TMR ration {_mixedRationLotId}, {barnWaterNeed * cowInputFactor:0} L water and made {_manureKg:0} kg manure."
            : "Milk production stalled: cow herd needs mixed ration, water, bedding, labor, pasture health and powered milking systems.";
        _eggSourceStatus = henInputFactor > 0 && livestockPower > 0
            ? $"Eggs come from {_layingHenCount} laying hens; flock consumed {henFeedNeed * henInputFactor:0.0} kg feed lot {_feedLotId}, {henWaterNeed * henInputFactor:0} L water, bedding and labor, then made {_poultryManureKg:0} kg manure."
            : "Egg production stalled: hens need feed, water, bedding, labor, clean nests and reactor-powered grading.";
        if (cowInputFactor <= 0)
            _pastureHealth = Math.Max(10, _pastureHealth - seconds * 0.035);
        if (henInputFactor <= 0)
            _henHouseHygienePct = Math.Max(0, _henHouseHygienePct - seconds * 0.018);
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

    private void UpdateWarehouse(double seconds, double power)
    {
        if (power >= 0.1)
            _forkliftBatteryPct = Math.Min(100, _forkliftBatteryPct + seconds * 0.08 * power);
        if (!_batchKitStaged)
            _warehousePalletSpacePct += (70 - _warehousePalletSpacePct) * Math.Min(1, seconds / 90.0);
    }

    private void UpdateSupplyDelivery(double seconds, double power)
    {
        if (!_supplyTruckEnRoute || _supplyTruckArrived) return;

        double travelFactor = power >= 0.05 ? 1.0 : 0.35;
        _supplyTruckEtaSeconds = Math.Max(0, _supplyTruckEtaSeconds - seconds * travelFactor);
        if (_supplyTruckEtaSeconds <= 0)
        {
            _supplyTruckArrived = true;
            _supplyOrderStatus = $"Supplier truck {_inboundSupplyManifestId} is at the receiving dock; unload it before ingredients enter inventory.";
            _warehouseStatus = _supplyOrderStatus;
        }
        else
        {
            _supplyOrderStatus = $"Supplier truck {_inboundSupplyManifestId} en route with audited finite supplies; ETA {_supplyTruckEtaSeconds:0}s.";
        }
    }

    private void UpdateOrders(double seconds, double power)
    {
        _orderSecondsRemaining -= seconds;
        if (power >= 0.12)
        {
            _dispatchTruckChargePct = Math.Min(100, _dispatchTruckChargePct + seconds * 0.06 * power);
            _dispatchColdChainC += (4.2 - _dispatchColdChainC) * Math.Min(1, seconds / 24.0);
        }
        else
        {
            _dispatchColdChainC += (13.5 - _dispatchColdChainC) * Math.Min(1, seconds / 90.0);
        }

        if (_orderSecondsRemaining < -120)
        {
            _reputationPct = Math.Max(0, _reputationPct - seconds * 0.006);
        }

        if (_finishedGoodsCakes >= _orderCakesRequired)
        {
            _orderStatus = _orderSecondsRemaining >= 0
                ? $"Order {_currentOrderId} ready to dispatch: {_finishedGoodsCakes}/{_orderCakesRequired} cakes, {_orderSecondsRemaining:0}s due."
                : $"Order {_currentOrderId} late but ready: {_finishedGoodsCakes}/{_orderCakesRequired} cakes.";
        }
        else
        {
            _orderStatus = _orderSecondsRemaining >= 0
                ? $"Order {_currentOrderId}: {_finishedGoodsCakes}/{_orderCakesRequired} cakes ready, {_orderSecondsRemaining:0}s due."
                : $"Order {_currentOrderId} late: {_finishedGoodsCakes}/{_orderCakesRequired} cakes ready.";
        }
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
        _finishedGoodsCakes += packed;
        _wasteKg += rejected * 0.42;
        _batterKg = Math.Max(0, _batterKg - (_activeBatchMassKg > 0 ? _activeBatchMassKg : BatchIngredientMass(recipe) + IcingNeedKg(recipe)));
        _activeBatchMassKg = 0;
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
        {
            if (_batchKitStaged && string.Equals(_batchKitRecipeKey, CurrentRecipe.Key, StringComparison.Ordinal))
                return $"Warehouse kit {_batchKitLotId} is staged. Start the batch when ready.";
            if (_batchKitStaged)
                return $"Warehouse kit {_batchKitLotId} is staged for another recipe. Select its recipe before starting.";
            return missing.Length == 0
                ? "Manual mode: harvest, collect, run factories, release QA lots, stage a warehouse kit, then start a batch."
                : "Manual mode: prep ingredients before batching. Missing: " + missing;
        }

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
        if (_forageKg < 48) low.Add("dairy forage");
        if (_grainKg < 22) low.Add("dairy grain");
        if (_dairyMineralKg < 2.2) low.Add("dairy mineral");
        if (_mixedRationKg < 20) low.Add("mixed dairy ration");
        if (_beddingKg < 10) low.Add("bedding");
        if (_barnLaborHours < 2) low.Add("barn labor");
        if (_manureKg > 1300) low.Add("manure storage");
        if (_dairyParlorHygienePct < 55) low.Add("dairy parlor hygiene");
        if (_packagingUnits < CurrentRecipe.BatchSize) low.Add("coded cartons");
        if (_paperboardKg < 42) low.Add("paperboard");
        if (_labelStockM < 140) low.Add("label stock");
        if (_packagingInkL < 2.4) low.Add("packaging ink");
        if (_adhesiveKg < 6) low.Add("food-grade adhesive");
        if (_icingKg < IcingNeedKg(CurrentRecipe)) low.Add("prepared icing");
        if (_vanillaBeansKg < 0.5 && _vanillaL < CurrentRecipe.VanillaL * CurrentRecipe.BatchSize) low.Add("vanilla beans");
        if (_cocoaBeansKg < 5 && _cocoaKg < CurrentRecipe.CocoaKg * CurrentRecipe.BatchSize) low.Add("cocoa beans");
        if (_brineL < 80 && _saltKg < CurrentRecipe.SaltKg * CurrentRecipe.BatchSize) low.Add("brine");
        if ((_sodaAshKg < 3 || _phosphateKg < 3 || _starchKg < 2)
            && _bakingPowderKg < CurrentRecipe.BakingPowderKg * CurrentRecipe.BatchSize)
            low.Add("leavening feedstocks");
        if (_processWaterL < 200) low.Add("process water");
        if (_culinarySteamKg < 260) low.Add("culinary steam");
        if (_compressedAirNm3 < 60) low.Add("compressed air");
        if (_filterMediaPct < 3) low.Add("filter media");
        if (_forkliftBatteryPct < 12) low.Add("forklift battery");
        if (_warehousePalletSpacePct < 18) low.Add("staging pallet space");
        if (ByproductStoragePctValue() > 85) low.Add("byproduct storage");
        if (EffluentTankPctValue() > 85) low.Add("factory effluent tank");
        if (_dispatchTruckChargePct < 18) low.Add("dispatch truck charge");
        if (_dispatchColdChainC > 8.0) low.Add("dispatch cold chain");
        return low.Count == 0 ? "Inputs stocked" : "Low: " + string.Join(", ", low);
    }

    private double TraceabilityScore()
    {
        int total = 0;
        int good = 0;
        void Count(double quantity, string lotId)
        {
            if (quantity <= 0.001) return;
            total++;
            if (!string.IsNullOrWhiteSpace(lotId)) good++;
        }

        Count(_wheatKg, _wheatLotId);
        Count(_sugarCropKg, _sugarCropLotId);
        Count(_wheatSeedKg, _wheatSeedLotId);
        Count(_beetSeedKg, _beetSeedLotId);
        Count(_animalFeedKg, _feedLotId);
        Count(_forageKg, _forageLotId);
        Count(_grainKg, _grainLotId);
        Count(_dairyMineralKg, _dairyMineralLotId);
        Count(_mixedRationKg, _mixedRationLotId);
        Count(_beddingKg, _beddingLotId);
        Count(_vanillaBeansKg, _vanillaBeanLotId);
        Count(_vanillaL, _vanillaLotId);
        Count(_milkL, _milkLotId);
        Count(_eggs, _eggLotId);
        Count(_flourKg, _flourLotId);
        Count(_sugarKg, _sugarLotId);
        Count(_butterKg, _butterLotId);
        Count(_cocoaBeansKg, _cocoaBeansLotId);
        Count(_cocoaKg, _cocoaLotId);
        Count(_brineL, _brineLotId);
        Count(_saltKg, _saltLotId);
        Count(_sodaAshKg + _phosphateKg + _starchKg, _mineralLotId);
        Count(_bakingPowderKg, _leaveningLotId);
        Count(_paperboardKg, _paperboardLotId);
        Count(_labelStockM, _labelStockLotId);
        Count(_packagingInkL, _packagingInkLotId);
        Count(_adhesiveKg, _adhesiveLotId);
        Count(_packagingUnits, _packagingLotId);
        Count(_icingKg, _icingLotId);
        Count(_processWaterL + _culinarySteamKg + _compressedAirNm3 + _filterMediaPct, _utilityLotId);
        return total == 0 ? 100 : good * 100.0 / total;
    }

    private string TraceabilityStatus()
    {
        var missing = new List<string>();
        if (!HasLot(_flourKg, _flourLotId)) missing.Add("flour");
        if (!HasLot(_sugarKg, _sugarLotId)) missing.Add("sugar");
        if (!HasLot(_eggs, _eggLotId)) missing.Add("eggs");
        if (!HasLot(_milkL, _milkLotId)) missing.Add("milk");
        if (!HasLot(_butterKg, _butterLotId)) missing.Add("butter");
        if (!HasLot(_vanillaL, _vanillaLotId)) missing.Add("vanilla");
        if (!HasLot(_bakingPowderKg, _leaveningLotId)) missing.Add("baking powder");
        if (!HasLot(_saltKg, _saltLotId)) missing.Add("salt");
        if (!HasLot(_cocoaKg, _cocoaLotId)) missing.Add("cocoa");
        if (!HasLot(_packagingUnits, _packagingLotId)) missing.Add("packaging");
        if (!HasLot(_icingKg, _icingLotId)) missing.Add("prepared icing");
        if (missing.Count > 0) return "Traceability hold: missing lot data for " + string.Join(", ", missing);
        return _traceabilityStatus;
    }

    private bool MilkQaInSpec() =>
        _milkL <= 0
        || (_bulkMilkTankC <= 7.0
            && _milkBacteriaCfuPerMl <= 100000
            && _milkSomaticCellCountKPerMl <= 400
            && _milkFatPct >= 3.0
            && _milkProteinPct >= 2.9
            && _dairyParlorHygienePct >= 45);

    private string MilkQaStatus()
    {
        var issues = new List<string>();
        if (_bulkMilkTankC > 7.0) issues.Add("bulk tank warm");
        if (_milkBacteriaCfuPerMl > 100000) issues.Add("bacteria high");
        if (_milkSomaticCellCountKPerMl > 400) issues.Add("somatic cells high");
        if (_milkFatPct < 3.0) issues.Add("low fat");
        if (_milkProteinPct < 2.9) issues.Add("low protein");
        if (_dairyParlorHygienePct < 45) issues.Add("parlor hygiene low");
        return issues.Count == 0 ? "Milk QA in spec" : "Milk QA hold: " + string.Join(", ", issues);
    }

    private bool EggQaInSpec() =>
        _eggs <= 0
        || (_eggShellQualityPct >= 78
            && _henHouseHygienePct >= 45
            && _eggWasherTemperatureC >= 32
            && _eggWasherTemperatureC <= 49);

    private string EggQaStatus()
    {
        var issues = new List<string>();
        if (_eggShellQualityPct < 78) issues.Add("shell quality low");
        if (_henHouseHygienePct < 45) issues.Add("nest hygiene low");
        if (_eggWasherTemperatureC < 32 || _eggWasherTemperatureC > 49) issues.Add("washer temperature out of range");
        return issues.Count == 0 ? "Egg QA in spec" : "Egg QA hold: " + string.Join(", ", issues);
    }

    private string FactoryUtilityStatus()
    {
        if (_utilityPlantActive)
            return _utilityPlantStatus;

        var low = new List<string>();
        if (_processWaterL < 200) low.Add("process water");
        if (_culinarySteamKg < 260) low.Add("culinary steam");
        if (_compressedAirNm3 < 60) low.Add("compressed air");
        if (_filterMediaPct < 3) low.Add("filter media");
        if (ByproductStoragePctValue() > 85) low.Add("byproduct bins");
        if (EffluentTankPctValue() > 85) low.Add("effluent tank");
        return low.Count == 0 ? _utilityPlantStatus : "Low utilities: " + string.Join(", ", low) + ". Run the utility plant or order supplies.";
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
        ConsumeTrackedStock(ref _flourKg, r.FlourKg * n, ref _flourLotId);
        ConsumeTrackedStock(ref _sugarKg, r.SugarKg * n, ref _sugarLotId);
        ConsumeTrackedStock(ref _eggs, r.EggCount * n, ref _eggLotId);
        ConsumeTrackedStock(ref _butterKg, r.ButterKg * n, ref _butterLotId);
        ConsumeTrackedStock(ref _milkL, r.MilkL * n, ref _milkLotId);
        ConsumeTrackedStock(ref _bakingPowderKg, r.BakingPowderKg * n, ref _leaveningLotId);
        ConsumeTrackedStock(ref _saltKg, r.SaltKg * n, ref _saltLotId);
        ConsumeTrackedStock(ref _vanillaL, r.VanillaL * n, ref _vanillaLotId);
        ConsumeTrackedStock(ref _cocoaKg, r.CocoaKg * n, ref _cocoaLotId);
    }

    private string MissingIngredients(CakeRecipe r)
    {
        var missing = new List<string>();
        double n = r.BatchSize;
        if (_flourKg < r.FlourKg * n) missing.Add("flour");
        if (_sugarKg < r.SugarKg * n) missing.Add("sugar");
        if (_eggs < r.EggCount * n) missing.Add("eggs");
        else if (r.EggCount > 0 && !EggQaInSpec()) missing.Add("egg QA");
        if (_butterKg < r.ButterKg * n) missing.Add("butter");
        if (_milkL < r.MilkL * n) missing.Add("milk");
        else if (r.MilkL > 0 && !MilkQaInSpec()) missing.Add("milk QA");
        if (_bakingPowderKg < r.BakingPowderKg * n) missing.Add("baking powder");
        if (_saltKg < r.SaltKg * n) missing.Add("salt");
        if (_vanillaL < r.VanillaL * n) missing.Add("vanilla");
        if (_cocoaKg < r.CocoaKg * n) missing.Add("cocoa");
        if (_icingKg < IcingNeedKg(r)) missing.Add("prepared icing");
        if (_packagingUnits < n) missing.Add("coded cartons");
        if (!RecipeLotDataPresent(r)) missing.Add("lot trace");
        else if (!RecipeFactoryLotsReleased(r)) missing.Add("lab release");
        return string.Join(", ", missing);
    }

    private double BatchIngredientMass(CakeRecipe r)
    {
        double n = r.BatchSize;
        return n * (r.FlourKg + r.SugarKg + r.ButterKg + r.MilkL + r.BakingPowderKg + r.SaltKg + r.VanillaL + r.CocoaKg + r.EggCount * 0.052);
    }
}
