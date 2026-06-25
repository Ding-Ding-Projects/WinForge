using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生 REST API 用戶端 · A native Postman/Insomnia-style REST client built purely on HttpClient.
/// Build a request (method · URL · query params · headers · body · auth), send it, and inspect the
/// response (status · time · size · headers · pretty/raw body). Save requests into collections shown
/// in a sidebar tree, and define environments whose {{var}} tokens are substituted before sending.
/// Everything is in-app managed C# — no Postman, no shelling out, no browser. Bilingual throughout.
/// </summary>
public sealed partial class ApiClientModule : Page
{
    private readonly ApiClientService _svc = new();

    // The request currently being edited (a working copy). Its lists drive the editor grids directly.
    private ApiRequest _req = new();
    private readonly ObservableCollection<ApiKeyValue> _params = new();
    private readonly ObservableCollection<ApiKeyValue> _headers = new();
    private readonly ObservableCollection<ApiKeyValue> _form = new();

    // Sidebar tree nodes.
    private readonly ObservableCollection<TreeNode> _tree = new();

    private ApiResponse? _lastResponse;
    private CancellationTokenSource? _sendCts;
    private bool _loading;

    public ApiClientModule()
    {
        InitializeComponent();
        ParamsList.ItemsSource = _params;
        HeadersList.ItemsSource = _headers;
        FormList.ItemsSource = _form;
        CollTree.ItemsSource = _tree;
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; _sendCts?.Cancel(); };
        Loaded += (_, _) =>
        {
            BuildMethodBox();
            BuildBodyModeBox();
            BuildAuthKindBox();
            Render();
            BuildEnvBox();
            BuildTree();
            LoadRequestIntoEditor(_svc.Workspace.Collections.FirstOrDefault()?.Requests.FirstOrDefault() ?? new ApiRequest());
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private void OnLang(object? s, EventArgs e) { Render(); BuildEnvBox(); BuildTree(); }

    private void Render()
    {
        HeaderTitle.Text = "API Client · REST API 用戶端";
        HeaderBlurb.Text = P(
            "A native REST API client — build requests with a method, URL, query params, headers, body and auth, send them over HttpClient, and inspect the status, timing, size, headers and a pretty-printed body. Save requests into collections and switch environments whose {{variables}} are substituted before sending.",
            "原生 REST API 用戶端 — 用方法、網址、查詢參數、標頭、內文同驗證砌請求，經 HttpClient 送出，再睇狀態、時間、大小、回應標頭同美化內文。可以將請求存入集合，亦可切換環境，發送前自動替換 {{變數}}。");

        CollectionsTitle.Text = P("Collections · 集合", "集合");
        SendBtn.Content = P("Send · 發送", "發送");

        TabParams.Header = P("Query Params · 查詢參數", "查詢參數");
        TabHeaders.Header = P("Headers · 標頭", "標頭");
        TabBody.Header = P("Body · 內文", "內文");
        TabAuth.Header = P("Auth · 驗證", "驗證");

        AddParamBtn.Content = P("Add param · 加參數", "加參數");
        AddHeaderBtn.Content = P("Add header · 加標頭", "加標頭");
        AddFormBtn.Content = P("Add field · 加欄位", "加欄位");
        BeautifyBtn.Content = P("Beautify JSON · 美化", "美化 JSON");

        BearerLabel.Text = P("Bearer token · Bearer 權杖", "Bearer 權杖");
        BasicLabel.Text = P("Basic credentials · 基本驗證", "基本驗證帳密");

        TabRespBody.Header = P("Body · 內文", "內文");
        TabRespHeaders.Header = P("Response Headers · 回應標頭", "回應標頭");
        CopyLabel.Text = P("Copy · 複製", "複製");
        SaveRespLabel.Text = P("Save · 儲存", "儲存");
        RespHint.Text = _lastResponse is null ? P("Send a request to see the response here.", "發送請求後喺呢度睇回應。") : RespHint.Text;

        StoreHint.Text = P("Saved on disk · 已存到磁碟", "已存到磁碟");

        UpdateBodyModeLabels();
        UpdateAuthKindLabels();
    }

    // ── Combo builders ──────────────────────────────────────────────────────────

    private void BuildMethodBox()
    {
        MethodBox.Items.Clear();
        foreach (var m in ApiClientService.Methods) MethodBox.Items.Add(m);
        MethodBox.SelectedIndex = 0;
    }

    private void BuildBodyModeBox()
    {
        int sel = BodyModeBox.SelectedIndex < 0 ? 0 : BodyModeBox.SelectedIndex;
        BodyModeBox.Items.Clear();
        BodyModeBox.Items.Add(new ComboBoxItem { Content = P("None · 無", "無"), Tag = ApiBodyMode.None });
        BodyModeBox.Items.Add(new ComboBoxItem { Content = P("Raw JSON · 原始 JSON", "原始 JSON"), Tag = ApiBodyMode.RawJson });
        BodyModeBox.Items.Add(new ComboBoxItem { Content = P("Raw Text · 原始文字", "原始文字"), Tag = ApiBodyMode.RawText });
        BodyModeBox.Items.Add(new ComboBoxItem { Content = P("x-www-form-urlencoded · 表單", "表單"), Tag = ApiBodyMode.FormUrlEncoded });
        BodyModeBox.SelectedIndex = sel;
    }

    private void BuildAuthKindBox()
    {
        int sel = AuthKindBox.SelectedIndex < 0 ? 0 : AuthKindBox.SelectedIndex;
        AuthKindBox.Items.Clear();
        AuthKindBox.Items.Add(new ComboBoxItem { Content = P("No Auth · 無驗證", "無驗證"), Tag = ApiAuthKind.None });
        AuthKindBox.Items.Add(new ComboBoxItem { Content = P("Bearer Token · Bearer 權杖", "Bearer 權杖"), Tag = ApiAuthKind.Bearer });
        AuthKindBox.Items.Add(new ComboBoxItem { Content = P("Basic Auth · 基本驗證", "基本驗證"), Tag = ApiAuthKind.Basic });
        AuthKindBox.SelectedIndex = sel;
    }

    private void UpdateBodyModeLabels() { BuildBodyModeBox(); }
    private void UpdateAuthKindLabels() { BuildAuthKindBox(); }

    private void BuildEnvBox()
    {
        EnvBox.SelectionChanged -= Env_Changed;
        EnvBox.Items.Clear();
        EnvBox.Items.Add(new ComboBoxItem { Content = P("No environment · 無環境", "無環境"), Tag = (string?)null });
        foreach (var env in _svc.Workspace.Environments)
            EnvBox.Items.Add(new ComboBoxItem { Content = env.Name, Tag = env.Id });
        int idx = 0;
        var active = _svc.Workspace.ActiveEnvironmentId;
        for (int i = 0; i < EnvBox.Items.Count; i++)
            if (((ComboBoxItem)EnvBox.Items[i]).Tag as string == active) { idx = i; break; }
        EnvBox.SelectedIndex = idx;
        EnvBox.SelectionChanged += Env_Changed;
    }

    // ── Sidebar tree ────────────────────────────────────────────────────────────

    private void BuildTree()
    {
        _tree.Clear();
        foreach (var coll in _svc.Workspace.Collections)
        {
            var node = new TreeNode { Label = coll.Name, Badge = "▤", Collection = coll };
            foreach (var r in coll.Requests)
                node.Children.Add(new TreeNode { Label = r.Name, Badge = r.Method, Collection = coll, Request = r });
            _tree.Add(node);
        }
    }

    private void CollTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeNode { Request: { } req }) LoadRequestIntoEditor(req);
    }

    // ── Load / collect the editor ───────────────────────────────────────────────

    private void LoadRequestIntoEditor(ApiRequest req)
    {
        _loading = true;
        _req = req.Clone();

        MethodBox.SelectedItem = ApiClientService.Methods.Contains(_req.Method) ? _req.Method : "GET";
        UrlBox.Text = _req.Url;

        _params.Clear();
        foreach (var p in _req.QueryParams) _params.Add(p);
        _headers.Clear();
        foreach (var h in _req.Headers) _headers.Add(h);
        _form.Clear();
        foreach (var f in _req.FormFields) _form.Add(f);

        SelectComboByTag(BodyModeBox, _req.BodyMode);
        BodyBox.Text = _req.Body;

        SelectComboByTag(AuthKindBox, _req.AuthKind);
        BearerBox.Text = _req.AuthToken;
        BasicUserBox.Text = _req.AuthUser;
        BasicPassBox.Password = _req.AuthPassword;

        _loading = false;
        UpdateBodyVisibility();
        UpdateAuthVisibility();
    }

    private void CollectEditorIntoReq()
    {
        _req.Method = MethodBox.SelectedItem as string ?? "GET";
        _req.Url = UrlBox.Text ?? "";
        _req.QueryParams = _params.ToList();
        _req.Headers = _headers.ToList();
        _req.FormFields = _form.ToList();
        _req.BodyMode = (ApiBodyMode)((ComboBoxItem)BodyModeBox.SelectedItem!).Tag;
        _req.Body = BodyBox.Text ?? "";
        _req.AuthKind = (ApiAuthKind)((ComboBoxItem)AuthKindBox.SelectedItem!).Tag;
        _req.AuthToken = BearerBox.Text ?? "";
        _req.AuthUser = BasicUserBox.Text ?? "";
        _req.AuthPassword = BasicPassBox.Password ?? "";
    }

    private static void SelectComboByTag(ComboBox box, object tag)
    {
        for (int i = 0; i < box.Items.Count; i++)
            if (box.Items[i] is ComboBoxItem ci && Equals(ci.Tag, tag)) { box.SelectedIndex = i; return; }
        if (box.Items.Count > 0) box.SelectedIndex = 0;
    }

    // ── Row add/remove ──────────────────────────────────────────────────────────

    private void AddParam_Click(object s, RoutedEventArgs e) => _params.Add(new ApiKeyValue());
    private void RemoveParam_Click(object s, RoutedEventArgs e) { if ((s as FrameworkElement)?.Tag is ApiKeyValue kv) _params.Remove(kv); }
    private void AddHeader_Click(object s, RoutedEventArgs e) => _headers.Add(new ApiKeyValue());
    private void RemoveHeader_Click(object s, RoutedEventArgs e) { if ((s as FrameworkElement)?.Tag is ApiKeyValue kv) _headers.Remove(kv); }
    private void AddForm_Click(object s, RoutedEventArgs e) => _form.Add(new ApiKeyValue());
    private void RemoveForm_Click(object s, RoutedEventArgs e) { if ((s as FrameworkElement)?.Tag is ApiKeyValue kv) _form.Remove(kv); }

    // ── Body / auth mode switching ──────────────────────────────────────────────

    private void BodyMode_Changed(object s, SelectionChangedEventArgs e) { if (!_loading) UpdateBodyVisibility(); }

    private void UpdateBodyVisibility()
    {
        var mode = (BodyModeBox.SelectedItem as ComboBoxItem)?.Tag as ApiBodyMode? ?? ApiBodyMode.None;
        bool isForm = mode == ApiBodyMode.FormUrlEncoded;
        bool isRaw = mode is ApiBodyMode.RawJson or ApiBodyMode.RawText;
        BodyBox.Visibility = isRaw ? Visibility.Visible : Visibility.Collapsed;
        FormList.Visibility = isForm ? Visibility.Visible : Visibility.Collapsed;
        AddFormBtn.Visibility = isForm ? Visibility.Visible : Visibility.Collapsed;
        BeautifyBtn.Visibility = mode == ApiBodyMode.RawJson ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AuthKind_Changed(object s, SelectionChangedEventArgs e) { if (!_loading) UpdateAuthVisibility(); }

    private void UpdateAuthVisibility()
    {
        var kind = (AuthKindBox.SelectedItem as ComboBoxItem)?.Tag as ApiAuthKind? ?? ApiAuthKind.None;
        BearerPanel.Visibility = kind == ApiAuthKind.Bearer ? Visibility.Visible : Visibility.Collapsed;
        BasicPanel.Visibility = kind == ApiAuthKind.Basic ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Beautify_Click(object s, RoutedEventArgs e)
    {
        var pretty = ApiClientService.PrettyJson(BodyBox.Text ?? "");
        if (pretty is not null) BodyBox.Text = pretty;
    }

    // ── Environments ────────────────────────────────────────────────────────────

    private void Env_Changed(object s, SelectionChangedEventArgs e)
    {
        var id = (EnvBox.SelectedItem as ComboBoxItem)?.Tag as string;
        _svc.SetActiveEnvironment(_svc.Workspace.Environments.FirstOrDefault(env => env.Id == id));
    }

    private async void NewEnv_Click(object s, RoutedEventArgs e)
    {
        var name = await PromptText(P("New environment", "新環境"), P("Name · 名稱", "名稱"), "Production");
        if (string.IsNullOrWhiteSpace(name)) return;
        var env = _svc.AddEnvironment(name);
        _svc.SetActiveEnvironment(env);
        BuildEnvBox();
    }

    private async void ManageEnv_Click(object s, RoutedEventArgs e)
    {
        var env = _svc.ActiveEnvironment;
        if (env is null)
        {
            await Info(P("Pick or create an environment first.", "請先揀或建立一個環境。"));
            return;
        }
        var vars = new ObservableCollection<ApiKeyValue>(env.Variables.Select(v => new ApiKeyValue { Enabled = v.Enabled, Key = v.Key, Value = v.Value }));

        // Build editable rows in code.
        var panel = new StackPanel { Spacing = 10, MinWidth = 480 };
        var rows = new StackPanel { Spacing = 6 };
        void Rebuild()
        {
            rows.Children.Clear();
            foreach (var v in vars.ToList())
            {
                var grid = new Grid { ColumnSpacing = 8 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var k = new TextBox { Text = v.Key, PlaceholderText = "name", FontFamily = new FontFamily("Consolas") };
                k.TextChanged += (_, _) => v.Key = k.Text;
                var val = new TextBox { Text = v.Value, PlaceholderText = "value", FontFamily = new FontFamily("Consolas") };
                val.TextChanged += (_, _) => v.Value = val.Text;
                var del = new Button { Content = new FontIcon { Glyph = "", FontSize = 13 }, Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0) };
                del.Click += (_, _) => { vars.Remove(v); Rebuild(); };
                Grid.SetColumn(k, 0); Grid.SetColumn(val, 1); Grid.SetColumn(del, 2);
                grid.Children.Add(k); grid.Children.Add(val); grid.Children.Add(del);
                rows.Children.Add(grid);
            }
        }
        Rebuild();
        var addBtn = new Button { Content = P("Add variable · 加變數", "加變數") };
        addBtn.Click += (_, _) => { vars.Add(new ApiKeyValue()); Rebuild(); };
        var renameBtn = new Button { Content = P("Rename env · 改名", "改名") };
        var delEnvBtn = new Button { Content = P("Delete env · 刪除環境", "刪除環境") };
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        btnRow.Children.Add(addBtn); btnRow.Children.Add(renameBtn); btnRow.Children.Add(delEnvBtn);
        panel.Children.Add(new TextBlock { Text = P($"Variables in \"{env.Name}\". Use {{{{name}}}} in URL, headers, or body.", $"「{env.Name}」嘅變數。喺網址、標頭或內文用 {{{{name}}}}。"), TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        var sv = new ScrollViewer { Content = rows, MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(sv);
        panel.Children.Add(btnRow);

        bool deleteRequested = false;
        renameBtn.Click += async (_, _) =>
        {
            var nn = await PromptText(P("Rename environment", "環境改名"), P("Name · 名稱", "名稱"), env.Name);
            if (!string.IsNullOrWhiteSpace(nn)) { env.Name = nn.Trim(); }
        };

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Manage environment", "管理環境"),
            Content = panel,
            PrimaryButtonText = P("Save · 儲存", "儲存"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        // The inner Delete button flags deletion and closes the dialog.
        delEnvBtn.Click += (_, _) => { deleteRequested = true; dlg.Hide(); };

        var result = await dlg.ShowAsync();
        if (deleteRequested)
        {
            _svc.RemoveEnvironment(env);
            BuildEnvBox();
            return;
        }
        if (result != ContentDialogResult.Primary) { BuildEnvBox(); return; }
        env.Variables = vars.Where(v => !string.IsNullOrWhiteSpace(v.Key) || !string.IsNullOrWhiteSpace(v.Value)).ToList();
        _svc.Save();
        BuildEnvBox();
    }

    // ── Collections ─────────────────────────────────────────────────────────────

    private async void NewCollection_Click(object s, RoutedEventArgs e)
    {
        var name = await PromptText(P("New collection", "新集合"), P("Name · 名稱", "名稱"), "My API");
        if (string.IsNullOrWhiteSpace(name)) return;
        _svc.AddCollection(name);
        BuildTree();
    }

    private async void SaveRequest_Click(object s, RoutedEventArgs e)
    {
        CollectEditorIntoReq();
        if (_svc.Workspace.Collections.Count == 0) _svc.AddCollection(P("My API", "我的 API"));

        var collBox = new ComboBox { Header = P("Collection · 集合", "集合"), HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var c in _svc.Workspace.Collections) collBox.Items.Add(new ComboBoxItem { Content = c.Name, Tag = c.Id });
        collBox.SelectedIndex = 0;
        var nameBox = new TextBox { Header = P("Request name · 請求名稱", "請求名稱"), Text = string.IsNullOrWhiteSpace(_req.Name) || _req.Name == "New Request" ? (UrlBox.Text ?? "Request") : _req.Name };
        var panel = new StackPanel { Spacing = 10, MinWidth = 380 };
        panel.Children.Add(collBox); panel.Children.Add(nameBox);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Save request", "儲存請求"),
            Content = panel,
            PrimaryButtonText = P("Save · 儲存", "儲存"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var collId = (collBox.SelectedItem as ComboBoxItem)?.Tag as string;
        var coll = _svc.Workspace.Collections.FirstOrDefault(c => c.Id == collId);
        if (coll is null) return;
        _req.Name = string.IsNullOrWhiteSpace(nameBox.Text) ? "Request" : nameBox.Text.Trim();
        var saved = _svc.SaveRequestInto(coll, _req);
        _req = saved.Clone();
        BuildTree();
        await Info(P($"Saved \"{saved.Name}\".", $"已儲存「{saved.Name}」。"));
    }

    // ── Send ────────────────────────────────────────────────────────────────────

    private async void Send_Click(object s, RoutedEventArgs e)
    {
        CollectEditorIntoReq();
        if (string.IsNullOrWhiteSpace(_req.Url))
        {
            await Info(P("Enter a URL first.", "請先輸入網址。"));
            return;
        }

        _sendCts?.Cancel();
        _sendCts = new CancellationTokenSource();
        SendBtn.IsEnabled = false;
        SendBar.Visibility = Visibility.Visible;
        try
        {
            var resp = await _svc.SendAsync(_req, _sendCts.Token);
            _lastResponse = resp;
            ShowResponse(resp);
        }
        finally
        {
            SendBtn.IsEnabled = true;
            SendBar.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowResponse(ApiResponse resp)
    {
        StatusPill.Visibility = Visibility.Visible;
        if (!resp.Ok)
        {
            StatusText.Text = P("ERROR · 錯誤", "錯誤");
            StatusPill.Background = (Brush)Application.Current.Resources["SystemFillColorCriticalBackgroundBrush"];
            StatusText.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            TimeText.Text = $"{resp.ElapsedMs} ms";
            SizeText.Text = "";
            RespHint.Text = "";
            RespBodyBox.Text = resp.Error ?? P("Request failed.", "請求失敗。");
            RespHeadersList.ItemsSource = null;
            return;
        }

        StatusText.Text = $"{resp.StatusCode} {resp.ReasonPhrase}".Trim();
        var key = resp.IsSuccessRange ? "SystemFillColorSuccessBackgroundBrush"
            : resp.StatusCode is >= 300 and < 400 ? "SystemFillColorAttentionBackgroundBrush"
            : "SystemFillColorCriticalBackgroundBrush";
        var fg = resp.IsSuccessRange ? "SystemFillColorSuccessBrush"
            : resp.StatusCode is >= 300 and < 400 ? "SystemFillColorAttentionBrush"
            : "SystemFillColorCriticalBrush";
        StatusPill.Background = (Brush)Application.Current.Resources[key];
        StatusText.Foreground = (Brush)Application.Current.Resources[fg];

        TimeText.Text = $"⏱ {resp.ElapsedMs} ms";
        SizeText.Text = $"⤓ {ApiClientService.HumanSize(resp.SizeBytes)}";
        RespHint.Text = string.IsNullOrEmpty(resp.ContentType) ? "" : resp.ContentType;

        RespHeadersList.ItemsSource = resp.Headers;
        UpdateRespBody();
    }

    private void Pretty_Toggled(object s, RoutedEventArgs e) => UpdateRespBody();

    private void UpdateRespBody()
    {
        if (_lastResponse is not { Ok: true } resp) return;
        if (PrettyToggle.IsOn && resp.LooksJson)
        {
            var pretty = ApiClientService.PrettyJson(resp.Body);
            RespBodyBox.Text = pretty ?? resp.Body;
        }
        else RespBodyBox.Text = resp.Body;
    }

    private void Copy_Click(object s, RoutedEventArgs e)
    {
        try
        {
            var dp = new DataPackage();
            dp.SetText(RespBodyBox.Text ?? "");
            Clipboard.SetContent(dp);
        }
        catch { }
    }

    private async void SaveResponse_Click(object s, RoutedEventArgs e)
    {
        if (_lastResponse is not { Ok: true } resp) { await Info(P("No response to save yet.", "未有回應可儲存。")); return; }
        var ext = resp.LooksJson ? ".json" : ".txt";
        var path = await FileDialogs.SaveFileAsync("response" + ext, ext, ".txt");
        if (path is null) return;
        try { await System.IO.File.WriteAllTextAsync(path, RespBodyBox.Text ?? ""); }
        catch (Exception ex) { await Info(ex.Message); }
    }

    // ── Small dialog helpers ────────────────────────────────────────────────────

    private async Task<string?> PromptText(string title, string label, string placeholder)
    {
        var box = new TextBox { Header = label, PlaceholderText = placeholder };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = box,
            PrimaryButtonText = P("OK · 確定", "確定"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary ? box.Text : null;
    }

    private async Task Info(string message)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("API Client", "REST API 用戶端"),
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = P("OK · 確定", "確定"),
        };
        await dlg.ShowAsync();
    }

    /// <summary>側欄樹節點 · One node in the collections sidebar tree (collection or request).</summary>
    private sealed class TreeNode
    {
        public string Label { get; set; } = "";
        public string Badge { get; set; } = "";
        public ApiCollection? Collection { get; set; }
        public ApiRequest? Request { get; set; }
        public ObservableCollection<TreeNode> Children { get; } = new();
    }
}
