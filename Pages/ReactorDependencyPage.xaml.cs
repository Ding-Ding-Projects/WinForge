using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Recovery page shown when a reactor-dependent module is opened without enough live reactor power.
/// </summary>
public sealed partial class ReactorDependencyPage : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(800) };
    private ReactorDependencyPageContext? _context;
    private bool _navigating;

    public ReactorDependencyPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Loc.I.LanguageChanged += OnLanguageChanged;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _context = e.Parameter as ReactorDependencyPageContext;
        _navigating = false;
        Render();
        UpdateStatus();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try { ReactorStatusApiService.I.Start(); } catch { }
        _timer.Tick += Timer_Tick;
        _timer.Start();
        UpdateStatus();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
        Loc.I.LanguageChanged -= OnLanguageChanged;
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

        Header.Title = P($"{nameEn} needs reactor power", $"{nameZh} 需要反應堆供電");
        Header.Subtitle = P(
            "Some high-load apps are wired to WinForge's simulated nuclear plant. Start the reactor and bring the generator on-load to unlock this app.",
            "部分高負載 app 已接駁到 WinForge 模擬核電廠。請啟動反應堆並將發電機帶載，先可以解鎖此 app。");

        RequirementTitle.Text = P("Reactor dependency", "反應堆相依");
        RequirementText.Text = P(
            $"Required bus power: {mw:0} MWe, generating, no SCRAM, no meltdown.",
            $"所需電網功率：{mw:0} MWe，必須發電中、無 SCRAM、無熔毀。");
        ReasonText.Text = P(dep?.ReasonEn ?? "", dep?.ReasonZh ?? "");

        OpenReactorButton.Content = P("Open reactor", "開啟反應堆");
        RetryButton.Content = P("Retry app", "重試 app");
        EnableApiButton.Content = P("Enable status API", "啟用狀態 API");
        SettingsButton.Content = P("Reactor settings", "反應堆設定");
    }

    private void UpdateStatus()
    {
        if (_context is null) return;

        var snapshot = ReactorStatusApiService.I.LastSnapshot;
        var check = ReactorDependencyService.Evaluate(_context.Dependency, snapshot, ReactorStatusApiService.I.Enabled);

        StatusBar.Severity = check.IsSatisfied
            ? InfoBarSeverity.Success
            : snapshot.IsMeltdown
                ? InfoBarSeverity.Error
                : InfoBarSeverity.Warning;
        StatusBar.Title = P(check.StatusEn, check.StatusZh);
        StatusBar.Message = P(check.DetailEn, check.DetailZh);

        SnapshotText.Text = P(
            $"Live bus: mode={snapshot.Mode ?? "Offline"}, generating={snapshot.IsGenerating}, electric={snapshot.ElectricMW:0.0} MWe, sequence={snapshot.Sequence}",
            $"即時電網：模式={snapshot.Mode ?? "Offline"}，發電={snapshot.IsGenerating}，電功率={snapshot.ElectricMW:0.0} MWe，序號={snapshot.Sequence}");

        EnableApiButton.Visibility = ReactorStatusApiService.I.Enabled ? Visibility.Collapsed : Visibility.Visible;

        if (check.IsSatisfied && !_navigating && !string.IsNullOrWhiteSpace(_context.TargetTag))
        {
            _navigating = true;
            Navigator.GoToModule?.Invoke(_context.TargetTag);
        }
    }

    private void OpenReactor_Click(object sender, RoutedEventArgs e) => Navigator.GoToModule?.Invoke("module.reactor");

    private void Settings_Click(object sender, RoutedEventArgs e) => Navigator.GoToModule?.Invoke("module.reactorsettings");

    private void Retry_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatus();
        if (_context is not null && ReactorDependencyService.Evaluate(_context.Dependency, ReactorStatusApiService.I.LastSnapshot, ReactorStatusApiService.I.Enabled).IsSatisfied)
            Navigator.GoToModule?.Invoke(_context.TargetTag);
    }

    private void EnableApi_Click(object sender, RoutedEventArgs e)
    {
        try { ReactorStatusApiService.I.SetEnabled(true); } catch { }
        try { ReactorStatusApiService.I.Start(); } catch { }
        UpdateStatus();
    }
}
