using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 變數代入（envsubst 式）· Variable substitution module. A template with <c>$VAR</c> / <c>${VAR}</c>
/// placeholders plus an editable key/value table are combined into an output string. Supports
/// <c>${VAR:-default}</c>, <c>${VAR:?}</c>, an optional process-environment fallback and <c>$$ → $</c>
/// escaping, auto-detects referenced names, reports unresolved ones, and copies the result.
/// Pure managed, robust — never throws. Bilingual (English + 粵語).
/// </summary>
public sealed partial class EnvSubstModule : Page
{
    /// <summary>Row model for the variables table — notifies so live edits re-run substitution.</summary>
    public sealed class VarRow : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _value = string.Empty;

        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value ?? string.Empty; OnChanged(nameof(Name)); } }
        }
        public string Value
        {
            get => _value;
            set { if (_value != value) { _value = value ?? string.Empty; OnChanged(nameof(Value)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    private readonly ObservableCollection<VarRow> _rows = new();
    private bool _loaded;

    public EnvSubstModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { _loaded = true; VarsList.ItemsSource = _rows; Render(); RunSafely(); };
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Variable Substitute · 變數代入";
            HeaderBlurb.Text = P(
                "Fill $VAR and ${VAR} placeholders in a template with values from the table below — envsubst-style. Supports ${VAR:-default}, ${VAR:?} for required, an environment fallback, and $$ → $ escaping.",
                "喺範本入面用下面表格嘅值代入 $VAR 同 ${VAR} 佔位符 — envsubst 咁款。支援 ${VAR:-預設值}、${VAR:?}（必填）、環境變數後備同 $$ → $ 轉義。");
            TemplateTitle.Text = P("Template", "範本");
            DetectBtn.Content = P("Auto-detect variables", "自動偵測變數");
            OptionsTitle.Text = P("Options", "選項");
            EnvChk.Content = P("Also read real process environment variables as a fallback", "冇值時用真實嘅系統環境變數做後備");
            EscapeChk.Content = P("Escape $$ → $ (literal dollar sign)", "$$ → $ 轉義（表示一個真嘅 $）");
            VarsTitle.Text = P("Variables", "變數");
            AddBtn.Content = P("Add", "新增");
            ClearBtn.Content = P("Clear", "清空");
            ColName.Text = P("Name", "名稱");
            ColValue.Text = P("Value", "值");
            OutputTitle.Text = P("Output", "輸出");
            RunBtn.Content = P("Substitute", "代入");
            CopyBtn.Content = P("Copy", "複製");
            if (string.IsNullOrEmpty(TemplateBox.Text))
                TemplateBox.PlaceholderText = P("e.g.  Hello ${NAME:-world}, port=$PORT", "例如  Hello ${NAME:-world}, port=$PORT");
        }
        catch { /* never throw */ }
        RunSafely();
    }

    // Snapshot the table into a case-sensitive map (last non-empty name wins).
    private Dictionary<string, string> BuildMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var r in _rows)
            {
                if (r == null) continue;
                var name = (r.Name ?? string.Empty).Trim();
                if (name.Length == 0) continue;
                map[name] = r.Value ?? string.Empty;
            }
        }
        catch { /* never throw */ }
        return map;
    }

    private void RunSafely()
    {
        if (!_loaded) return;
        try
        {
            var map = BuildMap();
            var res = EnvSubstService.Substitute(
                TemplateBox.Text ?? string.Empty,
                map,
                EnvChk.IsChecked == true,
                EscapeChk.IsChecked == true);

            OutputBox.Text = res.Output;
            UpdateReport(res);
        }
        catch { /* never throw */ }
    }

    private void UpdateReport(EnvSubstService.Result res)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append(P($"Referenced {res.Referenced.Count} variable(s).", $"引用咗 {res.Referenced.Count} 個變數。"));
            if (res.Unresolved.Count > 0)
                sb.Append(' ').Append(P($"Unresolved: {string.Join(", ", res.Unresolved)}.", $"未解析：{string.Join("、", res.Unresolved)}。"));
            if (res.Missing.Count > 0)
                sb.Append(' ').Append(P($"Required but missing (${{VAR:?}}): {string.Join(", ", res.Missing)}.", $"必填但缺少（${{VAR:?}}）：{string.Join("、", res.Missing)}。"));
            ReportText.Text = sb.ToString();

            if (res.Missing.Count > 0)
                ShowInfo(InfoBarSeverity.Error, P("Required variables are missing.", "有必填變數缺少。"));
            else if (res.Unresolved.Count > 0)
                ShowInfo(InfoBarSeverity.Warning, P("Some variables were left unresolved.", "有啲變數解析唔到。"));
            else
                Info.IsOpen = false;
        }
        catch { /* never throw */ }
    }

    private void ShowInfo(InfoBarSeverity sev, string msg)
    {
        try { Info.Severity = sev; Info.Message = msg; Info.IsOpen = true; }
        catch { /* never throw */ }
    }

    private void Template_Changed(object sender, TextChangedEventArgs e) => RunSafely();

    private void Options_Changed(object sender, RoutedEventArgs e) => RunSafely();

    private void Detect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var names = EnvSubstService.DetectNames(TemplateBox.Text ?? string.Empty);
            var existing = new HashSet<string>(_rows.Select(r => (r.Name ?? string.Empty).Trim()), StringComparer.Ordinal);
            int added = 0;
            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name) || existing.Contains(name)) continue;
                var row = new VarRow { Name = name, Value = string.Empty };
                row.PropertyChanged += Row_Changed;
                _rows.Add(row);
                existing.Add(name);
                added++;
            }
            ShowInfo(InfoBarSeverity.Success,
                added > 0 ? P($"Added {added} variable(s).", $"新增咗 {added} 個變數。")
                          : P("No new variables found.", "冇搵到新變數。"));
            RunSafely();
        }
        catch { /* never throw */ }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var row = new VarRow();
            row.PropertyChanged += Row_Changed;
            _rows.Add(row);
        }
        catch { /* never throw */ }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            foreach (var r in _rows) r.PropertyChanged -= Row_Changed;
            _rows.Clear();
            RunSafely();
        }
        catch { /* never throw */ }
    }

    private void RemoveRow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && fe.Tag is VarRow row)
            {
                row.PropertyChanged -= Row_Changed;
                _rows.Remove(row);
                RunSafely();
            }
        }
        catch { /* never throw */ }
    }

    private void Row_Changed(object? sender, PropertyChangedEventArgs e) => RunSafely();

    private void Run_Click(object sender, RoutedEventArgs e) => RunSafely();

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pkg = new DataPackage();
            pkg.SetText(OutputBox.Text ?? string.Empty);
            Clipboard.SetContent(pkg);
            ShowInfo(InfoBarSeverity.Success, P("Output copied to clipboard.", "已複製輸出到剪貼簿。"));
        }
        catch { /* never throw */ }
    }
}
