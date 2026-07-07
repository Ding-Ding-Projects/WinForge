using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內 Home Assistant REST 控制 · In-app Home Assistant control over the documented REST API.
/// Config (base URL + long-lived token) persists via SettingsStore. Template render, config check +
/// restart, reload, 24h history sparkline, set state, scenes/scripts, events, intents, light/climate,
/// notify, camera snapshot, calendar and error-log tail — all run in-app. No redirect. Bilingual.
/// </summary>
public sealed partial class HomeAssistantModule : Page
{
    private readonly HomeAssistantService _ha = new();
    private readonly HomeAssistantAcDefenderService _acDefender = new();
    private readonly DockerService _docker = new();
    private readonly ObservableCollection<HaCalendarEvent> _calEvents = new();
    private byte[]? _lastSnap;

    // Native toggle list state (lights + plugs/switches + input_boolean).
    private readonly ObservableCollection<HaToggleRow> _lights = new();
    private readonly ObservableCollection<HaToggleRow> _plugs = new();
    private List<HaToggleRow> _allToggles = new();
    private bool _togglesLoaded;
    private DispatcherTimer? _autoTimer;

    public HomeAssistantModule()
    {
        InitializeComponent();
        CalList.ItemsSource = _calEvents;
        LightsList.ItemsSource = _lights;
        PlugsList.ItemsSource = _plugs;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) =>
        {
            UrlBox.Text = _ha.BaseUrl;
            TokenBox.Password = _ha.Token;
            AcRepoBox.Text = string.IsNullOrWhiteSpace(_acDefender.RepositoryPath)
                ? HomeAssistantAcDefenderService.LocateCandidate() ?? HomeAssistantAcDefenderService.GitHubRoot()
                : _acDefender.RepositoryPath;
            if (string.IsNullOrEmpty(TplBox.Text)) TplBox.Text = "{{ states('sun.sun') }}";
            Render();
        };
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            _autoTimer?.Stop();
            _docker.Dispose();
        };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Home Assistant · 家居助理";
        HeaderBlurb.Text = P("Control your Home Assistant over its REST API — render templates, check config and restart, plot history, run scenes, control lights and thermostats, push phone notifications and more. Everything runs in-app.",
            "用 REST API 控制你嘅 Home Assistant — 跑範本、驗 config 再重啟、畫歷史走勢、跑場景、控制燈同冷氣、推手機通知等等。全部喺 app 內做。");

        CfgTitle.Text = P("Connection · 連線設定", "連線設定");
        SaveCfgBtn.Content = P("Save · 儲存", "儲存");
        TestBtn.Content = P("Test · 測試", "測試");

        // Pivot headers
        TabTemplate.Header = P("Template · 範本", "範本");
        TabConfig.Header = P("Config · 設定", "設定");
        TabStates.Header = P("States · 狀態", "狀態");
        TabAuto.Header = P("Automation · 自動化", "自動化");
        TabDevices.Header = P("Lights & Climate · 燈與冷氣", "燈與冷氣");
        TabNotify.Header = P("Notify · 通知", "通知");
        TabCamera.Header = P("Camera · 鏡頭", "鏡頭");
        TabCalendar.Header = P("Calendar · 日曆", "日曆");
        TabAcDefender.Header = P("AC Defender · 冷氣防護", "冷氣防護");
        TabLog.Header = P("Error log · 錯誤記錄", "錯誤記錄");

        // Template
        TplBlurb.Text = P("Render a Jinja template against live state.", "攞實時狀態嚟跑 Jinja 範本。");
        TplRunBtn.Content = P("Render · 渲染", "渲染");

        // Config
        CcBlurb.Text = P("Validate the configuration before restarting — restart is only safe after a valid check.", "重啟之前先驗下個 config — 驗到 valid 先好重啟。");
        CheckCfgBtn.Content = P("Check config · 驗證設定", "驗證設定");
        RestartBtn.Content = P("Restart HA · 重啟 HA", "重啟 HA");
        ReloadLbl.Text = P("Reload without a full restart · 唔使全部重啟", "唔使全部重啟");
        ReloadDomainBtn.Content = P("Reload domain · 重載網域", "重載網域");
        ReloadEntryBtn.Content = P("Reload entry · 重載整合", "重載整合");
        int sel = ReloadDomainBox.SelectedIndex < 0 ? 0 : ReloadDomainBox.SelectedIndex;
        ReloadDomainBox.Items.Clear();
        foreach (var d in new[] { "automation", "scene", "script", "template", "input_boolean", "group" })
            ReloadDomainBox.Items.Add(d);
        ReloadDomainBox.SelectedIndex = sel;

        // States
        LoadEntitiesBtn.Content = P("Load entities · 載入實體", "載入實體");
        HistLbl.Text = P("24-hour history · 24 小時歷史", "24 小時歷史");
        HistBtn.Content = P("Plot history · 畫走勢", "畫走勢");
        SetStateLbl.Text = P("Set in-memory state · 寫自訂狀態", "寫自訂狀態");
        SetStateBtn.Content = P("Set · 設定", "設定");

        // Automation
        SceneBtn.Content = P("Run scene · 跑場景", "跑場景");
        ReloadScenesBtn.Content = P("Refresh · 重整", "重整");
        ScriptBtn.Content = P("Run script · 跑腳本", "跑腳本");
        EventLbl.Text = P("Fire a custom event · 掟自訂事件", "掟自訂事件");
        EventBtn.Content = P("Fire · 掟出", "掟出");
        IntentLbl.Text = P("Trigger an intent · 觸發意圖", "觸發意圖");
        IntentBtn.Content = P("Handle · 觸發", "觸發");

        // Devices — native toggle list
        TogglesLbl.Text = P("Lights & plugs — toggle directly · 燈同插座 — 直接切換", "燈同插座 — 直接切換");
        ToggleSearch.PlaceholderText = P("Search by name or entity id · 用名或 entity id 搜尋", "用名或 entity id 搜尋");
        RefreshTogglesBtn.Content = P("Refresh · 重整", "重整");
        AutoRefreshToggle.OnContent = P("Auto · 自動", "自動");
        AutoRefreshToggle.OffContent = P("Auto · 自動", "自動");
        AutoRefreshToggle.Header = P("Auto-refresh (10s) · 自動更新（10秒）", "自動更新（10秒）");
        AllLightsOnBtn.Content = P("All lights on · 開晒燈", "開晒燈");
        AllLightsOffBtn.Content = P("All lights off · 熄晒燈", "熄晒燈");
        GroupLightsLbl.Text = P("Lights · 燈", "燈");
        GroupPlugsLbl.Text = P("Plugs & switches · 插座與開關", "插座與開關");
        foreach (var r in _allToggles) r.RefreshLabels();

        // Devices — advanced single-light control
        LightLbl.Text = P("Advanced light (colour temp / RGB) · 進階燈光（色溫／RGB）", "進階燈光（色溫／RGB）");
        BrightLbl.Text = P("Brightness % · 光暗 %", "光暗 %");
        TempLbl.Text = P("Colour temp (K) · 色溫 (K)", "色溫 (K)");
        LightOnBtn.Content = P("Apply · 套用", "套用");
        LightOffBtn.Content = P("Off · 熄", "熄");
        ClimateLbl.Text = P("Thermostat · 冷氣", "冷氣");
        SetTempBtn.Content = P("Set temp · 設溫度", "設溫度");
        SetHvacBtn.Content = P("Set mode · 設模式", "設模式");
        int hsel = HvacBox.SelectedIndex < 0 ? 0 : HvacBox.SelectedIndex;
        HvacBox.Items.Clear();
        foreach (var m in HomeAssistantService.HvacModes) HvacBox.Items.Add(m);
        HvacBox.SelectedIndex = hsel;

        // Notify
        LoadTargetsBtn.Content = P("Load targets · 載入目標", "載入目標");
        NotifyBtn.Content = P("Push notification · 推通知", "推通知");

        // Camera
        SnapBtn.Content = P("Snapshot · 影一格", "影一格");
        SaveSnapBtn.Content = P("Save…· 儲存…", "儲存…");

        // Calendar
        LoadCalsBtn.Content = P("Load · 載入", "載入");
        TodayBtn.Content = P("Today · 今日", "今日");

        // AC Defender
        AcBlurb.Text = P("Generate and manage a small Home Assistant AC Defender Docker deployment. Pick a repo under your GitHub folder, export Dockerfile/docker-compose.yml, run it locally through the managed Docker API, or create a zip bundle for upload to an SSH Docker host. Secrets are not written to the generated files.",
            "產生同管理一個細型 Home Assistant 冷氣防護 Docker 部署。揀你 GitHub 資料夾入面嘅 repo，匯出 Dockerfile/docker-compose.yml，用 managed Docker API 本機啟停，或者整 zip bundle 上傳去 SSH Docker 主機。權杖唔會寫入產生嘅檔案。");
        AcRepoBox.Header = P("Repository folder · Repo 資料夾", "Repo 資料夾");
        AcLocateBtn.Content = P("Locate · 尋找", "尋找");
        AcBrowseBtn.Content = P("Browse… · 瀏覽…", "瀏覽…");
        AcProjectBox.Header = P("Docker project · Docker 專案", "Docker 專案");
        AcClimateBox.Header = P("Climate entity · 冷氣實體", "冷氣實體");
        AcPollBox.Header = P("Poll seconds · 輪詢秒數", "輪詢秒數");
        AcCoolAboveBox.Header = P("Turn off cooling above °C · 高過此溫度關冷氣", "高過此溫度關冷氣");
        AcHeatBelowBox.Header = P("Turn off heating below °C · 低過此溫度關暖氣", "低過此溫度關暖氣");
        AcDryRunToggle.Header = P("Dry run · 試行", "試行");
        AcDryRunToggle.OnContent = P("No HA changes · 唔改 HA", "唔改 HA");
        AcDryRunToggle.OffContent = P("Can turn off · 可以關機", "可以關機");
        AcGenerateBtn.Content = P("Generate files · 產生檔案", "產生檔案");
        AcExportBtn.Content = P("Export SSH bundle · 匯出 SSH bundle", "匯出 SSH bundle");
        AcStartBtn.Content = P("Start local · 本機啟動", "本機啟動");
        AcStopBtn.Content = P("Stop local · 本機停止", "本機停止");
        AcStatusBtn.Content = P("Status · 狀態", "狀態");

        // Log
        TailBtn.Content = P("Tail log · 睇 log", "睇 log");
        CopyLogBtn.Content = P("Copy · 複製", "複製");
    }

    private bool Guard(InfoBar bar)
    {
        if (_ha.IsConfigured) return true;
        Warn(bar, P("Set the base URL and token first.", "請先填 base URL 同權杖。"), "");
        return false;
    }

    private static void Ok(InfoBar bar, string title, string msg)
    {
        bar.Severity = InfoBarSeverity.Success; bar.Title = title; bar.Message = msg; bar.IsOpen = true;
    }
    private static void Warn(InfoBar bar, string title, string msg)
    {
        bar.Severity = InfoBarSeverity.Warning; bar.Title = title; bar.Message = msg; bar.IsOpen = true;
    }

    private void Show(InfoBar bar, HaResult r, string okTitle)
    {
        if (r.Ok) Ok(bar, okTitle, Trim(r.Body));
        else Warn(bar, P($"Failed (HTTP {r.Status})", $"失敗（HTTP {r.Status}）"), Trim(r.Body));
        bar.IsOpen = true;
    }

    private static string Trim(string s) => s.Length > 600 ? s[..600] + "…" : s;

    // ── Config ───────────────────────────────────────────────────────────────

    private void SaveCfg_Click(object sender, RoutedEventArgs e)
    {
        _ha.SaveConfig(UrlBox.Text, TokenBox.Password);
        UrlBox.Text = _ha.BaseUrl;
        Ok(CfgResult, P("Saved", "已儲存"), _ha.BaseUrl);
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        _ha.SaveConfig(UrlBox.Text, TokenBox.Password);
        if (!Guard(CfgResult)) return;
        CfgBusy.IsActive = true;
        try
        {
            var r = await _ha.Ping();
            if (r.Ok) Ok(CfgResult, P("Connected", "連得到"), Trim(r.Body));
            else Warn(CfgResult, P($"No connection (HTTP {r.Status})", $"連唔到（HTTP {r.Status}）"), Trim(r.Body));
        }
        finally { CfgBusy.IsActive = false; }
    }

    // ── Template ─────────────────────────────────────────────────────────────

    private async void TplRun_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(CfgResult)) { Tabs.SelectedItem = TabTemplate; return; }
        TplOut.Text = P("Rendering…", "渲染緊…");
        var r = await _ha.RenderTemplate(TplBox.Text ?? "");
        TplOut.Text = r.Ok ? r.Body : P($"[HTTP {r.Status}] ", $"[HTTP {r.Status}] ") + r.Body;
    }

    // ── Config check / restart / reload ──────────────────────────────────────

    private async void CheckCfg_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(CcResult)) return;
        CcBusy.IsActive = true;
        try
        {
            var r = await _ha.CheckConfig();
            if (r.Ok && r.Body.Contains("\"valid\"")) Ok(CcResult, P("Config valid", "設定有效"), Trim(r.Body));
            else Warn(CcResult, P("Config invalid — do NOT restart", "設定無效 — 唔好重啟"), Trim(r.Body));
        }
        finally { CcBusy.IsActive = false; }
    }

    private async void Restart_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(CcResult)) return;
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Restart Home Assistant?", "重啟 Home Assistant？"),
            Content = P("This restarts the whole HA instance. Run a config check first if you have not.",
                "呢個會重啟成個 HA。如果未驗過 config，建議先驗。"),
            PrimaryButtonText = P("Restart", "重啟"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        CcBusy.IsActive = true;
        try { Show(CcResult, await _ha.Restart(), P("Restart requested", "已要求重啟")); }
        finally { CcBusy.IsActive = false; }
    }

    private async void ReloadDomain_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(CcResult)) return;
        var dom = ReloadDomainBox.SelectedItem as string ?? "automation";
        Show(CcResult, await _ha.ReloadDomain(dom), P($"Reloaded {dom}", $"已重載 {dom}"));
    }

    private async void ReloadEntry_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(CcResult)) return;
        var id = (EntryIdBox.Text ?? "").Trim();
        if (id.Length == 0) { Warn(CcResult, P("Enter a config_entry_id", "請填 config_entry_id"), ""); return; }
        Show(CcResult, await _ha.ReloadConfigEntry(id), P("Reloaded entry", "已重載整合"));
    }

    // ── States / history / set state ─────────────────────────────────────────

    private async void LoadEntities_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(StResult)) return;
        var all = await _ha.States();
        EntityPick.ItemsSource = all.Select(x => x.EntityId).ToList();
        Ok(StResult, P($"{all.Count} entities loaded", $"載入咗 {all.Count} 個實體"), P("Type to filter the entity box.", "可以喺實體框打字篩選。"));
    }

    private async void Hist_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(StResult)) return;
        var id = (EntityPick.Text ?? "").Trim();
        if (id.Length == 0) { Warn(StResult, P("Enter an entity id", "請填實體 id"), ""); return; }
        SparkInfo.Text = P("Loading…", "載入緊…");
        Spark.Points = new PointCollection();
        var pts = await _ha.History(id, 24);
        if (pts.Count < 2)
        {
            SparkInfo.Text = P("No numeric history in the last 24h.", "過去 24 小時冇數值歷史。");
            return;
        }
        DrawSpark(pts);
    }

    private void DrawSpark(List<HaHistoryPoint> pts)
    {
        double w = 880, h = 64, pad = 6;
        double min = pts.Min(p => p.Value), max = pts.Max(p => p.Value);
        double range = Math.Abs(max - min) < 1e-9 ? 1 : max - min;
        long t0 = pts.First().When.Ticks, t1 = pts.Last().When.Ticks;
        double tr = t1 - t0 < 1 ? 1 : t1 - t0;
        var coll = new PointCollection();
        foreach (var p in pts)
        {
            double x = pad + (p.When.Ticks - t0) / tr * (w - 2 * pad);
            double y = pad + (1 - (p.Value - min) / range) * (h - 2 * pad);
            coll.Add(new Point(x, y));
        }
        Spark.Points = coll;
        SparkInfo.Text = P($"min {min:0.##} · max {max:0.##} · {pts.Count} pts",
            $"最低 {min:0.##} · 最高 {max:0.##} · {pts.Count} 點");
    }

    private async void SetState_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(StResult)) return;
        var id = (EntityPick.Text ?? "").Trim();
        if (id.Length == 0) { Warn(StResult, P("Enter an entity id", "請填實體 id"), ""); return; }
        var attr = (StateAttrBox.Text ?? "").Trim();
        if (attr.Length > 0 && !HomeAssistantService.IsValidJson(attr))
        {
            Warn(StResult, P("Attributes must be valid JSON", "屬性要係有效 JSON"), attr);
            return;
        }
        Show(StResult, await _ha.SetState(id, StateValBox.Text ?? "", attr.Length > 0 ? attr : null), P("State set", "已設定狀態"));
    }

    // ── Automation: scenes / scripts / events / intents ──────────────────────

    private async Task FillDomainCombo(ComboBox box, string domain)
    {
        var items = await _ha.States(new[] { domain });
        box.ItemsSource = items;
        box.DisplayMemberPath = nameof(HaEntity.Display);
        if (items.Count > 0) box.SelectedIndex = 0;
    }

    private async void ReloadScenes_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(AutoResult)) return;
        await FillDomainCombo(SceneBox, "scene");
        await FillDomainCombo(ScriptBox, "script");
        Ok(AutoResult, P("Refreshed", "已重整"), "");
    }

    private async void Scene_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(AutoResult)) return;
        if (SceneBox.SelectedItem is not HaEntity ent) { Warn(AutoResult, P("Pick a scene", "揀個場景"), P("Press Refresh first.", "先撳重整。")); return; }
        Show(AutoResult, await _ha.RunScene(ent.EntityId), P("Scene run", "已跑場景"));
    }

    private async void Script_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(AutoResult)) return;
        if (ScriptBox.SelectedItem is not HaEntity ent) { Warn(AutoResult, P("Pick a script", "揀個腳本"), P("Press Refresh first.", "先撳重整。")); return; }
        Show(AutoResult, await _ha.RunScript(ent.EntityId), P("Script run", "已跑腳本"));
    }

    private async void Event_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(AutoResult)) return;
        var type = (EventTypeBox.Text ?? "").Trim();
        if (type.Length == 0) { Warn(AutoResult, P("Enter an event type", "請填事件類型"), ""); return; }
        var data = (EventDataBox.Text ?? "").Trim();
        if (data.Length > 0 && !HomeAssistantService.IsValidJson(data)) { Warn(AutoResult, P("Event data must be valid JSON", "事件資料要係有效 JSON"), data); return; }
        Show(AutoResult, await _ha.FireEvent(type, data.Length > 0 ? data : null), P("Event fired", "已掟出事件"));
    }

    private async void Intent_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(AutoResult)) return;
        var name = (IntentNameBox.Text ?? "").Trim();
        if (name.Length == 0) { Warn(AutoResult, P("Enter an intent name", "請填意圖名"), ""); return; }
        var data = (IntentDataBox.Text ?? "").Trim();
        if (data.Length > 0 && !HomeAssistantService.IsValidJson(data)) { Warn(AutoResult, P("Intent data must be valid JSON", "意圖資料要係有效 JSON"), data); return; }
        Show(AutoResult, await _ha.HandleIntent(name, data.Length > 0 ? data : null), P("Intent handled", "已處理意圖"));
    }

    // ── Native toggle list (lights + plugs/switches) ─────────────────────────

    private async Task LoadToggles()
    {
        if (!_ha.IsConfigured) return;
        TogglesBusy.IsActive = true;
        try
        {
            var entities = await _ha.Controllables();
            // Build/refresh row VMs, preserving existing instances so bindings stay live.
            var byId = _allToggles.ToDictionary(r => r.EntityId, StringComparer.Ordinal);
            var rebuilt = new List<HaToggleRow>(entities.Count);
            foreach (var ent in entities)
            {
                if (byId.TryGetValue(ent.EntityId, out var existing))
                {
                    existing.Apply(ent);
                    rebuilt.Add(existing);
                }
                else
                {
                    rebuilt.Add(new HaToggleRow(ent, OnRowToggleRequested));
                }
            }
            _allToggles = rebuilt;
            foreach (var r in _allToggles) r.RefreshLabels();
            _togglesLoaded = true;
            ApplyToggleFilter();
            if (entities.Count == 0)
                Warn(ToggleResult, P("No lights, plugs or switches found", "搵唔到燈、插座或開關"),
                    P("Make sure these are exposed in Home Assistant.", "確保呢啲已喺 Home Assistant 暴露。"));
            else
                ToggleResult.IsOpen = false;
        }
        catch (Exception ex)
        {
            Warn(ToggleResult, P("Could not load entities", "載入唔到實體"), ex.Message);
        }
        finally { TogglesBusy.IsActive = false; }
    }

    private void ApplyToggleFilter()
    {
        var q = (ToggleSearch.Text ?? "").Trim();
        bool Match(HaToggleRow r) => q.Length == 0
            || r.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
            || r.EntityId.Contains(q, StringComparison.OrdinalIgnoreCase);

        _lights.Clear();
        _plugs.Clear();
        foreach (var r in _allToggles.Where(Match))
        {
            if (r.IsLight) _lights.Add(r);
            else _plugs.Add(r);
        }
    }

    /// <summary>Called by a row when its ToggleSwitch / On / Off / brightness control fires.</summary>
    private async Task OnRowToggleRequested(HaToggleRow row, HaRowAction action)
    {
        if (!_ha.IsConfigured) { Warn(ToggleResult, P("Set the base URL and token first.", "請先填 base URL 同權杖。"), ""); return; }
        HaResult r;
        switch (action)
        {
            case HaRowAction.On: r = await _ha.TurnOn(row.EntityId); break;
            case HaRowAction.Off: r = await _ha.TurnOff(row.EntityId); break;
            case HaRowAction.Brightness: r = await _ha.SetLightBrightnessPct(row.EntityId, row.BrightnessPctValueInt); break;
            default: r = await _ha.Toggle(row.EntityId); break;
        }
        if (!r.Ok)
            Warn(ToggleResult, P($"Action failed (HTTP {r.Status})", $"操作失敗（HTTP {r.Status}）"), Trim(r.Body));
        else
            ToggleResult.IsOpen = false;
        await RefreshRowState(row);
    }

    private async Task RefreshRowState(HaToggleRow row)
    {
        // Re-read just this entity's state so the switch/slider reflect HA's truth.
        var all = await _ha.Controllables();
        var match = all.FirstOrDefault(e => e.EntityId == row.EntityId);
        if (match is not null) row.Apply(match);
    }

    private async void RefreshToggles_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(ToggleResult)) return;
        await LoadToggles();
    }

    private void ToggleSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        => ApplyToggleFilter();

    private void AutoRefresh_Toggled(object sender, RoutedEventArgs e)
    {
        if (AutoRefreshToggle.IsOn)
        {
            _autoTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _autoTimer.Tick -= AutoTimer_Tick;
            _autoTimer.Tick += AutoTimer_Tick;
            _autoTimer.Start();
        }
        else
        {
            _autoTimer?.Stop();
        }
    }

    private async void AutoTimer_Tick(object? sender, object e)
    {
        if (!_ha.IsConfigured || !ReferenceEquals(Tabs.SelectedItem, TabDevices)) return;
        await LoadToggles();
    }

    private async void AllLightsOn_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(ToggleResult)) return;
        var r = await _ha.AllLightsOn();
        if (!r.Ok) Warn(ToggleResult, P($"Failed (HTTP {r.Status})", $"失敗（HTTP {r.Status}）"), Trim(r.Body));
        else Ok(ToggleResult, P("All lights on", "已開晒燈"), "");
        await LoadToggles();
    }

    private async void AllLightsOff_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(ToggleResult)) return;
        var r = await _ha.AllLightsOff();
        if (!r.Ok) Warn(ToggleResult, P($"Failed (HTTP {r.Status})", $"失敗（HTTP {r.Status}）"), Trim(r.Body));
        else Ok(ToggleResult, P("All lights off", "已熄晒燈"), "");
        await LoadToggles();
    }

    private async void RowToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch ts && ts.Tag is HaToggleRow row && !row.Suppress)
            await OnRowToggleRequested(row, row.IsOn ? HaRowAction.On : HaRowAction.Off);
    }

    private async void RowOn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is HaToggleRow row)
            await OnRowToggleRequested(row, HaRowAction.On);
    }

    private async void RowOff_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is HaToggleRow row)
            await OnRowToggleRequested(row, HaRowAction.Off);
    }

    private async void RowApplyBrightness_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is HaToggleRow row)
            await OnRowToggleRequested(row, HaRowAction.Brightness);
    }

    // ── Lights / climate ─────────────────────────────────────────────────────

    private async void LightOn_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(DevResult)) return;
        await EnsureDevices();
        if (LightBox.SelectedItem is not HaEntity ent) { Warn(DevResult, P("No light selected", "未揀燈"), ""); return; }
        Show(DevResult, await _ha.SetLight(ent.EntityId, (int)BrightSlider.Value, (int)ColorTempSlider.Value, null), P("Light updated", "已校燈"));
    }

    private async void LightOff_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(DevResult)) return;
        if (LightBox.SelectedItem is not HaEntity ent) { Warn(DevResult, P("No light selected", "未揀燈"), ""); return; }
        Show(DevResult, await _ha.LightOff(ent.EntityId), P("Light off", "已熄燈"));
    }

    private async void SetTemp_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(DevResult)) return;
        if (ClimateBox.SelectedItem is not HaEntity ent) { Warn(DevResult, P("No thermostat selected", "未揀冷氣"), ""); return; }
        double t = double.IsNaN(TempBox.Value) ? 21 : TempBox.Value;
        Show(DevResult, await _ha.SetThermostatTemp(ent.EntityId, t), P("Temperature set", "已設溫度"));
    }

    private async void SetHvac_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(DevResult)) return;
        if (ClimateBox.SelectedItem is not HaEntity ent) { Warn(DevResult, P("No thermostat selected", "未揀冷氣"), ""); return; }
        var mode = HvacBox.SelectedItem as string ?? "off";
        Show(DevResult, await _ha.SetHvacMode(ent.EntityId, mode), P("Mode set", "已設模式"));
    }

    private bool _devicesLoaded;
    private async Task EnsureDevices()
    {
        if (!_ha.IsConfigured) return;
        if (!_togglesLoaded) await LoadToggles();
        if (_devicesLoaded) return;
        await FillDomainCombo(LightBox, "light");
        await FillDomainCombo(ClimateBox, "climate");
        _devicesLoaded = true;
    }

    private async void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ha.IsConfigured) return;
        if (ReferenceEquals(Tabs.SelectedItem, TabDevices)) await EnsureDevices();
        else if (ReferenceEquals(Tabs.SelectedItem, TabCamera) && CameraBox.ItemsSource is null) await FillDomainCombo(CameraBox, "camera");
        else if (ReferenceEquals(Tabs.SelectedItem, TabAuto) && SceneBox.ItemsSource is null)
        {
            await FillDomainCombo(SceneBox, "scene");
            await FillDomainCombo(ScriptBox, "script");
        }
    }

    // ── Notify ───────────────────────────────────────────────────────────────

    private async void LoadTargets_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(NotifyResult)) return;
        var t = await _ha.NotifyTargets();
        NotifyTargetBox.ItemsSource = t;
        if (t.Count > 0) NotifyTargetBox.SelectedIndex = 0;
        Ok(NotifyResult, P($"{t.Count} targets", $"{t.Count} 個目標"), string.Join(", ", t.Take(6)));
    }

    private async void Notify_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(NotifyResult)) return;
        var target = NotifyTargetBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(target)) { Warn(NotifyResult, P("Pick a notify target", "揀個通知目標"), P("Press Load targets.", "撳載入目標。")); return; }
        var msg = (NotifyMsgBox.Text ?? "").Trim();
        if (msg.Length == 0) { Warn(NotifyResult, P("Enter a message", "請填訊息"), ""); return; }
        Show(NotifyResult, await _ha.Notify(target, NotifyTitleBox.Text ?? "", msg), P("Pushed", "已推送"));
    }

    // ── Camera ───────────────────────────────────────────────────────────────

    private async void Snap_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(CamResult)) return;
        if (CameraBox.ItemsSource is null) await FillDomainCombo(CameraBox, "camera");
        if (CameraBox.SelectedItem is not HaEntity ent) { Warn(CamResult, P("No camera selected", "未揀鏡頭"), ""); return; }
        var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ha-snap-{DateTime.Now:yyyyMMddHHmmss}.jpg");
        var r = await _ha.CameraSnapshot(ent.EntityId, tmp);
        if (!r.Ok) { Warn(CamResult, P($"Snapshot failed (HTTP {r.Status})", $"影唔到（HTTP {r.Status}）"), Trim(r.Body)); return; }
        try
        {
            _lastSnap = await System.IO.File.ReadAllBytesAsync(tmp);
            var bmp = new BitmapImage();
            using (var fs = System.IO.File.OpenRead(tmp))
                await bmp.SetSourceAsync(fs.AsRandomAccessStream());
            CameraImg.Source = bmp;
            Ok(CamResult, P("Snapshot captured", "影到喇"), tmp);
        }
        catch (Exception ex) { Warn(CamResult, P("Could not display image", "顯示唔到"), ex.Message); }
    }

    private async void SaveSnap_Click(object sender, RoutedEventArgs e)
    {
        if (_lastSnap is null) { Warn(CamResult, P("Take a snapshot first", "先影一格"), ""); return; }
        var path = await FileDialogs.SaveFileAsync($"ha-snapshot-{DateTime.Now:yyyyMMdd-HHmmss}", ".jpg");
        if (path is null) return;
        await System.IO.File.WriteAllBytesAsync(path, _lastSnap);
        Ok(CamResult, P("Saved", "已儲存"), path);
    }

    // ── Calendar ─────────────────────────────────────────────────────────────

    private async void LoadCals_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(CalResult)) return;
        var cals = await _ha.Calendars();
        CalendarBox.ItemsSource = cals;
        CalendarBox.DisplayMemberPath = nameof(HaEntity.Display);
        if (cals.Count > 0) CalendarBox.SelectedIndex = 0;
        Ok(CalResult, P($"{cals.Count} calendars", $"{cals.Count} 個日曆"), "");
    }

    private async void Today_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(CalResult)) return;
        if (CalendarBox.SelectedItem is not HaEntity cal) { Warn(CalResult, P("Pick a calendar", "揀個日曆"), P("Press Load.", "撳載入。")); return; }
        var start = DateTime.Today;
        var end = start.AddDays(1);
        var events = await _ha.CalendarEvents(cal.EntityId, start, end);
        _calEvents.Clear();
        foreach (var ev in events) _calEvents.Add(ev);
        Ok(CalResult, P($"{events.Count} events today", $"今日 {events.Count} 個節目"), "");
    }

    // ── AC Defender Docker deployment ───────────────────────────────────────

    private HaAcDefenderRequest BuildAcRequest()
    {
        var repo = (AcRepoBox.Text ?? "").Trim();
        var climate = (AcClimateBox.Text ?? "").Trim();
        if (repo.Length == 0) throw new InvalidOperationException(P("Pick a repository folder first.", "請先揀 repo 資料夾。"));
        if (!System.IO.Directory.Exists(repo)) throw new System.IO.DirectoryNotFoundException(repo);
        if (climate.Length == 0) throw new InvalidOperationException(P("Enter a climate entity id.", "請填冷氣 entity id。"));
        return new HaAcDefenderRequest(
            repo,
            HomeAssistantAcDefenderService.ProjectName(AcProjectBox.Text ?? HomeAssistantAcDefenderService.DefaultProjectName),
            climate,
            double.IsNaN(AcCoolAboveBox.Value) ? 28 : AcCoolAboveBox.Value,
            double.IsNaN(AcHeatBelowBox.Value) ? 16 : AcHeatBelowBox.Value,
            double.IsNaN(AcPollBox.Value) ? 60 : (int)Math.Round(AcPollBox.Value),
            AcDryRunToggle.IsOn);
    }

    private void ShowAcArtifacts(HaAcDefenderArtifacts a)
    {
        AcOut.Text = string.Join(Environment.NewLine, new[]
        {
            P("Generated:", "已產生："),
            a.Folder,
            a.Dockerfile,
            a.Compose,
            a.App,
            a.Readme,
            a.DeployScript,
        });
    }

    private void AcLocate_Click(object sender, RoutedEventArgs e)
    {
        var found = HomeAssistantAcDefenderService.LocateCandidate();
        if (found is null)
        {
            Warn(AcResult, P("No candidate repo found", "搵唔到候選 repo"), HomeAssistantAcDefenderService.GitHubRoot());
            return;
        }
        AcRepoBox.Text = found;
        Ok(AcResult, P("Repository located", "已搵到 repo"), found);
    }

    private async void AcBrowse_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Pick AC Defender repository", "揀 AC Defender repo"));
        if (folder is null) return;
        AcRepoBox.Text = folder;
        _acDefender.RepositoryPath = folder;
    }

    private void AcGenerate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var artifacts = _acDefender.Generate(BuildAcRequest());
            ShowAcArtifacts(artifacts);
            Ok(AcResult, P("Deployment files generated", "已產生部署檔"), artifacts.Folder);
        }
        catch (Exception ex) { Warn(AcResult, P("Could not generate files", "產生唔到檔案"), ex.Message); }
    }

    private async void AcExport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var req = BuildAcRequest();
            var path = await FileDialogs.SaveFileAsync($"{req.ProjectName}-ssh-bundle.zip", ".zip");
            if (path is null) return;
            var zip = _acDefender.ExportBundle(req, path);
            AcOut.Text = zip;
            Ok(AcResult, P("SSH deployment bundle exported", "已匯出 SSH 部署 bundle"), zip);
        }
        catch (Exception ex) { Warn(AcResult, P("Could not export bundle", "匯出唔到 bundle"), ex.Message); }
    }

    private async Task<ComposeProject> LoadAcComposeForLocal()
    {
        _ha.SaveConfig(UrlBox.Text, TokenBox.Password);
        if (!_ha.IsConfigured) throw new InvalidOperationException(P("Save the Home Assistant URL and token first.", "請先儲存 Home Assistant URL 同權杖。"));

        var req = BuildAcRequest();
        var artifacts = _acDefender.Generate(req);
        ShowAcArtifacts(artifacts);
        var project = ComposeParser.Parse(await System.IO.File.ReadAllTextAsync(artifacts.Compose), req.ProjectName);
        foreach (var svc in project.Services)
        {
            svc.Env["HA_URL"] = _ha.BaseUrl;
            svc.Env["HA_TOKEN"] = _ha.Token;
            for (int i = 0; i < svc.Volumes.Count; i++)
            {
                if (svc.Volumes[i].StartsWith("./", StringComparison.Ordinal))
                    svc.Volumes[i] = System.IO.Path.Combine(artifacts.Folder, svc.Volumes[i][2..]).Replace('\\', '/');
            }
        }
        return project;
    }

    private async Task EnsureDocker()
    {
        if (_docker.Connected) return;
        await _docker.ConnectAsync();
    }

    private async void AcStart_Click(object sender, RoutedEventArgs e)
    {
        AcBusy.IsActive = true;
        AcStartBtn.IsEnabled = false;
        try
        {
            var project = await LoadAcComposeForLocal();
            await EnsureDocker();
            var rep = new Progress<string>(line => DispatcherQueue.TryEnqueue(() =>
                AcOut.Text += (AcOut.Text.Length > 0 ? Environment.NewLine : "") + line));
            await _docker.ComposeUpAsync(project, rep);
            Ok(AcResult, P("Local AC Defender stack started", "本機冷氣防護堆疊已啟動"), project.Name);
        }
        catch (Exception ex) { Warn(AcResult, P("Could not start local Docker stack", "啟動唔到本機 Docker 堆疊"), ex.Message); }
        finally { AcBusy.IsActive = false; AcStartBtn.IsEnabled = true; }
    }

    private async void AcStop_Click(object sender, RoutedEventArgs e)
    {
        AcBusy.IsActive = true;
        AcStopBtn.IsEnabled = false;
        try
        {
            var name = HomeAssistantAcDefenderService.ProjectName(AcProjectBox.Text ?? HomeAssistantAcDefenderService.DefaultProjectName);
            await EnsureDocker();
            AcOut.Text = "";
            var rep = new Progress<string>(line => DispatcherQueue.TryEnqueue(() =>
                AcOut.Text += (AcOut.Text.Length > 0 ? Environment.NewLine : "") + line));
            await _docker.ComposeDownAsync(name, rep);
            Ok(AcResult, P("Local AC Defender stack stopped", "本機冷氣防護堆疊已停止"), name);
        }
        catch (Exception ex) { Warn(AcResult, P("Could not stop local Docker stack", "停止唔到本機 Docker 堆疊"), ex.Message); }
        finally { AcBusy.IsActive = false; AcStopBtn.IsEnabled = true; }
    }

    private async void AcStatus_Click(object sender, RoutedEventArgs e)
    {
        AcBusy.IsActive = true;
        try
        {
            var name = HomeAssistantAcDefenderService.ProjectName(AcProjectBox.Text ?? HomeAssistantAcDefenderService.DefaultProjectName);
            await EnsureDocker();
            var rows = await _docker.ListContainersAsync(true);
            var mine = rows.Where(c => c.Labels is not null &&
                c.Labels.TryGetValue("com.docker.compose.project", out var pn) && pn == name).ToList();
            if (mine.Count == 0)
            {
                AcOut.Text = P("No local containers found for this project.", "呢個專案搵唔到本機容器。");
                Warn(AcResult, P("No stack found", "搵唔到堆疊"), name);
                return;
            }
            AcOut.Text = string.Join(Environment.NewLine, mine.Select(c =>
                $"{(c.Names.FirstOrDefault() ?? c.ID).TrimStart('/'),-32} {c.State,-12} {c.Status}"));
            Ok(AcResult, P($"{mine.Count} local container(s)", $"{mine.Count} 個本機容器"), name);
        }
        catch (Exception ex) { Warn(AcResult, P("Could not read local Docker status", "讀唔到本機 Docker 狀態"), ex.Message); }
        finally { AcBusy.IsActive = false; }
    }

    // ── Error log ────────────────────────────────────────────────────────────

    private async void Tail_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard(CfgResult)) { Tabs.SelectedItem = TabLog; return; }
        LogBusy.IsActive = true;
        try
        {
            var r = await _ha.ErrorLog();
            LogOut.Text = r.Ok ? (r.Body.Length == 0 ? P("(log is empty)", "（log 係空嘅）") : r.Body)
                               : P($"[HTTP {r.Status}] ", $"[HTTP {r.Status}] ") + r.Body;
        }
        finally { LogBusy.IsActive = false; }
    }

    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        var dp = new DataPackage();
        dp.SetText(LogOut.Text ?? "");
        Clipboard.SetContent(dp);
    }
}

/// <summary>切換清單一行嘅動作 · What a toggle-list row asked the page to do.</summary>
public enum HaRowAction { Toggle, On, Off, Brightness }

/// <summary>
/// 切換清單一行嘅 view-model · One row in the native toggle list (light / plug / switch / input_boolean).
/// 雙向綁定 ToggleSwitch、燈光度滑桿；programmatic 更新時用 <see cref="Suppress"/> 避免回呼。
/// Two-way binds a ToggleSwitch (and, for lights, a brightness slider). When state is refreshed
/// from HA we set <see cref="Suppress"/> so the Toggled handler does not fire a redundant call.
/// </summary>
public sealed class HaToggleRow : INotifyPropertyChanged
{
    private readonly Func<HaToggleRow, HaRowAction, Task> _onAction;
    private bool _isOn;
    private int _brightnessPctValue;
    private string _applyBrightnessLabel = "Apply brightness · 套用光度";
    private string _brightnessLabel = "";

    public HaToggleRow(HaEntity ent, Func<HaToggleRow, HaRowAction, Task> onAction)
    {
        _onAction = onAction;
        EntityId = ent.EntityId;
        IsLight = ent.IsLight;
        Apply(ent);
    }

    public string EntityId { get; }
    public bool IsLight { get; }
    public string Name { get; private set; } = "";

    /// <summary>True while a programmatic update is in flight, so RowToggle_Toggled skips its callback.</summary>
    public bool Suppress { get; private set; }

    public Microsoft.UI.Xaml.Visibility IsLightVisibility =>
        IsLight ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public bool IsOn
    {
        get => _isOn;
        set { if (_isOn != value) { _isOn = value; OnPropertyChanged(); } }
    }

    /// <summary>Brightness 0–100 bound to the slider (double-friendly for the Slider).</summary>
    public double BrightnessPctValue
    {
        get => _brightnessPctValue;
        set
        {
            int v = (int)Math.Round(value);
            if (_brightnessPctValue != v)
            {
                _brightnessPctValue = v;
                OnPropertyChanged();
                BrightnessLabel = $"{v}%";
            }
        }
    }

    public int BrightnessPctValueInt => _brightnessPctValue;

    public string BrightnessLabel
    {
        get => _brightnessLabel;
        private set { if (_brightnessLabel != value) { _brightnessLabel = value; OnPropertyChanged(); } }
    }

    public string ApplyBrightnessLabel
    {
        get => _applyBrightnessLabel;
        private set { if (_applyBrightnessLabel != value) { _applyBrightnessLabel = value; OnPropertyChanged(); } }
    }

    /// <summary>由 HA 嘅最新 state 更新呢行（唔會觸發服務呼叫）· Apply fresh HA state without firing a call.</summary>
    public void Apply(HaEntity ent)
    {
        Suppress = true;
        try
        {
            Name = ent.Name;
            OnPropertyChanged(nameof(Name));
            IsOn = ent.IsOn;
            if (IsLight)
            {
                int pct = ent.BrightnessPct ?? (ent.IsOn ? 100 : 0);
                _brightnessPctValue = pct;
                OnPropertyChanged(nameof(BrightnessPctValue));
                BrightnessLabel = $"{pct}%";
            }
        }
        finally { Suppress = false; }
    }

    public void RefreshLabels() =>
        ApplyBrightnessLabel = Loc.I.Pick("Apply brightness · 套用光度", "套用光度");

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
