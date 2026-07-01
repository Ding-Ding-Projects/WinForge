using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// cURL / fetch / PowerShell 片段產生器 · Snippet generator. Describe a request (URL, method,
/// headers, body, auth) and get copy-paste-ready <c>curl</c>, JavaScript <c>fetch()</c> and
/// PowerShell <c>Invoke-RestMethod</c> code. Generates code only — never makes a network call.
/// Live-updates on every change. Bilingual. Robust — never throws.
/// </summary>
public sealed partial class CurlGenModule : Page
{
    /// <summary>A single request header for the ListView (classic {Binding}).</summary>
    public sealed class HeaderRow
    {
        public string Key { get; set; } = "";
        public string Val { get; set; } = "";
        public string Display => Key + ": " + Val;
    }

    private readonly ObservableCollection<HeaderRow> _headers = new();
    private bool _ready;

    public CurlGenModule()
    {
        InitializeComponent();
        HeadersList.ItemsSource = _headers;
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ready = true;
        Render();
        Regenerate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "cURL Generator · cURL 產生器";
            HeaderBlurb.Text = P("Describe an HTTP request and get ready-to-paste curl, JavaScript fetch() and PowerShell snippets. Nothing is sent — this only generates code.",
                "填好一個 HTTP 請求，即刻整出可以貼上用嘅 curl、JavaScript fetch() 同 PowerShell 片段。乜都唔會send出去 — 淨係產生代碼。");
            HeadersTitle.Text = P("Request headers", "請求標頭");
            AddHdrBtn.Content = P("Add", "加入");
            AuthTitle.Text = P("Authentication", "身份驗證");
            BodyTitle.Text = P("Request body", "請求內文");
            CtLabel.Text = P("Content-Type", "內容類型");
            CopyBtn.Content = P("Copy", "複製");

            SetItem(AuthBox, 0, P("No auth", "冇驗證"));
            SetItem(AuthBox, 1, P("Bearer token", "Bearer 權杖"));
            SetItem(AuthBox, 2, P("Basic (user:pass)", "Basic（用戶:密碼）"));

            UpdateStatus();
        }
        catch { /* never throw from UI render */ }
    }

    private static void SetItem(ComboBox box, int i, string text)
    {
        if (i >= 0 && i < box.Items.Count && box.Items[i] is ComboBoxItem it)
            it.Content = text;
    }

    private CurlGenService.Request BuildRequest()
    {
        var r = new CurlGenService.Request
        {
            Url = UrlBox.Text ?? "",
            Method = (MethodBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "GET",
            Body = BodyBox.Text ?? "",
            ContentType = ContentTypeBox.Text ?? "",
            AuthKind = AuthBox.SelectedIndex switch { 1 => "bearer", 2 => "basic", _ => "none" },
            BearerToken = BearerBox.Text ?? "",
            BasicUser = BasicUserBox.Text ?? "",
            BasicPass = BasicPassBox.Text ?? "",
            Headers = new List<KeyValuePair<string, string>>()
        };
        foreach (var h in _headers)
            r.Headers.Add(new KeyValuePair<string, string>(h.Key, h.Val));
        return r;
    }

    private void Regenerate()
    {
        if (!_ready) return;
        try
        {
            var r = BuildRequest();
            OutputText.Text = OutputBox.SelectedIndex switch
            {
                1 => CurlGenService.Fetch(r),
                2 => CurlGenService.PowerShell(r),
                _ => CurlGenService.Curl(r),
            };
        }
        catch (Exception ex)
        {
            OutputText.Text = "# " + ex.Message;
        }
        UpdateStatus();
    }

    private void AnyChanged(object sender, object e) => Regenerate();
    private void Output_Changed(object sender, SelectionChangedEventArgs e) => Regenerate();

    private void Auth_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        int idx = AuthBox.SelectedIndex;
        BearerBox.Visibility = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
        BasicPanel.Visibility = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
        Regenerate();
    }

    private void AddHeader_Click(object sender, RoutedEventArgs e)
    {
        var k = (HdrKeyBox.Text ?? "").Trim();
        if (k.Length == 0) { UpdateStatus(P("Enter a header name first.", "先填標頭名。")); return; }
        _headers.Add(new HeaderRow { Key = k, Val = (HdrValBox.Text ?? "").Trim() });
        HdrKeyBox.Text = "";
        HdrValBox.Text = "";
        Regenerate();
    }

    private void RemoveHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is HeaderRow row)
        {
            _headers.Remove(row);
            Regenerate();
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = OutputText.Text ?? "";
            if (text.Length == 0) { UpdateStatus(P("Nothing to copy yet.", "暫時冇嘢可以複製。")); return; }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            UpdateStatus(P("Copied to clipboard.", "已複製到剪貼簿。"));
        }
        catch (Exception ex)
        {
            UpdateStatus(P("Copy failed: ", "複製失敗：") + ex.Message);
        }
    }

    private void UpdateStatus(string? msg = null)
    {
        if (StatusText == null) return;
        if (msg != null) { StatusText.Text = msg; return; }
        StatusText.Text = P("Code only — no request is ever sent.", "淨係產生代碼 — 唔會send任何請求。");
    }
}
