using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Markdown 即時預覽 · Markdown live-preview module — a two-pane editor (raw Markdown on the left,
/// rendered HTML in a WebView2 on the right). Rendering is debounced (~300ms) and driven by a
/// from-scratch converter (<see cref="MarkdownService"/>) — no Markdig / no NuGet. Bilingual chrome
/// (the rendered content is the user's own Markdown). Guards WebView2 init and never throws.
/// </summary>
public sealed partial class MarkdownModule : Page
{
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private bool _webReady;
    private string _lastHtml = string.Empty;

    private const string SampleMarkdown =
@"# Markdown Preview · Markdown 預覽

Type **Markdown** on the left — see it *rendered* live on the right.
喺左邊打 **Markdown**，右邊即時見到 *排版好* 嘅效果。

## Features 功能

- Headings, **bold**, *italic* and `inline code`
- Ordered lists, blockquotes and links
- Fenced code blocks with a monospace font

1. First item
2. Second item
3. Third item

> A blockquote reads nicely, in light **and** dark mode.
> 引言喺淺色同深色主題都靚。

```
// A fenced code block
int Square(int n) => n * n;
```

See [WinForge on GitHub](https://github.com) for more.

---

Happy writing! 寫得開心！
";

    public MarkdownModule()
    {
        InitializeComponent();
        Editor.Text = SampleMarkdown;

        _debounce.Tick += OnDebounceTick;
        Loc.I.LanguageChanged += OnLang;

        Loaded += (_, _) => Render();
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLang;
            _debounce.Stop();
            _debounce.Tick -= OnDebounceTick;
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLang(object? sender, EventArgs e) => Render();

    private async void Render()
    {
        Header.Title = P("Markdown Preview · Markdown 預覽", "Markdown 預覽 · Markdown Preview");
        Header.Subtitle = P("Live two-pane Markdown editor and previewer.", "即時雙欄 Markdown 編輯同預覽。");
        CopyHtmlButton.Content = P("Copy HTML", "複製 HTML");

        await EnsureWebAsync();
        UpdatePreview();
    }

    private async System.Threading.Tasks.Task EnsureWebAsync()
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
        string html;
        try { html = MarkdownService.ToHtml(Editor.Text); }
        catch (Exception ex)
        {
            SetStatus(P("Render error: ", "排版錯誤：") + ex.Message);
            return;
        }

        _lastHtml = html;

        if (!_webReady || Web.CoreWebView2 is null) return;
        try { Web.NavigateToString(html); }
        catch { /* transient nav failure — ignore, next keystroke retries */ }
    }

    private void CopyHtml_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string html = string.IsNullOrEmpty(_lastHtml) ? MarkdownService.ToHtml(Editor.Text) : _lastHtml;
            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(html);
            Clipboard.SetContent(pkg);
            SetStatus(P("HTML copied to clipboard.", "HTML 已複製到剪貼簿。"));
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
