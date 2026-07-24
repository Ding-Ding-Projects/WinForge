using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DewEncryption.Core;

namespace DewEncryption.Gui;

public sealed partial class MainWindow : Window
{
    private static readonly Regex DockerNamespacePattern = new("^[a-z0-9]+(?:[._-][a-z0-9]+)*$");
    private static readonly Regex DockerTagPattern = new("^[A-Za-z0-9_][A-Za-z0-9._-]{0,127}$");
    private static readonly Regex DockerHubReferencePattern = new(
        "^(?:docker\\.io|index\\.docker\\.io|registry-1\\.docker\\.io)/(?<user>[a-z0-9]+(?:[._-][a-z0-9]+)*)/(?<repo>[a-z0-9]+(?:[._-][a-z0-9]+)*)(?::(?<tag>[A-Za-z0-9_][A-Za-z0-9._-]{0,127}))?$");

    private readonly DewCliService cliService = new();
    private readonly DewDriveService driveService = new();
    private readonly DewSelectionService selectionService = new();
    private readonly SettingsService settingsService = new();
    private readonly DewSecretService secretService = new();
    private readonly DewStartupService startupService = new();
    private readonly ObservableCollection<DewPathItem> selectedPaths = [];
    private readonly ObservableCollection<string> containers = [];
    private readonly ObservableCollection<string> driveProfiles = [];
    private readonly Dictionary<string, FileSystemWatcher> driveWatchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> driveSyncDebounces = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> driveSyncInProgress = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool autoSyncOnLaunch;
    private readonly bool minimizedOnLaunch;
    private AppSettings settings;

    public MainWindow()
        : this([])
    {
    }

    public MainWindow(IReadOnlyList<string>? args = null)
    {
        autoSyncOnLaunch = args?.Any(arg => string.Equals(arg, "--auto-sync", StringComparison.OrdinalIgnoreCase)) == true;
        minimizedOnLaunch = args?.Any(arg => string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase)) == true;
        InitializeComponent();
        settings = settingsService.Load();
        FilesGrid.ItemsSource = selectedPaths;
        ContainersList.ItemsSource = containers;
        DriveProfilesList.ItemsSource = driveProfiles;
        RefreshContainerList();
        RefreshDriveList();
        LoadVeraCryptSettings();
        UpdateSelectionContext();
        LoadStartupState();
        Log("Ready.");
        Opened += MainWindow_Opened;
    }

    private async void MainWindow_Opened(object? sender, EventArgs e)
    {
        if (minimizedOnLaunch)
        {
            WindowState = WindowState.Minimized;
        }

        if (autoSyncOnLaunch)
        {
            await StartSavedDriveAutoSyncProfilesAsync();
        }
    }

    private void LoadStartupState()
    {
        DriveStartAtLoginBox.IsEnabled = startupService.IsSupported;
        DriveStartAtLoginBox.IsChecked = startupService.IsEnabledForCurrentUser();
    }

    private async void AddFiles_Click(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add files to Dew Encryption",
            AllowMultiple = true,
        });

        foreach (IStorageFile file in files)
        {
            AddPath(file.Path.LocalPath, "File");
        }
    }

    private async void AddFolder_Click(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Add a folder to Dew Encryption",
            AllowMultiple = false,
        });

        foreach (IStorageFolder folder in folders)
        {
            AddPath(folder.Path.LocalPath, "Folder");
        }
    }

    private void RemoveSelected_Click(object? sender, RoutedEventArgs e)
    {
        List<DewPathItem> items = FilesGrid.SelectedItems.Cast<DewPathItem>().ToList();
        selectionService.RemovePaths(items);
        foreach (DewPathItem item in items)
        {
            selectedPaths.Remove(item);
        }

        UpdateSelectionContext();
        Log($"Removed {items.Count} selected item(s).");
    }

    private async void RunSelected_Click(object? sender, RoutedEventArgs e)
    {
        if (selectedPaths.Count == 0)
        {
            SetStatus("Nothing selected.");
            Log("Add at least one file or folder before running Dew Encryption.");
            return;
        }

        await RunAndLogAsync(cliService.RunSelectedAsync(selectedPaths.Select(item => item.Path)));
    }

    private async void OpenHelp_Click(object? sender, RoutedEventArgs e)
    {
        await RunAndLogAsync(cliService.RunHelpAsync());
    }

    private async void RefreshHistory_Click(object? sender, RoutedEventArgs e)
    {
        DewPathItem? first = selectedPaths.FirstOrDefault();
        if (first is null)
        {
            SetStatus("Nothing selected.");
            Log("Select a file or folder before refreshing history.");
            return;
        }

        await RunAndLogAsync(cliService.RefreshHistoryAsync(first.Path));
    }

    private async void SnapshotHistory_Click(object? sender, RoutedEventArgs e)
    {
        DewPathItem? first = selectedPaths.FirstOrDefault();
        if (first is null)
        {
            SetStatus("Nothing selected.");
            Log("Select a file or folder before snapshotting history.");
            return;
        }

        await RunAndLogAsync(cliService.SnapshotHistoryAsync(first.Path));
    }

    private async void OpenPythonHistoryGui_Click(object? sender, RoutedEventArgs e)
    {
        DewPathItem? first = selectedPaths.FirstOrDefault();
        if (first is null)
        {
            SetStatus("Nothing selected.");
            Log("Select a file or folder before opening the history manager.");
            return;
        }

        await RunAndLogAsync(cliService.OpenHistoryGuiAsync(first.Path));
    }

    private void RegisterContainer_Click(object? sender, RoutedEventArgs e)
    {
        DewContainerProfile profile = ReadContainerProfile();
        if (string.IsNullOrWhiteSpace(profile.Path))
        {
            SetStatus("Container path required.");
            Log("Enter a container path before saving a container profile.");
            return;
        }

        List<ContainerProfile> profiles = (settings.Containers ?? []).ToList();
        int existingIndex = profiles.FindIndex(item =>
            string.Equals(item.Path, profile.Path, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Name, profile.Name, StringComparison.OrdinalIgnoreCase));

        IReadOnlyList<HookAction>? existingHooks = existingIndex >= 0 ? profiles[existingIndex].Hooks : [];
        ContainerProfile savedProfile = new(profile.Name, profile.Path, profile.MountPath, profile.Theme, existingHooks);
        if (existingIndex >= 0)
        {
            profiles[existingIndex] = savedProfile;
        }
        else
        {
            profiles.Add(savedProfile);
        }

        settings = settings with { Containers = profiles };
        settingsService.Save(settings);
        RefreshContainerList();
        ContainerStatusText.Text = $"Saved: {profile.DisplayLabel}";
        SetStatus("Container saved.");
        Log($"Saved container profile: {profile.DisplayLabel}");
    }

    private async void SnapshotContainer_Click(object? sender, RoutedEventArgs e)
    {
        DewContainerProfile profile = ReadContainerProfile();
        if (string.IsNullOrWhiteSpace(profile.Path))
        {
            SetStatus("Container path required.");
            Log("Enter a container path before snapshotting container history.");
            return;
        }

        await RunAndLogAsync(cliService.SnapshotContainerHistoryAsync(profile.Path));
    }

    private async void ViewContainerHistory_Click(object? sender, RoutedEventArgs e)
    {
        DewContainerProfile profile = ReadContainerProfile();
        if (string.IsNullOrWhiteSpace(profile.Path))
        {
            SetStatus("Container path required.");
            Log("Enter a container path before viewing container history.");
            return;
        }

        await RunAndLogAsync(cliService.ViewContainerHistoryAsync(profile.Path));
    }

    private async void TestOpenHooks_Click(object? sender, RoutedEventArgs e)
    {
        await TestHooksAsync("open");
    }

    private async void TestCloseHooks_Click(object? sender, RoutedEventArgs e)
    {
        await TestHooksAsync("close");
    }

    private async Task TestHooksAsync(string eventName)
    {
        DewContainerProfile profile = ReadContainerProfile();
        if (string.IsNullOrWhiteSpace(profile.Path))
        {
            SetStatus("Container path required.");
            Log($"Enter a container path before testing {eventName} hooks.");
            return;
        }

        await RunAndLogAsync(cliService.TestContainerHooksAsync(eventName, profile.Path, profile.MountPath));
    }

    private void ContainersList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        int index = ContainersList.SelectedIndex;
        ContainerProfile? profile = index >= 0 ? (settings.Containers ?? []).ElementAtOrDefault(index) : null;
        if (profile is null)
        {
            return;
        }

        DewThemeSettings theme = profile.Theme ?? new DewThemeSettings();
        ContainerNameBox.Text = profile.Name;
        ContainerPathBox.Text = profile.Path;
        MountPathBox.Text = profile.MountPath;
        FontFamilyBox.Text = theme.FontFamily;
        BackgroundBox.Text = theme.Background;
        ForegroundBox.Text = theme.Foreground;
        AccentBox.Text = theme.Accent;
        FontSizeBox.Text = theme.FontSize.ToString();
        ContainerStatusText.Text = profile.Path;
    }

    private DewContainerProfile ReadContainerProfile()
    {
        string name = string.IsNullOrWhiteSpace(ContainerNameBox.Text) ? "Container" : ContainerNameBox.Text.Trim();
        string path = ContainerPathBox.Text?.Trim() ?? string.Empty;
        string mountPath = MountPathBox.Text?.Trim() ?? string.Empty;
        int fontSize = int.TryParse(FontSizeBox.Text, out int parsedFontSize) ? parsedFontSize : 10;
        DewThemeSettings theme = new(
            FontFamilyBox.Text?.Trim() ?? "Segoe UI",
            BackgroundBox.Text?.Trim() ?? "#172033",
            ForegroundBox.Text?.Trim() ?? "#F8FAFC",
            AccentBox.Text?.Trim() ?? "#0F766E",
            fontSize);
        return new DewContainerProfile(name, path, mountPath, theme, HookBox.Text?.Trim() ?? string.Empty);
    }

    private void NewDrive_Click(object? sender, RoutedEventArgs e)
    {
        string name = string.IsNullOrWhiteSpace(DriveNameBox.Text) ? "Dew Drive" : DriveNameBox.Text.Trim();
        string folder = DriveFolderBox.Text?.Trim() ?? string.Empty;
        List<DewDriveProfile> profiles = (settings.DewDrives?.Drives ?? []).ToList();
        int existingIndex = FindDriveProfileIndex(profiles, name, folder);
        DewDriveProfile savedProfile = ReadDriveProfile(existingIndex >= 0 ? profiles[existingIndex] : null);
        if (string.IsNullOrWhiteSpace(DriveFolder(savedProfile)))
        {
            SetStatus("Stream folder required.");
            Log("Choose a local stream folder before saving a Dew Drive profile.");
            return;
        }

        if (existingIndex >= 0)
        {
            profiles[existingIndex] = savedProfile;
        }
        else
        {
            profiles.Add(savedProfile);
        }

        DewDriveSettings current = settings.DewDrives ?? new DewDriveSettings(Drives: []);
        settings = settings with { DewDrives = current with { Drives = profiles } };
        settingsService.Save(settings);
        RefreshDriveList();
        DriveStatusText.Text = $"Saved: {DriveLabel(savedProfile)}";
        SetStatus("Dew Drive saved.");
        Log($"Saved Dew Drive profile: {DriveLabel(savedProfile)}");
    }

    private void DeleteDrive_Click(object? sender, RoutedEventArgs e)
    {
        List<DewDriveProfile> profiles = (settings.DewDrives?.Drives ?? []).ToList();
        int index = DriveProfilesList.SelectedIndex;
        if (index < 0 || index >= profiles.Count)
        {
            string name = DriveNameBox.Text?.Trim() ?? string.Empty;
            string folder = DriveFolderBox.Text?.Trim() ?? string.Empty;
            index = string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(folder)
                ? -1
                : FindDriveProfileIndex(profiles, name, folder);
        }

        if (index < 0 || index >= profiles.Count)
        {
            SetStatus("No profile selected.");
            Log("Select a saved Dew Drive profile before deleting.");
            return;
        }

        DewDriveProfile removed = profiles[index];
        profiles.RemoveAt(index);
        StopDriveAutoSync(removed.Name);
        DewDriveSettings current = settings.DewDrives ?? new DewDriveSettings(Drives: []);
        settings = settings with { DewDrives = current with { Drives = profiles } };
        settingsService.Save(settings);
        RefreshDriveList();
        DriveStatusText.Text = $"Deleted: {DriveLabel(removed)}";
        SetStatus("Dew Drive profile deleted.");
        Log($"Deleted Dew Drive profile: {DriveLabel(removed)} (local folder kept on disk)");
    }

    private void UseDockerHub_Click(object? sender, RoutedEventArgs e)
    {
        string user = DockerHubUserBox.Text?.Trim().ToLowerInvariant() ?? string.Empty;
        string repo = DockerHubRepoBox.Text?.Trim().ToLowerInvariant() ?? string.Empty;
        string tag = DockerHubTagBox.Text?.Trim() ?? string.Empty;
        if (repo.Length == 0)
        {
            repo = "dew-drive";
        }

        if (tag.Length == 0)
        {
            tag = "latest";
        }

        if (!DockerNamespacePattern.IsMatch(user))
        {
            SetStatus("Docker Hub username needed.");
            Log("Enter your Docker Hub username (lowercase letters and digits, with . _ - separators) to build an upload tag.");
            return;
        }

        if (!DockerNamespacePattern.IsMatch(repo))
        {
            SetStatus("Repository name invalid.");
            Log("Docker Hub repository names use lowercase letters and digits with . _ - separators.");
            return;
        }

        if (!DockerTagPattern.IsMatch(tag))
        {
            SetStatus("Tag invalid.");
            Log("Docker tags use letters, digits, and . _ - (up to 128 characters).");
            return;
        }

        string reference = $"docker.io/{user}/{repo}:{tag}";
        DriveRegistryImageBox.Text = reference;
        DockerHubUserBox.Text = user;
        DockerHubRepoBox.Text = repo;
        DockerHubTagBox.Text = tag;
        DriveStatusText.Text = $"Upload tag set: {reference}";
        SetStatus("Docker Hub tag ready.");
        Log($"Docker Hub upload tag set: {reference}");
        Log("Run 'docker login' once with this Docker Hub account so Push can upload.");
    }

    private void FillDockerHubHelperFromReference(string reference)
    {
        Match match = DockerHubReferencePattern.Match(reference.Trim());
        if (!match.Success)
        {
            return;
        }

        DockerHubUserBox.Text = match.Groups["user"].Value;
        DockerHubRepoBox.Text = match.Groups["repo"].Value;
        DockerHubTagBox.Text = match.Groups["tag"].Success && match.Groups["tag"].Value.Length > 0 ? match.Groups["tag"].Value : "latest";
    }

    private async void PickDriveFolder_Click(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select local Dew Drive folder",
            AllowMultiple = false,
        });

        IStorageFolder? folder = folders.FirstOrDefault();
        if (folder is not null)
        {
            DriveFolderBox.Text = folder.Path.LocalPath;
            DriveStatusText.Text = folder.Path.LocalPath;
            Log($"Selected stream folder: {folder.Path.LocalPath}");
        }
    }

    private async void AddDriveFiles_Click(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Copy files into Dew Drive",
            AllowMultiple = true,
        });

        AppendDriveItems(files.Select(file => file.Path.LocalPath));
    }

    private async void AddDriveFolder_Click(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Copy folder into Dew Drive",
            AllowMultiple = false,
        });

        AppendDriveItems(folders.Select(folder => folder.Path.LocalPath));
    }

    private async void SyncDrive_Click(object? sender, RoutedEventArgs e)
    {
        await RunDewDriveAsync("sync", push: false);
    }

    private async void SyncPushDrive_Click(object? sender, RoutedEventArgs e)
    {
        await RunDewDriveAsync("sync", push: true);
    }

    private async void PullDrive_Click(object? sender, RoutedEventArgs e)
    {
        await RunDewDriveAsync("pull", push: false);
    }

    private async void RestoreDrive_Click(object? sender, RoutedEventArgs e)
    {
        await RunDewDriveAsync("restore", push: false);
    }

    private async void InstallVeraCrypt_Click(object? sender, RoutedEventArgs e)
    {
        SetStatus("Installing VeraCrypt...");
        Log("Installing VeraCrypt with winget.");
        await RunAndLogAsync(cliService.InstallVeraCryptAsync());
        string? path = DewDependencyService.FindVeraCryptPath(DriveVeraCryptPathBox.Text?.Trim() ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(path))
        {
            DriveVeraCryptPathBox.Text = path;
            SaveVeraCryptSettingsLocal();
            Log($"VeraCrypt detected: {path}");
        }
        else
        {
            Log("VeraCrypt was not detected after install. Check winget output above.");
        }
    }

    private async void SaveVeraCryptSettings_Click(object? sender, RoutedEventArgs e)
    {
        SaveVeraCryptSettingsLocal();
        await RunAndLogAsync(cliService.SaveVeraCryptSettingsAsync(settings.Veracrypt));
    }

    private async void StartDriveAutoSync_Click(object? sender, RoutedEventArgs e)
    {
        DewDriveProfile profile = SaveDriveProfileFromForm();
        if (await StartDriveAutoSyncAsync(profile))
        {
            DriveAutoPushBox.IsChecked = true;
            SaveDriveProfileFromForm();
        }
    }

    private void StopDriveAutoSync_Click(object? sender, RoutedEventArgs e)
    {
        string profileName = string.IsNullOrWhiteSpace(DriveNameBox.Text) ? string.Empty : DriveNameBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(profileName) && driveWatchers.ContainsKey(profileName))
        {
            StopDriveAutoSync(profileName);
            SetStatus("Auto sync stopped.");
            Log($"Auto sync stopped: {profileName}");
        }
        else
        {
            StopDriveAutoSync();
            SetStatus("Auto sync stopped.");
            Log("Auto sync stopped.");
        }
    }

    private void DriveStartAtLogin_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DriveStartAtLoginBox.IsChecked == true)
            {
                startupService.EnableForCurrentUser();
                SetStatus("Startup enabled.");
                Log("Dew Drive auto-sync will start when this Windows user signs in.");
            }
            else
            {
                startupService.DisableForCurrentUser();
                SetStatus("Startup disabled.");
                Log("Dew Drive auto-sync startup entry removed.");
            }
        }
        catch (Exception ex)
        {
            DriveStartAtLoginBox.IsChecked = startupService.IsEnabledForCurrentUser();
            SetStatus("Startup update failed.");
            Log(ex.Message);
        }
    }

    private async Task StartSavedDriveAutoSyncProfilesAsync()
    {
        settings = settingsService.Load();
        List<DewDriveProfile> startupProfiles = (settings.DewDrives?.Drives ?? [])
            .Where(profile => profile.AutoPush)
            .ToList();
        if (startupProfiles.Count == 0)
        {
            Log("Startup auto-sync found no profiles marked Auto push.");
            return;
        }

        int started = 0;
        foreach (DewDriveProfile profile in startupProfiles)
        {
            if (await StartDriveAutoSyncAsync(profile, fromStartup: true))
            {
                started++;
            }
        }

        SetStatus(started == 0 ? "Startup sync waiting." : $"Startup sync running: {started}");
    }

    private async Task<bool> StartDriveAutoSyncAsync(DewDriveProfile profile, bool fromStartup = false)
    {
        string folder = DriveFolder(profile);
        if (string.IsNullOrWhiteSpace(folder))
        {
            SetStatus("Stream folder required.");
            Log($"Choose a local stream folder before starting auto sync for {profile.Name}.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(DriveRegistry(profile)))
        {
            SetStatus("Upload target required.");
            Log("Enter an upload image tag like docker.io/your-name/dew-drive:latest.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(GetDrivePassword(profile)))
        {
            SetStatus("Password required.");
            Log($"Enter the Dew Drive password once and save {profile.Name} to enable startup sync.");
            return false;
        }

        if (!await EnsureEncryptionReadyAsync(profile.EncryptionMode))
        {
            return false;
        }

        Directory.CreateDirectory(folder);
        StopDriveAutoSync(profile.Name);
        FileSystemWatcher watcher = new(folder)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        watcher.Changed += (_, e) => DriveWatcher_Changed(profile, e);
        watcher.Created += (_, e) => DriveWatcher_Changed(profile, e);
        watcher.Deleted += (_, e) => DriveWatcher_Changed(profile, e);
        watcher.Renamed += (_, e) => DriveWatcher_Changed(profile, e);
        driveWatchers[profile.Name] = watcher;

        DriveStatusText.Text = fromStartup ? $"Startup auto sync running: {profile.Name}" : $"Auto sync running: {folder}";
        SetStatus("Auto sync running.");
        Log($"Auto sync started: {profile.Name} -> {DriveRegistry(profile)}");
        return true;
    }

    private void DriveProfilesList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        int index = DriveProfilesList.SelectedIndex;
        DewDriveProfile? profile = index >= 0 ? (settings.DewDrives?.Drives ?? []).ElementAtOrDefault(index) : null;
        if (profile is null)
        {
            return;
        }

        DriveNameBox.Text = profile.Name;
        DriveFolderBox.Text = DriveFolder(profile);
        DriveRegistryImageBox.Text = DriveRegistry(profile);
        FillDockerHubHelperFromReference(DriveRegistry(profile));
        SetDriveEncryptionMode(profile.EncryptionMode);
        DriveAutoPushBox.IsChecked = profile.AutoPush;
        DrivePasswordBox.Text = string.Empty;
        DrivePasswordBox.Watermark = string.IsNullOrWhiteSpace(profile.ProtectedPassword) ? "Password" : "Password saved on this Windows user";
        DriveCommitBox.Text = "HEAD";
        DriveStatusText.Text = string.IsNullOrWhiteSpace(profile.LastSync)
            ? $"{DriveLabel(profile)} (never synced)"
            : $"{DriveLabel(profile)} (last sync {profile.LastSync})";
    }

    private void AppendDriveItems(IEnumerable<string> paths)
    {
        List<string> pathList = paths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (pathList.Count == 0)
        {
            return;
        }

        string folder = DriveFolderBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(folder))
        {
            foreach (string path in pathList)
            {
                DriveItemsBox.Text += $"{path}{Environment.NewLine}";
                Log($"Staged Dew Drive item: {path}");
            }

            DriveStatusText.Text = "Choose a local folder to copy staged items.";
            return;
        }

        try
        {
            IReadOnlyList<string> copied = driveService.CopyIntoFolder(folder, pathList);
            foreach (string path in copied)
            {
                DriveItemsBox.Text += $"Copied: {path}{Environment.NewLine}";
                Log($"Copied into Dew Drive: {path}");
            }

            DriveStatusText.Text = $"Copied {copied.Count} item(s) into {folder}";
            SetStatus("Dew Drive copy complete.");
        }
        catch (Exception ex)
        {
            SetStatus("Dew Drive copy failed.");
            Log(ex.Message);
        }
    }

    private async Task RunDewDriveAsync(string command, bool push)
    {
        DewDriveProfile profile = SaveDriveProfileFromForm();
        string folder = DriveFolder(profile);
        if (string.IsNullOrWhiteSpace(folder))
        {
            SetStatus("Drive folder required.");
            Log("Choose a local Dew Drive folder before running a drive command.");
            return;
        }

        string registryImage = DriveRegistry(profile);
        string password = GetDrivePassword(profile);
        if (command == "sync")
        {
            if (push && string.IsNullOrWhiteSpace(registryImage))
            {
                SetStatus("Upload target required.");
                Log("Enter an upload image tag like docker.io/your-name/dew-drive:latest before using Sync + Push.");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                SetStatus("Password required.");
                Log("Enter the Dew Drive password once before syncing.");
                return;
            }

            if (!await EnsureEncryptionReadyAsync(profile.EncryptionMode))
            {
                return;
            }

            DewCommandResult? result = await RunAndLogAsync(cliService.SyncDewDriveProfileAsync(profile.Name, registryImage, push, password));
            if (result?.ExitCode == 0)
            {
                MarkDriveProfileSynced(profile.Name);
            }

            return;
        }

        if (command == "pull")
        {
            if (string.IsNullOrWhiteSpace(registryImage))
            {
                SetStatus("Remote required.");
                Log("Enter a registry image before pulling a Dew Drive.");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                SetStatus("Password required.");
                Log("Enter the Dew Drive password before pulling a remote image.");
                return;
            }

            await RunAndLogAsync(cliService.PullDewDriveAsync(registryImage, folder, password));
            return;
        }

        if (command == "restore")
        {
            string commit = string.IsNullOrWhiteSpace(DriveCommitBox.Text) ? "HEAD" : DriveCommitBox.Text.Trim();
            await RunAndLogAsync(cliService.RestoreDewDriveFolderAsync(folder, commit));
        }
    }

    private void AddPath(string path, string kind)
    {
        if (!selectionService.AddPath(path, kind))
        {
            SetStatus("Already selected.");
            return;
        }

        DewPathItem item = selectionService.SelectedPaths[^1];
        selectedPaths.Add(item);
        UpdateSelectionContext();
        Log($"Added {kind.ToLowerInvariant()}: {path}");
    }

    private async Task<DewCommandResult?> RunAndLogAsync(Task<DewCommandResult> commandTask)
    {
        SetStatus("Running...");
        try
        {
            DewCommandResult result = await commandTask;
            Log($"> {result.FileName} {string.Join(' ', result.Arguments.Select(QuoteForLog))}");
            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                Log(result.StandardOutput.TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                Log(result.StandardError.TrimEnd());
            }

            Log($"Exit code: {result.ExitCode}");
            SetStatus(result.ExitCode == 0 ? "Complete." : $"Exited with code {result.ExitCode}.");
            return result;
        }
        catch (Exception ex)
        {
            SetStatus("Command failed.");
            Log(ex.Message);
            return null;
        }
    }

    private void RefreshContainerList()
    {
        settings = settingsService.Load();
        containers.Clear();
        foreach (ContainerProfile profile in settings.Containers ?? [])
        {
            string label = string.IsNullOrWhiteSpace(profile.Path) ? profile.Name : $"{profile.Name} - {profile.Path}";
            containers.Add(label);
        }

        ContainerStatusText.Text = containers.Count == 0 ? "No saved containers." : $"{containers.Count} saved container(s).";
    }

    private void RefreshDriveList()
    {
        settings = settingsService.Load();
        driveProfiles.Clear();
        foreach (DewDriveProfile profile in settings.DewDrives?.Drives ?? [])
        {
            driveProfiles.Add(DriveLabel(profile));
        }

        DriveStatusText.Text = driveProfiles.Count == 0 ? "No saved Dew Drive profiles." : $"{driveProfiles.Count} saved Dew Drive profile(s).";
    }

    private DewDriveProfile SaveDriveProfileFromForm()
    {
        string name = string.IsNullOrWhiteSpace(DriveNameBox.Text) ? "Dew Drive" : DriveNameBox.Text.Trim();
        string folder = DriveFolderBox.Text?.Trim() ?? string.Empty;
        List<DewDriveProfile> profiles = (settings.DewDrives?.Drives ?? []).ToList();
        int existingIndex = FindDriveProfileIndex(profiles, name, folder);
        DewDriveProfile profile = ReadDriveProfile(existingIndex >= 0 ? profiles[existingIndex] : null);
        if (existingIndex >= 0)
        {
            profiles[existingIndex] = profile;
        }
        else
        {
            profiles.Add(profile);
        }

        DewDriveSettings current = settings.DewDrives ?? new DewDriveSettings(Drives: []);
        settings = settings with { DewDrives = current with { Drives = profiles } };
        settingsService.Save(settings);
        RefreshDriveList();
        return profile;
    }

    private DewDriveProfile ReadDriveProfile(DewDriveProfile? existingProfile = null)
    {
        string name = string.IsNullOrWhiteSpace(DriveNameBox.Text) ? "Dew Drive" : DriveNameBox.Text.Trim();
        string folder = DriveFolderBox.Text?.Trim() ?? string.Empty;
        string registry = DriveRegistryImageBox.Text?.Trim() ?? string.Empty;
        string mode = SelectedDriveMode();
        string protectedPassword = existingProfile?.ProtectedPassword ?? string.Empty;
        string password = DrivePasswordBox.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(password))
        {
            try
            {
                string protectedCandidate = secretService.ProtectForCurrentUser(password);
                if (string.IsNullOrWhiteSpace(protectedCandidate))
                {
                    Log("Password could not be stored for startup on this platform.");
                }
                else
                {
                    protectedPassword = protectedCandidate;
                }
            }
            catch (Exception ex)
            {
                Log($"Password could not be stored for startup: {ex.Message}");
            }
        }

        return new DewDriveProfile(
            Name: name,
            LocalPath: folder,
            Folder: folder,
            RegistryRef: registry,
            RegistryImage: registry,
            EncryptionMode: mode,
            LastSync: existingProfile?.LastSync ?? string.Empty,
            AutoPush: DriveAutoPushBox.IsChecked == true,
            IncludePatterns: existingProfile?.IncludePatterns ?? [],
            ExcludePatterns: existingProfile?.ExcludePatterns ?? [],
            ProtectedPassword: protectedPassword);
    }

    private string SelectedDriveMode()
    {
        return (DriveEncryptionModeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "veracrypt";
    }

    private void SetDriveEncryptionMode(string mode)
    {
        string normalized = string.IsNullOrWhiteSpace(mode) ? "7zip" : mode.Trim().ToLowerInvariant();
        DriveEncryptionModeBox.SelectedIndex = normalized is "7z" or "7zip" or "archive" or "password" or "standard" ? 0 : 1;
    }

    private void LoadVeraCryptSettings()
    {
        VeraCryptSettings vc = settings.Veracrypt;
        DriveVcEncryptionBox.Text = vc.Encryption;
        DriveVcHashBox.Text = vc.Hash;
        DriveVcFilesystemBox.Text = vc.Filesystem;
        DriveVcPimBox.Text = vc.Pim;
        DriveVeraCryptPathBox.Text = DewDependencyService.FindVeraCryptPath(vc.VeraCryptPath) ?? vc.VeraCryptPath;
    }

    private void SaveVeraCryptSettingsLocal()
    {
        VeraCryptSettings existing = settings.Veracrypt;
        settings = settings with
        {
            Veracrypt = existing with
            {
                Encryption = string.IsNullOrWhiteSpace(DriveVcEncryptionBox.Text) ? "AES" : DriveVcEncryptionBox.Text.Trim(),
                Hash = string.IsNullOrWhiteSpace(DriveVcHashBox.Text) ? "SHA-512" : DriveVcHashBox.Text.Trim(),
                Filesystem = string.IsNullOrWhiteSpace(DriveVcFilesystemBox.Text) ? "exFAT" : DriveVcFilesystemBox.Text.Trim(),
                Pim = string.IsNullOrWhiteSpace(DriveVcPimBox.Text) ? "0" : DriveVcPimBox.Text.Trim(),
                VeraCryptPath = DriveVeraCryptPathBox.Text?.Trim() ?? string.Empty,
            },
        };
        settingsService.Save(settings);
        SetStatus("VeraCrypt settings saved.");
        Log("VeraCrypt settings saved.");
    }

    private string GetDrivePassword(DewDriveProfile profile)
    {
        string password = DrivePasswordBox.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(password))
        {
            return password;
        }

        return secretService.UnprotectForCurrentUser(profile.ProtectedPassword);
    }

    private async Task<bool> EnsureEncryptionReadyAsync(string mode)
    {
        if (!string.Equals(mode, "veracrypt", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        SaveVeraCryptSettingsLocal();
        string? path = DewDependencyService.FindVeraCryptPath(settings.Veracrypt.VeraCryptPath);
        if (!string.IsNullOrWhiteSpace(path))
        {
            DriveVeraCryptPathBox.Text = path;
            return true;
        }

        Log("VeraCrypt is missing; installing with winget.");
        await RunAndLogAsync(cliService.InstallVeraCryptAsync());
        path = DewDependencyService.FindVeraCryptPath(settings.Veracrypt.VeraCryptPath);
        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("VeraCrypt missing.");
            Log("VeraCrypt was not detected after install.");
            return false;
        }

        DriveVeraCryptPathBox.Text = path;
        SaveVeraCryptSettingsLocal();
        return true;
    }

    private void DriveWatcher_Changed(DewDriveProfile profile, FileSystemEventArgs e)
    {
        if (ShouldIgnoreDrivePath(e.FullPath))
        {
            return;
        }

        string key = profile.Name;
        if (driveSyncDebounces.Remove(key, out CancellationTokenSource? previousDebounce))
        {
            previousDebounce.Cancel();
            previousDebounce.Dispose();
        }

        CancellationTokenSource debounce = new();
        driveSyncDebounces[key] = debounce;
        CancellationToken token = debounce.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(async () => await RunDriveAutoSyncAsync(profile), DispatcherPriority.Background);
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private async Task RunDriveAutoSyncAsync(DewDriveProfile profile)
    {
        string key = profile.Name;
        if (!driveSyncInProgress.Add(key))
        {
            return;
        }

        try
        {
            Log($"Auto sync detected changes: {profile.Name}");
            await RunDewDriveProfileSyncAsync(profile, push: true);
        }
        finally
        {
            driveSyncInProgress.Remove(key);
        }
    }

    private async Task RunDewDriveProfileSyncAsync(DewDriveProfile profile, bool push)
    {
        string registryImage = DriveRegistry(profile);
        string password = GetDrivePassword(profile);
        if (string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Password required.");
            Log($"Enter the Dew Drive password once and save {profile.Name} to continue auto sync.");
            return;
        }

        DewCommandResult? result = await RunAndLogAsync(cliService.SyncDewDriveProfileAsync(profile.Name, registryImage, push, password));
        if (result?.ExitCode == 0)
        {
            MarkDriveProfileSynced(profile.Name);
        }
    }

    private void MarkDriveProfileSynced(string profileName)
    {
        List<DewDriveProfile> profiles = (settings.DewDrives?.Drives ?? []).ToList();
        int index = profiles.FindIndex(item => string.Equals(item.Name, profileName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        profiles[index] = profiles[index] with { LastSync = stamp };
        DewDriveSettings current = settings.DewDrives ?? new DewDriveSettings(Drives: []);
        settings = settings with { DewDrives = current with { Drives = profiles } };
        settingsService.Save(settings);
        RefreshDriveList();
        DriveStatusText.Text = $"{profileName}: last sync {stamp}";
    }

    private void StopDriveAutoSync(string? profileName = null)
    {
        IEnumerable<string> names = string.IsNullOrWhiteSpace(profileName)
            ? driveWatchers.Keys.ToList()
            : [profileName];
        foreach (string name in names)
        {
            if (driveSyncDebounces.Remove(name, out CancellationTokenSource? debounce))
            {
                debounce.Cancel();
                debounce.Dispose();
            }

            if (driveWatchers.Remove(name, out FileSystemWatcher? watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        }
    }

    private static bool ShouldIgnoreDrivePath(string path)
    {
        return path.Contains(DewPathService.ArchiveDirName, StringComparison.OrdinalIgnoreCase) ||
            path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static int FindDriveProfileIndex(IReadOnlyList<DewDriveProfile> profiles, string name, string folder)
    {
        return profiles.ToList().FindIndex(item =>
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(folder) && string.Equals(DriveFolder(item), folder, StringComparison.OrdinalIgnoreCase)));
    }

    private static string DriveFolder(DewDriveProfile profile)
    {
        return !string.IsNullOrWhiteSpace(profile.LocalPath) ? profile.LocalPath : profile.Folder;
    }

    private static string DriveRegistry(DewDriveProfile profile)
    {
        return !string.IsNullOrWhiteSpace(profile.RegistryRef) ? profile.RegistryRef : profile.RegistryImage;
    }

    private static string DriveLabel(DewDriveProfile profile)
    {
        string folder = DriveFolder(profile);
        string registry = DriveRegistry(profile);
        string label = string.IsNullOrWhiteSpace(folder) ? profile.Name : $"{profile.Name} - {folder}";
        return string.IsNullOrWhiteSpace(registry) ? label : $"{label} -> {registry}";
    }

    private void UpdateSelectionContext()
    {
        SelectionSummaryText.Text = selectedPaths.Count == 0
            ? "No files or folders selected."
            : $"{selectedPaths.Count} selected item(s).";
        DewPathItem? first = selectedPaths.FirstOrDefault();
        HistoryTargetBox.Text = first is null ? string.Empty : DewPathService.RepoForSource(first.Path);
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private static string QuoteForLog(string value)
    {
        return value.Contains(' ') ? $"\"{value}\"" : value;
    }

    private void Log(string message)
    {
        LogBox.Text += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        LogBox.CaretIndex = LogBox.Text.Length;
    }
}
