using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// WinForge 保險庫模組 · WinForge Vault — a polished, bilingual front-end for on-the-fly disk
/// encryption: create an encrypted file container, mount/dismount to a drive letter (with keyfile,
/// PIM, read-only and removable-media options), change a container's password, run a benchmark and
/// browse a mounted volume. Wraps a bundled de-branded engine (renamed VeraCrypt-derived build);
/// no "VeraCrypt"/"TrueCrypt" branding is surfaced anywhere.
/// </summary>
public sealed partial class VaultVolumesModule : Page
{
    private bool _hasScanned;

    public sealed class Row
    {
        public string Letter { get; init; } = "";   // "X:"
        public string TitleText { get; init; } = "";
        public string SubText { get; init; } = "";
    }

    private bool _busy;

    public VaultVolumesModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) =>
        {
            Render();
            if (_hasScanned) Reload();
            else ShowUnscannedState();
        };
        Loaded += (_, _) => { Render(); FillCombos(); ShowUnscannedState(); CheckEngine(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "WinForge Vault · WinForge 保險庫";
        HeaderBlurb.Text = P("Create encrypted containers and mount them as a drive. Everything in a container is encrypted on the fly — files are only readable while mounted with the correct password. Mounting needs administrator rights.",
            "建立加密容器並掛載成磁碟機。容器內所有嘢都即時加密 — 只有用正確密碼掛載時先讀得到。掛載需要管理員權限。");

        RefreshBtn.Content = P("Refresh", "重新整理");
        ChangePwdBtn.Content = P("Change password…", "更改密碼…");
        BenchmarkBtn.Content = P("Benchmark / Settings", "效能測試／設定");
        WipeCacheBtn.Content = P("Wipe cache", "清除快取");
        DismountAllBtn.Content = P("Dismount all", "全部卸載");

        CreateHeader.Text = P("Create a new encrypted container", "建立新嘅加密容器");
        CreatePathBox.PlaceholderText = P("Container file path…", "容器檔案路徑…");
        CreatePathBtn.Content = P("Browse…", "瀏覽…");
        CreateSizeBox.Header = P("Size", "大細");
        CreateSizeUnit.Header = P("Unit", "單位");
        CreateAlgoBox.Header = P("Encryption", "加密演算法");
        CreateHashBox.Header = P("Hash (PRF)", "雜湊（PRF）");
        CreateFsBox.Header = P("File system", "檔案系統");
        CreatePimBox.Header = P("PIM (0 = default)", "PIM（0 = 預設）");
        CreateKeyfileBox.PlaceholderText = P("Keyfile (optional)", "鎖匙檔（選填）");
        CreateKeyfileBtn.Content = P("Browse…", "瀏覽…");
        CreatePwdBox.Header = P("Password", "密碼");
        CreatePwdBox.PlaceholderText = P("Volume password", "磁碟區密碼");
        CreateDynamicChk.Content = P("Dynamic", "動態");
        CreateQuickChk.Content = P("Quick format", "快速格式化");
        CreateBtn.Content = P("Create", "建立");

        MountHeader.Text = P("Mount a container", "掛載容器");
        MountPathBox.PlaceholderText = P("Container file to mount…", "要掛載嘅容器檔案…");
        MountPathBtn.Content = P("Browse…", "瀏覽…");
        MountLetterBox.Header = P("Drive letter", "磁碟機代號");
        MountPimBox.Header = P("PIM (0 = default)", "PIM（0 = 預設）");
        MountKeyfileBox.PlaceholderText = P("Keyfile (optional)", "鎖匙檔（選填）");
        MountKeyfileBtn.Content = P("Browse…", "瀏覽…");
        MountPwdBox.Header = P("Password", "密碼");
        MountPwdBox.PlaceholderText = P("Volume password", "磁碟區密碼");
        MountReadOnlyChk.Content = P("Read-only", "唯讀");
        MountRemovableChk.Content = P("Removable media", "可移除媒體");
        MountExploreChk.Content = P("Open in Explorer", "掛載後開啟");
        MountBtn.Content = P("Mount", "掛載");

        MountedHeader.Text = P("Mounted volumes", "已掛載磁碟區");
        EmptyHint.Text = _hasScanned
            ? P("No mountable volumes detected. Mount a container above; mounted drives appear here so you can browse, dismount or force-dismount them.",
                "未偵測到可卸載嘅磁碟區。喺上面掛載容器；掛載咗嘅磁碟機會喺呢度出現，方便瀏覽、卸載或強制卸載。")
            : P("Mounted volumes are hidden until you refresh. This avoids showing local drive labels in screenshots.",
                "已掛載磁碟區會隱藏到你重新整理為止，避免截圖顯示本機磁碟標籤。");
    }

    private void FillCombos()
    {
        if (CreateSizeUnit.Items.Count > 0) return; // only once
        CreateSizeUnit.Items.Add("MB");
        CreateSizeUnit.Items.Add("GB");
        CreateSizeUnit.Items.Add("TB");
        CreateSizeUnit.SelectedIndex = 0;

        foreach (var a in VaultVolumeService.Algorithms) CreateAlgoBox.Items.Add(P(a.En, a.Zh));
        CreateAlgoBox.SelectedIndex = 0;
        foreach (var h in VaultVolumeService.Hashes) CreateHashBox.Items.Add(P(h.En, h.Zh));
        CreateHashBox.SelectedIndex = 0;
        foreach (var f in VaultVolumeService.FileSystems) CreateFsBox.Items.Add(P(f.En, f.Zh));
        CreateFsBox.SelectedIndex = 0;

        foreach (var c in VaultVolumeService.FreeDriveLetters()) MountLetterBox.Items.Add($"{c}:");
        if (MountLetterBox.Items.Count > 0) MountLetterBox.SelectedIndex = 0;
    }

    private void CheckEngine()
    {
        EngineActionHost.Children.Clear();
        if (VaultVolumeService.FindMountBinary() is not null)
        {
            if (VaultVolumeService.IsBundledPresent())
            {
                EngineBar.IsOpen = false;
                return;
            }
            // Found only an upstream fallback install — works, but nudge towards the bundled engine.
            EngineBar.Severity = InfoBarSeverity.Informational;
            EngineBar.Title = P("Engine detected", "已偵測到引擎");
            EngineBar.Message = P("Using a system encryption engine as a fallback. The bundled WinForge Vault engine was not found in the app folder.",
                "正用系統加密引擎作為退路。喺 app 資料夾搵唔到隨附嘅 WinForge 保險庫引擎。");
            EngineBar.IsOpen = true;
            return;
        }

        EngineBar.Severity = InfoBarSeverity.Warning;
        EngineBar.Title = P("Engine missing", "缺少引擎");
        EngineBar.Message = P("The WinForge Vault engine (and its signed kernel driver) is not installed. Create/mount need it and an administrator session.",
            "未安裝 WinForge 保險庫引擎（連已簽署嘅核心驅動程式）。建立／掛載需要佢同管理員工作階段。");
        EngineBar.IsOpen = true;
        // Fallback installer points at the upstream package only if the bundled binary is absent.
        EngineActionHost.Children.Add(EngineBars.AutoInstallButton(
            "VeraCrypt.VeraCrypt",
            "Install engine", "安裝引擎",
            async () => { await Task.CompletedTask; CheckEngine(); Reload(); }));
    }

    private void Reload()
    {
        _hasScanned = true;
        var mounted = VaultVolumeService.ListMounted();
        var rows = mounted.Select(m => new Row
        {
            Letter = m.Letter,
            TitleText = string.IsNullOrEmpty(m.Label) ? m.Letter : $"{m.Letter}  {m.Label}",
            SubText = $"{m.FileSystem} · {m.FreeText} {P("free of", "可用 /")} {m.SizeText}",
        }).ToList();
        List.ItemsSource = rows;
        List.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Visibility = rows.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowUnscannedState()
    {
        _hasScanned = false;
        List.ItemsSource = Array.Empty<Row>();
        List.Visibility = Visibility.Collapsed;
        EmptyHint.Visibility = Visibility.Visible;
        EmptyHint.Text = P("Mounted volumes are hidden until you refresh. This avoids showing local drive labels in screenshots.",
            "已掛載磁碟區會隱藏到你重新整理為止，避免截圖顯示本機磁碟標籤。");
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Reload();

    // ===================== pickers =====================

    private async void PickCreatePath_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync("vault" + VaultVolumeService.ContainerExtension,
            VaultVolumeService.ContainerExtension, ".hc", ".tc");
        if (path is not null) CreatePathBox.Text = path;
    }

    private async void PickCreateKeyfile_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync();
        if (path is not null) CreateKeyfileBox.Text = path;
    }

    private async void PickMountPath_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(VaultVolumeService.ContainerExtension, ".hc", ".tc", ".*");
        if (path is not null) MountPathBox.Text = path;
    }

    private async void PickKeyfile_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync();
        if (path is not null) MountKeyfileBox.Text = path;
    }

    // ===================== create =====================

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CreatePathBox.Text))
        { Warn(P("Choose a container file path first.", "先揀容器檔案路徑。")); return; }
        if (CreatePwdBox.Password.Length == 0)
        { Warn(P("Enter a volume password.", "輸入磁碟區密碼。")); return; }

        long mult = CreateSizeUnit.SelectedIndex switch { 2 => 1024L * 1024 * 1024 * 1024, 1 => 1024L * 1024 * 1024, _ => 1024L * 1024 };
        long bytes = (long)(CreateSizeBox.Value * mult);

        var algo = VaultVolumeService.Algorithms[Math.Max(0, CreateAlgoBox.SelectedIndex)].Cli;
        var hash = VaultVolumeService.Hashes[Math.Max(0, CreateHashBox.SelectedIndex)].Cli;
        var fs = VaultVolumeService.FileSystems[Math.Max(0, CreateFsBox.SelectedIndex)].Cli;

        // Destructive: creating overwrites the target file — confirm first.
        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Create container?", "建立容器？"),
            Content = P($"This writes a {VaultVolumeService.Human(bytes)} encrypted container to:\n{CreatePathBox.Text}\nAny existing file at that path will be overwritten.",
                $"會寫入一個 {VaultVolumeService.Human(bytes)} 嘅加密容器到：\n{CreatePathBox.Text}\n該路徑現有嘅檔案會被覆寫。"),
            PrimaryButtonText = P("Create", "建立"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        await Run(() => VaultVolumeService.CreateContainerAsync(
            CreatePathBox.Text, bytes, CreatePwdBox.Password, algo, hash, fs,
            (int)CreatePimBox.Value,
            string.IsNullOrWhiteSpace(CreateKeyfileBox.Text) ? null : CreateKeyfileBox.Text,
            CreateDynamicChk.IsChecked == true, CreateQuickChk.IsChecked == true),
            P("Create", "建立"));

        // Pre-fill the mount box with what we just created for a smooth create → mount flow.
        if (System.IO.File.Exists(CreatePathBox.Text)) MountPathBox.Text = CreatePathBox.Text;
        CreatePwdBox.Password = "";
    }

    // ===================== mount / dismount =====================

    private async void Mount_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MountPathBox.Text))
        { Warn(P("Choose a container to mount.", "揀一個要掛載嘅容器。")); return; }
        if (MountLetterBox.SelectedItem is not string letterStr || letterStr.Length == 0)
        { Warn(P("Pick a drive letter.", "揀一個磁碟機代號。")); return; }
        if (MountPwdBox.Password.Length == 0)
        { Warn(P("Enter the volume password.", "輸入磁碟區密碼。")); return; }

        char letter = letterStr[0];
        await Run(() => VaultVolumeService.MountAsync(
            MountPathBox.Text, letter, MountPwdBox.Password,
            (int)MountPimBox.Value,
            string.IsNullOrWhiteSpace(MountKeyfileBox.Text) ? null : MountKeyfileBox.Text,
            MountReadOnlyChk.IsChecked == true, MountRemovableChk.IsChecked == true,
            MountExploreChk.IsChecked == true),
            P("Mount", "掛載"));
        MountPwdBox.Password = "";
        FillFreeLetters();
    }

    private async void Dismount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string letter && letter.Length > 0)
            await Run(() => VaultVolumeService.DismountAsync(letter[0], false), P("Dismount", "卸載"));
    }

    private async void ForceDismount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string letter || letter.Length == 0) return;
        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Force dismount?", "強制卸載？"),
            Content = P($"Force-dismounting {letter} ignores open files and can cause data loss in apps using it. Continue?",
                $"強制卸載 {letter} 會無視開住嘅檔案，正用緊佢嘅程式可能會遺失資料。繼續？"),
            PrimaryButtonText = P("Force dismount", "強制卸載"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        await Run(() => VaultVolumeService.DismountAsync(letter[0], true), P("Force dismount", "強制卸載"));
    }

    private async void DismountAll_Click(object sender, RoutedEventArgs e)
        => await Run(() => VaultVolumeService.DismountAllAsync(false), P("Dismount all", "全部卸載"));

    private async void Explore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string letter && letter.Length > 0)
            await VaultVolumeService.ExploreAsync(letter[0]);
    }

    // ===================== other actions =====================

    private async void ChangePwd_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(VaultVolumeService.ContainerExtension, ".hc", ".tc", ".*");
        if (path is null) return;
        await Run(() => VaultVolumeService.ChangePasswordAsync(path), P("Change password", "更改密碼"));
    }

    private async void Benchmark_Click(object sender, RoutedEventArgs e)
        => await Run(() => VaultVolumeService.LaunchGuiAsync(), P("Open", "打開"));

    private async void WipeCache_Click(object sender, RoutedEventArgs e)
        => await Run(() => VaultVolumeService.WipeCacheAsync(), P("Wipe cache", "清除快取"));

    // ===================== plumbing =====================

    private void FillFreeLetters()
    {
        var current = MountLetterBox.SelectedItem as string;
        MountLetterBox.Items.Clear();
        foreach (var c in VaultVolumeService.FreeDriveLetters()) MountLetterBox.Items.Add($"{c}:");
        if (current is not null && MountLetterBox.Items.Contains(current)) MountLetterBox.SelectedItem = current;
        else if (MountLetterBox.Items.Count > 0) MountLetterBox.SelectedIndex = 0;
    }

    private async Task Run(Func<Task<Models.TweakResult>> op, string verb)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var r = await op();
            bool needAdmin = !r.Success && !AdminHelper.IsElevated;
            ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            ResultBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
            ResultBar.Message = needAdmin
                ? P($"{verb} needs administrator rights — relaunch WinForge as admin.", $"{verb}需要管理員權限 — 請以管理員身分重開 WinForge。")
                : ((Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? r.Output ?? "");
            ResultBar.IsOpen = true;
        }
        finally { _busy = false; }
        Reload();
    }

    private void Warn(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Title = P("Heads up", "注意");
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
