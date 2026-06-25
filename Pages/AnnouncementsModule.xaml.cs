using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 喇叭語音廣播 · Speaker PA Announcements — type a message (or hit a preset) and have it spoken
/// through the PC speakers as a queued public-address announcement, with an optional two-tone chime.
/// Pick a voice, set volume &amp; rate, toggle the chime / mute, and fire normal or urgent (queue-jumping)
/// announcements. Built on <see cref="AnnouncementService"/> so the reactor PA / alarms can call the
/// same engine from anywhere. Bilingual. 100% in-app (System.Speech SAPI), no external assets.
/// </summary>
public sealed partial class AnnouncementsModule : Page
{
    private bool _loading;

    public AnnouncementsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        AnnouncementService.I.StateChanged += OnStateChanged;
        Loaded += (_, _) =>
        {
            LoadSettings();
            LoadVoices();
            Render();
            BuildPresets();
            SyncStatus();
        };
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            AnnouncementService.I.StateChanged -= OnStateChanged;
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Render();
        BuildPresets();
        LoadVoices(); // re-evaluate Cantonese voice availability note
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess) SyncStatus();
        else DispatcherQueue.TryEnqueue(SyncStatus);
    }

    private void Render()
    {
        HeaderTitle.Text = "PA Announcements · 喇叭語音廣播";
        HeaderBlurb.Text = P(
            "Speak public-address announcements through the PC speakers. Announcements queue so they never overlap; urgent ones jump the queue. A short two-tone chime can play first. This same engine can be called from anywhere in the app (e.g. by the reactor PA or alarms).",
            "用電腦喇叭發出語音廣播。廣播會排隊，唔會疊聲；緊急廣播會插隊。可以喺講之前先播一段雙音叮咚。呢個引擎可以喺 app 任何地方叫用（例如反應堆廣播或警報）。");

        SettingsHeader.Text = P("Voice & output", "語音與輸出");
        VoiceLabel.Text = P("Voice", "語音");
        VolumeLabel.Text = P("Volume (0 … 100)", "音量（0 … 100）");
        RateLabel.Text = P("Speed (-10 … +10)", "速度（-10 … +10）");
        ChimeToggle.Header = P("Play chime before announcement", "廣播前播叮咚");
        ChimeToggle.OnContent = P("On", "開");
        ChimeToggle.OffContent = P("Off", "關");
        MuteToggle.Header = P("Mute announcements", "靜音廣播");
        MuteToggle.OnContent = P("Muted", "靜音");
        MuteToggle.OffContent = P("Audible", "有聲");
        ChimeTestText.Text = P("Test chime", "試播叮咚");

        TestHeader.Text = P("Test announcement", "測試廣播");
        TextLabel.Text = P("Announcement text", "廣播文字");
        TextBox.PlaceholderText = P("Type the announcement to speak…", "輸入要廣播嘅文字…");
        UrgentCheck.Content = P("Urgent (jump the queue)", "緊急（插隊）");
        BothLangCheck.Content = P("Speak both languages", "讀出兩種語言");
        AnnounceText.Text = P("Announce", "廣播");
        StopText.Text = P("Stop / clear queue", "停止／清空隊列");

        PresetHeader.Text = P("Preset announcements", "預設廣播");
        PresetBlurb.Text = P("Tap a preset to hear a public-address style announcement.",
            "撳一個預設，聽吓廣播式語音。");
    }

    // ===================== Settings =====================

    private void LoadSettings()
    {
        _loading = true;
        var svc = AnnouncementService.I;
        VolumeSlider.Value = svc.Volume;
        RateSlider.Value = svc.Rate;
        ChimeToggle.IsOn = svc.ChimeEnabled;
        MuteToggle.IsOn = svc.Muted;
        _loading = false;
    }

    private void LoadVoices()
    {
        _loading = true;
        var svc = AnnouncementService.I;
        var voices = svc.GetVoices();

        VoiceCombo.Items.Clear();
        // First entry: automatic selection per language.
        VoiceCombo.Items.Add(new ComboBoxItem
        {
            Content = P("Automatic (match app language)", "自動（跟介面語言）"),
            Tag = "",
        });
        foreach (var v in voices)
            VoiceCombo.Items.Add(new ComboBoxItem { Content = v.Display, Tag = v.Name });

        // Restore the saved choice.
        var saved = svc.VoiceName;
        var match = VoiceCombo.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(i => string.Equals((string?)i.Tag, saved, StringComparison.OrdinalIgnoreCase));
        VoiceCombo.SelectedItem = match ?? VoiceCombo.Items[0];

        // Availability note (graceful — no install button).
        var (ok, en, zh) = svc.DescribeVoiceAvailability();
        if (ok)
        {
            VoiceBar.IsOpen = false;
        }
        else
        {
            VoiceBar.Severity = voices.Count == 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Informational;
            VoiceBar.Title = P("Voice note", "語音提示");
            VoiceBar.Message = P(en, zh);
            VoiceBar.IsOpen = true;
        }
        _loading = false;
    }

    private void VoiceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var tag = (VoiceCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        AnnouncementService.I.VoiceName = tag;
    }

    private void VolumeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_loading) return;
        AnnouncementService.I.Volume = (int)VolumeSlider.Value;
    }

    private void RateSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_loading) return;
        AnnouncementService.I.Rate = (int)RateSlider.Value;
    }

    private void ChimeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AnnouncementService.I.ChimeEnabled = ChimeToggle.IsOn;
    }

    private void MuteToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AnnouncementService.I.Muted = MuteToggle.IsOn;
        SyncStatus();
    }

    private async void ChimeTest_Click(object sender, RoutedEventArgs e)
    {
        await AnnouncementService.I.PreviewChimeAsync();
    }

    // ===================== Test announcement =====================

    private void Announce_Click(object sender, RoutedEventArgs e)
    {
        var text = TextBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowResult(InfoBarSeverity.Warning, P("Nothing to announce", "冇文字可廣播"),
                P("Type some text first.", "請先輸入文字。"));
            return;
        }
        ResultBar.IsOpen = false;

        var priority = UrgentCheck.IsChecked == true
            ? AnnouncementPriority.Urgent
            : AnnouncementPriority.Normal;

        // The test box is a single, already-localized string the user typed. If "both languages"
        // is ticked, speak it twice (the same text) — but typically the user types one language.
        if (BothLangCheck.IsChecked == true)
            AnnouncementService.I.AnnounceBoth(text, text, priority);
        else
            AnnouncementService.I.AnnounceRaw(text, priority);

        SyncStatus();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        AnnouncementService.I.StopAll();
        SyncStatus();
    }

    private void SyncStatus()
    {
        var svc = AnnouncementService.I;
        int q = svc.QueueLength;
        if (svc.Muted)
            StatusText.Text = P("Muted", "已靜音");
        else if (svc.IsSpeaking)
            StatusText.Text = P($"Speaking… ({q} queued)", $"廣播緊…（隊列 {q}）");
        else if (q > 0)
            StatusText.Text = P($"{q} queued", $"隊列 {q}");
        else
            StatusText.Text = P("Idle", "閒置");
    }

    // ===================== Presets =====================

    private void BuildPresets()
    {
        PresetPanel.Children.Clear();
        PresetPanel2.Children.Clear();

        // Row 1: PA-style demos.
        PresetPanel.Children.Add(MakePreset(
            P("Attention all personnel", "全體人員請注意"),
            () => AnnouncementService.I.Announce(
                "Attention all personnel. This is a test of the public address system.",
                "全體人員請注意。呢個係廣播系統測試。")));

        PresetPanel.Children.Add(MakePreset(
            P("Reactor PA demo", "反應堆廣播示範"),
            () => AnnouncementService.I.Announce(
                "Reactor status nominal. All systems operating within normal parameters.",
                "反應堆狀態正常。所有系統喺正常範圍內運作。")));

        PresetPanel.Children.Add(MakePreset(
            P("Countdown 5…1", "倒數 5…1"),
            () =>
            {
                AnnouncementService.I.Announce("Five.", "五。");
                AnnouncementService.I.Announce("Four.", "四。", chime: false);
                AnnouncementService.I.Announce("Three.", "三。", chime: false);
                AnnouncementService.I.Announce("Two.", "二。", chime: false);
                AnnouncementService.I.Announce("One.", "一。", chime: false);
            }));

        // Row 2: urgent / alarm demos.
        PresetPanel2.Children.Add(MakePreset(
            P("Evacuate (urgent)", "撤離（緊急）"),
            () => AnnouncementService.I.Announce(
                "Warning. Please evacuate the area immediately and proceed to the nearest exit.",
                "警告。請即刻離開呢個範圍，前往最近嘅出口。",
                AnnouncementPriority.Urgent)));

        PresetPanel2.Children.Add(MakePreset(
            P("Alarm (urgent)", "警報（緊急）"),
            () => AnnouncementService.I.Announce(
                "Alarm. Critical alert. Personnel respond immediately.",
                "警報。嚴重警示。人員請即刻回應。",
                AnnouncementPriority.Urgent)));

        PresetPanel2.Children.Add(MakePreset(
            P("All clear", "解除警報"),
            () => AnnouncementService.I.Announce(
                "All clear. Normal operations may resume.",
                "解除警報。可以回復正常運作。")));
    }

    private Button MakePreset(string label, Action onClick)
    {
        var btn = new Button { Content = label };
        btn.Click += (_, _) =>
        {
            ResultBar.IsOpen = false;
            try { onClick(); } catch { }
            SyncStatus();
        };
        return btn;
    }

    private void ShowResult(InfoBarSeverity severity, string title, string message)
    {
        ResultBar.Severity = severity;
        ResultBar.Title = title;
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }
}
