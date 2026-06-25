using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.Graphics;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 反應堆 HTML5 控制室視窗 · The dedicated reactor control-room window: one full-bleed WebView2 hosting
/// the self-contained HTML5 app (rooms as tabs). Shares the SAME <see cref="ReactorSimService"/> as the
/// in-module page — no second sim. A 10 Hz C# tick advances the physics and posts a JSON snapshot to JS;
/// JS posts control / fuel / ui actions back, which are funnelled through <c>sim.ApplyControl</c> and the
/// <see cref="FuelFactoryService"/>. The page never exposes the real-PC-shutdown toggle, so meltdown can
/// never arm a real shutdown from here.
/// </summary>
public sealed class ReactorHtmlWindow : Window
{
    private readonly ReactorSimService _sim;
    private readonly FuelFactoryService _fuel;
    private readonly NuclearWasteService _waste = new();
    private readonly WaterTreatmentService _water = new();
    private double _mwdSinceLastWaste;            // MWd accrued since last waste junk file
    private double _wasteFullGrace;               // seconds the cap has stayed full (drives mandate-shutdown)
    private const double WasteFullGraceSeconds = 8.0; // grace after runback before mandatory shutdown
    // Produce a waste junk file every ~10 MWd of whole-core energy: at rated power (~3411 MWth)
    // that is roughly one waste file every ~4 minutes of operation, so burning fuel visibly
    // accumulates real nuclear waste on disk.
    private const double WasteEveryMwd = 10.0;
    private readonly WebView2 _web = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) }; // 10 Hz
    private readonly OverlappedPresenter _presenter = OverlappedPresenter.Create();
    private DateTime _last = DateTime.UtcNow;
    private double _simClock;
    private bool _webReady;
    private bool _full;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public ReactorHtmlWindow(ReactorSimService sim, FuelFactoryService? fuel = null)
    {
        _sim = sim;
        _fuel = fuel ?? new FuelFactoryService();

        // Surface waste storage warnings + progress to the HTML room.
        _waste.StorageWarning += (en, zh) =>
        {
            try { DispatcherQueue?.TryEnqueue(() => PostFuel("waste", false, en, zh)); } catch { }
        };
        _waste.Changed += () =>
        {
            try { DispatcherQueue?.TryEnqueue(PostWasteStatus); } catch { }
        };

        Title = "Reactor Control Room · 反應堆控制室";
        try { AppWindow.SetIcon("Assets/AppIcon.ico"); } catch { }
        ExtendsContentIntoTitleBar = false; // HTML SPA owns its chrome; keep a normal title bar.
        AppWindow.SetPresenter(_presenter);

        _web.HorizontalAlignment = HorizontalAlignment.Stretch;
        _web.VerticalAlignment = VerticalAlignment.Stretch;
        Content = _web;

        try
        {
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            AppWindow.Resize(new SizeInt32(1280, 860));
            AppWindow.Move(new PointInt32(
                area.WorkArea.X + Math.Max(0, (area.WorkArea.Width - 1280) / 2),
                area.WorkArea.Y + Math.Max(0, (area.WorkArea.Height - 860) / 2)));
        }
        catch { }

        ReactorWindowManager.Track(this);

        _web.Loaded += async (_, _) => await InitWebAsync();
        Closed += (_, _) =>
        {
            _timer.Stop();
            _timer.Tick -= Tick;
            // Do NOT dispose _sim (owned by the page).
        };
    }

    private async System.Threading.Tasks.Task InitWebAsync()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateWithOptionsAsync(
                browserExecutableFolder: string.Empty,
                userDataFolder: Path.Combine(AppContext.BaseDirectory, "WebView2", "reactor-userdata"),
                options: new CoreWebView2EnvironmentOptions());
            await _web.EnsureCoreWebView2Async(env);

            var core = _web.CoreWebView2;
            core.Settings.IsWebMessageEnabled = true;          // REQUIRED both directions
            core.Settings.AreDevToolsEnabled = false;          // kiosk
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;

            core.WebMessageReceived += OnWebMessage;            // subscribe BEFORE navigate

            string assets = Path.Combine(AppContext.BaseDirectory, "SimAssets", "reactor");
            core.SetVirtualHostNameToFolderMapping("reactor.assets", assets,
                CoreWebView2HostResourceAccessKind.DenyCors);
            core.Navigate("https://reactor.assets/index.html");

            _webReady = true;
            _last = DateTime.UtcNow;
            _timer.Tick += Tick;
            _timer.Start();
        }
        catch { /* WebView2 runtime missing — window stays blank rather than crashing the app. */ }
    }

    private void Tick(object? sender, object e)
    {
        var now = DateTime.UtcNow;
        double dt = (now - _last).TotalSeconds;
        _last = now;
        if (dt <= 0 || dt > 1.0) dt = 0.1;
        _simClock += dt;

        // Fuel-availability gate (input gating only) before advancing.
        _sim.FuelAvailable = _fuel.CanReactorRun;

        // ---- WASTE CAP controller: runback then mandate shutdown + block operation ----
        ApplyWasteFullControl(dt);

        // ---- WATER TREATMENT plant: step it, then feed makeup availability/quality to the reactor ----
        bool reactorDrawing = _sim.Mode == ReactorMode.Run || _sim.Mode == ReactorMode.Startup
                              || _sim.ThermalPowerMW > 1.0 || _sim.Thot > 120.0;
        _water.Step(dt, _sim.MakeupDemandLpm, reactorDrawing);
        _sim.MakeupWaterAvailability = _water.Availability();
        _sim.MakeupWaterInSpec = _water.InSpec();

        _sim.Update(dt);

        // Accrue burnup onto loaded assemblies and re-sign on disk; collect any newly-spent assemblies.
        var newlySpent = _fuel.AccrueBurnup(_sim.ThermalPowerMW, dt);

        // MANDATORY nuclear waste: burning fuel produces real junk files. Trigger on burnup
        // milestones (every WasteEveryMwd of whole-core energy) and whenever an assembly becomes spent.
        if (_sim.ThermalPowerMW > 0)
        {
            double mwdThisTick = _sim.ThermalPowerMW * dt / 86400.0;
            _mwdSinceLastWaste += mwdThisTick;
            if (_mwdSinceLastWaste >= WasteEveryMwd && !_waste.IsGenerating)
            {
                _mwdSinceLastWaste = 0;
                // Scale waste size by energy produced this milestone into the [100MB, 2000MB] band.
                long target = NuclearWasteService.MinWasteBytes
                    + (long)(Math.Clamp(_sim.ThermalPowerMW / ReactorSimService.RatedThermalMW, 0, 1)
                             * (NuclearWasteService.MaxWasteBytes - NuclearWasteService.MinWasteBytes));
                _waste.GenerateWaste(target);
            }
        }
        if (newlySpent.Count > 0 && !_waste.IsGenerating)
            _waste.GenerateWaste(); // a spent assembly mandates a fresh waste file (random size)

        if (_webReady && _web.CoreWebView2 is not null)
        {
            try { _web.CoreWebView2.PostWebMessageAsJson(_sim.ExportStateJson(_simClock, Loc.I.IsCantonesePrimary)); }
            catch { }
            try { _web.CoreWebView2.PostWebMessageAsJson(_water.ExportStateJson()); }
            catch { }
        }
    }

    /// <summary>
    /// 廢料滿載控制 · WASTE-CAP controller. As waste approaches the cap the plant runs back power; at
    /// the cap, after a short grace, an auto-SCRAM is mandated and startup is blocked until waste is
    /// disposed below the cap. (Input/limit gating only — no physics edits.)
    /// </summary>
    private void ApplyWasteFullControl(double dt)
    {
        bool capReached = _waste.CapReached;
        bool runback = _waste.RunbackZone;
        _sim.SpentFuelStorageFull = capReached;

        if (capReached)
        {
            // BLOCK loading / startup until disposed below the cap.
            _sim.SetOperationBlock(
                "Spent fuel storage FULL — dispose waste below the cap before operating.",
                "乏燃料貯存已滿 — 運轉前請先將廢料處置至上限以下。");
            // First a hard runback (cap power low), then mandate shutdown after the grace.
            _sim.ExternalPowerCap = 0.15;
            _wasteFullGrace += dt;
            if (_wasteFullGrace >= WasteFullGraceSeconds && !_sim.IsScrammed
                && _sim.Mode != ReactorMode.Shutdown && _sim.Mode != ReactorMode.Meltdown)
            {
                _sim.Scram();                 // mandatory auto-SCRAM
                _sim.SetMode(ReactorMode.Shutdown);
            }
        }
        else if (runback)
        {
            // Controlled power reduction (runback) approaching the cap; operation still allowed.
            _sim.SetOperationBlock("", "");
            _sim.ExternalPowerCap = 0.50;
            _wasteFullGrace = 0;
        }
        else
        {
            // Cleared: waste disposed below the runback zone — restore normal operation.
            _sim.SetOperationBlock("", "");
            _sim.ExternalPowerCap = 1e9;
            _wasteFullGrace = 0;
        }
    }

    private void OnWebMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        BridgeMsg? msg;
        try { msg = JsonSerializer.Deserialize<BridgeMsg>(e.WebMessageAsJson, JsonOpts); }
        catch { return; }
        if (msg is null) return;

        switch (msg.Type)
        {
            case "control":
                _sim.ApplyControl(msg.Action, msg.Index, msg.Value, msg.Flag);
                break;

            case "fuel":
                HandleFuel(msg);
                break;

            case "ui":
                if (msg.Action == "setLanguage" && msg.Value2 is not null)
                {
                    if (string.Equals(msg.Value2, "Cantonese", StringComparison.OrdinalIgnoreCase))
                        Loc.I.Language = AppLanguage.Cantonese;
                    else if (string.Equals(msg.Value2, "English", StringComparison.OrdinalIgnoreCase))
                        Loc.I.Language = AppLanguage.English;
                }
                else if (msg.Action == "fullscreen")
                {
                    ToggleFull();
                }
                break;
        }
    }

    private void HandleFuel(BridgeMsg msg)
    {
        try
        {
            switch (msg.Op)
            {
                case "fabricate":
                {
                    var a = _fuel.Fabricate(msg.Enrichment, msg.Mass);
                    PostFuel("fabricate", true,
                        $"Fabricated {a.Id}.", $"已製造 {a.Id}。");
                    PostFuelList();
                    break;
                }
                case "listFuel":
                    PostFuelList();
                    break;
                case "validate":
                {
                    var path = _fuel.ResolvePath(msg.Path ?? "");
                    if (path is null) { PostFuel("validate", false, "File not found.", "找不到檔案。"); break; }
                    var v = _fuel.Validate(path);
                    PostFuelValidation(v);
                    break;
                }
                case "load":
                {
                    var path = _fuel.ResolvePath(msg.Path ?? "");
                    if (path is null) { PostFuel("load", false, "File not found.", "找不到檔案。"); break; }
                    // Waste-full block: cannot load new fuel with nowhere to put the waste.
                    if (_waste.CapReached)
                    {
                        PostFuel("load", false,
                            "Load blocked — spent fuel storage FULL. Dispose waste below the cap first.",
                            "拒絕入料 — 乏燃料貯存已滿。請先將廢料處置至上限以下。");
                        break;
                    }
                    // UNSAFE "send in": authentic fuel loads normally; forged/off-spec fuel is consumed
                    // and HARMS the SIMULATED reactor in proportion to how bad the forgery is.
                    var lr = _fuel.LoadIntoCoreUnsafe(path);
                    if (lr.Harmful && lr.HarmSeverity > 0)
                    {
                        _sim.InjectForgedFuelHarm(lr.HarmSeverity); // SIM-ONLY harm + auto-SCRAM
                        PostFuel("load", false,
                            $"⚠ Counterfeit / off-spec fuel — cladding breach! {lr.ReasonEn} Auto-SCRAM, core damage rising.",
                            $"⚠ 偽冒／不合格燃料 — 包殼破損！{lr.ReasonZh} 已自動緊急停堆，堆芯損傷上升。");
                    }
                    else
                    {
                        PostFuel("load", lr.Loaded, lr.ReasonEn, lr.ReasonZh);
                    }
                    PostFuelList();
                    break;
                }
                case "unload":
                {
                    bool ok = msg.Path is not null && _fuel.UnloadFromCore(Path.GetFileNameWithoutExtension(msg.Path));
                    PostFuel("unload", ok, ok ? "Assembly unloaded." : "Unload failed.",
                        ok ? "燃料已卸出。" : "卸料失敗。");
                    PostFuelList();
                    break;
                }
                case "discharge":
                    _fuel.DischargeAll();
                    PostFuel("discharge", true, "All loaded fuel discharged to spent pool.",
                        "全部在堆燃料已退役至乏燃料池。");
                    PostFuelList();
                    break;

                case "wasteStatus":
                    PostWasteStatus();
                    break;

                case "dispose":
                {
                    if (string.IsNullOrWhiteSpace(msg.Path))
                    {
                        int n = _waste.DisposeAll();
                        PostFuel("dispose", true,
                            $"Disposed {n} waste file(s) to deep geological repository.",
                            $"已將 {n} 個核廢料檔案送往深地質處置庫。");
                    }
                    else
                    {
                        bool ok = _waste.Dispose(msg.Path);
                        PostFuel("dispose", ok,
                            ok ? "Waste file disposed (deep geological repository)." : "Dispose failed.",
                            ok ? "核廢料已處置（深地質處置庫）。" : "處置失敗。");
                    }
                    PostWasteStatus();
                    break;
                }

                case "setSafetyFloor":
                {
                    // value carried as MB in msg.Mass.
                    long mb = (long)Math.Max(0, msg.Mass);
                    _waste.SafetyFloorBytes = mb * 1024L * 1024L;
                    PostFuel("setSafetyFloor", true,
                        $"Waste safety floor set to {mb} MB.", $"核廢料安全下限設為 {mb} MB。");
                    PostWasteStatus();
                    break;
                }

                case "setCap":
                {
                    // value carried as GB in msg.Mass.
                    double gb = Math.Max(1, msg.Mass);
                    _waste.CapBytes = (long)(gb * 1024L * 1024L * 1024L);
                    PostFuel("setCap", true,
                        $"Waste storage cap set to {gb:0.#} GB.", $"廢料貯存上限設為 {gb:0.#} GB。");
                    PostWasteStatus();
                    break;
                }

                case "generateWaste": // manual test trigger
                {
                    bool ok = _waste.GenerateWaste();
                    PostFuel("generateWaste", ok,
                        ok ? "Generating waste…" : "Busy or storage full.",
                        ok ? "正在產生核廢料…" : "忙碌中或廢料倉已滿。");
                    break;
                }

                // ---- Water Treatment plant controls ----
                case "waterStatus":
                    PostWaterStatus();
                    break;
                case "waterIntakePump": _water.IntakePumpOn = msg.Flag; PostWaterStatus(); break;
                case "waterIntakeRate": _water.IntakeRate = Math.Clamp(msg.Mass, 0, 1); PostWaterStatus(); break;
                case "waterRo": _water.RoOn = msg.Flag; PostWaterStatus(); break;
                case "waterDegasifier": _water.DegasifierOn = msg.Flag; PostWaterStatus(); break;
                case "waterMakeupValve": _water.MakeupValveOpen = msg.Flag; PostWaterStatus(); break;
                case "waterRegenerate":
                    _water.Regenerate();
                    PostFuel("waterRegenerate", true,
                        "Mixed-bed demineraliser regeneration started.", "混床除鹽器再生已開始。");
                    PostWaterStatus();
                    break;
                case "waterFlushRo":
                    _water.FlushRo();
                    PostFuel("waterFlushRo", true, "RO membranes flushed.", "RO 膜已沖洗。");
                    PostWaterStatus();
                    break;
            }
        }
        catch (Exception ex)
        {
            PostFuel(msg.Op ?? "fuel", false, "Fuel op failed: " + ex.Message, "燃料操作失敗：" + ex.Message);
        }
    }

    // camelCase so FuelAssembly records (EnrichmentPct → enrichmentPct, FabChain → fabChain, …)
    // and all anonymous DTOs land in the shape the HTML room reads.
    private static readonly JsonSerializerOptions PostOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private void Post(object o)
    {
        if (_webReady && _web.CoreWebView2 is not null)
            try { _web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(o, PostOpts)); } catch { }
    }

    private void PostFuel(string op, bool ok, string en, string zh) =>
        Post(new { type = "fuelResult", op, ok, msgEn = en, msgZh = zh });

    private void PostFuelValidation(ValidationResult v) =>
        Post(new
        {
            type = "fuelResult", op = "validate", ok = v.Valid,
            validation = new { valid = v.Valid, reason = v.Reason, en = v.ReasonEn, zh = v.ReasonZh },
            msgEn = v.ReasonEn, msgZh = v.ReasonZh,
        });

    private void PostFuelList() =>
        Post(new
        {
            type = "fuelResult", op = "listFuel", ok = true,
            items = new
            {
                fresh = _fuel.ListFresh(),
                loaded = _fuel.ListLoaded(),
                spent = _fuel.ListSpent(),
            },
            canRun = _fuel.CanReactorRun,
        });

    private void PostWasteStatus()
    {
        var s = _waste.Status();
        Post(new
        {
            type = "fuelResult", op = "wasteStatus", ok = true,
            waste = new
            {
                files = s.Files.Select(f => new
                {
                    id = f.Id,
                    bytes = f.Bytes,
                    mb = Math.Round(f.Bytes / (1024.0 * 1024.0), 1),
                    createdUtc = f.CreatedUtc.ToString("u"),
                }),
                totalBytes = s.TotalBytes,
                totalMb = Math.Round(s.TotalBytes / (1024.0 * 1024.0), 1),
                totalGb = Math.Round(s.TotalBytes / (1024.0 * 1024.0 * 1024.0), 2),
                count = s.Count,
                driveFreeGb = s.DriveFreeBytes == long.MaxValue ? -1
                    : Math.Round(s.DriveFreeBytes / (1024.0 * 1024.0 * 1024.0), 1),
                safetyFloorGb = Math.Round(s.SafetyFloorBytes / (1024.0 * 1024.0 * 1024.0), 1),
                capGb = Math.Round(s.CapBytes / (1024.0 * 1024.0 * 1024.0), 1),
                capUsedPct = Math.Round(s.CapUsedPct, 1),
                capReached = s.CapReached,
                runbackZone = s.RunbackZone,
                storageFull = s.StorageFull,
                generating = s.Generating,
                progressPct = Math.Round(s.GenProgressPct, 1),
                genTargetMb = Math.Round(s.GenTargetBytes / (1024.0 * 1024.0), 0),
                genId = s.GenId,
            },
        });
    }

    private void PostWaterStatus()
    {
        if (_webReady && _web.CoreWebView2 is not null)
            try { _web.CoreWebView2.PostWebMessageAsJson(_water.ExportStateJson()); } catch { }
    }

    private void ToggleFull()
    {
        _full = !_full;
        try
        {
            if (_full) AppWindow.SetPresenter(FullScreenPresenter.Create());
            else AppWindow.SetPresenter(_presenter);
        }
        catch { }
    }

    /// <summary>JS → C# 橋接訊息 · The single inbound bridge message shape (all fields optional).</summary>
    private sealed record BridgeMsg(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("action")] string? Action,
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("value")] double Value,
        [property: JsonPropertyName("flag")] bool Flag,
        [property: JsonPropertyName("op")] string? Op,
        [property: JsonPropertyName("enrichment")] double Enrichment,
        [property: JsonPropertyName("mass")] double Mass,
        [property: JsonPropertyName("path")] string? Path,
        // ui setLanguage carries a string in "value"; use a separate string field to avoid type clash.
        [property: JsonPropertyName("valueStr")] string? Value2);
}
