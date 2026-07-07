using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// DNS 查詢 · DNS Lookup — resolve A/AAAA/PTR via System.Net.Dns and MX/TXT/NS/CNAME via a
/// public DNS-over-HTTPS resolver (Google). Async, non-blocking, bilingual. No redirect.
/// </summary>
public sealed partial class DnsLookupModule : Page
{
    private readonly ObservableCollection<DnsLookupService.DnsAnswer> _answers = new();

    public DnsLookupModule()
    {
        InitializeComponent();
        AnswerList.ItemsSource = _answers;
        Loc.I.LanguageChanged += OnLang;
        Unloaded += OnUnloaded;
        Loaded += (_, _) => Render();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLang(object? sender, EventArgs e) => Render();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Unloaded -= OnUnloaded;
    }

    private void Render()
    {
        Header.Title = "DNS Lookup · DNS 查詢";
        HeaderBlurb.Text = P("Resolve DNS records for any host — addresses, mail servers, name servers, text and more.",
            "查詢任何主機嘅 DNS 記錄 — 位址、郵件伺服器、名稱伺服器、文字記錄等等。");
        NameLabel.Text = P("Host name or IP", "主機名或 IP");
        TypeLabel.Text = P("Record type", "記錄類型");
        LookupButton.Content = P("Look up", "查詢");
        ResolverNote.Text = P("Note: A/AAAA and PTR use the system resolver. MX/TXT/NS/CNAME use a public DNS-over-HTTPS resolver (Google, dns.google).",
            "註：A/AAAA 同 PTR 用系統解析器；MX/TXT/NS/CNAME 用公共 DNS-over-HTTPS 解析器（Google，dns.google）。");
        if (StatusText.Text.Length == 0)
            StatusText.Text = P("Enter a host and pick a record type.", "輸入主機並揀一個記錄類型。");
    }

    private async void Lookup_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text ?? "";
        string type = (TypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "A";

        LookupButton.IsEnabled = false;
        _answers.Clear();
        StatusText.Text = P($"Looking up {type} for \"{name}\"…", $"正在查詢「{name}」嘅 {type}…");

        try
        {
            var result = await DnsLookupService.LookupAsync(name, type);

            foreach (var a in result.Answers)
                _answers.Add(a);

            if (!string.IsNullOrEmpty(result.StatusEn) || !string.IsNullOrEmpty(result.StatusZh))
            {
                StatusText.Text = P(result.StatusEn, result.StatusZh);
            }
            else
            {
                StatusText.Text = P($"{_answers.Count} answer(s) · {result.ElapsedMs} ms",
                    $"{_answers.Count} 個結果 · {result.ElapsedMs} 毫秒");
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Lookup failed: " + ex.Message, "查詢失敗：" + ex.Message);
        }
        finally
        {
            LookupButton.IsEnabled = true;
        }
    }
}
