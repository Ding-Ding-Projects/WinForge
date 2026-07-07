using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 具名空間 UUID · Namespaced UUID (RFC 4122 v3 MD5 / v5 SHA-1) generator. Pick a namespace
/// (DNS/URL/OID/X500/Custom), a name and a version; get a deterministic UUID. Bulk mode maps
/// each line to a UUID. Pure managed, never redirects, bilingual (粵語 / English).
/// </summary>
public sealed partial class UuidV5Module : Page
{
    private readonly ObservableCollection<string> _bulkRows = new();
    private bool _suppress;

    public UuidV5Module()
    {
        InitializeComponent();
        BulkList.DataContext = _bulkRows;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _suppress = true;
        BuildCombos();
        _suppress = false;
        Render();
        Recompute();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void BuildCombos()
    {
        if (NamespaceCombo.Items.Count == 0)
        {
            NamespaceCombo.Items.Add("DNS  ·  6ba7b810-9dad-11d1-80b4-00c04fd430c8");
            NamespaceCombo.Items.Add("URL  ·  6ba7b811-9dad-11d1-80b4-00c04fd430c8");
            NamespaceCombo.Items.Add("OID  ·  6ba7b812-9dad-11d1-80b4-00c04fd430c8");
            NamespaceCombo.Items.Add("X500  ·  6ba7b814-9dad-11d1-80b4-00c04fd430c8");
            NamespaceCombo.Items.Add("Custom");
            NamespaceCombo.SelectedIndex = 0;
        }
        if (VersionCombo.Items.Count == 0)
        {
            VersionCombo.Items.Add("v5  ·  SHA-1");
            VersionCombo.Items.Add("v3  ·  MD5");
            VersionCombo.SelectedIndex = 0;
        }
    }

    private void Render()
    {
        Header.Title = "Namespaced UUID · 具名空間 UUID";
        HeaderBlurb.Text = P("Generate deterministic RFC 4122 name-based UUIDs (v5 SHA-1 or v3 MD5). Same namespace + name always yields the same UUID.",
            "整出穩定嘅 RFC 4122 具名 UUID（v5 SHA-1 或 v3 MD5）。同一個命名空間加同一個名，永遠出同一個 UUID。");
        NamespaceLabel.Text = P("Namespace", "命名空間");
        VersionLabel.Text = P("Version", "版本");
        NameLabel.Text = P("Name", "名稱");
        CopyBtn.Content = P("Copy", "複製");
        BulkTitle.Text = P("Bulk mode", "批量模式");
        BulkBlurb.Text = P("One name per line — get one UUID per line, using the namespace and version above.",
            "每行一個名 — 用上面嘅命名空間同版本，逐行整一個 UUID。");
        BulkRunBtn.Content = P("Generate", "生成");
        BulkCopyBtn.Content = P("Copy all", "全部複製");
        Recompute();
    }

    private bool TryGetNamespace(out Guid ns)
    {
        int idx = NamespaceCombo.SelectedIndex;
        switch (idx)
        {
            case 0: ns = UuidV5Service.NamespaceDns; return true;
            case 1: ns = UuidV5Service.NamespaceUrl; return true;
            case 2: ns = UuidV5Service.NamespaceOid; return true;
            case 3: ns = UuidV5Service.NamespaceX500; return true;
            default:
                if (UuidV5Service.TryParseNamespace(CustomGuidBox.Text, out ns)) return true;
                ns = Guid.Empty;
                return false;
        }
    }

    private int SelectedVersion() => VersionCombo.SelectedIndex == 1 ? 3 : 5;

    private void Namespace_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        CustomGuidBox.Visibility = NamespaceCombo.SelectedIndex == 4 ? Visibility.Visible : Visibility.Collapsed;
        Recompute();
    }

    private void Inputs_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        Recompute();
    }

    private void Recompute()
    {
        try
        {
            if (!TryGetNamespace(out Guid ns))
            {
                ResultBox.Text = string.Empty;
                StatusText.Text = P("Enter a valid custom namespace GUID (e.g. 00000000-0000-0000-0000-000000000000).",
                    "請輸入有效嘅自訂命名空間 GUID（例如 00000000-0000-0000-0000-000000000000）。");
                return;
            }
            int ver = SelectedVersion();
            Guid result = UuidV5Service.Compute(ns, NameBox.Text ?? string.Empty, ver);
            ResultBox.Text = result.ToString("D");
            StatusText.Text = P($"UUID v{ver} — deterministic for this namespace + name.",
                $"UUID v{ver} — 呢個命名空間加名嘅穩定結果。");
        }
        catch
        {
            ResultBox.Text = string.Empty;
            StatusText.Text = P("Could not compute — check the inputs.", "整唔到 — 請檢查輸入。");
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = ResultBox.Text ?? string.Empty;
            if (text.Length == 0) return;
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch
        {
            StatusText.Text = P("Copy failed.", "複製失敗。");
        }
    }

    private void BulkRun_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _bulkRows.Clear();
            if (!TryGetNamespace(out Guid ns))
            {
                StatusText.Text = P("Enter a valid custom namespace GUID first.", "請先輸入有效嘅自訂命名空間 GUID。");
                return;
            }
            List<string> rows = UuidV5Service.ComputeBulk(ns, BulkInput.Text, SelectedVersion());
            foreach (string r in rows) _bulkRows.Add(r);
            StatusText.Text = P($"Generated {rows.Count} UUID(s).", $"已生成 {rows.Count} 個 UUID。");
        }
        catch
        {
            StatusText.Text = P("Bulk generation failed.", "批量生成失敗。");
        }
    }

    private void BulkCopy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_bulkRows.Count == 0) return;
            var pkg = new DataPackage();
            pkg.SetText(string.Join(Environment.NewLine, _bulkRows));
            Clipboard.SetContent(pkg);
            StatusText.Text = P("All rows copied.", "已全部複製。");
        }
        catch
        {
            StatusText.Text = P("Copy failed.", "複製失敗。");
        }
    }
}
