using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 圖片 ↔ Base64 · Image ↔ Base64 module. Encode: pick an image → produce a
/// "data:image/&lt;ext&gt;;base64,…" data URI (read-only), show byte size + base64 length, preview it,
/// copy the URI. Decode: paste a data URI or raw base64 → strip the prefix → decode → preview → save.
/// Pure managed, bilingual (粵語). All IO is async + guarded; the UI never blocks and nothing throws.
/// </summary>
public sealed partial class ImgBase64Module : Page
{
    private byte[]? _decoded;   // bytes from the last successful decode (for Save)
    private string? _decodedExt;

    public ImgBase64Module()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Render();

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Image ↔ Base64 · 圖片 ↔ Base64";
        HeaderBlurb.Text = P("Turn an image into a Base64 data URI you can paste into HTML, CSS or JSON — or paste a data URI / raw Base64 back and save it as a file. All local, nothing leaves your PC.",
            "將圖片變做 Base64 data URI，可以貼落 HTML、CSS 或者 JSON — 又或者貼返個 data URI／純 Base64 入嚟，儲返做檔案。全部喺本機做，唔會上傳。");
        EncodeTitle.Text = P("Image → Base64", "圖片 → Base64");
        PickButton.Content = P("Pick image", "揀圖片");
        CopyUriButton.Content = P("Copy data URI", "複製 data URI");
        DecodeTitle.Text = P("Base64 → Image", "Base64 → 圖片");
        InputBox.PlaceholderText = P("Paste a data URI or raw Base64 here…", "喺呢度貼 data URI 或者純 Base64…");
        DecodeButton.Content = P("Decode & preview", "解碼並預覽");
        SaveButton.Content = P("Save image…", "儲存圖片…");
        PreviewTitle.Text = P("Preview", "預覽");
    }

    private async void Pick_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await FileDialogs.OpenFileAsync(".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp");
            if (string.IsNullOrEmpty(path)) return;

            EncodeStats.Text = P("Reading…", "讀緊…");
            var r = await ImgBase64Service.EncodeFileAsync(path);
            if (!r.Ok || r.Bytes is null || r.Text is null)
            {
                EncodeStats.Text = P("Couldn't read that image: ", "讀唔到嗰張圖：") + (r.Error ?? "?");
                return;
            }

            UriBox.Text = r.Text;
            CopyUriButton.IsEnabled = true;
            EncodeStats.Text = P($"{r.Bytes.Length:N0} bytes → {r.Text.Length:N0} Base64 chars",
                $"{r.Bytes.Length:N0} 位元組 → {r.Text.Length:N0} 個 Base64 字元");
            await SetPreviewAsync(r.Bytes);
        }
        catch (Exception ex)
        {
            EncodeStats.Text = P("Error: ", "錯誤：") + ex.Message;
        }
    }

    private void CopyUri_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(UriBox.Text)) return;
            var pkg = new DataPackage();
            pkg.SetText(UriBox.Text);
            Clipboard.SetContent(pkg);
            EncodeStats.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            EncodeStats.Text = P("Copy failed: ", "複製失敗：") + ex.Message;
        }
    }

    private async void Decode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var r = ImgBase64Service.Decode(InputBox.Text);
            if (!r.Ok || r.Bytes is null)
            {
                _decoded = null;
                _decodedExt = null;
                SaveButton.IsEnabled = false;
                DecodeStats.Text = P("That doesn't look like valid Base64.", "呢個唔似係有效嘅 Base64。");
                return;
            }

            _decoded = r.Bytes;
            _decodedExt = r.Ext;
            SaveButton.IsEnabled = true;
            DecodeStats.Text = P($"Decoded {r.Bytes.Length:N0} bytes.", $"解碼咗 {r.Bytes.Length:N0} 位元組。");
            await SetPreviewAsync(r.Bytes);
        }
        catch (Exception ex)
        {
            DecodeStats.Text = P("Error: ", "錯誤：") + ex.Message;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_decoded is null) return;
            var ext = string.IsNullOrWhiteSpace(_decodedExt) ? "png" : _decodedExt!;
            if (ext.Equals("jpeg", StringComparison.OrdinalIgnoreCase)) ext = "jpg";
            var path = await FileDialogs.SaveFileAsync("image." + ext, "." + ext);
            if (string.IsNullOrEmpty(path)) return;

            var err = await ImgBase64Service.SaveBytesAsync(path, _decoded);
            DecodeStats.Text = err is null
                ? P("Saved: ", "已儲存：") + path
                : P("Save failed: ", "儲存失敗：") + err;
        }
        catch (Exception ex)
        {
            DecodeStats.Text = P("Error: ", "錯誤：") + ex.Message;
        }
    }

    private async Task SetPreviewAsync(byte[] bytes)
    {
        try
        {
            var bmp = new BitmapImage();
            using var ms = new MemoryStream(bytes);
            await bmp.SetSourceAsync(ms.AsRandomAccessStream());
            Preview.Source = bmp;
        }
        catch
        {
            Preview.Source = null; // not a decodable image — leave the bytes/URI usable, just no preview
        }
    }
}
