using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Visual Studio installer panel · Lists installed VS instances, exports/imports .vsconfig, and applies
/// workload/component changes in-app. Uses the real Visual Studio Installer and winget.
/// </summary>
public sealed partial class VisualStudioInstallerModule : Page
{
    private readonly List<VsInstance> _instances = new();
    private ComboBox _editionBox = null!;
    private TextBox _configBox = null!;
    private TextBox _addBox = null!;
    private TextBox _removeBox = null!;
    private ComboBox _instanceBox = null!;

    public VisualStudioInstallerModule()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged += OnLanguageChanged;
        Render();
        await RefreshAsync();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Visual Studio Installer · Visual Studio 安裝器";
        HeaderBlurb.Text = P(
            "Manage installed Visual Studio instances in-app: export .vsconfig, import it back into an existing install, apply workload / component IDs, or kick off a new edition install with an existing config file.",
            "喺 app 內管理已裝嘅 Visual Studio：匯出 .vsconfig、再匯返去現有安裝、套用工作負載／組件 ID，或者用現成 config 開始新版本安裝。");

        Root.Children.Clear();

        Root.Children.Add(new TextBlock
        {
            Text = P(
                "This page talks to the real Visual Studio Installer (`setup.exe`) and `vswhere.exe` under `C:\\Program Files (x86)\\Microsoft Visual Studio\\Installer`.",
                "呢頁會直接用真嘅 Visual Studio Installer（`setup.exe`）同 `vswhere.exe`，位置喺 `C:\\Program Files (x86)\\Microsoft Visual Studio\\Installer`。"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        var actionGrid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _instanceBox = new ComboBox
        {
            MinWidth = 260,
            PlaceholderText = P("Select a Visual Studio instance", "揀一個 Visual Studio instance"),
            ItemsSource = _instances,
            DisplayMemberPath = nameof(VsInstance.Summary),
        };
        actionGrid.Children.Add(_instanceBox);

        var refresh = new Button { Content = P("Refresh", "重新整理") };
        refresh.Click += async (_, _) => await RefreshAsync();
        Grid.SetColumn(refresh, 1);
        actionGrid.Children.Add(refresh);

        var openInstaller = new Button { Content = P("Open Installer folder", "開 Installer 資料夾") };
        openInstaller.Click += async (_, _) =>
        {
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer");
                await Windows.System.Launcher.LaunchUriAsync(new Uri("file:///" + folder.Replace('\\', '/').TrimEnd('/')));
            }
            catch { }
        };
        Grid.SetColumn(openInstaller, 2);
        actionGrid.Children.Add(openInstaller);

        Root.Children.Add(actionGrid);

        var configRow = new Grid { ColumnSpacing = 8 };
        configRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        configRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        configRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _configBox = new TextBox
        {
            PlaceholderText = P("Pick a .vsconfig file", "揀 .vsconfig 檔案"),
            MinWidth = 320,
        };
        configRow.Children.Add(_configBox);

        var browseConfig = new Button { Content = P("Browse…", "瀏覽…") };
        browseConfig.Click += async (_, _) =>
        {
            var path = await FileDialogs.OpenFileAsync(".vsconfig");
            if (path is not null) _configBox.Text = path;
        };
        Grid.SetColumn(browseConfig, 1);
        configRow.Children.Add(browseConfig);

        var applyConfig = new Button { Content = P("Apply config", "套用 config") };
        applyConfig.Click += async (_, _) => await ApplyConfigAsync();
        Grid.SetColumn(applyConfig, 2);
        configRow.Children.Add(applyConfig);

        Root.Children.Add(configRow);

        var installRow = new Grid { ColumnSpacing = 8 };
        installRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        installRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        installRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _editionBox = new ComboBox { MinWidth = 180 };
        _editionBox.Items.Add(P("Community", "Community"));
        _editionBox.Items.Add(P("Professional", "Professional"));
        _editionBox.Items.Add(P("Enterprise", "Enterprise"));
        _editionBox.Items.Add(P("Build Tools", "Build Tools"));
        _editionBox.SelectedIndex = 0;
        installRow.Children.Add(_editionBox);

        var install = new Button { Content = P("Install edition", "安裝版本") };
        install.Click += async (_, _) => await InstallEditionAsync();
        Grid.SetColumn(install, 1);
        installRow.Children.Add(install);

        var export = new Button { Content = P("Export .vsconfig", "匯出 .vsconfig") };
        export.Click += async (_, _) => await ExportConfigAsync();
        Grid.SetColumn(export, 2);
        installRow.Children.Add(export);

        Root.Children.Add(installRow);

        Root.Children.Add(new TextBlock
        {
            Text = P("Workload / component IDs", "工作負載／組件 ID"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        _addBox = new TextBox
        {
            PlaceholderText = P("Add IDs, one per line (for example: Microsoft.VisualStudio.Workload.CoreEditor)", "逐行輸入要加嘅 ID（例如：Microsoft.VisualStudio.Workload.CoreEditor）"),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 80,
        };
        Root.Children.Add(_addBox);

        _removeBox = new TextBox
        {
            PlaceholderText = P("Remove IDs, one per line", "逐行輸入要移除嘅 ID"),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 64,
        };
        Root.Children.Add(_removeBox);

        var workloadButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var applyWorkloads = new Button { Content = P("Modify workloads", "修改 workloads") };
        applyWorkloads.Click += async (_, _) => await ApplyWorkloadsAsync();
        workloadButtons.Children.Add(applyWorkloads);

        var exportHint = new TextBlock
        {
            Text = P("Tip: save a config from one instance, then apply it to another.", "提示：先由一個 instance 匯出 config，再套去另一個。"),
            Margin = new Thickness(12, 0, 0, 0),
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            VerticalAlignment = VerticalAlignment.Center,
        };
        workloadButtons.Children.Add(exportHint);
        Root.Children.Add(workloadButtons);

        Root.Children.Add(new TextBlock
        {
            Text = P("Installed instances", "已安裝 instance"),
            Margin = new Thickness(0, 4, 0, 0),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        if (_instances.Count == 0)
        {
            Root.Children.Add(new TextBlock
            {
                Text = P("No Visual Studio instances were found yet. If Visual Studio is installed, make sure the Visual Studio Installer is present too.", "未搵到任何 Visual Studio instance。如果已裝 Visual Studio，請確認 Visual Studio Installer 也存在。"),
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        foreach (var instance in _instances)
            Root.Children.Add(BuildCard(instance));

        if (_instanceBox.SelectedIndex < 0 && _instances.Count > 0)
            _instanceBox.SelectedIndex = 0;
    }

    private Border BuildCard(VsInstance instance)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new FontIcon
        {
            Glyph = ((char)0xE70C).ToString(),
            FontSize = 18,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
        });

        var details = new StackPanel { Spacing = 4 };
        details.Children.Add(new TextBlock
        {
            Text = instance.DisplayName,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        details.Children.Add(new TextBlock
        {
            Text = instance.Summary,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
        });
        grid.Children.Add(Col(details, 1));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        var export = new Button { Content = P("Export", "匯出") };
        export.Click += async (_, _) => await ExportConfigAsync(instance);
        actions.Children.Add(export);

        var apply = new Button { Content = P("Apply config", "套用 config") };
        apply.Click += async (_, _) => await ApplyConfigAsync(instance);
        actions.Children.Add(apply);

        var modify = new Button { Content = P("Modify workloads", "修改 workloads") };
        modify.Click += async (_, _) => await ApplyWorkloadsAsync(instance);
        actions.Children.Add(modify);

        grid.Children.Add(Col(actions, 2));

        return new Border
        {
            Padding = new Thickness(14, 10, 14, 10),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            Child = grid,
        };
    }

    private async Task RefreshAsync()
    {
        try
        {
            _instances.Clear();
            _instances.AddRange(await VisualStudioInstallerService.ListInstancesAsync());
            Render();
            ShowStatus(InfoBarSeverity.Success, P("Visual Studio instances refreshed", "已重新整理 Visual Studio instances"), "");
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Refresh failed", "重新整理失敗"), ex.Message);
        }
    }

    private VsInstance? SelectedInstance => _instanceBox?.SelectedItem as VsInstance ?? _instances.FirstOrDefault();

    private async Task ExportConfigAsync(VsInstance? instance = null)
    {
        try
        {
            var target = instance ?? SelectedInstance;
            if (target is null)
            {
                ShowStatus(InfoBarSeverity.Warning, P("Pick a Visual Studio instance first", "請先揀一個 Visual Studio instance"), "");
                return;
            }

            var path = await FileDialogs.SaveFileAsync($"{target.Edition.ToLowerInvariant()}-{DateTime.Now:yyyyMMdd-HHmmss}", ".vsconfig");
            if (path is null) return;

            var r = await VisualStudioInstallerService.ExportConfigAsync(target, path);
            ShowStatus(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
                r.Success ? P("Exported .vsconfig", "已匯出 .vsconfig") : P("Export failed", "匯出失敗"), r.Output ?? path);
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Export failed", "匯出失敗"), ex.Message);
        }
    }

    private async Task ApplyConfigAsync(VsInstance? instance = null)
    {
        try
        {
            var target = instance ?? SelectedInstance;
            if (target is null)
            {
                ShowStatus(InfoBarSeverity.Warning, P("Pick a Visual Studio instance first", "請先揀一個 Visual Studio instance"), "");
                return;
            }

            var path = _configBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                path = await FileDialogs.OpenFileAsync(".vsconfig");
                if (path is null) return;
                _configBox.Text = path;
            }

            var r = await VisualStudioInstallerService.ModifyWithConfigAsync(target, path);
            ShowStatus(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
                r.Success ? P("Applied config", "已套用 config") : P("Apply failed", "套用失敗"), r.Output ?? path);
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Apply failed", "套用失敗"), ex.Message);
        }
    }

    private async Task ApplyWorkloadsAsync(VsInstance? instance = null)
    {
        try
        {
            var target = instance ?? SelectedInstance;
            if (target is null)
            {
                ShowStatus(InfoBarSeverity.Warning, P("Pick a Visual Studio instance first", "請先揀一個 Visual Studio instance"), "");
                return;
            }

            var add = SplitIds(_addBox.Text);
            var remove = SplitIds(_removeBox.Text);
            if (add.Count == 0 && remove.Count == 0)
            {
                ShowStatus(InfoBarSeverity.Warning, P("Add or remove at least one ID", "請至少加或移除一個 ID"), "");
                return;
            }

            var r = await VisualStudioInstallerService.ModifyWorkloadsAsync(target, add, remove);
            ShowStatus(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
                r.Success ? P("Modified workloads", "已修改 workloads") : P("Modify failed", "修改失敗"), r.Output ?? "");
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Modify failed", "修改失敗"), ex.Message);
        }
    }

    private async Task InstallEditionAsync()
    {
        try
        {
            var edition = _editionBox.SelectedIndex switch
            {
                1 => "Professional",
                2 => "Enterprise",
                3 => "Build Tools",
                _ => "Community",
            };
            var id = VisualStudioInstallerService.WingetIdForEdition(edition);
            var config = _configBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(config) || !File.Exists(config)) config = null;

            var r = await VisualStudioInstallerService.InstallEditionAsync(id, config);
            ShowStatus(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
                r.Success ? P("Install queued", "已開始安裝") : P("Install failed", "安裝失敗"), r.Output ?? id);
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Install failed", "安裝失敗"), ex.Message);
        }
    }

    private static List<string> SplitIds(string? text) =>
        (text ?? string.Empty)
            .Split(new[] { '\r', '\n', '\t', ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();

    private void ShowStatus(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.IsOpen = true;
        ResultBar.Severity = sev;
        ResultBar.Title = title;
        ResultBar.Message = msg;
    }

    private static FrameworkElement Col(FrameworkElement el, int col) { Grid.SetColumn(el, col); return el; }
}
