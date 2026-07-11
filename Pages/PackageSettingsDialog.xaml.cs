using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 套件管理器設定 · Scrollable settings ContentDialog for the in-app package manager
/// (UniGetUI-style background updates / notifications / proxy / vcpkg / backup / app settings).
/// 全部控件直接讀寫 <see cref="PackageManagerSettings"/>；關閉時 Stop()+Start() 重啟排程器令間隔即時生效。
/// Every control reads/writes <see cref="PackageManagerSettings"/>; on close it restarts the scheduler
/// so interval changes apply immediately. Bilingual throughout.
/// </summary>
public sealed partial class PackageSettingsDialog : ContentDialog
{
    private static string P(string en, string zh) => Loc.I.Pick(en, zh);

    // 間隔顯示文字 · interval preset labels (parallel to PackageManagerSettings.IntervalPresets).
    private static string IntervalLabel(int minutes) => minutes switch
    {
        10 => P("Every 10 minutes · 每 10 分鐘", "每 10 分鐘 · Every 10 minutes"),
        30 => P("Every 30 minutes · 每 30 分鐘", "每 30 分鐘 · Every 30 minutes"),
        60 => P("Every hour · 每小時", "每小時 · Every hour"),
        180 => P("Every 3 hours · 每 3 小時", "每 3 小時 · Every 3 hours"),
        360 => P("Every 6 hours · 每 6 小時", "每 6 小時 · Every 6 hours"),
        720 => P("Every 12 hours · 每 12 小時", "每 12 小時 · Every 12 hours"),
        1440 => P("Every day · 每日", "每日 · Every day"),
        10080 => P("Every week · 每星期", "每星期 · Every week"),
        _ => $"{minutes} min",
    };

    private static string AgeLabel(int days) => days switch
    {
        0 => P("No delay · 即時", "即時 · No delay"),
        1 => P("1 day old · 1 日", "1 日 · 1 day old"),
        3 => P("3 days old · 3 日", "3 日 · 3 days old"),
        7 => P("1 week old · 1 星期", "1 星期 · 1 week old"),
        14 => P("2 weeks old · 2 星期", "2 星期 · 2 weeks old"),
        30 => P("1 month old · 1 個月", "1 個月 · 1 month old"),
        _ => P($"{days} days · {days} 日", $"{days} 日 · {days} days"),
    };

    public PackageSettingsDialog()
    {
        InitializeComponent();
        Title = P("Package manager settings", "套件管理設定");
        CloseButtonText = P("Close", "關閉");
        DefaultButton = ContentDialogButton.Close;
        Build();
    }

    /// <summary>顯示設定對話框；關閉時套用排程變更 · Show the settings dialog; applies scheduler changes on close.</summary>
    public static async Task ShowAsync(XamlRoot root)
    {
        try
        {
            var dlg = new PackageSettingsDialog { XamlRoot = root };
            await dlg.ShowAsync();
        }
        catch { /* never throw onto the caller */ }
        finally
        {
            // 套用間隔／開關改變 · apply interval / toggle changes by restarting the scheduler.
            try { PackageUpdateScheduler.Stop(); PackageUpdateScheduler.Start(); } catch { }
        }
    }

    // ===== building blocks =====

    private void Build()
    {
        BuildScheduledUpdates();
        BuildUpdateSecurity();
        BuildOperations();
        BuildNotifications();
        BuildPerManager();
        BuildProxy();
        BuildVcpkg();
        BuildBackup();
        BuildAppSettings();
    }

    private void AddGroup(string title, params UIElement[] children)
    {
        Root.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 15,
            Margin = new Thickness(0, 6, 0, 0),
        });
        var panel = new StackPanel { Spacing = 8 };
        foreach (var c in children) panel.Children.Add(c);
        Root.Children.Add(new Border
        {
            Padding = new Thickness(14, 12, 14, 12),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = panel,
        });
    }

    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
    };

    private static ToggleSwitch Toggle(string header, bool value, Action<bool> set)
    {
        var t = new ToggleSwitch { Header = header, IsOn = value, HorizontalAlignment = HorizontalAlignment.Left };
        t.Toggled += (_, _) => { try { set(t.IsOn); } catch { } };
        return t;
    }

    private static ComboBox Combo(string header, IReadOnlyList<string> labels, int selectedIndex, Action<int> onChange)
    {
        var c = new ComboBox { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var l in labels) c.Items.Add(l);
        c.SelectedIndex = selectedIndex < 0 || selectedIndex >= labels.Count ? 0 : selectedIndex;
        c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0) { try { onChange(c.SelectedIndex); } catch { } } };
        return c;
    }

    // ===== 1) Scheduled updates =====

    private ToggleSwitch _meteredToggle = null!, _batteryToggle = null!, _saverToggle = null!;

    private void BuildScheduledUpdates()
    {
        var enable = Toggle(P("Check for updates automatically", "自動檢查更新"),
            PackageManagerSettings.AutoCheckEnabled, v => PackageManagerSettings.AutoCheckEnabled = v);

        var presets = PackageManagerSettings.IntervalPresets;
        int idx = Array.IndexOf(presets, PackageManagerSettings.CheckIntervalMinutes);
        if (idx < 0) idx = Array.IndexOf(presets, 60);
        var interval = Combo(P("Check interval", "檢查間隔"),
            presets.Select(IntervalLabel).ToList(), idx,
            i => PackageManagerSettings.CheckIntervalMinutes = presets[i]);

        var autoInstall = Toggle(P("Automatically install updates", "自動安裝更新"),
            PackageManagerSettings.AutoInstallUpdates, v => PackageManagerSettings.AutoInstallUpdates = v);

        _meteredToggle = Toggle(P("Skip on metered connections", "流量計費網絡時略過"),
            PackageManagerSettings.DisableOnMetered, v => PackageManagerSettings.DisableOnMetered = v);
        _batteryToggle = Toggle(P("Skip while on battery", "用電池時略過"),
            PackageManagerSettings.DisableOnBattery, v => PackageManagerSettings.DisableOnBattery = v);
        _saverToggle = Toggle(P("Skip in Battery Saver", "慳電模式時略過"),
            PackageManagerSettings.DisableOnBatterySaver, v => PackageManagerSettings.DisableOnBatterySaver = v);

        AddGroup(P("Scheduled updates", "排程更新"),
            enable,
            interval,
            autoInstall,
            Hint(P("When auto-install is on, found updates are installed in the background.",
                   "開咗自動安裝後，搵到嘅更新會喺背景自動安裝。")),
            _meteredToggle, _batteryToggle, _saverToggle);
    }

    // ===== 2) Update security =====

    private void BuildUpdateSecurity()
    {
        var presets = PackageManagerSettings.MinimumAgePresets;
        int cur = PackageManagerSettings.MinimumUpdateAgeDays;
        int idx = Array.IndexOf(presets, cur);
        // 加一個「自訂」選項 · add a "custom" entry.
        var labels = presets.Select(AgeLabel).ToList();
        labels.Add(P("Custom…", "自訂…"));
        int customIdx = labels.Count - 1;

        var custom = new NumberBox
        {
            Header = P("Custom minimum age (days)", "自訂最少年齡（日）"),
            Minimum = 0, Maximum = 3650, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Value = cur,
            Visibility = idx < 0 ? Visibility.Visible : Visibility.Collapsed,
        };
        custom.ValueChanged += (_, _) =>
        {
            if (double.IsNaN(custom.Value)) return;
            try { PackageManagerSettings.MinimumUpdateAgeDays = (int)Math.Round(custom.Value); } catch { }
        };

        var combo = Combo(P("Only show updates at least this old", "只顯示夠舊嘅更新"),
            labels, idx < 0 ? customIdx : idx, i =>
            {
                if (i == customIdx)
                {
                    custom.Visibility = Visibility.Visible;
                    try { PackageManagerSettings.MinimumUpdateAgeDays = (int)Math.Round(custom.Value); } catch { }
                }
                else
                {
                    custom.Visibility = Visibility.Collapsed;
                    PackageManagerSettings.MinimumUpdateAgeDays = presets[i];
                }
            });

        var hostWarn = Toggle(P("Warn when the installer source changes (winget)", "安裝程式來源改變時警告（winget）"),
            PackageManagerSettings.WarnInstallerHostChange, v => PackageManagerSettings.WarnInstallerHostChange = v);

        AddGroup(P("Update security", "更新安全"),
            combo, custom,
            Hint(P("A minimum age lets a release \"settle\" before you install it.",
                   "設最少年齡可以等個版本「沉澱」一陣先安裝。")),
            hostWarn);
    }

    // ===== 3) Operations =====

    private void BuildOperations()
    {
        var counts = Enumerable.Range(1, 10).ToList();
        int idx = counts.IndexOf(PackageManagerSettings.ParallelOperationCount);
        var combo = Combo(P("Parallel operations", "同時操作數"),
            counts.Select(n => n.ToString()).ToList(), idx < 0 ? 1 : idx,
            i =>
            {
                PackageManagerSettings.ParallelOperationCount = counts[i];
                PackageOperationCoordinator.RefreshConcurrency();
            });

        AddGroup(P("Operations", "操作"),
            combo,
            Hint(P("How many packages may be installed/updated at the same time.",
                   "同一時間最多可以安裝／更新幾多個套件。")));
    }

    // ===== 4) Notifications =====

    private void BuildNotifications()
    {
        var master = Toggle(P("Show notifications", "顯示通知"),
            PackageManagerSettings.NotificationsEnabled, v => PackageManagerSettings.NotificationsEnabled = v);

        var noUpdates = Toggle(P("Mute \"updates available\"", "靜音「有可用更新」"),
            PackageManagerSettings.DisableUpdatesAvailableNotifications,
            v => PackageManagerSettings.DisableUpdatesAvailableNotifications = v);
        var noProgress = Toggle(P("Mute progress", "靜音進度"),
            PackageManagerSettings.DisableProgressNotifications,
            v => PackageManagerSettings.DisableProgressNotifications = v);
        var noSuccess = Toggle(P("Mute success", "靜音成功"),
            PackageManagerSettings.DisableSuccessNotifications,
            v => PackageManagerSettings.DisableSuccessNotifications = v);
        var noError = Toggle(P("Mute errors", "靜音錯誤"),
            PackageManagerSettings.DisableErrorNotifications,
            v => PackageManagerSettings.DisableErrorNotifications = v);

        // 每管理器靜音 · per-manager mute checkboxes.
        var muteLabel = new TextBlock
        {
            Text = P("Mute specific managers", "靜音指定管理器"),
            FontWeight = FontWeights.SemiBold, FontSize = 13, Margin = new Thickness(0, 4, 0, 0),
        };
        var muteWrap = new StackPanel { Spacing = 2 };
        foreach (var m in PackageManagerRegistry.All)
        {
            string key = m.Key;
            var cb = new CheckBox
            {
                Content = $"{m.NameEn} · {m.NameZh}",
                IsChecked = PackageManagerSettings.IsManagerMuted(key),
            };
            cb.Checked += (_, _) => { try { PackageManagerSettings.SetManagerMuted(key, true); } catch { } };
            cb.Unchecked += (_, _) => { try { PackageManagerSettings.SetManagerMuted(key, false); } catch { } };
            muteWrap.Children.Add(cb);
        }

        AddGroup(P("Notifications", "通知"),
            master, noUpdates, noProgress, noSuccess, noError, muteLabel, muteWrap);
    }

    // ===== 5) Per-manager executable / args =====

    private void BuildPerManager()
    {
        var panel = new StackPanel { Spacing = 10 };
        foreach (var m in PackageManagerRegistry.All)
        {
            string key = m.Key;

            var pathBox = new TextBox
            {
                Header = $"{m.NameEn} · {m.NameZh} — {P("executable path", "可執行檔路徑")}",
                Text = PackageManagerSettings.GetManagerExecutablePath(key),
                PlaceholderText = m.Cli,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            pathBox.LostFocus += (_, _) => { try { PackageManagerSettings.SetManagerExecutablePath(key, pathBox.Text); } catch { } };

            var browse = new Button { Content = P("Browse…", "瀏覽…") };
            browse.Click += async (_, _) =>
            {
                try
                {
                    var pick = await FileDialogs.OpenFileAsync(".exe", ".cmd", ".bat", ".ps1");
                    if (!string.IsNullOrEmpty(pick))
                    {
                        pathBox.Text = pick;
                        PackageManagerSettings.SetManagerExecutablePath(key, pick);
                    }
                }
                catch { }
            };

            var argsBox = new TextBox
            {
                Header = P("Extra call arguments", "額外呼叫參數"),
                Text = PackageManagerSettings.GetManagerExecutableArgs(key),
                PlaceholderText = "--flag value",
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            argsBox.LostFocus += (_, _) => { try { PackageManagerSettings.SetManagerExecutableArgs(key, argsBox.Text); } catch { } };

            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(pathBox, 0);
            browse.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetColumn(browse, 1);
            row.Children.Add(pathBox);
            row.Children.Add(browse);

            panel.Children.Add(row);
            panel.Children.Add(argsBox);
        }

        var expander = new Expander
        {
            Header = P("Per-manager executable & arguments", "各管理器可執行檔同參數"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = new ScrollViewer { MaxHeight = 280, Content = panel },
        };

        AddGroup(P("Per-manager", "各管理器"), expander);
    }

    // ===== 6) Proxy =====

    private void BuildProxy()
    {
        var url = new TextBox
        {
            Header = P("Proxy URL", "代理 URL"),
            Text = PackageManagerSettings.ProxyUrl,
            PlaceholderText = "http://host:port",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        url.LostFocus += (_, _) => { try { PackageManagerSettings.ProxyUrl = url.Text; } catch { } };

        var user = new TextBox
        {
            Header = P("Proxy username (optional)", "代理使用者名稱（可選）"),
            Text = PackageManagerSettings.ProxyUser,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        user.LostFocus += (_, _) => { try { PackageManagerSettings.ProxyUser = user.Text; } catch { } };

        var pass = new PasswordBox
        {
            Header = P("Proxy password (optional)", "代理密碼（可選）"),
            Password = PackageManagerSettings.ProxyPassword,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        pass.LostFocus += (_, _) => { try { PackageManagerSettings.ProxyPassword = pass.Password; } catch { } };

        AddGroup(P("Proxy", "代理"),
            url, user, pass,
            Hint(P("winget uses --proxy; npm/pip/bun fold credentials into the URL. Other managers may need env vars.",
                   "winget 用 --proxy；npm／pip／bun 會將帳密塞入 URL。其他管理器可能要用環境變數。")));
    }

    // ===== 7) vcpkg =====

    private void BuildVcpkg()
    {
        var root = new TextBox
        {
            Header = P("vcpkg root (VCPKG_ROOT)", "vcpkg 根目錄（VCPKG_ROOT）"),
            Text = PackageManagerSettings.VcpkgRoot,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        root.LostFocus += (_, _) => { try { PackageManagerSettings.VcpkgRoot = root.Text; } catch { } };

        var browse = new Button { Content = P("Choose folder…", "揀資料夾…") };
        browse.Click += async (_, _) =>
        {
            try
            {
                var dir = await FileDialogs.OpenFolderAsync(P("Select vcpkg root", "揀 vcpkg 根目錄"));
                if (!string.IsNullOrEmpty(dir)) { root.Text = dir; PackageManagerSettings.VcpkgRoot = dir; }
            }
            catch { }
        };

        var triplet = new TextBox
        {
            Header = P("vcpkg triplet", "vcpkg triplet"),
            Text = PackageManagerSettings.VcpkgTriplet,
            PlaceholderText = "x64-windows",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        triplet.LostFocus += (_, _) => { try { PackageManagerSettings.VcpkgTriplet = triplet.Text; } catch { } };

        var rootRow = new Grid { ColumnSpacing = 8 };
        rootRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rootRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(root, 0);
        browse.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetColumn(browse, 1);
        rootRow.Children.Add(root);
        rootRow.Children.Add(browse);

        AddGroup(P("vcpkg", "vcpkg"), rootRow, triplet);
    }

    // ===== 8) Backup =====

    private void BuildBackup()
    {
        var enable = Toggle(P("Back up installed packages locally", "本地備份已安裝套件"),
            PackageManagerSettings.LocalBackupEnabled, v => PackageManagerSettings.LocalBackupEnabled = v);

        var dir = new TextBox
        {
            Header = P("Backup folder", "備份資料夾"),
            Text = PackageManagerSettings.LocalBackupDir,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        dir.LostFocus += (_, _) => { try { PackageManagerSettings.LocalBackupDir = dir.Text; } catch { } };

        var browse = new Button { Content = P("Choose folder…", "揀資料夾…") };
        browse.Click += async (_, _) =>
        {
            try
            {
                var pick = await FileDialogs.OpenFolderAsync(P("Select backup folder", "揀備份資料夾"));
                if (!string.IsNullOrEmpty(pick)) { dir.Text = pick; PackageManagerSettings.LocalBackupDir = pick; }
            }
            catch { }
        };

        var fileName = new TextBox
        {
            Header = P("Backup file name", "備份檔名"),
            Text = PackageManagerSettings.LocalBackupFileName,
            PlaceholderText = "winforge-packages",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        fileName.LostFocus += (_, _) => { try { PackageManagerSettings.LocalBackupFileName = fileName.Text; } catch { } };

        var timestamp = Toggle(P("Append timestamp to file name", "檔名加時間戳"),
            PackageManagerSettings.BackupTimestamping, v => PackageManagerSettings.BackupTimestamping = v);

        var dirRow = new Grid { ColumnSpacing = 8 };
        dirRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dirRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(dir, 0);
        browse.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetColumn(browse, 1);
        dirRow.Children.Add(dir);
        dirRow.Children.Add(browse);

        var backupNow = new Button { Content = P("Back up now", "立即備份") };
        var status = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };
        backupNow.Click += async (_, _) =>
        {
            backupNow.IsEnabled = false;
            status.Text = P("Backing up…", "備份緊…");
            try
            {
                // 先把目前框入面嘅值寫返去設定 · persist the current box values first.
                PackageManagerSettings.LocalBackupDir = dir.Text;
                PackageManagerSettings.LocalBackupFileName = fileName.Text;
                var ok = await BackupNowAsync();
                status.Text = ok
                    ? P("Backup written.", "已寫入備份。")
                    : P("Backup failed — check the folder.", "備份失敗 — 請檢查資料夾。");
            }
            catch (Exception ex) { status.Text = ex.Message; }
            finally { backupNow.IsEnabled = true; }
        };

        AddGroup(P("Local backup", "本地備份"),
            enable, dirRow, fileName, timestamp,
            Hint(P("A JSON list of installed packages is written here automatically once a day, and when you click \"Back up now\".",
                   "已安裝套件嘅 JSON 清單會每日自動寫一次，撳「立即備份」亦會即刻寫。")),
            backupNow, status);
    }

    /// <summary>立即寫一個本地備份（重用排程器嘅直接 File IO 路徑）· Write one local backup right now.</summary>
    private static async Task<bool> BackupNowAsync()
    {
        try
        {
            var dir = PackageManagerSettings.LocalBackupDir;
            if (string.IsNullOrWhiteSpace(dir)) return false;
            System.IO.Directory.CreateDirectory(dir);

            List<PackageItem> installed;
            try { installed = await PackageManagerRegistry.AllInstalledAsync(null, CancellationToken.None); }
            catch { installed = new(); }

            var entries = installed.Select(i => new
            {
                Manager = i.ManagerKey, i.Id, i.Name, i.Version, i.Source,
            }).ToList();

            var baseName = PackageManagerSettings.LocalBackupFileName;
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "winforge-packages";
            baseName = System.IO.Path.GetFileNameWithoutExtension(baseName);
            foreach (var c in System.IO.Path.GetInvalidFileNameChars()) baseName = baseName.Replace(c, '_');

            var fileName = PackageManagerSettings.BackupTimestamping
                ? $"{baseName}-{DateTime.Now:yyyyMMdd-HHmmss}.json"
                : $"{baseName}.json";

            var payload = new { CreatedUtc = DateTime.UtcNow, Count = entries.Count, Packages = entries };
            var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(dir, fileName), json);
            return true;
        }
        catch { return false; }
    }

    // ===== 9) App settings (export / import / reset) =====

    private void BuildAppSettings()
    {
        var export = new Button { Content = P("Export settings…", "匯出設定…") };
        var import = new Button { Content = P("Import settings…", "匯入設定…") };
        var reset = new Button { Content = P("Reset package settings", "重設套件設定") };

        export.Click += async (_, _) =>
        {
            try
            {
                var path = await FileDialogs.SaveFileAsync("winforge-settings", ".json");
                if (string.IsNullOrEmpty(path)) return;
                SettingsStore.ExportTo(path);
                Notify(InfoBarSeverity.Success, P("Settings exported.", "已匯出設定。"));
            }
            catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
        };

        import.Click += async (_, _) =>
        {
            try
            {
                var path = await FileDialogs.OpenFileAsync(".json");
                if (string.IsNullOrEmpty(path)) return;
                int n = SettingsStore.ImportFrom(path);
                Notify(InfoBarSeverity.Success, P($"Imported {n} setting(s). Reopen this dialog to see changes.",
                                                  $"已匯入 {n} 項設定。重開呢個對話框就睇到變更。"));
            }
            catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
        };

        reset.Click += async (_, _) =>
        {
            var confirm = new ContentDialog
            {
                Title = P("Reset package settings?", "重設套件設定？"),
                Content = P("This restores all package-manager settings to their defaults. Other app settings are untouched.",
                            "呢個會將所有套件管理設定還原做預設值。其他 app 設定唔受影響。"),
                PrimaryButtonText = P("Reset", "重設"),
                CloseButtonText = P("Cancel", "取消"),
                XamlRoot = this.XamlRoot,
                DefaultButton = ContentDialogButton.Close,
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
            try
            {
                ResetPackageSettings();
                Notify(InfoBarSeverity.Success, P("Package settings reset. Reopen this dialog to see defaults.",
                                                  "已重設套件設定。重開呢個對話框就睇到預設值。"));
            }
            catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
        };

        var btns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        btns.Children.Add(export);
        btns.Children.Add(import);
        btns.Children.Add(reset);

        AddGroup(P("App settings", "App 設定"),
            btns,
            Hint(P("Export/import/reset use the whole-app settings store.", "匯出／匯入／重設使用整個 app 嘅設定儲存。")));
    }

    /// <summary>將所有套件設定還原做預設 · Restore package settings to defaults.</summary>
    private static void ResetPackageSettings()
    {
        PackageManagerSettings.AutoCheckEnabled = false;
        PackageManagerSettings.CheckIntervalMinutes = 60;
        PackageManagerSettings.AutoInstallUpdates = false;
        PackageManagerSettings.DisableOnMetered = true;
        PackageManagerSettings.DisableOnBattery = true;
        PackageManagerSettings.DisableOnBatterySaver = true;
        PackageManagerSettings.MinimumUpdateAgeDays = 0;
        PackageManagerSettings.WarnInstallerHostChange = true;
        PackageManagerSettings.ParallelOperationCount = 2;
        PackageManagerSettings.NotificationsEnabled = true;
        PackageManagerSettings.DisableProgressNotifications = false;
        PackageManagerSettings.DisableSuccessNotifications = false;
        PackageManagerSettings.DisableErrorNotifications = false;
        PackageManagerSettings.DisableUpdatesAvailableNotifications = false;
        foreach (var m in PackageManagerRegistry.All) PackageManagerSettings.SetManagerMuted(m.Key, false);
        foreach (var m in PackageManagerRegistry.All)
        {
            PackageManagerSettings.SetManagerExecutablePath(m.Key, "");
            PackageManagerSettings.SetManagerExecutableArgs(m.Key, "");
        }
        PackageManagerSettings.ProxyUrl = "";
        PackageManagerSettings.ProxyUser = "";
        PackageManagerSettings.ProxyPassword = "";
        PackageManagerSettings.VcpkgRoot = "";
        PackageManagerSettings.VcpkgTriplet = "";
        PackageManagerSettings.LocalBackupEnabled = false;
        PackageManagerSettings.LocalBackupDir = "";
        PackageManagerSettings.LocalBackupFileName = "winforge-packages";
        PackageManagerSettings.BackupTimestamping = true;
    }

    private void Notify(InfoBarSeverity severity, string message)
    {
        Bar.Severity = severity;
        Bar.Title = severity == InfoBarSeverity.Error ? P("Error", "錯誤") : P("Done", "完成");
        Bar.Message = message;
        Bar.IsOpen = true;
    }
}
