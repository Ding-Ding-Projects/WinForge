using System;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// UUID v7（時間排序）· Generate RFC 9562 time-ordered UUIDv7 identifiers (count +
/// sortable/monotonic option) and decode any UUID to reveal its embedded timestamp,
/// version and variant (v7 native, v1 best-effort). Pure managed, never throws. Bilingual.
/// </summary>
public sealed partial class UuidV7Module : Page
{
    private string _lastTimestamp = "";

    public UuidV7Module()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "UUID v7 · 識別碼（時間排序）";
        HeaderBlurb.Text = P("Generate RFC 9562 UUIDv7 — time-ordered, database-friendly identifiers that sort by creation time — or paste one to decode its embedded timestamp, version and variant.",
            "產生 RFC 9562 UUIDv7 — 按時間排序、對資料庫友好嘅識別碼；又或者貼一個入嚟，解碼佢入面嵌住嘅時間戳、版本同變體。");

        GenTitle.Text = P("Generate", "產生");
        CountLabel.Text = P("How many", "數量");
        MonotonicChk.Content = P("Sortable / monotonic (strictly increasing within the same millisecond)", "可排序／單調遞增（同一毫秒內嚴格遞增）");
        GenBtn.Content = P("Generate", "產生");
        CopyGenBtn.Content = P("Copy all", "全部複製");

        DecTitle.Text = P("Decode", "解碼");
        DecInput.PlaceholderText = P("Paste a UUID here…", "喺度貼一個 UUID…");
        DecodeBtn.Content = P("Decode", "解碼");
        CopyTsBtn.Content = P("Copy timestamp", "複製時間戳");

        VerCaption.Text = P("Version", "版本");
        VarCaption.Text = P("Variant", "變體");
        UtcCaption.Text = P("Timestamp (UTC)", "時間戳（UTC）");
        LocalCaption.Text = P("Timestamp (local)", "時間戳（本地）");
        CanonCaption.Text = P("Canonical", "標準格式");
    }

    private void ShowStatus(InfoBarSeverity sev, string msg)
    {
        Status.Severity = sev;
        Status.Message = msg;
        Status.IsOpen = true;
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int count = (int)(double.IsNaN(CountBox.Value) ? 1 : CountBox.Value);
            if (count < 1) count = 1;
            if (count > 1000) count = 1000;
            bool mono = MonotonicChk.IsChecked == true;

            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(UuidV7Service.NewV7(mono));
            }
            GenOutput.Text = sb.ToString();
            ShowStatus(InfoBarSeverity.Success, P($"Generated {count} UUIDv7.", $"已產生 {count} 個 UUIDv7。"));
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Could not generate: ", "無法產生：") + ex.Message);
        }
    }

    private void CopyGen_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = GenOutput.Text ?? "";
            if (text.Length == 0)
            {
                ShowStatus(InfoBarSeverity.Informational, P("Nothing to copy — generate first.", "冇嘢好複製 — 先產生。"));
                return;
            }
            SetClipboard(text);
            ShowStatus(InfoBarSeverity.Success, P("Copied to clipboard.", "已複製到剪貼簿。"));
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Copy failed: ", "複製失敗：") + ex.Message);
        }
    }

    private void DecInput_Changed(object sender, TextChangedEventArgs e)
    {
        // Live-decode is opt-in via the button; keep typing responsive but hide stale results.
        if (string.IsNullOrWhiteSpace(DecInput.Text))
            DecResults.Visibility = Visibility.Collapsed;
    }

    private void Decode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var r = UuidV7Service.Decode(DecInput.Text);
            if (!r.Ok)
            {
                DecResults.Visibility = Visibility.Collapsed;
                _lastTimestamp = "";
                string why = r.Error == "empty"
                    ? P("Paste a UUID to decode.", "貼一個 UUID 入嚟解碼。")
                    : P("That is not a valid UUID.", "呢個唔係有效嘅 UUID。");
                ShowStatus(InfoBarSeverity.Warning, why);
                return;
            }

            VerValue.Text = r.Version.ToString();
            VarValue.Text = r.VariantName;
            CanonValue.Text = r.Canonical;

            if (r.HasTimestamp)
            {
                UtcValue.Text = r.Timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                LocalValue.Text = r.Timestamp.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
                _lastTimestamp = UtcValue.Text;
            }
            else
            {
                string none = P("(no embedded timestamp for this version)", "（呢個版本冇嵌入時間戳）");
                UtcValue.Text = none;
                LocalValue.Text = none;
                _lastTimestamp = "";
            }

            DecResults.Visibility = Visibility.Visible;

            if (r.Version == 7)
                ShowStatus(InfoBarSeverity.Success, P("Valid UUIDv7 — timestamp extracted.", "有效嘅 UUIDv7 — 已抽出時間戳。"));
            else if (r.Version == 1)
                ShowStatus(InfoBarSeverity.Informational, P("This is a UUIDv1 (not v7); timestamp decoded best-effort.", "呢個係 UUIDv1（唔係 v7）；時間戳盡力解碼。"));
            else
                ShowStatus(InfoBarSeverity.Warning, P($"This is a UUIDv{r.Version}, not v7 — it carries no time-ordered timestamp.", $"呢個係 UUIDv{r.Version}，唔係 v7 — 冇按時間排序嘅時間戳。"));
        }
        catch (Exception ex)
        {
            DecResults.Visibility = Visibility.Collapsed;
            ShowStatus(InfoBarSeverity.Error, P("Decode failed: ", "解碼失敗：") + ex.Message);
        }
    }

    private void CopyTs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_lastTimestamp))
            {
                ShowStatus(InfoBarSeverity.Informational, P("No timestamp to copy — decode a v7 or v1 UUID first.", "冇時間戳好複製 — 先解碼一個 v7 或 v1 UUID。"));
                return;
            }
            SetClipboard(_lastTimestamp);
            ShowStatus(InfoBarSeverity.Success, P("Timestamp copied.", "已複製時間戳。"));
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Copy failed: ", "複製失敗：") + ex.Message);
        }
    }

    private static void SetClipboard(string text)
    {
        var pkg = new DataPackage();
        pkg.SetText(text);
        Clipboard.SetContent(pkg);
    }
}
