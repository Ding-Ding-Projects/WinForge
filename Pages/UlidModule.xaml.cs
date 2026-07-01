using System;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// ULID / Snowflake · ULID／Snowflake 工具 — generate 26-char Crockford Base32 ULIDs
/// (48-bit ms timestamp + 80-bit randomness, optional monotonic), decode a ULID back to
/// its timestamp + randomness, and break out a 64-bit Snowflake id against a chosen epoch.
/// Pure managed C#. Never throws — invalid input surfaces a bilingual status.
/// </summary>
public sealed partial class UlidModule : Page
{
    // Epoch options: label + Unix-ms epoch. Order matches EpochBox items added in Render.
    private static readonly long[] EpochValues = { 1288834974657L, 1420070400000L, 0L };

    private string _lastRandHex = string.Empty;
    private string _lastUlidTsLine = string.Empty;
    private string _lastSnowTsLine = string.Empty;

    public UlidModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "ULID / Snowflake · ULID／Snowflake 工具";
        HeaderBlurb.Text = P("Generate sortable ULIDs and decode ULID or Snowflake ids — see the embedded timestamp, randomness, worker and sequence. All computed locally, nothing leaves this PC.",
            "產生可排序嘅 ULID，同埋解碼 ULID 或者 Snowflake ID — 睇返裡面嘅時間戳、隨機值、worker 同序號。全部喺本機計算，冇嘢會離開部電腦。");

        GenTitle.Text = P("Generate ULIDs", "產生 ULID");
        CountLabel.Text = P("Count", "數量");
        MonoLabel.Text = P("Monotonic", "單調遞增");
        GenButton.Content = P("Generate", "產生");
        GenCopyButton.Content = P("Copy all", "複製全部");

        DecUlidTitle.Text = P("Decode ULID", "解碼 ULID");
        DecUlidButton.Content = P("Decode", "解碼");
        UlidTsCopyButton.Content = P("Copy timestamp", "複製時間");
        UlidRandCopyButton.Content = P("Copy randomness", "複製隨機值");

        DecSnowTitle.Text = P("Decode Snowflake", "解碼 Snowflake");
        EpochLabel.Text = P("Epoch", "紀元");
        DecSnowButton.Content = P("Decode", "解碼");
        SnowTsCopyButton.Content = P("Copy timestamp", "複製時間");

        int sel = EpochBox.SelectedIndex < 0 ? 0 : EpochBox.SelectedIndex;
        EpochBox.Items.Clear();
        EpochBox.Items.Add(P("Twitter / X (1288834974657)", "Twitter／X（1288834974657）"));
        EpochBox.Items.Add(P("Discord (1420070400000)", "Discord（1420070400000）"));
        EpochBox.Items.Add(P("Unix (0)", "Unix（0）"));
        EpochBox.SelectedIndex = sel;
    }

    // --- 1. Generate ---------------------------------------------------------

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int count = (int)(double.IsNaN(CountBox.Value) ? 1 : CountBox.Value);
            if (count < 1) count = 1;
            string[] ids = UlidService.Generate(count, MonoSwitch.IsOn);
            GenOutput.Text = string.Join(Environment.NewLine, ids);
            ShowStatus(InfoBarSeverity.Success, P($"Generated {ids.Length} ULID(s).", $"已產生 {ids.Length} 個 ULID。"));
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Generation failed: " + ex.Message, "產生失敗：" + ex.Message));
        }
    }

    private void GenCopy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(GenOutput.Text))
        {
            ShowStatus(InfoBarSeverity.Warning, P("Nothing to copy — generate some ULIDs first.", "冇嘢可以複製 — 請先產生 ULID。"));
            return;
        }
        CopyToClipboard(GenOutput.Text, P("ULIDs copied.", "已複製 ULID。"));
    }

    // --- 2. Decode ULID ------------------------------------------------------

    private void DecodeUlid_Click(object sender, RoutedEventArgs e)
    {
        var parts = UlidService.DecodeUlid(UlidInput.Text);
        if (!parts.Ok)
        {
            UlidTsUtc.Text = UlidTsLocal.Text = UlidRand.Text = string.Empty;
            _lastRandHex = _lastUlidTsLine = string.Empty;
            ShowStatus(InfoBarSeverity.Error, P("Not a valid ULID — expected 26 Crockford Base32 characters.", "唔係有效嘅 ULID — 應該係 26 個 Crockford Base32 字元。"));
            return;
        }

        string utc = parts.Timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        string local = parts.Timestamp.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
        _lastUlidTsLine = utc;
        _lastRandHex = parts.RandomnessHex;

        UlidTsUtc.Text = P("Timestamp (UTC): ", "時間戳（UTC）：") + utc + $"  ({parts.TimestampMs} ms)";
        UlidTsLocal.Text = P("Timestamp (local): ", "時間戳（本地）：") + local;
        UlidRand.Text = P("Randomness (80-bit hex): ", "隨機值（80 位 hex）：") + parts.RandomnessHex;
        ShowStatus(InfoBarSeverity.Success, P("ULID decoded.", "已解碼 ULID。"));
    }

    private void UlidTsCopy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastUlidTsLine)) { WarnDecodeFirst(); return; }
        CopyToClipboard(_lastUlidTsLine, P("Timestamp copied.", "已複製時間。"));
    }

    private void UlidRandCopy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastRandHex)) { WarnDecodeFirst(); return; }
        CopyToClipboard(_lastRandHex, P("Randomness copied.", "已複製隨機值。"));
    }

    // --- 3. Decode Snowflake -------------------------------------------------

    private void DecodeSnowflake_Click(object sender, RoutedEventArgs e)
    {
        int idx = EpochBox.SelectedIndex;
        if (idx < 0 || idx >= EpochValues.Length) idx = 0;
        long epoch = EpochValues[idx];

        var parts = UlidService.DecodeSnowflake(SnowInput.Text, epoch);
        if (!parts.Ok)
        {
            SnowTsUtc.Text = SnowTsLocal.Text = SnowWorker.Text = SnowSeq.Text = string.Empty;
            _lastSnowTsLine = string.Empty;
            ShowStatus(InfoBarSeverity.Error, P("Not a valid 64-bit Snowflake id.", "唔係有效嘅 64 位 Snowflake ID。"));
            return;
        }

        string utc = parts.Timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        string local = parts.Timestamp.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
        _lastSnowTsLine = utc;

        SnowTsUtc.Text = P("Timestamp (UTC): ", "時間戳（UTC）：") + utc + $"  ({parts.TimestampMs} ms)";
        SnowTsLocal.Text = P("Timestamp (local): ", "時間戳（本地）：") + local;
        SnowWorker.Text = P($"Worker: {parts.WorkerId}   Process: {parts.ProcessId}", $"Worker：{parts.WorkerId}　　Process：{parts.ProcessId}");
        SnowSeq.Text = P($"Sequence: {parts.Sequence}", $"序號：{parts.Sequence}");
        ShowStatus(InfoBarSeverity.Success, P("Snowflake decoded.", "已解碼 Snowflake。"));
    }

    private void SnowTsCopy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastSnowTsLine)) { WarnDecodeFirst(); return; }
        CopyToClipboard(_lastSnowTsLine, P("Timestamp copied.", "已複製時間。"));
    }

    // --- helpers -------------------------------------------------------------

    private void WarnDecodeFirst() =>
        ShowStatus(InfoBarSeverity.Warning, P("Nothing to copy — decode a value first.", "冇嘢可以複製 — 請先解碼。"));

    private void CopyToClipboard(string text, string okMessage)
    {
        try
        {
            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(text ?? string.Empty);
            Clipboard.SetContent(pkg);
            ShowStatus(InfoBarSeverity.Success, okMessage);
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Copy failed: " + ex.Message, "複製失敗：" + ex.Message));
        }
    }

    private void ShowStatus(InfoBarSeverity severity, string message)
    {
        Status.Severity = severity;
        Status.Message = message;
        Status.IsOpen = true;
    }
}
