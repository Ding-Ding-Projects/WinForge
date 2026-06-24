using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// WinForge 保險庫服務 · WinForge Vault service — a thin, de-branded front-end over the bundled
/// on-the-fly disk-encryption CLI (a renamed VeraCrypt/TrueCrypt-derived build). It builds the
/// command-line argument strings for create / mount / dismount / change-password / benchmark,
/// routes them through <see cref="ShellRunner"/> (elevated where the kernel driver requires it),
/// and re-lists logical drives to confirm state because output is unavailable under UAC.
///
/// 去品牌：呢個服務唔會喺任何使用者可見字串度出現 "VeraCrypt"／"TrueCrypt"，只係用 "WinForge 保險庫"。
/// De-branding: no "VeraCrypt"/"TrueCrypt" trademark is surfaced to the user — only "WinForge Vault".
/// </summary>
public static class VaultVolumeService
{
    /// <summary>容器副檔名 · The default container file extension.</summary>
    public const string ContainerExtension = ".wfv";

    /// <summary>加密演算法（去品牌字串保持中性）· Encryption algorithm choices.</summary>
    public static readonly (string Cli, string En, string Zh)[] Algorithms =
    {
        ("AES", "AES", "AES"),
        ("Serpent", "Serpent", "Serpent"),
        ("Twofish", "Twofish", "Twofish"),
        ("Camellia", "Camellia", "Camellia"),
        ("Kuznyechik", "Kuznyechik", "Kuznyechik"),
        ("AES(Twofish)", "AES(Twofish)", "AES(Twofish)"),
        ("AES(Twofish(Serpent))", "AES(Twofish(Serpent))", "AES(Twofish(Serpent))"),
        ("Serpent(AES)", "Serpent(AES)", "Serpent(AES)"),
        ("Twofish(Serpent)", "Twofish(Serpent)", "Twofish(Serpent)"),
    };

    /// <summary>雜湊／金鑰衍生函數 · Hash / key-derivation function (PRF) choices.</summary>
    public static readonly (string Cli, string En, string Zh)[] Hashes =
    {
        ("sha512", "SHA-512", "SHA-512"),
        ("sha256", "SHA-256", "SHA-256"),
        ("whirlpool", "Whirlpool", "Whirlpool"),
        ("blake2s", "BLAKE2s-256", "BLAKE2s-256"),
        ("streebog", "Streebog", "Streebog"),
    };

    /// <summary>檔案系統 · File-system choices for a new container.</summary>
    public static readonly (string Cli, string En, string Zh)[] FileSystems =
    {
        ("FAT", "FAT / FAT32", "FAT／FAT32"),
        ("NTFS", "NTFS", "NTFS"),
        ("exFAT", "exFAT", "exFAT"),
        ("None", "None (format later)", "無（之後自行格式化）"),
    };

    /// <summary>一個已掛載嘅磁碟區（由邏輯磁碟機推斷）· A mounted vault volume row.</summary>
    public sealed class MountedVolume
    {
        public string Letter { get; init; } = "";     // e.g. "X:"
        public string Label { get; init; } = "";
        public string FileSystem { get; init; } = "";
        public long SizeBytes { get; init; }
        public long FreeBytes { get; init; }
        public string SizeText => Human(SizeBytes);
        public string FreeText => Human(FreeBytes);
    }

    // ===================== binary detection =====================

    /// <summary>
    /// 搵已綁定嘅去品牌主程式 · Locate the bundled de-branded GUI/mount executable.
    /// 先喺 app 目錄搵 WinForgeVault.exe，搵唔到先退而求其次用上游安裝（路徑唔會喺 UI 顯示）。
    /// Prefers the bundled WinForgeVault.exe next to the app; falls back to an upstream install.
    /// </summary>
    public static string? FindMountBinary()
    {
        foreach (var p in MountCandidates())
            if (File.Exists(p)) return p;
        return null;
    }

    /// <summary>
    /// 搵已綁定嘅去品牌建立磁碟區程式 · Locate the bundled de-branded Format/create executable.
    /// </summary>
    public static string? FindFormatBinary()
    {
        foreach (var p in FormatCandidates())
            if (File.Exists(p)) return p;
        return null;
    }

    private static IEnumerable<string> MountCandidates()
    {
        var dir = AppContext.BaseDirectory;
        yield return Path.Combine(dir, "Vault", "WinForgeVault.exe");
        yield return Path.Combine(dir, "WinForgeVault.exe");
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        // Fallback to an upstream install if the bundled binary is missing.
        yield return Path.Combine(pf, "VeraCrypt", "VeraCrypt.exe");
    }

    private static IEnumerable<string> FormatCandidates()
    {
        var dir = AppContext.BaseDirectory;
        yield return Path.Combine(dir, "Vault", "WinForgeVault Format.exe");
        yield return Path.Combine(dir, "WinForgeVault Format.exe");
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Path.Combine(pf, "VeraCrypt", "VeraCrypt Format.exe");
    }

    /// <summary>係咪用緊綁定嘅去品牌程式（相對上游退路）· True if the bundled de-branded binary is present.</summary>
    public static bool IsBundledPresent()
    {
        var dir = AppContext.BaseDirectory;
        return File.Exists(Path.Combine(dir, "Vault", "WinForgeVault.exe"))
            || File.Exists(Path.Combine(dir, "WinForgeVault.exe"));
    }

    // ===================== mount / dismount =====================

    /// <summary>
    /// 掛載一個容器到指定磁碟機 · Mount a container file to a drive letter.
    /// 經 /q /silent 無人值守，視乎需要用 keyfile / PIM / 唯讀 / 可移除媒體選項。
    /// Unattended via /q /silent, with optional keyfile, PIM, read-only and removable-media options.
    /// </summary>
    public static async Task<TweakResult> MountAsync(
        string volumePath, char driveLetter, string password,
        int pim = 0, string? keyfile = null, bool readOnly = false, bool removable = false,
        bool explore = false, CancellationToken ct = default)
    {
        var exe = FindMountBinary();
        if (exe is null) return MissingBinary();
        if (string.IsNullOrWhiteSpace(volumePath) || !File.Exists(volumePath))
            return TweakResult.Fail("Container file not found.", "搵唔到容器檔案。");

        var sb = new StringBuilder();
        sb.Append("/v ").Append(Quote(volumePath));
        sb.Append(" /l ").Append(char.ToUpperInvariant(driveLetter));
        sb.Append(" /p ").Append(Quote(password));
        if (pim > 0) sb.Append(" /pim ").Append(pim);
        if (!string.IsNullOrWhiteSpace(keyfile)) sb.Append(" /k ").Append(Quote(keyfile!));
        if (readOnly) sb.Append(" /m ro");
        if (removable) sb.Append(" /m rm");
        if (explore) sb.Append(" /e");
        sb.Append(" /q /silent");

        // The kernel driver requires elevation; output is unavailable under UAC so we confirm by re-listing.
        var r = await ShellRunner.Run(exe, sb.ToString(), elevated: true, ct);
        var mounted = ListMounted().Any(m => m.Letter.StartsWith(char.ToUpperInvariant(driveLetter)));
        if (mounted)
            return TweakResult.Ok($"Mounted to {char.ToUpperInvariant(driveLetter)}:", $"已掛載到 {char.ToUpperInvariant(driveLetter)}:");
        return r.Success
            ? TweakResult.Ok($"Mount requested for {char.ToUpperInvariant(driveLetter)}:", $"已要求掛載到 {char.ToUpperInvariant(driveLetter)}:")
            : TweakResult.Fail("Mount failed — check the password, keyfile and PIM.", "掛載失敗 — 檢查密碼、鎖匙檔同 PIM。");
    }

    /// <summary>卸載指定磁碟機 · Dismount one drive letter (optionally force).</summary>
    public static async Task<TweakResult> DismountAsync(char driveLetter, bool force = false, CancellationToken ct = default)
    {
        var exe = FindMountBinary();
        if (exe is null) return MissingBinary();
        var args = $"/q /d {char.ToUpperInvariant(driveLetter)}" + (force ? " /f" : "");
        await ShellRunner.Run(exe, args, elevated: true, ct);
        var stillThere = ListMounted().Any(m => m.Letter.StartsWith(char.ToUpperInvariant(driveLetter)));
        return stillThere
            ? TweakResult.Fail($"{char.ToUpperInvariant(driveLetter)}: is still mounted — files may be open (try Force).", $"{char.ToUpperInvariant(driveLetter)}: 仍然掛載住 — 可能有檔案開住（試下強制）。")
            : TweakResult.Ok($"Dismounted {char.ToUpperInvariant(driveLetter)}:", $"已卸載 {char.ToUpperInvariant(driveLetter)}:");
    }

    /// <summary>卸載全部磁碟區 · Dismount all mounted volumes (optionally force).</summary>
    public static async Task<TweakResult> DismountAllAsync(bool force = false, CancellationToken ct = default)
    {
        var exe = FindMountBinary();
        if (exe is null) return MissingBinary();
        var args = "/q /d" + (force ? " /f" : "");
        await ShellRunner.Run(exe, args, elevated: true, ct);
        return TweakResult.Ok("Dismount-all requested.", "已要求卸載全部。");
    }

    /// <summary>清除記憶體中嘅密碼快取 · Wipe the in-memory password cache.</summary>
    public static async Task<TweakResult> WipeCacheAsync(CancellationToken ct = default)
    {
        var exe = FindMountBinary();
        if (exe is null) return MissingBinary();
        await ShellRunner.Run(exe, "/q /w", elevated: true, ct);
        return TweakResult.Ok("Password cache wiped.", "已清除密碼快取。");
    }

    // ===================== create container =====================

    /// <summary>
    /// 建立一個新嘅加密檔案容器 · Create a new encrypted file container via the Format binary.
    /// </summary>
    public static async Task<TweakResult> CreateContainerAsync(
        string path, long sizeBytes, string password,
        string algorithm = "AES", string hash = "sha512", string fileSystem = "FAT",
        int pim = 0, string? keyfile = null, bool dynamic = false, bool quickFormat = false,
        CancellationToken ct = default)
    {
        var exe = FindFormatBinary();
        if (exe is null) return MissingBinary();
        if (string.IsNullOrWhiteSpace(password)) return TweakResult.Fail("A password is required.", "必須輸入密碼。");
        if (sizeBytes < 292 * 1024) return TweakResult.Fail("Container is too small.", "容器太細。");

        var sb = new StringBuilder();
        sb.Append("/create ").Append(Quote(path));
        sb.Append(" /size ").Append(sizeBytes);
        sb.Append(" /password ").Append(Quote(password));
        sb.Append(" /encryption ").Append(Quote(algorithm));
        sb.Append(" /hash ").Append(hash);
        sb.Append(" /filesystem ").Append(fileSystem);
        if (pim > 0) sb.Append(" /pim ").Append(pim);
        if (!string.IsNullOrWhiteSpace(keyfile)) sb.Append(" /keyfile ").Append(Quote(keyfile!));
        if (dynamic) sb.Append(" /dynamic 1");
        if (quickFormat) sb.Append(" /quick");
        sb.Append(" /silent /force /noisocheck");

        // Format runs unelevated for a file container in the user's own folder.
        var r = await ShellRunner.Run(exe, sb.ToString(), elevated: false, ct);
        if (File.Exists(path))
            return TweakResult.Ok($"Container created: {Path.GetFileName(path)} ({Human(sizeBytes)})",
                $"已建立容器：{Path.GetFileName(path)}（{Human(sizeBytes)}）");
        return r.Success
            ? TweakResult.Ok("Create requested.", "已要求建立。", r.Output)
            : TweakResult.Fail("Create failed.", "建立失敗。", r.Output);
    }

    // ===================== change password =====================

    /// <summary>
    /// 更改容器密碼 · Change a container's password. The CLI has no silent change-password switch,
    /// so we open the binary on the volume for an interactive password change (the only safe path —
    /// shipping builds must never pass new plaintext passwords on the command line).
    /// </summary>
    public static async Task<TweakResult> ChangePasswordAsync(string volumePath, CancellationToken ct = default)
    {
        var exe = FindMountBinary();
        if (exe is null) return MissingBinary();
        // Open with the volume preselected; the user completes Volume Tools ▸ Change Password.
        await ShellRunner.Run(exe, "/v " + Quote(volumePath), elevated: false, ct);
        return TweakResult.Ok(
            "Opened — use Volume Tools ▸ Change Volume Password to set a new password.",
            "已打開 — 用「磁碟區工具 ▸ 更改磁碟區密碼」設定新密碼。");
    }

    // ===================== benchmark / GUI =====================

    /// <summary>開啟主程式（效能測試／設定喺裡面）· Launch the main GUI (benchmark/settings live there).</summary>
    public static async Task<TweakResult> LaunchGuiAsync(CancellationToken ct = default)
    {
        var exe = FindMountBinary();
        if (exe is null) return MissingBinary();
        await ShellRunner.Run(exe, "", elevated: false, ct);
        return TweakResult.Ok("Opened WinForge Vault.", "已打開 WinForge 保險庫。");
    }

    /// <summary>瀏覽已掛載磁碟區 · Open a mounted volume in File Explorer.</summary>
    public static Task<TweakResult> ExploreAsync(char driveLetter, CancellationToken ct = default)
        => ShellRunner.Run("explorer.exe", $"{char.ToUpperInvariant(driveLetter)}:\\", elevated: false, ct);

    // ===================== mounted-volume listing =====================

    /// <summary>
    /// 列出可能係保險庫掛載出嚟嘅磁碟機 · List logical drives that look like vault mounts.
    /// 因為驅動程式經 UAC 卸載／掛載冇輸出，所以靠重新列出邏輯磁碟機嚟確認狀態 ·
    /// Confirms state by re-enumerating logical drives (no captured output under UAC).
    /// 用啟發式排除明顯嘅實體／系統磁碟機，淨低嗰啲畀使用者卸載／瀏覽。
    /// Excludes obvious system/physical drives heuristically; the rest are user-dismountable.
    /// </summary>
    public static List<MountedVolume> ListMounted()
    {
        var list = new List<MountedVolume>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Removable) continue;
                if (!d.IsReady) continue;
                var letter = d.Name.TrimEnd('\\'); // "X:"
                // Skip the system drive — a vault is never mounted there.
                var sysRoot = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\');
                if (string.Equals(letter, sysRoot, StringComparison.OrdinalIgnoreCase)) continue;
                list.Add(new MountedVolume
                {
                    Letter = letter,
                    Label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? "" : d.VolumeLabel,
                    FileSystem = d.DriveFormat,
                    SizeBytes = d.TotalSize,
                    FreeBytes = d.TotalFreeSpace,
                });
            }
            catch { /* drive vanished mid-enumeration */ }
        }
        return list;
    }

    /// <summary>未掛載嘅可用磁碟機字母（A–Z）· Free drive letters available to mount to.</summary>
    public static List<char> FreeDriveLetters()
    {
        var used = DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])).ToHashSet();
        var free = new List<char>();
        for (char c = 'Z'; c >= 'D'; c--)
            if (!used.Contains(c)) free.Add(c);
        return free;
    }

    // ===================== helpers =====================

    private static TweakResult MissingBinary() => TweakResult.Fail(
        "WinForge Vault engine not found — install it from the panel above.",
        "搵唔到 WinForge 保險庫引擎 — 喺上面嘅面板安裝。");

    private static string Quote(string s) => "\"" + s.Replace("\"", "") + "\"";

    public static string Human(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB", "PB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.##} {u[i]}";
    }
}
