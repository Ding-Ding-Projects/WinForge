using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 設定：語言、佈景主題、管理員、關於。
/// Settings: language, theme, administrator and about.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private bool _suppress;
    private bool _subscriptionsActive;
    private bool _universalSubscriptionsActive;
    private bool _scheduledSubscriptionsActive;
    private TextBlock? _tonePreview;
    private Slider? _englishToneSlider;
    private Slider? _cantoneseToneSlider;
    private Action? _captureScheduleDraft;
    private const string ScheduleDraftKey = "universal.scheduledSettings.draft.v1";

    private sealed class ScheduleDraft
    {
        public string Id { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public string Label { get; set; } = string.Empty;
        public string Field { get; set; } = "language";
        public string Value { get; set; } = string.Empty;
        public ScheduledSettingSource Source { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string CredentialKey { get; set; } = string.Empty;
        public int Priority { get; set; }
        public bool EveryDay { get; set; } = true;
        public bool UseDates { get; set; }
        public bool AllDay { get; set; } = true;
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public List<DayOfWeek> Weekdays { get; set; } = new();
    }

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += OnActualThemeChanged;
    }

    private void OnLang(object? sender, EventArgs e) => Build();

    private void OnUniversalChanged(object? sender, EventArgs e) => Build();

    private void OnToneChanged(object? sender, EventArgs e)
    {
        if (_englishToneSlider is not null)
            _englishToneSlider.Value = FunnyLevelSettings.I.EnglishLevel;
        if (_cantoneseToneSlider is not null)
            _cantoneseToneSlider.Value = FunnyLevelSettings.I.CantoneseLevel;
        UpdateTonePreview();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        if (IsLoaded) Build();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_subscriptionsActive)
        {
            Loc.I.LanguageChanged += OnLang;
            FunnyLevelSettings.I.Changed += OnToneChanged;
            UniversalSettingsService.Changed += OnUniversalChanged;
            ScheduledSettingsService.Changed += OnScheduledChanged;
            _subscriptionsActive = true;
            _universalSubscriptionsActive = true;
            _scheduledSubscriptionsActive = true;
        }

        Build();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _captureScheduleDraft?.Invoke();
        _captureScheduleDraft = null;
        if (!_subscriptionsActive) return;
        Loc.I.LanguageChanged -= OnLang;
        FunnyLevelSettings.I.Changed -= OnToneChanged;
        if (_universalSubscriptionsActive)
        {
            UniversalSettingsService.Changed -= OnUniversalChanged;
            _universalSubscriptionsActive = false;
        }
        if (_scheduledSubscriptionsActive)
        {
            ScheduledSettingsService.Changed -= OnScheduledChanged;
            _scheduledSubscriptionsActive = false;
        }
        _subscriptionsActive = false;
    }

    private void OnScheduledChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded) return;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (IsLoaded) Build();
        });
    }

    private void Build()
    {
        _captureScheduleDraft?.Invoke();
        _captureScheduleDraft = null;
        _tonePreview = null;
        _englishToneSlider = null;
        _cantoneseToneSlider = null;
        Root.Children.Clear();

        Root.Children.Add(new TextBlock
        {
            Text = Loc.I.Pick("Settings", "設定"),
            Style = (Style)Application.Current.Resources["TitleTextBlockStyle"],
        });

        Root.Children.Add(BuildUniversalCard());
        if (!UniversalSettingsService.SchoolModeEnabled)
            Root.Children.Add(BuildScheduleCard());
        if (!UniversalSettingsService.SchoolModeEnabled)
        {
            Root.Children.Add(BuildLanguageCard());
            Root.Children.Add(BuildToneCard());
            Root.Children.Add(BuildBrandingCard());
            Root.Children.Add(BuildThemeCard());
            Root.Children.Add(BuildBackupCard());
            Root.Children.Add(BuildAdminCard());
            Root.Children.Add(BuildAboutCard());
        }
        else
        {
            Root.Children.Add(Muted(Loc.I.Pick(
                $"{UniversalSettingsService.SchoolModeName} is on. WinForge is using English and has temporarily removed language, funny-level, personal-vocabulary, and dim-sum controls. Unlock it below to restore your saved choices.",
                $"{UniversalSettingsService.SchoolModeName} 已開啟。WinForge 而家用英文，暫時移除語言、搞笑等級、個人詞彙同點心控制。喺下面解鎖就可以還原之前嘅選擇。")));
        }
    }

    private Border BuildUniversalCard()
    {
        bool school = UniversalSettingsService.SchoolModeEnabled;
        var panel = new StackPanel { Spacing = 10 };
        string modeName = UniversalSettingsService.SchoolModeName;
        panel.Children.Add(Heading(
            Loc.I.Pick("Shared experience settings", "共用體驗設定"),
            Loc.I.Pick("These settings apply live to every WinForge window using this profile. The provenance line says when a value comes from the shared settings file.",
                "呢啲設定會即時套用到呢個使用者設定檔入面每個 WinForge 視窗。來源行會講明數值係咪由共用設定檔載入。")));

        var nameBox = new TextBox
        {
            Header = Loc.I.Pick(modeName + " display name", modeName + " 顯示名稱"),
            Text = modeName,
            MaxLength = 64,
            MaxWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = !school,
        };
        AutomationProperties.SetName(nameBox, Loc.I.Pick(modeName + " display name", modeName + " 顯示名稱"));
        ToolTipService.SetToolTip(nameBox, Loc.I.Pick("Rename the mode only; the app identity and data folder do not change.", "只會改模式名稱；app 身份同資料夾唔會變。"));
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = SecondaryTextBrush() };
        var rename = new Button { Content = Loc.I.Pick("Save display name", "儲存顯示名稱"), IsEnabled = !school, MinHeight = 40 };
        rename.Click += (_, _) =>
        {
            try { UniversalSettingsService.SchoolModeName = nameBox.Text; }
            catch (Exception ex) { status.Text = ex.Message; }
        };

        var nameRow = new StackPanel { Spacing = 4, Children = { nameBox, rename } };
        panel.Children.Add(nameRow);
        panel.Children.Add(Muted(Loc.I.Pick(
            $"Source: {(school ? "shared settings file" : "saved value or compiled-in app label")}. This name is only a label; it is not a security boundary.",
                $"來源：{(school ? "共用設定檔" : "已儲存值或內置 app 標籤")}。呢個名只係標籤，唔係安全防線。")));

        if (!school)
        {
            var emoji = new ToggleSwitch
            {
                Header = Loc.I.Pick("Show emojis in dialogs and message boxes", "喺對話框同訊息框顯示 emoji"),
                IsOn = UniversalSettingsService.EmojiDialogsEnabled,
                OnContent = Loc.I.Pick("On", "開"),
                OffContent = Loc.I.Pick("Off", "關"),
            };
            AutomationProperties.SetName(emoji, Loc.I.Pick("Show emojis in dialogs and message boxes", "喺對話框同訊息框顯示 emoji"));
            ToolTipService.SetToolTip(emoji, Loc.I.Pick("Adds a relevant decorative emoji to dialog and message-box copy without changing control labels or facts.", "喺對話框／訊息框文字加相關裝飾 emoji，但唔會改按鈕、標籤或者事實。"));
            emoji.Toggled += (_, _) => UniversalSettingsService.EmojiDialogsEnabled = emoji.IsOn;
            panel.Children.Add(emoji);
            panel.Children.Add(Muted(Loc.I.Pick(
                $"Source: {(SettingsStore.Get("universal.emojiDialogsEnabled", "") is "True" or "False" ? "shared settings file" : "compiled-in value True")}. The switch is persisted and keyboard accessible.",
                $"來源：{(SettingsStore.Get("universal.emojiDialogsEnabled", "") is "True" or "False" ? "共用設定檔" : "內置值 True")}。開關會保存，亦可以用鍵盤操作。")));

            var narrator = new ToggleSwitch
            {
                Header = Loc.I.Pick("Narrate app events (off by default)", "讀出 app 事件（預設關閉）"),
                IsOn = NarratorService.Enabled,
                OnContent = Loc.I.Pick("On", "開"),
                OffContent = Loc.I.Pick("Off", "關"),
            };
            AutomationProperties.SetName(narrator, Loc.I.Pick("Narrate app events", "讀出 app 事件"));
            ToolTipService.SetToolTip(narrator, Loc.I.Pick(
                "Opt in to serialized event narration. It is off by default, debounced, and rate-limited.",
                "選擇開啟事件旁白。預設關閉，會序列化、debounce 同限制頻率。"));
            narrator.Toggled += (_, _) => NarratorService.Enabled = narrator.IsOn;
            panel.Children.Add(narrator);

            var narratorLanguage = new ComboBox
            {
                Header = Loc.I.Pick("Narration language", "旁白語言"),
                MaxWidth = 420,
                HorizontalAlignment = HorizontalAlignment.Left,
                MinWidth = 240,
            };
            narratorLanguage.Items.Add(new ComboBoxItem { Content = Loc.I.Pick("English", "英文"), Tag = NarratorLanguage.English });
            narratorLanguage.Items.Add(new ComboBoxItem { Content = Loc.I.Pick("Cantonese", "粵語"), Tag = NarratorLanguage.Cantonese });
            narratorLanguage.Items.Add(new ComboBoxItem { Content = Loc.I.Pick("Both (English then Cantonese)", "兩種（英文再粵語）"), Tag = NarratorLanguage.Both });
            narratorLanguage.SelectedIndex = (int)NarratorService.Language;
            AutomationProperties.SetName(narratorLanguage, Loc.I.Pick("Narration language", "旁白語言"));
            narratorLanguage.SelectionChanged += (_, _) =>
            {
                if (narratorLanguage.SelectedItem is ComboBoxItem { Tag: NarratorLanguage language })
                    NarratorService.Language = language;
            };
            panel.Children.Add(narratorLanguage);
            panel.Children.Add(Muted(Loc.I.Pick(
                $"Source: enabled state and narration language are stored in the shared settings file. Narration yields to {modeName} and uses the selected language's funny level for tone.",
                $"來源：開關同旁白語言會保存喺共用設定檔。{modeName} 開啟時會停止旁白，語氣會跟所選語言嘅搞笑等級。")));
        }

        var schoolSwitch = new ToggleSwitch
        {
            Header = modeName,
            IsOn = school,
            OnContent = Loc.I.Pick("On", "開"),
            OffContent = Loc.I.Pick("Off", "關"),
        };
        AutomationProperties.SetName(schoolSwitch, modeName);
        ToolTipService.SetToolTip(schoolSwitch, Loc.I.Pick(
            "When on, English is forced and non-English, funny, vocabulary, and dim-sum surfaces are removed until unlocked.",
            "開啟後會強制英文，並移除非英文、搞笑、詞彙同點心介面，直到解鎖。"));

        var pinBox = new PasswordBox
        {
            Header = Loc.I.Pick("Unlock value (4–256 characters)", "解鎖值（4–256 個字元）"),
            PlaceholderText = Loc.I.Pick("Set or enter the local unlock value", "設定或輸入本機解鎖值"),
            MaxWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Left,
            PasswordRevealMode = PasswordRevealMode.Peek,
        };
        AutomationProperties.SetName(pinBox, Loc.I.Pick(modeName + " unlock value", modeName + " 解鎖值"));
        var pinButton = new Button { Content = Loc.I.Pick("Save unlock value", "儲存解鎖值"), MinHeight = 40 };
        pinButton.Click += (_, _) =>
        {
            try
            {
                UniversalSettingsService.SetSchoolUnlock(pinBox.Password);
                pinBox.Password = string.Empty;
                status.Text = Loc.I.Pick("Unlock value saved in the Windows credential vault; it was not written to settings.", "解鎖值已儲存喺 Windows credential vault，冇寫入設定檔。");
            }
            catch (Exception ex) { status.Text = ex.Message; }
        };

        var forgotten = new Button
        {
            Content = Loc.I.Pick("Forgotten your unlock value? Open Support Tickets", "唔記得解鎖值？開啟支援工單"),
            MinHeight = 40,
        };
        AutomationProperties.SetName(forgotten, Loc.I.Pick(
            "Forgotten your unlock value? Open Support Tickets",
            "唔記得解鎖值？開啟支援工單"));
        forgotten.Click += (_, _) => Navigator.GoToModule?.Invoke("module.supporttickets");

        schoolSwitch.Toggled += (_, _) =>
        {
            if (_suppress) return;
            if (schoolSwitch.IsOn)
            {
                UniversalSettingsService.SchoolModeEnabled = true;
                return;
            }
            if (!UniversalSettingsService.VerifySchoolUnlock(pinBox.Password))
            {
                _suppress = true;
                schoolSwitch.IsOn = true;
                _suppress = false;
                status.Text = Loc.I.Pick("That unlock value did not match. The mode remains on; use the Windows credential-vault value or reset the local app data folder.", "解鎖值唔啱。模式會繼續開；請用 Windows credential vault 入面嘅值，或者刪除本機 app data folder 重設。 ");
                return;
            }
            UniversalSettingsService.SchoolModeEnabled = false;
            pinBox.Password = string.Empty;
        };
        panel.Children.Add(schoolSwitch);
        panel.Children.Add(pinBox);
        panel.Children.Add(pinButton);
        panel.Children.Add(forgotten);
        panel.Children.Add(status);
        panel.Children.Add(Muted(Loc.I.Pick(
            "This is a local user-experience lock, not encryption or protection from another person using the machine. Deleting the WinForge LocalAppData folder resets it.",
            "呢個係本機使用體驗鎖，唔係加密，亦唔可以防止其他人用部機。刪除 WinForge LocalAppData 資料夾可以重設。")));
        return Card(panel);
    }

    private Border BuildBackupCard()
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(Heading(
            Loc.I.Pick("Import / export settings", "匯入／匯出設定"),
            Loc.I.Pick("Save WinForge's settings to a file, or load them back.", "將 WinForge 嘅設定存做檔案，或者載返入嚟。")));

        var bar = new InfoBar { IsClosable = true, IsOpen = false };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        var export = new Button { Content = Loc.I.Pick("Export…", "匯出…") };
        export.Click += async (_, _) =>
        {
            try
            {
                var path = await FileDialogs.SaveFileAsync("winforge-settings", ".json");
                if (path is not null)
                {
                    SettingsStore.ExportTo(path);
                    Show(bar, InfoBarSeverity.Success, Loc.I.Pick("Exported.", "已匯出。"), path);
                }
            }
            catch (Exception ex) { Show(bar, InfoBarSeverity.Error, Loc.I.Pick("Export failed", "匯出失敗"), ex.Message); }
        };

        var import = new Button { Content = Loc.I.Pick("Import…", "匯入…") };
        import.Click += async (_, _) =>
        {
            try
            {
                var path = await FileDialogs.OpenFileAsync(".json");
                if (path is not null)
                {
                    int n = SettingsStore.ImportFrom(path);
                    App.ApplyThemeFromSettings();
                    FunnyLevelSettings.I.ReloadFromSettings();
                    Show(bar, InfoBarSeverity.Success,
                        Loc.I.Pick($"Imported {n} setting(s).", $"已匯入 {n} 項設定。"),
                        Loc.I.Pick("Restart WinForge to fully apply.", "重啟 WinForge 完全生效。"));
                }
            }
            catch (Exception ex) { Show(bar, InfoBarSeverity.Error, Loc.I.Pick("Import failed", "匯入失敗"), ex.Message); }
        };

        row.Children.Add(export);
        row.Children.Add(import);
        panel.Children.Add(row);
        panel.Children.Add(bar);
        return Card(panel);
    }

    private Border BuildScheduleCard()
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(Heading(
            Loc.I.Pick("Scheduled settings", "排程設定"),
            Loc.I.Pick(
                "Temporarily override language, theme, density, accent, font, or display name from local data, a validated HTTPS API, or a Home Assistant boolean. Rules use this computer's local time zone and never overwrite the base setting.",
                "可以用本機資料、驗證過嘅 HTTPS API，或者 Home Assistant boolean 暫時改語言、主題、密度、accent、字體或者顯示名稱。規則用呢部機嘅本地時區，永遠唔會覆蓋基本設定。")));

        var timeZone = TimeZoneInfo.Local;
        panel.Children.Add(Muted(Loc.I.Pick(
            $"Time zone: {timeZone.Id}. Date and time values use this local zone; daylight-saving transitions use the operating system's time-zone rules. Equal start/end times mean a 24-hour window; an end earlier than the start crosses midnight.",
            $"時區：{timeZone.Id}。日期同時間會用本地時區；夏令時間跟操作系統規則。開始同結束時間相同代表 24 小時；結束早過開始就代表跨午夜。")));

        var search = new SearchPatternBox
        {
            PlaceholderText = Loc.I.Pick("Search scheduled rules", "搜尋排程規則"),
            AutomationName = Loc.I.Pick("Scheduled settings search", "排程設定搜尋"),
        };
        panel.Children.Add(search);

        var status = Muted(string.Empty);
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        panel.Children.Add(status);

        var current = Muted(string.Empty);
        current.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(current);

        var list = new StackPanel { Spacing = 8 };
        panel.Children.Add(list);

        var editor = new Border
        {
            Padding = new Thickness(12),
            Background = SurfaceBrush(light: 0xFFF0F4F0, dark: 0xFF1A211B),
            BorderBrush = SurfaceBrush(light: 0x330F6B3A, dark: 0x24FFFFFF),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
        };
        var editorPanel = new StackPanel { Spacing = 8 };
        editor.Child = editorPanel;
        panel.Children.Add(editor);

        var labelBox = new TextBox
        {
            Header = Loc.I.Pick("Rule label", "規則名稱"),
            PlaceholderText = Loc.I.Pick("For example: Cantonese after work", "例如：收工後轉粵語"),
            MaxLength = 120,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var propertyBox = new ComboBox
        {
            Header = Loc.I.Pick("Setting field", "設定欄位"),
            MaxWidth = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AddScheduleOption(propertyBox, "Language", "語言", "language");
        AddScheduleOption(propertyBox, "Theme", "主題", "theme");
        AddScheduleOption(propertyBox, "Density", "密度", "density");
        AddScheduleOption(propertyBox, "Accent / seed color", "Accent／seed 顏色", "accent");
        AddScheduleOption(propertyBox, "Font family", "字體", "fontFamily");
        AddScheduleOption(propertyBox, "Font scale", "字體比例", "fontScale");
        AddScheduleOption(propertyBox, "Font weight", "字體粗幼", "fontWeight");
        AddScheduleOption(propertyBox, "Display name", "顯示名稱", "displayName");
        propertyBox.SelectedIndex = 0;

        var valueBox = new TextBox
        {
            Header = Loc.I.Pick("Local value", "本機數值"),
            PlaceholderText = Loc.I.Pick("The value to apply while the rule matches", "規則符合時套用嘅數值"),
            MaxLength = ScheduledSettingsService.MaxValueLength,
        };
        var sourceBox = new ComboBox
        {
            Header = Loc.I.Pick("Value source", "數值來源"),
            MaxWidth = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AddScheduleOption(sourceBox, "Local value", "本機數值", ScheduledSettingSource.Local);
        AddScheduleOption(sourceBox, "Validated HTTPS API", "驗證過嘅 HTTPS API", ScheduledSettingSource.HttpsApi);
        AddScheduleOption(sourceBox, "Home Assistant boolean", "Home Assistant boolean", ScheduledSettingSource.HomeAssistantBoolean);
        sourceBox.SelectedIndex = 0;

        var endpointBox = new TextBox
        {
            Header = Loc.I.Pick("HTTPS API or Home Assistant base URL", "HTTPS API 或 Home Assistant 基本 URL"),
            PlaceholderText = "https://example.invalid/settings",
            MaxLength = 2048,
            TextWrapping = TextWrapping.Wrap,
        };
        var entityBox = new TextBox
        {
            Header = Loc.I.Pick("Home Assistant boolean entity", "Home Assistant boolean entity"),
            PlaceholderText = "input_boolean.work_mode",
            MaxLength = 255,
        };
        var credentialKeyBox = new TextBox
        {
            Header = Loc.I.Pick("Credential key", "憑證 key"),
            PlaceholderText = Loc.I.Pick("A stable local vault key", "本機 vault 穩定 key"),
            MaxLength = 128,
        };
        var tokenBox = new PasswordBox
        {
            Header = Loc.I.Pick("Home Assistant token (stored only in the Windows credential vault)", "Home Assistant token（只會存喺 Windows credential vault）"),
            MaxLength = 4096,
        };

        var priorityBox = new NumberBox
        {
            Header = Loc.I.Pick("Priority", "優先級"),
            Minimum = -1000,
            Maximum = 1000,
            Value = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            MaxWidth = 180,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var everyDay = new CheckBox
        {
            Content = Loc.I.Pick("Every day", "每日"),
            IsChecked = true,
        };
        var useDates = new CheckBox
        {
            Content = Loc.I.Pick("Use an optional start and end date", "使用可選開始同結束日期"),
            IsChecked = false,
        };
        var allDay = new CheckBox
        {
            Content = Loc.I.Pick("All day", "全日"),
            IsChecked = true,
        };
        var startDate = new DatePicker { Header = Loc.I.Pick("Start date", "開始日期"), Date = DateTimeOffset.Now };
        var endDate = new DatePicker { Header = Loc.I.Pick("End date", "結束日期"), Date = DateTimeOffset.Now };
        var startTime = new TimePicker { Header = Loc.I.Pick("Start time", "開始時間"), Time = new TimeSpan(8, 0, 0) };
        var endTime = new TimePicker { Header = Loc.I.Pick("End time", "結束時間"), Time = new TimeSpan(17, 0, 0) };
        var weekdaysPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var weekdayChecks = new Dictionary<DayOfWeek, CheckBox>();
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            var check = new CheckBox
            {
                Content = DayLabel(day),
                IsChecked = false,
                MinWidth = 44,
                MinHeight = 44,
            };
            AutomationProperties.SetName(check, Loc.I.Pick($"Weekday {day}", $"星期{DayLabel(day)}"));
            weekdayChecks[day] = check;
            weekdaysPanel.Children.Add(check);
        }
        var editId = string.Empty;
        var editEnabled = true;

        void UpdateEditorVisibility()
        {
            ScheduledSettingSource source = SelectedScheduleSource(sourceBox);
            bool api = source == ScheduledSettingSource.HttpsApi;
            bool homeAssistant = source == ScheduledSettingSource.HomeAssistantBoolean;
            valueBox.Visibility = api ? Visibility.Collapsed : Visibility.Visible;
            propertyBox.Visibility = api ? Visibility.Collapsed : Visibility.Visible;
            endpointBox.Visibility = source == ScheduledSettingSource.Local ? Visibility.Collapsed : Visibility.Visible;
            entityBox.Visibility = homeAssistant ? Visibility.Visible : Visibility.Collapsed;
            credentialKeyBox.Visibility = homeAssistant ? Visibility.Visible : Visibility.Collapsed;
            tokenBox.Visibility = homeAssistant ? Visibility.Visible : Visibility.Collapsed;
            useDates.IsEnabled = true;
            startDate.IsEnabled = useDates.IsChecked == true;
            endDate.IsEnabled = useDates.IsChecked == true;
            startTime.IsEnabled = allDay.IsChecked != true;
            endTime.IsEnabled = allDay.IsChecked != true;
            bool weekdaysEnabled = everyDay.IsChecked != true;
            weekdaysPanel.IsHitTestVisible = weekdaysEnabled;
            foreach (CheckBox check in weekdayChecks.Values) check.IsEnabled = weekdaysEnabled;
        }

        void ClearEditor(bool discardDraft = true)
        {
            if (discardDraft)
                SettingsStore.Set(ScheduleDraftKey, string.Empty);
            editId = string.Empty;
            editEnabled = true;
            labelBox.Text = string.Empty;
            propertyBox.SelectedIndex = 0;
            valueBox.Text = string.Empty;
            sourceBox.SelectedIndex = 0;
            endpointBox.Text = string.Empty;
            entityBox.Text = string.Empty;
            credentialKeyBox.Text = string.Empty;
            tokenBox.Password = string.Empty;
            priorityBox.Value = 0;
            everyDay.IsChecked = true;
            useDates.IsChecked = false;
            allDay.IsChecked = true;
            startDate.Date = DateTimeOffset.Now;
            endDate.Date = DateTimeOffset.Now;
            startTime.Time = new TimeSpan(8, 0, 0);
            endTime.Time = new TimeSpan(17, 0, 0);
            foreach (CheckBox check in weekdayChecks.Values) check.IsChecked = false;
            UpdateEditorVisibility();
        }

        void LoadEditor(ScheduledSettingRule rule)
        {
            editId = rule.Id;
            editEnabled = rule.Enabled;
            labelBox.Text = rule.Label;
            SelectScheduleOption(propertyBox, rule.Values.Keys.FirstOrDefault() ?? "language");
            valueBox.Text = rule.Values.Values.FirstOrDefault() ?? string.Empty;
            SelectScheduleOption(sourceBox, rule.Source);
            endpointBox.Text = rule.Endpoint;
            entityBox.Text = rule.EntityId;
            credentialKeyBox.Text = rule.CredentialKey;
            tokenBox.Password = string.Empty;
            priorityBox.Value = rule.Priority;
            everyDay.IsChecked = rule.EveryDay;
            useDates.IsChecked = rule.StartDate.HasValue || rule.EndDate.HasValue;
            if (rule.StartDate.HasValue) startDate.Date = new DateTimeOffset(rule.StartDate.Value.ToDateTime(TimeOnly.MinValue));
            if (rule.EndDate.HasValue) endDate.Date = new DateTimeOffset(rule.EndDate.Value.ToDateTime(TimeOnly.MinValue));
            allDay.IsChecked = !rule.StartTime.HasValue;
            if (rule.StartTime.HasValue) startTime.Time = rule.StartTime.Value.ToTimeSpan();
            if (rule.EndTime.HasValue) endTime.Time = rule.EndTime.Value.ToTimeSpan();
            foreach (var item in weekdayChecks) item.Value.IsChecked = rule.Weekdays.Contains(item.Key);
            UpdateEditorVisibility();
            status.Text = Loc.I.Pick($"Editing {rule.Label}.", $"而家編輯緊 {rule.Label}。 ");
        }

        void CaptureDraft()
        {
            try
            {
                var draft = new ScheduleDraft
                {
                    Id = editId,
                    Enabled = editEnabled,
                    Label = labelBox.Text ?? string.Empty,
                    Field = SelectedScheduleField(propertyBox),
                    Value = valueBox.Text ?? string.Empty,
                    Source = SelectedScheduleSource(sourceBox),
                    Endpoint = endpointBox.Text ?? string.Empty,
                    EntityId = entityBox.Text ?? string.Empty,
                    CredentialKey = credentialKeyBox.Text ?? string.Empty,
                    Priority = (int)Math.Round(priorityBox.Value),
                    EveryDay = everyDay.IsChecked == true,
                    UseDates = useDates.IsChecked == true,
                    AllDay = allDay.IsChecked == true,
                    StartDate = useDates.IsChecked == true ? startDate.Date.ToString("O") : null,
                    EndDate = useDates.IsChecked == true ? endDate.Date.ToString("O") : null,
                    StartTime = allDay.IsChecked == true ? null : startTime.Time.ToString(),
                    EndTime = allDay.IsChecked == true ? null : endTime.Time.ToString(),
                    Weekdays = weekdayChecks.Where(item => item.Value.IsChecked == true).Select(item => item.Key).ToList(),
                };
                SettingsStore.Set(ScheduleDraftKey, JsonSerializer.Serialize(draft));
            }
            catch
            {
                // A draft is a recovery convenience; failure never blocks the settings page.
            }
        }

        void RestoreDraft()
        {
            try
            {
                string raw = SettingsStore.Get(ScheduleDraftKey, string.Empty);
                if (string.IsNullOrWhiteSpace(raw)) return;
                ScheduleDraft? draft = JsonSerializer.Deserialize<ScheduleDraft>(raw);
                if (draft is null) return;
                editId = draft.Id;
                editEnabled = draft.Enabled;
                labelBox.Text = draft.Label;
                SelectScheduleOption(propertyBox, draft.Field);
                valueBox.Text = draft.Value;
                SelectScheduleOption(sourceBox, draft.Source);
                endpointBox.Text = draft.Endpoint;
                entityBox.Text = draft.EntityId;
                credentialKeyBox.Text = draft.CredentialKey;
                tokenBox.Password = string.Empty;
                priorityBox.Value = draft.Priority;
                everyDay.IsChecked = draft.EveryDay;
                useDates.IsChecked = draft.UseDates;
                allDay.IsChecked = draft.AllDay;
                if (DateTimeOffset.TryParse(draft.StartDate, out DateTimeOffset draftStartDate)) startDate.Date = draftStartDate;
                if (DateTimeOffset.TryParse(draft.EndDate, out DateTimeOffset draftEndDate)) endDate.Date = draftEndDate;
                if (TimeSpan.TryParse(draft.StartTime, out TimeSpan draftStartTime)) startTime.Time = draftStartTime;
                if (TimeSpan.TryParse(draft.EndTime, out TimeSpan draftEndTime)) endTime.Time = draftEndTime;
                foreach (var item in weekdayChecks) item.Value.IsChecked = draft.Weekdays.Contains(item.Key);
                UpdateEditorVisibility();
                status.Text = Loc.I.Pick(
                    "Restored an unsaved scheduled-rule draft. Save it or choose New rule to discard it.",
                    "已還原未儲存嘅排程規則草稿；可以儲存，或者揀新規則先丟棄。");
            }
            catch
            {
                // Invalid drafts are ignored; the live schedule remains authoritative.
            }
        }

        _captureScheduleDraft = CaptureDraft;

        void RenderRules()
        {
            list.Children.Clear();
            SearchPatternService.MatchResult queryResult = search.Match(string.Empty);
            if (!queryResult.Ok)
            {
                status.Text = search.ValidationError ?? Loc.I.Pick("The schedule search pattern is invalid.", "排程搜尋表達式錯誤。 ");
                return;
            }

            int shown = 0;
            foreach (ScheduledSettingRule rule in ScheduledSettingsService.Rules)
            {
                string haystack = string.Join(" ",
                    rule.Label, rule.Source, rule.Endpoint, rule.EntityId, rule.Values.Keys, rule.Values.Values,
                    rule.Enabled ? "enabled" : "disabled",
                    $"priority {rule.Priority}",
                    rule.StartDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                    rule.EndDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                    rule.StartTime?.ToString("HH:mm") ?? string.Empty,
                    rule.EndTime?.ToString("HH:mm") ?? string.Empty,
                    string.Join(" ", rule.Weekdays),
                    rule.TimeZoneId);
                if (!search.Match(haystack).IsMatch) continue;
                shown++;
                var toggle = new ToggleSwitch { IsOn = rule.Enabled, Header = rule.Label, MinHeight = 44 };
                AutomationProperties.SetName(toggle, Loc.I.Pick($"Enable scheduled rule {rule.Label}", $"啟用排程規則 {rule.Label}"));
                toggle.Toggled += (_, _) =>
                {
                    rule.Enabled = toggle.IsOn;
                    try { ScheduledSettingsService.Upsert(rule); }
                    catch (Exception ex) { status.Text = ex.Message; }
                };
                string source = rule.Source switch
                {
                    ScheduledSettingSource.Local => Loc.I.Pick("local", "本機"),
                    ScheduledSettingSource.HttpsApi => "HTTPS API",
                    _ => "Home Assistant",
                };
                string when = rule.EveryDay
                    ? Loc.I.Pick("every day", "每日")
                    : string.Join(", ", rule.Weekdays.Select(DayLabel));
                if (rule.StartTime.HasValue && rule.EndTime.HasValue)
                    when += $" · {rule.StartTime:HH\\:mm}–{rule.EndTime:HH\\:mm}";
                else
                    when += Loc.I.Pick(" · all day", " · 全日");
                var detail = Muted($"{source} · {when} · priority {rule.Priority}");
                detail.TextWrapping = TextWrapping.Wrap;
                var edit = new Button { Content = Loc.I.Pick("Edit", "編輯"), MinHeight = 40 };
                edit.Click += (_, _) => LoadEditor(rule);
                var remove = new Button { Content = Loc.I.Pick("Remove", "移除"), MinHeight = 40 };
                remove.Click += async (_, _) =>
                {
                    string actionKey = "REMOVE";
                    string recordKey = rule.Id[..Math.Min(8, rule.Id.Length)].ToUpperInvariant();
                    var keyOne = new TextBox { Header = Loc.I.Pick("Key 1", "第一條匙"), PlaceholderText = actionKey, MaxLength = actionKey.Length };
                    var keyTwo = new TextBox { Header = Loc.I.Pick("Key 2", "第二條匙"), PlaceholderText = recordKey, MaxLength = recordKey.Length };
                    var progress = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Visibility = Visibility.Collapsed };
                    var slider = new Slider { Minimum = 0, Maximum = 100, StepFrequency = 1, IsEnabled = false };
                    var content = new StackPanel
                    {
                        Spacing = 10,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = Loc.I.Pick(
                                    $"This removes the scheduled rule “{rule.Label}” and its temporary cached source values. Enter both keys, then move the full slider.",
                                    $"呢個動作會移除排程規則「{rule.Label}」同佢嘅臨時來源快取。輸入兩條匙，再推滿滑桿。"),
                                TextWrapping = TextWrapping.Wrap,
                            },
                            keyOne,
                            keyTwo,
                            slider,
                            progress,
                        },
                    };
                    void UpdateSliderState(object? sender, object? args)
                    {
                        slider.IsEnabled = string.Equals(keyOne.Text, actionKey, StringComparison.Ordinal) &&
                                           string.Equals(keyTwo.Text, recordKey, StringComparison.Ordinal);
                    }
                    keyOne.TextChanged += UpdateSliderState;
                    keyTwo.TextChanged += UpdateSliderState;
                    var dialog = new ContentDialog
                    {
                        XamlRoot = XamlRoot,
                        Title = Loc.I.Pick("Confirm scheduled-rule removal", "確認移除排程規則"),
                        Content = content,
                        PrimaryButtonText = Loc.I.Pick("Remove", "移除"),
                        CloseButtonText = Loc.I.Pick("Emergency exit", "緊急離開"),
                        DefaultButton = ContentDialogButton.Close,
                    };
                    dialog.PrimaryButtonClick += (sender, args) =>
                    {
                        if (!slider.IsEnabled || slider.Value < 100)
                        {
                            args.Cancel = true;
                            status.Text = Loc.I.Pick("Both keys and the full-range slider are required.", "要輸入兩條匙同推滿滑桿。");
                            return;
                        }
                        progress.Visibility = Visibility.Visible;
                        progress.IsIndeterminate = true;
                    };
                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        ScheduledSettingsService.Delete(rule.Id);
                        status.Text = Loc.I.Pick($"Removed {rule.Label}.", $"已移除 {rule.Label}。 ");
                        RenderRules();
                    }
                };
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { edit, remove } };
                list.Children.Add(Card(new StackPanel { Spacing = 4, Children = { toggle, detail, buttons } }));
            }
            if (shown == 0)
                list.Children.Add(Muted(Loc.I.Pick("No scheduled rules match this search.", "冇排程規則符合呢個搜尋。")));
            else
                status.Text = Loc.I.Pick($"Showing {shown} scheduled rule(s).", $"顯示緊 {shown} 條排程規則。 ");
        }

        var save = new Button
        {
            Content = Loc.I.Pick("Save scheduled rule", "儲存排程規則"),
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            MinHeight = 40,
        };
        save.Click += (_, _) =>
        {
            try
            {
                string id = string.IsNullOrWhiteSpace(editId) ? Guid.NewGuid().ToString("D") : editId;
                ScheduledSettingSource source = SelectedScheduleSource(sourceBox);
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                if (source != ScheduledSettingSource.HttpsApi)
                {
                    string field = SelectedScheduleField(propertyBox);
                    values[field] = valueBox.Text ?? string.Empty;
                }
                var rule = new ScheduledSettingRule
                {
                    Id = id,
                    Label = labelBox.Text ?? string.Empty,
                    Enabled = string.IsNullOrWhiteSpace(editId) || editEnabled,
                    Priority = (int)Math.Round(priorityBox.Value),
                    StartDate = useDates.IsChecked == true ? DateOnly.FromDateTime(startDate.Date.DateTime) : null,
                    EndDate = useDates.IsChecked == true ? DateOnly.FromDateTime(endDate.Date.DateTime) : null,
                    StartTime = allDay.IsChecked == true ? null : TimeOnly.FromTimeSpan(startTime.Time),
                    EndTime = allDay.IsChecked == true ? null : TimeOnly.FromTimeSpan(endTime.Time),
                    EveryDay = everyDay.IsChecked == true,
                    Weekdays = weekdayChecks.Where(item => item.Value.IsChecked == true).Select(item => item.Key).ToList(),
                    TimeZoneId = TimeZoneInfo.Local.Id,
                    Source = source,
                    Values = values,
                    Endpoint = endpointBox.Text ?? string.Empty,
                    EntityId = entityBox.Text ?? string.Empty,
                    CredentialKey = string.IsNullOrWhiteSpace(credentialKeyBox.Text) ? id : credentialKeyBox.Text.Trim(),
                };
                ScheduledSettingsService.Upsert(rule);
                if (source == ScheduledSettingSource.HomeAssistantBoolean && !string.IsNullOrWhiteSpace(tokenBox.Password))
                    ScheduledSettingsService.SetHomeAssistantToken(rule.CredentialKey, tokenBox.Password);
                status.Text = Loc.I.Pick("Scheduled rule saved. The base setting was not overwritten.", "排程規則已儲存，基本設定冇被覆蓋。 ");
                ClearEditor();
                RenderRules();
            }
            catch (Exception ex) { status.Text = ex.Message; }
        };
        var clear = new Button { Content = Loc.I.Pick("New rule", "新規則"), MinHeight = 40 };
        clear.Click += (_, _) => ClearEditor();
        var refresh = new Button { Content = Loc.I.Pick("Refresh external sources", "重新整理外部來源"), MinHeight = 40 };
        refresh.Click += async (_, _) =>
        {
            try
            {
                ScheduledSettingsRefreshReport report = await ScheduledSettingsService.RefreshExternalSourcesAsync();
                status.Text = report.Failures.Count == 0
                    ? Loc.I.Pick($"Refreshed {report.RefreshedRules} external rule(s).", $"已重新整理 {report.RefreshedRules} 條外部規則。 ")
                    : Loc.I.Pick($"Refreshed {report.RefreshedRules}; {report.Failures.Count} source(s) failed safely and kept their last valid value.", $"已重新整理 {report.RefreshedRules} 條；{report.Failures.Count} 個來源安全失敗，保留最後有效值。 ");
                RenderRules();
            }
            catch (Exception ex) { status.Text = ex.Message; }
        };

        sourceBox.SelectionChanged += (_, _) => UpdateEditorVisibility();
        everyDay.Checked += (_, _) => UpdateEditorVisibility();
        everyDay.Unchecked += (_, _) => UpdateEditorVisibility();
        useDates.Checked += (_, _) => UpdateEditorVisibility();
        useDates.Unchecked += (_, _) => UpdateEditorVisibility();
        allDay.Checked += (_, _) => UpdateEditorVisibility();
        allDay.Unchecked += (_, _) => UpdateEditorVisibility();

        editorPanel.Children.Add(Heading(
            Loc.I.Pick("Create or edit a rule", "建立或者編輯規則"),
            Loc.I.Pick("Plain-text search is the default; use the adjacent regex builder to search rule labels, sources, fields, and values.",
                "純文字搜尋係預設；用旁邊嘅 regex builder 可以搜尋規則名稱、來源、欄位同數值。")));
        editorPanel.Children.Add(labelBox);
        editorPanel.Children.Add(sourceBox);
        editorPanel.Children.Add(propertyBox);
        editorPanel.Children.Add(valueBox);
        editorPanel.Children.Add(endpointBox);
        editorPanel.Children.Add(entityBox);
        editorPanel.Children.Add(credentialKeyBox);
        editorPanel.Children.Add(tokenBox);
        editorPanel.Children.Add(priorityBox);
        editorPanel.Children.Add(useDates);
        editorPanel.Children.Add(new StackPanel { Spacing = 8, Children = { startDate, endDate } });
        editorPanel.Children.Add(allDay);
        editorPanel.Children.Add(new StackPanel { Spacing = 8, Children = { startTime, endTime } });
        editorPanel.Children.Add(everyDay);
        editorPanel.Children.Add(Muted(Loc.I.Pick("If Every day is off, select explicit weekdays. The selected window's start date controls weekday matching across midnight.", "如果關閉每日，請揀指定星期。跨午夜時會用時間窗開始嗰日判斷星期。")));
        editorPanel.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled,
            Content = weekdaysPanel,
        });
        editorPanel.Children.Add(new StackPanel { Spacing = 8, Children = { save, clear, refresh } });
        editorPanel.Children.Add(Muted(Loc.I.Pick(
            "External data is validated, bounded, and never persisted as a base setting. API redirects, embedded credentials, unknown fields, oversized responses, offline failures, and Home Assistant off states fail safe.",
            "外部資料會驗證同限制大小，永遠唔會存成基本設定。API redirect、URL 入面嘅憑證、未知欄位、過大回應、離線失敗同 Home Assistant 關閉狀態都會安全處理。")));

        search.PatternChanged += (_, _) => RenderRules();
        UpdateEditorVisibility();
        ClearEditor(discardDraft: false);
        RestoreDraft();
        var resolution = ScheduledSettingsService.Resolve();
        current.Text = resolution.RuleId is null
            ? Loc.I.Pick("Effective scheduled override: none. Base settings are active.", "目前有效排程覆蓋：冇。基本設定生效緊。")
            : resolution.PendingExternal
                ? Loc.I.Pick($"Effective scheduled override: {resolution.Label}; waiting for a valid external value, so the base setting remains active.", $"目前有效排程覆蓋：{resolution.Label}；等緊有效外部數值，所以基本設定繼續生效。")
                : Loc.I.Pick($"Effective scheduled override: {resolution.Label}; {resolution.Values.Count} setting field(s) are active temporarily.", $"目前有效排程覆蓋：{resolution.Label}；暫時套用緊 {resolution.Values.Count} 個設定欄位。 ");
        RenderRules();
        return Card(panel);
    }

    private static void AddScheduleOption(ComboBox box, string english, string cantonese, object tag)
        => box.Items.Add(new ComboBoxItem { Content = Loc.I.Pick(english, cantonese), Tag = tag });

    private static void SelectScheduleOption(ComboBox box, object tag)
    {
        for (int index = 0; index < box.Items.Count; index++)
        {
            if (box.Items[index] is ComboBoxItem item && Equals(item.Tag, tag))
            {
                box.SelectedIndex = index;
                return;
            }
        }
        box.SelectedIndex = box.Items.Count > 0 ? 0 : -1;
    }

    private static string SelectedScheduleField(ComboBox box) =>
        (box.SelectedItem as ComboBoxItem)?.Tag as string ?? "language";

    private static ScheduledSettingSource SelectedScheduleSource(ComboBox box) =>
        (box.SelectedItem as ComboBoxItem)?.Tag is ScheduledSettingSource source
            ? source : ScheduledSettingSource.Local;

    private static string DayLabel(DayOfWeek day) => day switch
    {
        DayOfWeek.Sunday => Loc.I.Pick("Sun", "日"),
        DayOfWeek.Monday => Loc.I.Pick("Mon", "一"),
        DayOfWeek.Tuesday => Loc.I.Pick("Tue", "二"),
        DayOfWeek.Wednesday => Loc.I.Pick("Wed", "三"),
        DayOfWeek.Thursday => Loc.I.Pick("Thu", "四"),
        DayOfWeek.Friday => Loc.I.Pick("Fri", "五"),
        _ => Loc.I.Pick("Sat", "六"),
    };

    private static void Show(InfoBar bar, InfoBarSeverity sev, string title, string msg)
    {
        bar.Severity = sev; bar.Title = title; bar.Message = msg; bar.IsOpen = true;
    }

    private Border BuildLanguageCard()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Heading(
            Loc.I.Pick("Language", "語言"),
            Loc.I.Pick("Show both languages, Cantonese only, or English only.",
                "顯示雙語、只顯示粵語，或者只顯示英文。")));

        _suppress = true;
        var radios = new RadioButtons();
        radios.Items.Add(Loc.I.Pick("Bilingual (English + Cantonese)", "雙語（英文 + 粵語）"));
        radios.Items.Add(Loc.I.Pick("Cantonese only", "只顯示粵語"));
        radios.Items.Add(Loc.I.Pick("English only", "English only"));
        radios.SelectedIndex = Loc.I.Language switch
        {
            AppLanguage.Cantonese => 1,
            AppLanguage.English => 2,
            _ => 0,
        };
        radios.SelectionChanged += (_, _) =>
        {
            if (_suppress) return;
            Loc.I.Language = radios.SelectedIndex switch
            {
                1 => AppLanguage.Cantonese,
                2 => AppLanguage.English,
                _ => AppLanguage.Bilingual,
            };
        };
        _suppress = false;
        panel.Children.Add(radios);
        return Card(panel);
    }

    private Border BuildToneCard()
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(Heading(
            Loc.I.Pick("Funny level (tone)", "搞笑等級（語氣）"),
            Loc.I.Pick(
                "Choose English and Cantonese playfulness independently. Level 1 is fully serious; level 5 is the most playful.",
                "英文同粵語可以分開揀玩味程度。第 1 級完全正經；第 5 級最玩得。")));

        panel.Children.Add(BuildToneLevelControl(isEnglish: true));
        panel.Children.Add(BuildToneLevelControl(isEnglish: false));

        var previewPanel = new StackPanel { Spacing = 4 };
        previewPanel.Children.Add(new TextBlock
        {
            Text = Loc.I.Pick("Live non-safety preview", "非安全訊息即時預覽"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        _tonePreview = Muted(string.Empty);
        _tonePreview.FontSize = 13;
        AutomationProperties.SetName(_tonePreview, Loc.I.Pick("Funny-level live preview", "搞笑等級即時預覽"));
        AutomationProperties.SetLiveSetting(_tonePreview, AutomationLiveSetting.Polite);
        previewPanel.Children.Add(_tonePreview);

        var previewBorder = new Border
        {
            Padding = new Thickness(12),
            Background = SurfaceBrush(light: 0xFFF0F4F0, dark: 0xFF1A211B),
            CornerRadius = new CornerRadius(6),
            Child = previewPanel,
        };
        panel.Children.Add(previewBorder);
        panel.Children.Add(Muted(Loc.I.Pick(
            "Only explicitly authored, non-safety copy changes. Errors, security, destructive actions, and accessibility wording stay clear and exact.",
            "只會改明確寫好嘅非安全訊息；錯誤、安全、破壞性操作同無障礙文字永遠保持清楚準確。")));

        UpdateTonePreview();
        return Card(panel);
    }

    private StackPanel BuildToneLevelControl(bool isEnglish)
    {
        var settings = FunnyLevelSettings.I;
        var current = isEnglish ? settings.EnglishLevel : settings.CantoneseLevel;
        var language = isEnglish
            ? Loc.I.Pick("English", "英文")
            : Loc.I.Pick("Cantonese", "粵語");
        var value = Muted(LevelValueText(isEnglish, current));
        var slider = new Slider
        {
            Minimum = FunnyLevelSettings.MinimumLevel,
            Maximum = FunnyLevelSettings.MaximumLevel,
            Value = current,
            StepFrequency = 1,
            SnapsTo = SliderSnapsTo.StepValues,
            IsThumbToolTipEnabled = true,
            MinHeight = 44,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(slider, isEnglish
            ? Loc.I.Pick("English funny level, 1 serious to 5 most playful", "英文搞笑等級，由第 1 級正經到第 5 級最玩得")
            : Loc.I.Pick("Cantonese funny level, 1 serious to 5 most playful", "粵語搞笑等級，由第 1 級正經到第 5 級最玩得"));
        AutomationProperties.SetHelpText(slider, Loc.I.Pick(
            "Use Left and Right Arrow to choose an exact level from 1 through 5.",
            "用向左同向右方向鍵揀 1 至 5 嘅準確等級。"));
        ToolTipService.SetToolTip(slider, Loc.I.Pick(
            "1 = fully serious · 5 = most playful",
            "1 = 完全正經 · 5 = 最玩得"));

        if (isEnglish) _englishToneSlider = slider;
        else _cantoneseToneSlider = slider;

        slider.ValueChanged += (_, args) =>
        {
            var level = (int)Math.Round(args.NewValue, MidpointRounding.AwayFromZero);
            if (isEnglish) settings.EnglishLevel = level;
            else settings.CantoneseLevel = level;
            value.Text = LevelValueText(isEnglish, level);
        };

        return new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = language,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                },
                slider,
                value,
                Muted(Loc.I.Pick("1 = fully serious · 5 = most playful", "1 = 完全正經 · 5 = 最玩得")),
            },
        };
    }

    private static string LevelValueText(bool isEnglish, int level) => isEnglish
        ? Loc.I.Pick($"English funny level: {level} of 5.", $"英文搞笑等級：5 級入面第 {level} 級。")
        : Loc.I.Pick($"Cantonese funny level: {level} of 5.", $"粵語搞笑等級：5 級入面第 {level} 級。");

    private void UpdateTonePreview()
    {
        if (_tonePreview is null) return;
        _tonePreview.Text = FunnyLevelSettings.I.Pick(PlayfulCopy.DashboardHero, Loc.I.Language);
    }

    private Border BuildBrandingCard()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Heading(
            Loc.I.Pick("App name (branding)", "應用程式名稱（品牌）"),
            Loc.I.Pick("Rename the app to your own name. Shown in the title bar and dashboard; your data folder and internal IDs stay unchanged.",
                "將 app 改成你自己嘅名。會喺標題列同概覽顯示；資料夾同內部識別碼維持不變。")));

        var enBox = new TextBox
        {
            Header = Loc.I.Pick("Name (English)", "名稱（英文）"),
            Text = BrandingService.NameEn,
            PlaceholderText = BrandingService.DefaultEn,
            MaxWidth = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var zhBox = new TextBox
        {
            Header = Loc.I.Pick("Name (Chinese)", "名稱（中文）"),
            Text = BrandingService.NameZh,
            PlaceholderText = BrandingService.DefaultZh,
            MaxWidth = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var status = new TextBlock { FontSize = 12, Foreground = SecondaryTextBrush() };

        var apply = new Button { Content = Loc.I.Pick("Apply name", "套用名稱"), Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        apply.Click += (_, _) =>
        {
            BrandingService.Set(enBox.Text, zhBox.Text);
            enBox.Text = BrandingService.NameEn;
            zhBox.Text = BrandingService.NameZh;
            status.Text = Loc.I.Pick($"Applied — now \"{BrandingService.NameEn} · {BrandingService.NameZh}\".",
                $"已套用 — 而家係「{BrandingService.NameEn} · {BrandingService.NameZh}」。");
        };
        var reset = new Button { Content = Loc.I.Pick("Reset to WinForge", "還原做 WinForge") };
        reset.Click += (_, _) =>
        {
            BrandingService.Reset();
            enBox.Text = BrandingService.NameEn;
            zhBox.Text = BrandingService.NameZh;
            status.Text = Loc.I.Pick("Reset to the default name.", "已還原做預設名稱。");
        };

        panel.Children.Add(enBox);
        panel.Children.Add(zhBox);
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { apply, reset } });
        panel.Children.Add(status);
        return Card(panel);
    }

    private Border BuildThemeCard()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Heading(
            Loc.I.Pick("App theme", "應用程式主題"),
            Loc.I.Pick("Light, dark or follow Windows.", "淺色、深色或者跟 Windows。")));

        var current = SettingsStore.Get("theme", "Default");
        var radios = new RadioButtons();
        radios.Items.Add(Loc.I.Pick("Use system setting", "跟系統設定"));
        radios.Items.Add(Loc.I.Pick("Light", "淺色"));
        radios.Items.Add(Loc.I.Pick("Dark", "深色"));
        radios.SelectedIndex = current switch { "Light" => 1, "Dark" => 2, _ => 0 };
        radios.SelectionChanged += (_, _) =>
        {
            var (key, theme) = radios.SelectedIndex switch
            {
                1 => ("Light", ElementTheme.Light),
                2 => ("Dark", ElementTheme.Dark),
                _ => ("Default", ElementTheme.Default),
            };
            SettingsStore.Set("theme", key);
            App.ApplyThemeFromSettings();
        };
        panel.Children.Add(radios);
        return Card(panel);
    }

    private Border BuildAdminCard()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Heading(
            Loc.I.Pick("Administrator rights", "管理員權限"),
            Loc.I.Pick("Needed for system-wide tweaks (HKLM, services, power).",
                "全系統調校需要（HKLM、服務、電源）。")));

        if (AdminHelper.IsElevated)
        {
            panel.Children.Add(new TextBlock
            {
                Text = Loc.I.Pick("✓ Running as administrator.", "✓ 正以管理員身分運行。"),
                Foreground = SurfaceBrush(light: 0xFF0F6B3A, dark: 0xFF54E07E),
            });
        }
        else
        {
            var b = new Button { Content = Loc.I.Pick("Relaunch as administrator", "以管理員身分重新啟動") };
            b.Click += (_, _) =>
            {
                if (AdminHelper.RelaunchElevated())
                    Application.Current.Exit();
            };
            panel.Children.Add(b);
        }
        return Card(panel);
    }

    private Border BuildAboutCard()
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(Heading("WinForge · 視窗調校", null));
        panel.Children.Add(Muted(Loc.I.Pick(
            $"{FeatureCountService.FullFeatureCount} bilingual features for Windows 11 ({FeatureCountService.ModuleCount} modules + {FeatureCountService.TweakFeatureCount} tweaks and ops).",
            $"{FeatureCountService.FullFeatureCount} 項 Windows 11 雙語功能（{FeatureCountService.ModuleCount} 個模組 + {FeatureCountService.TweakFeatureCount} 項調校／操作）。")));
        panel.Children.Add(Muted("Version 1.0.0"));
        panel.Children.Add(Muted(Loc.I.Pick(
            "Always review what a tweak does before applying it.",
            "套用之前，請睇清楚每項調校做乜。")));
        return Card(panel);
    }

    // ---- small builders ----
    private StackPanel Heading(string title, string? subtitle)
    {
        var p = new StackPanel { Spacing = 1 };
        p.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 15 });
        if (!string.IsNullOrEmpty(subtitle))
            p.Children.Add(Muted(subtitle));
        return p;
    }

    private TextBlock Muted(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        FontSize = 12,
        Foreground = SecondaryTextBrush(),
    };

    private Border Card(UIElement content) => new()
    {
        Padding = new Thickness(16, 14, 16, 14),
        Background = SurfaceBrush(light: 0xFFF7F9F7, dark: 0xFF131814),
        BorderBrush = SurfaceBrush(light: 0x330F6B3A, dark: 0x24FFFFFF),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Child = content,
    };

    private SolidColorBrush SurfaceBrush(uint light, uint dark)
    {
        var argb = ActualTheme == ElementTheme.Light ? light : dark;
        return new SolidColorBrush(Windows.UI.Color.FromArgb(
            (byte)(argb >> 24),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb));
    }

    private SolidColorBrush SecondaryTextBrush() =>
        SurfaceBrush(light: 0xFF3D5442, dark: 0xFFBFCBBF);
}
