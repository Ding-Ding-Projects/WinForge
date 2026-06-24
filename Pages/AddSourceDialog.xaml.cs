using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 加來源對話框（UniGetUI 式）· Add-source dialog (ContentDialog) for one package manager.
/// 提供精選已知來源下拉、名稱／URL 輸入，及需要管理員時嘅提示。
/// Offers a known-source dropdown, name/URL fields, and an admin-required note where applicable.
/// 全部字串雙語（繁體中文／廣東話 + English）。 All strings bilingual.
/// </summary>
public sealed partial class AddSourceDialog : ContentDialog
{
    private readonly string _managerKey;

    private AddSourceDialog(string managerKey)
    {
        _managerKey = managerKey ?? "";
        InitializeComponent();
        Render();
    }

    private static string P(string en, string zh) => Loc.I.Pick(en, zh);

    /// <summary>
    /// 顯示對話框並回傳 (name,url)，取消回 null · Show the dialog; returns (name,url) or null on cancel.
    /// </summary>
    public static async Task<(string name, string url)?> ShowAsync(XamlRoot root, string managerKey)
    {
        var dlg = new AddSourceDialog(managerKey) { XamlRoot = root };
        var r = await dlg.ShowAsync();
        if (r != ContentDialogResult.Primary) return null;
        return (dlg.NameBox.Text.Trim(), dlg.UrlBox.Text.Trim());
    }

    private void Render()
    {
        var m = PackageManagerRegistry.ByKey(_managerKey);
        string mgrName = m is null ? _managerKey : $"{m.NameEn} · {m.NameZh}";

        Title = $"{P("Add source", "加來源")} — {mgrName}";
        PrimaryButtonText = P("Add", "加入");
        CloseButtonText = P("Cancel", "取消");
        DefaultButton = ContentDialogButton.Primary;

        NameBox.Header = P("Name", "名稱");
        NameBox.PlaceholderText = P("e.g. extras", "例如：extras");
        UrlBox.Header = P("URL / location", "URL／位置");
        UrlBox.PlaceholderText = P("https://…", "https://…");

        // 精選已知來源下拉 · curated known-source dropdown.
        KnownBox.Header = P("Known source (optional)", "已知來源（可選）");
        KnownBox.Items.Add(P("— Custom —", "— 自訂 —"));
        foreach (var ks in SourceManager.KnownSourcesFor(_managerKey))
            KnownBox.Items.Add($"{ks.Name}  ·  {ks.Url}");
        KnownBox.SelectedIndex = 0;
        KnownBox.Visibility = KnownBox.Items.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        // 需要管理員時提示 · admin-required note.
        if (SourceManager.RequiresAdmin(_managerKey))
        {
            AdminBar.Title = P("Administrator required", "需要管理員權限");
            AdminBar.Message = P(
                "Adding a source for this manager runs elevated — you may see a UAC prompt.",
                "為呢個管理器加來源會以管理員身份執行，可能會彈出 UAC 提示。");
            AdminBar.IsOpen = true;
        }
    }

    private void Known_Changed(object sender, SelectionChangedEventArgs e)
    {
        int idx = KnownBox.SelectedIndex - 1; // 0 = custom
        if (idx < 0) return;
        var list = SourceManager.KnownSourcesFor(_managerKey);
        if (idx >= list.Count) return;
        var ks = list[idx];
        NameBox.Text = ks.Name;
        UrlBox.Text = ks.Url;
    }

    private void Primary_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var name = NameBox.Text.Trim();
        var url = UrlBox.Text.Trim();
        if (name.Length == 0)
        {
            Warn(P("Enter a source name.", "請輸入來源名稱。"));
            args.Cancel = true;
            return;
        }
        // scoop bucket 加入可以淨係靠名（已知 bucket），其餘要 URL · scoop can add a known bucket by name alone.
        if (url.Length == 0 && _managerKey != "scoop")
        {
            Warn(P("Enter a source URL / location.", "請輸入來源 URL／位置。"));
            args.Cancel = true;
        }
    }

    private void Warn(string msg)
    {
        Bar.Severity = InfoBarSeverity.Warning;
        Bar.Title = P("Check the form", "請檢查表格");
        Bar.Message = msg;
        Bar.IsOpen = true;
    }
}
