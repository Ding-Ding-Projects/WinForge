using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;
using Op = WinForge.Services.PackageOperations.Op;

namespace WinForge.Pages;

/// <summary>
/// 安裝選項對話框（UniGetUI 式，帶即時指令預覽）· Install-options dialog (UniGetUI-style) with a LIVE
/// command-line preview. 用分頁分組：主要／參數／更新及解除安裝／鈎／關閉程序。
/// Tabbed sections: Main / Args / Update &amp; Uninstall / Hooks / Close apps, plus a live preview and
/// follow-global / save-as-global / reset-to-global controls. Built entirely in code via <see cref="ShowAsync"/>,
/// matching the in-code dialog style used elsewhere in PackageManagerModule.
/// </summary>
public sealed partial class InstallOptionsDialog : UserControl
{
    public InstallOptionsDialog()
    {
        InitializeComponent();
    }

    private static string P(string en, string zh) => Loc.I.Pick(en, zh);

    /// <summary>
    /// 彈出對話框 · Show the dialog for one package + operation.
    /// 確認後（若無「跟全域」）會經 <see cref="InstallOptions.SaveForPackage"/> 寫每個套件嘅覆寫，回 true。
    /// On confirm (and unless follow-global is on) writes the per-package override and returns true.
    /// </summary>
    public static async System.Threading.Tasks.Task<bool> ShowAsync(
        XamlRoot root, PackageItem item, InstallOptions seed, Op op)
    {
        var o = (seed ?? new InstallOptions()).Clone();
        var managerKey = item?.ManagerKey ?? "";
        var id = item?.Id ?? "";
        var source = item?.Source ?? "";

        bool followGlobal = !InstallOptions.HasOverride(managerKey, id);

        // ===== 即時預覽 · Live command preview =====
        var preview = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            MinHeight = 56,
            Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
        };
        var previewLabel = new TextBlock
        {
            Text = P("Command preview", "指令預覽"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Margin = new Thickness(0, 2, 0, 0),
        };

        // 由控件讀返選項並更新預覽 · Pull every control's value into 'o' and refresh the preview.
        var pullers = new List<Action>();
        void Refresh()
        {
            foreach (var pull in pullers) pull();
            preview.Text = PackageOperations.BuildCommandPreview(managerKey, id, source, op, o);
        }

        // ---------- 小工具 · small builders ----------
        TextBox Tb(string header, string value, string placeholder = "")
        {
            var t = new TextBox { Header = header, Text = value ?? "", PlaceholderText = placeholder, HorizontalAlignment = HorizontalAlignment.Stretch };
            t.TextChanged += (_, _) => Refresh();
            return t;
        }
        CheckBox Cb(string content, bool value, Action<bool> set)
        {
            var c = new CheckBox { Content = content, IsChecked = value };
            pullers.Add(() => set(c.IsChecked == true));
            c.Checked += (_, _) => Refresh();
            c.Unchecked += (_, _) => Refresh();
            return c;
        }
        ComboBox Combo(string header, string[] items, string current)
        {
            var cb = new ComboBox { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var s in items) cb.Items.Add(s);
            var idx = Array.FindIndex(items, s => string.Equals(s, current, StringComparison.OrdinalIgnoreCase));
            cb.SelectedIndex = idx < 0 ? 0 : idx;
            cb.SelectionChanged += (_, _) => Refresh();
            return cb;
        }
        TextBlock Hint(string text) => new()
        {
            Text = text,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };

        // ===================== Main tab =====================
        var versionBox = Tb(P("Version (blank = latest)", "版本（留空＝最新）"), o.Version, "1.2.3");
        pullers.Add(() => o.Version = versionBox.Text.Trim());

        var scopeCombo = Combo(P("Scope", "範圍"), new[] { "default", "user", "machine" }, string.IsNullOrEmpty(o.Scope) ? "default" : o.Scope);
        pullers.Add(() => o.Scope = scopeCombo.SelectedIndex <= 0 ? "" : (string)scopeCombo.SelectedItem);

        var archCombo = Combo(P("Architecture", "架構"), new[] { "default", "x64", "x86", "arm64" }, string.IsNullOrEmpty(o.Architecture) ? "default" : o.Architecture);
        pullers.Add(() => o.Architecture = archCombo.SelectedIndex <= 0 ? "" : (string)archCombo.SelectedItem);

        var adminCb = Cb(P("Run as administrator", "以管理員身分執行"), o.RunAsAdministrator, v => o.RunAsAdministrator = v);
        var interactiveCb = Cb(P("Interactive installer", "互動式安裝"), o.Interactive, v => o.Interactive = v);
        var skipHashCb = Cb(P("Skip hash / integrity check", "略過雜湊／完整性檢查"), o.SkipHashCheck, v => o.SkipHashCheck = v);
        var preReleaseCb = Cb(P("Allow pre-release versions", "允許預先發佈版本"), o.PreRelease, v => o.PreRelease = v);

        var locBox = Tb(P("Custom install location", "自訂安裝位置"), o.CustomInstallLocation, @"C:\Apps\…");
        pullers.Add(() => o.CustomInstallLocation = locBox.Text.Trim());
        var browseBtn = new Button { Content = P("Browse…", "瀏覽…"), Margin = new Thickness(0, 0, 0, 0) };
        browseBtn.Click += async (_, _) =>
        {
            try
            {
                var picked = await FileDialogs.OpenFolderAsync(P("Choose install location", "揀安裝位置"));
                if (!string.IsNullOrWhiteSpace(picked)) { locBox.Text = picked; Refresh(); }
            }
            catch { /* best effort */ }
        };
        var locRow = new Grid { ColumnSpacing = 8, VerticalAlignment = VerticalAlignment.Bottom };
        locRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        locRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(locBox, 0);
        Grid.SetColumn(browseBtn, 1);
        browseBtn.VerticalAlignment = VerticalAlignment.Bottom;
        locRow.Children.Add(locBox);
        locRow.Children.Add(browseBtn);

        var mainPanel = new StackPanel { Spacing = 10 };
        foreach (var c in new UIElement[]
        {
            versionBox, scopeCombo, archCombo, adminCb, interactiveCb, skipHashCb, preReleaseCb, locRow,
            Hint(P("Scope / architecture / location apply where the manager supports them (mostly winget).",
                   "範圍／架構／位置只喺管理器支援時生效（主要係 winget）。")),
        }) mainPanel.Children.Add(c);

        // ===================== Args tab =====================
        var argsInstall = Tb(P("Install args", "安裝參數"), o.CustomArgsInstall, "--extra --flags");
        pullers.Add(() => o.CustomArgsInstall = argsInstall.Text);
        var argsUpdate = Tb(P("Update args", "更新參數"), o.CustomArgsUpdate, "--extra --flags");
        pullers.Add(() => o.CustomArgsUpdate = argsUpdate.Text);
        var argsUninstall = Tb(P("Uninstall args", "解除安裝參數"), o.CustomArgsUninstall, "--extra --flags");
        pullers.Add(() => o.CustomArgsUninstall = argsUninstall.Text);
        var argsPanel = new StackPanel { Spacing = 10 };
        foreach (var c in new UIElement[]
        {
            argsInstall, argsUpdate, argsUninstall,
            Hint(P("Extra CLI args appended to the matching operation's command.",
                   "額外 CLI 參數會加喺對應操作嘅指令尾。")),
        }) argsPanel.Children.Add(c);

        // ===================== Update & Uninstall tab =====================
        var removeDataCb = Cb(P("Remove data on uninstall", "解除安裝時移除資料"), o.RemoveDataOnUninstall, v => o.RemoveDataOnUninstall = v);
        var uninstallPrevCb = Cb(P("Uninstall previous version on update", "更新時解除安裝舊版本"), o.UninstallPreviousOnUpdate, v => o.UninstallPreviousOnUpdate = v);
        var skipMinorCb = Cb(P("Skip minor updates", "略過次要更新"), o.SkipMinorUpdates, v => o.SkipMinorUpdates = v);
        var autoUpdateCb = Cb(P("Auto-update this package", "自動更新此套件"), o.AutoUpdate, v => o.AutoUpdate = v);
        var updPanel = new StackPanel { Spacing = 10 };
        foreach (var c in new UIElement[]
        {
            removeDataCb, uninstallPrevCb, skipMinorCb, autoUpdateCb,
            Hint(P("Skip-minor and auto-update are stored preferences (no CLI flag).",
                   "「略過次要更新」同「自動更新」只係儲存偏好（冇 CLI 參數）。")),
        }) updPanel.Children.Add(c);

        // ===================== Hooks tab =====================
        var preInstall = Tb(P("Pre-install command", "安裝前指令"), o.PreInstallCommand);
        pullers.Add(() => o.PreInstallCommand = preInstall.Text);
        var postInstall = Tb(P("Post-install command", "安裝後指令"), o.PostInstallCommand);
        pullers.Add(() => o.PostInstallCommand = postInstall.Text);
        var preUpdate = Tb(P("Pre-update command", "更新前指令"), o.PreUpdateCommand);
        pullers.Add(() => o.PreUpdateCommand = preUpdate.Text);
        var postUpdate = Tb(P("Post-update command", "更新後指令"), o.PostUpdateCommand);
        pullers.Add(() => o.PostUpdateCommand = postUpdate.Text);
        var preUninstall = Tb(P("Pre-uninstall command", "解除安裝前指令"), o.PreUninstallCommand);
        pullers.Add(() => o.PreUninstallCommand = preUninstall.Text);
        var postUninstall = Tb(P("Post-uninstall command", "解除安裝後指令"), o.PostUninstallCommand);
        pullers.Add(() => o.PostUninstallCommand = postUninstall.Text);
        var abortInstall = Cb(P("Abort if pre-install command fails", "若安裝前指令失敗則中止"), o.AbortOnPreInstallFail, v => o.AbortOnPreInstallFail = v);
        var abortUpdate = Cb(P("Abort if pre-update command fails", "若更新前指令失敗則中止"), o.AbortOnPreUpdateFail, v => o.AbortOnPreUpdateFail = v);
        var abortUninstall = Cb(P("Abort if pre-uninstall command fails", "若解除安裝前指令失敗則中止"), o.AbortOnPreUninstallFail, v => o.AbortOnPreUninstallFail = v);
        var hooksPanel = new StackPanel { Spacing = 10 };
        foreach (var c in new UIElement[]
        {
            preInstall, abortInstall, postInstall,
            preUpdate, abortUpdate, postUpdate,
            preUninstall, abortUninstall, postUninstall,
            Hint(P("Hooks run through PowerShell. Post hooks are best-effort (never abort).",
                   "鈎經 PowerShell 執行；後置鈎盡力而為（唔會中止）。")),
        }) hooksPanel.Children.Add(c);

        // ===================== Close apps tab =====================
        var killBox = new TextBox
        {
            Header = P("Processes to close before operation (one per line)", "操作前要關閉嘅程序（每行一個）"),
            Text = string.Join(Environment.NewLine, o.KillBeforeOperation ?? new List<string>()),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 100,
            PlaceholderText = "chrome\nspotify",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        pullers.Add(() => o.KillBeforeOperation = killBox.Text
            .Replace("\r", "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList());
        var forceKillCb = Cb(P("Force-kill (no graceful close)", "強制終止（唔嘗試正常關閉）"), o.ForceKill, v => o.ForceKill = v);
        var killPanel = new StackPanel { Spacing = 10 };
        foreach (var c in new UIElement[]
        {
            killBox, forceKillCb,
            Hint(P("Each process is stopped via PowerShell before the operation runs.",
                   "每個程序喺操作前經 PowerShell 關閉。")),
        }) killPanel.Children.Add(c);

        // ===================== tabs =====================
        var pivot = new Pivot();
        PivotItem Pi(string header, UIElement content) => new()
        {
            Header = header,
            Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 8, 4, 0) },
        };
        pivot.Items.Add(Pi(P("Main", "主要"), mainPanel));
        pivot.Items.Add(Pi(P("Args", "參數"), argsPanel));
        pivot.Items.Add(Pi(P("Update / Uninstall", "更新／解除安裝"), updPanel));
        pivot.Items.Add(Pi(P("Hooks", "鈎"), hooksPanel));
        pivot.Items.Add(Pi(P("Close apps", "關閉程序"), killPanel));

        // ===================== global controls =====================
        var followGlobalToggle = new ToggleSwitch
        {
            Header = P("Follow global defaults", "跟隨全域預設"),
            IsOn = followGlobal,
            OnContent = P("On", "開"),
            OffContent = P("Off", "關"),
        };
        var saveGlobalBtn = new Button { Content = P("Save as global default", "存為全域預設") };
        var resetGlobalBtn = new Button { Content = P("Reset to global", "重設為全域") };
        saveGlobalBtn.Click += (_, _) => { foreach (var pull in pullers) pull(); InstallOptions.SaveGlobal(o); };
        resetGlobalBtn.Click += (_, _) =>
        {
            var g = InstallOptions.LoadGlobal();
            // 把全域值套返落控件 · Push global values back into the live controls.
            versionBox.Text = g.Version; locBox.Text = g.CustomInstallLocation;
            scopeCombo.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { "default", "user", "machine" }, string.IsNullOrEmpty(g.Scope) ? "default" : g.Scope));
            archCombo.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { "default", "x64", "x86", "arm64" }, string.IsNullOrEmpty(g.Architecture) ? "default" : g.Architecture));
            adminCb.IsChecked = g.RunAsAdministrator; interactiveCb.IsChecked = g.Interactive;
            skipHashCb.IsChecked = g.SkipHashCheck; preReleaseCb.IsChecked = g.PreRelease;
            argsInstall.Text = g.CustomArgsInstall; argsUpdate.Text = g.CustomArgsUpdate; argsUninstall.Text = g.CustomArgsUninstall;
            removeDataCb.IsChecked = g.RemoveDataOnUninstall; uninstallPrevCb.IsChecked = g.UninstallPreviousOnUpdate;
            skipMinorCb.IsChecked = g.SkipMinorUpdates; autoUpdateCb.IsChecked = g.AutoUpdate;
            preInstall.Text = g.PreInstallCommand; postInstall.Text = g.PostInstallCommand;
            preUpdate.Text = g.PreUpdateCommand; postUpdate.Text = g.PostUpdateCommand;
            preUninstall.Text = g.PreUninstallCommand; postUninstall.Text = g.PostUninstallCommand;
            abortInstall.IsChecked = g.AbortOnPreInstallFail; abortUpdate.IsChecked = g.AbortOnPreUpdateFail; abortUninstall.IsChecked = g.AbortOnPreUninstallFail;
            killBox.Text = string.Join(Environment.NewLine, g.KillBeforeOperation ?? new List<string>());
            forceKillCb.IsChecked = g.ForceKill;
            followGlobalToggle.IsOn = true;
            Refresh();
        };
        var globalRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        globalRow.Children.Add(saveGlobalBtn);
        globalRow.Children.Add(resetGlobalBtn);

        // ===================== assemble dialog body =====================
        var body = new StackPanel { Spacing = 10, Width = 520 };
        body.Children.Add(followGlobalToggle);
        body.Children.Add(pivot);
        body.Children.Add(previewLabel);
        body.Children.Add(preview);
        body.Children.Add(globalRow);

        Refresh();

        var opTitle = op switch
        {
            Op.Install => P("Install options", "安裝選項"),
            Op.Update => P("Update options", "更新選項"),
            Op.Uninstall => P("Uninstall options", "解除安裝選項"),
            _ => P("Options", "選項"),
        };

        var dlg = new ContentDialog
        {
            Title = $"{opTitle} · {item?.Name}",
            Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 620 },
            PrimaryButtonText = P("OK", "確定"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root,
        };

        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return false;

        // 確認：讀返所有控件 · Confirmed: pull every value once more.
        foreach (var pull in pullers) pull();

        if (followGlobalToggle.IsOn)
        {
            // 跟全域：刪除每個套件嘅覆寫 · Follow global: delete the per-package override.
            InstallOptions.ResetForPackage(managerKey, id);
        }
        else
        {
            // 寫每個套件嘅覆寫 · Persist the per-package override.
            InstallOptions.SaveForPackage(managerKey, id, o);
        }
        return true;
    }
}
