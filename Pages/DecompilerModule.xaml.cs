using System;
using System.Collections.Generic;
using System.IO;
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
/// 原生 .NET 組件瀏覽器與反編譯器（ILSpy 風格）· Native ILSpy-style .NET assembly browser &amp; decompiler.
/// 開一個受控 .dll／.exe，瀏覽「命名空間 → 型別 → 成員」樹，揀任何項目就反編譯做 C#（或切去 IL 反組譯），
/// 喺唯讀等寬程式碼視圖睇，仲可以搜尋、睇組件後設資料／參考組件／資源、另存 .cs。完全用受控嘅
/// ICSharpCode.Decompiler 引擎（ILSpy 自己嘅）—— 唔會啟動／bundle 任何外部工具。
/// Open a managed .dll/.exe, browse the namespace → type → member tree, decompile any item to C#
/// (or toggle to IL disassembly) in a read-only monospace view, search by name, inspect assembly
/// metadata / referenced assemblies / resources, and save the decompiled .cs. Pure managed via
/// ICSharpCode.Decompiler — no external tool is launched or bundled.
/// </summary>
public sealed partial class DecompilerModule : Page
{
    private readonly DecompilerService _svc = new();
    private bool _showIl;
    private TreeNode? _selected;
    private CancellationTokenSource? _cts;
    private IReadOnlyList<TreeNode> _roots = Array.Empty<TreeNode>();

    public DecompilerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => { _cts?.Cancel(); _svc.Dispose(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = ".NET Decompiler · .NET 反編譯器";
        HeaderBlurb.Text = P(
            "Open a managed .dll or .exe and browse it like ILSpy — namespaces, types and members — then decompile any item to C# or toggle to IL disassembly. Inspect assembly metadata, referenced assemblies and resources, search by name, and save the decompiled C#. Runs entirely in-app on the managed ICSharpCode.Decompiler engine; no external tool is launched.",
            "開一個受控 .dll 或 .exe，好似 ILSpy 咁瀏覽 —— 命名空間、型別同成員 —— 跟住將任何項目反編譯做 C# 或者切去 IL 反組譯。可以睇組件後設資料、參考組件同資源，按名搜尋，又可以另存反編譯出嚟嘅 C#。全部喺 app 內用受控嘅 ICSharpCode.Decompiler 引擎運行，唔會啟動任何外部工具。");

        OpenLbl.Text = P("Open assembly · 開組件", "開組件");
        SearchBox.PlaceholderText = P("Search types / members · 搜尋型別／成員", "搜尋型別／成員");
        SaveLbl.Text = P("Save .cs · 另存", "另存 .cs");
        CopyLbl.Text = P("Copy · 複製", "複製");
        TreeHeader.Text = P("Assembly explorer · 組件總管", "組件總管");
        MetaHeader.Text = P("Metadata · references · resources · 後設資料／參考／資源", "後設資料／參考／資源");
        FooterText.Text = P("Managed decompiler engine: ICSharpCode.Decompiler (ILSpy). Fully in-process — no external tool.",
            "受控反編譯引擎：ICSharpCode.Decompiler（ILSpy）。完全喺程序內運行，唔使外部工具。");

        if (!_svc.IsLoaded)
        {
            CodeTitle.Text = P("No selection · 未選取", "未選取");
            EmptyText.Text = P("Open a .dll or .exe to begin. Select a type or member on the left to decompile it.",
                "開一個 .dll 或 .exe 開始。喺左邊揀一個型別或成員就會反編譯。");
            EmptyState.Visibility = Visibility.Visible;
        }
        UpdateMetaPanelLabels();
    }

    private void UpdateMetaPanelLabels()
    {
        RefsHeader.Text = P("Referenced assemblies · 參考組件", "參考組件");
        ResHeader.Text = P("Resources · 資源", "資源");
    }

    // ===== Open 開檔 =====

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(
            new[]
            {
                new FileDialogs.Filter(P(".NET assemblies (*.dll;*.exe) · .NET 組件", ".NET 組件 (*.dll;*.exe)"), "*.dll;*.exe"),
                new FileDialogs.Filter(P("All files · 所有檔案", "所有檔案"), "*.*"),
            },
            P("Open a managed .NET assembly · 開一個受控 .NET 組件", "開一個受控 .NET 組件"));
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            await Task.Run(() => _svc.Load(path));
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error,
                P("Could not load assembly · 載入組件失敗", "載入組件失敗"),
                P($"'{Path.GetFileName(path)}' is not a managed .NET assembly, or could not be read. ({ex.Message})",
                  $"「{Path.GetFileName(path)}」唔係受控 .NET 組件，或者讀唔到。（{ex.Message}）"));
            return;
        }

        StatusBar.IsOpen = false;
        BuildTreeUi();
        PopulateMeta();
        SaveBtn.IsEnabled = false;
        CopyBtn.IsEnabled = false;
        CodeBox.Text = "";
        _selected = null;
        EmptyState.Visibility = Visibility.Visible;
        EmptyText.Text = P("Select a type or member on the left to decompile it.", "喺左邊揀一個型別或成員就會反編譯。");
        CodeTitle.Text = _svc.Meta?.Name ?? "";
        ShowStatus(InfoBarSeverity.Success,
            P("Loaded · 已載入", "已載入"),
            P($"{_svc.Meta?.Name} {_svc.Meta?.Version} — {_svc.Meta?.TargetFramework}",
              $"{_svc.Meta?.Name} {_svc.Meta?.Version} — {_svc.Meta?.TargetFramework}"));
    }

    // ===== Tree 樹 =====

    private void BuildTreeUi()
    {
        _roots = _svc.BuildTree();
        AssemblyTree.RootNodes.Clear();
        foreach (var r in _roots)
            AssemblyTree.RootNodes.Add(MakeNode(r));
    }

    private static TreeViewNode MakeNode(TreeNode model)
    {
        var node = new TreeViewNode { Content = new NodeContent(model) };
        foreach (var child in model.Children)
            node.Children.Add(MakeNode(child));
        return node;
    }

    private async void Tree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode tn && tn.Content is NodeContent nc && nc.Model.IsTypeOrMember)
            await ShowNode(nc.Model);
    }

    private async Task ShowNode(TreeNode model)
    {
        _selected = model;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        CodeTitle.Text = model.FullName ?? model.Label;
        CodeBusy.IsActive = true;
        EmptyState.Visibility = Visibility.Collapsed;
        SaveBtn.IsEnabled = false;
        CopyBtn.IsEnabled = false;

        try
        {
            string text = _showIl
                ? await _svc.DisassembleNodeAsync(model, ct)
                : await _svc.DecompileNodeAsync(model, ct);
            if (ct.IsCancellationRequested) return;
            CodeBox.Text = text;
            SaveBtn.IsEnabled = !_showIl && !string.IsNullOrEmpty(text);
            CopyBtn.IsEnabled = !string.IsNullOrEmpty(text);
        }
        catch (OperationCanceledException) { /* superseded */ }
        catch (Exception ex)
        {
            CodeBox.Text = $"// {ex.Message}";
        }
        finally
        {
            if (!ct.IsCancellationRequested) CodeBusy.IsActive = false;
        }
    }

    // ===== Metadata panel 後設資料面板 =====

    private void PopulateMeta()
    {
        var m = _svc.Meta;
        if (m is null) return;
        MetaText.Text =
            P("Name", "名稱") + $": {m.Name}\n" +
            P("Version", "版本") + $": {m.Version}\n" +
            P("Target framework", "目標框架") + $": {m.TargetFramework}\n" +
            P("Public key token", "公開金鑰權杖") + $": {m.PublicKeyToken}\n" +
            P("Architecture", "架構") + $": {m.Architecture}\n" +
            P("Kind", "種類") + $": {(m.IsExecutable ? P("Executable (.exe)", "可執行檔 (.exe)") : P("Library (.dll)", "程式庫 (.dll)"))}\n" +
            P("Full name", "完整名稱") + $": {m.FullName}";

        RefsList.ItemsSource = m.ReferencedAssemblies.Count > 0
            ? m.ReferencedAssemblies
            : new[] { P("(none)", "（無）") };
        var res = _svc.Resources();
        ResList.ItemsSource = res.Count > 0 ? res : new[] { P("(none)", "（無）") };
        MetaExpander.IsExpanded = true;
    }

    // ===== View toggle 視圖切換 =====

    private async void ViewMode_Click(object sender, RoutedEventArgs e)
    {
        bool wantIl = ReferenceEquals(sender, IlToggle);
        _showIl = wantIl;
        CSharpToggle.IsChecked = !wantIl;
        IlToggle.IsChecked = wantIl;
        if (_selected is not null) await ShowNode(_selected);
    }

    // ===== Search 搜尋 =====

    private void Search_Changed(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        string q = sender.Text.Trim();
        if (q.Length < 2) { sender.ItemsSource = null; return; }
        var hits = SearchNodes(q).Take(40).Select(n => n.FullName ?? n.Label).ToList();
        sender.ItemsSource = hits;
    }

    private async void Search_Submitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        string q = (args.ChosenSuggestion as string) ?? sender.Text.Trim();
        if (string.IsNullOrEmpty(q)) return;
        var match = SearchNodes(q).FirstOrDefault(n => (n.FullName ?? n.Label) == q)
                    ?? SearchNodes(q).FirstOrDefault();
        if (match is not null) await ShowNode(match);
    }

    private IEnumerable<TreeNode> SearchNodes(string q)
    {
        var qq = q.ToLowerInvariant();
        var stack = new Stack<TreeNode>(_roots);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (n.IsTypeOrMember &&
                ((n.FullName ?? n.Label).ToLowerInvariant().Contains(qq) || n.Label.ToLowerInvariant().Contains(qq)))
                yield return n;
            foreach (var c in n.Children) stack.Push(c);
        }
    }

    // ===== Save / Copy 另存／複製 =====

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(CodeBox.Text) || _selected is null) return;
        string suggested = (SafeName(_selected.Label)) + ".cs";
        var path = await FileDialogs.SaveFileAsync(suggested, ".cs");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            await File.WriteAllTextAsync(path, CodeBox.Text);
            ShowStatus(InfoBarSeverity.Success, P("Saved · 已儲存", "已儲存"), path);
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Save failed · 儲存失敗", "儲存失敗"), ex.Message);
        }
    }

    private static string SafeName(string label)
    {
        int paren = label.IndexOf('(');
        if (paren > 0) label = label.Substring(0, paren);
        int colon = label.IndexOf(':');
        if (colon > 0) label = label.Substring(0, colon);
        label = label.Trim();
        foreach (var c in Path.GetInvalidFileNameChars()) label = label.Replace(c, '_');
        return string.IsNullOrEmpty(label) ? "decompiled" : label;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(CodeBox.Text)) return;
        var dp = new DataPackage();
        dp.SetText(CodeBox.Text);
        Clipboard.SetContent(dp);
        ShowStatus(InfoBarSeverity.Informational, P("Copied · 已複製", "已複製"),
            P("The code view was copied to the clipboard.", "程式碼已複製到剪貼簿。"));
    }

    private void ShowStatus(InfoBarSeverity sev, string title, string msg)
    {
        StatusBar.Severity = sev;
        StatusBar.Title = title;
        StatusBar.Message = msg;
        StatusBar.IsOpen = true;
    }
}

/// <summary>TreeView 用嘅節點內容（圖示／顏色／標籤）· Content view-model for a TreeViewNode.</summary>
public sealed class NodeContent
{
    public TreeNode Model { get; }
    public string Label => Model.Label;
    public string Glyph { get; }
    public Brush Brush { get; }
    public string Tip => Model.FullName ?? Model.Label;

    public NodeContent(TreeNode model)
    {
        Model = model;
        (Glyph, Brush) = GlyphFor(model.Kind);
    }

    private static (string, Brush) GlyphFor(NodeKind kind)
    {
        // Segoe Fluent / MDL2 glyphs with kind-appropriate accents.
        Color c = kind switch
        {
            NodeKind.Namespace => Color.FromArgb(255, 0xC5, 0x86, 0x2C),  // amber
            NodeKind.Interface => Color.FromArgb(255, 0x4E, 0xC9, 0xB0),  // teal
            NodeKind.Enum => Color.FromArgb(255, 0xB8, 0xD7, 0xA3),       // light green
            NodeKind.Struct => Color.FromArgb(255, 0x86, 0xC6, 0x91),     // green
            NodeKind.Delegate => Color.FromArgb(255, 0xD6, 0x9D, 0x85),   // salmon
            NodeKind.Class => Color.FromArgb(255, 0xE8, 0xC5, 0x69),      // gold
            NodeKind.Method => Color.FromArgb(255, 0xB4, 0x80, 0xD6),     // purple
            NodeKind.Property => Color.FromArgb(255, 0x6F, 0xA8, 0xDC),   // blue
            NodeKind.Field => Color.FromArgb(255, 0x9C, 0xDC, 0xFE),      // light blue
            NodeKind.Event => Color.FromArgb(255, 0xDC, 0x82, 0x6F),      // orange-red
            _ => Color.FromArgb(255, 0x99, 0x99, 0x99),
        };
        char g = kind switch
        {
            NodeKind.Namespace => (char)0xE8B7,  // folder
            NodeKind.Interface => (char)0xE9D9,  // StatusCircleRing (interface)
            NodeKind.Enum => (char)0xE8FD,       // list (enum)
            NodeKind.Struct => (char)0xE950,     // KnowledgeArticle (struct)
            NodeKind.Delegate => (char)0xE71B,   // link (delegate)
            NodeKind.Class => (char)0xE8A5,      // document (class)
            NodeKind.Method => (char)0xE943,     // code (method)
            NodeKind.Property => (char)0xE713,   // settings (property)
            NodeKind.Field => (char)0xE7C3,      // page (field)
            NodeKind.Event => (char)0xE945,      // lightning (event)
            _ => (char)0xE8A5,
        };
        return (g.ToString(), new SolidColorBrush(c));
    }
}
