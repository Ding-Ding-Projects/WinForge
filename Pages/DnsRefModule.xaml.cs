using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// DNS 記錄參考 · DNS records reference — an offline, never-throwing cheat-sheet of ~30 DNS record
/// types (type, numeric code, bilingual purpose, example zone-file line), with a search box + a
/// category filter and a "which record for which task" table. Click a row to copy its example.
/// </summary>
public sealed partial class DnsRefModule : Page
{
    private bool _ready;

    public DnsRefModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); Populate(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e)
    {
        try { Render(); RebuildCategories(); Refresh(); } catch { }
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = P("DNS Records Reference", "DNS 記錄參考");
        HeaderBlurb.Text = P("An offline cheat-sheet of common DNS record types — what each one is for and an example zone-file line. Search or filter, then click a row to copy its example.",
            "常見 DNS 記錄類型嘅離線速查表 — 每種係做乜同一句區域檔範例。搜尋或篩選之後，撳一行就複製佢嘅範例。");
        if (SearchBox != null)
            SearchBox.PlaceholderText = P("Search type, code or purpose…", "搜尋類型、代碼或用途…");
        if (ListHint != null)
            ListHint.Text = P("Tip: click any record to copy its example line to the clipboard.", "貼士：撳任何一項記錄就會將佢嘅範例複製到剪貼簿。");
        if (HintsTitle != null)
            HintsTitle.Text = P("Which record for which task", "邊個任務用邊個記錄");
    }

    private void Populate()
    {
        try
        {
            RebuildCategories();
            HintsList.ItemsSource = DnsRefService.Hints;
            Refresh();
            _ready = true;
        }
        catch { }
    }

    private string CategoryLabel(string key) => key switch
    {
        "All" => P("All types", "全部類型"),
        "Addressing" => P("Addressing", "定址"),
        "Mail" => P("Mail", "電郵"),
        "Security / DNSSEC" => P("Security / DNSSEC", "安全／DNSSEC"),
        "Service" => P("Service", "服務"),
        "Modern" => P("Modern (HTTPS/SVCB)", "現代（HTTPS/SVCB）"),
        _ => key,
    };

    private void RebuildCategories()
    {
        if (CategoryCombo == null) return;
        try
        {
            int keep = CategoryCombo.SelectedIndex;
            var items = new List<ComboBoxItem>();
            foreach (var key in DnsRefService.Categories)
                items.Add(new ComboBoxItem { Content = CategoryLabel(key), Tag = key });

            CategoryCombo.SelectionChanged -= Filters_Changed;
            CategoryCombo.Items.Clear();
            foreach (var it in items) CategoryCombo.Items.Add(it);
            CategoryCombo.SelectedIndex = keep >= 0 && keep < items.Count ? keep : 0;
            CategoryCombo.SelectionChanged += Filters_Changed;
        }
        catch { }
    }

    private void Filters_Changed(object sender, object e)
    {
        if (_ready) Refresh();
    }

    private void Refresh()
    {
        try
        {
            string query = SearchBox?.Text ?? "";
            string category = (CategoryCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";
            RecordsList.ItemsSource = DnsRefService.Search(query, category);
        }
        catch
        {
            try { RecordsList.ItemsSource = DnsRefService.All; } catch { }
        }
    }

    private void Record_Click(object sender, ItemClickEventArgs e)
    {
        try
        {
            if (e.ClickedItem is not DnsRefService.DnsRecord rec) return;
            var pkg = new DataPackage();
            pkg.SetText(rec.Example ?? "");
            Clipboard.SetContent(pkg);

            if (Info != null)
            {
                Info.Severity = InfoBarSeverity.Success;
                Info.Message = P($"Copied the {rec.Type} example to the clipboard.", $"已將 {rec.Type} 範例複製到剪貼簿。");
                Info.IsOpen = true;
            }
        }
        catch
        {
            if (Info != null)
            {
                Info.Severity = InfoBarSeverity.Warning;
                Info.Message = P("Could not copy to the clipboard.", "無法複製到剪貼簿。");
                Info.IsOpen = true;
            }
        }
    }
}
