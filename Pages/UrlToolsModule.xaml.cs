using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// URL 及查詢字串工具 · URL &amp; query-string tools — parse a URL into parts, edit its query params,
/// rebuild it with proper encoding, and encode/decode arbitrary text. Pure managed, bilingual.
/// </summary>
public sealed partial class UrlToolsModule : Page
{
    private readonly ObservableCollection<UrlToolsService.QueryParam> _params = new();
    private UrlToolsService.UrlParts _parts = new();

    public UrlToolsModule()
    {
        InitializeComponent();
        ParamsList.ItemsSource = _params;
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
        Loaded += (_, _) => Render();
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "URL Tools · 網址工具";
            HeaderBlurb.Text = P(
                "Break a URL into its parts, edit the query string, rebuild it with proper encoding, and encode or decode any text — all offline.",
                "將網址拆開做各個部分、改查詢字串、正確編碼後重組，仲可以編碼／解碼任何文字 — 全程離線。");

            ParseTitle.Text = P("Parse a URL", "拆解網址");
            if (string.IsNullOrEmpty(UrlBox.Text))
                UrlBox.PlaceholderText = P("https://user@host:8080/path?a=1&b=hello%20world#top", "https://user@host:8080/path?a=1&b=hello%20world#top");
            ParseBtn.Content = P("Parse", "拆解");

            SchemeLabel.Text = P("Scheme", "協定");
            UserLabel.Text = P("User info", "用戶資訊");
            HostLabel.Text = P("Host", "主機");
            PortLabel.Text = P("Port", "連接埠");
            PathLabel.Text = P("Path", "路徑");
            QueryLabel.Text = P("Query", "查詢");
            FragLabel.Text = P("Fragment", "片段");

            ParamsTitle.Text = P("Query parameters", "查詢參數");
            KeyInput.PlaceholderText = P("key", "鍵");
            ValInput.PlaceholderText = P("value", "值");
            AddBtn.Content = P("Add", "新增");
            EditBtn.Content = P("Edit selected", "編輯所選");
            RemoveBtn.Content = P("Remove selected", "移除所選");
            RebuildBtn.Content = P("Rebuild URL", "重組網址");
            CopyUrlBtn.Content = P("Copy URL", "複製網址");

            CodecTitle.Text = P("Encode / Decode text", "編碼／解碼文字");
            EncodeBtn.Content = P("Encode", "編碼");
            DecodeBtn.Content = P("Decode", "解碼");
            CopyCodecBtn.Content = P("Copy", "複製");

            if (StatusText.Text.Length == 0)
                StatusText.Text = P("Ready.", "已就緒。");
        }
        catch { /* never throw from UI render */ }
    }

    private void Status(string en, string zh)
    {
        try { StatusText.Text = P(en, zh); } catch { }
    }

    private void Parse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _parts = UrlToolsService.Parse(UrlBox.Text);
            SchemeField.Text = _parts.Scheme;
            UserField.Text = _parts.UserInfo;
            HostField.Text = _parts.Host;
            PortField.Text = _parts.Port;
            PathField.Text = _parts.Path;
            QueryField.Text = _parts.Query;
            FragField.Text = _parts.Fragment;

            _params.Clear();
            foreach (var pr in UrlToolsService.ParseQuery(_parts.Query))
                _params.Add(pr);

            if (_parts.Valid)
                Status("Parsed as an absolute URL.", "已當作絕對網址拆解。");
            else if (UrlBox.Text.Trim().Length == 0)
                Status("Enter a URL to parse.", "輸入一個網址嚟拆解。");
            else
                Status("Not an absolute URL — parsed best-effort.", "唔係絕對網址 — 盡力拆解咗。");
        }
        catch { Status("Could not parse that input.", "拆解唔到呢個輸入。"); }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string k = KeyInput.Text?.Trim() ?? "";
            if (k.Length == 0) { Status("Enter a key first.", "先輸入一個鍵。"); return; }
            _params.Add(new UrlToolsService.QueryParam(k, ValInput.Text ?? ""));
            KeyInput.Text = ""; ValInput.Text = "";
            Status("Parameter added.", "已新增參數。");
        }
        catch { Status("Could not add that parameter.", "加唔到呢個參數。"); }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ParamsList.SelectedItem is not UrlToolsService.QueryParam sel)
            {
                Status("Select a parameter to edit.", "揀一個參數嚟編輯。");
                return;
            }
            string k = KeyInput.Text?.Trim() ?? "";
            sel.Key = k.Length > 0 ? k : sel.Key;
            sel.Value = ValInput.Text ?? "";
            // refresh the bound item
            int idx = _params.IndexOf(sel);
            if (idx >= 0) { _params.RemoveAt(idx); _params.Insert(idx, sel); ParamsList.SelectedIndex = idx; }
            Status("Parameter updated.", "已更新參數。");
        }
        catch { Status("Could not edit that parameter.", "改唔到呢個參數。"); }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ParamsList.SelectedItem is UrlToolsService.QueryParam sel)
            {
                _params.Remove(sel);
                Status("Parameter removed.", "已移除參數。");
            }
            else Status("Select a parameter to remove.", "揀一個參數嚟移除。");
        }
        catch { Status("Could not remove that parameter.", "移除唔到呢個參數。"); }
    }

    private void Rebuild_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // keep non-query parts in sync with the read-only fields (in case they were re-parsed)
            _parts.Scheme = SchemeField.Text;
            _parts.UserInfo = UserField.Text;
            _parts.Host = HostField.Text;
            _parts.Port = PortField.Text;
            _parts.Path = PathField.Text;
            _parts.Fragment = FragField.Text;

            string url = UrlToolsService.Rebuild(_parts, _params);
            RebuiltBox.Text = url;
            QueryField.Text = UrlToolsService.BuildQuery(_params);
            Status("URL rebuilt with encoded parameters.", "已用編碼參數重組網址。");
        }
        catch { Status("Could not rebuild the URL.", "重組唔到網址。"); }
    }

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
        => CopyToClipboard(RebuiltBox.Text.Length > 0 ? RebuiltBox.Text : UrlBox.Text);

    private void CopyPart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string tag = (sender as FrameworkElement)?.Tag as string ?? "";
            string text = tag switch
            {
                "scheme" => SchemeField.Text,
                "user" => UserField.Text,
                "host" => HostField.Text,
                "port" => PortField.Text,
                "path" => PathField.Text,
                "query" => QueryField.Text,
                "frag" => FragField.Text,
                _ => ""
            };
            CopyToClipboard(text);
        }
        catch { Status("Could not copy.", "複製唔到。"); }
    }

    private void Encode_Click(object sender, RoutedEventArgs e)
    {
        try { CodecBox.Text = UrlToolsService.Encode(CodecBox.Text); Status("Encoded.", "已編碼。"); }
        catch { Status("Could not encode.", "編碼唔到。"); }
    }

    private void Decode_Click(object sender, RoutedEventArgs e)
    {
        try { CodecBox.Text = UrlToolsService.Decode(CodecBox.Text); Status("Decoded.", "已解碼。"); }
        catch { Status("Could not decode.", "解碼唔到。"); }
    }

    private void CopyCodec_Click(object sender, RoutedEventArgs e) => CopyToClipboard(CodecBox.Text);

    private void CopyToClipboard(string? text)
    {
        try
        {
            if (string.IsNullOrEmpty(text)) { Status("Nothing to copy.", "冇嘢可以複製。"); return; }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            Status("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch { Status("Could not access the clipboard.", "用唔到剪貼簿。"); }
    }
}
