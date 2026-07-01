using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 連接器 · Connectors — manage external integrations (MCP servers, REST APIs, webhooks, databases)
/// that WinForge modules can use. Secrets are DPAPI-protected; each connector can be enabled, tested
/// and edited. Fully in-app &amp; bilingual.
/// </summary>
public sealed partial class ConnectorsModule : Page
{
    private bool _busy;

    public ConnectorsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); Refresh(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) { Render(); Refresh(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Connectors · 連接器";
        HeaderBlurb.Text = P(
            "Connect WinForge to external services — MCP servers, REST APIs, webhooks and databases. Secrets are encrypted (DPAPI); enabled connectors are available to other modules (e.g. the AI tools).",
            "將 WinForge 連去外部服務 — MCP 伺服器、REST API、webhook 同資料庫。密鑰會加密（DPAPI）；啟用咗嘅連接器可畀其他模組（例如 AI 工具）使用。");
        NewBtn.Content = P("Add connector", "新增連接器");
        RefreshBtn.Content = P("Refresh", "重新整理");
        EmptyText.Text = P("No connectors yet. Click “Add connector” to wire up an external service.",
            "未有連接器。撳「新增連接器」連接外部服務。");
    }

    private void Refresh()
    {
        try
        {
            var list = ConnectorService.Load();
            ConnList.ItemsSource = list;
            EmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex) { CrashLogger.Log("connectors.refresh", ex); }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private Connector? ById(object? tag) => tag is string id ? ConnectorService.Load().Find(c => c.Id == id) : null;

    private void Show(TweakResult r, string verb)
    {
        ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
        ResultBar.Message = r.Message is null ? verb : $"{verb} — {r.Message.Primary}";
        ResultBar.IsOpen = true;
    }

    private void Warn(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Title = P("Heads up", "提提你");
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }

    private async void New_Click(object sender, RoutedEventArgs e)
    {
        var c = new Connector { Name = P("New connector", "新連接器") };
        var (edited, secret) = await EditDialog(c, isNew: true);
        if (edited is null) return;
        ConnectorService.SaveConnector(edited, secret);
        Show(TweakResult.Ok($"Added “{edited.Name}”.", $"已新增「{edited.Name}」。"), P("Add connector", "新增連接器"));
        Refresh();
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        var c = ById((sender as Button)?.Tag);
        if (c is null) return;
        var (edited, secret) = await EditDialog(c, isNew: false);
        if (edited is null) return;
        ConnectorService.SaveConnector(edited, secret);
        Show(TweakResult.Ok($"Saved “{edited.Name}”.", $"已儲存「{edited.Name}」。"), P("Edit connector", "編輯連接器"));
        Refresh();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var c = ById((sender as Button)?.Tag);
        if (c is null) return;
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Delete connector?", "刪除連接器？"),
            Content = P($"Remove “{c.Name}”? Its stored secret is deleted too.", $"移除「{c.Name}」？儲存咗嘅密鑰都會刪除。"),
            PrimaryButtonText = P("Delete", "刪除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await SafeShow(dlg) != ContentDialogResult.Primary) return;
        ConnectorService.Delete(c.Id);
        Show(TweakResult.Ok($"Deleted “{c.Name}”.", $"已刪除「{c.Name}」。"), P("Delete", "刪除"));
        Refresh();
    }

    private void Enable_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch ts) return;
        var c = ById(ts.Tag);
        if (c is null || c.Enabled == ts.IsOn) return;
        c.Enabled = ts.IsOn;
        ConnectorService.SaveConnector(c, null);
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var c = ById((sender as Button)?.Tag);
        if (c is null) return;
        _busy = true;
        try { Show(await ConnectorService.TestAsync(c), P($"Test “{c.Name}”", $"測試「{c.Name}」")); }
        catch (Exception ex) { CrashLogger.Log("connectors.test", ex); Warn(ex.Message); }
        finally { _busy = false; }
    }

    // ───────────────────────── editor dialog ─────────────────────────

    private async Task<(Connector? connector, string? secret)> EditDialog(Connector src, bool isNew)
    {
        var c = new Connector
        {
            Id = src.Id, Name = src.Name, Kind = src.Kind, Auth = src.Auth, Endpoint = src.Endpoint,
            AuthHeaderName = src.AuthHeaderName, Username = src.Username, Headers = src.Headers,
            Notes = src.Notes, Enabled = src.Enabled, SecretBlob = src.SecretBlob, CreatedUtc = src.CreatedUtc,
        };

        var nameBox = new TextBox { Text = c.Name, Header = P("Name", "名稱") };
        var kindBox = new ComboBox { Header = P("Kind", "種類"), MinWidth = 220 };
        foreach (var kk in new[] { ConnectorKind.McpServer, ConnectorKind.RestApi, ConnectorKind.Webhook, ConnectorKind.Database, ConnectorKind.Custom })
            kindBox.Items.Add(kk.ToString());
        kindBox.SelectedItem = c.Kind.ToString();

        var endpointBox = new TextBox { Text = c.Endpoint, Header = P("Endpoint / URL (or command for stdio MCP)", "端點／網址（或 stdio MCP 指令）"), FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") };

        var authBox = new ComboBox { Header = P("Auth", "驗證"), MinWidth = 220 };
        foreach (var aa in new[] { ConnectorAuth.None, ConnectorAuth.Bearer, ConnectorAuth.ApiKeyHeader, ConnectorAuth.Basic })
            authBox.Items.Add(aa.ToString());
        authBox.SelectedItem = c.Auth.ToString();

        var headerNameBox = new TextBox { Text = c.AuthHeaderName, Header = P("API-key header name", "API-key 標頭名稱") };
        var userBox = new TextBox { Text = c.Username, Header = P("Username (Basic)", "使用者名稱（Basic）") };
        var secretBox = new PasswordBox { Header = P("Secret / token / key / password", "密鑰／權杖／密碼"), PlaceholderText = string.IsNullOrEmpty(c.SecretBlob) ? "" : "••••••••" };
        var headersBox = new TextBox { Text = c.Headers, Header = P("Extra headers (one \"Key: Value\" per line)", "額外標頭（每行一個「Key: Value」）"), AcceptsReturn = true, Height = 70, TextWrapping = TextWrapping.Wrap };
        var notesBox = new TextBox { Text = c.Notes, Header = P("Notes", "備註") };

        var panel = new StackPanel { Spacing = 10, MinWidth = 460 };
        panel.Children.Add(nameBox);
        panel.Children.Add(kindBox);
        panel.Children.Add(endpointBox);
        panel.Children.Add(authBox);
        panel.Children.Add(headerNameBox);
        panel.Children.Add(userBox);
        panel.Children.Add(secretBox);
        panel.Children.Add(headersBox);
        panel.Children.Add(notesBox);

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = isNew ? P("Add connector", "新增連接器") : P("Edit connector", "編輯連接器"),
            Content = new ScrollViewer { Content = panel, MaxHeight = 540, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            PrimaryButtonText = P("Save", "儲存"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        dlg.Resources["ContentDialogMaxWidth"] = 720.0;

        if (await SafeShow(dlg) != ContentDialogResult.Primary) return (null, null);

        c.Name = string.IsNullOrWhiteSpace(nameBox.Text) ? P("Unnamed", "未命名") : nameBox.Text.Trim();
        c.Kind = Enum.TryParse<ConnectorKind>(kindBox.SelectedItem as string, out var k) ? k : ConnectorKind.RestApi;
        c.Endpoint = endpointBox.Text.Trim();
        c.Auth = Enum.TryParse<ConnectorAuth>(authBox.SelectedItem as string, out var a) ? a : ConnectorAuth.None;
        c.AuthHeaderName = headerNameBox.Text.Trim();
        c.Username = userBox.Text.Trim();
        c.Headers = headersBox.Text;
        c.Notes = notesBox.Text.Trim();
        // Only overwrite the secret if the user typed a new one.
        string? secret = string.IsNullOrEmpty(secretBox.Password) ? null : secretBox.Password;
        return (c, secret);
    }

    private static async Task<ContentDialogResult> SafeShow(ContentDialog dlg)
    {
        try { return await dlg.ShowAsync(); }
        catch (Exception ex) { CrashLogger.Log("connectors.dialog", ex); return ContentDialogResult.None; }
    }
}
