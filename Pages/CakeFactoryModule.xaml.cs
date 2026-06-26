using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Nuclear-powered cake factory and farm simulator hosted as a self-contained HTML5/WebView2 control
/// room. The C# service remains authoritative; JavaScript only renders controls and posts operator
/// actions back through this bridge.
/// </summary>
public sealed partial class CakeFactoryModule : Page
{
    private readonly CakeFactoryService _sim = new();
    private readonly CakeFileService _cakeFiles = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private DateTime _lastTick = DateTime.UtcNow;
    private bool _coreReady;
    private bool _webReady;
    private int _cakeFilesIssuedForPacked;

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

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ReactorStatusApiService.I.Start();
        RenderText();
        _lastTick = DateTime.UtcNow;

        if (!_coreReady)
            await InitWebAsync();

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
            "Hands-on HTML5 farm, mill, bakery, QA and sanitation controls driven by the live reactor bus.",
            "以 HTML5 操作農場、磨粉、焗爐、品檢同清潔；全部由即時反應堆供電。");
        OpenReactorText.Text = P("Open reactor", "開反應堆");
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
            PowerInfo.Severity = InfoBarSeverity.Error;
            PowerInfo.Title = P("Could not start cake controls", "無法啟動蛋糕控制台");
            PowerInfo.Message = ex.Message;
            PowerInfo.IsOpen = true;
        }
    }

    private void ShowRuntimeMissing()
    {
        LoadingRing.IsActive = false;
        PowerInfo.Severity = InfoBarSeverity.Warning;
        PowerInfo.Title = P("WebView2 Runtime not found", "搵唔到 WebView2 執行階段");
        PowerInfo.Message = P(
            "The cake simulator now uses an embedded HTML5 control room and needs the Microsoft Edge WebView2 Runtime.",
            "蛋糕模擬器現時使用內嵌 HTML5 控制室，需要 Microsoft Edge WebView2 執行階段。");
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

        switch (msg.Action)
        {
            case "recipe":
                if (msg.Index is int recipeIndex)
                    _sim.SelectRecipe(recipeIndex);
                break;

            case "farmIntensity":
                if (msg.Value is double farm)
                    _sim.FarmIntensity = Math.Clamp(farm, 0, 1);
                break;

            case "lineSpeed":
                if (msg.Value is double speed)
                    _sim.LineSpeed = Math.Clamp(speed, 0, 1.2);
                break;

            case "harvest":
                PostNotice("success", P("Harvest complete", "收成完成"), _sim.HarvestNow());
                break;

            case "collect":
                PostNotice("success", P("Barn collected", "畜舍已收集"), _sim.CollectDairyAndEggs());
                break;

            case "mixDairyRation":
                PostNotice("success", P("Dairy ration mixed", "奶牛飼料已混合"), _sim.MixDairyRation());
                break;

            case "washDairyParlor":
                PostNotice("info", P("Dairy parlor washed", "擠奶間已清洗"), _sim.WashDairyParlor());
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

            case "receiveSupplies":
                PostNotice("success", P("Supplies received", "補給已接收"), _sim.ReceiveSupplies());
                break;

            case "processCocoa":
                PostNotice("success", P("Cocoa processed", "可可已處理"), _sim.ProcessCocoa());
                break;

            case "runSaltWorks":
                PostNotice("success", P("Salt works run", "鹽廠已運行"), _sim.RunSaltWorks());
                break;

            case "runLeaveningPlant":
                PostNotice("success", P("Leavening plant run", "膨鬆劑廠已運行"), _sim.RunLeaveningPlant());
                break;

            case "serviceFactories":
                PostNotice("info", P("Factories serviced", "工廠已維修"), _sim.ServiceIngredientFactories());
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
                    var v = _cakeFiles.ValidateLatest();
                    PostNotice(v.Valid ? "success" : "warning",
                        v.Valid ? P("Cake file authentic", "蛋糕檔可信") : P("Cake file rejected", "蛋糕檔被拒絕"),
                        v.ReasonEn);
                    break;
                }

            case "trustCakeKey":
                {
                    var latest = _cakeFiles.ListFresh().FirstOrDefault();
                    if (latest is null)
                    {
                        PostNotice("warning", P("No cake file", "未有蛋糕檔"), P("No cake file is available to trust.", "未有蛋糕檔可匯入公鑰。"));
                        break;
                    }

                    string keyId = _cakeFiles.TrustPublicKeyFromCake(latest.Path, P("Imported WinForge bakery", "已匯入 WinForge 烘焙房"));
                    PostNotice(string.IsNullOrWhiteSpace(keyId) ? "warning" : "success",
                        P("Bakery key trusted", "烘焙公鑰已信任"),
                        string.IsNullOrWhiteSpace(keyId) ? P("Could not read the cake public key.", "無法讀取蛋糕公鑰。") : keyId);
                    break;
                }

            case "eatCake":
                {
                    var r = _cakeFiles.EatLatest();
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

        RefreshSnapshot();
    }

    private void RefreshSnapshot()
    {
        _sim.Tick(0.016, ReactorStatusApiService.I.LastSnapshot);
        IssueCakeFilesIfNeeded(_sim.Snapshot);
        UpdatePowerInfo(_sim.Snapshot);
        PostSnapshot(_sim.Snapshot);
    }

    private void IssueCakeFilesIfNeeded(CakeFactorySnapshot s)
    {
        int pending = Math.Max(0, s.CakesPacked - _cakeFilesIssuedForPacked);
        if (pending <= 0) return;

        var issued = _cakeFiles.IssueBatch(s.Recipe, pending, s.QualityScore, s.SanitationScore);
        _cakeFilesIssuedForPacked += issued.Count;
        if (issued.Count > 0)
        {
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
        var cakes = _cakeFiles.ListFresh();
        var latestCake = cakes.FirstOrDefault();
        var latestValidation = latestCake is null ? null : _cakeFiles.Validate(latestCake.Path);
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
        if (!_webReady || CakeWeb.CoreWebView2 is null) return;
        try
        {
            CakeWeb.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message, JsonOpts));
        }
        catch { }
    }

    private void OpenReactor_Click(object sender, RoutedEventArgs e) => Navigator.GoToModule?.Invoke("module.reactor");

    private sealed class CakeBridgeMessage
    {
        public string? Type { get; set; }
        public string? Action { get; set; }
        public int? Index { get; set; }
        public double? Value { get; set; }
    }
}
