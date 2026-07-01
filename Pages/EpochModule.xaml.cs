using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 紀元轉換器 · Epoch (Unix timestamp) converter — live "now" clock, epoch→human and
/// human→epoch conversion with a UTC/local toggle, relative-time phrasing and one-tap copy.
/// Pure managed; every parse is guarded and surfaces a bilingual status/error instead of throwing.
/// </summary>
public sealed partial class EpochModule : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public EpochModule()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => UpdateNow();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) =>
        {
            Render();
            UpdateNow();
            RecomputeHumanToEpoch();
            _timer.Start();
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = P("Epoch Converter · 紀元轉換器", "紀元轉換器 · Epoch Converter");
        HeaderBlurb.Text = P("Convert between Unix timestamps and human date-times, in both local and UTC. Live clock, relative phrasing, and one-tap copy.",
            "喺 Unix 時間戳同人類日期時間之間轉換，本地同 UTC 都得。有實時時鐘、相對時間同一撳複製。");

        NowTitle.Text = P("Right now", "而家");
        NowSecLabel.Text = P("Unix seconds", "Unix 秒");
        NowMsLabel.Text = P("Unix milliseconds", "Unix 毫秒");
        NowLocalLabel.Text = P("Local time", "本地時間");
        NowUtcLabel.Text = P("UTC (ISO 8601)", "UTC（ISO 8601）");
        CopyNowBtn.Content = P("Copy now (seconds)", "複製而家（秒）");

        E2HTitle.Text = P("Epoch → human", "紀元 → 人類時間");
        SetComboItems();
        ConvertBtn.Content = P("Convert", "轉換");
        RLocalLabel.Text = P("Local", "本地");
        RUtcLabel.Text = P("UTC", "UTC");
        RIsoLabel.Text = P("ISO 8601", "ISO 8601");
        RDowLabel.Text = P("Day of week", "星期幾");
        RRelLabel.Text = P("Relative", "相對");

        H2ETitle.Text = P("Human → epoch", "人類時間 → 紀元");
        UtcToggleTitle.Text = P("Interpret picked time as UTC", "當揀嘅時間係 UTC");
        UtcToggleHint.Text = P("Off = your local time zone", "關 = 你嘅本地時區");
        OutSecLabel.Text = P("Unix seconds", "Unix 秒");
        OutMsLabel.Text = P("Unix milliseconds", "Unix 毫秒");
        CopySecBtn.Content = P("Copy", "複製");
        CopyMsBtn.Content = P("Copy", "複製");
    }

    private void SetComboItems()
    {
        int sel = EpochUnit.SelectedIndex < 0 ? 0 : EpochUnit.SelectedIndex;
        EpochUnit.Items.Clear();
        EpochUnit.Items.Add(P("Seconds", "秒"));
        EpochUnit.Items.Add(P("Milliseconds", "毫秒"));
        EpochUnit.SelectedIndex = sel;
    }

    // ---- Live now ----
    private void UpdateNow()
    {
        var now = DateTimeOffset.Now;
        NowSecValue.Text = EpochService.NowSeconds.ToString(CultureInfo.InvariantCulture);
        NowMsValue.Text = EpochService.NowMilliseconds.ToString(CultureInfo.InvariantCulture);
        NowLocalValue.Text = now.LocalDateTime.ToString("F", CultureInfo.CurrentCulture);
        NowUtcValue.Text = now.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    private void CopyNow_Click(object sender, RoutedEventArgs e)
        => Copy(EpochService.NowSeconds.ToString(CultureInfo.InvariantCulture));

    // ---- Epoch -> human ----
    private void EpochInput_Changed(object sender, TextChangedEventArgs e) => Convert();
    private void EpochUnit_Changed(object sender, SelectionChangedEventArgs e) => Convert();
    private void Convert_Click(object sender, RoutedEventArgs e) => Convert();

    private void Convert()
    {
        string raw = (EpochInput?.Text ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            E2HResults.Visibility = Visibility.Collapsed;
            ShowError(E2HError, null);
            return;
        }

        if (!long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value))
        {
            E2HResults.Visibility = Visibility.Collapsed;
            ShowError(E2HError, P("Enter a whole number (digits only).", "請輸入整數（淨係數字）。"));
            return;
        }

        bool ms = EpochUnit.SelectedIndex == 1;
        if (!EpochService.TryFromEpoch(value, ms, out var when))
        {
            E2HResults.Visibility = Visibility.Collapsed;
            ShowError(E2HError, P("That timestamp is out of the supported range.", "呢個時間戳超出支援範圍。"));
            return;
        }

        ShowError(E2HError, null);
        RLocalValue.Text = when.LocalDateTime.ToString("F", CultureInfo.CurrentCulture);
        RUtcValue.Text = when.UtcDateTime.ToString("F", CultureInfo.CurrentCulture);
        RIsoValue.Text = when.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        RDowValue.Text = when.LocalDateTime.ToString("dddd", CultureInfo.CurrentCulture);
        var (relEn, relZh) = EpochService.Relative(when);
        RRelValue.Text = P(relEn, relZh);
        E2HResults.Visibility = Visibility.Visible;
    }

    // ---- Human -> epoch ----
    private void HumanInput_Changed(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs e) => RecomputeHumanToEpoch();
    private void HumanTime_Changed(object sender, TimePickerSelectedValueChangedEventArgs e) => RecomputeHumanToEpoch();
    private void Utc_Toggled(object sender, RoutedEventArgs e) => RecomputeHumanToEpoch();

    private void RecomputeHumanToEpoch()
    {
        if (DatePick?.Date is not DateTimeOffset date)
        {
            H2EResults.Visibility = Visibility.Collapsed;
            ShowError(H2EError, P("Pick a date to get an epoch.", "揀個日期先有紀元值。"));
            return;
        }

        TimeSpan tod = TimePick?.SelectedTime ?? TimeSpan.Zero;
        try
        {
            DateTimeOffset instant;
            if (UtcSwitch != null && UtcSwitch.IsOn)
            {
                instant = new DateTimeOffset(date.Year, date.Month, date.Day,
                    tod.Hours, tod.Minutes, tod.Seconds, TimeSpan.Zero);
            }
            else
            {
                var localDt = new DateTime(date.Year, date.Month, date.Day,
                    tod.Hours, tod.Minutes, tod.Seconds, DateTimeKind.Unspecified);
                instant = new DateTimeOffset(localDt, TimeZoneInfo.Local.GetUtcOffset(localDt));
            }

            ShowError(H2EError, null);
            OutSecValue.Text = EpochService.ToSeconds(instant).ToString(CultureInfo.InvariantCulture);
            OutMsValue.Text = EpochService.ToMilliseconds(instant).ToString(CultureInfo.InvariantCulture);
            H2EResults.Visibility = Visibility.Visible;
        }
        catch (Exception)
        {
            H2EResults.Visibility = Visibility.Collapsed;
            ShowError(H2EError, P("Could not read that date/time.", "讀唔到呢個日期／時間。"));
        }
    }

    private void CopySec_Click(object sender, RoutedEventArgs e) => Copy(OutSecValue.Text);
    private void CopyMs_Click(object sender, RoutedEventArgs e) => Copy(OutMsValue.Text);

    // ---- helpers ----
    private void Copy(string text)
    {
        try
        {
            if (string.IsNullOrEmpty(text)) return;
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P($"Copied: {text}", $"已複製：{text}");
        }
        catch (Exception)
        {
            StatusText.Text = P("Couldn't copy to the clipboard.", "複製唔到去剪貼簿。");
        }
    }

    private static void ShowError(TextBlock block, string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            block.Text = string.Empty;
            block.Visibility = Visibility.Collapsed;
        }
        else
        {
            block.Text = message;
            block.Visibility = Visibility.Visible;
        }
    }
}
