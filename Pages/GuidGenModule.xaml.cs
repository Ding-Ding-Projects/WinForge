using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// GUID &amp; ID 產生器 · Generate GUIDs (with format + case options), bulk lists,
/// time-sortable ULIDs, nano-style URL-safe random strings, and inspect a GUID's
/// bytes / version / variant. All random material from RandomNumberGenerator.
/// Pure managed; every string bilingual. No throwing to the UI.
/// </summary>
public sealed partial class GuidGenModule : Page
{
    public GuidGenModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) =>
        {
            if (FormatCombo.SelectedIndex < 0) FormatCombo.SelectedIndex = 0;
            Render();
            GenGuid();
            GenUlid();
            GenNano();
        };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = P("GUID & ID Generator · GUID／ID 產生器", "GUID 同 ID 產生器 · GUID／ID 產生器");
        HeaderBlurb.Text = P("Generate GUIDs, time-sortable ULIDs and nano-style random IDs — all from a cryptographic random source — or inspect a GUID's bytes, version and variant.",
            "產生 GUID、可按時間排序嘅 ULID 同 nano 式隨機 ID（全部用加密級隨機源），亦可以拆解一個 GUID 嘅位元組、版本同變體。");

        GuidTitle.Text = P("GUID", "GUID");
        FormatLabel.Text = P("Format", "格式");
        UpperSwitch.Header = P("UPPERCASE", "大階");
        UpperSwitch.OnContent = P("On", "開");
        UpperSwitch.OffContent = P("Off", "關");
        GuidGenBtn.Content = P("Generate", "產生");
        GuidCopyBtn.Content = P("Copy", "複製");

        BulkTitle.Text = P("Bulk generate", "批量產生");
        CountLabel.Text = P("Count (1–1000)", "數量（1–1000）");
        BulkGenBtn.Content = P("Generate", "產生");
        BulkCopyBtn.Content = P("Copy all", "全部複製");

        OtherTitle.Text = P("ULID & nano-ID", "ULID 同 nano-ID");
        UlidGenBtn.Content = P("New ULID", "新 ULID");
        UlidCopyBtn.Content = P("Copy", "複製");
        NanoLenLabel.Text = P("nano-ID length (4–64)", "nano-ID 長度（4–64）");
        NanoGenBtn.Content = P("New nano-ID", "新 nano-ID");
        NanoCopyBtn.Content = P("Copy", "複製");

        InspectTitle.Text = P("GUID inspector", "GUID 拆解器");
        InspectInput.PlaceholderText = P("Paste a GUID to inspect…", "貼上一個 GUID 嚟拆解…");
        InspectHexLabel.Text = P("16 bytes (RFC 4122 order)", "16 位元組（RFC 4122 排序）");
        Inspect();
    }

    private string SelectedFormat()
    {
        if (FormatCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag && tag.Length > 0)
            return tag;
        return "D";
    }

    // --- GUID ---
    private void Guid_OptionChanged(object sender, RoutedEventArgs e) => GenGuid();
    private void GenGuid_Click(object sender, RoutedEventArgs e) => GenGuid();

    private void GenGuid()
    {
        try
        {
            GuidBox.Text = GuidGenService.NewGuid(SelectedFormat(), UpperSwitch.IsOn);
            SetStatus(P("Generated a GUID.", "已產生一個 GUID。"), false);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void CopyGuid_Click(object sender, RoutedEventArgs e) => Copy(GuidBox.Text);

    // --- Bulk ---
    private void GenBulk_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int n = (int)(double.IsNaN(CountBox.Value) ? 1 : CountBox.Value);
            BulkBox.Text = GuidGenService.BulkGuids(n, SelectedFormat(), UpperSwitch.IsOn);
            SetStatus(P($"Generated {Math.Clamp(n, 1, 1000)} GUIDs.", $"已產生 {Math.Clamp(n, 1, 1000)} 個 GUID。"), false);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void CopyBulk_Click(object sender, RoutedEventArgs e) => Copy(BulkBox.Text);

    // --- ULID ---
    private void GenUlid_Click(object sender, RoutedEventArgs e) => GenUlid();

    private void GenUlid()
    {
        try
        {
            UlidBox.Text = GuidGenService.NewUlid();
            SetStatus(P("Generated a ULID.", "已產生一個 ULID。"), false);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void CopyUlid_Click(object sender, RoutedEventArgs e) => Copy(UlidBox.Text);

    // --- nano-ID ---
    private void GenNano_Click(object sender, RoutedEventArgs e) => GenNano();

    private void GenNano()
    {
        try
        {
            int len = (int)(double.IsNaN(NanoLenBox.Value) ? 21 : NanoLenBox.Value);
            NanoBox.Text = GuidGenService.NewNanoId(len);
            SetStatus(P("Generated a nano-ID.", "已產生一個 nano-ID。"), false);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void CopyNano_Click(object sender, RoutedEventArgs e) => Copy(NanoBox.Text);

    // --- Inspector ---
    private void Inspect_Changed(object sender, TextChangedEventArgs e) => Inspect();

    private void Inspect()
    {
        string text = InspectInput.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            InspectHexBox.Text = string.Empty;
            InspectMeta.Text = string.Empty;
            return;
        }

        try
        {
            var info = GuidGenService.Inspect(text);
            InspectHexBox.Text = info.Hex;
            InspectMeta.Text = P($"Version: {info.Version}    Variant: {info.Variant}",
                $"版本：{info.Version}    變體：{info.Variant}");
            SetStatus(P("GUID parsed.", "GUID 已解析。"), false);
        }
        catch (Exception)
        {
            InspectHexBox.Text = string.Empty;
            InspectMeta.Text = string.Empty;
            SetStatus(P("That is not a valid GUID.", "呢個唔係有效嘅 GUID。"), true);
        }
    }

    // --- Helpers ---
    private void Copy(string? text)
    {
        try
        {
            if (string.IsNullOrEmpty(text))
            {
                SetStatus(P("Nothing to copy.", "冇嘢可以複製。"), true);
                return;
            }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            Clipboard.Flush();
            SetStatus(P("Copied to clipboard.", "已複製到剪貼簿。"), false);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Fail(Exception ex) =>
        SetStatus(P($"Error: {ex.Message}", $"錯誤：{ex.Message}"), true);

    private void SetStatus(string text, bool isError)
    {
        StatusText.Text = text;
        StatusText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
            isError ? "SystemFillColorCriticalBrush" : "TextFillColorSecondaryBrush"];
    }
}
