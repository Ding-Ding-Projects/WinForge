using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 反應堆設定 · Reactor Settings — a dedicated, scrollable, bilingual page that groups every reactor
/// control that touches the REAL computer or EXTERNAL systems, relocated off the main reactor page:
///   • Keep PC awake while generating (AwakeService, default ON);
///   • Link reactor to Windows settings — power plan / accent / brightness (ReactorSystemLinkService, default OFF);
///   • ARM real shutdown on meltdown (ReactorRealShutdownArm, default OFF, abortable countdown unchanged);
///   • Expose public status API (ReactorStatusApiService, default ON);
///   • Crash-safe autosave (PersistenceService, default ON);
///   • NEW: Mirror reactor to Home Assistant (ReactorHomeAssistantMirror, default OFF).
///
/// The page only configures these settings; the live reactor sim/tick (in <c>ReactorModule</c>) reads the
/// shared service state. The HA mirror is opt-in and fully reversible. Pure managed C#. Bilingual.
/// </summary>
public sealed partial class ReactorSettingsModule : Page
{
    private readonly DispatcherTimer _liveTimer = new() { Interval = TimeSpan.FromMilliseconds(800) };
    private bool _suppress; // guard so programmatic toggle/combobox updates don't re-fire handlers
    private bool _languageSubscribed;
    private List<HaEntity> _haLights = new();
    private List<HaEntity> _haSwitches = new();

    private sealed class HaEntityChoice
    {
        public required HaEntity Entity { get; init; }
        public string EntityId => Entity.EntityId;
        public string SearchText => $"{Entity.Display} {Entity.EntityId} {Entity.Domain}".ToLowerInvariant();
        public override string ToString() => Entity.Display;
    }

    public ReactorSettingsModule()
    {
        InitializeComponent();
        _liveTimer.Tick += OnLiveTimerTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        SubscribeLanguage();
        LoadState();
        Render();
        _liveTimer.Start();
        await LoadHaEntitiesAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _liveTimer.Stop();
        UnsubscribeLanguage();
    }

    private void SubscribeLanguage()
    {
        if (_languageSubscribed) return;
        Loc.I.LanguageChanged += OnLanguageChanged;
        _languageSubscribed = true;
    }

    private void UnsubscribeLanguage()
    {
        if (!_languageSubscribed) return;
        Loc.I.LanguageChanged -= OnLanguageChanged;
        _languageSubscribed = false;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLanguageChanged(object? s, EventArgs e) => Render();

    private void OnLiveTimerTick(object? sender, object e)
    {
        UpdateApiState();
    }

    // ============================================================ load current state ====
    private void LoadState()
    {
        _suppress = true;
        try
        {
            KeepAwakeToggle.IsOn = ReactorKeepAwakeSetting.Enabled;                 // default ON
            SysLinkToggle.IsOn = ReactorSystemLinkService.EnabledSetting;           // default OFF
            ArmToggle.IsOn = ReactorRealShutdownArm.Armed;                          // default OFF (in-memory)
            ApiToggle.IsOn = ReactorStatusApiService.I.Enabled;                     // default ON
            AutosaveToggle.IsOn = PersistenceService.I.AutosaveEnabled;             // default ON
            HaMirrorToggle.IsOn = ReactorHomeAssistantMirror.I.Enabled;            // default OFF
        }
        finally { _suppress = false; }
    }

    // ============================================================ localized labels ====
    private void Render()
    {
        Header.Title = "⚙ Reactor Settings · 反應堆設定";
        Header.Subtitle = P(
            "Controls that affect the REAL computer or EXTERNAL systems live here, separate from the pure simulation. All are reversible; the dangerous one (real shutdown) defaults OFF.",
            "會影響真實電腦或外部系統嘅控制集中喺呢度，同純模擬分開。全部可還原；最危險嗰個（真實關機）預設關閉。");

        BackToReactorButton.Content = P("← Back to reactor · 返回反應堆", "← 返回反應堆 · Back to reactor");

        // Keep awake
        KeepAwakeToggle.Header = P("Keep PC awake while generating · 發電時保持電腦喚醒",
                                   "發電時保持電腦喚醒 · Keep PC awake while generating");
        KeepAwakeToggle.OnContent = P("On", "開");
        KeepAwakeToggle.OffContent = P("Off", "關");
        KeepAwakeNote.Text = P(
            "While the simulated generator is on-load and delivering power, tell Windows the system + display are required (Win32 SetThreadExecutionState). Released automatically on SCRAM, turbine trip, offline or when you leave the reactor page.",
            "當模擬發電機帶載供電時，通知 Windows 需要保持系統同顯示器（Win32 SetThreadExecutionState）。SCRAM、汽輪機跳機、離線或離開反應堆頁面時會自動釋放。");

        // System linkage
        SysLinkToggle.Header = P("Link reactor to Windows settings · 將反應堆連動 Windows 設定",
                                 "將反應堆連動 Windows 設定 · Link reactor to Windows settings");
        SysLinkToggle.OnContent = P("On", "開");
        SysLinkToggle.OffContent = P("Off", "關");
        SysLinkWarn.Text = P(
            "Changes your Windows power plan, accent colour and screen brightness to match the reactor. All originals are restored when you turn this off or leave the reactor page. Default off.",
            "會按反應堆狀態改變你嘅 Windows 電源計劃、強調色同螢幕亮度。當你關閉此選項或離開反應堆頁面時，全部會還原為原狀。預設關閉。");

        // ARM real shutdown
        ArmTitle.Text = P("⚠ Real shutdown on meltdown · 熔毀時真實關機", "⚠ 熔毀時真實關機 · Real shutdown on meltdown");
        ArmToggle.Header = P("ARM REAL SHUTDOWN ON MELTDOWN · 啟用熔毀時真實關機",
                             "啟用熔毀時真實關機 · ARM REAL SHUTDOWN ON MELTDOWN");
        ArmToggle.OnContent = P("Armed", "已啟用");
        ArmToggle.OffContent = P("Safe (off)", "安全（關）");
        ArmWarn.Text = P(
            "⚠ WARNING: When ON, a meltdown starts a 10-second abortable countdown and then REALLY shuts down this PC (normal shutdown via Win32 API — unsaved work in other apps could be lost). Default is OFF: meltdown only shows a simulated screen and never powers off your PC. This setting resets to OFF every time the app starts.",
            "⚠ 警告：開啟後，熔毀會開始 10 秒可中止倒數，然後真實關閉呢部電腦（用 Win32 API 嘅正常關機 — 其他程式未儲存嘅工作可能會遺失）。預設為關閉：熔毀只會顯示模擬畫面，唔會關機。每次啟動 app 此設定都會重設為關閉。");

        // Status API
        ApiTitle.Text = P("Public status API · 對外狀態 API", "對外狀態 API · Public status API");
        ApiBlurb.Text = P(
            "Other local apps can read this reactor's power/status in real time and depend on it (e.g. run only while generating). Copy Sdk/ReactorStatusClient.cs into your app — no WinForge reference needed.",
            "其他本機 app 可即時讀取本反應堆嘅功率／狀態並依賴佢（例如只喺發電時運行）。將 Sdk/ReactorStatusClient.cs 複製入你嘅 app 即可，無需引用 WinForge。");
        ApiNames.Text =
            $"Pipe   \\\\.\\pipe\\{ReactorStatusApiService.PipeName}\n" +
            $"MMF    {ReactorStatusApiService.MmfName}\n" +
            $"Mutex  {ReactorStatusApiService.MutexName}";
        ApiToggle.Header = P("Expose status API · 開放狀態 API", "開放狀態 API · Expose status API");
        ApiToggle.OnContent = P("On", "開");
        ApiToggle.OffContent = P("Off", "關");
        UpdateApiState();

        // Autosave
        AutosaveToggle.Header = P("Crash-safe autosave · 防崩潰自動儲存", "防崩潰自動儲存 · Crash-safe autosave");
        AutosaveToggle.OnContent = P("On", "開");
        AutosaveToggle.OffContent = P("Off", "關");
        AutosaveNote.Text = P(
            "Periodically saves the full reactor state so a crash, shutdown or app restart resumes exactly where you left off. Default on.",
            "定期儲存反應堆完整狀態，令崩潰、關機或重啟 app 後可由斷點繼續。預設開啟。");

        // Home Assistant
        HaTitle.Text = P("Mirror reactor to Home Assistant · 反應堆連動 Home Assistant",
                         "反應堆連動 Home Assistant · Mirror reactor to Home Assistant");
        HaBlurb.Text = P(
            "Opt-in: mirror the reactor's state to your smart home. Alarm lights turn red during SCRAM or meltdown. Generating lights are held on at full-bright white while the generator is on-load, and generating switches/plugs stay on too. Default off; turning it off stops driving and restores selected entities to off.",
            "可選：將反應堆狀態連動到你嘅智能家居。警報燈喺 SCRAM 或熔毀時變紅。發電燈喺發電機帶載時保持全亮白色，發電開關／插座亦保持開啟。預設關閉；關閉時會停止驅動並將已選實體還原為熄。");
        HaNotConfiguredText.Text = P(
            "Home Assistant is not configured. Set your HA URL and token in the Home Assistant module first, then return here.",
            "未設定 Home Assistant。請先喺 Home Assistant 模組設定你嘅 HA 網址同權杖，再返嚟呢度。");
        HaOpenModuleButton.Content = P("Open Home Assistant module · 開啟 Home Assistant 模組",
                                       "開啟 Home Assistant 模組 · Open Home Assistant module");
        HaMirrorToggle.Header = P("Mirror reactor to Home Assistant · 反應堆連動 Home Assistant",
                                  "反應堆連動 Home Assistant · Mirror reactor to Home Assistant");
        HaMirrorToggle.OnContent = P("On", "開");
        HaMirrorToggle.OffContent = P("Off", "關");
        HaAlarmLabel.Text = P("Alarm lights (SCRAM / meltdown red) · 警報燈（SCRAM／熔毀紅燈）",
                              "警報燈（SCRAM／熔毀紅燈）· Alarm lights");
        HaAlarmSearchBox.PlaceholderText = P("Search alarm lights…", "搜尋警報燈…");
        HaGenLightsLabel.Text = P("Generating lights (full-bright white) · 發電燈（全亮白色）",
                                  "發電燈（全亮白色）· Generating lights");
        HaGenLightsSearchBox.PlaceholderText = P("Search generating lights…", "搜尋發電燈…");
        HaGenLabel.Text = P("Generating switches / plugs · 「發電中」開關／插座",
                            "「發電中」開關／插座 · Generating switches / plugs");
        HaGenSwitchesSearchBox.PlaceholderText = P("Search switches / plugs…", "搜尋開關／插座…");
        HaRefreshButton.Content = P("Refresh entities · 重新整理實體", "重新整理實體 · Refresh entities");

        UpdateHaVisibility();
    }

    // ============================================================ status API live ====
    private void UpdateApiState()
    {
        bool enabled = ReactorStatusApiService.I.Enabled;
        bool running = ReactorStatusApiService.I.IsRunning;
        if (!enabled)
        {
            ApiStateText.Text = P("● Disabled · 已停用", "● 已停用 · Disabled");
            ApiStateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 0x75, 0x75, 0x75));
        }
        else if (running)
        {
            ApiStateText.Text = P(
                $"● Live — seq {ReactorStatusApiService.I.LastSnapshot.Sequence} · 運行中",
                $"● 運行中 — 序號 {ReactorStatusApiService.I.LastSnapshot.Sequence} · Live");
            ApiStateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 0x4C, 0xAF, 0x50));
        }
        else
        {
            ApiStateText.Text = P("● Enabled (starting…) · 啟用中", "● 啟用中（啟動中…） · Enabled");
            ApiStateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 0xFF, 0xB3, 0x00));
        }
    }

    // ============================================================ handlers ====
    private void BackToReactor_Click(object sender, RoutedEventArgs e)
    {
        try { Navigator.GoToModule?.Invoke("module.reactor"); } catch { }
    }

    private void KeepAwake_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        ReactorKeepAwakeSetting.Enabled = KeepAwakeToggle.IsOn;
        // The live reactor page reads this each tick and releases the OS hold if turned off mid-generation.
    }

    private void SysLink_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        if (SysLinkToggle.IsOn)
            ReactorSystemLinkService.I.Enable();   // snapshots originals, begins driving on next tick
        else
            ReactorSystemLinkService.I.Disable();  // restores all originals immediately
    }

    private void Arm_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        ReactorRealShutdownArm.Armed = ArmToggle.IsOn; // in-memory only; meltdown logic itself unchanged
    }

    private void Api_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        try { ReactorStatusApiService.I.SetEnabled(ApiToggle.IsOn); } catch { }
        UpdateApiState();
    }

    private void Autosave_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        PersistenceService.I.AutosaveEnabled = AutosaveToggle.IsOn;
        if (AutosaveToggle.IsOn) PersistenceService.I.Flush(); // capture immediately when re-enabled
    }

    // ============================================================ Home Assistant ====
    private void UpdateHaVisibility()
    {
        bool configured = ReactorHomeAssistantMirror.I.IsHaConfigured;
        HaNotConfigured.Visibility = configured ? Visibility.Collapsed : Visibility.Visible;
        HaConfigured.Visibility = configured ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HaOpenModule_Click(object sender, RoutedEventArgs e)
    {
        try { Navigator.GoToModule?.Invoke("module.homeassistant"); } catch { }
    }

    private async void HaRefresh_Click(object sender, RoutedEventArgs e)
    {
        ReactorHomeAssistantMirror.I.RefreshHaConfig();
        await LoadHaEntitiesAsync();
    }

    private async System.Threading.Tasks.Task LoadHaEntitiesAsync()
    {
        UpdateHaVisibility();
        if (!ReactorHomeAssistantMirror.I.IsHaConfigured) return;

        HaStatusText.Text = P("Loading entities…", "載入實體中…");
        List<HaEntity> entities;
        try { entities = await ReactorHomeAssistantMirror.I.Ha.Controllables(); }
        catch { entities = new List<HaEntity>(); }

        // Lights for the alarm/generating pickers; switches + plugs (and input_boolean) for generating.
        _haLights = entities.Where(en => en.Domain == "light").OrderBy(en => en.Display).ToList();
        _haSwitches = entities.Where(en => en.Domain is "switch" or "input_boolean").OrderBy(en => en.Display).ToList();
        RefreshHaPickers();

        HaStatusText.Text = P(
            $"{_haLights.Count} lights · {_haSwitches.Count} switches",
            $"{_haLights.Count} 盞燈 · {_haSwitches.Count} 個開關");
    }

    private void RefreshHaPickers()
    {
        FillEntityList(HaAlarmLightsList, HaAlarmSearchBox, _haLights, ReactorHomeAssistantMirror.I.AlarmLightIds);
        FillEntityList(HaGenLightsList, HaGenLightsSearchBox, _haLights, ReactorHomeAssistantMirror.I.GenLightIds);
        FillEntityList(HaGenSwitchesList, HaGenSwitchesSearchBox, _haSwitches, ReactorHomeAssistantMirror.I.GenSwitchIds);
    }

    private void FillEntityList(ListBox list, TextBox search, List<HaEntity> source, IReadOnlyList<string> selectedIds)
    {
        var selected = new HashSet<string>(selectedIds, StringComparer.OrdinalIgnoreCase);
        string q = (search.Text ?? "").Trim().ToLowerInvariant();
        var items = source
            .Select(en => new HaEntityChoice { Entity = en })
            .Where(item => q.Length == 0 || item.SearchText.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _suppress = true;
        try
        {
            list.ItemsSource = items;
            list.SelectedItems.Clear();
            foreach (var item in items)
                if (selected.Contains(item.EntityId))
                    list.SelectedItems.Add(item);
        }
        finally { _suppress = false; }
    }

    private static IReadOnlyList<string> SelectedIds(ListBox list, IReadOnlyList<string> existingIds)
    {
        var visibleIds = list.Items.OfType<HaEntityChoice>()
            .Select(item => item.EntityId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = existingIds
            .Where(id => !visibleIds.Contains(id))
            .ToList();
        result.AddRange(list.SelectedItems.OfType<HaEntityChoice>().Select(item => item.EntityId));
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void HaSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        RefreshHaPickers();
    }

    private void HaAlarmLights_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        ReactorHomeAssistantMirror.I.SetAlarmLightIds(
            SelectedIds(HaAlarmLightsList, ReactorHomeAssistantMirror.I.AlarmLightIds));
    }

    private void HaGenLights_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        ReactorHomeAssistantMirror.I.SetGenLightIds(
            SelectedIds(HaGenLightsList, ReactorHomeAssistantMirror.I.GenLightIds));
    }

    private void HaGenSwitches_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        ReactorHomeAssistantMirror.I.SetGenSwitchIds(
            SelectedIds(HaGenSwitchesList, ReactorHomeAssistantMirror.I.GenSwitchIds));
    }

    private void HaMirror_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        ReactorHomeAssistantMirror.I.Enabled = HaMirrorToggle.IsOn;
    }

}
