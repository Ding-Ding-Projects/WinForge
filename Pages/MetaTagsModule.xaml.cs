using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// HTML meta-tag 產生器 · Meta-tag generator — fill in Title/Description/Open Graph/Twitter fields and
/// get a ready-to-paste &lt;head&gt; block, attribute values HTML-encoded, only filled tags emitted.
/// Live output + one-click copy. Pure managed, robust, bilingual (粵語).
/// </summary>
public sealed partial class MetaTagsModule : Page
{
    public MetaTagsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SeedDefaults();
        Render();
        Regenerate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void SeedDefaults()
    {
        try
        {
            if (CharsetBox != null && string.IsNullOrEmpty(CharsetBox.Text)) CharsetBox.Text = "UTF-8";
            if (ViewportBox != null && string.IsNullOrEmpty(ViewportBox.Text)) ViewportBox.Text = "width=device-width, initial-scale=1";
            if (OgTypeBox != null && string.IsNullOrEmpty(OgTypeBox.Text)) OgTypeBox.Text = "website";
            if (TwCardBox != null && string.IsNullOrEmpty(TwCardBox.Text)) TwCardBox.Text = "summary_large_image";
        }
        catch { }
    }

    private void Render()
    {
        try
        {
            Header.Title = "Meta Tag Generator · Meta 標籤產生器";
            HeaderBlurb.Text = P("Fill in the fields you care about and get a ready-to-paste HTML head block — <title>, <meta>, canonical <link>, plus Open Graph and Twitter cards. Only filled fields are emitted, and every value is HTML-encoded.",
                "填你想要嘅欄位，就會即時砌出可以貼落 <head> 嘅 HTML — <title>、<meta>、canonical <link>，仲有 Open Graph 同 Twitter card。只會輸出有填嘅欄位，每個值都會做 HTML 編碼。");

            BasicHeading.Text = P("Basic", "基本");
            OgHeading.Text = P("Open Graph (Facebook, LinkedIn…)", "Open Graph（Facebook、LinkedIn⋯）");
            TwHeading.Text = P("Twitter / X card", "Twitter / X card");
            OutHeading.Text = P("Output", "輸出");

            SetHeader(TitleBox, P("Page title", "頁面標題"));
            SetHeader(DescBox, P("Description", "描述"));
            SetHeader(KeywordsBox, P("Keywords (comma-separated)", "關鍵字（用逗號分隔）"));
            SetHeader(AuthorBox, P("Author", "作者"));
            SetHeader(CanonicalBox, P("Canonical URL", "Canonical 網址"));
            SetHeader(ViewportBox, P("Viewport", "Viewport"));
            SetHeader(ThemeColorBox, P("Theme color (e.g. #0F172A)", "主題色（例如 #0F172A）"));
            SetHeader(CharsetBox, P("Charset", "字元編碼"));

            SetHeader(OgTitleBox, P("og:title", "og:title"));
            SetHeader(OgDescBox, P("og:description", "og:description"));
            SetHeader(OgImageBox, P("og:image URL", "og:image 網址"));
            SetHeader(OgUrlBox, P("og:url", "og:url"));
            SetHeader(OgTypeBox, P("og:type", "og:type"));

            SetHeader(TwCardBox, P("twitter:card", "twitter:card"));
            SetHeader(TwSiteBox, P("twitter:site (@handle)", "twitter:site（@帳號）"));
            SetHeader(TwCreatorBox, P("twitter:creator (@handle)", "twitter:creator（@帳號）"));

            CopyButton.Content = P("Copy", "複製");
            UpdateStatus();
        }
        catch { }
    }

    private static void SetHeader(TextBox? box, string header)
    {
        if (box != null) box.Header = header;
    }

    private void Field_Changed(object sender, TextChangedEventArgs e) => Regenerate();

    private MetaTagsService.Input Collect() => new()
    {
        Title = TitleBox?.Text,
        Description = DescBox?.Text,
        Keywords = KeywordsBox?.Text,
        Author = AuthorBox?.Text,
        Canonical = CanonicalBox?.Text,
        Viewport = ViewportBox?.Text,
        ThemeColor = ThemeColorBox?.Text,
        Charset = CharsetBox?.Text,
        OgTitle = OgTitleBox?.Text,
        OgDescription = OgDescBox?.Text,
        OgImage = OgImageBox?.Text,
        OgUrl = OgUrlBox?.Text,
        OgType = OgTypeBox?.Text,
        TwitterCard = TwCardBox?.Text,
        TwitterSite = TwSiteBox?.Text,
        TwitterCreator = TwCreatorBox?.Text,
    };

    private void Regenerate()
    {
        try
        {
            if (OutputBox == null) return;
            OutputBox.Text = MetaTagsService.Build(Collect());
            UpdateStatus();
        }
        catch { }
    }

    private void UpdateStatus()
    {
        try
        {
            if (StatusText == null) return;
            int n = MetaTagsService.Count(Collect());
            StatusText.Text = n == 0
                ? P("Fill in at least one field above to generate tags.", "上面至少填一個欄位先會產生標籤。")
                : P($"{n} tag(s) generated.", $"已產生 {n} 個標籤。");
        }
        catch { }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox?.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                if (StatusText != null) StatusText.Text = P("Nothing to copy yet.", "暫時冇嘢可以複製。");
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            if (StatusText != null) StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch
        {
            if (StatusText != null) StatusText.Text = P("Couldn't copy — try selecting the text manually.", "複製唔到 — 試下自己揀返段文字。");
        }
    }
}
