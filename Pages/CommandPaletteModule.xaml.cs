using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 指令面板設定（PowerToys Run／Command Palette 式）· Command Palette control page — enable toggle,
/// hotkey picker, enabled-provider checkboxes and max-results. The launcher itself is a separate
/// borderless topmost window opened by the global hotkey (see CommandPaletteService / CommandPaletteWindow).
/// 全部介面文字雙語。Fully bilingual UI.
/// </summary>
public sealed partial class CommandPaletteModule : Page
{
    private bool _suppress;
    private bool _suppressExtensionChanges;

    public CommandPaletteModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); Sync(); };
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Command Palette · 指令面板";
        HeaderBlurb.Text = P(
            "A global quick-launcher (like PowerToys Run and Command Palette). Press the hotkey anywhere to launch apps, modules, files and Terminal profiles; switch open windows; open saved bookmarks or Remote Desktop sessions; browse local clipboard history; use time/date; type perf for on-demand system metrics; type $display for Windows Settings; type > followed by an intentional command; or manage services with service start/stop/restart <name>.",
            "全域快速啟動器（似 PowerToys Run 同 Command Palette）。喺任何地方按熱鍵就可以啟動程式、模組、檔案同終端機設定檔；切換已開啟視窗；開啟已儲存書籤或者遠端桌面工作階段；瀏覽本機剪貼簿記錄；查時間／日期；輸入 perf 睇即時系統指標；輸入 $顯示器 開 Windows 設定；輸入 > 再加明確指令；或者用 service start／stop／restart <名稱> 管理服務。");

        EnableTitle.Text = P("Enable Command Palette", "啟用指令面板");
        HotkeyLabel.Text = P("Hotkey", "熱鍵");
        OpenNowButton.Content = P("Open now", "立即打開");
        MaxLabel.Text = P("Max results", "最多結果數");
        AppearanceTitle.Text = P("Palette appearance", "指令面板外觀");
        AppearanceBlurb.Text = P(
            "Choose a Solid, Mica or Acrylic backdrop. An optional local image stays behind the palette's contrast-preserving surface.",
            "揀 Solid、Mica 或 Acrylic 背景效果。可選本機圖片會放喺指令面板保留對比度嘅表面後面。");
        BackdropLabel.Text = P("Backdrop", "背景效果");
        BackgroundImageLabel.Text = P("Optional background image", "可選背景圖片");
        BackgroundImageBrowseButton.Content = P("Browse", "瀏覽");
        BackgroundImageClearButton.Content = P("Clear", "清除");
        ProvidersTitle.Text = P("Result providers", "結果提供者");
        ProvidersBlurb.Text = P("Choose which sources contribute results. Disable any you don't want.",
            "揀邊啲來源會貢獻結果。唔想用嘅可以關閉。");
        ExtensionPacksTitle.Text = P("Extension packs", "擴充套件");
        ExtensionPacksBlurb.Text = P(
            "Import a user-managed JSON manifest for safe module, HTTP(S) URL, or copy-text commands. Packs are disabled until you explicitly enable them.",
            "匯入由用戶管理嘅 JSON 資訊檔，加入安全嘅模組、HTTP(S) 網址或者複製文字指令。擴充套件要你明確啟用先會運作。");
        ExtensionImportButton.Content = P("Import manifest", "匯入資訊檔");
        ExtensionTemplateButton.Content = P("Create template", "建立範本");
        BookmarksTitle.Text = P("Bookmarks", "書籤");
        BookmarksBlurb.Text = P("Save web addresses here, then type bookmark <name> in Command Palette. Bookmarks can also be pinned to the Dock with Ctrl+P.",
            "喺呢度儲存網址，之後喺指令面板輸入 bookmark <名稱>。書籤亦都可以用 Ctrl+P 釘選到 Dock。");
        BookmarkNameBox.PlaceholderText = P("Name", "名稱");
        BookmarkUrlBox.PlaceholderText = P("https://example.com", "https://example.com");
        BookmarkAddButton.Content = P("Add bookmark", "加入書籤");
        RemoteDesktopTitle.Text = P("Remote Desktop sessions", "遠端桌面工作階段");
        RemoteDesktopBlurb.Text = P("Save a name and host or host:port, then type rdp <name> in Command Palette. Credentials are never stored here; Windows Remote Desktop prompts normally.",
            "儲存名稱同主機或者主機:連接埠，之後喺指令面板輸入 rdp <名稱>。呢度唔會儲存登入資料；Windows 遠端桌面會照常要求登入。");
        RemoteDesktopNameBox.PlaceholderText = P("Name", "名稱");
        RemoteDesktopHostBox.PlaceholderText = P("host or host:port", "主機或者主機:連接埠");
        RemoteDesktopAddButton.Content = P("Add session", "加入工作階段");
        DockTitle.Text = P("Command Palette Dock", "指令面板 Dock");
        DockBlurb.Text = P(
            "Keep a compact launcher on any screen edge. In the palette, press Ctrl+P to pin or unpin the selected result; saved pins stay on the Dock.",
            "喺任何螢幕邊緣保留精簡啟動器。喺指令面板入面按 Ctrl+P 就可以釘選或者取消釘選所揀結果；已儲存嘅釘選會留喺 Dock。");
        DockSideLabel.Text = P("Dock edge", "Dock 位置");
        DockOpenButton.Content = P("Show Dock", "顯示 Dock");

        // Hotkey choices (preserve current).
        var cur = CommandPaletteService.HotkeyText;
        _suppress = true;
        HotkeyCombo.Items.Clear();
        foreach (var c in CommandPaletteService.HotkeyChoices) HotkeyCombo.Items.Add(c);
        if (!CommandPaletteService.HotkeyChoices.Contains(cur)) HotkeyCombo.Items.Add(cur);
        HotkeyCombo.SelectedItem = cur;
        _suppress = false;

        BuildProviders();
        BuildExtensionPacks();
        BuildBookmarks();
        BuildRemoteDesktopProfiles();
        BuildDockSides();
        BuildAppearance();
        UpdateStatus();
    }

    private void BuildProviders()
    {
        ProvidersPanel.Children.Clear();
        foreach (var p in CommandPaletteService.AllProviders)
        {
            var (en, zh) = CommandPaletteService.ProviderName(p);
            var chk = new CheckBox
            {
                Content = $"{en} · {zh}",
                IsChecked = CommandPaletteService.IsProviderEnabled(p),
                Tag = p,
            };
            chk.Checked += Provider_Changed;
            chk.Unchecked += Provider_Changed;
            ProvidersPanel.Children.Add(chk);
        }
    }

    private void BuildExtensionPacks()
    {
        ExtensionPacksPanel.Children.Clear();
        var packs = CommandPaletteExtensionService.I.Installed;
        ExtensionPacksEmptyText.Text = P(
            "No extension packs are installed. Create a template to begin with a reviewed, safe format.",
            "未安裝擴充套件。建立範本，就可以由已審視嘅安全格式開始。");
        ExtensionPacksEmptyText.Visibility = packs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        _suppressExtensionChanges = true;
        try
        {
            foreach (var pack in packs)
            {
                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var details = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
                details.Children.Add(new TextBlock
                {
                    Text = P(pack.Name, pack.Zh),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
                var description = P(pack.Description, pack.ZhDescription);
                details.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(description)
                        ? P($"{pack.Commands.Count} safe command(s) · {pack.Id}", $"{pack.Commands.Count} 個安全指令 · {pack.Id}")
                        : P($"{description} · {pack.Commands.Count} safe command(s)", $"{description} · {pack.Commands.Count} 個安全指令"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                });
                row.Children.Add(details);

                var enabled = new ToggleSwitch
                {
                    Header = P("Enabled", "已啟用"),
                    IsOn = pack.Enabled,
                    Tag = pack.Id,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                enabled.Toggled += ExtensionEnabled_Toggled;
                Grid.SetColumn(enabled, 1);
                row.Children.Add(enabled);

                var remove = new Button { Content = P("Remove", "移除"), Tag = pack.Id, VerticalAlignment = VerticalAlignment.Center };
                remove.Click += ExtensionRemove_Click;
                Grid.SetColumn(remove, 2);
                row.Children.Add(remove);
                ExtensionPacksPanel.Children.Add(row);
            }
        }
        finally
        {
            _suppressExtensionChanges = false;
        }
    }

    private async void ExtensionImport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var source = await FileDialogs.OpenFileAsync(".json");
            if (string.IsNullOrWhiteSpace(source)) return;

            var result = await Task.Run(() => CommandPaletteExtensionService.I.TryImport(source));
            if (!result.Success || result.Pack is null)
            {
                ShowExtensionInfo(P(
                    "This manifest could not be imported safely. Check its schema, ids, and declarative command targets.",
                    "未能安全匯入呢個資訊檔。請檢查 schema、識別碼同宣告式指令目標。"), InfoBarSeverity.Error);
                return;
            }

            BuildExtensionPacks();
            ShowExtensionInfo(P(
                $"Imported {result.Pack.Name}. It is disabled until you explicitly enable it.",
                $"已匯入 {result.Pack.Zh}。要你明確啟用後先會運作。"), InfoBarSeverity.Success);
        }
        catch
        {
            ShowExtensionInfo(P("WinForge could not import that manifest.", "WinForge 未能匯入呢個資訊檔。"), InfoBarSeverity.Error);
        }
    }

    private async void ExtensionTemplate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await FileDialogs.SaveFileAsync("winforge-command-palette-extension.json", ".json");
            if (string.IsNullOrWhiteSpace(path)) return;

            await File.WriteAllTextAsync(path, CommandPaletteExtensionService.I.CreateManifestTemplate(), new UTF8Encoding(false));
            ShowExtensionInfo(P("Created a safe extension manifest template.", "已建立安全嘅擴充套件資訊檔範本。"), InfoBarSeverity.Success);
        }
        catch
        {
            ShowExtensionInfo(P("WinForge could not create the template.", "WinForge 未能建立範本。"), InfoBarSeverity.Error);
        }
    }

    private void ExtensionEnabled_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressExtensionChanges || sender is not ToggleSwitch { Tag: string id } toggle) return;
        if (!CommandPaletteExtensionService.I.SetEnabled(id, toggle.IsOn))
        {
            BuildExtensionPacks();
            ShowExtensionInfo(P("WinForge could not update that extension pack.", "WinForge 未能更新呢個擴充套件。"), InfoBarSeverity.Error);
            return;
        }

        BuildExtensionPacks();
        ShowExtensionInfo(toggle.IsOn
            ? P("Extension pack enabled.", "已啟用擴充套件。")
            : P("Extension pack disabled.", "已停用擴充套件。"), InfoBarSeverity.Informational);
    }

    private void ExtensionRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;
        if (!CommandPaletteExtensionService.I.TryRemove(id))
        {
            ShowExtensionInfo(P("WinForge could not remove that extension pack.", "WinForge 未能移除呢個擴充套件。"), InfoBarSeverity.Error);
            return;
        }

        BuildExtensionPacks();
        ShowExtensionInfo(P("Extension pack removed.", "已移除擴充套件。"), InfoBarSeverity.Informational);
    }

    private void ShowExtensionInfo(string message, InfoBarSeverity severity)
    {
        ExtensionPacksInfo.Message = message;
        ExtensionPacksInfo.Severity = severity;
        ExtensionPacksInfo.IsOpen = true;
    }

    private void BuildBookmarks()
    {
        BookmarksPanel.Children.Clear();
        var bookmarks = CommandPaletteService.Bookmarks;
        if (bookmarks.Count == 0)
        {
            BookmarksPanel.Children.Add(new TextBlock
            {
                Text = P("No bookmarks yet. Add one above to make it searchable and Dock-pinnable.", "未有書籤。喺上面加入一個，就可以搜尋同釘選到 Dock。"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        foreach (var bookmark in bookmarks)
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = bookmark.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            var url = new TextBlock { Text = bookmark.Url, FontSize = 11, Opacity = 0.75, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(url, 1);
            row.Children.Add(url);
            var remove = new Button { Content = P("Remove", "移除"), Tag = bookmark };
            remove.Click += BookmarkRemove_Click;
            Grid.SetColumn(remove, 2);
            row.Children.Add(remove);
            BookmarksPanel.Children.Add(row);
        }
    }

    private void BookmarkAdd_Click(object sender, RoutedEventArgs e)
    {
        if (!CommandPaletteService.TryAddBookmark(BookmarkNameBox.Text, BookmarkUrlBox.Text, out var bookmark))
        {
            ShowBookmarkInfo(P("Use a valid HTTP(S) address, for example https://example.com.", "請使用有效 HTTP(S) 網址，例如 https://example.com。"), InfoBarSeverity.Error);
            return;
        }
        BookmarkNameBox.Text = "";
        BookmarkUrlBox.Text = "";
        BuildBookmarks();
        ShowBookmarkInfo(P($"Saved {bookmark.Name}.", $"已儲存 {bookmark.Name}。"), InfoBarSeverity.Success);
    }

    private void BookmarkRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CommandPaletteService.PaletteBookmark bookmark })
        {
            CommandPaletteService.RemoveBookmark(bookmark);
            BuildBookmarks();
            ShowBookmarkInfo(P($"Removed {bookmark.Name}.", $"已移除 {bookmark.Name}。"), InfoBarSeverity.Informational);
        }
    }

    private void ShowBookmarkInfo(string message, InfoBarSeverity severity)
    {
        BookmarkInfo.Message = message;
        BookmarkInfo.Severity = severity;
        BookmarkInfo.IsOpen = true;
    }

    private void BuildRemoteDesktopProfiles()
    {
        RemoteDesktopPanel.Children.Clear();
        var profiles = CommandPaletteService.RemoteDesktopProfiles;
        if (profiles.Count == 0)
        {
            RemoteDesktopPanel.Children.Add(new TextBlock
            {
                Text = P("No Remote Desktop sessions yet. Add a host above; Windows will handle credentials when you connect.", "未有遠端桌面工作階段。喺上面加入主機；連線嗰陣 Windows 會處理登入資料。"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        foreach (var profile in profiles)
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = profile.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            var host = new TextBlock { Text = profile.Host, FontSize = 11, Opacity = 0.75, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(host, 1);
            row.Children.Add(host);
            var remove = new Button { Content = P("Remove", "移除"), Tag = profile };
            remove.Click += RemoteDesktopRemove_Click;
            Grid.SetColumn(remove, 2);
            row.Children.Add(remove);
            RemoteDesktopPanel.Children.Add(row);
        }
    }

    private void RemoteDesktopAdd_Click(object sender, RoutedEventArgs e)
    {
        if (!CommandPaletteService.TryAddRemoteDesktopProfile(RemoteDesktopNameBox.Text, RemoteDesktopHostBox.Text, out var profile))
        {
            ShowRemoteDesktopInfo(P("Use a host or host:port without spaces, for example workstation:3389.", "請使用冇空白字元嘅主機或者主機:連接埠，例如 workstation:3389。"), InfoBarSeverity.Error);
            return;
        }
        RemoteDesktopNameBox.Text = "";
        RemoteDesktopHostBox.Text = "";
        BuildRemoteDesktopProfiles();
        ShowRemoteDesktopInfo(P($"Saved {profile.Name}. Credentials stay with Windows Remote Desktop.", $"已儲存 {profile.Name}。登入資料會留喺 Windows 遠端桌面。"), InfoBarSeverity.Success);
    }

    private void RemoteDesktopRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CommandPaletteService.RemoteDesktopProfile profile })
        {
            CommandPaletteService.RemoveRemoteDesktopProfile(profile);
            BuildRemoteDesktopProfiles();
            ShowRemoteDesktopInfo(P($"Removed {profile.Name}.", $"已移除 {profile.Name}。"), InfoBarSeverity.Informational);
        }
    }

    private void ShowRemoteDesktopInfo(string message, InfoBarSeverity severity)
    {
        RemoteDesktopInfo.Message = message;
        RemoteDesktopInfo.Severity = severity;
        RemoteDesktopInfo.IsOpen = true;
    }

    private void Sync()
    {
        _suppress = true;
        EnableSwitch.IsOn = CommandPaletteService.Enabled;
        MaxBox.Value = CommandPaletteService.MaxResults;
        DockSwitch.IsOn = CommandPaletteDockService.Enabled;
        SelectDockSide();
        _suppress = false;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        bool on = CommandPaletteService.Enabled;
        EnableStatus.Text = on
            ? P($"On — press {CommandPaletteService.HotkeyText} anywhere to open.",
                $"已開 — 喺任何地方按 {CommandPaletteService.HotkeyText} 打開。")
            : P("Off — the global hotkey is not registered.", "已關 — 全域熱鍵未註冊。");
        OpenNowButton.IsEnabled = on;
        DockOpenButton.IsEnabled = on;
        var side = DockSideName(CommandPaletteDockService.Side);
        DockStatus.Text = CommandPaletteDockService.Enabled && on
            ? P($"On — docked at the {side.En} edge. Ctrl+P pins palette results.", $"已開 — 停靠喺{side.Zh}邊。Ctrl+P 可以釘選指令面板結果。")
            : P("Off — enable the palette and Dock to keep the edge launcher visible.", "已關 — 啟用指令面板同 Dock 後，邊緣啟動器先會保持顯示。");
    }

    private void Enable_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        CommandPaletteService.Enabled = EnableSwitch.IsOn;
        CommandPaletteService.Reapply();
        UpdateStatus();
    }

    private void Hotkey_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        if (HotkeyCombo.SelectedItem is string hk && !string.IsNullOrWhiteSpace(hk))
        {
            CommandPaletteService.HotkeyText = hk;
            CommandPaletteService.Reapply();
            UpdateStatus();
        }
    }

    private void Max_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppress) return;
        if (!double.IsNaN(sender.Value)) CommandPaletteService.MaxResults = (int)sender.Value;
    }

    private void Provider_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        if (sender is CheckBox { Tag: CommandPaletteService.Provider p } chk)
            CommandPaletteService.SetProviderEnabled(p, chk.IsChecked == true);
    }

    private void Backdrop_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        if (BackdropCombo.SelectedItem is ComboBoxItem { Tag: CommandPaletteService.CommandPaletteBackdrop backdrop })
        {
            CommandPaletteService.AppearanceBackdrop = backdrop;
            CommandPaletteWindow.RefreshAppearance();
        }
    }

    private async void BackgroundImageBrowse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await FileDialogs.OpenFileAsync(".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp");
            if (string.IsNullOrWhiteSpace(path)) return;
            BackgroundImageBox.Text = path;
            CommandPaletteService.BackgroundImagePath = path;
            CommandPaletteWindow.RefreshAppearance();
        }
        catch { }
    }

    private void BackgroundImageClear_Click(object sender, RoutedEventArgs e)
    {
        BackgroundImageBox.Text = "";
        CommandPaletteService.BackgroundImagePath = "";
        CommandPaletteWindow.RefreshAppearance();
    }

    private void OpenNow_Click(object sender, RoutedEventArgs e)
    {
        try { CommandPaletteWindow.Open(); } catch { }
    }

    private void BuildDockSides()
    {
        _suppress = true;
        DockSideCombo.Items.Clear();
        AddDockSide(CommandPaletteDockSide.Top, "Top", "頂部");
        AddDockSide(CommandPaletteDockSide.Bottom, "Bottom", "底部");
        AddDockSide(CommandPaletteDockSide.Left, "Left", "左邊");
        AddDockSide(CommandPaletteDockSide.Right, "Right", "右邊");
        SelectDockSide();
        _suppress = false;
    }

    private void BuildAppearance()
    {
        _suppress = true;
        BackdropCombo.Items.Clear();
        AddBackdrop(CommandPaletteService.CommandPaletteBackdrop.Solid, "Solid", "實色");
        AddBackdrop(CommandPaletteService.CommandPaletteBackdrop.Mica, "Mica", "雲母");
        AddBackdrop(CommandPaletteService.CommandPaletteBackdrop.Acrylic, "Acrylic", "壓克力");
        for (int i = 0; i < BackdropCombo.Items.Count; i++)
        {
            if (BackdropCombo.Items[i] is ComboBoxItem { Tag: CommandPaletteService.CommandPaletteBackdrop backdrop }
                && backdrop == CommandPaletteService.AppearanceBackdrop)
            {
                BackdropCombo.SelectedIndex = i;
                break;
            }
        }
        BackgroundImageBox.Text = CommandPaletteService.BackgroundImagePath;
        _suppress = false;
    }

    private void AddBackdrop(CommandPaletteService.CommandPaletteBackdrop backdrop, string en, string zh)
        => BackdropCombo.Items.Add(new ComboBoxItem { Content = $"{en} · {zh}", Tag = backdrop });

    private void AddDockSide(CommandPaletteDockSide side, string en, string zh)
        => DockSideCombo.Items.Add(new ComboBoxItem { Content = $"{en} · {zh}", Tag = side });

    private void SelectDockSide()
    {
        for (int i = 0; i < DockSideCombo.Items.Count; i++)
        {
            if (DockSideCombo.Items[i] is ComboBoxItem { Tag: CommandPaletteDockSide side } && side == CommandPaletteDockService.Side)
            {
                DockSideCombo.SelectedIndex = i;
                return;
            }
        }
    }

    private (string En, string Zh) DockSideName(CommandPaletteDockSide side) => side switch
    {
        CommandPaletteDockSide.Top => ("top", "頂部"),
        CommandPaletteDockSide.Left => ("left", "左"),
        CommandPaletteDockSide.Right => ("right", "右"),
        _ => ("bottom", "底部"),
    };

    private void Dock_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        CommandPaletteDockService.Enabled = DockSwitch.IsOn;
        CommandPaletteDockService.Reapply();
        UpdateStatus();
    }

    private void DockSide_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        if (DockSideCombo.SelectedItem is ComboBoxItem { Tag: CommandPaletteDockSide side })
        {
            CommandPaletteDockService.Side = side;
            CommandPaletteDockService.Reapply();
            UpdateStatus();
        }
    }

    private void DockOpen_Click(object sender, RoutedEventArgs e)
    {
        if (!CommandPaletteService.Enabled)
        {
            CommandPaletteService.Enabled = true;
            CommandPaletteService.Reapply();
            _suppress = true;
            EnableSwitch.IsOn = true;
            _suppress = false;
        }
        CommandPaletteDockService.Enabled = true;
        _suppress = true;
        DockSwitch.IsOn = true;
        _suppress = false;
        CommandPaletteDockService.Reapply();
        UpdateStatus();
    }
}
