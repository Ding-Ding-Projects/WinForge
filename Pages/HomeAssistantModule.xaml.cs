using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
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
    private readonly ObservableCollection<HaCalendarEvent> _calEvents = new();
    private byte[]? _lastSnap;

    public HomeAssistantModule()
    {
        InitializeComponent();
        CalList.ItemsSource = _calEvents;
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) =>
        {
            UrlBox.Text = _ha.BaseUrl;
            TokenBox.Password = _ha.Token;
            if (string.IsNullOrEmpty(TplBox.Text)) TplBox.Text = "{{ states('sun.sun') }}";
            Render();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Home Assistant · 家居助理";
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

        // Devices
        LightLbl.Text = P("Light · 燈", "燈");
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
        if (_devicesLoaded || !_ha.IsConfigured) return;
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
