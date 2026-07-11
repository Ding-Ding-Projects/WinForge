namespace DewEncryption.Core;

public sealed record DewResult(string Source, string Repo, string Archive, string Commit);

public sealed record HistoryEntry(string Commit, string AuthorDate, string Subject);

public sealed record FileChange(string Status, string Path);

public sealed record VeraCryptSettings(
    string Encryption = "AES",
    string Hash = "SHA-512",
    string Filesystem = "exFAT",
    string Pim = "0",
    int SizePaddingMb = 64,
    double SizeMultiplier = 1.25,
    int MinimumSizeMb = 32,
    bool KeepSourceAfterEncrypt = false,
    bool KeepContainerAfterDecrypt = true,
    string VeraCryptPath = "");

public sealed record HookAction(
    string Name = "New action",
    string Event = "open",
    string Kind = "script",
    string Target = "",
    string Payload = "",
    bool Enabled = true);

public sealed record ContainerProfile(
    string Name = "Default",
    string Path = "",
    string MountPath = "",
    DewThemeSettings? Theme = null,
    IReadOnlyList<HookAction>? Hooks = null);

public sealed record DewDriveProfile(
    string Name = "Default",
    string LocalPath = "",
    string Folder = "",
    string RegistryRef = "",
    string RegistryImage = "",
    string EncryptionMode = "7zip",
    string LastSync = "",
    bool AutoPush = false,
    IReadOnlyList<string>? IncludePatterns = null,
    IReadOnlyList<string>? ExcludePatterns = null,
    string ProtectedPassword = "");

public sealed record DewDriveSettings(
    string DockerPath = "",
    string DefaultRegistry = "",
    IReadOnlyList<DewDriveProfile>? Drives = null);

public sealed record AppSettings(VeraCryptSettings Veracrypt, IReadOnlyList<ContainerProfile>? Containers, DewDriveSettings? DewDrives = null)
{
    public static AppSettings Default => new(new VeraCryptSettings(), [], new DewDriveSettings(Drives: []));
}
