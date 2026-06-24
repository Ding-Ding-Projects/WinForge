using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinForge.Services;

/// <summary>傳輸協定 · The transfer protocol for a saved site.</summary>
public enum FtpProtocol
{
    Ftp,    // 純 FTP · plain FTP
    Ftps,   // FTP over explicit TLS · FTPS（明示 TLS）
    Sftp,   // SSH File Transfer Protocol · SFTP（SSH）
}

/// <summary>SFTP 驗證方式 · How an SFTP site authenticates.</summary>
public enum SftpAuth
{
    Password,   // 用密碼 · password
    KeyFile,    // 用私鑰檔 · private-key file
}

/// <summary>
/// 一個已儲存嘅站台 · One saved site (Site Manager row). Passwords / key passphrases are NEVER
/// stored in plaintext — they live encrypted in <see cref="EncryptedSecret"/> via DPAPI.
/// </summary>
public sealed class FtpSite
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public FtpProtocol Protocol { get; set; } = FtpProtocol.Sftp;
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string User { get; set; } = "";

    /// <summary>DPAPI 加密咗嘅密碼（base64）· DPAPI-encrypted password, base64. Never plaintext on disk.</summary>
    public string EncryptedSecret { get; set; } = "";

    public SftpAuth Auth { get; set; } = SftpAuth.Password;

    /// <summary>SFTP 私鑰檔路徑（揀咗用 key 時）· Path to the SFTP private-key file.</summary>
    public string KeyFilePath { get; set; } = "";

    /// <summary>登入後遠端起始目錄（可空）· Optional remote start directory.</summary>
    public string RemoteDir { get; set; } = "";

    /// <summary>本機起始目錄（可空）· Optional local start directory.</summary>
    public string LocalDir { get; set; } = "";

    /// <summary>已信任嘅 host-key / 憑證指紋（TOFU）· Trusted host-key / cert fingerprint (TOFU).</summary>
    public string TrustedFingerprint { get; set; } = "";

    public int DefaultPort => Protocol switch
    {
        FtpProtocol.Sftp => 22,
        _ => 21,
    };

    public FtpSite Clone() => new()
    {
        Id = Id, Name = Name, Protocol = Protocol, Host = Host, Port = Port, User = User,
        EncryptedSecret = EncryptedSecret, Auth = Auth, KeyFilePath = KeyFilePath,
        RemoteDir = RemoteDir, LocalDir = LocalDir, TrustedFingerprint = TrustedFingerprint,
    };
}

/// <summary>
/// 站台清單 + DPAPI 秘密儲存 · Saved-site list persisted to %LOCALAPPDATA%\WinForge\ftp-sites.json,
/// with every password / passphrase encrypted at rest with Windows DPAPI (CurrentUser scope).
/// This is the single DPAPI-backed credential store the spec asks the SSH/SFTP work to share —
/// other modules can reuse <see cref="Protect"/> / <see cref="Unprotect"/> for the same schema.
/// </summary>
public static class FtpSiteStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge");

    private static readonly string FilePath = Path.Combine(Dir, "ftp-sites.json");
    private static readonly object Gate = new();

    // 額外 entropy，令秘密只可以喺呢個 app 嘅 context 解密 · App-specific DPAPI entropy.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WinForge.FtpSiteStore.v1");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static List<FtpSite> _cache = Load();

    private static List<FtpSite> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<List<FtpSite>>(File.ReadAllText(FilePath), JsonOpts) ?? new();
        }
        catch { /* 損壞就重來 · ignore corrupt file */ }
        return new();
    }

    private static void SaveLocked()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_cache, JsonOpts));
        }
        catch { /* best effort */ }
    }

    /// <summary>所有站台（複本）· A snapshot copy of every saved site.</summary>
    public static List<FtpSite> All()
    {
        lock (Gate) return _cache.Select(s => s.Clone()).ToList();
    }

    /// <summary>新增或更新一個站台 · Add or update one site (matched by Id).</summary>
    public static void Save(FtpSite site)
    {
        lock (Gate)
        {
            var idx = _cache.FindIndex(s => s.Id == site.Id);
            if (idx >= 0) _cache[idx] = site.Clone();
            else _cache.Add(site.Clone());
            SaveLocked();
        }
    }

    /// <summary>刪除一個站台 · Delete one site by Id.</summary>
    public static void Delete(string id)
    {
        lock (Gate)
        {
            _cache.RemoveAll(s => s.Id == id);
            SaveLocked();
        }
    }

    /// <summary>記住信任嘅指紋（TOFU）· Persist a trusted host-key / cert fingerprint for a site.</summary>
    public static void TrustFingerprint(string id, string fingerprint)
    {
        lock (Gate)
        {
            var s = _cache.FirstOrDefault(x => x.Id == id);
            if (s is null) return;
            s.TrustedFingerprint = fingerprint;
            SaveLocked();
        }
    }

    /// <summary>用 DPAPI 加密一個秘密成 base64 · DPAPI-encrypt a secret to base64. Empty in → empty out.</summary>
    public static string Protect(string? secret)
    {
        if (string.IsNullOrEmpty(secret)) return "";
        try
        {
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }
        catch { return ""; }
    }

    /// <summary>用 DPAPI 解密一個 base64 秘密 · DPAPI-decrypt a base64 secret. Returns "" on failure.</summary>
    public static string Unprotect(string? encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return "";
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return ""; }
    }
}
