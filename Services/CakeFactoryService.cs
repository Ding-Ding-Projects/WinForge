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
    public bool CanHarvestFeedCrops { get; init; }
    public bool CanHarvestCocoa { get; init; }
    public bool CanCollectDairy { get; init; }
    public bool CanPasteurizeMilk { get; init; }
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
    public bool CanRunStarchPlant { get; init; }
    public bool CanRunLeaveningPlant { get; init; }
    public bool CanRunPackagingPlant { get; init; }
    public bool CanPrepareIcing { get; init; }
    public bool CanRunFeedMill { get; init; }
    public bool CanRunCompostPlant { get; init; }
    public bool CanRunBeddingPlant { get; init; }
    public bool CanRunMineralPlant { get; init; }
    public bool CanReleaseLabLot { get; init; }
    public bool CanStageBatchKit { get; init; }
    public double WheatGrowth { get; init; }
    public double BeetGrowth { get; init; }
    public double PastureHealth { get; init; }
    public double VanillaGrowth { get; init; }
    public double CocoaGrowth { get; init; }
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
    public string FeedCropStatus { get; init; } = "";
    public string CocoaGreenhouseStatus { get; init; } = "";
    public string ForageLotId { get; init; } = "";
    public string GrainLotId { get; init; } = "";
    public string MixedRationLotId { get; init; } = "";
    public string DairyMineralLotId { get; init; } = "";
    public string BeddingLotId { get; init; } = "";
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
    public string RawMilkLotId { get; init; } = "";
    public string MilkLotId { get; init; } = "";
    public string EggLotId { get; init; } = "";
    public string FlourLotId { get; init; } = "";
    public string SugarLotId { get; init; } = "";
    public string ButterLotId { get; init; } = "";
    public string CocoaBeansLotId { get; init; } = "";
    public string CocoaLotId { get; init; } = "";
    public string BrineLotId { get; init; } = "";
    public string SaltLotId { get; init; } = "";
    public string StarchLotId { get; init; } = "";
    public string LeaveningLotId { get; init; } = "";
    public string PackagingLotId { get; init; } = "";
    public string IcingLotId { get; init; } = "";
    public string FeedLotId { get; init; } = "";
    public string FertilizerLotId { get; init; } = "";
    public string StrawLotId { get; init; } = "";
    public string LimestoneLotId { get; init; } = "";
    public string TraceMineralLotId { get; init; } = "";
    public string SodaAshLotId { get; init; } = "";
    public string PhosphateLotId { get; init; } = "";
    public double WheatKg { get; init; }
    public double WheatMoisturePct { get; init; }
    public double WheatForeignMaterialPct { get; init; }
    public double WheatProteinPct { get; init; }
    public double SugarCropKg { get; init; }
    public double SugarCropSugarPct { get; init; }
    public double SugarCropSoilTarePct { get; init; }
    public double FlourKg { get; init; }
    public double SugarKg { get; init; }
    public double Eggs { get; init; }
    public double RawMilkL { get; init; }
    public double MilkL { get; init; }
    public double ButterKg { get; init; }
    public double BakingPowderKg { get; init; }
    public double SaltKg { get; init; }
    public double VanillaL { get; init; }
    public double VanillaBeansKg { get; init; }
    public double CocoaBeansKg { get; init; }
    public double CocoaKg { get; init; }
    public double CocoaBeanMoisturePct { get; init; }
    public double CocoaFermentationPct { get; init; }
    public double BrineL { get; init; }
    public double SodaAshKg { get; init; }
    public double PhosphateKg { get; init; }
    public double SodaAshAssayPct { get; init; }
    public double PhosphateAcidValuePct { get; init; }
    public double StarchKg { get; init; }
    public double StrawKg { get; init; }
    public double ForageMoisturePct { get; init; }
    public double FeedGrainMoisturePct { get; init; }
    public double LimestoneKg { get; init; }
    public double TraceMineralKg { get; init; }
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
    public double MolassesKg { get; init; }
    public double SkimMilkL { get; init; }
    public double ButtermilkL { get; init; }
    public double VanillaPomaceKg { get; init; }
    public double CocoaShellKg { get; init; }
    public double CocoaButterKg { get; init; }
    public double BrineBlowdownL { get; init; }
    public double LeaveningDustKg { get; init; }
    public double PackagingTrimKg { get; init; }
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
    public double MilkConditionPct { get; init; }
    public double MilkCalibrationPct { get; init; }
    public double VanillaConditionPct { get; init; }
    public double VanillaCalibrationPct { get; init; }
    public double CocoaConditionPct { get; init; }
    public double CocoaCalibrationPct { get; init; }
    public double SaltConditionPct { get; init; }
    public double SaltCalibrationPct { get; init; }
    public double StarchConditionPct { get; init; }
    public double StarchCalibrationPct { get; init; }
    public double LeaveningConditionPct { get; init; }
    public double LeaveningCalibrationPct { get; init; }
    public double PackagingConditionPct { get; init; }
    public double PackagingCalibrationPct { get; init; }
    public double IcingConditionPct { get; init; }
    public double IcingCalibrationPct { get; init; }
    public double FeedConditionPct { get; init; }
    public double FeedCalibrationPct { get; init; }
    public double MillRollGapMm { get; init; }
    public double FlourExtractionPct { get; init; }
    public double WheatTemperMoisturePct { get; init; }
    public double MillSifterLoadPct { get; init; }
    public double FlourMoisturePct { get; init; }
    public double FlourAshPct { get; init; }
    public double FlourProteinPct { get; init; }
    public double SugarJuiceBrix { get; init; }
    public double SugarEvaporatorTemperatureC { get; init; }
    public double SugarJuicePurityPct { get; init; }
    public double SugarLimePh { get; init; }
    public double SugarPanVacuumKPa { get; init; }
    public double SugarCentrifugeRpm { get; init; }
    public double SugarMoisturePct { get; init; }
    public double SugarColorIcumsa { get; init; }
    public double SugarPolarizationPct { get; init; }
    public double MilkPasteurizerTemperatureC { get; init; }
    public double MilkHomogenizerPressureBar { get; init; }
    public double MilkPasteurizationHoldSeconds { get; init; }
    public double MilkMicroLogReduction { get; init; }
    public double CreamSeparatorRpm { get; init; }
    public double CreamYieldL { get; init; }
    public double CreamFatPct { get; init; }
    public double CreamPasteurizerTemperatureC { get; init; }
    public double CreamPasteurizationHoldSeconds { get; init; }
    public double ButterChurnTemperatureC { get; init; }
    public double ButterFatPct { get; init; }
    public double ButterMoisturePct { get; init; }
    public double ButterSaltPct { get; init; }
    public double ButterWorkingPressureKPa { get; init; }
    public double VanillaExtractorTemperatureC { get; init; }
    public double VanillaExtractStrengthPct { get; init; }
    public double CocoaRoasterTemperatureC { get; init; }
    public double CocoaRoasterAirflowM3Min { get; init; }
    public double CocoaRoastDevelopmentPct { get; init; }
    public double CocoaWinnowerEfficiencyPct { get; init; }
    public double CocoaNibYieldPct { get; init; }
    public double CocoaPressPressureBar { get; init; }
    public double CocoaGrindMicrons { get; init; }
    public double CocoaPowderFatPct { get; init; }
    public double CocoaPowderMoisturePct { get; init; }
    public double BrineSalinityPct { get; init; }
    public double BrineHardnessPpm { get; init; }
    public double BrineTurbidityNtu { get; init; }
    public double BrineClarifierTurbidityNtu { get; init; }
    public double SaltEvaporatorVacuumKPa { get; init; }
    public double SaltCrystallizerTemperatureC { get; init; }
    public double SaltCentrifugeRpm { get; init; }
    public double SaltDryerTemperatureC { get; init; }
    public double SaltMoisturePct { get; init; }
    public double SaltPurityPct { get; init; }
    public double SaltScreenPassingPct { get; init; }
    public double StarchSlurryBrix { get; init; }
    public double StarchDryerTemperatureC { get; init; }
    public double StarchMoisturePct { get; init; }
    public double LeaveningMixerRpm { get; init; }
    public double LeaveningHomogeneityPct { get; init; }
    public double LeaveningBlendMoisturePct { get; init; }
    public double LeaveningSifterLoadPct { get; init; }
    public double LeaveningDustCollectorPressurePa { get; init; }
    public double CartonFormerSpeedCpm { get; init; }
    public double CartonBoardCaliperMm { get; init; }
    public double CartonBoardMoisturePct { get; init; }
    public double CartonDieCutWastePct { get; init; }
    public double LabelWebTensionN { get; init; }
    public double PrintRegistrationMm { get; init; }
    public double GluePotTemperatureC { get; init; }
    public double GlueBeadGPerCarton { get; init; }
    public double CaseCodeReadRatePct { get; init; }
    public bool IcingPrepActive { get; init; }
    public string IcingPrepPhase { get; init; } = "";
    public double IcingPrepProgress { get; init; }
    public double IcingPrepPowerMW { get; init; }
    public double IcingPrepSecondsRemaining { get; init; }
    public string IcingPrepStatus { get; init; } = "";
    public double IcingMixerRpm { get; init; }
    public double IcingTemperatureC { get; init; }
    public double IcingViscosityPaS { get; init; }
    public double FeedMillHammerRpm { get; init; }
    public double FeedPelletTemperatureC { get; init; }
    public double FeedMoisturePct { get; init; }
    public double FertilizerConditionPct { get; init; }
    public double FertilizerCalibrationPct { get; init; }
    public double CompostTemperatureC { get; init; }
    public double CompostMoisturePct { get; init; }
    public double CompostAerationPct { get; init; }
    public double BeddingConditionPct { get; init; }
    public double BeddingCalibrationPct { get; init; }
    public double BeddingChopperRpm { get; init; }
    public double BeddingMoisturePct { get; init; }
    public double BeddingDustPct { get; init; }
    public double MineralConditionPct { get; init; }
    public double MineralCalibrationPct { get; init; }
    public double MineralMixerRpm { get; init; }
    public double MineralHomogeneityPct { get; init; }
    public double MineralMetalPpm { get; init; }
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
        Milk,
        Butter,
        Vanilla,
        Cocoa,
        Salt,
        Starch,
        Leavening,
        Packaging,
        Icing,
        Feed,
        Fertilizer,
        Bedding,
        MineralPremix,
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
    private double _cocoaGrowth = 52;
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
    private string _feedCropStatus = "Feed crop header idle: pasture and feed-grain lots can be harvested for the TMR mixer.";
    private string _cocoaGreenhouseStatus = "Cocoa greenhouse idle: cacao pods are growing under irrigation, fertilizer and shade-house controls.";

    private double _wheatKg = 260;
    private double _sugarCropKg = 380;
    private double _flourKg = 120;
    private double _sugarKg = 92;
    private double _eggs = 240;
    private double _rawMilkL = 90;
    private double _milkL = 140;
    private double _butterKg = 28;
    private double _bakingPowderKg = 14;
    private double _saltKg = 18;
    private double _vanillaL = 2.8;
    private double _vanillaBeansKg = 9.5;
    private double _cocoaBeansKg = 60;
    private double _cocoaKg = 20;
    private double _cocoaBeanMoisturePct = 6.8;
    private double _cocoaFermentationPct = 91.0;
    private double _brineL = 900;
    private double _sodaAshKg = 28;
    private double _phosphateKg = 30;
    private double _starchKg = 24;
    private double _strawKg = 170;
    private double _forageMoisturePct = 13.8;
    private double _feedGrainMoisturePct = 12.4;
    private double _wheatMoisturePct = 12.7;
    private double _wheatForeignMaterialPct = 0.8;
    private double _wheatProteinPct = 8.4;
    private double _sugarCropSugarPct = 16.8;
    private double _sugarCropSoilTarePct = 2.4;
    private double _limestoneKg = 46;
    private double _traceMineralKg = 22;
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
    private string _rawMilkLotId = "RAWMILK-OPENING";
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
    private string _starchLotId = "STARCH-OPENING";
    private string _fertilizerLotId = "FERT-OPENING";
    private string _strawLotId = "STRAW-OPENING";
    private string _limestoneLotId = "LIMESTONE-OPENING";
    private string _traceMineralLotId = "TRACE-MIN-OPENING";
    private string _sodaAshLotId = "SODA-OPENING";
    private string _phosphateLotId = "PHOSPHATE-OPENING";
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
        "MILK-OPENING",
        "BUTTER-OPENING",
        "VANILLA-OPENING",
        "COCOA-OPENING",
        "SALT-OPENING",
        "STARCH-OPENING",
        "LEAVEN-OPENING",
        "PACK-OPENING",
        "ICING-OPENING",
        "FEED-OPENING",
        "FERT-OPENING",
        "BEDDING-OPENING",
        "DAIRYMIN-OPENING",
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
        [IngredientFactoryKind.Milk] = new(94, 96, 34, 1.2),
        [IngredientFactoryKind.Butter] = new(95, 97, 34, 1.4),
        [IngredientFactoryKind.Vanilla] = new(92, 95, 35, 1.5),
        [IngredientFactoryKind.Cocoa] = new(90, 95, 41, 2.1),
        [IngredientFactoryKind.Salt] = new(92, 93, 38, 1.9),
        [IngredientFactoryKind.Starch] = new(92, 95, 37, 1.6),
        [IngredientFactoryKind.Leavening] = new(94, 96, 33, 1.3),
        [IngredientFactoryKind.Packaging] = new(91, 94, 37, 1.7),
        [IngredientFactoryKind.Icing] = new(92, 95, 34, 1.5),
        [IngredientFactoryKind.Feed] = new(93, 96, 35, 1.6),
        [IngredientFactoryKind.Fertilizer] = new(90, 94, 38, 2.2),
        [IngredientFactoryKind.Bedding] = new(92, 95, 36, 1.5),
        [IngredientFactoryKind.MineralPremix] = new(94, 96, 34, 1.4),
    };
    private double _millRollGapMm = 0.32;
    private double _flourExtractionPct = 76;
    private double _wheatTemperMoisturePct = 13.0;
    private double _millSifterLoadPct = 0;
    private double _flourMoisturePct = 13.1;
    private double _flourAshPct = 0.42;
    private double _flourProteinPct = 8.1;
    private double _sugarJuiceBrix = 0;
    private double _sugarEvaporatorTemperatureC = 24;
    private double _sugarJuicePurityPct = 88.0;
    private double _sugarLimePh = 8.3;
    private double _sugarPanVacuumKPa = 0;
    private double _sugarCentrifugeRpm = 0;
    private double _sugarMoisturePct = 0.05;
    private double _sugarColorIcumsa = 45;
    private double _sugarPolarizationPct = 99.8;
    private double _milkPasteurizerTemperatureC = 4;
    private double _milkHomogenizerPressureBar = 0;
    private double _milkPasteurizationHoldSeconds = 0;
    private double _milkMicroLogReduction = 0;
    private double _creamSeparatorRpm = 0;
    private double _creamYieldL = 0;
    private double _creamFatPct = 38.0;
    private double _creamPasteurizerTemperatureC = 4.0;
    private double _creamPasteurizationHoldSeconds = 0;
    private double _butterChurnTemperatureC = 10.0;
    private double _butterFatPct = 0;
    private double _butterMoisturePct = 16.0;
    private double _butterSaltPct = 0.05;
    private double _butterWorkingPressureKPa = 0;
    private double _vanillaExtractorTemperatureC = 24;
    private double _vanillaExtractStrengthPct = 0;
    private double _cocoaRoasterTemperatureC = 24;
    private double _cocoaRoasterAirflowM3Min = 0;
    private double _cocoaRoastDevelopmentPct = 0;
    private double _cocoaWinnowerEfficiencyPct = 0;
    private double _cocoaNibYieldPct = 0;
    private double _cocoaPressPressureBar = 0;
    private double _cocoaGrindMicrons = 0;
    private double _cocoaPowderFatPct = 11.0;
    private double _cocoaPowderMoisturePct = 4.2;
    private double _brineSalinityPct = 2.6;
    private double _brineHardnessPpm = 420;
    private double _brineTurbidityNtu = 12.0;
    private double _brineClarifierTurbidityNtu = 12.0;
    private double _saltEvaporatorVacuumKPa = 0;
    private double _saltCrystallizerTemperatureC = 24;
    private double _saltCentrifugeRpm = 0;
    private double _saltDryerTemperatureC = 24;
    private double _saltMoisturePct = 0.18;
    private double _saltPurityPct = 99.2;
    private double _saltScreenPassingPct = 96.0;
    private double _sodaAshAssayPct = 99.2;
    private double _phosphateAcidValuePct = 98.4;
    private double _starchSlurryBrix = 0;
    private double _starchDryerTemperatureC = 24;
    private double _starchMoisturePct = 12.5;
    private double _leaveningMixerRpm = 0;
    private double _leaveningHomogeneityPct = 0;
    private double _leaveningBlendMoisturePct = 2.8;
    private double _leaveningSifterLoadPct = 0;
    private double _leaveningDustCollectorPressurePa = 160;
    private double _cartonFormerSpeedCpm = 0;
    private double _cartonBoardCaliperMm = 0.42;
    private double _cartonBoardMoisturePct = 6.4;
    private double _cartonDieCutWastePct = 0;
    private double _labelWebTensionN = 0;
    private double _printRegistrationMm = 0.18;
    private double _gluePotTemperatureC = 24;
    private double _glueBeadGPerCarton = 0;
    private double _caseCodeReadRatePct = 100;
    private double _icingMixerRpm = 0;
    private double _icingTemperatureC = 22;
    private double _icingViscosityPaS = 7.8;
    private double _feedMillHammerRpm = 0;
    private double _feedPelletTemperatureC = 24;
    private double _feedMoisturePct = 11.5;
    private double _compostTemperatureC = 32;
    private double _compostMoisturePct = 42;
    private double _compostAerationPct = 78;
    private double _beddingChopperRpm = 0;
    private double _beddingMoisturePct = 11.8;
    private double _beddingDustPct = 3.2;
    private double _mineralMixerRpm = 0;
    private double _mineralHomogeneityPct = 96.0;
    private double _mineralMetalPpm = 12.0;
    private double _factoryRunQualityPct = 100;
    private IngredientFactoryRun? _factoryRun;
    private double _batterKg;
    private double _wasteKg;
    private const double ByproductStorageCapacityKg = 900;
    private const double FactoryEffluentCapacityL = 3000;
    private double _branKg = 34;
    private double _beetPulpKg = 52;
    private double _molassesKg = 12;
    private double _skimMilkL = 24;
    private double _buttermilkL = 18;
    private double _vanillaPomaceKg = 2.0;
    private double _cocoaShellKg = 6;
    private double _cocoaButterKg = 3.0;
    private double _brineBlowdownL = 80;
    private double _leaveningDustKg = 1.2;
    private double _packagingTrimKg = 1.5;
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
               && FactoryLotReleased(r.MilkL * n, _milkLotId)
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
               && HasFactoryUtilities(38, 46, 12, 0.22);
    }

    private bool IcingInputLotsReady(CakeRecipe r)
    {
        var formula = IcingFormula(r);
        return FactoryLotReleased(formula.SugarKg, _sugarLotId)
               && FactoryLotReleased(formula.ButterKg, _butterLotId)
               && FactoryLotReleased(formula.MilkL, _milkLotId)
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
        return $"{batchLotId}: {r.Name} uses flour {_flourLotId}, sugar {_sugarLotId}, eggs {_eggLotId}, pasteurized milk {_milkLotId}, butter {_butterLotId}, leavening {_leaveningLotId}, salt {_saltLotId}, vanilla {_vanillaLotId}, cocoa {(r.CocoaKg > 0 ? _cocoaLotId : "not required")}, prepared icing {_icingLotId} and packaging {_packagingLotId}.";
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
        + _molassesKg
        + _vanillaPomaceKg
        + _cocoaShellKg
        + _cocoaButterKg
        + _leaveningDustKg
        + _packagingTrimKg
        + _skimMilkL * 1.03
        + _buttermilkL * 1.03
        + _brineBlowdownL * 1.05;

    private double CompostableOrganicsKg() =>
        Math.Min(_branKg, 18.0)
        + Math.Min(_beetPulpKg, 24.0)
        + Math.Min(_molassesKg, 10.0)
        + Math.Min(_vanillaPomaceKg, 2.0)
        + Math.Min(_cocoaShellKg, 5.0)
        + Math.Min(_skimMilkL, 10.0) * 1.03
        + Math.Min(_buttermilkL, 12.0) * 1.03;

    private bool CompostPlantInputsReady() =>
        _manureKg >= 120
        && _poultryManureKg >= 42
        && CompostableOrganicsKg() >= 18;

    private double ByproductStoragePctValue() =>
        Math.Clamp(ByproductStorageLoadKg() / ByproductStorageCapacityKg * 100.0, 0, 160);

    private double EffluentTankPctValue() =>
        Math.Clamp(_factoryEffluentL / FactoryEffluentCapacityL * 100.0, 0, 160);

    private static double ExpectedByproductLoadKg(IngredientFactoryRun run) => run.Kind switch
    {
        IngredientFactoryKind.Mill => run.PrimaryInput * 0.22,
        IngredientFactoryKind.Sugar => run.PrimaryInput * 0.39,
        IngredientFactoryKind.Milk => run.PrimaryInput * 0.012,
        IngredientFactoryKind.Butter => run.PrimaryInput * 0.92 * 1.03,
        IngredientFactoryKind.Vanilla => run.PrimaryInput * 0.32,
        IngredientFactoryKind.Cocoa => run.PrimaryInput * 0.36,
        IngredientFactoryKind.Salt => run.PrimaryInput * 0.16 * 1.05,
        IngredientFactoryKind.Starch => run.PrimaryInput * 0.18,
        IngredientFactoryKind.Leavening => run.Product * 0.025,
        IngredientFactoryKind.Packaging => run.PrimaryInput * 0.065 + run.SecondaryInput * 0.010,
        IngredientFactoryKind.Feed => 0,
        IngredientFactoryKind.Fertilizer => -run.TertiaryInput,
        IngredientFactoryKind.MineralPremix => run.Waste,
        _ => run.Waste,
    };

    private static double ExpectedEffluentL(IngredientFactoryRun run) => run.Kind switch
    {
        IngredientFactoryKind.Sugar => run.ProcessWaterL * 0.72 + run.CulinarySteamKg * 0.08,
        IngredientFactoryKind.Milk => run.ProcessWaterL * 0.50 + run.PrimaryInput * 0.03,
        IngredientFactoryKind.Butter => run.ProcessWaterL * 0.55 + run.PrimaryInput * 0.08,
        IngredientFactoryKind.Vanilla => run.ProcessWaterL * 0.62,
        IngredientFactoryKind.Salt => run.PrimaryInput * 0.14,
        IngredientFactoryKind.Starch => run.ProcessWaterL * 0.48 + run.CulinarySteamKg * 0.04,
        IngredientFactoryKind.Cocoa => run.ProcessWaterL * 0.35,
        IngredientFactoryKind.Leavening => run.FilterMediaPct * 8.0,
        IngredientFactoryKind.Packaging => run.ProcessWaterL * 0.45 + run.QuaternaryInput * 0.7,
        IngredientFactoryKind.Feed => run.CulinarySteamKg * 0.06 + run.ProcessWaterL * 0.25,
        IngredientFactoryKind.Fertilizer => run.ProcessWaterL * 0.18,
        IngredientFactoryKind.MineralPremix => run.ProcessWaterL * 0.20,
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
        double straw = wheat > 0 ? wheat * 0.62 : 0;
        _wheatKg += wheat;
        _strawKg += straw;
        _sugarCropKg += beet;
        _vanillaBeansKg += vanillaBeans;
        var lots = new List<string>();
        if (wheat > 0)
        {
            _wheatLotId = NewLotId("WHEAT");
            _strawLotId = NewLotId("STRAW");
            _wheatMoisturePct = Math.Clamp(17.0 - _wheatGrowth * 0.050 + _rng.NextDouble() * 0.8, 11.5, 15.8);
            _wheatForeignMaterialPct = Math.Clamp(1.4 - _wheatGrowth * 0.004 + _rng.NextDouble() * 0.5, 0.35, 2.2);
            _wheatProteinPct = Math.Clamp(7.7 + _pastureHealth * 0.009 + _rng.NextDouble() * 0.35, 7.8, 9.4);
            lots.Add($"wheat {_wheatLotId}");
            lots.Add($"straw {_strawLotId}");
        }
        if (beet > 0)
        {
            _sugarCropLotId = NewLotId("SUGARCROP");
            _sugarCropSugarPct = Math.Clamp(13.4 + _beetGrowth * 0.035 + _rng.NextDouble() * 0.65, 14.0, 18.4);
            _sugarCropSoilTarePct = Math.Clamp(4.8 - _beetGrowth * 0.026 + _rng.NextDouble() * 0.85, 1.4, 5.8);
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
        return $"Harvested {wheat:0} kg wheat, {straw:0} kg straw, {beet:0} kg sugar crop, {vanillaBeans:0.0} kg green vanilla beans for curing/extraction.";
    }

    public string HarvestFeedCrops()
    {
        if (_lastPowerAvailability < 0.15)
            return "Forage mower, wagon scale and grain cleaner need reactor bus power.";

        const double labor = 0.7;
        if (_barnLaborHours < labor)
            return "Feed crop harvest needs barn labor for cutting, raking, moisture checks and wagon unloading.";
        if (_pastureHealth < 35 && _wheatGrowth < 55)
            return "Feed crops are not ready; pasture or wheat must mature before forage/grain harvest.";

        double forage = _pastureHealth >= 35 ? _pastureHealth * 4.6 : 0;
        double grain = _wheatGrowth >= 55 ? _wheatGrowth * 0.95 : 0;
        double straw = grain > 0 ? grain * 0.28 : 0;
        var lots = new List<string>();

        if (forage > 0)
        {
            _forageKg += forage;
            _forageLotId = NewLotId("FORAGE");
            _forageMoisturePct = Math.Clamp(22.0 - _pastureHealth * 0.08 + _rng.NextDouble() * 1.4, 12.0, 22.0);
            _pastureHealth = Math.Max(18, _pastureHealth - 31);
            lots.Add($"forage {_forageLotId}");
        }

        if (grain > 0)
        {
            _grainKg += grain;
            _strawKg += straw;
            _grainLotId = NewLotId("GRAIN");
            _strawLotId = NewLotId("STRAW");
            _feedGrainMoisturePct = 11.6 + _rng.NextDouble() * 2.2;
            _wheatGrowth = 14 + _rng.NextDouble() * 8;
            lots.Add($"feed grain {_grainLotId}");
            lots.Add($"straw {_strawLotId}");
        }

        _barnLaborHours = Math.Max(0, _barnLaborHours - labor);
        _forkliftBatteryPct = Math.Max(0, _forkliftBatteryPct - 1.4);
        _feedCropStatus = $"Feed crop harvest logged: {forage:0} kg forage at {_forageMoisturePct:0.0}% moisture, {grain:0} kg feed grain at {_feedGrainMoisturePct:0.0}% moisture and {straw:0} kg straw.";
        _traceabilityStatus = lots.Count == 0
            ? "Feed crop harvest completed with no new traceable lots."
            : "Feed crop trace logged: " + string.Join(", ", lots) + ".";
        return _feedCropStatus;
    }

    public string HarvestCocoa()
    {
        if (_lastPowerAvailability < 0.15)
            return "Cocoa greenhouse harvest needs reactor bus power for irrigation valves, shade controls and drying fans.";

        const double labor = 0.55;
        if (_barnLaborHours < labor)
            return "Cocoa harvest needs farm labor for pod cutting, bean scooping, fermentation box turns and dryer checks.";
        if (_forkliftBatteryPct < 6)
            return "Cocoa harvest needs forklift battery for moving fermentation trays and drying racks.";
        if (_cocoaGrowth < 35)
            return "Cacao pods are not mature enough for harvest.";

        double beans = _cocoaGrowth * 0.34;
        _cocoaBeansKg += beans;
        _cocoaBeansLotId = NewLotId("COCOABEAN");
        _cocoaBeanMoisturePct = Math.Clamp(7.8 - _cocoaGrowth * 0.018 + _rng.NextDouble() * 0.6, 5.8, 8.2);
        _cocoaFermentationPct = Math.Clamp(76 + _cocoaGrowth * 0.22 + _rng.NextDouble() * 5.0, 82, 99);
        _cocoaGrowth = 16 + _rng.NextDouble() * 8;
        _barnLaborHours = Math.Max(0, _barnLaborHours - labor);
        _forkliftBatteryPct = Math.Max(0, _forkliftBatteryPct - 1.1);
        _cocoaGreenhouseStatus = $"Cocoa greenhouse harvest logged: {beans:0.0} kg fermented cocoa beans lot {_cocoaBeansLotId}, {_cocoaFermentationPct:0}% fermentation and {_cocoaBeanMoisturePct:0.0}% moisture.";
        _traceabilityStatus = $"Cocoa greenhouse trace logged: cacao pods -> fermented bean lot {_cocoaBeansLotId}; cocoa roaster/winnower/press/pin mill still required before chocolate batching.";
        return _cocoaGreenhouseStatus;
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
        if (!FactoryLotReleased(_dairyMineralKg, _dairyMineralLotId))
            return $"Dairy ration cannot be mixed; mineral premix lot {_dairyMineralLotId} is waiting for QA lab release.";
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
        _rawMilkL += milk;
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
        if (milk > 0) _rawMilkLotId = NewLotId("RAWMILK");
        if (eggs > 0) _eggLotId = NewLotId("EGG");
        _milkParlorThroughputLPerHour = 680 + _rng.NextDouble() * 80;
        _milkingVacuumKPa = 40.5 + _rng.NextDouble() * 3.0;
        _milkFatPct = 3.55 + _cowComfort / 100.0 * 0.55 + _rng.NextDouble() * 0.12;
        _milkProteinPct = 3.05 + _pastureHealth / 100.0 * 0.28 + _rng.NextDouble() * 0.06;
        double hygienePenalty = Math.Max(0, 86 - _dairyParlorHygienePct);
        double manurePenalty = Math.Max(0, _manureKg - 650) * 0.025;
        _milkSomaticCellCountKPerMl = Math.Clamp(250 - _cowComfort * 1.25 + hygienePenalty * 3.0 + manurePenalty + _rng.NextDouble() * 25, 80, 520);
        _milkBacteriaCfuPerMl = Math.Clamp(_milkBacteriaCfuPerMl + milk * (24 + hygienePenalty * 1.8) + (100 - _sanitationScore) * 35, 1200, 85000);
        _bulkMilkTankC = Math.Min(6.0, (_bulkMilkTankC * Math.Max(0, _rawMilkL - milk) + 37.0 * milk) / Math.Max(1, _rawMilkL));
        _dairyParlorHygienePct = Math.Max(0, _dairyParlorHygienePct - milk * 0.045);
        _milkSourceStatus = $"Transferred {milk:0.0} L raw milk lot {_rawMilkLotId} from {_lactatingCowCount} lactating cows fed by TMR lot {_mixedRationLotId} through the milking parlor to cold storage; pasteurization is still required before batching.";
        _traceabilityStatus = $"Dairy and poultry trace logged: raw milk lot {_rawMilkLotId} from {_lactatingCowCount} lactating cows, ration {_mixedRationLotId}, egg lot {_eggLotId} from {_layingHenCount} hens using feed lot {_feedLotId}.";
        return $"Collected {milk:0.0} L raw cow milk and {eggs:0} graded eggs; parlor hygiene {_dairyParlorHygienePct:0}%, bulk tank {_bulkMilkTankC:0.0} degC, bacteria {_milkBacteriaCfuPerMl:0} CFU/mL.";
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
        if (string.IsNullOrWhiteSpace(_wheatLotId))
            return "Roller mill cannot run because the wheat lot is missing from the grain ledger.";

        double targetTemperMoisture = 15.2;
        double temperWater = Math.Max(0, targetTemperMoisture - _wheatMoisturePct) * wheat * 0.11;
        double cleaningRejects = wheat * Math.Clamp(_wheatForeignMaterialPct / 100.0 * 0.72, 0.0025, 0.026);
        _millRollGapMm = 0.28 + _rng.NextDouble() * 0.05;
        _flourExtractionPct = 0;
        _wheatTemperMoisturePct = _wheatMoisturePct;
        _millSifterLoadPct = 0;
        _flourMoisturePct = 0;
        _flourAshPct = 0;
        _flourProteinPct = 0;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Mill,
            Name = "Roller mill",
            StartedMessage = $"Started cleaning, tempering and milling {wheat:0} kg wheat lot {_wheatLotId}: {_wheatMoisturePct:0.0}% moisture, {_wheatForeignMaterialPct:0.0}% foreign material and {_wheatProteinPct:0.0}% protein.",
            DurationSeconds = 8.0,
            PowerDemandMW = 1.8,
            PrimaryInput = wheat,
            Product = wheat * Math.Clamp(0.775 - _wheatForeignMaterialPct * 0.012 - Math.Abs(_wheatProteinPct - 8.4) * 0.006, 0.70, 0.78),
            Waste = wheat * 0.025 + cleaningRejects,
            ProcessWaterL = 18 + temperWater,
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
        if (string.IsNullOrWhiteSpace(_sugarCropLotId))
            return "Sugar house cannot run because the sugar-crop lot is missing from the field ledger.";

        double washRejects = crop * Math.Clamp(_sugarCropSoilTarePct / 100.0 * 0.82, 0.012, 0.052);
        double recoverableSugar = crop * Math.Clamp(_sugarCropSugarPct / 100.0 * 0.86 - _sugarCropSoilTarePct * 0.002, 0.105, 0.158);
        double processWater = 260 + crop * _sugarCropSoilTarePct * 0.25;
        double steam = 500 + Math.Max(0, 16.6 - _sugarCropSugarPct) * 34.0;
        _sugarJuiceBrix = 0;
        _sugarEvaporatorTemperatureC = 32.0;
        _sugarJuicePurityPct = 0;
        _sugarLimePh = 0;
        _sugarPanVacuumKPa = 0;
        _sugarCentrifugeRpm = 0;
        _sugarMoisturePct = 0;
        _sugarColorIcumsa = 0;
        _sugarPolarizationPct = 0;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Sugar,
            Name = "Sugar house diffuser, pan and centrifuge",
            StartedMessage = $"Started flume washing, slicing, diffusing, carbonating and crystallizing {crop:0} kg sugar-crop lot {_sugarCropLotId}: {_sugarCropSugarPct:0.0}% sugar and {_sugarCropSoilTarePct:0.0}% soil tare.",
            DurationSeconds = 10.0,
            PowerDemandMW = 2.2,
            PrimaryInput = crop,
            Product = recoverableSugar,
            Waste = crop * 0.018 + washRejects,
            ProcessWaterL = processWater,
            CulinarySteamKg = steam,
            CompressedAirNm3 = 22,
            FilterMediaPct = 1.2,
            WearPct = 2.35,
            CalibrationDriftPct = 0.75,
            InputLotId = _sugarCropLotId,
            OutputLotId = NewLotId("SUGAR"),
        };
        return StartFactoryRun(run, () => ConsumeTrackedStock(ref _sugarCropKg, crop, ref _sugarCropLotId));
    }

    public string PasteurizeMilk()
    {
        if (_lastPowerAvailability < 0.2)
            return "Milk pasteurizer, balance tank and homogenizer need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        double milk = Math.Min(_rawMilkL, 60);
        if (milk < 5)
            return "Not enough raw bulk-tank milk is available for pasteurization.";
        if (string.IsNullOrWhiteSpace(_rawMilkLotId))
            return "Milk pasteurizer cannot run because the raw milk lot is missing from the dairy ledger.";
        if (!MilkQaInSpec())
            return "Milk pasteurizer cannot run; raw milk QA is on hold. Wash the parlor, restore cooling and wait for in-spec milk.";

        _milkPasteurizerTemperatureC = _bulkMilkTankC;
        _milkHomogenizerPressureBar = 0;
        _milkPasteurizationHoldSeconds = 0;
        _milkMicroLogReduction = 0;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Milk,
            Name = "Milk pasteurizer and homogenizer",
            StartedMessage = $"Started HTST pasteurizing and homogenizing {milk:0.0} L raw cow milk from lot {_rawMilkLotId}.",
            DurationSeconds = 6.5,
            PowerDemandMW = 1.2,
            PrimaryInput = milk,
            Product = milk * 0.985,
            Waste = milk * 0.005,
            ProcessWaterL = 80,
            CulinarySteamKg = 160,
            CompressedAirNm3 = 14,
            FilterMediaPct = 0.45,
            WearPct = 1.10,
            CalibrationDriftPct = 0.38,
            InputLotId = _rawMilkLotId,
            OutputLotId = NewLotId("MILK"),
        };
        return StartFactoryRun(run, () => ConsumeTrackedStock(ref _rawMilkL, milk, ref _rawMilkLotId));
    }

    public string ChurnButter()
    {
        if (_lastPowerAvailability < 0.2)
            return "Cream separator and churn need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        double milk = Math.Min(Math.Max(0, _rawMilkL - 30), 54);
        if (milk < 5)
            return "Keep at least 30 L raw milk in cold storage before churning butter.";
        if (string.IsNullOrWhiteSpace(_rawMilkLotId))
            return "Butter room cannot run because the raw milk lot is missing from the dairy ledger.";
        if (!MilkQaInSpec())
            return "Butter room cannot run; raw milk QA is on hold. Wash the parlor, restore cooling and wait for in-spec milk.";

        double targetCreamFatPct = Math.Clamp(37.5 + _milkFatPct * 0.22 + _rng.NextDouble() * 1.4, 37.0, 41.5);
        double cream = milk * Math.Clamp(_milkFatPct / targetCreamFatPct * 0.98, 0.075, 0.125);
        double fatKg = milk * 1.03 * (_milkFatPct / 100.0);
        double butter = fatKg * Math.Clamp(0.93 - Math.Max(0, _milkBacteriaCfuPerMl - 22000) / 600000.0, 0.86, 0.94) / 0.82;
        _creamSeparatorRpm = 0;
        _creamYieldL = 0;
        _creamFatPct = 0;
        _creamPasteurizerTemperatureC = _bulkMilkTankC;
        _creamPasteurizationHoldSeconds = 0;
        _butterChurnTemperatureC = 0;
        _butterFatPct = 0;
        _butterMoisturePct = 0;
        _butterSaltPct = 0;
        _butterWorkingPressureKPa = 0;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Butter,
            Name = "Cream separator, pasteurizer and butter churn",
            StartedMessage = $"Started separating {cream:0.0} L cream from {milk:0.0} L raw cow milk lot {_rawMilkLotId}: {_milkFatPct:0.00}% milk fat, {_milkProteinPct:0.00}% protein, bulk tank {_bulkMilkTankC:0.0} degC.",
            DurationSeconds = 7.6,
            PowerDemandMW = 1.4,
            PrimaryInput = milk,
            SecondaryInput = cream,
            Product = butter,
            Waste = milk * 0.006,
            ProcessWaterL = 90 + milk * 0.55,
            CulinarySteamKg = 120 + cream * 10.5,
            CompressedAirNm3 = 15,
            FilterMediaPct = 0.8,
            WearPct = 1.45,
            CalibrationDriftPct = 0.45,
            InputLotId = _rawMilkLotId,
            OutputLotId = NewLotId("BUTTER"),
        };
        return StartFactoryRun(run, () => ConsumeTrackedStock(ref _rawMilkL, milk, ref _rawMilkLotId));
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
        _forageKg += 380;
        _grainKg += 260;
        _strawKg += 260;
        _limestoneKg += 72;
        _traceMineralKg += 24;
        _barnLaborHours = Math.Min(60, _barnLaborHours + 12);
        _brineL += 1600;
        _brineSalinityPct = 2.55 + _rng.NextDouble() * 0.45;
        _brineHardnessPpm = 320 + _rng.NextDouble() * 260;
        _brineTurbidityNtu = 7.0 + _rng.NextDouble() * 11.0;
        _brineClarifierTurbidityNtu = _brineTurbidityNtu;
        _sodaAshKg += 42;
        _phosphateKg += 48;
        _sodaAshAssayPct = 98.9 + _rng.NextDouble() * 0.7;
        _phosphateAcidValuePct = 97.8 + _rng.NextDouble() * 1.4;
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
        _forageLotId = NewLotId("FORAGE");
        _grainLotId = NewLotId("GRAIN");
        _strawLotId = NewLotId("STRAW");
        _limestoneLotId = NewLotId("LIMESTONE");
        _traceMineralLotId = NewLotId("TRACE-MIN");
        _cocoaBeansLotId = NewLotId("COCOABEAN");
        _brineLotId = NewLotId("BRINE");
        _sodaAshLotId = NewLotId("SODA");
        _phosphateLotId = NewLotId("PHOSPHATE");
        _paperboardLotId = NewLotId("PAPERBOARD");
        _labelStockLotId = NewLotId("LABEL");
        _packagingInkLotId = NewLotId("INK");
        _adhesiveLotId = NewLotId("ADHESIVE");
        _utilityLotId = NewLotId("UTILITY");
        _warehouseStatus = $"Receiving manifest {_lastSupplyManifestId} booked into warehouse; forklift battery {_forkliftBatteryPct:0}% and {_warehousePalletSpacePct:0}% pallet space free.";
        _traceabilityStatus = $"Receiving manifest {_lastSupplyManifestId} logged seed, dairy forage, grain, limestone, trace minerals, straw-bedding feedstock, feed-mill inputs, cocoa, brine lot {_brineLotId} at {_brineSalinityPct:0.0}% salinity, baking-soda lot {_sodaAshLotId}, phosphate lot {_phosphateLotId}, packaging feedstocks and utility lots; salt, starch carrier, crop fertilizer, livestock bedding, baking powder and dairy mineral premix must be made on site.";
    }

    public string ProcessCocoa()
    {
        if (_lastPowerAvailability < 0.2)
            return "Cocoa roaster, winnower, hydraulic press and pin mill need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        double beans = Math.Min(_cocoaBeansKg, 45);
        if (beans < 5)
            return "Not enough cocoa beans are available for the cocoa roaster/winnower/press/pin mill run.";
        if (string.IsNullOrWhiteSpace(_cocoaBeansLotId))
            return "Cocoa roaster cannot run because the cocoa bean lot is missing from the ledger.";

        _cocoaRoasterTemperatureC = 24;
        _cocoaRoasterAirflowM3Min = 0;
        _cocoaRoastDevelopmentPct = 0;
        _cocoaWinnowerEfficiencyPct = 0;
        _cocoaNibYieldPct = 0;
        _cocoaPressPressureBar = 0;
        _cocoaGrindMicrons = 0;
        _cocoaPowderFatPct = 0;
        _cocoaPowderMoisturePct = 0;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Cocoa,
            Name = "Cocoa roaster, winnower, press and pin mill",
            StartedMessage = $"Started roasting, cracking, winnowing, pressing and pin-milling {beans:0} kg cocoa beans lot {_cocoaBeansLotId}; beans {_cocoaFermentationPct:0}% fermented and {_cocoaBeanMoisturePct:0.0}% moisture.",
            DurationSeconds = 12.4,
            PowerDemandMW = 2.05,
            PrimaryInput = beans,
            Product = beans * 0.52,
            Waste = beans * 0.025,
            ProcessWaterL = 32,
            CompressedAirNm3 = 58,
            FilterMediaPct = 0.95,
            WearPct = 2.25,
            CalibrationDriftPct = 0.72,
            InputLotId = _cocoaBeansLotId,
            OutputLotId = NewLotId("COCOA"),
        };
        return StartFactoryRun(run, () => ConsumeTrackedStock(ref _cocoaBeansKg, beans, ref _cocoaBeansLotId));
    }

    public string RunSaltWorks()
    {
        if (_lastPowerAvailability < 0.2)
            return "Brine clarifier, vacuum evaporator, centrifuge and salt dryer need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        double brine = Math.Min(_brineL, 600);
        if (brine < 80)
            return "Not enough brine is available for a salt works run.";
        if (string.IsNullOrWhiteSpace(_brineLotId))
            return "Salt works cannot run because the brine source lot is missing from the ledger.";

        double dissolvedSalt = brine * (_brineSalinityPct / 100.0);
        double salt = dissolvedSalt * 0.94;
        _brineClarifierTurbidityNtu = _brineTurbidityNtu;
        _saltEvaporatorVacuumKPa = 0;
        _saltCrystallizerTemperatureC = 28;
        _saltCentrifugeRpm = 0;
        _saltDryerTemperatureC = 28;
        _saltMoisturePct = 0.32;
        _saltPurityPct = 0;
        _saltScreenPassingPct = 0;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Salt,
            Name = "Brine clarifier, vacuum pan and salt dryer",
            StartedMessage = $"Started clarifying {brine:0} L brine lot {_brineLotId} at {_brineSalinityPct:0.0}% salinity, {_brineHardnessPpm:0} ppm hardness and {_brineTurbidityNtu:0.0} NTU turbidity before vacuum evaporation.",
            DurationSeconds = 9.8,
            PowerDemandMW = 1.75,
            PrimaryInput = brine,
            Product = salt,
            Waste = brine * 0.002,
            ProcessWaterL = 18,
            CulinarySteamKg = 780,
            CompressedAirNm3 = 38,
            FilterMediaPct = 0.65,
            WearPct = 2.05,
            CalibrationDriftPct = 0.58,
            InputLotId = _brineLotId,
            OutputLotId = NewLotId("SALT"),
        };
        return StartFactoryRun(run, () => ConsumeTrackedStock(ref _brineL, brine, ref _brineLotId));
    }

    public string RunStarchPlant()
    {
        if (_lastPowerAvailability < 0.2)
            return "Starch wet mill, centrifuge and flash dryer need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        const double grain = 52.0;
        if (_grainKg < grain)
            return "Starch plant needs harvested or received feed grain before it can make starch carrier.";
        if (string.IsNullOrWhiteSpace(_grainLotId))
            return "Starch plant cannot run because the grain lot is missing from the ledger.";

        _starchSlurryBrix = 8.0;
        _starchDryerTemperatureC = 28.0;
        _starchMoisturePct = 18.5;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Starch,
            Name = "Starch wet mill and flash dryer",
            StartedMessage = $"Started wet-milling {grain:0} kg feed grain into starch carrier for leavening and poultry feed.",
            DurationSeconds = 7.2,
            PowerDemandMW = 1.25,
            PrimaryInput = grain,
            Product = grain * 0.56,
            Waste = grain * 0.025,
            ProcessWaterL = 85,
            CulinarySteamKg = 140,
            CompressedAirNm3 = 16,
            FilterMediaPct = 0.45,
            WearPct = 1.20,
            CalibrationDriftPct = 0.48,
            InputLotId = _grainLotId,
            OutputLotId = NewLotId("STARCH"),
        };

        return StartFactoryRun(run, () => ConsumeTrackedStock(ref _grainKg, grain, ref _grainLotId));
    }

    public string RunLeaveningPlant()
    {
        if (_lastPowerAvailability < 0.2)
            return "Baking-powder leavening plant, dehumidifier and dust collector need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        if (_sodaAshKg < 3 || _phosphateKg < 3 || _starchKg < 2)
            return "Not enough baking soda, phosphate and released starch carrier are available for baking powder.";
        if (string.IsNullOrWhiteSpace(_sodaAshLotId) || string.IsNullOrWhiteSpace(_phosphateLotId) || string.IsNullOrWhiteSpace(_starchLotId))
            return "Leavening plant cannot run because a baking-soda, phosphate or starch source lot is missing from the ledger.";
        if (!FactoryLotReleased(_starchKg, _starchLotId))
            return $"Leavening plant cannot run; starch lot {_starchLotId} is waiting for QA lab release.";

        double scale = Math.Min(Math.Min(_sodaAshKg / 18.0, _phosphateKg / 18.0), _starchKg / 12.0);
        scale = Math.Min(1.0, scale);
        double soda = 18.0 * scale;
        double phosphate = 18.0 * scale;
        double starch = 12.0 * scale;
        double input = soda + phosphate + starch;

        _leaveningMixerRpm = 0;
        _leaveningHomogeneityPct = 0;
        _leaveningBlendMoisturePct = 3.3;
        _leaveningSifterLoadPct = 0;
        _leaveningDustCollectorPressurePa = 145;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Leavening,
            Name = "Baking-powder micro-dosing and blend line",
            StartedMessage = $"Started micro-weighing {soda:0.0} kg baking soda lot {_sodaAshLotId}, {phosphate:0.0} kg phosphate lot {_phosphateLotId} and {starch:0.0} kg released starch carrier lot {_starchLotId} into baking powder.",
            DurationSeconds = 6.4,
            PowerDemandMW = 1.1,
            PrimaryInput = soda,
            SecondaryInput = phosphate,
            TertiaryInput = starch,
            Product = input * 0.92,
            Waste = input * 0.02,
            CompressedAirNm3 = 58,
            FilterMediaPct = 1.3,
            WearPct = 1.25,
            CalibrationDriftPct = 0.85,
            InputLotId = $"{_sodaAshLotId}/{_phosphateLotId}/{_starchLotId}",
            OutputLotId = NewLotId("LEAVEN"),
        };
        return StartFactoryRun(run, () =>
        {
            ConsumeTrackedStock(ref _sodaAshKg, soda, ref _sodaAshLotId);
            ConsumeTrackedStock(ref _phosphateKg, phosphate, ref _phosphateLotId);
            ConsumeTrackedStock(ref _starchKg, starch, ref _starchLotId);
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
        _cartonBoardCaliperMm = 0.42;
        _cartonBoardMoisturePct = 6.4;
        _cartonDieCutWastePct = 0;
        _labelWebTensionN = 0;
        _printRegistrationMm = 0.65;
        _gluePotTemperatureC = 28;
        _glueBeadGPerCarton = 0;
        _caseCodeReadRatePct = 0;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Packaging,
            Name = "Paperboard carton former, labeler, gluer and vision coder",
            StartedMessage = $"Started unwinding, scoring, die-cutting, forming, gluing, label-registering and vision-verifying {board:0} kg paperboard, {labels:0} m labels, {ink:0.0} L ink and {adhesive:0.0} kg adhesive into coded cake cartons.",
            DurationSeconds = 8.6,
            PowerDemandMW = 1.45,
            PrimaryInput = board,
            SecondaryInput = labels,
            TertiaryInput = ink,
            QuaternaryInput = adhesive,
            Product = 160,
            Waste = board * 0.018 + adhesive * 0.025,
            ProcessWaterL = 24,
            CompressedAirNm3 = 52,
            FilterMediaPct = 0.60,
            WearPct = 1.72,
            CalibrationDriftPct = 0.82,
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

    public string RunCompostPlant()
    {
        if (_lastPowerAvailability < 0.2)
            return "Compost turner, aeration blower and screen need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        const double dairyManure = 120.0;
        const double poultryManure = 42.0;
        double bran = Math.Min(_branKg, 18.0);
        double beetPulp = Math.Min(_beetPulpKg, 24.0);
        double molasses = Math.Min(_molassesKg, 10.0);
        double pomace = Math.Min(_vanillaPomaceKg, 2.0);
        double shell = Math.Min(_cocoaShellKg, 5.0);
        double skimMilk = Math.Min(_skimMilkL, 10.0);
        double buttermilk = Math.Min(_buttermilkL, 12.0);
        double organics = bran + beetPulp + molasses + pomace + shell + skimMilk * 1.03 + buttermilk * 1.03;

        if (_manureKg < dairyManure || _poultryManureKg < poultryManure || organics < 18.0)
            return "Compost plant needs dairy manure, poultry manure and enough factory organics before it can make crop fertilizer.";
        if (string.IsNullOrWhiteSpace(_mixedRationLotId) || string.IsNullOrWhiteSpace(_eggLotId))
            return "Compost plant cannot run because the dairy ration or poultry source lots are missing from the manure ledger.";

        double input = dairyManure + poultryManure + organics;
        _compostTemperatureC = 32;
        _compostMoisturePct = 42;
        _compostAerationPct = 0;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Fertilizer,
            Name = "Compost fertilizer plant",
            StartedMessage = $"Started composting {input:0.0} kg manure and factory organics into screened crop fertilizer.",
            DurationSeconds = 8.2,
            PowerDemandMW = 0.72,
            PrimaryInput = dairyManure,
            SecondaryInput = poultryManure,
            TertiaryInput = organics,
            Product = input * 0.52,
            Waste = input * 0.026,
            ProcessWaterL = 70,
            CompressedAirNm3 = 34,
            FilterMediaPct = 0.55,
            WearPct = 1.10,
            CalibrationDriftPct = 0.45,
            InputLotId = $"DAIRY-MANURE/{_mixedRationLotId}/POULTRY-MANURE/{_eggLotId}/BYPRODUCTS",
            OutputLotId = NewLotId("FERT"),
        };

        return StartFactoryRun(run, () =>
        {
            _manureKg = Math.Max(0, _manureKg - dairyManure);
            _poultryManureKg = Math.Max(0, _poultryManureKg - poultryManure);
            _branKg = Math.Max(0, _branKg - bran);
            _beetPulpKg = Math.Max(0, _beetPulpKg - beetPulp);
            _molassesKg = Math.Max(0, _molassesKg - molasses);
            _vanillaPomaceKg = Math.Max(0, _vanillaPomaceKg - pomace);
            _cocoaShellKg = Math.Max(0, _cocoaShellKg - shell);
            _skimMilkL = Math.Max(0, _skimMilkL - skimMilk);
            _buttermilkL = Math.Max(0, _buttermilkL - buttermilk);
        });
    }

    public string RunBeddingPlant()
    {
        if (_lastPowerAvailability < 0.2)
            return "Straw bedding chopper, dust collector and bagger need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        const double straw = 54.0;
        double bran = Math.Min(_branKg, 10.0);
        if (_strawKg < straw)
            return "Bedding plant needs harvested or received straw before it can make livestock bedding.";
        if (string.IsNullOrWhiteSpace(_strawLotId))
            return "Bedding plant cannot run because the straw lot is missing from the ledger.";

        double input = straw + bran;
        _beddingChopperRpm = 0;
        _beddingMoisturePct = 12.6;
        _beddingDustPct = 8.5;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Bedding,
            Name = "Straw bedding chopper and dust extractor",
            StartedMessage = $"Started chopping {input:0.0} kg straw and bran into low-dust livestock bedding.",
            DurationSeconds = 5.8,
            PowerDemandMW = 0.62,
            PrimaryInput = straw,
            SecondaryInput = bran,
            Product = input * 0.88,
            Waste = input * 0.035,
            ProcessWaterL = 18,
            CompressedAirNm3 = 26,
            FilterMediaPct = 0.35,
            WearPct = 0.92,
            CalibrationDriftPct = 0.38,
            InputLotId = bran > 0.001 ? $"{_strawLotId}/BRAN" : _strawLotId,
            OutputLotId = NewLotId("BEDDING"),
        };

        return StartFactoryRun(run, () =>
        {
            ConsumeTrackedStock(ref _strawKg, straw, ref _strawLotId);
            _branKg = Math.Max(0, _branKg - bran);
        });
    }

    public string RunMineralPremixPlant()
    {
        if (_lastPowerAvailability < 0.2)
            return "Mineral premix weigh room, micro-doser and ribbon blender need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        const double limestone = 16.0;
        const double traceMineral = 5.5;
        const double phosphate = 3.0;
        const double salt = 4.0;
        if (_limestoneKg < limestone || _traceMineralKg < traceMineral || _phosphateKg < phosphate || _saltKg < salt)
            return "Mineral premix plant needs limestone, trace mineral concentrate, phosphate and released baking salt.";
        if (string.IsNullOrWhiteSpace(_limestoneLotId) || string.IsNullOrWhiteSpace(_traceMineralLotId) || string.IsNullOrWhiteSpace(_phosphateLotId) || string.IsNullOrWhiteSpace(_saltLotId))
            return "Mineral premix plant cannot run because a mineral source lot is missing from the ledger.";
        if (!FactoryLotReleased(_saltKg, _saltLotId))
            return $"Mineral premix plant cannot run; salt lot {_saltLotId} is waiting for QA lab release.";

        double input = limestone + traceMineral + phosphate + salt;
        _mineralMixerRpm = 0;
        _mineralHomogeneityPct = 0;
        _mineralMetalPpm = 38;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.MineralPremix,
            Name = "Dairy mineral premix micro-doser",
            StartedMessage = $"Started blending {input:0.0} kg limestone, trace minerals, phosphate and salt into dairy mineral premix.",
            DurationSeconds = 5.6,
            PowerDemandMW = 0.58,
            PrimaryInput = limestone,
            SecondaryInput = traceMineral,
            TertiaryInput = phosphate,
            QuaternaryInput = salt,
            Product = input * 0.94,
            Waste = input * 0.018,
            ProcessWaterL = 12,
            CompressedAirNm3 = 22,
            FilterMediaPct = 0.30,
            WearPct = 0.88,
            CalibrationDriftPct = 0.42,
            InputLotId = $"{_limestoneLotId}/{_traceMineralLotId}/{_phosphateLotId}/{_saltLotId}",
            OutputLotId = NewLotId("DAIRYMIN"),
        };

        return StartFactoryRun(run, () =>
        {
            ConsumeTrackedStock(ref _limestoneKg, limestone, ref _limestoneLotId);
            ConsumeTrackedStock(ref _traceMineralKg, traceMineral, ref _traceMineralLotId);
            ConsumeTrackedStock(ref _phosphateKg, phosphate, ref _phosphateLotId);
            ConsumeTrackedStock(ref _saltKg, salt, ref _saltLotId);
        });
    }

    public string RunFeedMill()
    {
        if (_lastPowerAvailability < 0.2)
            return "Feed mill, pellet conditioner and cooler need reactor power.";
        if (_factoryRun is not null)
            return $"{_factoryRun.Name} is already running; wait for the ingredient factory run to finish.";

        const double grain = 42.0;
        const double starch = 4.8;
        const double mineral = 1.6;
        double bran = Math.Min(_branKg, 18.0);
        double beetPulp = Math.Min(_beetPulpKg, 24.0);
        if (_grainKg < grain || _starchKg < starch || _dairyMineralKg < mineral)
            return "Feed mill needs grain, starch carrier and mineral premix before it can make poultry feed.";
        if (string.IsNullOrWhiteSpace(_grainLotId) || string.IsNullOrWhiteSpace(_dairyMineralLotId) || string.IsNullOrWhiteSpace(_starchLotId))
            return "Feed mill cannot run because a grain, mineral premix or starch lot is missing from the ledger.";
        if (!FactoryLotReleased(_dairyMineralKg, _dairyMineralLotId))
            return $"Feed mill cannot run; mineral premix lot {_dairyMineralLotId} is waiting for QA lab release.";
        if (!FactoryLotReleased(_starchKg, _starchLotId))
            return $"Feed mill cannot run; starch lot {_starchLotId} is waiting for QA lab release.";

        double input = grain + starch + mineral + bran + beetPulp;
        _feedMillHammerRpm = 0;
        _feedPelletTemperatureC = 24;
        _feedMoisturePct = 14.2;
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Feed,
            Name = "Poultry feed mill and pellet cooler",
            StartedMessage = $"Started milling {input:0.0} kg poultry feed from grain, starch carrier, mineral premix, bran and beet pulp.",
            DurationSeconds = 6.5,
            PowerDemandMW = 0.85,
            PrimaryInput = grain,
            SecondaryInput = starch,
            TertiaryInput = mineral,
            QuaternaryInput = bran + beetPulp,
            Product = input * 0.94,
            Waste = input * 0.012,
            ProcessWaterL = 28,
            CulinarySteamKg = 55,
            CompressedAirNm3 = 18,
            FilterMediaPct = 0.25,
            WearPct = 1.05,
            CalibrationDriftPct = 0.42,
            InputLotId = $"{_grainLotId}/{_dairyMineralLotId}/{_starchLotId}/BYPRODUCTS",
            OutputLotId = NewLotId("FEED"),
        };

        return StartFactoryRun(run, () =>
        {
            ConsumeTrackedStock(ref _grainKg, grain, ref _grainLotId);
            ConsumeTrackedStock(ref _dairyMineralKg, mineral, ref _dairyMineralLotId);
            ConsumeTrackedStock(ref _starchKg, starch, ref _starchLotId);
            _branKg = Math.Max(0, _branKg - bran);
            _beetPulpKg = Math.Max(0, _beetPulpKg - beetPulp);
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
            return "Icing kitchen needs released sugar, butter, pasteurized milk, vanilla, cocoa when required, process water, culinary steam, compressed air and filter media.";
        if (!IcingInputLotsReady(recipe))
            return "Icing kitchen cannot run because sugar, butter, pasteurized milk, vanilla or cocoa lot release is incomplete.";

        _icingMixerRpm = 0;
        _icingTemperatureC = 24;
        _icingViscosityPaS = 12.5;
        string cocoaLot = formula.CocoaKg > 0 ? "/" + _cocoaLotId : "";
        var run = new IngredientFactoryRun
        {
            Kind = IngredientFactoryKind.Icing,
            Name = "Icing tempering kitchen",
            StartedMessage = $"Started preparing {formula.ProductKg:0.0} kg icing for {recipe.Name}: sugar, butter, pasteurized cow milk, vanilla{(formula.CocoaKg > 0 ? " and cocoa" : "")} are being weighed, cooked, cooled and tempered.",
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
        _factoryStatus = "Maintenance crew serviced roller mill, sugar house, milk pasteurizer, butter room, vanilla extractor, cocoa line, salt works, starch wet mill, leavening blender, packaging plant, icing tempering kitchen, poultry feed mill, compost fertilizer plant and straw bedding chopper.";
        return "Serviced all ingredient factories: lubricated bearings, verified guards, replaced filters, calibrated scales, extraction temperature probes, starch dryer moisture probes, packaging registration sensors, icing viscosity probes, feed mill magnets, compost aeration probes, bedding dust collectors and safety interlocks, and signed the maintenance log.";
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

        double feedMillCredit = Math.Min(120, _branKg * 0.35 + _beetPulpKg * 0.18 + _skimMilkL * 0.05 + _buttermilkL * 0.08 + _vanillaPomaceKg * 0.04);
        double revenue = Math.Round(load * 0.045, 2);
        _grainKg += feedMillCredit;
        if (feedMillCredit > 0.001) _grainLotId = NewLotId("FEEDGRAIN");
        _cashBalance += revenue;
        _forkliftBatteryPct = Math.Max(0, _forkliftBatteryPct - 5.5);
        _warehousePalletSpacePct = Math.Max(0, _warehousePalletSpacePct - 3.0);
        string detail = $"Hauled {load:0} kg-equivalent byproducts: bran {_branKg:0} kg, beet pulp {_beetPulpKg:0} kg, molasses {_molassesKg:0.0} kg, skim milk {_skimMilkL:0} L, buttermilk {_buttermilkL:0} L, vanilla pomace {_vanillaPomaceKg:0.0} kg, cocoa shell {_cocoaShellKg:0} kg, cocoa butter {_cocoaButterKg:0.0} kg, brine blowdown {_brineBlowdownL:0} L, leavening dust {_leaveningDustKg:0.0} kg and packaging trim {_packagingTrimKg:0.0} kg.";
        _branKg = 0;
        _beetPulpKg = 0;
        _molassesKg = 0;
        _skimMilkL = 0;
        _buttermilkL = 0;
        _vanillaPomaceKg = 0;
        _cocoaShellKg = 0;
        _cocoaButterKg = 0;
        _brineBlowdownL = 0;
        _leaveningDustKg = 0;
        _packagingTrimKg = 0;
        _wasteHandlingStatus = $"{detail} Sold/repurposed for ${revenue:0.00} and recovered {feedMillCredit:0.0} kg feed-mill grain equivalent for the poultry feed mill.";
        _traceabilityStatus = $"Byproduct recovery logged feed-mill input lot {_grainLotId}; poultry feed still requires the feed mill and QA release.";
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
                _wheatTemperMoisturePct = Math.Clamp(_wheatMoisturePct + Math.Min(1.0, p / 0.28) * Math.Max(0, 15.2 - _wheatMoisturePct), _wheatMoisturePct, 15.8);
                _millSifterLoadPct = Math.Clamp(p < 0.35 ? 0 : (p - 0.35) / 0.65 * (78.0 + _wheatForeignMaterialPct * 4.5), 0, 100);
                _flourExtractionPct = Math.Clamp((74.5 - Math.Max(0, _wheatForeignMaterialPct - 0.7) * 1.6 + Math.Max(0, 8.8 - _wheatProteinPct) * 0.35) * p, 0, 80);
                _flourMoisturePct = Math.Clamp(p < 0.48 ? 0 : _wheatTemperMoisturePct - 1.4 - p * 0.22 + Math.Sin(p * Math.PI * 3) * 0.08, 0, 15.0);
                _flourAshPct = Math.Clamp(0.34 + _millRollGapMm * 0.24 + _wheatForeignMaterialPct * 0.020 + p * 0.035, 0.32, 0.62);
                _flourProteinPct = Math.Clamp(_wheatProteinPct - 0.25 + Math.Sin(p * Math.PI) * 0.05, 7.2, 10.2);
                if (p > 0.35) _factoryRunQualityPct -= Math.Abs(_millRollGapMm - 0.30) * 120;
                if (p > 0.45) _factoryRunQualityPct -= Math.Abs(_wheatTemperMoisturePct - 15.2) * 1.6;
                if (p > 0.62) _factoryRunQualityPct -= Math.Max(0, _millSifterLoadPct - 88.0) * 0.45;
                if (p > 0.70) _factoryRunQualityPct -= Math.Abs(_flourAshPct - 0.44) * 34.0 + Math.Max(0, _flourMoisturePct - 14.2) * 2.0;
                break;
            case IngredientFactoryKind.Sugar:
                _sugarJuiceBrix = p < 0.30
                    ? 9.5 + p / 0.30 * (4.5 + _sugarCropSugarPct * 0.10)
                    : p < 0.68
                        ? 14.5 + (p - 0.30) / 0.38 * 53.0
                        : 67.5 + (p - 0.68) / 0.32 * 2.5;
                _sugarEvaporatorTemperatureC = p < 0.34 ? 34.0 + p * 115.0 : 98.0 + Math.Sin(p * Math.PI * 3) * 5.0;
                _sugarJuicePurityPct = Math.Clamp(78.0 + Math.Min(1.0, p / 0.52) * 12.0 - _sugarCropSoilTarePct * 0.55, 70, 92);
                _sugarLimePh = p < 0.20
                    ? 6.8 + p / 0.20 * 2.3
                    : p < 0.52
                        ? 9.1 + Math.Sin((p - 0.20) / 0.32 * Math.PI) * 1.8
                        : 8.35 + Math.Sin(p * Math.PI * 2) * 0.12;
                _sugarPanVacuumKPa = p < 0.64 ? 0 : 54.0 + (p - 0.64) / 0.36 * 10.0;
                _sugarCentrifugeRpm = p < 0.82 ? 0 : 760.0 + (p - 0.82) / 0.18 * 520.0;
                _sugarMoisturePct = p < 0.84 ? 0 : Math.Clamp(1.25 - (p - 0.84) / 0.16 * 1.19, 0.04, 1.3);
                _sugarColorIcumsa = Math.Clamp(190.0 - p * 148.0 + _sugarCropSoilTarePct * 4.0, 32, 260);
                _sugarPolarizationPct = p < 0.84 ? 0 : Math.Clamp(99.25 + (p - 0.84) / 0.16 * 0.55 - Math.Max(0, _sugarColorIcumsa - 70.0) * 0.0015, 98.8, 99.9);
                if (p > 0.48) _factoryRunQualityPct -= Math.Max(0, 86.0 - _sugarJuicePurityPct) * 0.50 + Math.Abs(_sugarLimePh - 8.35) * 1.1;
                if (p > 0.68) _factoryRunQualityPct -= Math.Abs(_sugarJuiceBrix - 68.5) * 0.55 + Math.Abs(_sugarEvaporatorTemperatureC - 101.0) * 0.08;
                if (p > 0.86) _factoryRunQualityPct -= Math.Max(0, _sugarColorIcumsa - 62.0) * 0.22 + Math.Max(0, _sugarMoisturePct - 0.07) * 150.0 + Math.Max(0, 99.70 - _sugarPolarizationPct) * 20.0;
                break;
            case IngredientFactoryKind.Milk:
                _milkPasteurizerTemperatureC = p < 0.25 ? _bulkMilkTankC + p / 0.25 * 68.0 : 72.0 + Math.Sin(p * Math.PI * 4) * 1.4;
                _milkHomogenizerPressureBar = p < 0.38 ? 0 : 126.0 + Math.Sin(p * Math.PI * 3) * 8.0;
                _milkPasteurizationHoldSeconds = p < 0.34 ? 0 : Math.Min(18.5, (p - 0.34) / 0.66 * 18.5);
                _milkMicroLogReduction = p < 0.40 ? 0 : Math.Min(5.8, (p - 0.40) / 0.60 * 5.8);
                if (p > 0.35) _factoryRunQualityPct -= Math.Abs(_milkPasteurizerTemperatureC - 72.0) * 0.55;
                if (p > 0.55) _factoryRunQualityPct -= Math.Max(0, 15.0 - _milkPasteurizationHoldSeconds) * 1.5;
                if (p > 0.62) _factoryRunQualityPct -= Math.Max(0, 4.8 - _milkMicroLogReduction) * 3.5;
                if (p > 0.45) _factoryRunQualityPct -= Math.Abs(_milkHomogenizerPressureBar - 130.0) * 0.06;
                break;
            case IngredientFactoryKind.Butter:
                _creamSeparatorRpm = p < 0.16 ? 6450.0 * p / 0.16 : 6450.0 + Math.Sin(p * Math.PI * 3) * 180.0;
                _creamYieldL = Math.Clamp(run.SecondaryInput * Math.Min(1.0, p / 0.24), 0, run.SecondaryInput);
                _creamFatPct = p < 0.12 ? 0 : Math.Clamp(38.5 + Math.Sin(p * Math.PI * 2.2) * 1.2 + (_milkFatPct - 3.8) * 0.6, 35, 43);
                _creamPasteurizerTemperatureC = p < 0.30
                    ? _bulkMilkTankC + p / 0.30 * (76.0 - _bulkMilkTankC)
                    : 76.0 + Math.Sin(p * Math.PI * 4) * 1.6;
                _creamPasteurizationHoldSeconds = p < 0.34 ? 0 : Math.Min(22.0, (p - 0.34) / 0.66 * 22.0);
                _butterChurnTemperatureC = p < 0.48 ? 0 : Math.Clamp(8.0 + Math.Sin(p * Math.PI * 5) * 1.1 + p * 1.2, 7.2, 11.8);
                _butterFatPct = p < 0.52 ? 0 : Math.Clamp(76.0 + (p - 0.52) / 0.48 * 6.4, 0, 84.5);
                _butterMoisturePct = p < 0.64 ? 0 : Math.Clamp(17.7 - (p - 0.64) / 0.36 * 1.5 + Math.Sin(p * Math.PI * 4) * 0.16, 15.4, 18.0);
                _butterSaltPct = p < 0.72 ? 0 : Math.Clamp(0.045 + Math.Sin(p * Math.PI * 3) * 0.012, 0.02, 0.08);
                _butterWorkingPressureKPa = p < 0.72 ? 0 : 95.0 + (p - 0.72) / 0.28 * 80.0 + Math.Sin(p * Math.PI * 4) * 6.0;
                if (p > 0.28) _factoryRunQualityPct -= Math.Abs(_creamFatPct - 39.0) * 0.45;
                if (p > 0.38) _factoryRunQualityPct -= Math.Abs(_creamPasteurizerTemperatureC - 76.0) * 0.32 + Math.Max(0, 16.0 - _creamPasteurizationHoldSeconds) * 1.4;
                if (p > 0.56) _factoryRunQualityPct -= Math.Abs(_butterChurnTemperatureC - 9.8) * 1.2;
                if (p > 0.82) _factoryRunQualityPct -= Math.Abs(_butterFatPct - 82.0) * 1.15 + Math.Abs(_butterMoisturePct - 16.1) * 2.4 + Math.Max(0, _butterSaltPct - 0.10) * 40.0;
                break;
            case IngredientFactoryKind.Vanilla:
                _vanillaExtractorTemperatureC = 32.0 + p * 56.0;
                _vanillaExtractStrengthPct = Math.Clamp(18.0 + p * 77.0, 0, 100);
                if (p > 0.35) _factoryRunQualityPct -= Math.Abs(_vanillaExtractorTemperatureC - 82.0) * 0.10;
                if (p > 0.62) _factoryRunQualityPct -= Math.Max(0, 88.0 - _vanillaExtractStrengthPct) * 0.22;
                break;
            case IngredientFactoryKind.Cocoa:
                _cocoaRoasterTemperatureC = p < 0.34 ? 24.0 + p * 345.0 : 136.0 + Math.Sin(p * Math.PI * 3) * 5.5;
                _cocoaRoasterAirflowM3Min = p < 0.12 ? 8.0 * p / 0.12 : 8.0 + Math.Sin(p * Math.PI * 4) * 1.1;
                _cocoaRoastDevelopmentPct = Math.Clamp(p < 0.20 ? 0 : (p - 0.20) / 0.28 * 100.0, 0, 100);
                _cocoaWinnowerEfficiencyPct = p < 0.46 ? 0 : Math.Clamp(86.0 + (p - 0.46) / 0.18 * 10.5, 0, 98.5);
                _cocoaNibYieldPct = p < 0.48 ? 0 : Math.Clamp(70.0 + (p - 0.48) / 0.18 * 9.0, 0, 82);
                _cocoaPressPressureBar = p < 0.66 ? 0 : Math.Clamp(110.0 + (p - 0.66) / 0.18 * 330.0 + Math.Sin(p * Math.PI * 5) * 12.0, 0, 455);
                _cocoaGrindMicrons = p < 0.76 ? 0 : Math.Clamp(132.0 - (p - 0.76) / 0.24 * 58.0 + Math.Sin(p * Math.PI * 5) * 2.5, 62, 145);
                _cocoaPowderFatPct = p < 0.70 ? 0 : Math.Clamp(13.6 - (p - 0.70) / 0.30 * 2.3 + Math.Sin(p * Math.PI * 4) * 0.12, 10.4, 14.2);
                _cocoaPowderMoisturePct = p < 0.58 ? 0 : Math.Clamp(_cocoaBeanMoisturePct - (p - 0.58) / 0.42 * 2.7 + Math.Sin(p * Math.PI * 4) * 0.10, 3.0, 7.4);
                if (p > 0.36) _factoryRunQualityPct -= Math.Abs(_cocoaRoasterTemperatureC - 136.0) * 0.06 + Math.Abs(_cocoaRoastDevelopmentPct - 92.0) * 0.045;
                if (p > 0.55) _factoryRunQualityPct -= Math.Max(0, 86.0 - _cocoaFermentationPct) * 0.22 + Math.Abs(_cocoaBeanMoisturePct - 6.8) * 0.9 + Math.Max(0, 94.0 - _cocoaWinnowerEfficiencyPct) * 0.22;
                if (p > 0.80) _factoryRunQualityPct -= Math.Abs(_cocoaPowderFatPct - 11.4) * 1.2 + Math.Abs(_cocoaPowderMoisturePct - 4.2) * 0.85 + Math.Max(0, _cocoaGrindMicrons - 82.0) * 0.18;
                break;
            case IngredientFactoryKind.Salt:
                _brineClarifierTurbidityNtu = Math.Clamp(_brineTurbidityNtu * (1.0 - Math.Min(p / 0.28, 1.0) * 0.82), 0.4, _brineTurbidityNtu);
                _saltEvaporatorVacuumKPa = p < 0.22 ? 0 : Math.Clamp(34.0 + (p - 0.22) / 0.78 * 31.0 + Math.Sin(p * Math.PI * 4) * 1.8, 0, 68);
                _saltCrystallizerTemperatureC = p < 0.28 ? 28.0 + p * 70.0 : 72.0 + Math.Sin(p * Math.PI * 3) * 4.5;
                _saltCentrifugeRpm = p < 0.66 ? 0 : Math.Clamp(840.0 + (p - 0.66) / 0.34 * 590.0 + Math.Sin(p * Math.PI * 5) * 35.0, 0, 1500);
                _saltDryerTemperatureC = p < 0.74 ? 28.0 + p * 78.0 : 84.0 + Math.Sin(p * Math.PI * 4) * 3.0;
                _saltMoisturePct = Math.Clamp(0.34 - p * 0.24 + Math.Sin(p * Math.PI * 4) * 0.012, 0.055, 0.36);
                _saltPurityPct = Math.Clamp(96.8 + p * 2.9 - _brineHardnessPpm / 4200.0 - _brineClarifierTurbidityNtu * 0.018, 96.0, 99.9);
                _saltScreenPassingPct = p < 0.78 ? 0 : Math.Clamp(86.0 + (p - 0.78) / 0.22 * 11.5, 0, 99.5);
                if (p > 0.32) _factoryRunQualityPct -= Math.Abs(_brineSalinityPct - 2.7) * 6.0 + Math.Max(0, _brineClarifierTurbidityNtu - 2.5) * 1.2;
                if (p > 0.54) _factoryRunQualityPct -= Math.Abs(_saltEvaporatorVacuumKPa - 58.0) * 0.12 + Math.Max(0, _brineHardnessPpm - 520.0) * 0.018;
                if (p > 0.78) _factoryRunQualityPct -= Math.Abs(_saltMoisturePct - 0.10) * 36.0 + Math.Max(0, 99.0 - _saltPurityPct) * 1.5 + Math.Max(0, 94.0 - _saltScreenPassingPct) * 0.35;
                break;
            case IngredientFactoryKind.Starch:
                _starchSlurryBrix = 8.0 + p * 22.0;
                _starchDryerTemperatureC = p < 0.58 ? 28.0 + p * 108.0 : 91.0 - (p - 0.58) / 0.42 * 23.0;
                _starchMoisturePct = Math.Clamp(18.5 - p * 6.7 + Math.Sin(p * Math.PI * 4) * 0.35, 10.8, 19.2);
                if (p > 0.44) _factoryRunQualityPct -= Math.Abs(_starchSlurryBrix - 25.0) * 0.14;
                if (p > 0.65) _factoryRunQualityPct -= Math.Abs(_starchMoisturePct - 12.0) * 1.5;
                break;
            case IngredientFactoryKind.Leavening:
                _leaveningMixerRpm = p < 0.12 ? 90.0 * p / 0.12 : 90.0 + Math.Sin(p * Math.PI * 4) * 12.0;
                _leaveningHomogeneityPct = Math.Clamp(48.0 + p * 50.0, 0, 100);
                _leaveningBlendMoisturePct = Math.Clamp(3.3 - p * 0.75 + Math.Sin(p * Math.PI * 4) * 0.08, 2.3, 3.4);
                _leaveningSifterLoadPct = p < 0.48 ? 0 : Math.Clamp(58.0 + (p - 0.48) / 0.52 * 24.0 + Math.Sin(p * Math.PI * 3) * 3.0, 0, 92);
                _leaveningDustCollectorPressurePa = Math.Clamp(145.0 + p * 90.0 + Math.Sin(p * Math.PI * 5) * 8.0, 120, 255);
                _factoryRunQualityPct = Math.Min(_factoryRunQualityPct, _leaveningHomogeneityPct);
                if (p > 0.24) _factoryRunQualityPct -= Math.Abs(_sodaAshAssayPct - 99.2) * 1.8 + Math.Abs(_phosphateAcidValuePct - 98.6) * 1.1;
                if (p > 0.58) _factoryRunQualityPct -= Math.Abs(_leaveningBlendMoisturePct - 2.6) * 4.2 + Math.Max(0, _leaveningDustCollectorPressurePa - 238.0) * 0.08;
                if (p > 0.75) _factoryRunQualityPct -= Math.Max(0, _leaveningSifterLoadPct - 86.0) * 0.55;
                break;
            case IngredientFactoryKind.Packaging:
                _cartonBoardCaliperMm = Math.Clamp(0.415 + Math.Sin(p * Math.PI * 2.0) * 0.012, 0.38, 0.46);
                _cartonBoardMoisturePct = Math.Clamp(6.7 - p * 0.45 + Math.Sin(p * Math.PI * 3) * 0.16, 5.8, 7.2);
                _labelWebTensionN = p < 0.18 ? 0 : Math.Clamp(66.0 + Math.Sin(p * Math.PI * 4) * 5.5, 52, 78);
                _cartonFormerSpeedCpm = p < 0.18 ? 122.0 * p / 0.18 : 120.0 + Math.Sin(p * Math.PI * 5) * 10.0;
                _cartonDieCutWastePct = p < 0.20 ? 0 : Math.Clamp(8.6 - (p - 0.20) / 0.80 * 2.7 + Math.Sin(p * Math.PI * 5) * 0.18, 5.4, 8.8);
                _printRegistrationMm = p < 0.36
                    ? Math.Clamp(0.62 - p * 1.05, 0.18, 0.70)
                    : Math.Clamp(0.14 - (p - 0.36) / 0.64 * 0.07 + Math.Sin(p * Math.PI * 4) * 0.010, 0.035, 0.16);
                _gluePotTemperatureC = p < 0.58 ? 34.0 + p / 0.58 * 118.0 : 152.0 + Math.Sin(p * Math.PI * 4) * 3.0;
                _glueBeadGPerCarton = p < 0.62 ? 0 : Math.Clamp(1.02 + Math.Sin(p * Math.PI * 5) * 0.08, 0.82, 1.18);
                _caseCodeReadRatePct = p < 0.72 ? 0 : Math.Clamp(93.0 + (p - 0.72) / 0.28 * 6.5 - Math.Max(0, _printRegistrationMm - 0.10) * 20.0, 88, 99.8);
                if (p > 0.28) _factoryRunQualityPct -= Math.Abs(_cartonBoardCaliperMm - 0.42) * 42.0 + Math.Abs(_cartonBoardMoisturePct - 6.4) * 0.55;
                if (p > 0.45) _factoryRunQualityPct -= Math.Abs(_printRegistrationMm - 0.08) * 24.0 + Math.Abs(_labelWebTensionN - 66.0) * 0.035;
                if (p > 0.62) _factoryRunQualityPct -= Math.Abs(_gluePotTemperatureC - 152.0) * 0.055 + Math.Abs(_glueBeadGPerCarton - 1.02) * 4.8 + Math.Max(0, _cartonDieCutWastePct - 6.3) * 0.45;
                if (p > 0.82) _factoryRunQualityPct -= Math.Max(0, 98.0 - _caseCodeReadRatePct) * 0.75;
                break;
            case IngredientFactoryKind.Icing:
                _icingMixerRpm = p < 0.16 ? 110.0 * p / 0.16 : 110.0 + Math.Sin(p * Math.PI * 5) * 18.0;
                _icingTemperatureC = p < 0.42 ? 24.0 + p * 92.0 : 62.0 - (p - 0.42) / 0.58 * 39.0;
                _icingViscosityPaS = Math.Clamp(12.5 - p * 5.9 + Math.Sin(p * Math.PI * 4) * 0.35, 5.8, 13.0);
                if (p > 0.45) _factoryRunQualityPct -= Math.Abs(_icingTemperatureC - 45.0) * 0.06;
                if (p > 0.60) _factoryRunQualityPct -= Math.Abs(_icingViscosityPaS - 6.6) * 1.2;
                break;
            case IngredientFactoryKind.Feed:
                _feedMillHammerRpm = p < 0.18 ? 3600.0 * p / 0.18 : 3600.0 + Math.Sin(p * Math.PI * 5) * 140.0;
                _feedPelletTemperatureC = p < 0.52 ? 24.0 + p * 118.0 : 84.0 - (p - 0.52) / 0.48 * 46.0;
                _feedMoisturePct = Math.Clamp(14.2 - p * 3.0 + Math.Sin(p * Math.PI * 4) * 0.25, 10.4, 14.8);
                if (p > 0.42) _factoryRunQualityPct -= Math.Abs(_feedPelletTemperatureC - 80.0) * 0.05;
                if (p > 0.65) _factoryRunQualityPct -= Math.Abs(_feedMoisturePct - 11.2) * 1.4;
                break;
            case IngredientFactoryKind.Fertilizer:
                _compostTemperatureC = p < 0.30 ? 32.0 + p * 78.0 : 56.0 + Math.Sin(p * Math.PI * 2.5) * 4.5;
                _compostMoisturePct = Math.Clamp(42.0 - p * 7.5 + Math.Sin(p * Math.PI * 3) * 1.0, 31.0, 46.0);
                _compostAerationPct = Math.Clamp(p * 105.0, 0, 100);
                if (p > 0.35) _factoryRunQualityPct -= Math.Abs(_compostTemperatureC - 58.0) * 0.08;
                if (p > 0.55) _factoryRunQualityPct -= Math.Abs(_compostMoisturePct - 35.0) * 0.55;
                break;
            case IngredientFactoryKind.Bedding:
                _beddingChopperRpm = p < 0.16 ? 1850.0 * p / 0.16 : 1850.0 + Math.Sin(p * Math.PI * 5) * 95.0;
                _beddingMoisturePct = Math.Clamp(12.6 - p * 1.6 + Math.Sin(p * Math.PI * 3) * 0.35, 9.6, 13.2);
                _beddingDustPct = Math.Clamp(8.5 - p * 5.6 + Math.Sin(p * Math.PI * 4) * 0.45, 1.8, 9.0);
                if (p > 0.45) _factoryRunQualityPct -= Math.Abs(_beddingMoisturePct - 11.0) * 0.75;
                if (p > 0.55) _factoryRunQualityPct -= Math.Max(0, _beddingDustPct - 3.0) * 1.1;
                break;
            case IngredientFactoryKind.MineralPremix:
                _mineralMixerRpm = p < 0.18 ? 92.0 * p / 0.18 : 92.0 + Math.Sin(p * Math.PI * 4) * 10.0;
                _mineralHomogeneityPct = Math.Clamp(44.0 + p * 54.0, 0, 100);
                _mineralMetalPpm = Math.Clamp(38.0 - p * 30.0 + Math.Sin(p * Math.PI * 5) * 1.6, 4.0, 42.0);
                _factoryRunQualityPct = Math.Min(_factoryRunQualityPct, _mineralHomogeneityPct);
                if (p > 0.55) _factoryRunQualityPct -= Math.Max(0, _mineralMetalPpm - 10.0) * 1.0;
                break;
        }
        _factoryRunQualityPct = Math.Clamp(_factoryRunQualityPct, 0, 100);
    }

    private static string FactoryPhase(IngredientFactoryRun run)
    {
        double p = run.Progress;
        return run.Kind switch
        {
            IngredientFactoryKind.Mill when p < 0.16 => "scalper aspiration and magnet check",
            IngredientFactoryKind.Mill when p < 0.32 => "tempering water addition and rest",
            IngredientFactoryKind.Mill when p < 0.56 => "break rolling and stock grading",
            IngredientFactoryKind.Mill when p < 0.78 => "plansifter separation",
            IngredientFactoryKind.Mill => "purifier, ash sample and flour-bin transfer",
            IngredientFactoryKind.Sugar when p < 0.16 => "flume wash, stone trap and beet slicing",
            IngredientFactoryKind.Sugar when p < 0.34 => "cossette diffusion and raw juice screen",
            IngredientFactoryKind.Sugar when p < 0.52 => "lime defecation, carbonation and filter press",
            IngredientFactoryKind.Sugar when p < 0.70 => "multiple-effect evaporation",
            IngredientFactoryKind.Sugar when p < 0.86 => "vacuum pan crystallization",
            IngredientFactoryKind.Sugar => "centrifuge, dryer, color sample and silo transfer",
            IngredientFactoryKind.Milk when p < 0.18 => "raw balance tank and filter check",
            IngredientFactoryKind.Milk when p < 0.42 => "HTST heat-up",
            IngredientFactoryKind.Milk when p < 0.68 => "72 degC holding tube",
            IngredientFactoryKind.Milk when p < 0.86 => "homogenization",
            IngredientFactoryKind.Milk => "plate-cooler and sterile tank transfer",
            IngredientFactoryKind.Butter when p < 0.18 => "separator bowl spin-up and cream split",
            IngredientFactoryKind.Butter when p < 0.38 => "cream HTST pasteurization hold",
            IngredientFactoryKind.Butter when p < 0.54 => "cream cooling and aging tank",
            IngredientFactoryKind.Butter when p < 0.72 => "churning to butter granules",
            IngredientFactoryKind.Butter when p < 0.88 => "buttermilk drain, wash and moisture trim",
            IngredientFactoryKind.Butter => "working, metal check and cold-room transfer",
            IngredientFactoryKind.Vanilla when p < 0.18 => "bean grading and blanch",
            IngredientFactoryKind.Vanilla when p < 0.42 => "conditioning and chopping",
            IngredientFactoryKind.Vanilla when p < 0.72 => "hot extraction",
            IngredientFactoryKind.Vanilla when p < 0.90 => "polish filtration",
            IngredientFactoryKind.Vanilla => "extract strength sample and tank transfer",
            IngredientFactoryKind.Cocoa when p < 0.20 => "bean lot assay, roaster charge and airflow check",
            IngredientFactoryKind.Cocoa when p < 0.44 => "roast development and moisture drive-off",
            IngredientFactoryKind.Cocoa when p < 0.62 => "crack, aspirate and winnow shell separation",
            IngredientFactoryKind.Cocoa when p < 0.76 => "nib milling to cocoa liquor",
            IngredientFactoryKind.Cocoa when p < 0.90 => "hydraulic press cake and cocoa butter separation",
            IngredientFactoryKind.Cocoa => "pin mill, sieve and fat/moisture QA sample",
            IngredientFactoryKind.Salt when p < 0.18 => "brine source-lot assay and prefilter",
            IngredientFactoryKind.Salt when p < 0.36 => "lime softening and clarifier settling",
            IngredientFactoryKind.Salt when p < 0.62 => "vacuum pan evaporation",
            IngredientFactoryKind.Salt when p < 0.78 => "crystallizer crop growth",
            IngredientFactoryKind.Salt when p < 0.92 => "centrifuge spin and dryer bed",
            IngredientFactoryKind.Salt => "sieve classification and metal check",
            IngredientFactoryKind.Starch when p < 0.18 => "grain steep and wet mill",
            IngredientFactoryKind.Starch when p < 0.42 => "slurry screening and germ separation",
            IngredientFactoryKind.Starch when p < 0.68 => "centrifuge wash and dewatering",
            IngredientFactoryKind.Starch when p < 0.88 => "flash drying and moisture trim",
            IngredientFactoryKind.Starch => "sieve check and starch-bin transfer",
            IngredientFactoryKind.Leavening when p < 0.16 => "barcode lot verification and dehumidified room check",
            IngredientFactoryKind.Leavening when p < 0.36 => "baking soda and phosphate micro-weighing",
            IngredientFactoryKind.Leavening when p < 0.64 => "starch carrier dosing and ribbon blend",
            IngredientFactoryKind.Leavening when p < 0.84 => "dust collection and sifter classification",
            IngredientFactoryKind.Leavening => "CO2 release assay and bin discharge",
            IngredientFactoryKind.Packaging when p < 0.16 => "paperboard lot assay, web splice and unwind tension",
            IngredientFactoryKind.Packaging when p < 0.34 => "scoring, die cutting and blank stripping",
            IngredientFactoryKind.Packaging when p < 0.54 => "forming plows and compression belt",
            IngredientFactoryKind.Packaging when p < 0.72 => "label web registration and ink cure",
            IngredientFactoryKind.Packaging when p < 0.90 => "hot-melt glue bead, flap compression and code vision",
            IngredientFactoryKind.Packaging => "case count, seal integrity and QA sample pull",
            IngredientFactoryKind.Icing when p < 0.18 => "micro scale weigh-up",
            IngredientFactoryKind.Icing when p < 0.42 => "sugar syrup cook and butter emulsification",
            IngredientFactoryKind.Icing when p < 0.70 => "tempering jacket cooldown",
            IngredientFactoryKind.Icing when p < 0.88 => "viscosity trim and cocoa blend",
            IngredientFactoryKind.Icing => "hopper transfer and QA sample pull",
            IngredientFactoryKind.Feed when p < 0.18 => "grain magnet and hammer-mill startup",
            IngredientFactoryKind.Feed when p < 0.42 => "bran, beet pulp and mineral micro-dosing",
            IngredientFactoryKind.Feed when p < 0.68 => "steam conditioning and pelleting",
            IngredientFactoryKind.Feed when p < 0.88 => "crumbler screen and cooler",
            IngredientFactoryKind.Feed => "feed-bin transfer and mycotoxin sample pull",
            IngredientFactoryKind.Fertilizer when p < 0.18 => "manure receiving and carbon mix",
            IngredientFactoryKind.Fertilizer when p < 0.44 => "aerated thermophilic composting",
            IngredientFactoryKind.Fertilizer when p < 0.70 => "moisture trim and curing",
            IngredientFactoryKind.Fertilizer when p < 0.88 => "trommel screening",
            IngredientFactoryKind.Fertilizer => "fertilizer bagging and soil QA sample pull",
            IngredientFactoryKind.Bedding when p < 0.20 => "straw bale dewire and metal detection",
            IngredientFactoryKind.Bedding when p < 0.48 => "hammer chopping",
            IngredientFactoryKind.Bedding when p < 0.72 => "dedusting cyclone and mist trim",
            IngredientFactoryKind.Bedding when p < 0.90 => "screening and density check",
            IngredientFactoryKind.Bedding => "bale press and barn hygiene sample pull",
            IngredientFactoryKind.MineralPremix when p < 0.20 => "limestone and trace-mineral weigh-up",
            IngredientFactoryKind.MineralPremix when p < 0.48 => "micro-dosing phosphate and salt",
            IngredientFactoryKind.MineralPremix when p < 0.74 => "ribbon blending and magnet pass",
            IngredientFactoryKind.MineralPremix when p < 0.90 => "sieve check and bagger fill",
            IngredientFactoryKind.MineralPremix => "premix assay and QA sample pull",
            _ => "processing",
        };
    }

    private IngredientFactoryEquipment EquipmentFor(IngredientFactoryKind kind) => _factoryEquipment[kind];

    private static string FactoryName(IngredientFactoryKind kind) => kind switch
    {
        IngredientFactoryKind.Mill => "roller mill",
        IngredientFactoryKind.Sugar => "sugar house",
        IngredientFactoryKind.Milk => "milk pasteurizer",
        IngredientFactoryKind.Butter => "butter room",
        IngredientFactoryKind.Vanilla => "vanilla extraction line",
        IngredientFactoryKind.Cocoa => "cocoa line",
        IngredientFactoryKind.Salt => "salt works",
        IngredientFactoryKind.Starch => "starch wet mill",
        IngredientFactoryKind.Leavening => "leavening plant",
        IngredientFactoryKind.Packaging => "packaging plant",
        IngredientFactoryKind.Icing => "icing tempering kitchen",
        IngredientFactoryKind.Feed => "poultry feed mill",
        IngredientFactoryKind.Fertilizer => "compost fertilizer plant",
        IngredientFactoryKind.Bedding => "straw bedding chopper",
        IngredientFactoryKind.MineralPremix => "mineral premix plant",
        _ => "ingredient plant",
    };

    private static string FactoryProductName(IngredientFactoryKind kind) => kind switch
    {
        IngredientFactoryKind.Mill => "cake flour",
        IngredientFactoryKind.Sugar => "sugar",
        IngredientFactoryKind.Milk => "pasteurized milk",
        IngredientFactoryKind.Butter => "butter",
        IngredientFactoryKind.Vanilla => "vanilla extract",
        IngredientFactoryKind.Cocoa => "cocoa",
        IngredientFactoryKind.Salt => "baking salt",
        IngredientFactoryKind.Starch => "starch carrier",
        IngredientFactoryKind.Leavening => "baking powder",
        IngredientFactoryKind.Packaging => "cake cartons",
        IngredientFactoryKind.Icing => "prepared icing",
        IngredientFactoryKind.Feed => "poultry feed",
        IngredientFactoryKind.Fertilizer => "crop fertilizer",
        IngredientFactoryKind.Bedding => "livestock bedding",
        IngredientFactoryKind.MineralPremix => "dairy mineral premix",
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
        _ingredientLabStatus = run.Kind switch
        {
            IngredientFactoryKind.Milk => $"QA lab hold: {_pendingLabProductName} lot {_pendingLabLotId} awaiting phosphatase, micro, temperature and label checks.",
            IngredientFactoryKind.Mill => $"QA lab hold: {_pendingLabProductName} lot {_pendingLabLotId} awaiting moisture, ash, protein, sieve and micro checks.",
            IngredientFactoryKind.Sugar => $"QA lab hold: {_pendingLabProductName} lot {_pendingLabLotId} awaiting polarization, color, moisture, insoluble solids and label checks.",
            IngredientFactoryKind.Butter => $"QA lab hold: {_pendingLabProductName} lot {_pendingLabLotId} awaiting fat, moisture, salt, micro, temperature and label checks.",
            IngredientFactoryKind.Cocoa => $"QA lab hold: {_pendingLabProductName} lot {_pendingLabLotId} awaiting roast profile, shell, fat, moisture, grind, micro and label checks.",
            IngredientFactoryKind.Salt => $"QA lab hold: {_pendingLabProductName} lot {_pendingLabLotId} awaiting purity, moisture, insoluble matter, screen sizing, metal and label checks.",
            IngredientFactoryKind.Leavening => $"QA lab hold: {_pendingLabProductName} lot {_pendingLabLotId} awaiting CO2 release, neutralization, moisture, homogeneity, sieve and label checks.",
            IngredientFactoryKind.Packaging => $"QA lab hold: {_pendingLabProductName} lot {_pendingLabLotId} awaiting board caliper/moisture, die-cut waste, glue bead, code vision, seal and label checks.",
            _ => $"QA lab hold: {_pendingLabProductName} lot {_pendingLabLotId} awaiting moisture, sieve, micro and label checks.",
        };
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
            case IngredientFactoryKind.Milk:
                ConsumeTrackedStock(ref _milkL, quantity, ref _milkLotId);
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
            case IngredientFactoryKind.Starch:
                ConsumeTrackedStock(ref _starchKg, quantity, ref _starchLotId);
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
            case IngredientFactoryKind.Feed:
                ConsumeTrackedStock(ref _animalFeedKg, quantity, ref _feedLotId);
                break;
            case IngredientFactoryKind.Fertilizer:
                ConsumeTrackedStock(ref _fertilizerKg, quantity, ref _fertilizerLotId);
                break;
            case IngredientFactoryKind.Bedding:
                ConsumeTrackedStock(ref _beddingKg, quantity, ref _beddingLotId);
                break;
            case IngredientFactoryKind.MineralPremix:
                ConsumeTrackedStock(ref _dairyMineralKg, quantity, ref _dairyMineralLotId);
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
                detail = $"{bran:0.0} kg bran and {run.Waste:0.0} kg scalper screenings/germ dust";
                break;
            case IngredientFactoryKind.Sugar:
                double pulp = run.PrimaryInput * 0.34;
                double molasses = run.PrimaryInput * Math.Clamp(_sugarCropSugarPct / 100.0 * 0.18, 0.022, 0.038);
                _beetPulpKg += pulp;
                _molassesKg += molasses;
                detail = $"{pulp:0.0} kg beet pulp and {molasses:0.0} kg molasses";
                break;
            case IngredientFactoryKind.Milk:
                double milkSolids = run.PrimaryInput * 0.012;
                detail = $"{milkSolids:0.0} kg separator solids, filter soil and plate-pasteurizer rinse";
                break;
            case IngredientFactoryKind.Butter:
                double skim = Math.Max(0, run.PrimaryInput - run.SecondaryInput);
                double buttermilk = run.SecondaryInput * 0.55;
                _skimMilkL += skim;
                _buttermilkL += buttermilk;
                detail = $"{skim:0.0} L separator skim milk and {buttermilk:0.0} L churn buttermilk";
                break;
            case IngredientFactoryKind.Vanilla:
                double pomace = run.PrimaryInput * 0.32;
                _vanillaPomaceKg += pomace;
                detail = $"{pomace:0.0} kg vanilla bean pomace";
                break;
            case IngredientFactoryKind.Cocoa:
                double shell = run.PrimaryInput * 0.14;
                double cocoaButter = run.PrimaryInput * 0.22;
                _cocoaShellKg += shell;
                _cocoaButterKg += cocoaButter;
                detail = $"{shell:0.0} kg cocoa shell and {cocoaButter:0.0} kg separated cocoa butter";
                break;
            case IngredientFactoryKind.Salt:
                double blowdown = run.PrimaryInput * 0.16;
                _brineBlowdownL += blowdown;
                detail = $"{blowdown:0.0} L brine blowdown";
                break;
            case IngredientFactoryKind.Starch:
                double fiber = run.PrimaryInput * 0.18;
                _branKg += fiber;
                detail = $"{fiber:0.0} kg starch fiber and screenings";
                break;
            case IngredientFactoryKind.Leavening:
                double dust = run.Product * 0.025;
                _leaveningDustKg += dust;
                detail = $"{dust:0.0} kg leavening dust";
                break;
            case IngredientFactoryKind.Packaging:
                double cartonTrim = run.PrimaryInput * 0.065;
                double labelMatrix = run.SecondaryInput * 0.010;
                _packagingTrimKg += cartonTrim + labelMatrix;
                detail = $"{cartonTrim:0.0} kg carton trim and {labelMatrix:0.0} kg label matrix scrap";
                break;
            case IngredientFactoryKind.Icing:
                double kettleRinse = run.Product * 0.05;
                _factoryEffluentL = Math.Min(FactoryEffluentCapacityL, _factoryEffluentL + kettleRinse * 6.0);
                detail = $"{run.Waste:0.0} kg icing smear plus {kettleRinse * 6.0:0} L kettle rinse";
                break;
            case IngredientFactoryKind.Feed:
                detail = $"{run.Waste:0.0} kg feed screenings";
                break;
            case IngredientFactoryKind.Fertilizer:
                detail = $"{run.TertiaryInput:0.0} kg factory organics composted with manure and {run.Waste:0.0} kg screening rejects";
                break;
            case IngredientFactoryKind.Bedding:
                detail = $"{run.Waste:0.0} kg straw fines and dust collector sweepings";
                break;
            case IngredientFactoryKind.MineralPremix:
                detail = $"{run.Waste:0.0} kg mineral dust and screen rejects";
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
                _wheatTemperMoisturePct = Math.Clamp(15.0 + _rng.NextDouble() * 0.4, 14.7, 15.5);
                _millSifterLoadPct = Math.Clamp(76.0 + _wheatForeignMaterialPct * 3.2 + _rng.NextDouble() * 5.0, 70, 96);
                _flourExtractionPct = Math.Clamp(75.0 - Math.Max(0, _wheatForeignMaterialPct - 0.7) * 1.3 + _rng.NextDouble() * 1.4, 69, 78);
                _flourMoisturePct = Math.Clamp(_wheatTemperMoisturePct - 1.45 + _rng.NextDouble() * 0.25, 12.4, 14.4);
                _flourAshPct = Math.Clamp(0.36 + _millRollGapMm * 0.20 + _wheatForeignMaterialPct * 0.016 + _rng.NextDouble() * 0.025, 0.36, 0.58);
                _flourProteinPct = Math.Clamp(_wheatProteinPct - 0.25 + _rng.NextDouble() * 0.12, 7.5, 10.0);
                _factoryRunQualityPct = Math.Clamp(98
                    - Math.Abs(_millRollGapMm - 0.30) * 120
                    - Math.Abs(_wheatTemperMoisturePct - 15.2) * 1.8
                    - Math.Max(0, _millSifterLoadPct - 88.0) * 0.5
                    - Math.Abs(_flourAshPct - 0.44) * 36.0
                    - Math.Max(0, _flourMoisturePct - 14.2) * 2.2
                    - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Roller mill completed: {output:0.0} kg cake flour from cleaned/tempered wheat lot {run.InputLotId}; temper {_wheatTemperMoisturePct:0.0}% moisture, {_millSifterLoadPct:0}% sifter load, extraction {_flourExtractionPct:0.0}%, flour {_flourMoisturePct:0.0}% moisture, {_flourAshPct:0.00}% ash and {_flourProteinPct:0.0}% protein, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Sugar:
                _sugarKg += output;
                _sugarLotId = run.OutputLotId;
                _wasteKg += waste;
                _sugarJuiceBrix = Math.Clamp(68.0 + _rng.NextDouble() * 1.8, 66.5, 70.5);
                _sugarEvaporatorTemperatureC = 101.0 + _rng.NextDouble() * 4.0;
                _sugarJuicePurityPct = Math.Clamp(89.5 - _sugarCropSoilTarePct * 0.42 + _rng.NextDouble() * 1.3, 82, 92);
                _sugarLimePh = 8.25 + _rng.NextDouble() * 0.22;
                _sugarPanVacuumKPa = 61.0 + _rng.NextDouble() * 6.0;
                _sugarCentrifugeRpm = 1140.0 + _rng.NextDouble() * 180.0;
                _sugarMoisturePct = 0.035 + _rng.NextDouble() * 0.035;
                _sugarColorIcumsa = Math.Clamp(36.0 + _sugarCropSoilTarePct * 2.2 + Math.Max(0, 16.0 - _sugarCropSugarPct) * 3.0 + _rng.NextDouble() * 7.0, 30, 85);
                _sugarPolarizationPct = Math.Clamp(99.72 + _rng.NextDouble() * 0.10 - Math.Max(0, _sugarColorIcumsa - 60.0) * 0.0015, 99.60, 99.90);
                _factoryRunQualityPct = Math.Clamp(98
                    - Math.Abs(_sugarJuiceBrix - 68.5) * 1.15
                    - Math.Max(0, 88.0 - _sugarJuicePurityPct) * 0.75
                    - Math.Abs(_sugarLimePh - 8.35) * 4.0
                    - Math.Max(0, _sugarColorIcumsa - 62.0) * 0.25
                    - Math.Max(0, _sugarMoisturePct - 0.07) * 180.0
                    - Math.Max(0, 99.70 - _sugarPolarizationPct) * 22.0
                    - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Sugar house completed: {output:0.0} kg crystallized sugar from purified crop lot {run.InputLotId}; juice {_sugarJuicePurityPct:0.0}% purity at {_sugarJuiceBrix:0.0} Brix, pH {_sugarLimePh:0.00}, pan vacuum {_sugarPanVacuumKPa:0} kPa, centrifuge {_sugarCentrifugeRpm:0} rpm, sugar {_sugarMoisturePct:0.000}% moisture, {_sugarColorIcumsa:0} ICUMSA and {_sugarPolarizationPct:0.00}% polarization, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Milk:
                _milkL += output;
                _milkLotId = run.OutputLotId;
                _wasteKg += waste;
                _milkPasteurizerTemperatureC = 71.8 + _rng.NextDouble() * 1.2;
                _milkHomogenizerPressureBar = 126 + _rng.NextDouble() * 10;
                _milkPasteurizationHoldSeconds = 16.0 + _rng.NextDouble() * 2.4;
                _milkMicroLogReduction = 5.1 + _rng.NextDouble() * 0.8;
                _factoryRunQualityPct = Math.Clamp(99
                    - Math.Abs(_milkPasteurizerTemperatureC - 72.0) * 0.7
                    - Math.Max(0, 15.0 - _milkPasteurizationHoldSeconds) * 1.6
                    - Math.Max(0, 4.8 - _milkMicroLogReduction) * 4.0
                    - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Milk pasteurizer completed: {output:0.0} L pasteurized milk from raw cow milk, {_milkPasteurizerTemperatureC:0.0} degC HTST, {_milkPasteurizationHoldSeconds:0.0}s hold, {_milkHomogenizerPressureBar:0} bar homogenizer and {_milkMicroLogReduction:0.0}-log micro reduction, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Butter:
                _butterKg += output;
                _butterLotId = run.OutputLotId;
                _creamSeparatorRpm = 6420 + _rng.NextDouble() * 360;
                _creamYieldL = run.SecondaryInput;
                _creamFatPct = Math.Clamp(38.4 + (_milkFatPct - 3.8) * 0.55 + _rng.NextDouble() * 1.2, 37.2, 41.5);
                _creamPasteurizerTemperatureC = 75.2 + _rng.NextDouble() * 1.5;
                _creamPasteurizationHoldSeconds = 18.0 + _rng.NextDouble() * 4.0;
                _butterChurnTemperatureC = 8.8 + _rng.NextDouble() * 1.8;
                _butterFatPct = 81.2 + _rng.NextDouble() * 1.8;
                _butterMoisturePct = 15.7 + _rng.NextDouble() * 0.75;
                _butterSaltPct = 0.035 + _rng.NextDouble() * 0.035;
                _butterWorkingPressureKPa = 128 + _rng.NextDouble() * 52;
                _wasteKg += waste;
                _factoryRunQualityPct = Math.Clamp(98
                    - Math.Abs(_creamFatPct - 39.0) * 0.60
                    - Math.Abs(_creamPasteurizerTemperatureC - 76.0) * 0.40
                    - Math.Max(0, 16.0 - _creamPasteurizationHoldSeconds) * 1.5
                    - Math.Abs(_butterChurnTemperatureC - 9.8) * 1.3
                    - Math.Abs(_butterFatPct - 82.0) * 1.2
                    - Math.Abs(_butterMoisturePct - 16.1) * 2.7
                    - Math.Max(0, _butterSaltPct - 0.10) * 40.0
                    - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Butter room completed: {output:0.0} kg sweet-cream baking butter from raw cow milk lot {run.InputLotId}; cream {_creamYieldL:0.0} L at {_creamFatPct:0.0}% fat, pasteurized {_creamPasteurizerTemperatureC:0.0} degC for {_creamPasteurizationHoldSeconds:0.0}s, churn {_butterChurnTemperatureC:0.0} degC, butter {_butterFatPct:0.0}% fat, {_butterMoisturePct:0.0}% moisture and {_butterSaltPct:0.00}% salt, QA {_factoryRunQualityPct:0}%.";
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
                _cocoaRoasterTemperatureC = 132 + _rng.NextDouble() * 8;
                _cocoaRoasterAirflowM3Min = 7.6 + _rng.NextDouble() * 1.8;
                _cocoaRoastDevelopmentPct = 90 + _rng.NextDouble() * 6;
                _cocoaWinnowerEfficiencyPct = 95.0 + _rng.NextDouble() * 2.6;
                _cocoaNibYieldPct = 77.5 + _rng.NextDouble() * 2.8;
                _cocoaPressPressureBar = 388 + _rng.NextDouble() * 48;
                _cocoaGrindMicrons = 66 + _rng.NextDouble() * 16;
                _cocoaPowderFatPct = 10.8 + _rng.NextDouble() * 1.5;
                _cocoaPowderMoisturePct = 3.6 + _rng.NextDouble() * 1.0;
                _factoryRunQualityPct = Math.Clamp(99
                    - Math.Abs(_cocoaRoasterTemperatureC - 136.0) * 0.07
                    - Math.Abs(_cocoaRoastDevelopmentPct - 92.0) * 0.06
                    - Math.Max(0, 94.0 - _cocoaWinnowerEfficiencyPct) * 0.30
                    - Math.Abs(_cocoaPowderFatPct - 11.4) * 1.25
                    - Math.Abs(_cocoaPowderMoisturePct - 4.2) * 0.90
                    - Math.Max(0, _cocoaGrindMicrons - 82.0) * 0.18
                    - Math.Max(0, 86.0 - _cocoaFermentationPct) * 0.25
                    - Math.Abs(_cocoaBeanMoisturePct - 6.8) * 0.85
                    - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Cocoa line completed: {output:0.0} kg cocoa powder from bean lot {run.InputLotId}; beans {_cocoaFermentationPct:0}% fermented and {_cocoaBeanMoisturePct:0.0}% moisture, roast {_cocoaRoasterTemperatureC:0} degC with {_cocoaRoastDevelopmentPct:0}% development, winnower {_cocoaWinnowerEfficiencyPct:0.0}% efficient, nib yield {_cocoaNibYieldPct:0.0}%, press {_cocoaPressPressureBar:0} bar, powder {_cocoaPowderFatPct:0.0}% fat, {_cocoaPowderMoisturePct:0.0}% moisture and {_cocoaGrindMicrons:0} micron grind, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Salt:
                _saltKg += output;
                _saltLotId = run.OutputLotId;
                _wasteKg += waste;
                _brineClarifierTurbidityNtu = Math.Clamp(0.8 + _rng.NextDouble() * 1.2, 0.6, 2.4);
                _saltEvaporatorVacuumKPa = 55 + _rng.NextDouble() * 7;
                _saltCrystallizerTemperatureC = 70 + _rng.NextDouble() * 8;
                _saltCentrifugeRpm = 1320 + _rng.NextDouble() * 150;
                _saltDryerTemperatureC = 82 + _rng.NextDouble() * 8;
                _saltMoisturePct = 0.075 + _rng.NextDouble() * 0.055;
                _saltPurityPct = Math.Clamp(99.55 - Math.Max(0, _brineHardnessPpm - 420.0) * 0.00045 - _brineClarifierTurbidityNtu * 0.035 + _rng.NextDouble() * 0.10, 98.7, 99.9);
                _saltScreenPassingPct = 94.8 + _rng.NextDouble() * 3.4;
                _factoryRunQualityPct = Math.Clamp(99
                    - Math.Abs(_brineSalinityPct - 2.7) * 5.0
                    - Math.Max(0, _brineClarifierTurbidityNtu - 2.2) * 1.4
                    - Math.Abs(_saltEvaporatorVacuumKPa - 58.0) * 0.10
                    - Math.Abs(_saltMoisturePct - 0.10) * 34.0
                    - Math.Max(0, 99.0 - _saltPurityPct) * 1.5
                    - Math.Max(0, 94.0 - _saltScreenPassingPct) * 0.35
                    - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Salt works completed: {output:0.0} kg baking-grade salt from brine lot {run.InputLotId}; brine {_brineSalinityPct:0.0}% salinity, {_brineHardnessPpm:0} ppm hardness and {_brineClarifierTurbidityNtu:0.0} NTU after clarification, vacuum {_saltEvaporatorVacuumKPa:0} kPa, centrifuge {_saltCentrifugeRpm:0} rpm, dryer {_saltDryerTemperatureC:0} degC, salt {_saltPurityPct:0.00}% purity, {_saltMoisturePct:0.000}% moisture and {_saltScreenPassingPct:0.0}% screen pass, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Starch:
                _starchKg += output;
                _starchLotId = run.OutputLotId;
                _wasteKg += waste;
                _starchSlurryBrix = 24.0 + _rng.NextDouble() * 3.0;
                _starchDryerTemperatureC = 66.0 + _rng.NextDouble() * 6.0;
                _starchMoisturePct = 11.5 + _rng.NextDouble() * 1.1;
                _factoryRunQualityPct = Math.Clamp(98 - Math.Abs(_starchMoisturePct - 12.0) * 1.8 - Math.Abs(_starchSlurryBrix - 25.0) * 0.22 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Starch plant completed: {output:0.0} kg starch carrier at {_starchMoisturePct:0.0}% moisture, {_starchSlurryBrix:0.0} Brix slurry and {_starchDryerTemperatureC:0} degC dryer discharge, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Leavening:
                _bakingPowderKg += output;
                _leaveningLotId = run.OutputLotId;
                _wasteKg += waste;
                _sodaAshAssayPct = Math.Clamp(_sodaAshAssayPct + _rng.NextDouble() * 0.12 - 0.06, 98.2, 99.8);
                _phosphateAcidValuePct = Math.Clamp(_phosphateAcidValuePct + _rng.NextDouble() * 0.16 - 0.08, 96.8, 100.0);
                _leaveningMixerRpm = 82 + _rng.NextDouble() * 24;
                _leaveningHomogeneityPct = 96.6 + _rng.NextDouble() * 2.4;
                _leaveningBlendMoisturePct = 2.45 + _rng.NextDouble() * 0.35;
                _leaveningSifterLoadPct = 72 + _rng.NextDouble() * 14;
                _leaveningDustCollectorPressurePa = 190 + _rng.NextDouble() * 38;
                _factoryRunQualityPct = Math.Clamp(99
                    - Math.Abs(_leaveningHomogeneityPct - 98.0) * 0.9
                    - Math.Abs(_leaveningBlendMoisturePct - 2.6) * 4.8
                    - Math.Abs(_sodaAshAssayPct - 99.2) * 1.8
                    - Math.Abs(_phosphateAcidValuePct - 98.6) * 1.1
                    - Math.Max(0, _leaveningSifterLoadPct - 86.0) * 0.55
                    - Math.Max(0, _leaveningDustCollectorPressurePa - 238.0) * 0.08
                    - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Leavening plant completed: {output:0.0} kg baking powder from source lots {run.InputLotId}; baking soda assay {_sodaAshAssayPct:0.0}%, phosphate acid value {_phosphateAcidValuePct:0.0}%, blend moisture {_leaveningBlendMoisturePct:0.0}%, sifter {_leaveningSifterLoadPct:0}% load, dust collector {_leaveningDustCollectorPressurePa:0} Pa, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Packaging:
                _packagingUnits += Math.Floor(output);
                _packagingLotId = run.OutputLotId;
                _wasteKg += waste;
                _cartonBoardCaliperMm = 0.410 + _rng.NextDouble() * 0.022;
                _cartonBoardMoisturePct = 6.05 + _rng.NextDouble() * 0.70;
                _cartonFormerSpeedCpm = 116 + _rng.NextDouble() * 18;
                _cartonDieCutWastePct = 5.7 + _rng.NextDouble() * 0.9;
                _labelWebTensionN = 62 + _rng.NextDouble() * 8;
                _printRegistrationMm = 0.045 + _rng.NextDouble() * 0.075;
                _gluePotTemperatureC = 149 + _rng.NextDouble() * 7;
                _glueBeadGPerCarton = 0.96 + _rng.NextDouble() * 0.14;
                _caseCodeReadRatePct = 98.1 + _rng.NextDouble() * 1.6;
                _factoryRunQualityPct = Math.Clamp(99
                    - Math.Abs(_cartonBoardCaliperMm - 0.42) * 45.0
                    - Math.Abs(_cartonBoardMoisturePct - 6.4) * 0.70
                    - Math.Max(0, _cartonDieCutWastePct - 6.3) * 0.60
                    - Math.Abs(_printRegistrationMm - 0.08) * 28.0
                    - Math.Abs(_labelWebTensionN - 66.0) * 0.04
                    - Math.Abs(_gluePotTemperatureC - 152.0) * 0.08
                    - Math.Abs(_glueBeadGPerCarton - 1.02) * 5.5
                    - Math.Max(0, 98.0 - _caseCodeReadRatePct) * 0.80
                    - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Packaging plant completed: {Math.Floor(output):0} coded cartons from feedstock lots {run.InputLotId}; board {_cartonBoardCaliperMm:0.000} mm caliper at {_cartonBoardMoisturePct:0.0}% moisture, former {_cartonFormerSpeedCpm:0} cpm, die-cut waste {_cartonDieCutWastePct:0.0}%, label tension {_labelWebTensionN:0} N, registration {_printRegistrationMm:0.00} mm, glue {_gluePotTemperatureC:0} degC at {_glueBeadGPerCarton:0.00} g/carton, code vision {_caseCodeReadRatePct:0.0}% read rate, QA {_factoryRunQualityPct:0}%.";
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
            case IngredientFactoryKind.Feed:
                _animalFeedKg += output;
                _feedLotId = run.OutputLotId;
                _wasteKg += waste;
                _feedMillHammerRpm = 3500 + _rng.NextDouble() * 240;
                _feedPelletTemperatureC = 36 + _rng.NextDouble() * 6;
                _feedMoisturePct = 10.8 + _rng.NextDouble() * 0.8;
                _factoryRunQualityPct = Math.Clamp(97 - Math.Abs(_feedMoisturePct - 11.2) * 1.8 - Math.Abs(_feedPelletTemperatureC - 39.0) * 0.12 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Feed mill completed: {output:0.0} kg poultry feed at {_feedMoisturePct:0.0}% moisture and {_feedPelletTemperatureC:0} degC cooler discharge, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Fertilizer:
                _fertilizerKg += output;
                _fertilizerLotId = run.OutputLotId;
                _wasteKg += waste;
                _compostTemperatureC = 56 + _rng.NextDouble() * 5;
                _compostMoisturePct = 33 + _rng.NextDouble() * 4;
                _compostAerationPct = 88 + _rng.NextDouble() * 10;
                _factoryRunQualityPct = Math.Clamp(96 - Math.Abs(_compostMoisturePct - 35.0) * 0.75 - Math.Abs(_compostTemperatureC - 58.0) * 0.16 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Compost plant completed: {output:0.0} kg crop fertilizer at {_compostTemperatureC:0} degC, {_compostMoisturePct:0.0}% moisture and {_compostAerationPct:0}% aeration, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.Bedding:
                _beddingKg += output;
                _beddingLotId = run.OutputLotId;
                _wasteKg += waste;
                _beddingChopperRpm = 1780 + _rng.NextDouble() * 140;
                _beddingMoisturePct = 10.4 + _rng.NextDouble() * 1.2;
                _beddingDustPct = 2.1 + _rng.NextDouble() * 0.9;
                _factoryRunQualityPct = Math.Clamp(98 - Math.Abs(_beddingMoisturePct - 11.0) * 0.9 - Math.Max(0, _beddingDustPct - 3.0) * 1.6 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Bedding plant completed: {output:0.0} kg low-dust livestock bedding at {_beddingMoisturePct:0.0}% moisture, {_beddingDustPct:0.0}% dust and {_beddingChopperRpm:0} rpm, QA {_factoryRunQualityPct:0}%.";
                break;
            case IngredientFactoryKind.MineralPremix:
                _dairyMineralKg += output;
                _dairyMineralLotId = run.OutputLotId;
                _wasteKg += waste;
                _mineralMixerRpm = 78 + _rng.NextDouble() * 22;
                _mineralHomogeneityPct = 96.0 + _rng.NextDouble() * 2.4;
                _mineralMetalPpm = 7.0 + _rng.NextDouble() * 4.0;
                _factoryRunQualityPct = Math.Clamp(_mineralHomogeneityPct - Math.Max(0, _mineralMetalPpm - 10.0) * 1.4 - FactoryEquipmentPenalty(run.Kind), 0, 100);
                _factoryStatus = $"Mineral premix plant completed: {output:0.0} kg dairy mineral premix at {_mineralHomogeneityPct:0.0}% homogeneity, {_mineralMetalPpm:0.0} ppm metal check and {_mineralMixerRpm:0} rpm, QA {_factoryRunQualityPct:0}%.";
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
            CanHarvestFeedCrops = power >= 0.15 && _barnLaborHours >= 0.7 && (_pastureHealth >= 35 || _wheatGrowth >= 55),
            CanHarvestCocoa = power >= 0.15 && _barnLaborHours >= 0.55 && _forkliftBatteryPct >= 6 && _cocoaGrowth >= 35,
            CanCollectDairy = power >= 0.12 && (_dairyReadyL >= 1 || _eggsReady >= 1),
            CanPasteurizeMilk = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _rawMilkL >= 5 && HasFactoryUtilities(80, 160, 14, 0.45)
                                && !string.IsNullOrWhiteSpace(_rawMilkLotId) && MilkQaInSpec(),
            CanMixDairyRation = power >= 0.15 && _forageKg >= 48 && _grainKg >= 22 && _dairyMineralKg >= 2.2 && FactoryLotReleased(_dairyMineralKg, _dairyMineralLotId) && _irrigationWaterL >= 38 && _barnLaborHours >= 0.8,
            CanWashDairyParlor = power >= 0.15 && HasFactoryUtilities(180, 60, 12, 0.5) && _barnLaborHours >= 1.4 && (_dairyParlorHygienePct < 96 || _manureKg > 80),
            CanWashPoultryHouse = power >= 0.15 && HasFactoryUtilities(90, 30, 8, 0.35) && _barnLaborHours >= 1.0 && (_henHouseHygienePct < 96 || _poultryManureKg > 80),
            CanMillWheat = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _wheatKg >= 5 && HasFactoryUtilities(50, 0, 38, 0.6),
            CanRefineSugar = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _sugarCropKg >= 10 && HasFactoryUtilities(520, 610, 22, 1.2),
            CanChurnButter = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _rawMilkL > 35 && HasFactoryUtilities(125, 180, 15, 0.8)
                             && !string.IsNullOrWhiteSpace(_rawMilkLotId) && MilkQaInSpec(),
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
            CanProcessCocoa = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _cocoaBeansKg >= 5 && HasFactoryUtilities(32, 0, 58, 0.95)
                              && !string.IsNullOrWhiteSpace(_cocoaBeansLotId),
            CanRunSaltWorks = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _brineL >= 80 && HasFactoryUtilities(18, 780, 38, 0.65)
                              && !string.IsNullOrWhiteSpace(_brineLotId),
            CanRunStarchPlant = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _grainKg >= 52 && HasFactoryUtilities(85, 140, 16, 0.45)
                                && !string.IsNullOrWhiteSpace(_grainLotId),
            CanRunLeaveningPlant = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _sodaAshKg >= 3 && _phosphateKg >= 3 && _starchKg >= 2 && FactoryLotReleased(_starchKg, _starchLotId) && HasFactoryUtilities(0, 0, 58, 1.3)
                                   && !string.IsNullOrWhiteSpace(_sodaAshLotId) && !string.IsNullOrWhiteSpace(_phosphateLotId) && !string.IsNullOrWhiteSpace(_starchLotId),
            CanRunPackagingPlant = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _paperboardKg >= 42 && _labelStockM >= 140 && _packagingInkL >= 2.4 && _adhesiveKg >= 6 && HasFactoryUtilities(24, 0, 52, 0.60),
            CanPrepareIcing = CanPrepareIcing(recipe, power, labClear, wasteReady),
            CanRunFeedMill = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _grainKg >= 42 && _starchKg >= 4.8 && _dairyMineralKg >= 1.6 && FactoryLotReleased(_dairyMineralKg, _dairyMineralLotId) && FactoryLotReleased(_starchKg, _starchLotId) && HasFactoryUtilities(28, 55, 18, 0.25)
                            && !string.IsNullOrWhiteSpace(_grainLotId) && !string.IsNullOrWhiteSpace(_dairyMineralLotId) && !string.IsNullOrWhiteSpace(_starchLotId),
            CanRunCompostPlant = power >= 0.2 && _factoryRun is null && labClear && CompostPlantInputsReady() && HasFactoryUtilities(70, 0, 34, 0.55),
            CanRunBeddingPlant = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _strawKg >= 54 && HasFactoryUtilities(18, 0, 26, 0.35)
                                 && !string.IsNullOrWhiteSpace(_strawLotId),
            CanRunMineralPlant = power >= 0.2 && _factoryRun is null && labClear && wasteReady && _limestoneKg >= 16 && _traceMineralKg >= 5.5 && _phosphateKg >= 3 && _saltKg >= 4 && FactoryLotReleased(_saltKg, _saltLotId) && HasFactoryUtilities(12, 0, 22, 0.30)
                                  && !string.IsNullOrWhiteSpace(_limestoneLotId) && !string.IsNullOrWhiteSpace(_traceMineralLotId) && !string.IsNullOrWhiteSpace(_phosphateLotId) && !string.IsNullOrWhiteSpace(_saltLotId),
            CanReleaseLabLot = power >= 0.15 && _factoryRun is null && !labClear && HasFactoryUtilities(12, 0, 4, 0.1),
            CanStageBatchKit = _stage == CakeBatchStage.Idle && !CipActive && !_batchKitStaged && rawMissing.Length == 0 && power >= 0.2 && _forkliftBatteryPct >= 12 && _warehousePalletSpacePct >= 18,
            CanServiceFactories = power >= 0.2 && _factoryRun is null && NeedsFactoryService() && HasFactoryUtilities(120, 80, 35, 1.5),
            CanHaulByproducts = power >= 0.12 && ByproductStorageLoadKg() > 10 && _forkliftBatteryPct >= 8 && _warehousePalletSpacePct >= 5,
            CanTreatFactoryEffluent = power >= 0.15 && _factoryEffluentL >= 25 && _compressedAirNm3 >= 24 && _filterMediaPct >= 0.8,
            WheatGrowth = _wheatGrowth,
            BeetGrowth = _beetGrowth,
            PastureHealth = _pastureHealth,
            VanillaGrowth = _vanillaGrowth,
            CocoaGrowth = _cocoaGrowth,
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
            FeedCropStatus = _feedCropStatus,
            CocoaGreenhouseStatus = _cocoaGreenhouseStatus,
            ForageLotId = _forageLotId,
            GrainLotId = _grainLotId,
            MixedRationLotId = _mixedRationLotId,
            DairyMineralLotId = _dairyMineralLotId,
            BeddingLotId = _beddingLotId,
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
            RawMilkLotId = _rawMilkLotId,
            MilkLotId = _milkLotId,
            EggLotId = _eggLotId,
            FlourLotId = _flourLotId,
            SugarLotId = _sugarLotId,
            ButterLotId = _butterLotId,
            CocoaBeansLotId = _cocoaBeansLotId,
            CocoaLotId = _cocoaLotId,
            BrineLotId = _brineLotId,
            SaltLotId = _saltLotId,
            StarchLotId = _starchLotId,
            LeaveningLotId = _leaveningLotId,
            PackagingLotId = _packagingLotId,
            IcingLotId = _icingLotId,
            FeedLotId = _feedLotId,
            FertilizerLotId = _fertilizerLotId,
            StrawLotId = _strawLotId,
            LimestoneLotId = _limestoneLotId,
            TraceMineralLotId = _traceMineralLotId,
            SodaAshLotId = _sodaAshLotId,
            PhosphateLotId = _phosphateLotId,
            WheatKg = _wheatKg,
            WheatMoisturePct = _wheatMoisturePct,
            WheatForeignMaterialPct = _wheatForeignMaterialPct,
            WheatProteinPct = _wheatProteinPct,
            SugarCropKg = _sugarCropKg,
            SugarCropSugarPct = _sugarCropSugarPct,
            SugarCropSoilTarePct = _sugarCropSoilTarePct,
            FlourKg = _flourKg,
            SugarKg = _sugarKg,
            Eggs = _eggs,
            RawMilkL = _rawMilkL,
            MilkL = _milkL,
            ButterKg = _butterKg,
            BakingPowderKg = _bakingPowderKg,
            SaltKg = _saltKg,
            VanillaL = _vanillaL,
            VanillaBeansKg = _vanillaBeansKg,
            CocoaBeansKg = _cocoaBeansKg,
            CocoaKg = _cocoaKg,
            CocoaBeanMoisturePct = _cocoaBeanMoisturePct,
            CocoaFermentationPct = _cocoaFermentationPct,
            BrineL = _brineL,
            SodaAshKg = _sodaAshKg,
            PhosphateKg = _phosphateKg,
            SodaAshAssayPct = _sodaAshAssayPct,
            PhosphateAcidValuePct = _phosphateAcidValuePct,
            StarchKg = _starchKg,
            StrawKg = _strawKg,
            ForageMoisturePct = _forageMoisturePct,
            FeedGrainMoisturePct = _feedGrainMoisturePct,
            LimestoneKg = _limestoneKg,
            TraceMineralKg = _traceMineralKg,
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
            MolassesKg = _molassesKg,
            SkimMilkL = _skimMilkL,
            ButtermilkL = _buttermilkL,
            VanillaPomaceKg = _vanillaPomaceKg,
            CocoaShellKg = _cocoaShellKg,
            CocoaButterKg = _cocoaButterKg,
            BrineBlowdownL = _brineBlowdownL,
            LeaveningDustKg = _leaveningDustKg,
            PackagingTrimKg = _packagingTrimKg,
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
            MilkConditionPct = EquipmentFor(IngredientFactoryKind.Milk).ConditionPct,
            MilkCalibrationPct = EquipmentFor(IngredientFactoryKind.Milk).CalibrationPct,
            ButterConditionPct = EquipmentFor(IngredientFactoryKind.Butter).ConditionPct,
            ButterCalibrationPct = EquipmentFor(IngredientFactoryKind.Butter).CalibrationPct,
            VanillaConditionPct = EquipmentFor(IngredientFactoryKind.Vanilla).ConditionPct,
            VanillaCalibrationPct = EquipmentFor(IngredientFactoryKind.Vanilla).CalibrationPct,
            CocoaConditionPct = EquipmentFor(IngredientFactoryKind.Cocoa).ConditionPct,
            CocoaCalibrationPct = EquipmentFor(IngredientFactoryKind.Cocoa).CalibrationPct,
            SaltConditionPct = EquipmentFor(IngredientFactoryKind.Salt).ConditionPct,
            SaltCalibrationPct = EquipmentFor(IngredientFactoryKind.Salt).CalibrationPct,
            StarchConditionPct = EquipmentFor(IngredientFactoryKind.Starch).ConditionPct,
            StarchCalibrationPct = EquipmentFor(IngredientFactoryKind.Starch).CalibrationPct,
            LeaveningConditionPct = EquipmentFor(IngredientFactoryKind.Leavening).ConditionPct,
            LeaveningCalibrationPct = EquipmentFor(IngredientFactoryKind.Leavening).CalibrationPct,
            PackagingConditionPct = EquipmentFor(IngredientFactoryKind.Packaging).ConditionPct,
            PackagingCalibrationPct = EquipmentFor(IngredientFactoryKind.Packaging).CalibrationPct,
            IcingConditionPct = EquipmentFor(IngredientFactoryKind.Icing).ConditionPct,
            IcingCalibrationPct = EquipmentFor(IngredientFactoryKind.Icing).CalibrationPct,
            FeedConditionPct = EquipmentFor(IngredientFactoryKind.Feed).ConditionPct,
            FeedCalibrationPct = EquipmentFor(IngredientFactoryKind.Feed).CalibrationPct,
            FertilizerConditionPct = EquipmentFor(IngredientFactoryKind.Fertilizer).ConditionPct,
            FertilizerCalibrationPct = EquipmentFor(IngredientFactoryKind.Fertilizer).CalibrationPct,
            BeddingConditionPct = EquipmentFor(IngredientFactoryKind.Bedding).ConditionPct,
            BeddingCalibrationPct = EquipmentFor(IngredientFactoryKind.Bedding).CalibrationPct,
            MineralConditionPct = EquipmentFor(IngredientFactoryKind.MineralPremix).ConditionPct,
            MineralCalibrationPct = EquipmentFor(IngredientFactoryKind.MineralPremix).CalibrationPct,
            MillRollGapMm = _millRollGapMm,
            FlourExtractionPct = _flourExtractionPct,
            WheatTemperMoisturePct = _wheatTemperMoisturePct,
            MillSifterLoadPct = _millSifterLoadPct,
            FlourMoisturePct = _flourMoisturePct,
            FlourAshPct = _flourAshPct,
            FlourProteinPct = _flourProteinPct,
            SugarJuiceBrix = _sugarJuiceBrix,
            SugarEvaporatorTemperatureC = _sugarEvaporatorTemperatureC,
            SugarJuicePurityPct = _sugarJuicePurityPct,
            SugarLimePh = _sugarLimePh,
            SugarPanVacuumKPa = _sugarPanVacuumKPa,
            SugarCentrifugeRpm = _sugarCentrifugeRpm,
            SugarMoisturePct = _sugarMoisturePct,
            SugarColorIcumsa = _sugarColorIcumsa,
            SugarPolarizationPct = _sugarPolarizationPct,
            MilkPasteurizerTemperatureC = _milkPasteurizerTemperatureC,
            MilkHomogenizerPressureBar = _milkHomogenizerPressureBar,
            MilkPasteurizationHoldSeconds = _milkPasteurizationHoldSeconds,
            MilkMicroLogReduction = _milkMicroLogReduction,
            CreamSeparatorRpm = _creamSeparatorRpm,
            CreamYieldL = _creamYieldL,
            CreamFatPct = _creamFatPct,
            CreamPasteurizerTemperatureC = _creamPasteurizerTemperatureC,
            CreamPasteurizationHoldSeconds = _creamPasteurizationHoldSeconds,
            ButterChurnTemperatureC = _butterChurnTemperatureC,
            ButterFatPct = _butterFatPct,
            ButterMoisturePct = _butterMoisturePct,
            ButterSaltPct = _butterSaltPct,
            ButterWorkingPressureKPa = _butterWorkingPressureKPa,
            VanillaExtractorTemperatureC = _vanillaExtractorTemperatureC,
            VanillaExtractStrengthPct = _vanillaExtractStrengthPct,
            CocoaRoasterTemperatureC = _cocoaRoasterTemperatureC,
            CocoaRoasterAirflowM3Min = _cocoaRoasterAirflowM3Min,
            CocoaRoastDevelopmentPct = _cocoaRoastDevelopmentPct,
            CocoaWinnowerEfficiencyPct = _cocoaWinnowerEfficiencyPct,
            CocoaNibYieldPct = _cocoaNibYieldPct,
            CocoaPressPressureBar = _cocoaPressPressureBar,
            CocoaGrindMicrons = _cocoaGrindMicrons,
            CocoaPowderFatPct = _cocoaPowderFatPct,
            CocoaPowderMoisturePct = _cocoaPowderMoisturePct,
            BrineSalinityPct = _brineSalinityPct,
            BrineHardnessPpm = _brineHardnessPpm,
            BrineTurbidityNtu = _brineTurbidityNtu,
            BrineClarifierTurbidityNtu = _brineClarifierTurbidityNtu,
            SaltEvaporatorVacuumKPa = _saltEvaporatorVacuumKPa,
            SaltCrystallizerTemperatureC = _saltCrystallizerTemperatureC,
            SaltCentrifugeRpm = _saltCentrifugeRpm,
            SaltDryerTemperatureC = _saltDryerTemperatureC,
            SaltMoisturePct = _saltMoisturePct,
            SaltPurityPct = _saltPurityPct,
            SaltScreenPassingPct = _saltScreenPassingPct,
            StarchSlurryBrix = _starchSlurryBrix,
            StarchDryerTemperatureC = _starchDryerTemperatureC,
            StarchMoisturePct = _starchMoisturePct,
            LeaveningMixerRpm = _leaveningMixerRpm,
            LeaveningHomogeneityPct = _leaveningHomogeneityPct,
            LeaveningBlendMoisturePct = _leaveningBlendMoisturePct,
            LeaveningSifterLoadPct = _leaveningSifterLoadPct,
            LeaveningDustCollectorPressurePa = _leaveningDustCollectorPressurePa,
            CartonFormerSpeedCpm = _cartonFormerSpeedCpm,
            CartonBoardCaliperMm = _cartonBoardCaliperMm,
            CartonBoardMoisturePct = _cartonBoardMoisturePct,
            CartonDieCutWastePct = _cartonDieCutWastePct,
            LabelWebTensionN = _labelWebTensionN,
            PrintRegistrationMm = _printRegistrationMm,
            GluePotTemperatureC = _gluePotTemperatureC,
            GlueBeadGPerCarton = _glueBeadGPerCarton,
            CaseCodeReadRatePct = _caseCodeReadRatePct,
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
            FeedMillHammerRpm = _feedMillHammerRpm,
            FeedPelletTemperatureC = _feedPelletTemperatureC,
            FeedMoisturePct = _feedMoisturePct,
            CompostTemperatureC = _compostTemperatureC,
            CompostMoisturePct = _compostMoisturePct,
            CompostAerationPct = _compostAerationPct,
            BeddingChopperRpm = _beddingChopperRpm,
            BeddingMoisturePct = _beddingMoisturePct,
            BeddingDustPct = _beddingDustPct,
            MineralMixerRpm = _mineralMixerRpm,
            MineralHomogeneityPct = _mineralHomogeneityPct,
            MineralMetalPpm = _mineralMetalPpm,
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
        bool fertilizerLotReleased = FactoryLotReleased(_fertilizerKg, _fertilizerLotId);
        double fieldInputFactor = fertilizerLotReleased
            ? SupplyFactor(
                (_irrigationWaterL, fieldWaterNeed),
                (_fertilizerKg, fertilizerNeed),
                (_wheatSeedKg, wheatSeedNeed),
                (_beetSeedKg, beetSeedNeed))
            : 0;

        if (fieldInputFactor > 0)
        {
            _irrigationWaterL = Math.Max(0, _irrigationWaterL - fieldWaterNeed * fieldInputFactor);
            ConsumeTrackedStock(ref _fertilizerKg, fertilizerNeed * fieldInputFactor, ref _fertilizerLotId);
            _wheatSeedKg = Math.Max(0, _wheatSeedKg - wheatSeedNeed * fieldInputFactor);
            _beetSeedKg = Math.Max(0, _beetSeedKg - beetSeedNeed * fieldInputFactor);
        }
        else if (!fertilizerLotReleased && fieldDemand > 0)
        {
            _traceabilityStatus = $"Crop growth stalled: fertilizer lot {_fertilizerLotId} is waiting for QA lab release.";
        }

        double fieldEffect = FarmIntensity * (0.12 + 0.88 * power) * fieldInputFactor;
        _wheatGrowth = Math.Min(100, _wheatGrowth + seconds * 0.16 * fieldEffect);
        _beetGrowth = Math.Min(100, _beetGrowth + seconds * 0.13 * fieldEffect);
        _pastureHealth = Math.Clamp(_pastureHealth + seconds * (0.09 * fieldEffect - 0.015), 10, 100);
        _vanillaGrowth = Math.Min(100, _vanillaGrowth + seconds * 0.045 * fieldEffect);
        _cocoaGrowth = Math.Min(100, _cocoaGrowth + seconds * 0.040 * fieldEffect);

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
        bool beddingLotReleased = FactoryLotReleased(_beddingKg, _beddingLotId);
        double cowInputFactor = beddingLotReleased
            ? SupplyFactor(
                (_mixedRationKg, rationNeed),
                (_irrigationWaterL, barnWaterNeed),
                (_beddingKg, beddingNeed),
                (_barnLaborHours, laborNeed))
            : 0;
        double henFeedNeed = _layingHenCount * 0.026 * simHours * livestockPower;
        double henWaterNeed = _layingHenCount * 0.055 * simHours * livestockPower;
        double henBeddingNeed = _layingHenCount * 0.006 * simHours * livestockPower;
        double henLaborNeed = _layingHenCount * 0.0008 * simHours * livestockPower;
        bool feedLotReleased = FactoryLotReleased(_animalFeedKg, _feedLotId);
        double henInputFactor = feedLotReleased && beddingLotReleased
            ? SupplyFactor(
                (_animalFeedKg, henFeedNeed),
                (_irrigationWaterL, henWaterNeed),
                (_beddingKg, henBeddingNeed),
                (_barnLaborHours, henLaborNeed))
            : 0;

        if (cowInputFactor > 0)
        {
            ConsumeTrackedStock(ref _mixedRationKg, rationNeed * cowInputFactor, ref _mixedRationLotId);
            _irrigationWaterL = Math.Max(0, _irrigationWaterL - barnWaterNeed * cowInputFactor);
            ConsumeTrackedStock(ref _beddingKg, beddingNeed * cowInputFactor, ref _beddingLotId);
            _barnLaborHours = Math.Max(0, _barnLaborHours - laborNeed * cowInputFactor);
            _manureKg = Math.Min(1800, _manureKg + _dairyCowCount * 0.42 * simHours * cowInputFactor);
            _dairyParlorHygienePct = Math.Max(0, _dairyParlorHygienePct - seconds * (0.006 + 0.006 * livestockPower) - Math.Max(0, _manureKg - 700) * 0.00018);
        }

        if (henInputFactor > 0)
        {
            _animalFeedKg = Math.Max(0, _animalFeedKg - henFeedNeed * henInputFactor);
            _irrigationWaterL = Math.Max(0, _irrigationWaterL - henWaterNeed * henInputFactor);
            ConsumeTrackedStock(ref _beddingKg, henBeddingNeed * henInputFactor, ref _beddingLotId);
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
            ? $"Milk comes from {_lactatingCowCount} lactating cows; herd consumed {rationNeed * cowInputFactor:0.0} kg TMR ration {_mixedRationLotId}, {beddingNeed * cowInputFactor:0.0} kg released bedding lot {_beddingLotId}, {barnWaterNeed * cowInputFactor:0} L water and made {_manureKg:0} kg manure. Raw bulk-tank milk still requires pasteurization before batching."
            : beddingLotReleased
                ? "Milk production stalled: cow herd needs mixed ration, water, released bedding, labor, pasture health and powered milking systems."
                : $"Milk production stalled: livestock bedding lot {_beddingLotId} is waiting for QA lab release.";
        _eggSourceStatus = henInputFactor > 0 && livestockPower > 0
            ? $"Eggs come from {_layingHenCount} laying hens; flock consumed {henFeedNeed * henInputFactor:0.0} kg released feed lot {_feedLotId}, {henBeddingNeed * henInputFactor:0.0} kg released bedding lot {_beddingLotId}, {henWaterNeed * henInputFactor:0} L water and labor, then made {_poultryManureKg:0} kg manure."
            : !feedLotReleased
                ? $"Egg production stalled: poultry feed lot {_feedLotId} is waiting for QA lab release."
                : beddingLotReleased
                    ? "Egg production stalled: hens need released feed, water, released bedding, labor, clean nests and reactor-powered grading."
                    : $"Egg production stalled: livestock bedding lot {_beddingLotId} is waiting for QA lab release.";
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
        if (_fertilizerKg < 8 && !CompostPlantInputsReady()) low.Add("compost plant inputs");
        if (!FactoryLotReleased(_fertilizerKg, _fertilizerLotId)) low.Add("fertilizer QA release");
        if (_wheatSeedKg < 1) low.Add("wheat seed");
        if (_beetSeedKg < 1) low.Add("beet seed");
        if (_animalFeedKg < 20) low.Add("poultry feed");
        if (_animalFeedKg < 20 && (_grainKg < 42 || _starchKg < 4.8 || _dairyMineralKg < 1.6)) low.Add("feed mill inputs");
        if (_starchKg < 8 && _grainKg < 52) low.Add("starch plant grain");
        if (!FactoryLotReleased(_starchKg, _starchLotId)) low.Add("starch QA release");
        if (_forageKg < 48) low.Add("dairy forage");
        if (_grainKg < 22) low.Add("dairy grain");
        if (_dairyMineralKg < 2.2) low.Add("dairy mineral premix");
        if (_dairyMineralKg < 2.2 && (_limestoneKg < 16 || _traceMineralKg < 5.5 || _phosphateKg < 3 || _saltKg < 4)) low.Add("mineral premix plant feedstocks");
        if (!FactoryLotReleased(_dairyMineralKg, _dairyMineralLotId)) low.Add("dairy mineral QA release");
        if (_mixedRationKg < 20) low.Add("mixed dairy ration");
        double neededMilk = CurrentRecipe.MilkL * CurrentRecipe.BatchSize;
        if (_milkL < neededMilk) low.Add(_rawMilkL >= 5 ? "milk pasteurizer" : "raw milk collection");
        if (_rawMilkL > 0 && !MilkQaInSpec()) low.Add("raw milk QA");
        if (!FactoryLotReleased(_milkL, _milkLotId)) low.Add("pasteurized milk QA release");
        if (_beddingKg < 10) low.Add("bedding");
        if (_beddingKg < 10 && _strawKg < 54) low.Add("straw bedding feedstock");
        if (!FactoryLotReleased(_beddingKg, _beddingLotId)) low.Add("bedding QA release");
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
        if (_cocoaBeansKg < 5 && _cocoaKg < CurrentRecipe.CocoaKg * CurrentRecipe.BatchSize) low.Add(_cocoaGrowth >= 35 ? "cocoa harvest" : "cocoa greenhouse maturity");
        if (_brineL < 80 && _saltKg < CurrentRecipe.SaltKg * CurrentRecipe.BatchSize) low.Add("brine");
        if ((_sodaAshKg < 3 || _phosphateKg < 3 || _starchKg < 2)
            && _bakingPowderKg < CurrentRecipe.BakingPowderKg * CurrentRecipe.BatchSize)
            low.Add("baking powder feedstocks");
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
        Count(_fertilizerKg, _fertilizerLotId);
        Count(_strawKg, _strawLotId);
        Count(_animalFeedKg, _feedLotId);
        Count(_forageKg, _forageLotId);
        Count(_grainKg, _grainLotId);
        Count(_dairyMineralKg, _dairyMineralLotId);
        Count(_limestoneKg, _limestoneLotId);
        Count(_traceMineralKg, _traceMineralLotId);
        Count(_mixedRationKg, _mixedRationLotId);
        Count(_beddingKg, _beddingLotId);
        Count(_vanillaBeansKg, _vanillaBeanLotId);
        Count(_vanillaL, _vanillaLotId);
        Count(_rawMilkL, _rawMilkLotId);
        Count(_milkL, _milkLotId);
        Count(_eggs, _eggLotId);
        Count(_flourKg, _flourLotId);
        Count(_sugarKg, _sugarLotId);
        Count(_butterKg, _butterLotId);
        Count(_cocoaBeansKg, _cocoaBeansLotId);
        Count(_cocoaKg, _cocoaLotId);
        Count(_brineL, _brineLotId);
        Count(_saltKg, _saltLotId);
        Count(_sodaAshKg, _sodaAshLotId);
        Count(_phosphateKg, _phosphateLotId);
        Count(_starchKg, _starchLotId);
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
        if (!HasLot(_rawMilkL, _rawMilkLotId)) missing.Add("raw milk");
        if (!HasLot(_milkL, _milkLotId)) missing.Add("pasteurized milk");
        if (!HasLot(_butterKg, _butterLotId)) missing.Add("butter");
        if (!HasLot(_vanillaL, _vanillaLotId)) missing.Add("vanilla");
        if (!HasLot(_bakingPowderKg, _leaveningLotId)) missing.Add("baking powder");
        if (!HasLot(_saltKg, _saltLotId)) missing.Add("salt");
        if (!HasLot(_sodaAshKg, _sodaAshLotId)) missing.Add("baking soda");
        if (!HasLot(_phosphateKg, _phosphateLotId)) missing.Add("phosphate");
        if (!HasLot(_starchKg, _starchLotId)) missing.Add("starch");
        if (!HasLot(_cocoaBeansKg, _cocoaBeansLotId)) missing.Add("cocoa beans");
        if (!HasLot(_cocoaKg, _cocoaLotId)) missing.Add("cocoa");
        if (!HasLot(_packagingUnits, _packagingLotId)) missing.Add("packaging");
        if (!HasLot(_icingKg, _icingLotId)) missing.Add("prepared icing");
        if (!HasLot(_animalFeedKg, _feedLotId)) missing.Add("poultry feed");
        if (!HasLot(_fertilizerKg, _fertilizerLotId)) missing.Add("fertilizer");
        if (!HasLot(_beddingKg, _beddingLotId)) missing.Add("bedding");
        if (!HasLot(_strawKg, _strawLotId)) missing.Add("straw");
        if (!HasLot(_dairyMineralKg, _dairyMineralLotId)) missing.Add("dairy mineral premix");
        if (!HasLot(_limestoneKg, _limestoneLotId)) missing.Add("limestone");
        if (!HasLot(_traceMineralKg, _traceMineralLotId)) missing.Add("trace minerals");
        if (missing.Count > 0) return "Traceability hold: missing lot data for " + string.Join(", ", missing);
        return _traceabilityStatus;
    }

    private bool MilkQaInSpec() =>
        _rawMilkL <= 0
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
        return issues.Count == 0 ? "Raw milk QA in spec" : "Raw milk QA hold: " + string.Join(", ", issues);
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
        if (_milkL < r.MilkL * n) missing.Add("pasteurized milk");
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
