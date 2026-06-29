using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinForge.Controls;

/// <summary>
/// Standard module page header — accent icon chip + title + optional subtitle +
/// a right-aligned actions slot. See ModuleHeader.xaml for usage.
/// 標準模組頁首：強調色圖示方塊 + 標題 + 可選副標題 + 右邊動作區。
/// </summary>
public sealed partial class ModuleHeader : UserControl
{
    public ModuleHeader()
    {
        InitializeComponent();
    }

    /// <summary>Segoe Fluent Icons glyph for the header chip, e.g. "".</summary>
    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(ModuleHeader),
            new PropertyMetadata(string.Empty, OnGlyphChanged));

    private static void OnGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var h = (ModuleHeader)d;
        h.HeaderGlyph.Glyph = e.NewValue as string ?? string.Empty;
    }

    /// <summary>Page title (already-localized, e.g. via Loc.I.Pick).</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ModuleHeader),
            new PropertyMetadata(string.Empty, OnTitleChanged));

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var h = (ModuleHeader)d;
        h.TitleText.Text = e.NewValue as string ?? string.Empty;
    }

    /// <summary>Optional subtitle / blurb under the title. Hidden when empty.</summary>
    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(ModuleHeader),
            new PropertyMetadata(string.Empty, OnSubtitleChanged));

    private static void OnSubtitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var h = (ModuleHeader)d;
        var text = e.NewValue as string ?? string.Empty;
        h.SubtitleText.Text = text;
        h.SubtitleText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Right-aligned action content (buttons, pills, etc.).</summary>
    public object ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    public static readonly DependencyProperty ActionContentProperty =
        DependencyProperty.Register(nameof(ActionContent), typeof(object), typeof(ModuleHeader),
            new PropertyMetadata(null));
}
