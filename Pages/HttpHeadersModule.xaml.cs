using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// HTTP 標頭檢測 · HTTP header inspector — enter a URL, pick GET/HEAD, optionally follow redirects and
/// add custom request headers, then fire a real network request and see the final status, elapsed time
/// and every response/content header. Pure managed HttpClient. Bilingual (粵語). Never throws.
/// </summary>
public sealed partial class HttpHeadersModule : Page
{
    public HttpHeadersModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += OnUnloaded;
        Loaded += (_, _) => Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "HTTP Header Inspector · HTTP 標頭檢測";
        HeaderBlurb.Text = P(
            "Send a real GET or HEAD request to any URL and inspect the response — final status, round-trip time and every header.",
            "向任何網址發送真正嘅 GET 或 HEAD 請求，睇返回應 — 最終狀態、來回時間同每一個標頭。");
        NetworkNotice.Message = P(
            "This makes a real network request to the URL you enter.",
            "呢個會向你輸入嘅網址發送真正嘅網絡請求。");
        UrlLabel.Text = P("URL", "網址");
        MethodLabel.Text = P("Method", "方法");
        RedirectLabel.Text = P("Follow redirects", "跟隨重新導向");
        CustomLabel.Text = P("Custom request headers (optional)", "自訂請求標頭（可選）");
        SendButton.Content = P("Send", "發送");
        if (string.IsNullOrEmpty(StatusText.Text))
            StatusText.Text = P("Enter a URL and press Send.", "輸入網址，然後撳「發送」。");
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        bool head = (MethodBox.SelectedItem as ComboBoxItem)?.Content?.ToString() == "HEAD";
        bool follow = RedirectSwitch.IsOn;

        var custom = new List<(string, string)>
        {
            (H1Key.Text, H1Val.Text),
            (H2Key.Text, H2Val.Text),
        };

        SendButton.IsEnabled = false;
        Busy.IsActive = true;
        SummaryCard.Visibility = Visibility.Collapsed;
        HeadersCard.Visibility = Visibility.Collapsed;
        StatusText.Text = P("Sending…", "發送緊…");

        HttpHeadersService.Result result;
        try
        {
            result = await HttpHeadersService.InspectAsync(UrlBox.Text, head, follow, custom);
        }
        catch (Exception ex)
        {
            // Defensive: the service never throws, but keep the UI alive regardless.
            result = new HttpHeadersService.Result { Ok = false, Message = P($"Error: {ex.Message}", $"發生錯誤：{ex.Message}") };
        }
        finally
        {
            Busy.IsActive = false;
            SendButton.IsEnabled = true;
        }

        StatusText.Text = result.Message;
        if (!result.Ok) return;

        StatusLine.Text = P(
            $"{result.StatusCode} {result.Reason} · {result.ElapsedMs} ms",
            $"{result.StatusCode} {result.Reason} · {result.ElapsedMs} 毫秒");
        ContentTypeLine.Text = P($"Content-Type: {result.ContentType}", $"內容類型 (Content-Type)：{result.ContentType}");
        ContentLengthLine.Text = P($"Content-Length: {result.ContentLength}", $"內容長度 (Content-Length)：{result.ContentLength}");

        if (result.StatusCode is >= 300 and < 400 && !string.IsNullOrEmpty(result.Location))
        {
            LocationLine.Text = P($"Location: {result.Location}", $"重新導向到 (Location)：{result.Location}");
            LocationLine.Visibility = Visibility.Visible;
        }
        else
        {
            LocationLine.Visibility = Visibility.Collapsed;
        }

        FinalUrlLine.Text = P($"Final URL: {result.FinalUrl}", $"最終網址：{result.FinalUrl}");
        SummaryCard.Visibility = Visibility.Visible;

        HeadersList.ItemsSource = result.Headers;
        HeadersCard.Visibility = result.Headers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
