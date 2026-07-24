using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// AWS Manager · AWS Console 式資源管理中心，CLI 工作台只保留做進階後備入口。
/// AWS Console-style resource manager with an advanced CLI workbench kept as the exact-access escape hatch.
/// </summary>
public sealed partial class AwsCliModule : Page
{
    private List<string> _services = new();
    private List<string> _operations = new();
    private List<AwsCliService.AwsProfile> _profiles = new();
    private string? _selectedService;
    private string? _selectedOperation;
    private Dictionary<string, TextBox> _paramBoxes = new();
    private readonly HashSet<string> _pendingAwsTempFiles = new(StringComparer.OrdinalIgnoreCase);
    private bool _loadingContext;
    private string? _pendingConsoleView;

    public AwsCliModule()
    {
        InitializeComponent();
        CleanupStaleAwsTempFiles();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLang;
            AwsCliService.StopStream();
            string[] pending;
            lock (_pendingAwsTempFiles) pending = _pendingAwsTempFiles.ToArray();
            DeleteAwsTempFiles(pending);
            DisposeConsoleSession();
        };
        Loaded += async (_, _) =>
        {
            Render();
            InitializeConsoleShell();
            await CheckEngine();
            LoadContext();
            await RefreshProfiles();
            RefreshHistory();
            await LoadConsoleAsync();
            ApplyPendingConsoleView();
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var view = (e.Parameter as string)?.Trim().ToLowerInvariant();
        _pendingConsoleView = view is "home" or "resources" or "s3" or "ec2" or "services" or "operations" or "advanced"
            ? view
            : null;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private void OnLang(object? sender, EventArgs e)
    {
        RebuildConsoleServiceCatalogForLanguage();
        Render();
        RefreshHistory();
    }

    private void Render()
    {
        Header.Title = "AWS Manager · AWS 管理中心";
        HeaderBlurb.Text = P(
            "A full AWS Console-style manager inside WinForge: account and Region context, unified resource search, native S3 and EC2 management, service workspaces, operations dashboards, and an advanced CLI workbench when exact command-level access is needed.",
            "WinForge 入面嘅完整 AWS Console 式管理中心：帳戶同區域情境、統一資源搜尋、原生 S3 同 EC2 管理、服務工作區、營運儀表板，另加進階 CLI 工作台畀你需要精確指令級控制時使用。");

        ContextHeader.Text = P("Profile & context · Profile 同情境", "Profile 同情境 · Profile & context");
        ProfileLabel.Text = P("Profile", "設定檔");
        RegionLabel.Text = P("Region", "區域");
        ToolTipService.SetToolTip(ProfileContextIcon, ProfileLabel.Text);
        ToolTipService.SetToolTip(RegionContextIcon, RegionLabel.Text);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ProfileBox, ProfileLabel.Text);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(RegionBox, RegionLabel.Text);
        OutputLabel.Text = P("Output", "輸出格式");
        WhoAmIBtn.Content = P("Who am I", "我係邊個");
        SsoBtn.Content = P("SSO login", "SSO 登入");
        ConfigureBtn.Content = P("Add / edit profile", "新增／編輯 profile");
        RefreshProfilesBtn.Content = P("Refresh", "重新整理");

        BrowserHeader.Text = P("Generated command form (advanced)", "生成指令表單（進階）");
        BrowserHint.Text = P(
            "This compatibility form is secondary to the resource manager. Pick a service and operation, or use the raw workbench for exact CLI access.",
            "呢個相容表單只係資源管理中心嘅輔助工具。揀服務同操作，或者用原始工作台做精確 CLI 控制。");
        ServiceLabel.Text = P("Services", "服務");
        OperationLabel.Text = P("Operations", "操作");
        ServiceFilter.PlaceholderText = P("Filter services…", "篩選服務…");
        OperationFilter.PlaceholderText = P("Filter operations…", "篩選操作…");
        ParamLabel.Text = P("Parameters", "參數");
        RawJsonLabel.Text = P("Raw input JSON (overrides fields when shown):", "原始輸入 JSON（顯示時會蓋過欄位）：");
        ToggleJsonBtn.Content = P("Raw JSON", "原始 JSON");
        BuildRunBtn.Content = P("Build & Run", "組合並執行");
        ShowHelpBtn.Content = P("Help", "說明");

        RawHeader.Text = P("Raw command", "原始指令");
        RawHint.Text = P(
            "Type any aws command (without the leading 'aws'). Output streams live; history is opt-in and commands containing secret material are never persisted.",
            "打任何 aws 指令（唔使加開頭嘅 'aws'）。輸出即時串流；歷史記錄要自行開啟，而包含密鑰資料嘅指令永遠唔會儲存。");
        RawBox.PlaceholderText = P("s3 ls   ·   ec2 describe-instances   ·   sts get-caller-identity",
            "s3 ls   ·   ec2 describe-instances   ·   sts get-caller-identity");
        RawRunBtn.Content = P("Run", "執行");
        RawStopBtn.Content = P("Stop", "停止");
        RawCopyBtn.Content = P("Copy output", "複製輸出");
        RawSaveBtn.Content = P("Save output…", "儲存輸出…");
        RawClearBtn.Content = P("Clear", "清除");
        RawFavBtn.Content = P("★ Favorite", "★ 收藏");
        RawHistoryToggle.Content = P("Save safe commands to history", "將安全指令儲存到歷史");

        HistHeader.Text = P("History & favorites", "歷史與收藏");
        ClearHistBtn.Content = P("Clear history", "清除歷史");
        HistTab.Header = P("History", "歷史");
        FavTab.Header = P("Favorites", "收藏");

        ServicePanelsHeader.Text = P("Quick service panels", "服務快捷面板");
        ServicePanelsHint.Text = P(
            "Convenience wrappers for the most-used services. Everything here also works through the generic browser above.",
            "最常用服務嘅貼心包裝。呢度所有嘢透過上面嘅通用瀏覽器一樣做到。");

        if (RegionBox.Items.Count == 0)
        {
            RegionBox.Items.Add(P("(none / default)", "（無／預設）"));
            foreach (var r in AwsCliService.AllRegions) RegionBox.Items.Add(r);
        }
        if (OutputBox.Items.Count == 0)
            foreach (var o in AwsCliService.OutputFormats) OutputBox.Items.Add(o);

        RenderConsoleShell();
    }

    // ── Engine detection ───────────────────────────────────────────────────────────────

    private async Task CheckEngine()
    {
        bool ok = await AwsCliService.IsInstalledAsync();
        _cliInstalled = ok;
        if (ok)
        {
            EngineBar.IsOpen = false;
            EngineBar.ActionButton = null;
            EngineBar.Content = null;
            var ver = await AwsCliService.VersionAsync();
            if (!string.IsNullOrWhiteSpace(ver))
            {
                EngineBar.IsOpen = true;
                EngineBar.Severity = InfoBarSeverity.Success;
                EngineBar.Title = P("AWS CLI ready", "AWS CLI 就緒");
                EngineBar.Message = ver;
            }
            return;
        }
        // The managed Console uses the AWS SDK and does not require aws.exe. Surface installation only
        // inside the Advanced CLI destination instead of blocking the whole manager with a warning.
        EngineBar.IsOpen = false;
        EngineBar.ActionButton = null;
        EngineBar.Content = null;
    }

    // ── Context (profile / region / output) ──────────────────────────────────────────────

    private void LoadContext()
    {
        _loadingContext = true;
        // region
        var r = AwsCliService.ActiveRegion;
        RegionBox.SelectedIndex = string.IsNullOrEmpty(r) ? 0 : Math.Max(0, RegionBox.Items.ToList().IndexOf(r));
        // output
        var o = AwsCliService.ActiveOutput;
        OutputBox.SelectedItem = OutputBox.Items.Contains(o) ? o : (object?)"json";
        _loadingContext = false;
    }

    private async Task RefreshProfiles()
    {
        try { _profiles = AwsCliService.ListProfiles(); } catch { _profiles = new(); }
        _loadingContext = true;
        ProfileBox.Items.Clear();
        ProfileBox.Items.Add(P("(default / none)", "（預設／無）"));
        foreach (var p in _profiles)
        {
            var tag = p.IsSso ? " · SSO" : (p.HasCredentials ? " · keys" : "");
            ProfileBox.Items.Add($"{p.Name}{tag}");
        }
        var active = AwsCliService.ActiveProfile;
        if (!string.IsNullOrEmpty(active))
        {
            var idx = _profiles.FindIndex(p => p.Name.Equals(active, StringComparison.OrdinalIgnoreCase));
            ProfileBox.SelectedIndex = idx >= 0 ? idx + 1 : 0;
        }
        else ProfileBox.SelectedIndex = 0;
        _loadingContext = false;
        await Task.CompletedTask;
    }

    private void ProfileBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingContext) return;
        var idx = ProfileBox.SelectedIndex;
        AwsCliService.ActiveProfile = (idx <= 0 || idx - 1 >= _profiles.Count) ? "" : _profiles[idx - 1].Name;
        // If the profile carries its own region/output, reflect it.
        if (idx > 0 && idx - 1 < _profiles.Count)
        {
            var p = _profiles[idx - 1];
            if (!string.IsNullOrEmpty(p.Region))
            {
                AwsCliService.ActiveRegion = p.Region!;
                _loadingContext = true;
                RegionBox.SelectedIndex = Math.Max(0, RegionBox.Items.ToList().IndexOf(p.Region!));
                _loadingContext = false;
            }
        }
        AwsCliService.PersistContext();
        ScheduleConsoleContextRefresh();
    }

    private void RegionBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingContext) return;
        AwsCliService.ActiveRegion = RegionBox.SelectedIndex <= 0 ? "" : (RegionBox.SelectedItem as string ?? "");
        AwsCliService.PersistContext();
        ScheduleConsoleContextRefresh();
    }

    private void OutputBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingContext) return;
        AwsCliService.ActiveOutput = OutputBox.SelectedItem as string ?? "json";
        AwsCliService.PersistContext();
    }

    private async void RefreshProfiles_Click(object sender, RoutedEventArgs e) => await RefreshProfiles();

    private async void WhoAmI_Click(object sender, RoutedEventArgs e)
    {
        await RefreshConsoleContextAsync(loadResources: true);
    }

    private void Sso_Click(object sender, RoutedEventArgs e)
    {
        var r = AwsCliService.SsoLogin(AwsCliService.ActiveProfile);
        ShowResult(r);
    }

    private async void Configure_Click(object sender, RoutedEventArgs e)
    {
        var profileName = new TextBox { PlaceholderText = P("Profile name (e.g. default)", "profile 名稱（例如 default）"), Text = AwsCliService.ActiveProfile };
        var keyId = new TextBox { PlaceholderText = P("Access key ID", "存取金鑰 ID") };
        var secret = new PasswordBox { PlaceholderText = P("Secret access key (masked)", "私密存取金鑰（已遮蔽）") };
        var region = new TextBox { PlaceholderText = P("Region (optional)", "區域（選填）"), Text = AwsCliService.ActiveRegion };
        var output = new TextBox { PlaceholderText = P("Output (json/text/table/yaml)", "輸出（json/text/table/yaml）"), Text = AwsCliService.ActiveOutput };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = P(
                "Secrets go directly to the standard AWS shared credentials store through the SDK; they are never logged or placed on a child-process command line.",
                "密鑰經 AWS SDK 直接寫入標準 shared credentials store；唔會記錄，亦唔會放落 child-process 指令列。"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
        });
        panel.Children.Add(profileName);
        panel.Children.Add(keyId);
        panel.Children.Add(secret);
        panel.Children.Add(region);
        panel.Children.Add(output);

        var dlg = new ContentDialog
        {
            Title = P("Add / edit AWS profile", "新增／編輯 AWS profile"),
            Content = panel,
            PrimaryButtonText = P("Save", "儲存"),
            CloseButtonText = P("Cancel", "取消"),
            XamlRoot = this.XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        var r = await AwsCliService.ConfigureProfile(profileName.Text.Trim(), keyId.Text.Trim(),
            secret.Password, region.Text.Trim(), output.Text.Trim());
        ShowResult(r);
        if (r.Success)
        {
            AwsCliService.ActiveProfile = profileName.Text.Trim();
            if (!string.IsNullOrWhiteSpace(region.Text))
                AwsCliService.ActiveRegion = region.Text.Trim();
            if (!string.IsNullOrWhiteSpace(output.Text))
                AwsCliService.ActiveOutput = output.Text.Trim();
            AwsCliService.PersistContext();
            ScheduleConsoleContextRefresh();
            LoadContext();
            await RefreshProfiles();
        }
    }

    // ── Generic command browser ─────────────────────────────────────────────────────────

    private async Task LoadServices()
    {
        _services = await AwsCliService.ListServicesAsync();
        FilterServices(ServiceFilter.Text ?? "");
    }

    private void FilterServices(string filter)
    {
        ServiceList.Items.Clear();
        IEnumerable<string> shown = _services;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _services.Where(s => s.Contains(f, StringComparison.OrdinalIgnoreCase));
        }
        foreach (var s in shown) ServiceList.Items.Add(s);
    }

    private void ServiceFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) FilterServices(sender.Text ?? "");
    }

    private async void ServiceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedService = ServiceList.SelectedItem as string;
        _selectedOperation = null;
        OperationList.Items.Clear();
        ParamFields.Children.Clear();
        _paramBoxes.Clear();
        if (string.IsNullOrEmpty(_selectedService)) return;
        SkeletonRing.IsActive = true;
        try
        {
            _operations = await AwsCliService.ListOperationsAsync(_selectedService!);
            FilterOperations(OperationFilter.Text ?? "");
        }
        finally { SkeletonRing.IsActive = false; }
    }

    private void FilterOperations(string filter)
    {
        OperationList.Items.Clear();
        IEnumerable<string> shown = _operations;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _operations.Where(s => s.Contains(f, StringComparison.OrdinalIgnoreCase));
        }
        foreach (var s in shown) OperationList.Items.Add(s);
    }

    private void OperationFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) FilterOperations(sender.Text ?? "");
    }

    private async void OperationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedOperation = OperationList.SelectedItem as string;
        ParamFields.Children.Clear();
        _paramBoxes.Clear();
        ParamJsonBox.Text = "";
        if (string.IsNullOrEmpty(_selectedService) || string.IsNullOrEmpty(_selectedOperation)) return;

        SkeletonRing.IsActive = true;
        try
        {
            var skeleton = await AwsCliService.GenerateSkeletonAsync(_selectedService!, _selectedOperation!);
            ParamJsonBox.Text = string.IsNullOrWhiteSpace(skeleton) ? "{}" : Prettify(skeleton);
            BuildParamFields(skeleton);
        }
        finally { SkeletonRing.IsActive = false; }
    }

    private void BuildParamFields(string skeletonJson)
    {
        ParamFields.Children.Clear();
        _paramBoxes.Clear();
        if (string.IsNullOrWhiteSpace(skeletonJson)) return;
        try
        {
            using var doc = JsonDocument.Parse(skeletonJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                // Only render simple (string/number/bool) top-level params as fields.
                // Complex (object/array) params are best edited via the raw JSON box.
                if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    var note = new TextBlock
                    {
                        Text = P($"{prop.Name} — complex; edit via Raw JSON", $"{prop.Name} — 複雜，請用原始 JSON 編輯"),
                        FontSize = 11,
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    };
                    ParamFields.Children.Add(note);
                    continue;
                }
                var label = "--" + CamelToKebab(prop.Name);
                var tb = new TextBox { Header = label, PlaceholderText = prop.Value.ToString() };
                _paramBoxes[label] = tb;
                ParamFields.Children.Add(tb);
            }
            if (_paramBoxes.Count == 0 && ParamFields.Children.Count == 0)
                ParamFields.Children.Add(new TextBlock { Text = P("(no input parameters)", "（無輸入參數）"), FontSize = 12 });
        }
        catch
        {
            ParamFields.Children.Add(new TextBlock { Text = P("Could not parse the skeleton — use Raw JSON.", "無法解析 skeleton — 請用原始 JSON。"), FontSize = 12 });
        }
    }

    private void ToggleJson_Click(object sender, RoutedEventArgs e)
    {
        ParamJsonBox.Visibility = ParamJsonBox.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void BuildRun_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedService) || string.IsNullOrEmpty(_selectedOperation))
        {
            ShowResult(TweakResult.Fail("Pick a service and operation first.", "請先揀服務同操作。"));
            return;
        }
        var sb = new StringBuilder($"{_selectedService} {_selectedOperation}");
        List<string>? cleanupFiles = null;
        if (ParamJsonBox.Visibility == Visibility.Visible && !IsEmptyJson(ParamJsonBox.Text))
        {
            // Use --cli-input-json with the raw JSON (written to a temp file to avoid quoting issues).
            try
            {
                var tmp = Path.Combine(Path.GetTempPath(), $"winforge-aws-{Guid.NewGuid():N}.json");
                await File.WriteAllTextAsync(tmp, ParamJsonBox.Text);
                cleanupFiles = new List<string> { tmp };
                sb.Append($" --cli-input-json file://{tmp.Replace('\\', '/')}");
            }
            catch { }
        }
        else
        {
            foreach (var (flag, box) in _paramBoxes)
            {
                var v = box.Text?.Trim();
                if (string.IsNullOrEmpty(v)) continue;
                sb.Append(' ').Append(flag).Append(' ').Append(v.Contains(' ') ? $"\"{v}\"" : v);
            }
        }
        await RunToOutput(sb.ToString(), decorate: true, cleanupFiles: cleanupFiles);
    }

    private async void ShowHelp_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedService)) return;
        var args = string.IsNullOrEmpty(_selectedOperation)
            ? $"{_selectedService} help"
            : $"{_selectedService} {_selectedOperation} help";
        await RunToOutput(args, decorate: false);
    }

    // ── Raw command box (streaming) ─────────────────────────────────────────────────────

    private void RawBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) { RawRun_Click(sender, e); e.Handled = true; }
    }

    private void RawRun_Click(object sender, RoutedEventArgs e)
    {
        var args = (RawBox.Text ?? "").Trim();
        if (args.StartsWith("aws ", StringComparison.OrdinalIgnoreCase)) args = args[4..].Trim();
        if (string.IsNullOrEmpty(args)) return;
        StreamRun(args, decorate: true, record: true);
    }

    private async Task RunToOutput(string args, bool decorate, IReadOnlyList<string>? cleanupFiles = null)
    {
        // For browser/help actions: stream so long output appears progressively.
        StreamRun(args, decorate, record: decorate, cleanupFiles: cleanupFiles);
        await Task.CompletedTask;
    }

    private void StreamRun(string args, bool decorate, bool record, IReadOnlyList<string>? cleanupFiles = null)
    {
        if (AwsCliService.IsStreaming)
        {
            DeleteAwsTempFiles(cleanupFiles);
            ShowResult(TweakResult.Fail("A command is already running — Stop it first.", "已有指令喺度行 — 請先停止。"));
            return;
        }
        TrackAwsTempFiles(cleanupFiles);
        OutText.Text = "";
        var shown = decorate ? AwsCliService.Decorate(args) : args;
        AppendLine($"$ aws {AwsCliService.Redact(shown)}");
        RawRunBtn.IsEnabled = false;
        RawStopBtn.IsEnabled = true;
        if (record && RawHistoryToggle.IsChecked == true) AwsCliService.AddHistory(args);

        bool started = AwsCliService.StartStream(args, decorate,
            line => DispatcherQueue.TryEnqueue(() => AppendLine(line)),
            code =>
            {
                DeleteAwsTempFiles(cleanupFiles);
                DispatcherQueue.TryEnqueue(() =>
                {
                    AppendLine(P($"— done (exit {code}) —", $"— 完成（結束代碼 {code}）—"));
                    RawRunBtn.IsEnabled = true;
                    RawStopBtn.IsEnabled = false;
                    RefreshHistory();
                });
            });

        if (!started)
        {
            DeleteAwsTempFiles(cleanupFiles);
            AppendLine(P("Failed to start aws.", "無法啟動 aws。"));
            RawRunBtn.IsEnabled = true;
            RawStopBtn.IsEnabled = false;
        }
    }

    private void AppendLine(string line)
    {
        OutText.Text += (OutText.Text.Length == 0 ? "" : "\n") + line;
        // keep the buffer bounded
        if (OutText.Text.Length > 200_000) OutText.Text = OutText.Text[^150_000..];
        OutScroller.ChangeView(null, OutScroller.ScrollableHeight, null);
    }

    private void RawStop_Click(object sender, RoutedEventArgs e)
    {
        AwsCliService.StopStream();
        AppendLine(P("— stopped —", "— 已停止 —"));
        RawRunBtn.IsEnabled = true;
        RawStopBtn.IsEnabled = false;
    }

    private void RawCopy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dp = new DataPackage();
            dp.SetText(OutText.Text ?? "");
            Clipboard.SetContent(dp);
            ShowResult(TweakResult.Ok("Output copied.", "已複製輸出。"));
        }
        catch (Exception ex) { ShowResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}")); }
    }

    private async void RawSave_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync("aws-output.txt", ".txt", ".json", ".log");
        if (path is null) return;
        try { await File.WriteAllTextAsync(path, OutText.Text ?? ""); ShowResult(TweakResult.Ok($"Saved to {path}", $"已儲存到 {path}")); }
        catch (Exception ex) { ShowResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}")); }
    }

    private void RawClear_Click(object sender, RoutedEventArgs e) => OutText.Text = "";

    private void RawFav_Click(object sender, RoutedEventArgs e)
    {
        var args = (RawBox.Text ?? "").Trim();
        if (args.StartsWith("aws ", StringComparison.OrdinalIgnoreCase)) args = args[4..].Trim();
        if (string.IsNullOrEmpty(args)) return;
        if (AwsCliService.ContainsSensitiveMaterial(args))
        {
            ShowResult(TweakResult.Fail(
                "Commands containing inline credentials cannot be saved as favorites.",
                "包含行內憑證嘅指令唔可以儲存做收藏。"));
            return;
        }
        AwsCliService.ToggleFavorite(args);
        RefreshHistory();
        ShowResult(AwsCliService.IsFavorite(args)
            ? TweakResult.Ok("Added to favorites.", "已加入收藏。")
            : TweakResult.Ok("Removed from favorites.", "已移除收藏。"));
    }

    // ── History & favorites ─────────────────────────────────────────────────────────────

    private void RefreshHistory()
    {
        HistoryList.Items.Clear();
        foreach (var h in AwsCliService.History()) HistoryList.Items.Add("aws " + h);
        FavoritesList.Items.Clear();
        foreach (var f in AwsCliService.Favorites()) FavoritesList.Items.Add("aws " + f);
    }

    private void HistoryList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => RunFromList(HistoryList.SelectedItem as string);
    private void FavoritesList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => RunFromList(FavoritesList.SelectedItem as string);

    private void RunFromList(string? item)
    {
        if (string.IsNullOrEmpty(item)) return;
        var args = item.StartsWith("aws ", StringComparison.OrdinalIgnoreCase) ? item[4..].Trim() : item;
        RawBox.Text = args;
        StreamRun(args, decorate: true, record: true);
    }

    private void ClearHist_Click(object sender, RoutedEventArgs e) { AwsCliService.ClearHistory(); RefreshHistory(); }

    // ── Rich service panels (S3 / EC2 / IAM / Lambda / CloudWatch) ──────────────────────

    private void BuildServicePanels()
    {
        ServicePanels.Children.Clear();
        ServicePanels.Children.Add(BuildPanel(
            P("S3 — buckets & objects", "S3 — 儲存桶同物件"),
            new (string label, Action act)[]
            {
                (P("List buckets", "列出儲存桶"), () => StreamRun("s3 ls", true, true)),
                (P("List objects in bucket…", "列出儲存桶物件…"), () => PromptThenRun(P("Bucket / prefix (e.g. my-bucket or my-bucket/path)", "儲存桶／前綴（例如 my-bucket 或 my-bucket/path）"), v => $"s3 ls s3://{v} --recursive")),
                (P("Upload file…", "上傳檔案…"), async () => await S3Upload()),
                (P("Download object…", "下載物件…"), async () => await S3Download()),
                (P("Make bucket…", "建立儲存桶…"), () => PromptThenRun(P("New bucket name", "新儲存桶名稱"), v => $"s3 mb s3://{v}")),
                (P("Remove bucket…", "刪除儲存桶…"), () => ConfirmThenRun(P("Remove bucket", "刪除儲存桶"), P("Bucket name (must be empty)", "儲存桶名稱（必須為空）"), v => $"s3 rb s3://{v}")),
            }));

        ServicePanels.Children.Add(BuildPanel(
            P("EC2 — instances", "EC2 — 執行個體"),
            new (string label, Action act)[]
            {
                (P("Describe instances", "列出執行個體"), () => StreamRun("ec2 describe-instances --output table", true, true)),
                (P("Start instance…", "啟動執行個體…"), () => PromptThenRun(P("Instance ID (i-…)", "執行個體 ID（i-…）"), v => $"ec2 start-instances --instance-ids {v}")),
                (P("Stop instance…", "停止執行個體…"), () => PromptThenRun(P("Instance ID (i-…)", "執行個體 ID（i-…）"), v => $"ec2 stop-instances --instance-ids {v}")),
                (P("Reboot instance…", "重啟執行個體…"), () => PromptThenRun(P("Instance ID (i-…)", "執行個體 ID（i-…）"), v => $"ec2 reboot-instances --instance-ids {v}")),
                (P("Terminate instance…", "終止執行個體…"), () => ConfirmThenRun(P("Terminate instance", "終止執行個體"), P("Instance ID (i-…)", "執行個體 ID（i-…）"), v => $"ec2 terminate-instances --instance-ids {v}")),
            }));

        ServicePanels.Children.Add(BuildPanel(
            P("IAM — users & roles", "IAM — 使用者同角色"),
            new (string label, Action act)[]
            {
                (P("List users", "列出使用者"), () => StreamRun("iam list-users --output table", true, true)),
                (P("List roles", "列出角色"), () => StreamRun("iam list-roles --output table", true, true)),
                (P("List groups", "列出群組"), () => StreamRun("iam list-groups --output table", true, true)),
                (P("List policies (local)", "列出政策（本帳號）"), () => StreamRun("iam list-policies --scope Local --output table", true, true)),
            }));

        ServicePanels.Children.Add(BuildPanel(
            P("Lambda — functions", "Lambda — 函式"),
            new (string label, Action act)[]
            {
                (P("List functions", "列出函式"), () => StreamRun("lambda list-functions --output table", true, true)),
                (P("Invoke function…", "呼叫函式…"), async () => await LambdaInvoke()),
            }));

        ServicePanels.Children.Add(BuildPanel(
            P("CloudWatch Logs", "CloudWatch 記錄"),
            new (string label, Action act)[]
            {
                (P("List log groups", "列出記錄群組"), () => StreamRun("logs describe-log-groups --output table", true, true)),
                (P("Tail log group…", "追蹤記錄群組…"), () => PromptThenRun(P("Log group name (/aws/…)", "記錄群組名稱（/aws/…）"), v => $"logs tail {v} --follow")),
            }));
    }

    private Border BuildPanel(string title, (string label, Action act)[] actions)
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var flow = new ButtonFlowPanel();
        foreach (var a in actions)
        {
            var btn = new Button { Content = a.label };
            btn.Click += (_, _) => a.act();
            flow.Children.Add(btn);
        }
        sp.Children.Add(flow);
        return new Border
        {
            Padding = new Thickness(14),
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = sp,
        };
    }

    private void PromptThenRun(string prompt, Func<string, string> build)
    {
        _ = PromptThenRunAsync(prompt, build);
    }

    private async Task PromptThenRunAsync(string prompt, Func<string, string> build)
    {
        var tb = new TextBox { PlaceholderText = prompt, Width = 360 };
        var dlg = new ContentDialog
        {
            Title = prompt,
            Content = tb,
            PrimaryButtonText = P("Run", "執行"),
            CloseButtonText = P("Cancel", "取消"),
            XamlRoot = this.XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var v = tb.Text?.Trim();
        if (string.IsNullOrEmpty(v)) return;
        StreamRun(build(v), decorate: true, record: true);
    }

    private void ConfirmThenRun(string title, string prompt, Func<string, string> build)
    {
        _ = ConfirmThenRunAsync(title, prompt, build);
    }

    private async Task ConfirmThenRunAsync(string title, string prompt, Func<string, string> build)
    {
        var tb = new TextBox { PlaceholderText = prompt, Width = 360 };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = P("This is destructive. Confirm to proceed.", "呢個操作有破壞性。確認先繼續。"), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(tb);
        var dlg = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = P("Confirm & run", "確認並執行"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var v = tb.Text?.Trim();
        if (string.IsNullOrEmpty(v)) return;
        StreamRun(build(v), decorate: true, record: true);
    }

    private async Task S3Upload()
    {
        var local = await FileDialogs.OpenFileAsync();
        if (local is null) return;
        var tb = new TextBox { PlaceholderText = P("Destination, e.g. my-bucket/path/key", "目的地，例如 my-bucket/path/key"), Width = 360 };
        var dlg = new ContentDialog { Title = P("Upload to S3", "上傳到 S3"), Content = tb, PrimaryButtonText = P("Upload", "上傳"), CloseButtonText = P("Cancel", "取消"), XamlRoot = this.XamlRoot };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var dest = tb.Text?.Trim();
        if (string.IsNullOrEmpty(dest)) return;
        StreamRun($"s3 cp \"{local}\" s3://{dest}", true, true);
    }

    private async Task S3Download()
    {
        var tb = new TextBox { PlaceholderText = P("S3 object, e.g. my-bucket/path/key", "S3 物件，例如 my-bucket/path/key"), Width = 360 };
        var dlg = new ContentDialog { Title = P("Download from S3", "由 S3 下載"), Content = tb, PrimaryButtonText = P("Choose destination…", "選擇目的地…"), CloseButtonText = P("Cancel", "取消"), XamlRoot = this.XamlRoot };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var src = tb.Text?.Trim();
        if (string.IsNullOrEmpty(src)) return;
        var name = src.Split('/').LastOrDefault() ?? "download";
        var local = await FileDialogs.SaveFileAsync(name);
        if (local is null) return;
        StreamRun($"s3 cp s3://{src} \"{local}\"", true, true);
    }

    private async Task LambdaInvoke()
    {
        var fn = new TextBox { PlaceholderText = P("Function name", "函式名稱") };
        var payload = new TextBox { PlaceholderText = P("Payload JSON (optional)", "Payload JSON（選填）"), AcceptsReturn = true, Height = 80 };
        var panel = new StackPanel { Spacing = 8, Width = 380 };
        panel.Children.Add(fn);
        panel.Children.Add(payload);
        var dlg = new ContentDialog { Title = P("Invoke Lambda", "呼叫 Lambda"), Content = panel, PrimaryButtonText = P("Invoke", "呼叫"), CloseButtonText = P("Cancel", "取消"), XamlRoot = this.XamlRoot };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var name = fn.Text?.Trim();
        if (string.IsNullOrEmpty(name)) return;
        var outFile = Path.Combine(Path.GetTempPath(), $"lambda-out-{Guid.NewGuid():N}.json");
        var cleanupFiles = new List<string> { outFile };
        var args = new StringBuilder($"lambda invoke --function-name {name}");
        if (!string.IsNullOrWhiteSpace(payload.Text))
        {
            var tmp = Path.Combine(Path.GetTempPath(), $"lambda-payload-{Guid.NewGuid():N}.json");
            try
            {
                await File.WriteAllTextAsync(tmp, payload.Text);
                cleanupFiles.Add(tmp);
                args.Append($" --payload file://{tmp.Replace('\\', '/')} --cli-binary-format raw-in-base64-out");
            }
            catch { }
        }
        args.Append($" \"{outFile}\"");
        StreamRun(args.ToString(), true, true, cleanupFiles);
    }

    private void TrackAwsTempFiles(IEnumerable<string>? paths)
    {
        if (paths is null) return;
        lock (_pendingAwsTempFiles)
        {
            foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
                _pendingAwsTempFiles.Add(path);
        }
    }

    private void DeleteAwsTempFiles(IEnumerable<string>? paths)
    {
        if (paths is null) return;
        foreach (var path in paths.ToArray())
        {
            try { File.Delete(path); } catch { }
            lock (_pendingAwsTempFiles) _pendingAwsTempFiles.Remove(path);
        }
    }

    private static void CleanupStaleAwsTempFiles()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddHours(-1);
            foreach (var pattern in new[] { "winforge-aws-*.json", "lambda-payload-*.json", "lambda-out-*.json" })
            {
                foreach (var path in Directory.EnumerateFiles(Path.GetTempPath(), pattern, SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(path) < cutoff) File.Delete(path);
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────────

    private void ShowResult(TweakResult r)
    {
        ResultBar.IsOpen = true;
        ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = r.Success ? P("Done", "完成") : P("Error", "出錯");
        var msg = (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";
        if (!string.IsNullOrWhiteSpace(r.Output)) msg += "\n" + AwsCliService.Redact(r.Output);
        ResultBar.Message = msg;
        if (!string.IsNullOrWhiteSpace(r.Output))
        {
            OutText.Text = AwsCliService.Redact(r.Output);
            OutScroller.ChangeView(null, OutScroller.ScrollableHeight, null);
        }
    }

    private static string Prettify(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch { return json; }
    }

    private static bool IsEmptyJson(string? json)
    {
        var t = (json ?? "").Trim();
        return t.Length == 0 || t == "{}" || t == "[]";
    }

    private static string CamelToKebab(string name)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0) sb.Append('-');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}

/// <summary>簡單嘅按鈕換行面板 · A minimal horizontal wrap panel for action buttons (WinUI 3 has none).</summary>
internal sealed class ButtonFlowPanel : Panel
{
    private const double Gap = 8;

    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
    {
        double maxWidth = double.IsInfinity(availableSize.Width) ? double.MaxValue : availableSize.Width;
        double x = 0, rowHeight = 0, totalHeight = 0, widest = 0;
        foreach (var child in Children)
        {
            child.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            var ds = child.DesiredSize;
            if (x > 0 && x + ds.Width > maxWidth)
            {
                totalHeight += rowHeight + Gap;
                widest = Math.Max(widest, x - Gap);
                x = 0; rowHeight = 0;
            }
            x += ds.Width + Gap;
            rowHeight = Math.Max(rowHeight, ds.Height);
        }
        totalHeight += rowHeight;
        widest = Math.Max(widest, x - Gap);
        return new Windows.Foundation.Size(double.IsInfinity(availableSize.Width) ? widest : availableSize.Width, totalHeight);
    }

    protected override Windows.Foundation.Size ArrangeOverride(Windows.Foundation.Size finalSize)
    {
        double x = 0, y = 0, rowHeight = 0;
        foreach (var child in Children)
        {
            var ds = child.DesiredSize;
            if (x > 0 && x + ds.Width > finalSize.Width)
            {
                x = 0; y += rowHeight + Gap; rowHeight = 0;
            }
            child.Arrange(new Windows.Foundation.Rect(x, y, ds.Width, ds.Height));
            x += ds.Width + Gap;
            rowHeight = Math.Max(rowHeight, ds.Height);
        }
        return finalSize;
    }
}
