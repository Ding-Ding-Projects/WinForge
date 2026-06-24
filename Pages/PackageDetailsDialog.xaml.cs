using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 結構化套件詳情對話框（UniGetUI 式）· Structured package-details dialog, UniGetUI-style.
/// 即時開啟並顯示「載入緊…」，喺 <see cref="PackageDetails.GetAsync"/> 完成後填欄位（鏡像舊 ShowDetails 模式）。
/// Opens immediately showing "Loading…", then fills fields when GetAsync completes (mirrors the old
/// ShowDetails pattern). Footer carries copy-command / download / chained reinstall-and-update ops.
/// 所有用家可見字串都係雙語 · Every user-facing string is bilingual.
/// </summary>
public sealed partial class PackageDetailsDialog : ContentDialog
{
    private readonly PackageItem _item;
    private readonly IPackageManager? _mgr;
    private TextBlock? _status;          // 底部狀態列 · footer status line
    private ComboBox? _versionCombo;     // 可安裝版本 · installable-versions picker
    private StackPanel? _fields;         // 欄位容器 · field list container

    private PackageDetailsDialog(PackageItem item)
    {
        InitializeComponent();
        _item = item;
        _mgr = PackageManagerRegistry.ByKey(item.ManagerKey);
        Title = $"{(string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name)} · {item.ManagerKey}";
        CloseButtonText = P("Close", "關閉");
        DefaultButton = ContentDialogButton.Close;
    }

    private static string P(string en, string zh) => Loc.I.Pick(en, zh);

    /// <summary>
    /// 開啟詳情對話框 · Open the details dialog for one package (immediate, then async-filled).
    /// </summary>
    public static async Task ShowAsync(XamlRoot root, PackageItem item)
    {
        try
        {
            var dlg = new PackageDetailsDialog(item) { XamlRoot = root };
            dlg.BuildSkeleton();
            _ = dlg.FillAsync();   // 唔等載入，背景填 · fill in the background; don't block opening
            await dlg.ShowAsync();
        }
        catch { /* defensive — never throw to the caller */ }
    }

    // ===== layout =====

    /// <summary>砌出初始骨架（標題、載入提示、底部動作列）· Build the initial skeleton with a "Loading…" hint and footer.</summary>
    private void BuildSkeleton()
    {
        Root.Children.Clear();

        // Header: name / id / manager badge.
        var header = new StackPanel { Spacing = 2 };
        header.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(_item.Name) ? _item.Id : _item.Name,
            FontWeight = FontWeights.SemiBold, FontSize = 18, TextWrapping = TextWrapping.Wrap,
        });
        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        idRow.Children.Add(ManagerBadge(_item.ManagerKey));
        idRow.Children.Add(new TextBlock
        {
            Text = _item.Id, FontSize = 12, FontFamily = Mono(),
            Foreground = Sec(), VerticalAlignment = VerticalAlignment.Center,
            IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap,
        });
        header.Children.Add(idRow);
        Root.Children.Add(header);

        Root.Children.Add(Divider());

        // Field list (filled async).
        _fields = new StackPanel { Spacing = 8 };
        _fields.Children.Add(new TextBlock
        {
            Text = P("Loading…", "載入緊…"), Foreground = Sec(),
            FontStyle = Windows.UI.Text.FontStyle.Italic,
        });
        Root.Children.Add(_fields);

        Root.Children.Add(Divider());

        // Footer: actions.
        Root.Children.Add(BuildFooter());

        // Status line.
        _status = new TextBlock
        {
            Text = "", FontSize = 12, Foreground = Sec(),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0),
        };
        Root.Children.Add(_status);
    }

    /// <summary>底部動作列（複製指令／下載／鏈式操作）· Footer action rows: copy / download / chained ops.</summary>
    private UIElement BuildFooter()
    {
        var wrap = new StackPanel { Spacing = 8 };

        // Row 1: copy install command + download installer.
        var row1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var copyBtn = new Button { Content = P("Copy install command", "複製安裝指令") };
        copyBtn.Click += (_, _) =>
        {
            try
            {
                var cmd = PackageDetails.BuildInstallCommand(_item);
                var dp = new DataPackage();
                dp.SetText(cmd);
                Clipboard.SetContent(dp);
                SetStatus(P($"Copied: {cmd}", $"已複製：{cmd}"));
            }
            catch (Exception ex) { SetStatus(ex.Message); }
        };
        row1.Children.Add(copyBtn);

        var downloadBtn = new Button { Content = P("Download installer…", "下載安裝程式…") };
        downloadBtn.Click += async (_, _) =>
        {
            try
            {
                var dir = await FileDialogs.OpenFolderAsync(P("Choose download folder", "揀下載資料夾"));
                if (dir is null) return;
                downloadBtn.IsEnabled = false;
                SetStatus(P("Downloading installer…", "下載安裝程式緊…"));
                var r = await PackageDetails.DownloadInstallerAsync(_item, dir, CancellationToken.None);
                SetStatus(r.Message?.Primary ?? (r.Success ? P("Downloaded.", "已下載。") : P("Download failed.", "下載失敗。")));
            }
            catch (Exception ex) { SetStatus(ex.Message); }
            finally { downloadBtn.IsEnabled = true; }
        };
        row1.Children.Add(downloadBtn);
        wrap.Children.Add(row1);

        // Row 2: chained operations.
        var row2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        var reinstallBtn = new Button { Content = P("Reinstall", "重新安裝") };
        reinstallBtn.Click += async (_, _) => await ChainedAsync(reinstallBtn,
            P("Reinstalling…", "重新安裝緊…"),
            new (string en, string zh, Func<IPackageManager, CancellationToken, Task<TweakResult>> op)[]
            {
                ("Uninstall", "解除安裝", (m, c) => m.UninstallAsync(_item.Id, c)),
                ("Install", "安裝", (m, c) => m.InstallAsync(_item.Id, c)),
            });
        row2.Children.Add(reinstallBtn);

        var unReBtn = new Button { Content = P("Uninstall then reinstall", "解除後重裝") };
        unReBtn.Click += async (_, _) => await ChainedAsync(unReBtn,
            P("Uninstall then reinstall…", "解除後重裝緊…"),
            new (string en, string zh, Func<IPackageManager, CancellationToken, Task<TweakResult>> op)[]
            {
                ("Uninstall", "解除安裝", (m, c) => m.UninstallAsync(_item.Id, c)),
                ("Install", "安裝", (m, c) => m.InstallAsync(_item.Id, c)),
            });
        row2.Children.Add(unReBtn);

        var unUpBtn = new Button { Content = P("Uninstall then update", "解除後更新") };
        unUpBtn.Click += async (_, _) => await ChainedAsync(unUpBtn,
            P("Uninstall then update…", "解除後更新緊…"),
            new (string en, string zh, Func<IPackageManager, CancellationToken, Task<TweakResult>> op)[]
            {
                ("Uninstall", "解除安裝", (m, c) => m.UninstallAsync(_item.Id, c)),
                ("Install", "安裝", (m, c) => m.InstallAsync(_item.Id, c)),
                ("Update", "更新", (m, c) => m.UpdateAsync(_item.Id, c)),
            });
        row2.Children.Add(unUpBtn);

        wrap.Children.Add(row2);
        return wrap;
    }

    /// <summary>
    /// 順序執行一連串操作並即時報告進度 · Run a sequence of manager ops in order, surfacing progress.
    /// </summary>
    private async Task ChainedAsync(Button btn, string busyText,
        (string en, string zh, Func<IPackageManager, CancellationToken, Task<TweakResult>> op)[] steps)
    {
        if (_mgr is null) { SetStatus(P("Manager not available.", "管理器唔可用。")); return; }
        btn.IsEnabled = false;
        var original = btn.Content;
        btn.Content = busyText;
        try
        {
            int i = 0;
            foreach (var step in steps)
            {
                i++;
                SetStatus(P($"[{i}/{steps.Length}] {step.en} {_item.Name}…", $"[{i}/{steps.Length}] {step.zh} {_item.Name}…"));
                TweakResult r;
                try { r = await step.op(_mgr, CancellationToken.None); }
                catch (Exception ex) { r = TweakResult.Fail(ex.Message, ex.Message); }
                if (!r.Success)
                {
                    SetStatus(P($"Step '{step.en}' failed: {r.Message?.En}", $"步驟「{step.zh}」失敗：{r.Message?.Zh}"));
                    return;
                }
            }
            SetStatus(P("Done.", "完成。"));
        }
        finally { btn.Content = original; btn.IsEnabled = true; }
    }

    // ===== async fill =====

    private async Task FillAsync()
    {
        PackageDetails.Details d;
        try { d = await PackageDetails.GetAsync(_item, CancellationToken.None); }
        catch { d = PackageDetails.Details.Empty(_item); }

        if (_fields is null) return;
        _fields.Children.Clear();

        // Description.
        AddTextField(P("Description", "描述"), d.Description, wrap: true);

        // Version + installable-versions combo.
        AddVersionRow(d.Version);

        AddTextField(P("Author", "作者"), d.Author);
        AddTextField(P("Publisher", "發行者"), d.Publisher);
        AddLinkField(P("Homepage", "首頁"), d.Homepage);
        AddLicenseRow(d.License, d.LicenseUrl);
        AddLinkField(P("Manifest URL", "資訊清單網址"), d.ManifestUrl);
        AddLinkField(P("Installer URL", "安裝程式網址"), d.InstallerUrl);
        AddTextField(P("Installer type", "安裝程式類型"), d.InstallerType);
        AddHashField(P("Installer hash", "安裝程式雜湊"), d.InstallerHash);
        AddTextField(P("Release date", "發佈日期"), d.ReleaseDate);
        AddTagsRow(d.Tags);
        AddDependenciesRow(d.Dependencies);
        AddReleaseNotes(d.ReleaseNotes, d.ReleaseNotesUrl);

        // 如果一個欄位都冇 · if nothing filled, say so.
        if (_fields.Children.Count == 0)
            _fields.Children.Add(new TextBlock
            {
                Text = P("No structured details available for this package.", "呢個套件冇結構化詳情。"),
                Foreground = Sec(), TextWrapping = TextWrapping.Wrap,
            });

        // Populate installable versions async.
        _ = FillVersionsAsync();
    }

    private async Task FillVersionsAsync()
    {
        if (_versionCombo is null) return;
        List<string> versions;
        try { versions = await PackageDetails.GetInstallableVersionsAsync(_item, CancellationToken.None); }
        catch { versions = new List<string>(); }

        if (versions.Count == 0)
        {
            _versionCombo.Items.Clear();
            _versionCombo.Items.Add(P("(no other versions)", "（冇其他版本）"));
            _versionCombo.SelectedIndex = 0;
            _versionCombo.IsEnabled = false;
            return;
        }
        _versionCombo.Items.Clear();
        foreach (var v in versions) _versionCombo.Items.Add(v);
        // 預選目前版本（如果喺清單入面）· preselect the current version if present.
        int idx = versions.FindIndex(v => string.Equals(v, _item.Version, StringComparison.OrdinalIgnoreCase));
        _versionCombo.SelectedIndex = idx >= 0 ? idx : 0;
        _versionCombo.IsEnabled = true;
    }

    // ===== field builders =====

    private void AddVersionRow(string version)
    {
        if (_fields is null) return;
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(Label(P("Version", "版本")));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(version) ? _item.Version : version,
            VerticalAlignment = VerticalAlignment.Center, IsTextSelectionEnabled = true,
        });
        _versionCombo = new ComboBox
        {
            MinWidth = 180,
            PlaceholderText = P("Installable versions…", "可安裝版本…"),
        };
        _versionCombo.Items.Add(P("Loading…", "載入緊…"));
        _versionCombo.SelectedIndex = 0;
        _versionCombo.IsEnabled = false;
        ToolTipService.SetToolTip(_versionCombo, P("Installable versions", "可安裝版本"));
        row.Children.Add(_versionCombo);
        panel.Children.Add(row);
        _fields.Children.Add(panel);
    }

    private void AddTextField(string label, string value, bool wrap = false)
    {
        if (_fields is null || string.IsNullOrWhiteSpace(value)) return;
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(Label(label));
        panel.Children.Add(new TextBlock
        {
            Text = value.Trim(), IsTextSelectionEnabled = true,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        });
        _fields.Children.Add(panel);
    }

    private void AddLinkField(string label, string url)
    {
        if (_fields is null || string.IsNullOrWhiteSpace(url)) return;
        if (!TryUri(url, out var uri))
        {
            AddTextField(label, url);
            return;
        }
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(Label(label));
        panel.Children.Add(new HyperlinkButton
        {
            Content = url.Trim(), NavigateUri = uri, Padding = new Thickness(0),
        });
        _fields.Children.Add(panel);
    }

    private void AddLicenseRow(string license, string licenseUrl)
    {
        if (_fields is null) return;
        if (string.IsNullOrWhiteSpace(license) && string.IsNullOrWhiteSpace(licenseUrl)) return;
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(Label(P("License", "授權條款")));
        if (!string.IsNullOrWhiteSpace(licenseUrl) && TryUri(licenseUrl, out var uri))
        {
            panel.Children.Add(new HyperlinkButton
            {
                Content = string.IsNullOrWhiteSpace(license) ? licenseUrl.Trim() : license.Trim(),
                NavigateUri = uri, Padding = new Thickness(0),
            });
        }
        else
        {
            panel.Children.Add(new TextBlock { Text = license.Trim(), IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap });
        }
        _fields.Children.Add(panel);
    }

    private void AddHashField(string label, string hash)
    {
        if (_fields is null || string.IsNullOrWhiteSpace(hash)) return;
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(Label(label));
        panel.Children.Add(new TextBlock
        {
            Text = hash.Trim(), FontFamily = Mono(), FontSize = 12,
            IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap,
        });
        _fields.Children.Add(panel);
    }

    private void AddTagsRow(List<string> tags)
    {
        if (_fields is null || tags is null || tags.Count == 0) return;
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(Label(P("Tags", "標籤")));
        // 一行可橫向捲動嘅膠囊 · a single horizontally-scrollable row of pills.
        var inner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var t in tags.Take(24))
            inner.Children.Add(Pill(t));
        var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = inner };
        panel.Children.Add(scroll);
        _fields.Children.Add(panel);
    }

    private void AddDependenciesRow(List<PackageDetails.DependencyInfo> deps)
    {
        if (_fields is null || deps is null || deps.Count == 0) return;
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(Label(P("Dependencies", "相依")));
        foreach (var dep in deps.Take(40))
        {
            var line = dep.Name;
            if (!string.IsNullOrWhiteSpace(dep.Version)) line += $" ({dep.Version})";
            var tag = dep.Mandatory ? P("required", "必需") : P("optional", "可選");
            panel.Children.Add(new TextBlock
            {
                Text = $"• {line}  —  {tag}", FontSize = 12,
                IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap,
            });
        }
        _fields.Children.Add(panel);
    }

    private void AddReleaseNotes(string notes, string notesUrl)
    {
        if (_fields is null) return;
        if (string.IsNullOrWhiteSpace(notes) && string.IsNullOrWhiteSpace(notesUrl)) return;
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(Label(P("Release notes", "發佈說明")));
        if (!string.IsNullOrWhiteSpace(notes))
        {
            var notesBlock = new TextBlock
            {
                Text = notes.Trim(), FontSize = 12, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true,
            };
            panel.Children.Add(new ScrollViewer
            {
                MaxHeight = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = notesBlock,
            });
        }
        if (!string.IsNullOrWhiteSpace(notesUrl) && TryUri(notesUrl, out var uri))
        {
            panel.Children.Add(new HyperlinkButton
            {
                Content = P("Open release notes", "開啟發佈說明"), NavigateUri = uri, Padding = new Thickness(0),
            });
        }
        _fields.Children.Add(panel);
    }

    // ===== shared visuals =====

    private void SetStatus(string text)
    {
        if (_status is not null) _status.Text = text;
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text, FontWeight = FontWeights.SemiBold, FontSize = 12,
        Foreground = Sec(),
    };

    private static Border Pill(string text) => new()
    {
        Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(8, 2, 8, 2),
        Child = new TextBlock { Text = text, FontSize = 11 },
    };

    private static Border ManagerBadge(string key) => new()
    {
        Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(6, 2, 6, 2),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = key, FontSize = 10, FontWeight = FontWeights.SemiBold },
    };

    private static Border Divider() => new()
    {
        Height = 1,
        Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        Margin = new Thickness(0, 2, 0, 2),
    };

    private static Brush Sec() => (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

    private static FontFamily Mono() => new("Consolas");

    private static bool TryUri(string s, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim();
        if (!t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;
        return Uri.TryCreate(t, UriKind.Absolute, out uri!);
    }
}
