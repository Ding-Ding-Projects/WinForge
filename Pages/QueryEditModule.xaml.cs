using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 網址查詢字串編輯器 · URL Query Editor — paste a full URL or a raw query string, edit its key/value pairs
/// (percent-decoded, add / remove / reorder / toggle on-off), then rebuild the URL or query with proper
/// percent-encoding, preserving scheme/host/path/fragment. Pure managed C#. Robust; never throws. Bilingual (粵語).
/// </summary>
public sealed partial class QueryEditModule : Page
{
    /// <summary>An editable row backing the ListView — INotifyPropertyChanged so classic TwoWay {Binding} works.</summary>
    public sealed class PairRow : INotifyPropertyChanged
    {
        private string _key = "";
        private string _value = "";
        private bool _enabled = true;

        public bool HasEquals = true;

        public string Key
        {
            get => _key;
            set { if (_key != value) { _key = value ?? ""; Raise(nameof(Key)); Owner?.OnRowEdited(); } }
        }

        public string Value
        {
            get => _value;
            set { if (_value != value) { _value = value ?? ""; HasEquals = true; Raise(nameof(Value)); Owner?.OnRowEdited(); } }
        }

        public bool Enabled
        {
            get => _enabled;
            set { if (_enabled != value) { _enabled = value; Raise(nameof(Enabled)); Owner?.OnRowEdited(); } }
        }

        public QueryEditModule? Owner;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    private readonly ObservableCollection<PairRow> _rows = new();
    private QueryEditService.UrlParts _parts = new();
    private bool _suppress;

    public QueryEditModule()
    {
        InitializeComponent();
        PairsList.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); if (_rows.Count == 0) Rebuild(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = P("URL Query Editor", "網址查詢編輯器");
            HeaderBlurb.Text = P("Paste a full URL or a raw query string, then edit its parameters — decode, re-order, toggle and re-encode — and copy the rebuilt link. Everything happens locally.",
                "貼一條完整網址或者查詢字串，跟住編輯佢啲參數 — 解碼、重新排序、開關同重新編碼 — 再複製砌好嘅連結。全部喺本機處理。");
            InputLabel.Text = P("URL or query string", "網址或查詢字串");
            ParseBtn.Content = P("Parse", "解析");
            AddBtn.Content = P("Add parameter", "加參數");
            SortBtn.Content = P("Sort keys", "排序鍵");
            ParamsLabel.Text = P("Parameters", "參數");
            ColOn.Text = P("On", "開");
            ColKey.Text = P("Key", "鍵");
            ColValue.Text = P("Value", "值");
            EmptyHint.Text = P("No parameters yet — paste a URL above and press Parse, or add one.", "未有參數 — 喺上面貼個網址㩒解析，或者自己加一個。");
            ResultLabel.Text = P("Result", "結果");
            CopyUrlBtn.Content = P("Copy URL", "複製網址");
            CopyQueryBtn.Content = P("Copy query only", "只複製查詢");
            DecodedToggle.Content = DecodedToggle.IsChecked == true ? P("Showing decoded", "顯示解碼") : P("Showing encoded", "顯示編碼");
            SchemeLbl.Text = P("Scheme", "協定");
            HostLbl.Text = P("Host", "主機");
            PathLbl.Text = P("Path", "路徑");
            FragLbl.Text = P("Fragment", "片段");
        }
        catch { }
    }

    // ---- input ----

    private void Parse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var (parts, pairs) = QueryEditService.Parse(InputBox.Text);
            _parts = parts;
            _suppress = true;
            _rows.Clear();
            foreach (var p in pairs)
                _rows.Add(new PairRow { Key = p.Key, Value = p.Value, Enabled = true, HasEquals = p.HasEquals, Owner = this });
            _suppress = false;
            ShowInfo(P($"Parsed {pairs.Count} parameter(s).", $"解析咗 {pairs.Count} 個參數。"), InfoBarSeverity.Success);
            Rebuild();
        }
        catch (Exception ex) { ShowInfo(P("Could not parse that input.", "解析唔到嗰個輸入。") + " " + ex.Message, InfoBarSeverity.Error); }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _rows.Add(new PairRow { Key = "key", Value = "value", Enabled = true, HasEquals = true, Owner = this });
            Rebuild();
        }
        catch { }
    }

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sorted = _rows.OrderBy(r => r.Key, StringComparer.OrdinalIgnoreCase).ToList();
            _suppress = true;
            _rows.Clear();
            foreach (var r in sorted) _rows.Add(r);
            _suppress = false;
            Rebuild();
        }
        catch { }
    }

    private void Up_Click(object sender, RoutedEventArgs e) => Move(sender, -1);
    private void Down_Click(object sender, RoutedEventArgs e) => Move(sender, +1);

    private void Move(object sender, int delta)
    {
        try
        {
            if (sender is FrameworkElement fe && fe.Tag is PairRow row)
            {
                int i = _rows.IndexOf(row);
                int j = i + delta;
                if (i >= 0 && j >= 0 && j < _rows.Count)
                {
                    _suppress = true;
                    _rows.Move(i, j);
                    _suppress = false;
                    Rebuild();
                }
            }
        }
        catch { }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && fe.Tag is PairRow row)
            {
                _rows.Remove(row);
                Rebuild();
            }
        }
        catch { }
    }

    /// <summary>Called by a row when the user edits a key/value/toggle — live-rebuilds the result.</summary>
    internal void OnRowEdited()
    {
        if (_suppress) return;
        Rebuild();
    }

    // ---- output ----

    private void Rebuild()
    {
        try
        {
            var enabled = _rows.Where(r => r.Enabled)
                               .Select(r => new QueryEditService.Pair { Key = r.Key, Value = r.Value, HasEquals = r.HasEquals })
                               .ToList();

            bool decoded = DecodedToggle.IsChecked == true;
            string url = QueryEditService.BuildUrl(_parts, enabled);
            ResultBox.Text = decoded ? QueryEditService.SafeDecode(url) : url;

            SchemeVal.Text = string.IsNullOrEmpty(_parts.Scheme) ? "—" : _parts.Scheme;
            HostVal.Text = string.IsNullOrEmpty(_parts.Authority) ? "—" : _parts.Authority;
            PathVal.Text = string.IsNullOrEmpty(_parts.Path) ? "—" : _parts.Path;
            FragVal.Text = string.IsNullOrEmpty(_parts.Fragment) ? "—" : _parts.Fragment;

            bool empty = _rows.Count == 0;
            EmptyHint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

            DecodedToggle.Content = decoded ? P("Showing decoded", "顯示解碼") : P("Showing encoded", "顯示編碼");
        }
        catch { }
    }

    private void DecodedToggle_Click(object sender, RoutedEventArgs e) => Rebuild();

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        CopyText(ResultBox.Text, P("URL copied to clipboard.", "網址已複製到剪貼簿。"));
    }

    private void CopyQuery_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var enabled = _rows.Where(r => r.Enabled)
                               .Select(r => new QueryEditService.Pair { Key = r.Key, Value = r.Value, HasEquals = r.HasEquals });
            string q = QueryEditService.BuildQuery(enabled);
            if (DecodedToggle.IsChecked == true) q = QueryEditService.SafeDecode(q);
            CopyText(q, P("Query string copied to clipboard.", "查詢字串已複製到剪貼簿。"));
        }
        catch { }
    }

    private void CopyText(string? text, string ok)
    {
        try
        {
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text ?? "");
            Clipboard.SetContent(dp);
            ShowInfo(ok, InfoBarSeverity.Success);
        }
        catch (Exception ex) { ShowInfo(P("Copy failed.", "複製失敗。") + " " + ex.Message, InfoBarSeverity.Error); }
    }

    private void ShowInfo(string msg, InfoBarSeverity sev)
    {
        try
        {
            Info.Severity = sev;
            Info.Message = msg;
            Info.IsOpen = true;
        }
        catch { }
    }
}
