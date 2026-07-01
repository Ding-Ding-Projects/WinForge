using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// HTML 即時預覽 · HTML live-preview module — a two-pane workbench (raw HTML on the left, a live
/// <c>WebView2</c> render on the right). Rendering is debounced (~300ms); on each tick the current
/// source is fed to <c>NavigateToString</c> after WebView2 has initialised once. "Escape HTML"
/// shows the source as literal, entity-encoded text; "Copy" puts the source on the clipboard.
/// WebView2 init is guarded — if it fails the editor still works and a bilingual status explains why.
/// Bilingual chrome (Loc.I.Pick); never throws.
/// </summary>
public sealed partial class HtmlPreviewModule : Page
{
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private bool _webReady;

    public HtmlPreviewModule()
    {
        InitializeComponent();
        Editor.Text = HtmlPreviewService.SampleHtml;

        _debounce.Tick += OnDebounceTick;
        Loc.I.LanguageChanged += OnLang;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Render();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _debounce.Stop();
        _debounce.Tick -= OnDebounceTick;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private async void Render()
    {
        Header.Title = P("HTML Preview · HTML 預覽", "HTML 預覽 · HTML Preview");
        Header.Subtitle = P("Live two-pane HTML editor and previewer.", "即時雙欄 HTML 編輯同預覽。");
        EscapeButton.Content = P("Escape HTML", "轉義 HTML");
        CopyButton.Content = P("Copy", "複製");

        await EnsureWebAsync();
        UpdatePreview();
    }

    private async Task EnsureWebAsync()
    {
        if (_webReady) return;
        try
        {
            await Web.EnsureCoreWebView2Async();
            if (Web.CoreWebView2 is not null)
            {
                try { Web.CoreWebView2.Settings.AreDevToolsEnabled = false; } catch { }
                _webReady = true;
                SetStatus(P("Ready — preview updates as you type.", "已就緒 — 打字時預覽即時更新。"));
            }
        }
        catch (Exception ex)
        {
            _webReady = false;
            SetStatus(P("Preview unavailable (WebView2 failed to start). The editor still works. ",
                        "預覽用唔到（WebView2 起動失敗）。編輯器仍然可用。 ") + ex.Message);
        }
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Restart the debounce; render on the trailing edge.
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnDebounceTick(object? sender, object e)
    {
        _debounce.Stop();
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (!_webReady || Web.CoreWebView2 is null) return;
        string html;
        try { html = HtmlPreviewService.WrapFragment(Editor.Text); }
        catch (Exception ex) { SetStatus(P("Render error: ", "排版錯誤：") + ex.Message); return; }

        try { Web.NavigateToString(html); }
        catch { /* transient nav failure — ignore, next keystroke retries */ }
    }

    private void Escape_Click(object sender, RoutedEventArgs e)
    {
        if (!_webReady || Web.CoreWebView2 is null)
        {
            SetStatus(P("Preview unavailable — cannot show escaped HTML.", "預覽用唔到 — 顯示唔到轉義 HTML。"));
            return;
        }
        try
        {
            string escaped = HtmlPreviewService.Escape(Editor.Text);
            string doc = "<!DOCTYPE html><html><head><meta charset=\"utf-8\" /></head>"
                       + "<body><pre style=\"white-space:pre-wrap;font-family:'Cascadia Code',Consolas,monospace;\">"
                       + escaped + "</pre></body></html>";
            Web.NavigateToString(doc);
            SetStatus(P("Showing escaped HTML source on the right.", "右邊顯示緊轉義後嘅 HTML 原始碼。"));
        }
        catch (Exception ex)
        {
            SetStatus(P("Could not escape: ", "轉義唔到：") + ex.Message);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(Editor.Text ?? string.Empty);
            Clipboard.SetContent(pkg);
            SetStatus(P("HTML source copied to clipboard.", "HTML 原始碼已複製到剪貼簿。"));
        }
        catch (Exception ex)
        {
            SetStatus(P("Could not copy: ", "複製唔到：") + ex.Message);
        }
    }

    private void SetStatus(string text)
    {
        if (StatusText is not null) StatusText.Text = text;
    }
}
