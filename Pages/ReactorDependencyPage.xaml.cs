using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Recovery page shown when a deliberately power-gated module lacks either its preferred nuclear
/// source or an explicitly enabled, manually started emergency-diesel fallback.
/// </summary>
public sealed partial class ReactorDependencyPage : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(800) };
    private ReactorDependencyPageContext? _context;
    private bool _languageSubscribed;

    public ReactorDependencyPage()
    {
        InitializeComponent();
        _timer.Tick += Timer_Tick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _context = e.Parameter as ReactorDependencyPageContext;
        Render();
        UpdateStatus();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SubscribeLanguage();
        try { ReactorStatusApiService.I.Start(); } catch { }
        _timer.Start();
        UpdateStatus();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
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

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Render();
        UpdateStatus();
    }

    private void Timer_Tick(object? sender, object e) => UpdateStatus();

    private void Render()
    {
        var dep = _context?.Dependency;
        string nameEn = dep?.NameEn ?? P("This app", "呢個 app");
        string nameZh = dep?.NameZh ?? P("此應用程式", "This app");
        double mw = dep?.MinimumElectricMW ?? 1;
        bool allowFallback = ReactorFeaturePowerService.I.AllowEmergencyDieselFallback;

        Header.Title = P($"{nameEn} needs feature-bus power", $"{nameZh} 需要功能匯流排電力");
        Header.Subtitle = P(
            allowFallback
                ? "The live nuclear bus is preferred. Because this gate is just for fun, you may instead fill and manually start the session-only simulated emergency diesel below."
                : "The live nuclear bus is required. You can optionally allow a manually started, session-only simulated emergency-diesel fallback.",
            allowFallback
                ? "即時核電匯流排係首選。因為呢個閘門純粹玩吓，你亦可以喺下面為只限今次 session 嘅模擬柴油機入油，再手動啟動。"
                : "而家需要即時核電匯流排。你可以選擇容許一部要手動啟動、只限今次 session 嘅模擬應急柴油機做後備。");

        RequirementTitle.Text = P("Feature-power dependency", "功能電源相依");
        RequirementText.Text = P(
            allowFallback
                ? $"Required: {mw:0} MWe from a healthy generating reactor, or one of the " +
                  $"{ReactorFeaturePowerService.EmergencyDieselMaxModules} fueled EDG module outlets."
                : $"Required nuclear bus power: {mw:0} MWe, generating, no SCRAM, no meltdown.",
            allowFallback
                ? $"所需電力：健康發電中嘅反應堆提供 {mw:0} MWe，或者已入油柴油機嘅 " +
                  $"{ReactorFeaturePowerService.EmergencyDieselMaxModules} 個模組插槽其中一個。"
                : $"所需核電匯流排功率：{mw:0} MWe，必須發電中、無 SCRAM、無熔毀。");
        ReasonText.Text = P(dep?.ReasonEn ?? "", dep?.ReasonZh ?? "");

        DieselTitle.Text = P("Simulated emergency-diesel fallback", "模擬應急柴油後備");
        DieselDescription.Text = P(
            $"This is a local game mechanic, not plant equipment. Fill its {ReactorFeaturePowerService.EmergencyDieselFuelCapacityLitres:0} L tank, then start it manually. " +
            $"Fuel, running state, and its {ReactorFeaturePowerService.EmergencyDieselMaxModules} module outlets reset each app session; no real hardware or Windows power setting is touched.",
            $"呢個只係本機遊戲機制，唔係電站設備。請先為 {ReactorFeaturePowerService.EmergencyDieselFuelCapacityLitres:0} L 油缸入滿油，再手動啟動。" +
            $"油量、運行狀態同 {ReactorFeaturePowerService.EmergencyDieselMaxModules} 個模組插槽每次 app session 都會重設；唔會掂真實硬件或者 Windows 電源設定。");
        AutomationProperties.SetName(
            DieselProgress,
            P("Emergency diesel start progress", "應急柴油發電機啟動進度"));
        AutomationProperties.SetName(
            DieselFuelProgress,
            P("Emergency diesel fuel level", "應急柴油發電機油量"));
        FillDieselButton.Content = P("Fill diesel tank", "為柴油缸入滿油");
        StartDieselButton.Content = P("Start emergency diesel", "啟動應急柴油發電機");
        StopDieselButton.Content = P("Stop emergency diesel", "停止應急柴油發電機");
        EnableFallbackButton.Content = P("Allow emergency-diesel fallback", "容許應急柴油後備");
        OpenReactorButton.Content = P("Open reactor", "開啟反應堆");
        RetryButton.Content = P("Retry app", "重試 app");
        EnableApiButton.Content = P("Enable status API", "啟用狀態 API");
        SettingsButton.Content = P("Reactor settings", "反應堆設定");

        DieselPanel.Visibility = allowFallback ? Visibility.Visible : Visibility.Collapsed;
        EnableFallbackButton.Visibility = allowFallback ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateStatus()
    {
        if (_context is null) return;

        var snapshot = ReactorStatusApiService.I.LastSnapshot;
        var featurePower = ReactorFeaturePowerService.I;
        var diesel = featurePower.EmergencyDiesel;
        var check = ReactorDependencyService.EvaluateConfigured(
            _context.Dependency,
            snapshot,
            ReactorStatusApiService.I.Enabled,
            _context.OwnerToken);

        StatusBar.Severity = check.IsSatisfied
            ? InfoBarSeverity.Success
            : snapshot.IsMeltdown && !featurePower.AllowEmergencyDieselFallback
                ? InfoBarSeverity.Error
                : InfoBarSeverity.Warning;
        StatusBar.Title = P(check.StatusEn, check.StatusZh);
        StatusBar.Message = P(check.DetailEn, check.DetailZh);

        string dieselStateEn = diesel.State switch
        {
            FeatureEmergencyDieselState.Starting => "Starting",
            FeatureEmergencyDieselState.Running => "Running",
            _ => "Stopped",
        };
        string dieselStateZh = diesel.State switch
        {
            FeatureEmergencyDieselState.Starting => "啟動中",
            FeatureEmergencyDieselState.Running => "運行中",
            _ => "已停",
        };
        string reactorModeZh = (snapshot.Mode ?? "").Trim().ToLowerInvariant() switch
        {
            "shutdown" => "停堆",
            "startup" => "啟動",
            "run" => "運行",
            "tripped" => "跳機",
            "meltdown" => "熔毀",
            "offline" or "" => "離線",
            _ => "未知",
        };
        string generatingZh = snapshot.IsGenerating ? "有" : "無";
        SnapshotText.Text = P(
            $"Nuclear bus: mode={snapshot.Mode ?? "Offline"}, generating={snapshot.IsGenerating}, electric={snapshot.ElectricMW:0.0} MWe, sequence={snapshot.Sequence}\n" +
            $"Feature EDG: state={dieselStateEn}, fuel={diesel.FuelLitres:0.0}/{diesel.FuelCapacityLitres:0.0} L, " +
            $"start={diesel.StartProgressSeconds:0.0}/{diesel.StartTimeSeconds:0.0} s, modules={diesel.ActiveModuleCount}/{diesel.MaxModuleCount}",
            $"核電匯流排：模式={reactorModeZh}，發電={generatingZh}，電功率={snapshot.ElectricMW:0.0} MWe，序號={snapshot.Sequence}\n" +
            $"功能柴油機：狀態={dieselStateZh}，油量={diesel.FuelLitres:0.0}/{diesel.FuelCapacityLitres:0.0} L，" +
            $"啟動={diesel.StartProgressSeconds:0.0}/{diesel.StartTimeSeconds:0.0} 秒，模組={diesel.ActiveModuleCount}/{diesel.MaxModuleCount}");

        DieselFuelProgress.Maximum = diesel.FuelCapacityLitres;
        DieselFuelProgress.Value = diesel.FuelLitres;
        DieselFuelText.Text = P(
            $"Fuel {diesel.FuelLitres:0.0}/{diesel.FuelCapacityLitres:0.0} L · " +
            $"{diesel.FuelBurnLitresPerMinute:0.0} L/min · module outlets {diesel.ActiveModuleCount}/{diesel.MaxModuleCount}",
            $"油量 {diesel.FuelLitres:0.0}/{diesel.FuelCapacityLitres:0.0} L · " +
            $"每分鐘耗油 {diesel.FuelBurnLitresPerMinute:0.0} L · 模組插槽 {diesel.ActiveModuleCount}/{diesel.MaxModuleCount}");
        DieselProgress.Maximum = diesel.StartTimeSeconds;
        DieselProgress.Value = diesel.StartProgressSeconds;
        DieselStateText.Text = diesel.State switch
        {
            FeatureEmergencyDieselState.Starting => P(
                $"Starting — {diesel.RemainingStartSeconds:0.0} seconds to rated output.",
                $"啟動中 — 仲有 {diesel.RemainingStartSeconds:0.0} 秒達到額定輸出。"),
            FeatureEmergencyDieselState.Running => P(
                $"Running — {diesel.AvailableModuleSlots} of {diesel.MaxModuleCount} module outlets free.",
                $"運行中 — {diesel.MaxModuleCount} 個模組插槽尚餘 {diesel.AvailableModuleSlots} 個。"),
            _ => P(
                diesel.HasFuel
                    ? "Stopped and fueled — manual start required."
                    : "Stopped and empty — fill the diesel tank before starting.",
                diesel.HasFuel
                    ? "已停而且有油 — 仍要手動啟動。"
                    : "已停而且無油 — 啟動前請先為柴油缸入滿油。"),
        };
        FillDieselButton.Visibility =
            diesel.State == FeatureEmergencyDieselState.Stopped
            && diesel.FuelLitres < diesel.FuelCapacityLitres
                ? Visibility.Visible
                : Visibility.Collapsed;
        StartDieselButton.Visibility = diesel.State == FeatureEmergencyDieselState.Stopped
            ? Visibility.Visible
            : Visibility.Collapsed;
        StopDieselButton.Visibility = diesel.State == FeatureEmergencyDieselState.Stopped
            ? Visibility.Collapsed
            : Visibility.Visible;
        StartDieselButton.IsEnabled = featurePower.AllowEmergencyDieselFallback && diesel.HasFuel;
        DieselPanel.Visibility = featurePower.AllowEmergencyDieselFallback
            ? Visibility.Visible
            : Visibility.Collapsed;
        EnableFallbackButton.Visibility = featurePower.AllowEmergencyDieselFallback
            ? Visibility.Collapsed
            : Visibility.Visible;

        EnableApiButton.Visibility = ReactorStatusApiService.I.Enabled
            ? Visibility.Collapsed
            : Visibility.Visible;
        RetryButton.IsEnabled = check.IsSatisfied;
    }

    private void OpenReactor_Click(object sender, RoutedEventArgs e) => Navigator.GoToModule?.Invoke("module.reactor");

    private void Settings_Click(object sender, RoutedEventArgs e) => Navigator.GoToModule?.Invoke("module.reactorsettings");

    private void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (_context is not null && ReactorDependencyService.EvaluateConfigured(
                    _context.Dependency,
                    ReactorStatusApiService.I.LastSnapshot,
                    ReactorStatusApiService.I.Enabled,
                    _context.OwnerToken).IsSatisfied)
            Navigator.GoToModule?.Invoke(_context.TargetTag);
        else
            UpdateStatus();
    }

    private void EnableApi_Click(object sender, RoutedEventArgs e)
    {
        try { ReactorStatusApiService.I.SetEnabled(true); } catch { }
        try { ReactorStatusApiService.I.Start(); } catch { }
        UpdateStatus();
    }

    private void EnableFallback_Click(object sender, RoutedEventArgs e)
    {
        ReactorFeaturePowerService.I.AllowEmergencyDieselFallback = true;
        Render();
        UpdateStatus();
    }

    private void StartDiesel_Click(object sender, RoutedEventArgs e)
    {
        ReactorFeaturePowerService.I.StartEmergencyDiesel();
        UpdateStatus();
    }

    private void FillDiesel_Click(object sender, RoutedEventArgs e)
    {
        ReactorFeaturePowerService.I.FillEmergencyDiesel();
        UpdateStatus();
    }

    private void StopDiesel_Click(object sender, RoutedEventArgs e)
    {
        ReactorFeaturePowerService.I.StopEmergencyDiesel();
        UpdateStatus();
    }
}
