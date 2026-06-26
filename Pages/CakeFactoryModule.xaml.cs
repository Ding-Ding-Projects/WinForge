using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Web.WebView2.Core;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Nuclear-powered cake factory and farm simulator with native WinUI 3 controls and an optional
/// full HTML5/WebView2 factory mode. The C# service remains authoritative; every UI surface posts
/// operator actions back through one bridge.
/// </summary>
public sealed partial class CakeFactoryModule : Page
{
    private readonly CakeFactoryService _sim = new();
    private readonly CakeFileService _cakeFiles = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private DateTime _lastTick = DateTime.UtcNow;
    private bool _coreReady;
    private bool _webReady;
    private bool _nativeSyncing;
    private int _cakeFilesIssuedForPacked;
    private DateTime _lastCakeFileUiRefresh = DateTime.MinValue;
    private IReadOnlyList<CakeFileRecord> _cachedCakeFiles = Array.Empty<CakeFileRecord>();
    private CakeValidationResult? _cachedLatestCakeValidation;
    private int _cakeFileRefreshInFlight;
    private int _cakeFileIssueInFlight;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public CakeFactoryModule()
    {
        InitializeComponent();
        _timer.Tick += OnTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Loc.I.LanguageChanged += OnLanguageChanged;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ReactorStatusApiService.I.Start();
        RenderText();
        _lastTick = DateTime.UtcNow;
        LoadingRing.IsActive = false;
        RefreshSnapshot(forceCakeFileRefresh: true);
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        Loc.I.LanguageChanged -= OnLanguageChanged;
        try
        {
            if (CakeWeb.CoreWebView2 is not null)
                CakeWeb.CoreWebView2.WebMessageReceived -= OnWebMessage;
            CakeWeb.Close();
        }
        catch { }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RenderText();
        PostInit();
        PostSnapshot(_sim.Snapshot);
    }

    private void RenderText()
    {
        HeaderTitle.Text = "Nuclear Cake Factory & Farm · 核能蛋糕工廠與農場";
        HeaderBlurb.Text = P(
            "Native WinUI 3 controls for daily operation, with full HTML5 factory mode available on demand.",
            "日常操作用原生 WinUI 3 控制；需要時可開啟完整 HTML5 工廠模式。");
        OpenFullFactoryText.Text = P("Full factory mode", "完整工廠模式");
        OpenReactorText.Text = P("Open reactor", "開反應堆");
        FullFactoryTitle.Text = P("HTML5 full factory mode", "HTML5 完整工廠模式");
        CloseFullFactoryText.Text = P("Back to WinUI", "返回 WinUI");

        NativeControlsTitle.Text = P("WinUI 3 factory controls", "WinUI 3 工廠控制");
        RecipeBox.Header = P("Recipe", "配方");
        FarmIntensityLabel.Text = P("Farm intensity", "農場強度");
        LineSpeedLabel.Text = P("Line speed", "生產線速度");
        ReceiveSuppliesText.Text = P("Receive", "收貨");
        HarvestText.Text = P("Harvest", "收成");
        CollectText.Text = P("Milk + eggs", "牛奶雞蛋");
        MillText.Text = P("Mill flour", "磨粉");
        RefineText.Text = P("Refine sugar", "煉糖");
        ChurnText.Text = P("Churn butter", "攪牛油");
        ProcessCocoaText.Text = P("Roast cocoa", "烘可可");
        StartBatchText.Text = P("Start batch", "開批");
        AdvanceText.Text = P("Release step", "放行工序");
        CleanText.Text = P("CIP clean", "CIP 清潔");

        FactoryStatusTitle.Text = P("Factory status", "工廠狀態");
        StageLabel.Text = P("Stage", "工序");
        PowerLabel.Text = P("Power", "電力");
        QualityLabel.Text = P("Quality", "品質");
        SanitationLabel.Text = P("Sanitation", "衛生");
        OvenLabel.Text = P("Oven", "焗爐");
        PackedLabel.Text = P("Packed", "已包裝");
        PromptLabel.Text = P("Operator prompt", "操作提示");
        StageProgressLabel.Text = P("Stage progress", "工序進度");
        CoreProgressLabel.Text = P("Core temperature gate", "中心溫度閘");
        CipProgressLabel.Text = P("CIP progress", "CIP 進度");

        InventoryTitle.Text = P("Ingredients and farm buffers", "材料與農場緩衝");
        CakeFilesTitle.Text = P("Signed cake files", "已簽署蛋糕檔");
        BakeryKeyLabel.Text = P("Bakery key", "烘焙公鑰");
        CakeCountLabel.Text = P("Files", "檔案");
        LatestCakeLabel.Text = P("Latest", "最新");
        TrustCakeKeyText.Text = P("Trust key", "信任公鑰");
        ValidateCakeText.Text = P("Validate", "驗證");
        EatCakeText.Text = P("Eat + delete", "食用刪除");
        OpenCakeFolderText.Text = P("Open folder", "開資料夾");

        RefreshRecipeItems();
        UpdateNativeSnapshot(_sim.Snapshot, forceCakeFileRefresh: true);
    }

    private void RefreshRecipeItems()
    {
        int selected = Math.Clamp(_sim.RecipeIndex, 0, CakeFactoryService.Recipes.Count - 1);
        _nativeSyncing = true;
        try
        {
            RecipeBox.Items.Clear();
            for (int i = 0; i < CakeFactoryService.Recipes.Count; i++)
            {
                var recipe = CakeFactoryService.Recipes[i];
                RecipeBox.Items.Add(new ComboBoxItem
                {
                    Content = P(recipe.Name, recipe.NameZh),
                    Tag = i,
                });
            }
            RecipeBox.SelectedIndex = selected;
        }
        finally
        {
            _nativeSyncing = false;
        }
    }

    private void UpdateNativeSnapshot(CakeFactorySnapshot s, bool forceCakeFileRefresh = false)
    {
        _nativeSyncing = true;
        try
        {
            if (RecipeBox.SelectedIndex != _sim.RecipeIndex)
                RecipeBox.SelectedIndex = _sim.RecipeIndex;
            double farmValue = Math.Round(_sim.FarmIntensity * 100);
            double lineValue = Math.Round(_sim.LineSpeed * 100);
            if (Math.Abs(FarmIntensitySlider.Value - farmValue) > 0.5)
                FarmIntensitySlider.Value = farmValue;
            if (Math.Abs(LineSpeedSlider.Value - lineValue) > 0.5)
                LineSpeedSlider.Value = lineValue;
            FarmIntensityValue.Text = $"{_sim.FarmIntensity * 100:0}%";
            LineSpeedValue.Text = $"{_sim.LineSpeed * 100:0}%";
        }
        finally
        {
            _nativeSyncing = false;
        }

        string release = P("release ready", "可放行");
        StageValue.Text = s.StageReadyForOperator ? $"{s.StageName} - {release}" : s.StageName;
        PowerValue.Text = $"{s.PowerAvailability * 100:0}% · {s.ReactorElectricMW:0.0} MWe";
        QualityValue.Text = $"{s.QualityScore:0}%";
        SanitationValue.Text = $"{s.SanitationScore:0}%";
        OvenValue.Text = $"{s.OvenTemperatureC:0} degC · core {s.ProductInternalC:0} degC";
        PackedValue.Text = $"{s.CakesPacked} / {s.CakesBaked}";
        PromptText.Text = s.OperatorPrompt;

        StageProgressBar.Value = Math.Clamp(s.StageProgress * 100, 0, 100);
        CoreProgressLabel.Text = P($"Core gate: {s.ProductInternalC:0} degC", $"中心溫度閘: {s.ProductInternalC:0} degC");
        CoreProgressBar.Value = Math.Clamp(s.ProductInternalC / 71.0 * 100, 0, 100);
        CipProgressLabel.Text = s.CipActive
            ? P($"CIP progress: {s.CipProgress * 100:0}%", $"CIP 進度: {s.CipProgress * 100:0}%")
            : P("CIP progress: ready", "CIP 進度: 就緒");
        CipProgressBar.Value = s.CipActive ? Math.Clamp(s.CipProgress * 100, 0, 100) : 100;

        ReceiveSuppliesButton.IsEnabled = s.CanReceiveSupplies;
        HarvestButton.IsEnabled = s.CanHarvest;
        CollectButton.IsEnabled = s.CanCollectDairy;
        MillButton.IsEnabled = s.CanMillWheat;
        RefineButton.IsEnabled = s.CanRefineSugar;
        ChurnButton.IsEnabled = s.CanChurnButter;
        ProcessCocoaButton.IsEnabled = s.CanProcessCocoa;
        StartBatchButton.IsEnabled = s.CanStartBatch;
        AdvanceButton.IsEnabled = s.CanAdvanceStage;
        CleanButton.IsEnabled = !s.CipActive && s.Stage == CakeBatchStage.Idle;

        FlourValue.Text = $"{P("Flour", "麵粉")}: {s.FlourKg:0.0} kg";
        SugarValue.Text = $"{P("Sugar", "糖")}: {s.SugarKg:0.0} kg";
        EggsValue.Text = $"{P("Eggs", "雞蛋")}: {s.Eggs:0}";
        MilkValue.Text = $"{P("Milk", "牛奶")}: {s.MilkL:0.0} L";
        ButterValue.Text = $"{P("Butter", "牛油")}: {s.ButterKg:0.0} kg";
        CocoaValue.Text = $"{P("Cocoa", "可可")}: {s.CocoaKg:0.0} kg";
        FieldValue.Text = P(
            $"Fields: wheat {s.WheatGrowth:0}%, beets {s.BeetGrowth:0}%, vanilla {s.VanillaGrowth:0}%, pasture {s.PastureHealth:0}%",
            $"田區: 小麥 {s.WheatGrowth:0}%, 甜菜 {s.BeetGrowth:0}%, 雲呢拿 {s.VanillaGrowth:0}%, 牧草 {s.PastureHealth:0}%");
        ResourceStatusText.Text = string.IsNullOrWhiteSpace(s.MissingIngredients)
            ? s.ResourceStatus
            : $"{s.ResourceStatus} · {P("Missing", "缺少")}: {s.MissingIngredients}";

        RefreshCakeFileUiCache(forceCakeFileRefresh);
        ApplyCakeFileUiCache();
    }

    private void ApplyCakeFileUiCache()
    {
        var latestCake = _cachedCakeFiles.FirstOrDefault();
        BakeryKeyValue.Text = _cakeFiles.PublicKeyId;
        CakeCountValue.Text = _cachedCakeFiles.Count.ToString();
        LatestCakeValue.Text = latestCake?.CakeId ?? P("No cake file", "未有蛋糕檔");
        CakeValidationText.Text = _cachedLatestCakeValidation?.ReasonEn ?? P("No cake file is ready.", "未有蛋糕檔。");
        TrustCakeKeyButton.IsEnabled = latestCake is not null;
        ValidateCakeButton.IsEnabled = latestCake is not null;
        EatCakeButton.IsEnabled = latestCake is not null;
    }

    private async void RefreshCakeFileUiCache(bool force = false)
    {
        var now = DateTime.UtcNow;
        if (!force && (now - _lastCakeFileUiRefresh).TotalSeconds < 1)
            return;
        if (Interlocked.CompareExchange(ref _cakeFileRefreshInFlight, 1, 0) != 0)
            return;

        _lastCakeFileUiRefresh = now;
        try
        {
            var result = await Task.Run(() =>
            {
                IReadOnlyList<CakeFileRecord> files = Array.Empty<CakeFileRecord>();
                CakeValidationResult? validation = null;
                try
                {
                    files = _cakeFiles.ListFresh();
                    var latestCake = files.FirstOrDefault();
                    validation = latestCake is null ? null : _cakeFiles.Validate(latestCake.Path);
                }
                catch { }
                return (files, validation);
            });

            _cachedCakeFiles = result.files;
            _cachedLatestCakeValidation = result.validation;
            ApplyCakeFileUiCache();
        }
        catch { }
        finally
        {
            Interlocked.Exchange(ref _cakeFileRefreshInFlight, 0);
        }
    }

    private async System.Threading.Tasks.Task InitWebAsync()
    {
        try
        {
            _ = CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch
        {
            ShowRuntimeMissing();
            return;
        }

        try
        {
            LoadingRing.IsActive = true;
            var env = await CoreWebView2Environment.CreateWithOptionsAsync(
                browserExecutableFolder: string.Empty,
                userDataFolder: Path.Combine(AppContext.BaseDirectory, "WebView2", "cake-userdata"),
                options: new CoreWebView2EnvironmentOptions());
            await CakeWeb.EnsureCoreWebView2Async(env);

            var core = CakeWeb.CoreWebView2;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;

            core.WebMessageReceived += OnWebMessage;

            string assets = Path.Combine(AppContext.BaseDirectory, "SimAssets", "cake");
            core.SetVirtualHostNameToFolderMapping("cake.assets", assets, CoreWebView2HostResourceAccessKind.DenyCors);
            core.Navigate("https://cake.assets/index.html");
            _coreReady = true;
        }
        catch (Exception ex)
        {
            LoadingRing.IsActive = false;
            FullFactoryPanel.Visibility = Visibility.Collapsed;
            NativeFactoryView.Visibility = Visibility.Visible;
            PowerInfo.Severity = InfoBarSeverity.Error;
            PowerInfo.Title = P("Could not start cake controls", "無法啟動蛋糕控制台");
            PowerInfo.Message = ex.Message;
            PowerInfo.IsOpen = true;
        }
    }

    private void ShowRuntimeMissing()
    {
        LoadingRing.IsActive = false;
        FullFactoryPanel.Visibility = Visibility.Collapsed;
        NativeFactoryView.Visibility = Visibility.Visible;
        PowerInfo.Severity = InfoBarSeverity.Warning;
        PowerInfo.Title = P("WebView2 Runtime not found", "搵唔到 WebView2 執行階段");
        PowerInfo.Message = P(
            "Native WinUI controls still work. Full factory mode needs the Microsoft Edge WebView2 Runtime.",
            "原生 WinUI 控制仍可使用；完整工廠模式需要 Microsoft Edge WebView2 執行階段。");
        PowerInfo.IsOpen = true;
    }

    private void OnTick(object? sender, object e)
    {
        var now = DateTime.UtcNow;
        double dt = Math.Clamp((now - _lastTick).TotalSeconds, 0.016, 0.25);
        _lastTick = now;

        _sim.Tick(dt, ReactorStatusApiService.I.LastSnapshot);
        IssueCakeFilesIfNeeded(_sim.Snapshot);
        UpdatePowerInfo(_sim.Snapshot);
        UpdateNativeSnapshot(_sim.Snapshot);
        PostSnapshot(_sim.Snapshot);
    }

    private void UpdatePowerInfo(CakeFactorySnapshot s)
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
        PowerInfo.IsOpen = true;
    }

    private void OnWebMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        CakeBridgeMessage? msg;
        try { msg = JsonSerializer.Deserialize<CakeBridgeMessage>(e.WebMessageAsJson, JsonOpts); }
        catch { return; }
        if (msg is null) return;

        if (string.Equals(msg.Type, "ready", StringComparison.OrdinalIgnoreCase))
        {
            _webReady = true;
            LoadingRing.IsActive = false;
            PostInit();
            RefreshSnapshot();
            return;
        }

        if (!string.Equals(msg.Type, "cake", StringComparison.OrdinalIgnoreCase))
            return;

        HandleCakeAction(msg.Action, msg.Index, msg.Value);
    }

    private async void HandleCakeAction(string? action, int? index = null, double? value = null)
    {
        bool forceCakeFileRefresh = false;
        switch (action)
        {
            case "recipe":
                if (index is int recipeIndex)
                    _sim.SelectRecipe(recipeIndex);
                break;

            case "farmIntensity":
                if (value is double farm)
                    _sim.FarmIntensity = Math.Clamp(farm, 0, 1);
                break;

            case "lineSpeed":
                if (value is double speed)
                    _sim.LineSpeed = Math.Clamp(speed, 0, 1.2);
                break;

            case "harvest":
                PostNotice("success", P("Harvest complete", "收成完成"), _sim.HarvestNow());
                break;

            case "harvestFeedCrops":
                PostNotice("success", P("Feed crops harvested", "飼料作物已收成"), _sim.HarvestFeedCrops());
                break;

            case "harvestCocoa":
                PostNotice("success", P("Cocoa harvested", "可可已收成"), _sim.HarvestCocoa());
                break;

            case "collect":
                PostNotice("success", P("Barn collected", "畜舍已收集"), _sim.CollectDairyAndEggs());
                break;

            case "getCowFromPetStore":
                PostNotice("success", P("Cow acquired", "已取得奶牛"), _sim.GetCowFromPetStore());
                break;

            case "pasteurizeMilk":
                PostNotice("success", P("Milk pasteurized", "牛奶已巴氏殺菌"), _sim.PasteurizeMilk());
                break;

            case "mixDairyRation":
                PostNotice("success", P("Dairy ration mixed", "奶牛飼料已混合"), _sim.MixDairyRation());
                break;

            case "washDairyParlor":
                PostNotice("info", P("Dairy parlor washed", "擠奶間已清洗"), _sim.WashDairyParlor());
                break;

            case "washPoultryHouse":
                PostNotice("info", P("Poultry house washed", "禽舍已清洗"), _sim.WashPoultryHouse());
                break;

            case "mill":
                PostNotice("success", P("Mill run complete", "磨粉完成"), _sim.MillWheat());
                break;

            case "refine":
                PostNotice("success", P("Sugar refined", "煉糖完成"), _sim.RefineSugar());
                break;

            case "churn":
                PostNotice("success", P("Butter churned", "牛油製成"), _sim.ChurnButter());
                break;

            case "extractVanilla":
                PostNotice("success", P("Vanilla extraction run", "雲呢拿萃取已運行"), _sim.ExtractVanilla());
                break;

            case "receiveSupplies":
                PostNotice("success", P("Supplies received", "補給已接收"), _sim.ReceiveSupplies());
                break;

            case "orderSupplyDelivery":
                PostNotice("info", P("Supply order", "補給訂單"), _sim.OrderSupplyDelivery());
                break;

            case "unloadSupplyDelivery":
                PostNotice("success", P("Supply delivery unloaded", "補給已卸貨"), _sim.UnloadSupplyDelivery());
                break;

            case "runUtilityPlant":
                PostNotice("info", P("Utility plant started", "公用工程已啟動"), _sim.RunUtilityPlant());
                break;

            case "processCocoa":
                PostNotice("success", P("Cocoa processed", "可可已處理"), _sim.ProcessCocoa());
                break;

            case "runSaltWorks":
                PostNotice("success", P("Salt works run", "鹽廠已運行"), _sim.RunSaltWorks());
                break;

            case "runStarchPlant":
                PostNotice("success", P("Starch plant run", "澱粉廠已運行"), _sim.RunStarchPlant());
                break;

            case "runBakingSodaPlant":
                PostNotice("success", P("Baking soda plant run", "小蘇打廠已運行"), _sim.RunBakingSodaPlant());
                break;

            case "runLeaveningPlant":
                PostNotice("success", P("Leavening plant run", "膨鬆劑廠已運行"), _sim.RunLeaveningPlant());
                break;

            case "runPackagingPlant":
                PostNotice("success", P("Packaging plant run", "包裝廠已運行"), _sim.RunPackagingPlant());
                break;

            case "runFeedMill":
                PostNotice("info", P("Feed mill run", "飼料廠已運行"), _sim.RunFeedMill());
                break;

            case "runCompostPlant":
                PostNotice("info", P("Compost plant run", "堆肥廠已運行"), _sim.RunCompostPlant());
                break;

            case "runBeddingPlant":
                PostNotice("info", P("Bedding plant run", "墊料廠已運行"), _sim.RunBeddingPlant());
                break;

            case "runMineralPlant":
                PostNotice("info", P("Mineral premix run", "礦物預混料已運行"), _sim.RunMineralPremixPlant());
                break;

            case "prepareIcing":
                PostNotice("info", P("Icing kitchen started", "糖霜廚房已啟動"), _sim.PrepareIcing());
                break;

            case "serviceFactories":
                PostNotice("info", P("Factories serviced", "工廠已維修"), _sim.ServiceIngredientFactories());
                break;

            case "haulByproducts":
                PostNotice("info", P("Byproducts hauled", "副產物已運走"), _sim.HaulFactoryByproducts());
                break;

            case "treatEffluent":
                PostNotice("info", P("Effluent treated", "廢水已處理"), _sim.TreatFactoryEffluent());
                break;

            case "releaseLabLot":
                PostNotice("info", P("Lab lot released", "品檢批號已放行"), _sim.ReleaseIngredientLabLot());
                break;

            case "stageBatchKit":
                PostNotice("info", P("Batch kit staged", "批次套件已備料"), _sim.StageBatchKit());
                break;

            case "dispatchOrder":
                PostNotice("info", P("Order dispatch", "訂單出貨"), _sim.DispatchOrder());
                break;

            case "startBatch":
                {
                    bool ok = _sim.TryStartBatch(out var message);
                    PostNotice(ok ? "success" : "warning", ok ? P("Batch started", "已開批") : P("Cannot start", "未能開批"), message);
                    break;
                }

            case "advance":
                {
                    bool ok = _sim.TryAdvanceStage(out var message);
                    PostNotice(ok ? "success" : "warning", ok ? P("Step released", "工序已放行") : P("Release blocked", "放行受阻"), message);
                    break;
                }

            case "clean":
                _sim.StartClean();
                PostNotice("info", P("CIP started", "CIP 已開始"),
                    P("Sanitation loop is washing mixer, depositor, oven belt, icing head and packer.",
                        "清潔迴路正沖洗攪拌機、落模機、爐帶、唧花頭同包裝機。"));
                break;

            case "validateCake":
                {
                    var v = await Task.Run(() => _cakeFiles.ValidateLatest());
                    forceCakeFileRefresh = true;
                    PostNotice(v.Valid ? "success" : "warning",
                        v.Valid ? P("Cake file authentic", "蛋糕檔可信") : P("Cake file rejected", "蛋糕檔被拒絕"),
                        v.ReasonEn);
                    break;
                }

            case "trustCakeKey":
                {
                    var latest = await Task.Run(() => _cakeFiles.ListFresh().FirstOrDefault());
                    forceCakeFileRefresh = true;
                    if (latest is null)
                    {
                        PostNotice("warning", P("No cake file", "未有蛋糕檔"), P("No cake file is available to trust.", "未有蛋糕檔可匯入公鑰。"));
                        break;
                    }

                    string keyName = P("Imported WinForge bakery", "已匯入 WinForge 烘焙房");
                    string keyId = await Task.Run(() => _cakeFiles.TrustPublicKeyFromCake(latest.Path, keyName));
                    PostNotice(string.IsNullOrWhiteSpace(keyId) ? "warning" : "success",
                        P("Bakery key trusted", "烘焙公鑰已信任"),
                        string.IsNullOrWhiteSpace(keyId) ? P("Could not read the cake public key.", "無法讀取蛋糕公鑰。") : keyId);
                    break;
                }

            case "eatCake":
                {
                    var r = await Task.Run(() => _cakeFiles.EatLatest());
                    forceCakeFileRefresh = true;
                    PostNotice(r.Eaten ? "success" : "warning",
                        r.Eaten ? P("Cake eaten", "蛋糕已食用") : P("Cannot eat cake", "未能食用蛋糕"),
                        r.ReasonEn);
                    break;
                }

            case "openCakeFolder":
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = _cakeFiles.CakeDir, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    PostNotice("warning", P("Could not open folder", "無法開啟資料夾"), ex.Message);
                }
                break;

            case "openReactor":
                Navigator.GoToModule?.Invoke("module.reactor");
                break;
        }

        RefreshSnapshot(forceCakeFileRefresh);
    }

    private void RefreshSnapshot(bool forceCakeFileRefresh = false)
    {
        _sim.Tick(0.016, ReactorStatusApiService.I.LastSnapshot);
        IssueCakeFilesIfNeeded(_sim.Snapshot);
        UpdatePowerInfo(_sim.Snapshot);
        UpdateNativeSnapshot(_sim.Snapshot, forceCakeFileRefresh);
        PostSnapshot(_sim.Snapshot);
    }

    private void IssueCakeFilesIfNeeded(CakeFactorySnapshot s)
    {
        int pending = Math.Max(0, s.CakesPacked - _cakeFilesIssuedForPacked);
        if (pending <= 0) return;
        if (Interlocked.CompareExchange(ref _cakeFileIssueInFlight, 1, 0) != 0) return;

        _ = IssueCakeFilesAsync(s.Recipe, pending, s.QualityScore, s.SanitationScore);
    }

    private async Task IssueCakeFilesAsync(CakeRecipe recipe, int pending, double qualityScore, double sanitationScore)
    {
        IReadOnlyList<CakeFileRecord> issued = Array.Empty<CakeFileRecord>();
        try
        {
            issued = await Task.Run(() => _cakeFiles.IssueBatch(recipe, pending, qualityScore, sanitationScore));
        }
        catch { }
        finally
        {
            Interlocked.Exchange(ref _cakeFileIssueInFlight, 0);
        }

        _cakeFilesIssuedForPacked += issued.Count;
        if (issued.Count > 0)
        {
            RefreshCakeFileUiCache(force: true);
            PostNotice("success", P("Cake files signed", "蛋糕檔已簽署"),
                $"{issued.Count} .cake files minted with bakery key {_cakeFiles.PublicKeyId}.");
        }
    }

    private void PostInit()
    {
        PostJson(new
        {
            type = "init",
            language = Loc.I.Language.ToString(),
            recipes = CakeFactoryService.Recipes.Select((r, i) => new
            {
                index = i,
                r.Key,
                r.Name,
                r.NameZh,
                r.BatchSize,
                r.FlourKg,
                r.SugarKg,
                r.EggCount,
                r.ButterKg,
                r.MilkL,
                r.BakingPowderKg,
                r.SaltKg,
                r.VanillaL,
                r.CocoaKg,
                r.MixSeconds,
                r.BakeSeconds,
                r.OvenSetpointC,
                r.TargetSpecificGravity,
            })
        });
    }

    private void PostSnapshot(CakeFactorySnapshot s)
    {
        if (!_webReady || CakeWeb.CoreWebView2 is null || FullFactoryPanel.Visibility != Visibility.Visible)
            return;

        RefreshCakeFileUiCache();
        var cakes = _cachedCakeFiles;
        var latestCake = cakes.FirstOrDefault();
        var latestValidation = latestCake is null ? null : _cachedLatestCakeValidation;
        PostJson(new
        {
            type = "snapshot",
            recipeIndex = _sim.RecipeIndex,
            farmIntensity = _sim.FarmIntensity,
            lineSpeed = _sim.LineSpeed,
            stageKey = s.Stage.ToString(),
            cleanEnabled = !s.CipActive && s.Stage == CakeBatchStage.Idle,
            bakeryKeyId = _cakeFiles.PublicKeyId,
            cakeFileCount = cakes.Count,
            latestCakeId = latestCake?.CakeId ?? "",
            latestCakeFile = latestCake?.Path ?? "",
            latestCakeValid = latestValidation?.Valid ?? false,
            latestCakeStatus = latestValidation?.ReasonEn ?? P("No cake file is ready.", "未有蛋糕檔。"),
            snapshot = s,
        });
    }

    private void PostNotice(string severity, string title, string message)
    {
        NativeActionBar.Severity = severity switch
        {
            "success" => InfoBarSeverity.Success,
            "warning" => InfoBarSeverity.Warning,
            "error" => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational,
        };
        NativeActionBar.Title = title;
        NativeActionBar.Message = message;
        NativeActionBar.IsOpen = true;

        PostJson(new
        {
            type = "notice",
            severity,
            title,
            message,
        });
    }

    private void PostJson(object message)
    {
        if (!_webReady || CakeWeb.CoreWebView2 is null || FullFactoryPanel.Visibility != Visibility.Visible) return;
        try
        {
            CakeWeb.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message, JsonOpts));
        }
        catch { }
    }

    private async void OpenFullFactory_Click(object sender, RoutedEventArgs e)
    {
        NativeFactoryView.Visibility = Visibility.Collapsed;
        FullFactoryPanel.Visibility = Visibility.Visible;
        LoadingRing.IsActive = !_webReady;

        if (!_coreReady)
            await InitWebAsync();
        else
        {
            PostInit();
            RefreshSnapshot(forceCakeFileRefresh: true);
        }
    }

    private void CloseFullFactory_Click(object sender, RoutedEventArgs e)
    {
        FullFactoryPanel.Visibility = Visibility.Collapsed;
        NativeFactoryView.Visibility = Visibility.Visible;
        LoadingRing.IsActive = false;
    }

    private void OpenReactor_Click(object sender, RoutedEventArgs e) => Navigator.GoToModule?.Invoke("module.reactor");

    private void RecipeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_nativeSyncing || RecipeBox.SelectedIndex < 0) return;
        HandleCakeAction("recipe", RecipeBox.SelectedIndex);
    }

    private void FarmIntensitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_nativeSyncing) return;
        HandleCakeAction("farmIntensity", value: e.NewValue / 100.0);
    }

    private void LineSpeedSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_nativeSyncing) return;
        HandleCakeAction("lineSpeed", value: e.NewValue / 100.0);
    }

    private void ReceiveSupplies_Click(object sender, RoutedEventArgs e) => HandleCakeAction("receiveSupplies");
    private void Harvest_Click(object sender, RoutedEventArgs e) => HandleCakeAction("harvest");
    private void Collect_Click(object sender, RoutedEventArgs e) => HandleCakeAction("collect");
    private void Mill_Click(object sender, RoutedEventArgs e) => HandleCakeAction("mill");
    private void Refine_Click(object sender, RoutedEventArgs e) => HandleCakeAction("refine");
    private void Churn_Click(object sender, RoutedEventArgs e) => HandleCakeAction("churn");
    private void ProcessCocoa_Click(object sender, RoutedEventArgs e) => HandleCakeAction("processCocoa");
    private void StartBatch_Click(object sender, RoutedEventArgs e) => HandleCakeAction("startBatch");
    private void Advance_Click(object sender, RoutedEventArgs e) => HandleCakeAction("advance");
    private void Clean_Click(object sender, RoutedEventArgs e) => HandleCakeAction("clean");
    private void TrustCakeKey_Click(object sender, RoutedEventArgs e) => HandleCakeAction("trustCakeKey");
    private void ValidateCake_Click(object sender, RoutedEventArgs e) => HandleCakeAction("validateCake");
    private void EatCake_Click(object sender, RoutedEventArgs e) => HandleCakeAction("eatCake");
    private void OpenCakeFolder_Click(object sender, RoutedEventArgs e) => HandleCakeAction("openCakeFolder");

    private sealed class CakeBridgeMessage
    {
        public string? Type { get; set; }
        public string? Action { get; set; }
        public int? Index { get; set; }
        public double? Value { get; set; }
    }
}
