using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內 Nmap 掃描器（包真實 nmap.exe）· In-app Nmap scanner wrapping nmap.exe — enter targets,
/// pick a scan profile, toggle common flags, see a read-only command preview, run with -oX - and parse
/// the XML into a hosts / ports / services grid, watch a live log, cancel a running scan, and save the
/// raw XML or a flattened CSV. Install via winget (Insecure.Nmap, bundles Npcap). No redirect. Bilingual.
/// </summary>
public sealed partial class NmapModule : Page
{
    private readonly DispatcherQueue _ui;
    private readonly ObservableCollection<NmapPort> _rows = new();
    private readonly List<CheckBox> _flagChecks = new();
    private CancellationTokenSource? _cts;
    private NmapScanResult? _last;
    private List<TweakDefinition>? _ops;
    private bool _building;
    private bool _rowBusy; // guard so only one control-row action runs at a time

    public NmapModule()
    {
        InitializeComponent();
        _ui = DispatcherQueue.GetForCurrentThread();
        List.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; try { _cts?.Cancel(); } catch { } };
        Loaded += async (_, _) => { Render(); PopulateOps(); UpdatePreview(); await CheckEngine(); };
    }

    private void OnLang(object? sender, EventArgs e) { Render(); PopulateOps(); UpdatePreview(); }
    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        _building = true;
        Header.Title = "Nmap Scanner · 網絡掃描";
        HeaderBlurb.Text = P(
            "Wrap the Nmap port scanner: enter a target (IP, hostname, CIDR like 192.168.1.0/24, or a range), pick a profile, toggle flags, run, and read the parsed hosts / ports / services. Only scan networks you own or are authorised to test.",
            "包裝 Nmap 連接埠掃描器：輸入目標（IP、主機名、CIDR 如 192.168.1.0/24，或範圍），揀設定檔、開關旗標、執行，然後睇解析好嘅主機／連接埠／服務。只可掃描你擁有或獲授權測試嘅網絡。");

        ScanTab.Header = P("Scan", "掃描");
        OpsTab.Header = P("Tools", "工具");
        LogTab.Header = P("Live log", "即時記錄");

        TargetLabel.Text = P("Target(s)", "目標");
        TargetBox.PlaceholderText = P("e.g. 192.168.1.0/24, scanme.nmap.org, 10.0.0.1-50",
            "例如 192.168.1.0/24、scanme.nmap.org、10.0.0.1-50");
        ProfileLabel.Text = P("Scan profile", "掃描設定檔");
        FlagsLabel.Text = P("Common flags", "常用旗標");
        ExtraLabel.Text = P("Extra flags (optional)", "額外旗標（可選）");
        ExtraBox.PlaceholderText = P("e.g. -p 80,443 --script vuln", "例如 -p 80,443 --script vuln");
        PreviewLabel.Text = P("Command preview", "命令預覽");

        RunBtn.Content = P("Run scan", "開始掃描");
        CancelBtn.Content = P("Cancel", "取消");
        SaveBtn.Content = P("Save results…", "儲存結果…");

        ColHost.Text = P("Host", "主機");
        ColPort.Text = P("Port", "連接埠");
        ColProto.Text = P("Proto", "協定");
        ColState.Text = P("State", "狀態");
        ColService.Text = P("Service", "服務");
        ColVersion.Text = P("Version / OS", "版本／作業系統");
        if (_rows.Count == 0)
        {
            EmptyHint.Visibility = Visibility.Visible;
            EmptyHint.Text = P("No results yet — enter a target and run a scan.",
                "暫時未有結果 — 輸入目標然後開始掃描。");
        }

        // Profiles
        int sel = ProfileBox.SelectedIndex < 0 ? 1 : ProfileBox.SelectedIndex;
        ProfileBox.Items.Clear();
        foreach (var pr in NmapService.Profiles)
            ProfileBox.Items.Add(P(pr.En, pr.Zh) + (pr.NeedsAdmin ? " ★" : ""));
        ProfileBox.SelectedIndex = Math.Min(sel, ProfileBox.Items.Count - 1);

        // Flag toggles — rebuilt with current language. Preserve checked state by flag.
        var wasOn = _flagChecks.Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToHashSet();
        FlagsHost.Children.Clear();
        FlagsHost2.Children.Clear();
        _flagChecks.Clear();
        int i = 0;
        foreach (var fo in NmapService.CommonFlags)
        {
            var cb = new CheckBox
            {
                Content = P(fo.En, fo.Zh) + (fo.NeedsAdmin ? " ★" : ""),
                Tag = fo.Flag,
                IsChecked = wasOn.Contains(fo.Flag),
                MinWidth = 0,
            };
            cb.Checked += Any_Changed;
            cb.Unchecked += Any_Changed;
            _flagChecks.Add(cb);
            (i++ < 4 ? FlagsHost : FlagsHost2).Children.Add(cb);
        }
        _building = false;
    }

    // ===== engine detection =====

    private async Task CheckEngine()
    {
        bool ok = await Task.Run(NmapService.IsAvailable);
        EngineBar.IsOpen = !ok;
        if (!ok)
        {
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("Nmap not found", "搵唔到 Nmap");
            EngineBar.Message = P("Click to install Nmap automatically (winget · Insecure.Nmap, bundles the Npcap driver) — no restart needed.",
                "撳一下自動安裝 Nmap（winget · Insecure.Nmap，附帶 Npcap 驅動）— 唔使重啟。");
            EngineBar.ActionButton = EngineBars.AutoInstallButton(
                NmapService.WingetId, "Install Nmap automatically", "自動安裝 Nmap",
                async () => { await CheckEngine(); }, NmapService.Rescan);
        }
        else EngineBar.ActionButton = null;

        RunBtn.IsEnabled = ok && _cts is null;
    }

    // ===== ops rows (hand-built control rows — no TweakCard) =====

    private void PopulateOps()
    {
        _ops ??= NmapOperations.All().ToList();
        OpsPanel.Children.Clear();
        bool first = true;
        foreach (var op in _ops)
        {
            if (!first) OpsPanel.Children.Add(BuildDivider());
            first = false;
            OpsPanel.Children.Add(BuildRow(op));
        }
    }

    // ---- One clean row: bilingual title + description on the left, control on the right ----
    private FrameworkElement BuildRow(TweakDefinition op)
    {
        var grid = new Grid { Padding = new Thickness(0, 12, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };

        text.Children.Add(new TextBlock { Text = op.Title.Primary, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });

        if (!string.IsNullOrWhiteSpace(op.Title.Secondary))
            text.Children.Add(new TextBlock
            {
                Text = op.Title.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });

        if (!string.IsNullOrWhiteSpace(op.Description.Primary))
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Primary,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            });
        if (!string.IsNullOrWhiteSpace(op.Description.Secondary))
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });

        // A cheap coloured status pill (e.g. "Installed / Not found"), if the op supplies one.
        var pill = BuildStatusPill(op);
        if (pill is not null) text.Children.Add(pill);

        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var control = BuildControl(op);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        return grid;
    }

    private Border BuildDivider() => new()
    {
        Height = 1,
        Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        Opacity = 0.6,
    };

    /// <summary>對應每種 Tweak 種類砌一個真控件 · Build the matching WinUI control for the tweak kind.</summary>
    private FrameworkElement BuildControl(TweakDefinition op) => op.Kind switch
    {
        TweakKind.Toggle => BuildToggle(op),
        TweakKind.Choice => BuildChoice(op),
        TweakKind.Slider => BuildSlider(op),
        TweakKind.Number => BuildNumber(op),
        TweakKind.Info => BuildInfo(op),
        _ => BuildAction(op), // Action (and any other kind) → button
    };

    // ---------------- Optional coloured status pill ----------------
    private FrameworkElement? BuildStatusPill(TweakDefinition op)
    {
        if (op.ColoredStatus is null) return null;
        string en, zh; StatusColor color;
        try { (en, zh, color) = op.ColoredStatus(); }
        catch { return null; }

        var (bg, fg) = StatusBrushes(color);
        var border = new Border
        {
            Background = bg,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2, 8, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0),
        };
        border.Child = new TextBlock { Text = P(en, zh), Foreground = fg, FontSize = 11, FontWeight = FontWeights.SemiBold };
        return border;
    }

    private static (Brush bg, Brush fg) StatusBrushes(StatusColor color)
    {
        string bgKey = color switch
        {
            StatusColor.Good => "SystemFillColorSuccessBackgroundBrush",
            StatusColor.Warn => "SystemFillColorCautionBackgroundBrush",
            StatusColor.Bad => "SystemFillColorCriticalBackgroundBrush",
            _ => "SystemFillColorNeutralBackgroundBrush",
        };
        string fgKey = color switch
        {
            StatusColor.Good => "SystemFillColorSuccessBrush",
            StatusColor.Warn => "SystemFillColorCautionBrush",
            StatusColor.Bad => "SystemFillColorCriticalBrush",
            _ => "TextFillColorPrimaryBrush",
        };
        var res = Application.Current.Resources;
        var bg = res.TryGetValue(bgKey, out var b) && b is Brush bb ? bb : new SolidColorBrush(Microsoft.UI.Colors.Gray);
        var fg = res.TryGetValue(fgKey, out var f) && f is Brush ff ? ff : new SolidColorBrush(Microsoft.UI.Colors.Black);
        return (bg, fg);
    }

    // ---------------- Action → Button awaiting RunAsync ----------------
    private FrameworkElement BuildAction(TweakDefinition op)
    {
        var label = op.ActionLabel?.Get(Loc.I.Language) ?? P("Run", "執行");
        var btn = new Button { Content = label, MinWidth = 110 };
        if (op.ActionLabel is not null)
            ToolTipService.SetToolTip(btn, $"{op.ActionLabel.En} · {op.ActionLabel.Zh}");

        btn.Click += async (_, _) =>
        {
            if (_rowBusy || op.RunAsync is null) return;
            if (op.Destructive && !await ConfirmAsync(op)) return;

            _rowBusy = true;
            btn.IsEnabled = false;
            var restore = btn.Content;
            btn.Content = new ProgressRing { IsActive = true, Width = 18, Height = 18 };
            try
            {
                var result = await op.RunAsync(CancellationToken.None);
                ShowResult(result);
                // Rebuild rows so any coloured status pill (installed / not found) refreshes.
                PopulateOps();
            }
            catch (Exception ex)
            {
                ShowError(op, ex);
            }
            finally
            {
                btn.Content = restore;
                btn.IsEnabled = true;
                _rowBusy = false;
            }
        };
        return btn;
    }

    // ---------------- Toggle → ToggleSwitch ----------------
    private FrameworkElement BuildToggle(TweakDefinition op)
    {
        var toggle = new ToggleSwitch { OnContent = "On · 開", OffContent = "Off · 熄" };
        bool suppress = true;
        try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* show as off */ }
        suppress = false;

        toggle.Toggled += (_, _) =>
        {
            if (suppress || op.SetIsOn is null) return;
            try { op.SetIsOn(toggle.IsOn); ShowApplied(op); }
            catch (Exception ex)
            {
                suppress = true;
                try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* ignore */ }
                suppress = false;
                ShowError(op, ex);
            }
        };
        return toggle;
    }

    // ---------------- Choice → ComboBox ----------------
    private FrameworkElement BuildChoice(TweakDefinition op)
    {
        var combo = new ComboBox { MinWidth = 170 };
        if (op.Choices is not null)
            foreach (var c in op.Choices)
                combo.Items.Add(new ComboBoxItem { Content = c.Label.Get(Loc.I.Language), Tag = c.Value });

        bool suppress = true;
        try
        {
            var cur = op.GetCurrentChoice?.Invoke();
            if (cur is not null && op.Choices is not null)
                for (int i = 0; i < op.Choices.Count; i++)
                    if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                    { combo.SelectedIndex = i; break; }
        }
        catch { /* leave unselected */ }
        suppress = false;

        combo.SelectionChanged += (_, _) =>
        {
            if (suppress || op.SetChoice is null) return;
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string val)
            {
                try { op.SetChoice(val); ShowApplied(op); }
                catch (Exception ex)
                {
                    ShowError(op, ex);
                    suppress = true;
                    try
                    {
                        var cur = op.GetCurrentChoice?.Invoke();
                        if (cur is not null && op.Choices is not null)
                            for (int i = 0; i < op.Choices.Count; i++)
                                if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                                { combo.SelectedIndex = i; break; }
                    }
                    catch { /* ignore */ }
                    suppress = false;
                }
            }
        };
        return combo;
    }

    // ---------------- Slider → Slider + value label ----------------
    private FrameworkElement BuildSlider(TweakDefinition op)
    {
        string Format(double v)
        {
            bool whole = op.Step >= 1 && Math.Abs(op.Step % 1) < 1e-9;
            string num = whole ? Math.Round(v).ToString(CultureInfo.InvariantCulture)
                               : v.ToString("0.###", CultureInfo.InvariantCulture);
            return op.Unit is null ? num : $"{num} {op.Unit.Primary}";
        }
        double Clamp(double v) => Math.Max(op.Min, Math.Min(op.Max, v));

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider { Minimum = op.Min, Maximum = op.Max, StepFrequency = op.Step, Width = 160, VerticalAlignment = VerticalAlignment.Center };
        var valueText = new TextBlock
        {
            MinWidth = 56,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };

        bool suppress = true;
        try { slider.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { slider.Value = op.Min; }
        suppress = false;
        valueText.Text = Format(slider.Value);

        slider.ValueChanged += (_, e) =>
        {
            valueText.Text = Format(e.NewValue);
            if (suppress || op.SetNumber is null) return;
            try { op.SetNumber(e.NewValue); ShowApplied(op); }
            catch (Exception ex)
            {
                ShowError(op, ex);
                suppress = true;
                try { slider.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { /* ignore */ }
                suppress = false;
            }
        };
        panel.Children.Add(slider);
        panel.Children.Add(valueText);
        return panel;
    }

    // ---------------- Number → NumberBox ----------------
    private FrameworkElement BuildNumber(TweakDefinition op)
    {
        double Clamp(double v) => Math.Max(op.Min, Math.Min(op.Max, v));
        var box = new NumberBox
        {
            Minimum = op.Min,
            Maximum = op.Max,
            SmallChange = op.Step,
            LargeChange = op.Step,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            MinWidth = 140,
            ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
        };
        bool suppress = true;
        try { box.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { box.Value = op.Min; }
        suppress = false;

        box.ValueChanged += (_, e) =>
        {
            if (suppress || op.SetNumber is null || double.IsNaN(e.NewValue)) return;
            try { op.SetNumber(e.NewValue); ShowApplied(op); }
            catch (Exception ex)
            {
                ShowError(op, ex);
                suppress = true;
                try { box.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { /* ignore */ }
                suppress = false;
            }
        };
        return box;
    }

    // ---------------- Info → refreshable TextBlock ----------------
    private FrameworkElement BuildInfo(TweakDefinition op)
    {
        string Safe() { try { return op.GetInfo?.Invoke() ?? "—"; } catch { return "—"; } }

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var info = new TextBlock
        {
            Text = Safe(),
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300,
            HorizontalTextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var refresh = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 }, Padding = new Thickness(8) };
        ToolTipService.SetToolTip(refresh, "Refresh · 重新整理");
        refresh.Click += (_, _) => info.Text = Safe();
        panel.Children.Add(info);
        panel.Children.Add(refresh);
        return panel;
    }

    // ---------------- Confirmation for destructive actions ----------------
    private async Task<bool> ConfirmAsync(TweakDefinition op)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Are you sure?", "確定嗎？"),
            Content = $"{op.Title.En}\n{op.Title.Zh}\n\n" +
                      "This action may be hard to undo.\n呢個動作可能難以復原。",
            PrimaryButtonText = P("Proceed", "繼續"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        try { return await dlg.ShowAsync() == ContentDialogResult.Primary; }
        catch { return false; }
    }

    // ---------------- Applied / result / error status (routes through the persistent ResultBar) ----------------
    private void ShowApplied(TweakDefinition op)
    {
        string en = "Applied.", zh = "已套用。";
        switch (op.Restart)
        {
            case RestartScope.Explorer: en = "Applied. Restart Explorer to see the change."; zh = "已套用。重啟檔案總管就睇到變化。"; break;
            case RestartScope.SignOut: en = "Applied. Sign out and back in to take effect."; zh = "已套用。登出再登入後生效。"; break;
            case RestartScope.Reboot: en = "Applied. Reboot to take effect."; zh = "已套用。重新開機後生效。"; break;
        }
        ShowOpStatus(P("Done", "完成"), P(en, zh), InfoBarSeverity.Success);
    }

    private void ShowResult(TweakResult r)
        => ShowOpStatus(
            r.Success ? P("Done", "完成") : P("Failed", "失敗"),
            r.Message is null ? "" : r.Message.Get(Loc.I.Language),
            r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error);

    private void ShowError(TweakDefinition op, Exception ex)
    {
        bool needAdmin = op.RequiresAdmin && !AdminHelper.IsElevated;
        ShowOpStatus(P("Failed", "失敗"),
            needAdmin ? P("This change needs administrator rights.", "呢項更改需要管理員權限。") : ex.Message,
            InfoBarSeverity.Error);
    }

    private void ShowOpStatus(string title, string message, InfoBarSeverity severity)
    {
        ResultBar.IsOpen = true;
        ResultBar.Severity = severity;
        ResultBar.Title = title;
        ResultBar.Message = message ?? "";
    }

    // ===== command building / preview =====

    private void Any_Changed(object sender, object e)
    {
        if (_building) return;
        UpdatePreview();
    }

    private IEnumerable<string> CheckedFlags()
        => _flagChecks.Where(c => c.IsChecked == true).Select(c => (string)c.Tag);

    private string ProfileKey()
    {
        int idx = ProfileBox.SelectedIndex;
        if (idx < 0 || idx >= NmapService.Profiles.Count) idx = 1;
        return NmapService.Profiles[idx].Key;
    }

    private void UpdatePreview()
    {
        var target = (TargetBox.Text ?? "").Trim();
        var args = NmapService.BuildArgs(ProfileKey(), CheckedFlags(), ExtraBox.Text ?? "",
            target.Length == 0 ? "<target>" : target);
        PreviewText.Text = NmapService.PreviewCommand(args);

        // Surface an elevation hint for admin-needing options.
        bool needsAdmin = NmapService.NeedsAdmin(ProfileKey(), CheckedFlags());
        if (needsAdmin && !AdminHelper.IsElevated)
        {
            ConsentBar.IsOpen = true;
            ConsentBar.Severity = InfoBarSeverity.Warning;
            ConsentBar.Title = P("This scan needs administrator", "呢個掃描需要管理員權限");
            ConsentBar.Message = P("OS detection (-O), raw SYN, UDP and aggressive scans require admin + the Npcap driver. Relaunch WinForge as administrator, or the scan may fail or fall back.",
                "作業系統偵測（-O）、raw SYN、UDP 同進取掃描需要管理員權限同 Npcap 驅動。請以管理員身分重開 WinForge，否則掃描可能失敗或退回。");
        }
        else if (ConsentBar.Severity == InfoBarSeverity.Warning)
        {
            ConsentBar.IsOpen = false;
        }
    }

    // ===== run / cancel =====

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        if (_cts is not null) return;

        var target = (TargetBox.Text ?? "").Trim();
        if (!NmapService.IsValidTarget(target))
        {
            ResultBar.IsOpen = true;
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Enter a valid target", "請輸入有效目標");
            ResultBar.Message = P("Provide an IP, hostname, CIDR (192.168.1.0/24) or range (10.0.0.1-50). No shell characters.",
                "請提供 IP、主機名、CIDR（192.168.1.0/24）或範圍（10.0.0.1-50）。不可有 shell 字元。");
            return;
        }

        if (!NmapService.IsAvailable()) { await CheckEngine(); return; }

        var args = NmapService.BuildArgs(ProfileKey(), CheckedFlags(), ExtraBox.Text ?? "", target);

        _cts = new CancellationTokenSource();
        SetRunning(true);
        LogText.Text = "";
        ResultBar.IsOpen = false;
        StatusText.Text = P("Scanning…", "掃描緊…");

        void OnProgress(string line) => _ui.TryEnqueue(() =>
        {
            LogText.Text += line + "\n";
            // Keep the live log from growing without bound.
            if (LogText.Text.Length > 60000) LogText.Text = LogText.Text[^40000..];
            LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null);
        });

        NmapScanResult result;
        try
        {
            result = await NmapService.RunScanAsync(args, OnProgress, _cts.Token);
        }
        catch (Exception ex)
        {
            result = new NmapScanResult { Error = ex.Message };
        }

        _last = result;
        FillResults(result);
        SetRunning(false);
        _cts?.Dispose();
        _cts = null;

        if (result.Cancelled)
        {
            ResultBar.IsOpen = true;
            ResultBar.Severity = InfoBarSeverity.Informational;
            ResultBar.Title = P("Scan cancelled", "已取消掃描");
            ResultBar.Message = "";
            StatusText.Text = P("Cancelled.", "已取消。");
        }
        else if (result.Ok || result.Hosts.Count > 0)
        {
            int ports = result.Hosts.Sum(h => h.Ports.Count(p => p.State == "open"));
            ResultBar.IsOpen = true;
            ResultBar.Severity = InfoBarSeverity.Success;
            ResultBar.Title = P("Scan complete", "掃描完成");
            ResultBar.Message = P($"{result.Hosts.Count(h => h.Status == "up")} host(s) up · {ports} open port(s). {result.Summary}",
                $"{result.Hosts.Count(h => h.Status == "up")} 部主機上線 · {ports} 個開放連接埠。{result.Summary}");
            StatusText.Text = result.Summary;
            SaveBtn.IsEnabled = true;
        }
        else
        {
            ResultBar.IsOpen = true;
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Scan failed", "掃描失敗");
            ResultBar.Message = string.IsNullOrWhiteSpace(result.Error)
                ? P("No output — check the target and your privileges.", "無輸出 — 檢查目標同權限。")
                : result.Error;
            StatusText.Text = "";
        }

        await CheckEngine();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        try { _cts?.Cancel(); } catch { }
        StatusText.Text = P("Cancelling…", "取消緊…");
    }

    private void SetRunning(bool running)
    {
        Busy.IsActive = running;
        RunBtn.IsEnabled = !running && NmapService.IsAvailable();
        CancelBtn.IsEnabled = running;
        TargetBox.IsEnabled = !running;
        ProfileBox.IsEnabled = !running;
        ExtraBox.IsEnabled = !running;
        foreach (var c in _flagChecks) c.IsEnabled = !running;
    }

    private void FillResults(NmapScanResult result)
    {
        _rows.Clear();
        foreach (var port in result.AllPorts) _rows.Add(port);

        // Show hosts with no ports as a synthetic row so a ping sweep still lists discovered hosts.
        foreach (var h in result.Hosts.Where(h => h.Ports.Count == 0 && h.Status == "up"))
        {
            _rows.Add(new NmapPort
            {
                HostAddress = h.Address,
                HostName = h.Hostname,
                State = "up",
                Service = string.IsNullOrEmpty(h.Os) ? (string.IsNullOrEmpty(h.Vendor) ? "" : h.Vendor) : "",
                Version = string.IsNullOrEmpty(h.Os) ? h.Latency : $"{h.Os} ({h.OsAccuracy}%)",
            });
        }

        EmptyHint.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_rows.Count == 0)
            EmptyHint.Text = P("No hosts/ports returned.", "無主機／連接埠回傳。");
    }

    // ===== save =====

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_last is null) return;
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");
        var path = await FileDialogs.SaveFileAsync(
            $"nmap-scan-{stamp}",
            new[]
            {
                new FileDialogs.Filter(P("Nmap XML", "Nmap XML"), "*.xml"),
                new FileDialogs.Filter(P("CSV (flattened)", "CSV（攤平）"), "*.csv"),
            },
            "xml",
            P("Save scan results", "儲存掃描結果"));
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            string content = path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                ? NmapService.ToCsv(_last)
                : (_last.RawXml.Length > 0 ? _last.RawXml : NmapService.ToCsv(_last));
            await System.IO.File.WriteAllTextAsync(path, content);
            ResultBar.IsOpen = true;
            ResultBar.Severity = InfoBarSeverity.Success;
            ResultBar.Title = P("Saved", "已儲存");
            ResultBar.Message = path;
        }
        catch (Exception ex)
        {
            ResultBar.IsOpen = true;
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Could not save", "儲存唔到");
            ResultBar.Message = ex.Message;
        }
    }
}
