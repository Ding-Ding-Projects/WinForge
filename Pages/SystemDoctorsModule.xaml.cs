using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 系統醫生 · System Doctors — guided in-app rescue routines for common Windows 11 breakages
/// (print queue, network/DNS, sleep/wake, taskbar &amp; Start, search index, Explorer, caches,
/// take-ownership). Every action runs real commands; diagnostics are parsed into native bilingual
/// lists, not raw dumps. Fully bilingual, no redirects.
/// </summary>
public sealed partial class SystemDoctorsModule : Page
{
    private bool _busy;
    private string _ownPath = "";
    private string _driverBackupFolder = "";
    private string _exportedDriverPackage = "";

    public SystemDoctorsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => Build();
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Build();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    // Application-level theme dictionaries can resolve against the Windows theme even when
    // WinForge explicitly requests the opposite theme on its window root. Use ActualTheme so
    // dynamically created text keeps AA contrast in both modes (and in automation captures).
    private Brush Sub => new SolidColorBrush(ActualTheme == ElementTheme.Light
        ? Windows.UI.Color.FromArgb(255, 73, 69, 79)
        : Windows.UI.Color.FromArgb(255, 202, 196, 208));
    private Brush Tert => new SolidColorBrush(ActualTheme == ElementTheme.Light
        ? Windows.UI.Color.FromArgb(255, 95, 91, 101)
        : Windows.UI.Color.FromArgb(255, 184, 177, 191));

    private void Build()
    {
        Header.Title = "System Doctors · 系統醫生";
        Header.Subtitle = P("Guided Windows 11 controls and rescue routines — configure, audit, back up, then repair, all in-app.",
            "Windows 11 引導式設定同急救流程 — 設定、審核、備份、再修復，全程喺 app 內。");

        if (!AdminHelper.IsElevated)
        {
            AdminBar.Severity = InfoBarSeverity.Warning;
            AdminBar.Title = P("Some doctors need administrator rights", "部分醫生需要管理員權限");
            AdminBar.Message = P("Windows Update, driver rollback, DISM association import/export, ResetBase and several rescue tools need elevation. Relaunch as admin for full effect.",
                "Windows Update、驅動回復、DISM 關聯匯入匯出、ResetBase 同部分急救工具要提升權限。以管理員身分重開先有完整效果。");
            var relaunch = new Button { Content = P("Relaunch as admin", "以管理員身分重新啟動") };
            relaunch.Click += (_, _) => { if (AdminHelper.RelaunchElevated()) Application.Current.Exit(); };
            AdminBar.ActionButton = relaunch;
            AdminBar.IsOpen = true;
        }
        else
        {
            AdminBar.IsOpen = false;
        }

        DoctorsPanel.Children.Clear();
        BuildStorageSenseDoctor();
        BuildFilterKeysDoctor();
        BuildDefaultAssociationsDoctor();
        BuildWindowsUpdateDoctor();
        BuildDriverRollbackDoctor();
        BuildStartupAuditDoctor();
        BuildComponentStoreDoctor();
        BuildStoreAppDoctor();
        BuildPrintDoctor();
        BuildNetworkDoctor();
        BuildSleepWakeDoctor();
        BuildShellDoctor();
        BuildSearchDoctor();
        BuildExplorerDoctor();
        BuildCacheDoctor();
        BuildOwnershipDoctor();
    }

    // ===================== shared card scaffolding =====================

    /// <summary>建立一張醫生卡（Expander）· Build one doctor card and return its body panel.</summary>
    private (Expander card, StackPanel body) NewCard(string glyph, string titleEn, string titleZh, string descEn, string descZh)
    {
        var header = new Grid { ColumnSpacing = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = new FontIcon { Glyph = glyph, FontSize = 20, VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(icon);
        var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        t.Children.Add(new TextBlock { Text = $"{titleEn} · {titleZh}", FontWeight = FontWeights.SemiBold });
        t.Children.Add(new TextBlock { Text = P(descEn, descZh), FontSize = 12, Foreground = Sub, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(t, 1);
        header.Children.Add(t);

        var body = new StackPanel { Spacing = 10 };
        var card = new Expander
        {
            Header = header,
            Content = body,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0),
        };
        AutomationProperties.SetName(card, $"{titleEn} · {titleZh}");
        DoctorsPanel.Children.Add(card);
        return (card, body);
    }

    /// <summary>一行動作按鈕 · A horizontal row of action buttons.</summary>
    private static WrapButtons Buttons() => new();

    private sealed class WrapButtons : StackPanel
    {
        public WrapButtons()
        {
            Orientation = Orientation.Horizontal;
            Spacing = 8;
        }
    }

    private Button MakeButton(string en, string zh, string glyph, Func<Task> onClick, bool destructive = false)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14 });
        content.Children.Add(new TextBlock { Text = $"{en} · {zh}", FontSize = 13 });
        var b = new Button { Content = content, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 0, 4) };
        AutomationProperties.SetName(b, $"{en} · {zh}");
        if (destructive) b.Background = (Brush)Application.Current.Resources["SystemFillColorCautionBackgroundBrush"];
        b.Click += async (_, _) => await Guard(onClick);
        return b;
    }

    private async Task Guard(Func<Task> work)
    {
        if (_busy) return;
        _busy = true;
        try { await work(); }
        catch (Exception ex) { ShowResult(false, P("Error", "出錯"), ex.Message); }
        finally { _busy = false; }
    }

    /// <summary>顯示動作結果（橫額）· Show an action result in the bottom InfoBar.</summary>
    private void ShowResult(bool ok, string title, string message)
    {
        ResultBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = title;
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }

    private void ShowTweakResult(TweakResult r, string verb, StackPanel? outputHost = null)
    {
        bool needAdmin = !r.Success && !AdminHelper.IsElevated;
        ShowResult(r.Success, r.Success ? P("Done", "完成") : P("Failed", "失敗"),
            needAdmin
                ? P($"{verb} needs administrator rights.", $"{verb}需要管理員權限。")
                : $"{verb} — {(r.Success ? "OK" : (r.Message?.Primary ?? ""))}");
        if (outputHost is not null && !string.IsNullOrWhiteSpace(r.Output))
            RenderOutputPane(outputHost, r.Output!);
    }

    /// <summary>一個等寬、可捲動、可複製嘅輸出面板 · A monospace, scrollable, copyable output pane.</summary>
    private void RenderOutputPane(StackPanel host, string text)
    {
        host.Children.Clear();
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        bar.Children.Add(new TextBlock { Text = P("Output", "輸出"), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Foreground = Sub });
        var copy = new Button { Content = P("Copy", "複製"), Padding = new Thickness(10, 3, 10, 3) };
        copy.Click += (_, _) =>
        {
            var dp = new DataPackage();
            dp.SetText(text);
            Clipboard.SetContent(dp);
        };
        bar.Children.Add(copy);
        host.Children.Add(bar);

        var box = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            MaxHeight = 220,
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            BorderThickness = new Thickness(1),
        };
        ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(box, ScrollBarVisibility.Auto);
        host.Children.Add(box);
    }

    /// <summary>渲染診斷清單 · Render a parsed diagnostic report into a host panel.</summary>
    private void RenderReport(StackPanel host, DoctorReport rep, Func<DoctorRow, Button?>? rowAction = null)
    {
        host.Children.Clear();
        host.Children.Add(new TextBlock
        {
            Text = Loc.I.Pick(rep.Summary.En, rep.Summary.Zh),
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });

        foreach (var row in rep.Rows)
        {
            var grid = new Grid { Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 2, 0, 0) };
            grid.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"];
            grid.CornerRadius = new CornerRadius(6);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new FontIcon { Glyph = string.IsNullOrEmpty(row.Glyph) ? ((char)0xE73E).ToString() : row.Glyph, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            var texts = new StackPanel();
            texts.Children.Add(new TextBlock { Text = row.Primary, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            if (!string.IsNullOrWhiteSpace(row.Secondary))
                texts.Children.Add(new TextBlock { Text = row.Secondary, FontSize = 12, Foreground = Sub, TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(texts, 1);
            grid.Children.Add(texts);

            var act = rowAction?.Invoke(row);
            if (act is not null)
            {
                Grid.SetColumn(act, 2);
                act.VerticalAlignment = VerticalAlignment.Center;
                grid.Children.Add(act);
            }
            host.Children.Add(grid);
        }

        if (rep.Rows.Count == 0 && !string.IsNullOrWhiteSpace(rep.RawOutput) && rep.RawOutput!.Trim().Length > 0
            && !rep.RawOutput.Trim().StartsWith("[") && rep.RawOutput.Trim() != "{}")
        {
            RenderOutputPane(host, rep.RawOutput!.Trim());
        }
    }

    private static StackPanel ResultHost()
        => new() { Spacing = 6 };

    // ===================== Windows 11 & maintenance roadmap workflows =====================

    private FrameworkElement LabeledControl(string en, string zh, string descriptionEn, string descriptionZh,
        FrameworkElement control)
    {
        var host = new StackPanel { Spacing = 4 };
        host.Children.Add(new TextBlock
        {
            Text = P(en, zh),
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        host.Children.Add(new TextBlock
        {
            Text = P(descriptionEn, descriptionZh),
            FontSize = 12,
            Foreground = Sub,
            TextWrapping = TextWrapping.Wrap,
        });
        control.HorizontalAlignment = HorizontalAlignment.Left;
        host.Children.Add(control);
        AutomationProperties.SetName(control, $"{en} · {zh}");
        return host;
    }

    private static ComboBox Choice(IEnumerable<(string en, string zh, int value)> options, int selected)
    {
        var combo = new ComboBox { MinWidth = 230 };
        foreach (var option in options)
        {
            var item = new ComboBoxItem { Content = $"{option.en} · {option.zh}", Tag = option.value };
            combo.Items.Add(item);
            if (option.value == selected) combo.SelectedItem = item;
        }
        if (combo.SelectedIndex < 0 && combo.Items.Count > 0) combo.SelectedIndex = 0;
        return combo;
    }

    private static int ChoiceValue(ComboBox combo)
        => combo.SelectedItem is ComboBoxItem { Tag: int value }
            ? value
            : throw new InvalidOperationException("Choose a supported value first.");

    private static NumberBox TimingBox(uint value)
    {
        var box = new NumberBox
        {
            Minimum = 0,
            Maximum = 20_000,
            SmallChange = 50,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            Width = 230,
        };
        box.Value = value;
        return box;
    }

    private static uint TimingValue(NumberBox box)
    {
        if (double.IsNaN(box.Value) || box.Value < 0 || box.Value > 20_000)
            throw new InvalidOperationException("Enter a timing between 0 and 20,000 milliseconds.");
        return checked((uint)Math.Round(box.Value));
    }

    private async Task<bool> ConfirmAsync(string titleEn, string titleZh, string bodyEn, string bodyZh,
        string actionEn, string actionZh)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P(titleEn, titleZh),
            Content = new TextBlock { Text = P(bodyEn, bodyZh), TextWrapping = TextWrapping.Wrap, MaxWidth = 520 },
            PrimaryButtonText = P(actionEn, actionZh),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void BuildStorageSenseDoctor()
    {
        var (_, body) = NewCard(((char)0xE74E).ToString(), "Storage Sense policy", "儲存空間感知政策",
            "Set cadence plus Recycle Bin and Downloads retention together.",
            "一次過設定清理週期、回收筒同下載資料夾保留期。");
        StorageSenseSettings current = SystemMaintenanceService.ReadStorageSense();
        var enabled = new ToggleSwitch { OnContent = P("On", "開"), OffContent = P("Off", "關") };
        enabled.IsOn = current.Enabled;
        var cadence = Choice(new[]
        {
            ("When space is low", "空間不足時", 0), ("Daily", "每日", 1),
            ("Weekly", "每週", 7), ("Monthly", "每月", 30),
        }, current.CadenceDays);
        var recycle = Choice(new[]
        {
            ("Never", "永不", 0), ("1 day", "1 日", 1), ("14 days", "14 日", 14),
            ("30 days", "30 日", 30), ("60 days", "60 日", 60),
        }, current.RecycleBinDays);
        var downloads = Choice(new[]
        {
            ("Never", "永不", 0), ("1 day", "1 日", 1), ("14 days", "14 日", 14),
            ("30 days", "30 日", 30), ("60 days", "60 日", 60),
        }, current.DownloadsDays);

        body.Children.Add(LabeledControl("Automatic cleanup", "自動清理",
            "Controls StoragePolicy value 01.", "控制 StoragePolicy 值 01。", enabled));
        body.Children.Add(LabeledControl("Run cadence", "執行週期",
            "Low-space, daily, weekly, or monthly (value 2048).", "空間不足、每日、每週或者每月（值 2048）。", cadence));
        body.Children.Add(LabeledControl("Recycle Bin retention", "回收筒保留期",
            "Delete older Recycle Bin items (value 256).", "刪除較舊回收筒項目（值 256）。", recycle));
        body.Children.Add(LabeledControl("Downloads retention", "下載資料夾保留期",
            "Delete untouched Downloads files only after the selected age (value 512).", "只會喺所選日數後刪除冇用過嘅下載檔案（值 512）。", downloads));
        var output = ResultHost();
        body.Children.Add(MakeButton("Save Storage Sense policy", "儲存感知政策", ((char)0xE74E).ToString(), () =>
        {
            var result = SystemMaintenanceService.ApplyStorageSense(new StorageSenseSettings(
                enabled.IsOn, ChoiceValue(cadence), ChoiceValue(recycle), ChoiceValue(downloads)));
            ShowTweakResult(result, P("Storage Sense", "儲存空間感知"), output);
            return Task.CompletedTask;
        }));
        body.Children.Add(output);
    }

    private void BuildFilterKeysDoctor()
    {
        var (_, body) = NewCard(((char)0xE776).ToString(), "Filter Keys & Slow Keys", "篩選鍵同慢速鍵",
            "Configure all timing values and apply them live through SPI_SETFILTERKEYS.",
            "設定所有時間值，再用 SPI_SETFILTERKEYS 即時套用。");
        FilterKeysSettings current = SystemMaintenanceService.ReadFilterKeys();
        var enabled = new ToggleSwitch { OnContent = P("On", "開"), OffContent = P("Off", "關") };
        enabled.IsOn = current.Enabled;
        var accept = TimingBox(current.DelayBeforeAcceptanceMs);
        var repeatDelay = TimingBox(current.AutoRepeatDelayMs);
        var repeatRate = TimingBox(current.AutoRepeatRateMs);
        var bounce = TimingBox(current.BounceTimeMs);
        body.Children.Add(LabeledControl("Filter Keys", "篩選鍵",
            "Enable the accessibility filter without leaving WinForge.", "唔使離開 WinForge 都可以啟用呢個協助工具。", enabled));
        body.Children.Add(LabeledControl("Delay before acceptance (ms)", "接受前延遲（毫秒）",
            "How long a key must be held before Windows accepts it.", "按鍵要撳住幾耐 Windows 先接受。", accept));
        body.Children.Add(LabeledControl("Auto-repeat delay (ms)", "自動重複延遲（毫秒）",
            "Wait before a held key starts repeating.", "撳住按鍵後等幾耐先開始重複。", repeatDelay));
        body.Children.Add(LabeledControl("Auto-repeat rate (ms)", "自動重複間距（毫秒）",
            "Delay between repeated keystrokes.", "重複按鍵之間嘅時間。", repeatRate));
        body.Children.Add(LabeledControl("Bounce time (ms)", "彈跳忽略時間（毫秒）",
            "Ignore repeated presses inside this window; 0 disables bounce filtering.", "呢段時間內忽略重複按鍵；0 會停用彈跳過濾。", bounce));
        var output = ResultHost();
        body.Children.Add(MakeButton("Apply Filter Keys", "套用篩選鍵", ((char)0xE73E).ToString(), () =>
        {
            var result = SystemMaintenanceService.ApplyFilterKeys(new FilterKeysSettings(
                enabled.IsOn, TimingValue(accept), TimingValue(repeatDelay), TimingValue(repeatRate), TimingValue(bounce)));
            ShowTweakResult(result, P("Filter Keys", "篩選鍵"), output);
            return Task.CompletedTask;
        }));
        body.Children.Add(output);
    }

    private void BuildDefaultAssociationsDoctor()
    {
        var (_, body) = NewCard(((char)0xE8E5).ToString(), "Default app associations", "預設程式關聯",
            "Export or import the machine-wide DISM XML template for new users.",
            "用 DISM 匯出／匯入畀新使用者嘅全機預設關聯 XML 範本。");
        body.Children.Add(new TextBlock
        {
            Text = P("Import changes the machine template for new profiles; it does not bypass the protected per-user UserChoice hash.",
                "匯入只會改新使用者設定檔嘅全機範本；唔會繞過受保護嘅每使用者 UserChoice hash。"),
            Foreground = Sub,
            TextWrapping = TextWrapping.Wrap,
        });
        var output = ResultHost();
        var buttons = Buttons();
        buttons.Children.Add(MakeButton("Export XML…", "匯出 XML…", ((char)0xE74E).ToString(), async () =>
        {
            string? path = await FileDialogs.SaveFileAsync("DefaultAppAssociations.xml",
                new[] { new FileDialogs.Filter("XML files", "*.xml") }, "xml",
                P("Export default app associations", "匯出預設程式關聯"));
            if (path is null) return;
            ShowTweakResult(await SystemMaintenanceService.ExportDefaultAssociations(path), P("Export associations", "匯出關聯"), output);
        }));
        buttons.Children.Add(MakeButton("Import XML…", "匯入 XML…", ((char)0xE8B5).ToString(), async () =>
        {
            string? path = await FileDialogs.OpenFileAsync(
                new[] { new FileDialogs.Filter("XML files", "*.xml") },
                P("Import default app associations", "匯入預設程式關聯"));
            if (path is null) return;
            if (!await ConfirmAsync("Import machine association template?", "匯入全機關聯範本？",
                    "DISM will replace the default-association template used by new user profiles. Existing per-user choices remain protected.",
                    "DISM 會取代新使用者設定檔嘅預設關聯範本；現有每使用者選擇仍然受保護。",
                    "Import", "匯入")) return;
            ShowTweakResult(await SystemMaintenanceService.ImportDefaultAssociations(path), P("Import associations", "匯入關聯"), output);
        }, destructive: true));
        body.Children.Add(buttons);
        body.Children.Add(output);
    }

    private void BuildWindowsUpdateDoctor()
    {
        var (_, body) = NewCard(((char)0xE895).ToString(), "Windows Update pause", "Windows Update 暫停",
            "Pause quality and feature updates for a bounded period, or remove every pause value.",
            "有期限咁暫停品質同功能更新，或者移除全部暫停值。");
        DateTimeOffset? expiry = SystemMaintenanceService.ReadWindowsUpdatePauseExpiry();
        var status = new TextBlock { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        void RefreshStatus()
        {
            expiry = SystemMaintenanceService.ReadWindowsUpdatePauseExpiry();
            status.Text = expiry is { } until && until > DateTimeOffset.Now
                ? P($"Paused until {until.LocalDateTime:g}", $"已暫停到 {until.LocalDateTime:g}")
                : P("Not paused by WinForge", "WinForge 冇設定暫停");
        }
        RefreshStatus();
        body.Children.Add(status);
        var duration = Choice(new[]
        {
            ("7 days", "7 日", 7), ("14 days", "14 日", 14), ("21 days", "21 日", 21),
            ("28 days", "28 日", 28), ("35 days (maximum)", "35 日（上限）", 35),
        }, 7);
        body.Children.Add(LabeledControl("Pause duration", "暫停日數",
            "Windows supports a maximum 35-day bounded pause.", "Windows 最多只支援 35 日有限暫停。", duration));
        var output = ResultHost();
        var buttons = Buttons();
        buttons.Children.Add(MakeButton("Pause updates", "暫停更新", ((char)0xE769).ToString(), () =>
        {
            TweakResult result = SystemMaintenanceService.PauseWindowsUpdate(ChoiceValue(duration));
            ShowTweakResult(result, P("Pause updates", "暫停更新"), output);
            RefreshStatus();
            return Task.CompletedTask;
        }));
        buttons.Children.Add(MakeButton("Resume updates", "恢復更新", ((char)0xE768).ToString(), () =>
        {
            TweakResult result = SystemMaintenanceService.ResumeWindowsUpdate();
            ShowTweakResult(result, P("Resume updates", "恢復更新"), output);
            RefreshStatus();
            return Task.CompletedTask;
        }));
        body.Children.Add(buttons);
        body.Children.Add(output);
    }

    private void BuildDriverRollbackDoctor()
    {
        var (_, body) = NewCard(((char)0xE777).ToString(), "Driver package backup & rollback", "驅動套件備份同回復",
            "Export a selected OEM package before rollback, restore exports, or back up every third-party driver.",
            "回復前先匯出所選 OEM 套件，亦可以還原備份或者備份全部第三方驅動。");
        var packages = new ComboBox
        {
            MinWidth = 230,
            PlaceholderText = P("Choose oem*.inf…", "揀 oem*.inf…"),
            ItemsSource = SystemMaintenanceService.ListPublishedDriverPackages(),
        };
        AutomationProperties.SetName(packages, "Published driver package · 已發佈驅動套件");
        body.Children.Add(LabeledControl("Published driver package", "已發佈驅動套件",
            "Select the package identity Windows assigned (for example oem42.inf).",
            "揀 Windows 指派嘅套件身份（例如 oem42.inf）。", packages));
        var folderText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(_driverBackupFolder)
                ? P("No backup folder selected.", "未揀備份資料夾。")
                : _driverBackupFolder,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            Foreground = Sub,
        };
        body.Children.Add(folderText);
        var output = ResultHost();
        Button rollback = null!;
        var choose = MakeButton("Choose backup folder…", "揀備份資料夾…", ((char)0xE8B7).ToString(), async () =>
        {
            string? folder = await FileDialogs.OpenFolderAsync(P("Driver backup folder", "驅動備份資料夾"));
            if (folder is null) return;
            _driverBackupFolder = folder;
            _exportedDriverPackage = string.Empty;
            folderText.Text = folder;
            rollback.IsEnabled = false;
        });
        body.Children.Add(choose);

        var exports = Buttons();
        exports.Children.Add(MakeButton("Export selected", "匯出所選", ((char)0xE74E).ToString(), async () =>
        {
            if (packages.SelectedItem is not string package || string.IsNullOrWhiteSpace(_driverBackupFolder))
            {
                ShowResult(false, P("Choose package and folder", "請揀套件同資料夾"),
                    P("Select an OEM INF and a backup folder first.", "請先揀 OEM INF 同備份資料夾。"));
                return;
            }
            TweakResult result = await SystemMaintenanceService.ExportDriver(package, _driverBackupFolder);
            if (result.Success) _exportedDriverPackage = package;
            rollback.IsEnabled = result.Success;
            ShowTweakResult(result, P("Export driver", "匯出驅動"), output);
        }));
        exports.Children.Add(MakeButton("Export all", "匯出全部", ((char)0xE8B5).ToString(), async () =>
        {
            if (string.IsNullOrWhiteSpace(_driverBackupFolder))
            {
                ShowResult(false, P("Choose a backup folder", "請揀備份資料夾"), P("Pick a destination first.", "請先揀目的地。"));
                return;
            }
            ShowTweakResult(await SystemMaintenanceService.ExportAllDrivers(_driverBackupFolder), P("Export all drivers", "匯出全部驅動"), output);
        }));
        body.Children.Add(exports);

        var restoreRow = Buttons();
        rollback = MakeButton("Rollback exported package", "回復已備份套件", ((char)0xE7A7).ToString(), async () =>
        {
            if (packages.SelectedItem is not string package ||
                !string.Equals(package, _exportedDriverPackage, StringComparison.OrdinalIgnoreCase))
            {
                ShowResult(false, P("Backup required", "需要先備份"),
                    P("Export this exact package during the current session before rollback.", "回復前要喺今次工作階段先匯出同一個套件。"));
                return;
            }
            if (!await ConfirmAsync("Roll back this driver package?", "回復呢個驅動套件？",
                    $"Windows will uninstall {package} without /force. A compatible staged driver may take over. The exported copy remains in {_driverBackupFolder}.",
                    $"Windows 會喺唔用 /force 嘅情況下解除安裝 {package}；另一個相容已暫存驅動可能會接手。備份仍然留喺 {_driverBackupFolder}。",
                    "Roll back", "回復")) return;
            ShowTweakResult(await SystemMaintenanceService.RollBackDriver(package), P("Driver rollback", "驅動回復"), output);
        }, destructive: true);
        rollback.IsEnabled = packages.SelectedItem is string packageName &&
            string.Equals(packageName, _exportedDriverPackage, StringComparison.OrdinalIgnoreCase);
        packages.SelectionChanged += (_, _) => rollback.IsEnabled = packages.SelectedItem is string selected &&
            string.Equals(selected, _exportedDriverPackage, StringComparison.OrdinalIgnoreCase);
        restoreRow.Children.Add(rollback);
        restoreRow.Children.Add(MakeButton("Restore exported INFs", "還原已匯出 INF", ((char)0xE8B5).ToString(), async () =>
        {
            if (string.IsNullOrWhiteSpace(_driverBackupFolder))
            {
                ShowResult(false, P("Choose a backup folder", "請揀備份資料夾"), P("Pick the export folder first.", "請先揀匯出資料夾。"));
                return;
            }
            ShowTweakResult(await SystemMaintenanceService.RestoreExportedDrivers(_driverBackupFolder), P("Restore drivers", "還原驅動"), output);
        }));
        body.Children.Add(restoreRow);
        body.Children.Add(new TextBlock
        {
            Text = P("Safety gate: rollback stays disabled until the exact selected package has exported successfully in this session. WinForge never uses pnputil /force here.",
                "安全閘：今次工作階段未成功匯出同一套件之前，回復掣會保持停用。呢度永遠唔會用 pnputil /force。"),
            FontSize = 12,
            Foreground = Sub,
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(output);
    }

    private void BuildStartupAuditDoctor()
    {
        var (_, body) = NewCard(((char)0xE7B5).ToString(), "Startup impact & Autoruns audit", "開機影響同 Autoruns 審核",
            "Audit Run/RunOnce, Startup folders, Winlogon, AppInit, automatic services, and boot/logon tasks.",
            "審核 Run／RunOnce、開機資料夾、Winlogon、AppInit、自動服務同開機／登入工作。");
        body.Children.Add(new TextBlock
        {
            Text = P("Impact is a transparent source-risk estimate, not invented boot-time telemetry. Critical/high entries start earliest or inject into shared processes.",
                "影響等級係透明嘅來源風險估算，唔係虛構開機時間數據；關鍵／高風險項目會最早啟動或者注入共用程序。"),
            Foreground = Sub,
            TextWrapping = TextWrapping.Wrap,
        });
        var report = ResultHost();
        body.Children.Add(MakeButton("Run full autoruns audit", "執行完整 Autoruns 審核", ((char)0xE721).ToString(), async () =>
        {
            IReadOnlyList<StartupAuditEntry> rows = await SystemMaintenanceService.AuditStartupAsync(CancellationToken.None);
            RenderStartupAudit(report, rows);
            ShowResult(true, P("Audit complete", "審核完成"),
                P($"Found {rows.Count} startup entries across the inspected surfaces.", $"喺已檢查介面搵到 {rows.Count} 個開機項目。"));
        }));
        body.Children.Add(report);
    }

    private void RenderStartupAudit(StackPanel host, IReadOnlyList<StartupAuditEntry> rows)
    {
        host.Children.Clear();
        int critical = rows.Count(row => row.Impact == StartupImpact.Critical);
        int high = rows.Count(row => row.Impact == StartupImpact.High);
        host.Children.Add(new TextBlock
        {
            Text = P($"{rows.Count} entries · {critical} critical · {high} high",
                $"{rows.Count} 個項目 · {critical} 個關鍵 · {high} 個高風險"),
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        var list = new StackPanel { Spacing = 4 };
        foreach (StartupAuditEntry row in rows.Take(300))
        {
            var text = new StackPanel { Spacing = 2 };
            text.Children.Add(new TextBlock { Text = $"{row.ImpactText} · {row.Name}", FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            text.Children.Add(new TextBlock { Text = row.Location + " · " + row.ImpactReason, FontSize = 12, Foreground = Sub, TextWrapping = TextWrapping.Wrap });
            text.Children.Add(new TextBlock { Text = row.Command, FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = Tert, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            list.Children.Add(new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8),
                Child = text,
            });
        }
        var scroll = new ScrollViewer { MaxHeight = 360, Content = list };
        ScrollViewer.SetVerticalScrollBarVisibility(scroll, ScrollBarVisibility.Auto);
        host.Children.Add(scroll);
        if (rows.Count > 300)
            host.Children.Add(new TextBlock { Text = P("Showing the first 300 entries.", "顯示頭 300 個項目。"), FontSize = 12, Foreground = Sub });
    }

    private void BuildComponentStoreDoctor()
    {
        var (_, body) = NewCard(((char)0xE74D).ToString(), "Component store ResetBase", "元件存放區 ResetBase",
            "Run DISM StartComponentCleanup /ResetBase with an explicit irreversible-state gate.",
            "經清楚不可逆安全閘執行 DISM StartComponentCleanup /ResetBase。");
        var warning = new InfoBar
        {
            IsOpen = true,
            IsClosable = false,
            Severity = InfoBarSeverity.Warning,
            Title = P("Irreversible", "不可逆"),
            Message = P("ResetBase removes superseded component versions. Installed Windows updates cannot be uninstalled afterward.",
                "ResetBase 會移除已取代嘅元件版本；完成後無法解除安裝現有 Windows 更新。"),
        };
        body.Children.Add(warning);
        var acknowledge = new CheckBox
        {
            Content = P("I understand that installed updates can no longer be uninstalled.", "我明白完成後唔可以再解除安裝已裝更新。"),
        };
        AutomationProperties.SetName(acknowledge, "Acknowledge irreversible ResetBase effect · 確認 ResetBase 不可逆影響");
        body.Children.Add(acknowledge);
        var output = ResultHost();
        var reset = MakeButton("Run ResetBase", "執行 ResetBase", ((char)0xE74D).ToString(), async () =>
        {
            if (!await ConfirmAsync("Permanently reset the component base?", "永久重設元件基礎？",
                    "This removes superseded WinSxS component versions and permanently removes the option to uninstall installed updates. Keep the PC powered on until DISM finishes.",
                    "呢個操作會移除已取代嘅 WinSxS 元件版本，永久取消解除安裝現有更新嘅選項。DISM 完成之前請保持電腦供電。",
                    "Run ResetBase", "執行 ResetBase")) return;
            ShowTweakResult(await SystemMaintenanceService.ResetComponentBase(), P("ResetBase", "ResetBase"), output);
        }, destructive: true);
        reset.IsEnabled = false;
        acknowledge.Checked += (_, _) => reset.IsEnabled = true;
        acknowledge.Unchecked += (_, _) => reset.IsEnabled = false;
        body.Children.Add(reset);
        body.Children.Add(output);
    }

    private void BuildStoreAppDoctor()
    {
        var (_, body) = NewCard(((char)0xE71D).ToString(), "Store app reset & re-register", "商店 app 重設同重新註冊",
            "Select an installed Store app, reset its data, or re-register its own manifest.",
            "揀已安裝商店 app，重設佢嘅資料，或者重新註冊佢自己嘅 manifest。");
        var apps = new ComboBox { MinWidth = 300, PlaceholderText = P("Load and choose an app…", "載入再揀一個 app…") };
        AutomationProperties.SetName(apps, "Installed Store app · 已安裝商店 app");
        body.Children.Add(apps);
        var output = ResultHost();
        body.Children.Add(MakeButton("Load installed apps", "載入已安裝 app", ((char)0xE72C).ToString(), async () =>
        {
            apps.Items.Clear();
            foreach (AppInfo app in await UninstallManager.ListAsync())
                apps.Items.Add(new ComboBoxItem { Content = $"{app.DisplayName} · {app.Name}", Tag = app });
            if (apps.Items.Count > 0) apps.SelectedIndex = 0;
            ShowResult(true, P("Apps loaded", "app 已載入"), P($"Loaded {apps.Items.Count} resettable apps.", $"載入咗 {apps.Items.Count} 個可重設 app。"));
        }));
        var buttons = Buttons();
        buttons.Children.Add(MakeButton("Reset app data", "重設 app 資料", ((char)0xE74D).ToString(), async () =>
        {
            if (apps.SelectedItem is not ComboBoxItem { Tag: AppInfo app })
            {
                ShowResult(false, P("Choose an app", "請揀 app"), P("Load and select an app first.", "請先載入再揀 app。"));
                return;
            }
            if (!await ConfirmAsync("Reset this app's data?", "重設呢個 app 嘅資料？",
                    $"{app.DisplayName} will return to its first-run state. Local settings, sessions, and unsynced data may be removed.",
                    $"{app.DisplayName} 會回復首次啟動狀態；本機設定、工作階段同未同步資料可能會刪除。",
                    "Reset data", "重設資料")) return;
            ShowTweakResult(await SystemMaintenanceService.ResetStoreApp(app.Name), P("Reset app", "重設 app"), output);
        }, destructive: true));
        buttons.Children.Add(MakeButton("Re-register manifest", "重新註冊 manifest", ((char)0xE8B5).ToString(), async () =>
        {
            if (apps.SelectedItem is not ComboBoxItem { Tag: AppInfo app })
            {
                ShowResult(false, P("Choose an app", "請揀 app"), P("Load and select an app first.", "請先載入再揀 app。"));
                return;
            }
            ShowTweakResult(await SystemMaintenanceService.ReregisterStoreApp(app.Name), P("Re-register app", "重新註冊 app"), output);
        }));
        body.Children.Add(buttons);
        body.Children.Add(output);
    }

    // ===================== 1) Print Spooler & queue rescue =====================

    private void BuildPrintDoctor()
    {
        var (_, body) = NewCard(((char)0xE749).ToString(), "Print Spooler & queue rescue", "列印多工與佇列救援",
            "Clear stuck print jobs and revive the spooler.", "清走卡住嘅列印工作、救返個多工緩衝處理器。");

        var report = ResultHost();
        var output = ResultHost();

        var btns = Buttons();
        btns.Children.Add(MakeButton("Diagnose queue", "診斷佇列", ((char)0xE721).ToString(), async () =>
        {
            var rep = await SystemDoctors.ListPrintJobsAsync();
            RenderReport(report, rep, row =>
            {
                if (row.Tag is null) return null;
                var b = new Button { Content = P("Cancel", "取消"), Padding = new Thickness(10, 3, 10, 3) };
                b.Click += async (_, _) => await Guard(async () =>
                {
                    var r = await SystemDoctors.CancelPrintJobAsync(row.Tag!);
                    ShowTweakResult(r, P("Cancel job", "取消工作"), output);
                    RenderReport(report, await SystemDoctors.ListPrintJobsAsync());
                });
                return b;
            });
        }));
        btns.Children.Add(MakeButton("Rescue spooler (purge queue)", "救援（清佇列）", ((char)0xE777).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.RescueSpoolerAsync(), P("Rescue spooler", "救援多工緩衝"), output), destructive: true));
        btns.Children.Add(MakeButton("Restart spooler only", "只重啟", ((char)0xE72C).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.RestartSpoolerAsync(), P("Restart spooler", "重啟多工緩衝"), output)));

        body.Children.Add(btns);
        body.Children.Add(report);
        body.Children.Add(output);
    }

    // ===================== 2) Network / DNS doctor =====================

    private void BuildNetworkDoctor()
    {
        var (_, body) = NewCard(((char)0xE968).ToString(), "Network / DNS doctor", "網絡 / DNS 醫生",
            "Reset Winsock/TCP-IP, flush DNS, renew lease, bounce adapters, one-click repair.",
            "重設 Winsock／TCP-IP、清 DNS、重續租約、重啟介面卡、一鍵修復。");

        var report = ResultHost();
        var output = ResultHost();

        var diag = Buttons();
        diag.Children.Add(MakeButton("List adapters", "列出介面卡", ((char)0xE721).ToString(), async () =>
        {
            var rep = await SystemDoctors.ListAdaptersAsync();
            RenderReport(report, rep, row =>
            {
                if (row.Tag is null) return null;
                var b = new Button { Content = P("Bounce", "重啟"), Padding = new Thickness(10, 3, 10, 3) };
                b.Click += async (_, _) => await Guard(async () =>
                {
                    var r = await SystemDoctors.BounceAdapterAsync(row.Tag!);
                    ShowTweakResult(r, P("Bounce adapter", "重啟介面卡"), output);
                    RenderReport(report, await SystemDoctors.ListAdaptersAsync());
                });
                return b;
            });
        }));
        body.Children.Add(diag);

        var ops = Buttons();
        ops.Children.Add(MakeButton("Flush DNS", "清 DNS", ((char)0xE74D).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.FlushDnsAsync(), P("Flush DNS", "清 DNS"), output)));
        ops.Children.Add(MakeButton("Reset Winsock", "重設 Winsock", ((char)0xE72C).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.ResetWinsockAsync(), P("Reset Winsock", "重設 Winsock"), output)));
        ops.Children.Add(MakeButton("Reset TCP/IP", "重設 TCP/IP", ((char)0xE72C).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.ResetTcpIpAsync(), P("Reset TCP/IP", "重設 TCP/IP"), output)));
        ops.Children.Add(MakeButton("Release + renew", "釋放＋重續", ((char)0xE895).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.ReleaseRenewAsync(), P("Release/renew", "釋放／重續"), output)));
        body.Children.Add(ops);

        var repair = Buttons();
        repair.Children.Add(MakeButton("Repair connection (all of the above)", "修復連線（全部）", ((char)0xE90F).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.RepairConnectionAsync(), P("Repair connection", "修復連線"), output), destructive: true));
        body.Children.Add(repair);

        body.Children.Add(report);
        body.Children.Add(output);
    }

    // ===================== 3) Sleep / Wake doctor =====================

    private void BuildSleepWakeDoctor()
    {
        var (_, body) = NewCard(((char)0xE708).ToString(), "Sleep / Wake doctor", "睡眠 / 喚醒醫生",
            "Find what blocks sleep or wakes the PC, disarm wake sources, tune fast startup & power scheme.",
            "搵出乜嘢阻住睡眠或整醒部機、解除喚醒來源、調整快速啟動同電源計劃。");

        var report = ResultHost();
        var output = ResultHost();

        var diag = Buttons();
        diag.Children.Add(MakeButton("What blocks sleep", "乜阻住睡眠", ((char)0xE721).ToString(), async () =>
            RenderReport(report, await SystemDoctors.SleepBlockersAsync())));
        diag.Children.Add(MakeButton("Last wake source", "最近喚醒", ((char)0xE7C1).ToString(), async () =>
            RenderReport(report, await SystemDoctors.LastWakeAsync())));
        diag.Children.Add(MakeButton("Wake timers", "喚醒計時器", ((char)0xE823).ToString(), async () =>
            RenderReport(report, await SystemDoctors.WakeTimersAsync())));
        diag.Children.Add(MakeButton("Wake-armed devices", "可喚醒裝置", ((char)0xE975).ToString(), async () =>
        {
            var rep = await SystemDoctors.WakeArmedDevicesAsync();
            RenderReport(report, rep, row =>
            {
                if (row.Tag is null) return null;
                var b = new Button { Content = P("Disarm", "解除"), Padding = new Thickness(10, 3, 10, 3) };
                b.Click += async (_, _) => await Guard(async () =>
                {
                    var r = await SystemDoctors.DisarmWakeDeviceAsync(row.Tag!);
                    ShowTweakResult(r, P("Disarm wake", "解除喚醒"), output);
                    RenderReport(report, await SystemDoctors.WakeArmedDevicesAsync(), null);
                });
                return b;
            });
        }));
        diag.Children.Add(MakeButton("Fast startup state", "快速啟動狀態", ((char)0xE945).ToString(), async () =>
            RenderReport(report, await SystemDoctors.FastStartupStateAsync())));
        body.Children.Add(diag);

        var ops = Buttons();
        ops.Children.Add(MakeButton("Disable all wake timers", "停用全部喚醒計時器", ((char)0xE711).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.DisableWakeTimersAsync(), P("Disable wake timers", "停用喚醒計時器"), output)));
        ops.Children.Add(MakeButton("Turn off fast startup", "關閉快速啟動", ((char)0xE711).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.SetFastStartupAsync(false), P("Disable fast startup", "關閉快速啟動"), output)));
        ops.Children.Add(MakeButton("Turn on fast startup", "開啟快速啟動", ((char)0xE73E).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.SetFastStartupAsync(true), P("Enable fast startup", "開啟快速啟動"), output)));
        ops.Children.Add(MakeButton("Unlock Ultimate Performance", "解鎖終極效能", ((char)0xE945).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.UnlockUltimatePerformanceAsync(), P("Unlock Ultimate Performance", "解鎖終極效能"), output)));
        body.Children.Add(ops);

        body.Children.Add(report);
        body.Children.Add(output);
    }

    // ===================== 4) Shell recovery — Fix taskbar & Start =====================

    private void BuildShellDoctor()
    {
        var (_, body) = NewCard(((char)0xE71D).ToString(), "Fix taskbar & Start", "修復工作列與開始功能表",
            "Clear the Start/IrisService cache, re-register shell packages, restart Explorer.",
            "清開始功能表／IrisService 快取、重新註冊外殼套件、重啟檔案總管。");

        var output = ResultHost();
        var btns = Buttons();
        btns.Children.Add(MakeButton("Repair taskbar & Start", "修復工作列與開始", ((char)0xE90F).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.FixTaskbarAndStartAsync(), P("Fix taskbar & Start", "修復工作列與開始"), output), destructive: true));
        body.Children.Add(new TextBlock
        {
            Text = P("Your screen will flash as Explorer restarts. Open apps stay running.",
                "重啟檔案總管時畫面會閃一閃，已開嘅程式照樣運行。"),
            FontSize = 12, Foreground = Sub, TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(btns);
        body.Children.Add(output);
    }

    // ===================== 5) Search index governor =====================

    private void BuildSearchDoctor()
    {
        var (_, body) = NewCard(((char)0xE721).ToString(), "Search index governor", "搜尋索引管理",
            "Pause/resume or rebuild the search index, and kill web (Bing) results in Start search.",
            "暫停／繼續或重建搜尋索引，並關閉開始功能表嘅網頁（Bing）結果。");

        var report = ResultHost();
        var output = ResultHost();

        var diag = Buttons();
        diag.Children.Add(MakeButton("Check search state", "檢查搜尋狀態", ((char)0xE721).ToString(), async () =>
            RenderReport(report, await SystemDoctors.SearchStateAsync())));
        body.Children.Add(diag);

        var ops = Buttons();
        ops.Children.Add(MakeButton("Pause search", "暫停搜尋", ((char)0xE769).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.PauseSearchAsync(), P("Pause search", "暫停搜尋"), output)));
        ops.Children.Add(MakeButton("Resume search", "繼續搜尋", ((char)0xE768).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.ResumeSearchAsync(), P("Resume search", "繼續搜尋"), output)));
        ops.Children.Add(MakeButton("Rebuild index", "重建索引", ((char)0xE72C).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.RebuildSearchIndexAsync(), P("Rebuild index", "重建索引"), output), destructive: true));
        body.Children.Add(ops);

        var web = Buttons();
        web.Children.Add(MakeButton("Disable web results", "關閉網頁結果", ((char)0xE711).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.DisableWebResultsAsync(), P("Disable web results", "關閉網頁結果"), output)));
        web.Children.Add(MakeButton("Enable web results", "開啟網頁結果", ((char)0xE73E).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.EnableWebResultsAsync(), P("Enable web results", "開啟網頁結果"), output)));
        body.Children.Add(web);

        body.Children.Add(report);
        body.Children.Add(output);
    }

    // ===================== 6) Explorer perf tuner =====================

    private void BuildExplorerDoctor()
    {
        var (_, body) = NewCard(((char)0xE8B7).ToString(), "Explorer perf tuner", "檔案總管效能調校",
            "Run folder windows in a separate process and clear ghost Explorer instances.",
            "用獨立程序開啟資料夾視窗、清走鬼影 Explorer 程序。");

        var report = ResultHost();
        var output = ResultHost();

        var diag = Buttons();
        diag.Children.Add(MakeButton("Check Explorer state", "檢查狀態", ((char)0xE721).ToString(), async () =>
            RenderReport(report, await SystemDoctors.ExplorerStateAsync())));
        body.Children.Add(diag);

        var ops = Buttons();
        ops.Children.Add(MakeButton("Separate process: ON", "獨立程序：開", ((char)0xE73E).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.SetSeparateProcessAsync(true), P("Separate process ON", "獨立程序開"), output)));
        ops.Children.Add(MakeButton("Separate process: OFF", "獨立程序：關", ((char)0xE711).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.SetSeparateProcessAsync(false), P("Separate process OFF", "獨立程序關"), output)));
        ops.Children.Add(MakeButton("Kill ghost Explorers", "清鬼影程序", ((char)0xE74D).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.KillGhostExplorersAsync(), P("Kill ghost Explorers", "清鬼影程序"), output)));
        body.Children.Add(ops);

        body.Children.Add(report);
        body.Children.Add(output);
    }

    // ===================== 7) Icon / thumbnail cache rebuilder =====================

    private void BuildCacheDoctor()
    {
        var (_, body) = NewCard(((char)0xE8B9).ToString(), "Icon & thumbnail cache rebuilder", "圖示與縮圖快取重建",
            "Fix blank, wrong or corrupt icons and thumbnails.", "修復空白、錯誤或損壞嘅圖示同縮圖。");

        var output = ResultHost();
        var btns = Buttons();
        btns.Children.Add(MakeButton("Rebuild icon cache", "重建圖示快取", ((char)0xE72C).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.RebuildIconCacheAsync(), P("Rebuild icon cache", "重建圖示快取"), output)));
        btns.Children.Add(MakeButton("Rebuild thumbnail cache", "重建縮圖快取", ((char)0xE72C).ToString(), async () =>
            ShowTweakResult(await SystemDoctors.RebuildThumbnailCacheAsync(), P("Rebuild thumbnail cache", "重建縮圖快取"), output)));
        body.Children.Add(btns);
        body.Children.Add(output);
    }

    // ===================== 8) Take ownership / reset permissions =====================

    private void BuildOwnershipDoctor()
    {
        var (_, body) = NewCard(((char)0xE72E).ToString(), "Take ownership / reset permissions", "取得擁有權 / 重設權限",
            "Take ownership of a locked file/folder and grant yourself full control — with one-click undo.",
            "對鎖死嘅檔案／資料夾取得擁有權並賦予自己完整控制 — 一鍵還原。");

        var output = ResultHost();

        var pathBox = new TextBox { PlaceholderText = P("Path to a file or folder…", "檔案或資料夾路徑…"), Text = _ownPath };
        pathBox.TextChanged += (_, _) => _ownPath = pathBox.Text;

        var pickRow = new Grid { ColumnSpacing = 8 };
        pickRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pickRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pickRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(pathBox, 0);
        pickRow.Children.Add(pathBox);

        var pickFolder = new Button { Content = P("Browse folder…", "瀏覽資料夾…"), Padding = new Thickness(10, 6, 10, 6) };
        pickFolder.Click += async (_, _) =>
        {
            var f = await PickFolder();
            if (f is not null) { _ownPath = f; pathBox.Text = f; }
        };
        Grid.SetColumn(pickFolder, 1);
        pickRow.Children.Add(pickFolder);

        var pickFile = new Button { Content = P("Browse file…", "瀏覽檔案…"), Padding = new Thickness(10, 6, 10, 6) };
        pickFile.Click += async (_, _) =>
        {
            var f = await PickFile();
            if (f is not null) { _ownPath = f; pathBox.Text = f; }
        };
        Grid.SetColumn(pickFile, 2);
        pickRow.Children.Add(pickFile);
        body.Children.Add(pickRow);

        var recurse = new CheckBox { Content = P("Apply to all contents (recursive)", "套用到所有內容（遞迴）"), IsChecked = true };
        body.Children.Add(recurse);

        var btns = Buttons();
        btns.Children.Add(MakeButton("Take ownership + full control", "取得擁有權＋完整控制", ((char)0xE72E).ToString(), async () =>
        {
            if (string.IsNullOrWhiteSpace(_ownPath)) { ShowResult(false, P("No path", "未選路徑"), P("Pick a file or folder first.", "請先揀檔案或資料夾。")); return; }
            ShowTweakResult(await SystemDoctors.TakeOwnershipAsync(_ownPath, recurse.IsChecked == true), P("Take ownership", "取得擁有權"), output);
        }, destructive: true));
        btns.Children.Add(MakeButton("Undo — reset permissions", "還原 — 重設權限", ((char)0xE7A7).ToString(), async () =>
        {
            if (string.IsNullOrWhiteSpace(_ownPath)) { ShowResult(false, P("No path", "未選路徑"), P("Pick a file or folder first.", "請先揀檔案或資料夾。")); return; }
            ShowTweakResult(await SystemDoctors.ResetPermissionsAsync(_ownPath, recurse.IsChecked == true), P("Reset permissions", "重設權限"), output);
        }));
        body.Children.Add(btns);
        body.Children.Add(output);
    }

    private static async Task<string?> PickFolder()
        => await FileDialogs.OpenFolderAsync();

    private static async Task<string?> PickFile()
        => await FileDialogs.OpenFileAsync();
}
