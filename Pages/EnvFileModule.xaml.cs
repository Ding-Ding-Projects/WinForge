using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// .env 編輯器／轉換器 · Dotenv editor / converter. Parse a raw .env (KEY=VALUE, "# comments",
/// quoted values, optional `export `) into an editable key/value list; validate for duplicate keys,
/// invalid names and unquoted-space values; convert to shell / JSON / docker / canonical .env; load
/// and save via FileDialogs. Pure managed C#. Bilingual. Robust — never throws.
/// </summary>
public sealed partial class EnvFileModule : Page
{
    private readonly ObservableCollection<EnvFileService.EnvPair> _pairs = new();

    public EnvFileModule()
    {
        InitializeComponent();
        PairsList.ItemsSource = _pairs;
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Dotenv Editor · .env 編輯器";
        HeaderBlurb.Text = P("Paste a .env file, edit the key/value pairs, catch problems, then convert to shell exports, JSON, docker --env args or a clean .env.",
            "貼一個 .env 檔案入嚟，改鍵值對、揪返啲問題，再轉做 shell export、JSON、docker --env 引數或者乾淨嘅 .env。");
        RawTitle.Text = P("Raw .env text", "原始 .env 文字");
        RawBox.PlaceholderText = P("# paste .env here\nexport API_KEY=\"abc123\"\nPORT=8080", "# 喺度貼 .env\nexport API_KEY=\"abc123\"\nPORT=8080");
        ParseBtn.Content = P("Parse", "解析");
        LoadBtn.Content = P("Load…", "載入…");
        SaveBtn.Content = P("Save…", "儲存…");
        PairsTitle.Text = P("Key / value pairs", "鍵 / 值");
        AddBtn.Content = P("Add", "新增");
        ConvertTitle.Text = P("Convert", "轉換");
        ShellBtn.Content = P("→ shell", "→ shell");
        JsonBtn.Content = P("→ JSON", "→ JSON");
        DockerBtn.Content = P("→ docker", "→ docker");
        EnvBtn.Content = P("→ .env", "→ .env");
        CopyBtn.Content = P("Copy output", "複製輸出");
        if (StatusText.Text.Length == 0)
            StatusText.Text = P("Ready.", "準備就緒。");
    }

    private void SyncRawToList()
    {
        try
        {
            _pairs.Clear();
            foreach (var p in EnvFileService.Parse(RawBox.Text))
                _pairs.Add(p);
            ShowWarnings();
        }
        catch { /* never throw */ }
    }

    private void ShowWarnings()
    {
        try
        {
            var warns = EnvFileService.Validate(_pairs, P);
            if (warns.Count == 0)
            {
                WarningsBlock.Visibility = Visibility.Collapsed;
                WarningsBlock.Text = "";
            }
            else
            {
                WarningsBlock.Text = "⚠ " + string.Join("\n⚠ ", warns);
                WarningsBlock.Visibility = Visibility.Visible;
            }
        }
        catch { /* never throw */ }
    }

    private void Parse_Click(object sender, RoutedEventArgs e)
    {
        SyncRawToList();
        StatusText.Text = P($"Parsed {_pairs.Count} pair(s).", $"解析咗 {_pairs.Count} 對。");
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        _pairs.Add(new EnvFileService.EnvPair { Key = "", Value = "" });
        ShowWarnings();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && fe.Tag is EnvFileService.EnvPair p)
            {
                _pairs.Remove(p);
                ShowWarnings();
            }
        }
        catch { /* never throw */ }
    }

    private async void Load_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var filters = new[]
            {
                new FileDialogs.Filter("Env files", "*.env;.env;*.env.*;*.txt"),
                new FileDialogs.Filter("All files", "*.*"),
            };
            var path = await FileDialogs.OpenFileAsync(filters, P("Open .env file", "開啟 .env 檔案"));
            if (path is null) return;
            var (ok, text, err) = await EnvFileService.LoadAsync(path);
            if (!ok)
            {
                StatusText.Text = P("Load failed: ", "載入失敗：") + err;
                return;
            }
            RawBox.Text = text;
            SyncRawToList();
            StatusText.Text = P($"Loaded {_pairs.Count} pair(s) from ", $"由呢度載入咗 {_pairs.Count} 對：") + path;
        }
        catch (Exception ex) { StatusText.Text = P("Load failed: ", "載入失敗：") + ex.Message; }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var filters = new[]
            {
                new FileDialogs.Filter("Env file", "*.env"),
                new FileDialogs.Filter("All files", "*.*"),
            };
            var path = await FileDialogs.SaveFileAsync(".env", filters, "env", P("Save .env file", "儲存 .env 檔案"));
            if (path is null) return;
            var text = EnvFileService.ToEnv(_pairs);
            var (ok, err) = await EnvFileService.SaveAsync(path, text);
            StatusText.Text = ok
                ? P("Saved to ", "已儲存到 ") + path
                : P("Save failed: ", "儲存失敗：") + err;
        }
        catch (Exception ex) { StatusText.Text = P("Save failed: ", "儲存失敗：") + ex.Message; }
    }

    private void Shell_Click(object sender, RoutedEventArgs e) => Emit(EnvFileService.ToShell(_pairs), "shell");
    private void Json_Click(object sender, RoutedEventArgs e) => Emit(EnvFileService.ToJson(_pairs), "JSON");
    private void Docker_Click(object sender, RoutedEventArgs e) => Emit(EnvFileService.ToDocker(_pairs), "docker");
    private void Env_Click(object sender, RoutedEventArgs e) => Emit(EnvFileService.ToEnv(_pairs), ".env");

    private void Emit(string text, string kind)
    {
        try
        {
            ShowWarnings();
            OutputBox.Text = text;
            StatusText.Text = P($"Converted to {kind}.", $"已轉做 {kind}。");
        }
        catch (Exception ex) { StatusText.Text = P("Convert failed: ", "轉換失敗：") + ex.Message; }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = OutputBox.Text ?? "";
            if (text.Length == 0)
            {
                StatusText.Text = P("Nothing to copy — convert first.", "冇嘢好複製 — 先轉換。");
                return;
            }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            StatusText.Text = P("Output copied to clipboard.", "輸出已複製到剪貼簿。");
        }
        catch (Exception ex) { StatusText.Text = P("Copy failed: ", "複製失敗：") + ex.Message; }
    }
}
