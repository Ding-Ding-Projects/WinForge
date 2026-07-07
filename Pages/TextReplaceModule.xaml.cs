using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 尋找及取代 · Multi-rule find &amp; replace. An input box, an ordered list of rules (each literal or
/// regex, optionally case-insensitive), and a live output box with a total-replacement count. Pure
/// managed (System.Text.RegularExpressions, 1s match timeout). Bilingual; never throws on bad regex.
/// </summary>
public sealed partial class TextReplaceModule : Page
{
    private readonly ObservableCollection<RuleRow> _rules = new();

    public TextReplaceModule()
    {
        InitializeComponent();
        RulesList.ItemsSource = _rules;
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_rules.Count == 0)
        {
            AddRow(new TextReplaceService.Rule { Find = "", Replace = "" });
        }
        Render();
        ReApply();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        foreach (var row in _rules) row.Changed -= OnRuleChanged;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Find & Replace · 尋找及取代";
        HeaderBlurb.Text = P("Run a stack of find-and-replace rules over your text — literal or regex, in order. Everything stays on this PC.",
            "喺你嘅文字上面順序行一疊尋找／取代規則 — 可以係普通字或者正規表達式。全部喺呢部電腦度處理。");
        InputLabel.Text = P("Input text", "輸入文字");
        RulesLabel.Text = P("Replace rules", "取代規則");
        AddRuleBtn.Content = P("Add rule", "加規則");
        OutputLabel.Text = P("Output", "輸出");
        CopyBtn.Content = P("Copy output", "複製輸出");

        foreach (var row in _rules) row.RefreshLabels();
        UpdateTotal(_lastTotal, _lastError);
    }

    // ---- rule list management --------------------------------------------

    private void AddRow(TextReplaceService.Rule rule)
    {
        var row = new RuleRow(rule);
        row.Changed += OnRuleChanged;
        row.RefreshLabels();
        _rules.Add(row);
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        AddRow(new TextReplaceService.Rule { Find = "", Replace = "" });
        ReApply();
    }

    private void RemoveRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RuleRow row })
        {
            row.Changed -= OnRuleChanged;
            _rules.Remove(row);
            if (_rules.Count == 0) AddRow(new TextReplaceService.Rule { Find = "", Replace = "" });
            ReApply();
        }
    }

    private void OnRuleChanged(object? sender, EventArgs e) => ReApply();

    // ---- apply / output --------------------------------------------------

    private void Input_TextChanged(object sender, TextChangedEventArgs e) => ReApply();

    private int _lastTotal;
    private bool _lastError;

    private void ReApply()
    {
        try
        {
            var rules = new List<TextReplaceService.Rule>(_rules.Count);
            foreach (var row in _rules) rules.Add(row.Model);

            var result = TextReplaceService.Apply(InputBox?.Text ?? "", rules);

            if (OutputBox != null) OutputBox.Text = result.Output;
            foreach (var row in _rules) row.RefreshStatus();

            _lastTotal = result.TotalReplacements;
            _lastError = result.AnyError;
            UpdateTotal(_lastTotal, _lastError);
        }
        catch (Exception)
        {
            // The engine is never-throw, but guard the UI path anyway.
            UpdateTotal(0, true);
        }
    }

    private void UpdateTotal(int total, bool anyError)
    {
        if (TotalText == null) return;
        if (anyError)
            TotalText.Text = P($"{total} replacement(s) — some rules have errors (see below).",
                               $"取代咗 {total} 處 — 有規則出錯（見下面）。");
        else
            TotalText.Text = P($"{total} replacement(s).", $"取代咗 {total} 處。");
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pkg = new DataPackage();
            pkg.SetText(OutputBox?.Text ?? "");
            Clipboard.SetContent(pkg);
        }
        catch (Exception)
        {
            // Clipboard can transiently fail; ignore.
        }
    }

    // ---- bindable row ----------------------------------------------------

    /// <summary>
    /// A ListView-bound wrapper over a <see cref="TextReplaceService.Rule"/>. Uses classic {Binding}
    /// (no x:Bind), raises Changed on any edit so the module re-applies live, and exposes bilingual
    /// header/label strings refreshed on language change.
    /// </summary>
    public sealed class RuleRow : INotifyPropertyChanged
    {
        public RuleRow(TextReplaceService.Rule model) => Model = model;

        public TextReplaceService.Rule Model { get; }

        public event EventHandler? Changed;
        public event PropertyChangedEventHandler? PropertyChanged;

        private string P(string en, string zh) => Loc.I.Pick(en, zh);

        public string Find
        {
            get => Model.Find;
            set { if (Model.Find != value) { Model.Find = value ?? ""; OnProp(); Changed?.Invoke(this, EventArgs.Empty); } }
        }

        public string Replace
        {
            get => Model.Replace;
            set { if (Model.Replace != value) { Model.Replace = value ?? ""; OnProp(); Changed?.Invoke(this, EventArgs.Empty); } }
        }

        public bool Regex
        {
            get => Model.Regex;
            set { if (Model.Regex != value) { Model.Regex = value; OnProp(); Changed?.Invoke(this, EventArgs.Empty); } }
        }

        public bool IgnoreCase
        {
            get => Model.IgnoreCase;
            set { if (Model.IgnoreCase != value) { Model.IgnoreCase = value; OnProp(); Changed?.Invoke(this, EventArgs.Empty); } }
        }

        // Bilingual labels (refreshed via RefreshLabels on language change).
        public string FindHeader => P("Find", "尋找");
        public string ReplaceHeader => P("Replace with", "取代為");
        public string RegexLabel => P("Regex", "正規表達式");
        public string IgnoreCaseLabel => P("Ignore case", "唔理大細楷");
        public string RemoveLabel => P("Remove", "移除");

        public string HitsText => Model.Hits > 0 ? P($"{Model.Hits} hit(s)", $"命中 {Model.Hits} 處") : "";

        public string Status => P(Model.ErrorEn ?? "", Model.ErrorZh ?? "");
        public Visibility StatusVisibility =>
            string.IsNullOrEmpty(Model.ErrorEn) ? Visibility.Collapsed : Visibility.Visible;

        public void RefreshLabels()
        {
            OnProp(nameof(FindHeader));
            OnProp(nameof(ReplaceHeader));
            OnProp(nameof(RegexLabel));
            OnProp(nameof(IgnoreCaseLabel));
            OnProp(nameof(RemoveLabel));
            OnProp(nameof(HitsText));
            OnProp(nameof(Status));
        }

        public void RefreshStatus()
        {
            OnProp(nameof(HitsText));
            OnProp(nameof(Status));
            OnProp(nameof(StatusVisibility));
        }

        private void OnProp([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
