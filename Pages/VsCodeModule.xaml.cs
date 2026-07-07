using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// VS Code 模組 · The Visual Studio Code workbench: wraps the <c>code</c> CLI for opening files/folders/
/// workspaces (new/reuse/empty window), diffing and merging picked files, go-to file:line, profiles,
/// settings/keybindings quick-edit, the <c>code tunnel</c> remote-dev control, an extension manager
/// (list / install / uninstall / import / export sets) and the full operation library. Detects a missing
/// <c>code</c> and offers a one-click winget install (Microsoft.VisualStudioCode).
/// </summary>
public sealed partial class VsCodeModule : Page
{
    private List<TweakDefinition>? _ops;
    private List<VsCodeService.Extension> _extensions = new();
    private string? _diffA, _diffB, _gotoFile;
    private bool _rowBusy; // guard so only one op control-row action runs at a time

    public VsCodeModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
        Loaded += async (_, _) =>
        {
            Render();
            BuildOpenActions();
            PopulateOps(string.Empty);
            await Recheck();
        };
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        BuildOpenActions();
        BuildExtList();
        PopulateOps(OpsFilter.Text ?? string.Empty);
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "VS Code · VS Code 編輯器";
        HeaderBlurb.Text = P(
            "Drive Visual Studio Code from WinForge: open files/folders/workspaces, new windows, diff & merge, go-to line, profiles, settings, the remote tunnel, and a full extension manager — all through the code CLI.",
            "由 WinForge 操控 Visual Studio Code：開檔案／資料夾／工作區、開新視窗、比對與合併、跳去指定行、profile、設定、遠端 tunnel，仲有完整擴充功能管理 — 全部經 code CLI。");

        RefreshBtn.Content = P("Refresh", "重新整理");
        InsidersToggle.OnContent = "Insiders";
        InsidersToggle.OffContent = P("Stable", "穩定版");
        ToolTipService.SetToolTip(InsidersToggle, P("Target the Insiders build (code-insiders)", "改用 Insiders 版本（code-insiders）"));

        OpenLabel.Text = P("Open in VS Code", "喺 VS Code 開啟");
        BuildOpenModeCombo();

        CompareLabel.Text = P("Compare & navigate", "比對與導覽");
        DiffABox.PlaceholderText = P("Left file…", "左邊檔案…");
        DiffBBox.PlaceholderText = P("Right file…", "右邊檔案…");
        DiffBtn.Content = P("Diff", "比對");
        GotoFileBox.PlaceholderText = P("File…", "檔案…");
        ToolTipService.SetToolTip(GotoLineBox, P("Line", "行"));
        ToolTipService.SetToolTip(GotoColBox, P("Column", "欄"));
        GotoBtn.Content = P("Go to line", "跳去");

        ConfigLabel.Text = P("Profiles, config & remote", "Profile、設定與遠端");
        ProfileBox.PlaceholderText = P("Profile name (then pick a path to open)…", "Profile 名（再揀路徑開啟）…");
        ProfileOpenBtn.Content = P("Open with profile…", "用 profile 開…");
        SettingsBtn.Content = P("Edit settings.json", "編輯 settings.json");
        KeybindingsBtn.Content = P("Edit keybindings.json", "編輯 keybindings.json");
        TunnelBtn.Content = P("Start tunnel", "啟動 tunnel");

        ExtLabel.Text = P("Extensions", "擴充功能");
        ExtRefreshBtn.Content = P("Refresh", "重新整理");
        ExtExportBtn.Content = P("Export…", "匯出…");
        ExtImportBtn.Content = P("Import…", "匯入…");
        ExtInstallBox.PlaceholderText = P("Extension ID (e.g. ms-python.python)…", "擴充功能 ID（例如 ms-python.python）…");
        ExtInstallBtn.Content = P("Install", "安裝");
        ExtFilterBox.PlaceholderText = P("Filter installed extensions…", "篩選已安裝擴充功能…");

        OpsLabel.Text = P("Operation library", "操作庫");
        OpsFilter.PlaceholderText = P("Filter operations…", "篩選操作…");
    }

    private void BuildOpenModeCombo()
    {
        int sel = OpenModeCombo.SelectedIndex < 0 ? 0 : OpenModeCombo.SelectedIndex;
        OpenModeCombo.Items.Clear();
        OpenModeCombo.Items.Add(P("Same window", "同一視窗"));
        OpenModeCombo.Items.Add(P("New window (-n)", "新視窗（-n）"));
        OpenModeCombo.Items.Add(P("Reuse window (-r)", "重用視窗（-r）"));
        OpenModeCombo.Items.Add(P("Add to window (--add)", "加入視窗（--add）"));
        OpenModeCombo.SelectedIndex = sel;
    }

    private void BuildOpenActions()
    {
        OpenActions.Children.Clear();
        AddOpen(P("File…", "檔案…"), PickFileToOpen);
        AddOpen(P("Folder…", "資料夾…"), PickFolderToOpen);
        AddOpen(P("Workspace…", "工作區…"), PickWorkspaceToOpen);
        AddOpen(P("Empty window", "空白視窗"), async () => { await Run(VsCodeService.OpenEmptyWindow()); });
        AddOpen(P("Terminal here…", "喺度開終端機…"), PickFolderTerminal);
    }

    private void AddOpen(string label, Func<Task> run)
    {
        var b = new Button { Content = label };
        b.Click += async (_, _) => { b.IsEnabled = false; try { await run(); } finally { b.IsEnabled = true; } };
        OpenActions.Children.Add(b);
    }

    private int OpenMode => OpenModeCombo.SelectedIndex < 0 ? 0 : OpenModeCombo.SelectedIndex;

    private Task<TweakResult> OpenWithMode(string path) => OpenMode switch
    {
        1 => VsCodeService.OpenNewWindow(path),
        2 => VsCodeService.OpenReuseWindow(path),
        3 => VsCodeService.AddFolder(path),
        _ => VsCodeService.Open(path),
    };

    // ===== Engine detection =====

    private async Task Recheck()
    {
        VsCodeService.Rescan();
        bool installed = VsCodeService.IsInstalled;
        SetActionsEnabled(installed);

        if (!installed)
        {
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("VS Code not found", "搵唔到 VS Code");
            EngineBar.Message = P("The code CLI isn't on PATH. Install Visual Studio Code to use this module.",
                "PATH 上面搵唔到 code CLI。安裝 Visual Studio Code 先可以用呢個模組。");
            EngineBar.ActionButton = null;
            EngineBar.Content = EngineBars.AutoInstallProgress(
                "Microsoft.VisualStudioCode", "Install VS Code", "安裝 VS Code",
                async () => await Recheck(), VsCodeService.Rescan);
            EngineBar.IsOpen = true;
            VersionText.Text = P("Not installed.", "未安裝。");
            return;
        }

        EngineBar.IsOpen = false;
        EngineBar.ActionButton = null;
        EngineBar.Content = null;
        var ver = await VsCodeService.Version();
        VersionText.Text = P($"Visual Studio Code {ver} detected.", $"偵測到 Visual Studio Code {ver}。");
        await LoadExtensions();
    }

    private void SetActionsEnabled(bool on)
    {
        foreach (var b in OpenActions.Children.OfType<Button>()) b.IsEnabled = on;
        DiffBtn.IsEnabled = on; GotoBtn.IsEnabled = on;
        ProfileOpenBtn.IsEnabled = on; SettingsBtn.IsEnabled = on; KeybindingsBtn.IsEnabled = on; TunnelBtn.IsEnabled = on;
        ExtRefreshBtn.IsEnabled = on; ExtInstallBtn.IsEnabled = on; ExtImportBtn.IsEnabled = on; ExtExportBtn.IsEnabled = on;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Recheck();

    private async void Insiders_Toggled(object sender, RoutedEventArgs e)
    {
        VsCodeService.UseInsiders = InsidersToggle.IsOn;
        await Recheck();
    }

    // ===== Open pickers =====

    private async Task PickFileToOpen()
    {
        var f = await FileDialogs.OpenFileAsync();
        if (f is not null) await Run(OpenWithMode(f));
    }

    private async Task PickFolderToOpen()
    {
        var f = await FileDialogs.OpenFolderAsync();
        if (f is not null) await Run(OpenWithMode(f));
    }

    private async Task PickWorkspaceToOpen()
    {
        var f = await FileDialogs.OpenFileAsync(".code-workspace");
        if (f is not null) await Run(OpenWithMode(f));
    }

    private async Task PickFolderTerminal()
    {
        var f = await FileDialogs.OpenFolderAsync();
        if (f is null) return;
        if (!VsCodeService.OpenTerminalAt(f))
            AppendConsole(P("Could not open a terminal.\n", "開唔到終端機。\n"));
    }

    // ===== Diff / goto =====

    private async void DiffAPick_Click(object sender, RoutedEventArgs e)
    {
        var f = await FileDialogs.OpenFileAsync();
        if (f is not null) { _diffA = f; DiffABox.Text = f; }
    }

    private async void DiffBPick_Click(object sender, RoutedEventArgs e)
    {
        var f = await FileDialogs.OpenFileAsync();
        if (f is not null) { _diffB = f; DiffBBox.Text = f; }
    }

    private async void Diff_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_diffA) || string.IsNullOrEmpty(_diffB))
        {
            AppendConsole(P("Pick both files to diff first.\n", "請先揀兩個檔案嚟比對。\n"));
            return;
        }
        await Run(VsCodeService.Diff(_diffA!, _diffB!, wait: false));
    }

    private async void GotoPick_Click(object sender, RoutedEventArgs e)
    {
        var f = await FileDialogs.OpenFileAsync();
        if (f is not null) { _gotoFile = f; GotoFileBox.Text = f; }
    }

    private async void Goto_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_gotoFile))
        {
            AppendConsole(P("Pick a file first.\n", "請先揀檔案。\n"));
            return;
        }
        int line = (int)Math.Max(1, GotoLineBox.Value);
        int col = (int)Math.Max(1, GotoColBox.Value);
        await Run(VsCodeService.GotoLine(_gotoFile!, line, col));
    }

    // ===== Profiles / config / tunnel =====

    private async void ProfileOpen_Click(object sender, RoutedEventArgs e)
    {
        var prof = ProfileBox.Text?.Trim();
        if (string.IsNullOrEmpty(prof))
        {
            AppendConsole(P("Enter a profile name first.\n", "請先輸入 profile 名。\n"));
            return;
        }
        var folder = await FileDialogs.OpenFolderAsync();
        if (folder is not null) await Run(VsCodeService.OpenWithProfile(prof!, folder));
    }

    private async void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var p = VsCodeService.UserSettingsPath();
        if (!File.Exists(p))
        {
            AppendConsole(P($"settings.json not found at {p}\n", $"喺 {p} 搵唔到 settings.json\n"));
            return;
        }
        await Run(VsCodeService.Open(p));
    }

    private async void OpenKeybindings_Click(object sender, RoutedEventArgs e)
    {
        var p = VsCodeService.UserKeybindingsPath();
        if (!File.Exists(p))
        {
            AppendConsole(P($"keybindings.json not found at {p}\n", $"喺 {p} 搵唔到 keybindings.json\n"));
            return;
        }
        await Run(VsCodeService.Open(p));
    }

    private void Tunnel_Click(object sender, RoutedEventArgs e)
    {
        if (VsCodeService.StartTunnel())
            AppendConsole(P("Started 'code tunnel' in a console — follow the device-login prompt.\n",
                "已喺主控台啟動 'code tunnel' — 跟住裝置登入提示做。\n"));
        else
            AppendConsole(P("Could not start the tunnel.\n", "啟動唔到 tunnel。\n"));
    }

    // ===== Extensions =====

    private async void ExtRefresh_Click(object sender, RoutedEventArgs e) => await LoadExtensions();

    private async Task LoadExtensions()
    {
        ExtListPanel.Children.Clear();
        ExtListPanel.Children.Add(new TextBlock
        {
            Text = P("Loading…", "載入緊…"),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize = 13,
        });
        _extensions = await VsCodeService.ListExtensions();
        BuildExtList();
    }

    private void BuildExtList()
    {
        ExtListPanel.Children.Clear();
        var filter = (ExtFilterBox.Text ?? string.Empty).Trim().ToLowerInvariant();
        var shown = string.IsNullOrEmpty(filter)
            ? _extensions
            : _extensions.Where(x => x.Id.ToLowerInvariant().Contains(filter)).ToList();

        if (shown.Count == 0)
        {
            ExtListPanel.Children.Add(new TextBlock
            {
                Text = _extensions.Count == 0
                    ? P("No extensions installed (or VS Code unavailable).", "未有安裝擴充功能（或者 VS Code 不可用）。")
                    : P("No matches.", "冇符合項目。"),
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
            });
            ExtLabel.Text = P($"Extensions ({_extensions.Count})", $"擴充功能（{_extensions.Count}）");
            return;
        }
        ExtLabel.Text = P($"Extensions ({_extensions.Count})", $"擴充功能（{_extensions.Count}）");

        foreach (var ext in shown)
        {
            var grid = new Grid { ColumnSpacing = 6 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var texts = new StackPanel();
            texts.Children.Add(new TextBlock { Text = ext.Id, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            if (!string.IsNullOrEmpty(ext.Version))
                texts.Children.Add(new TextBlock
                {
                    Text = "v" + ext.Version,
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                });
            Grid.SetColumn(texts, 0);
            grid.Children.Add(texts);

            string id = ext.Id;
            var uninstall = new Button { Content = P("Uninstall", "解除安裝"), Padding = new Thickness(8, 2, 8, 2) };
            uninstall.Click += async (_, _) =>
            {
                uninstall.IsEnabled = false;
                AppendConsole($"$ code --uninstall-extension {id}\n");
                await Run(VsCodeService.UninstallExtension(id));
                await LoadExtensions();
            };
            Grid.SetColumn(uninstall, 1);
            grid.Children.Add(uninstall);

            ExtListPanel.Children.Add(new Border
            {
                Child = grid,
                Padding = new Thickness(8, 6, 8, 6),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0, 0, 0, 1),
            });
        }
    }

    private void ExtFilter_Changed(object sender, TextChangedEventArgs e) => BuildExtList();

    private async void ExtInstall_Click(object sender, RoutedEventArgs e)
    {
        var id = ExtInstallBox.Text?.Trim();
        if (string.IsNullOrEmpty(id))
        {
            AppendConsole(P("Enter an extension ID first.\n", "請先輸入擴充功能 ID。\n"));
            return;
        }
        ExtInstallBtn.IsEnabled = false;
        AppendConsole($"$ code --install-extension {id}\n");
        try
        {
            await Run(VsCodeService.InstallExtension(id!));
            ExtInstallBox.Text = string.Empty;
            await LoadExtensions();
        }
        finally { ExtInstallBtn.IsEnabled = true; }
    }

    private async void ExtExport_Click(object sender, RoutedEventArgs e)
    {
        if (_extensions.Count == 0) await LoadExtensions();
        var dest = await FileDialogs.SaveFileAsync("vscode-extensions.txt", ".txt");
        if (dest is null) return;
        try
        {
            await File.WriteAllLinesAsync(dest, _extensions.Select(x => x.Id));
            AppendConsole(P($"Exported {_extensions.Count} extension IDs to {dest}\n",
                $"已匯出 {_extensions.Count} 個擴充功能 ID 去 {dest}\n"));
        }
        catch (Exception ex) { AppendConsole(ex.Message + "\n"); }
    }

    private async void ExtImport_Click(object sender, RoutedEventArgs e)
    {
        var src = await FileDialogs.OpenFileAsync(".txt");
        if (src is null) return;
        ExtImportBtn.IsEnabled = false;
        try
        {
            var ids = (await File.ReadAllLinesAsync(src))
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith('#'))
                .ToList();
            AppendConsole(P($"Importing {ids.Count} extension(s)…\n", $"匯入緊 {ids.Count} 個擴充功能…\n"));
            var progress = new Progress<string>(s => AppendConsole(s));
            var r = await VsCodeService.InstallMany(ids, progress, CancellationToken.None);
            AppendConsole((Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) + "\n");
            await LoadExtensions();
        }
        catch (Exception ex) { AppendConsole(ex.Message + "\n"); }
        finally { ExtImportBtn.IsEnabled = true; }
    }

    // ===== shared run + console =====

    private async Task Run(Task<TweakResult> action)
    {
        try
        {
            var r = await action;
            var txt = (r.Output ?? string.Empty).Trim();
            if (txt.Length > 0) AppendConsole(txt + "\n");
            else if (!r.Success) AppendConsole((Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) + "\n");
        }
        catch (Exception ex) { AppendConsole(ex.Message + "\n"); }
    }

    private void AppendConsole(string text)
    {
        ConsoleBorder.Visibility = Visibility.Visible;
        ConsoleLog.Text += text;
        if (ConsoleLog.Text.Length > 20000) ConsoleLog.Text = ConsoleLog.Text[^20000..];
    }

    // ===== Operation library =====

    private void OpsFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateOps(sender.Text ?? string.Empty);
    }

    private void PopulateOps(string filter)
    {
        _ops ??= VsCodeOperations.All().ToList();
        OpsPanel.Children.Clear();
        IEnumerable<TweakDefinition> shown = _ops;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = shown.Where(t => t.SearchHaystack.Contains(f));
        }

        bool first = true;
        foreach (var op in shown)
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
            string num = whole ? Math.Round(v).ToString(System.Globalization.CultureInfo.InvariantCulture)
                               : v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
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

    // ---------------- Applied / error status (routes through the persistent StatusBar) ----------------
    private void ShowApplied(TweakDefinition op)
    {
        string en = "Applied.", zh = "已套用。";
        switch (op.Restart)
        {
            case RestartScope.Explorer: en = "Applied. Restart Explorer to see the change."; zh = "已套用。重啟檔案總管就睇到變化。"; break;
            case RestartScope.SignOut: en = "Applied. Sign out and back in to take effect."; zh = "已套用。登出再登入後生效。"; break;
            case RestartScope.Reboot: en = "Applied. Reboot to take effect."; zh = "已套用。重新開機後生效。"; break;
        }
        ShowStatus(P(en, zh), InfoBarSeverity.Success);
    }

    private void ShowError(TweakDefinition op, Exception ex)
    {
        bool needAdmin = op.RequiresAdmin && !AdminHelper.IsElevated;
        ShowStatus(needAdmin
            ? P("This change needs administrator rights.", "呢項更改需要管理員權限。")
            : ex.Message, InfoBarSeverity.Error);
    }

    private void ShowResult(TweakResult r)
        => ShowStatus(Loc.I.IsCantonesePrimary ? (r.Message?.Zh ?? "") : (r.Message?.En ?? ""),
            r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error);

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        StatusBar.IsOpen = true;
        StatusBar.Severity = severity;
        StatusBar.Message = message;
    }
}
