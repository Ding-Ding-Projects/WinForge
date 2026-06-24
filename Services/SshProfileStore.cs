using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>SSH 認證方式 · The authentication method for an SSH profile.</summary>
public enum SshAuthKind
{
    Password,   // 密碼 · password authentication
    PrivateKey, // 私鑰檔 · private-key file authentication
}

/// <summary>
/// 一個已儲存嘅 SSH 連線設定檔 · One saved SSH connection profile.
/// 純資料、可變、可無參數建構（俾 System.Text.Json 用）。秘密（密碼／私鑰密碼短語）
/// 只會以 DPAPI 加密後嘅 base64 形式持久化，明文絕不寫入磁碟。
/// Plain mutable data, parameterless-constructible. Secrets (password / key passphrase) are
/// only ever persisted as a DPAPI-encrypted base64 blob — plaintext never touches disk.
/// </summary>
public sealed class SshProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string User { get; set; } = string.Empty;
    public SshAuthKind Auth { get; set; } = SshAuthKind.Password;

    /// <summary>私鑰檔路徑（Auth=PrivateKey 時用）· Private-key file path (when Auth=PrivateKey).</summary>
    public string KeyPath { get; set; } = string.Empty;

    /// <summary>
    /// DPAPI 加密咗嘅秘密（密碼，或私鑰嘅密碼短語）嘅 base64 · DPAPI-encrypted base64 of the
    /// secret (the password, or the private-key passphrase). Empty means "no secret stored".
    /// </summary>
    public string SecretBlob { get; set; } = string.Empty;

    /// <summary>顯示用標籤 · A display label like "user@host:port".</summary>
    public string Display =>
        $"{(string.IsNullOrWhiteSpace(User) ? "?" : User)}@{(string.IsNullOrWhiteSpace(Host) ? "?" : Host)}:{Port}";
}

/// <summary>
/// SSH 設定檔儲存 · Persistent SSH profile list with DPAPI-protected secrets at rest.
/// 設定檔（不含明文秘密）以 JSON 寫入 %LOCALAPPDATA%\WinForge\ssh-profiles.json；每個秘密
/// 單獨以 <see cref="ProtectedData"/>（CurrentUser scope）加密，所以即使有人攞到個 JSON 檔
/// 都解唔到密碼。Mirrors the DPAPI usage referenced by the Vault catalog.
/// Profiles (without plaintext secrets) are persisted as JSON; each secret is individually
/// DPAPI-encrypted (CurrentUser scope) so the JSON file alone never reveals a password.
/// </summary>
public static class SshProfileStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge");

    private static readonly string FilePath = Path.Combine(Dir, "ssh-profiles.json");

    // Extra entropy tying these blobs to this app (defence-in-depth, not a substitute for the user key).
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WinForge.SSH.v1");

    private static readonly object Gate = new();
    private static readonly List<SshProfile> _all = new();

    /// <summary>清單一有改動就觸發 · Raised after any mutation to the list.</summary>
    public static event EventHandler? Changed;

    /// <summary>記憶體中嘅設定檔清單（只讀複本）· The in-memory profile list (read-only copy).</summary>
    public static IReadOnlyList<SshProfile> All
    {
        get { lock (Gate) return _all.Select(Clone).ToList(); }
    }

    static SshProfileStore() => Load();

    private static SshProfile Clone(SshProfile p) => new()
    {
        Id = p.Id, Name = p.Name, Host = p.Host, Port = p.Port, User = p.User,
        Auth = p.Auth, KeyPath = p.KeyPath, SecretBlob = p.SecretBlob,
    };

    // ---- persistence -------------------------------------------------------

    private static void Load()
    {
        lock (Gate)
        {
            _all.Clear();
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var loaded = JsonSerializer.Deserialize<List<SshProfile>>(json);
                    if (loaded is not null)
                        foreach (var p in loaded)
                            if (p is not null && !string.IsNullOrWhiteSpace(p.Id))
                                _all.Add(p);
                }
            }
            catch { /* 損壞就當空 · ignore corrupt file */ }
        }
    }

    private static void SaveLocked()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_all,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best effort */ }
    }

    // ---- mutations ---------------------------------------------------------

    /// <summary>
    /// 新增或更新一個設定檔 · Insert or update a profile (matched by Id). Pass a non-null
    /// <paramref name="secret"/> to (re)encrypt it; pass null to keep the existing SecretBlob.
    /// 傳 secret="" 會清除秘密。Passing an empty secret clears the stored secret.
    /// </summary>
    public static void Save(SshProfile profile, string? secret)
    {
        if (profile is null) return;
        lock (Gate)
        {
            if (secret is not null)
                profile.SecretBlob = string.IsNullOrEmpty(secret) ? string.Empty : Protect(secret);

            var idx = _all.FindIndex(p => p.Id == profile.Id);
            var stored = Clone(profile);
            if (idx >= 0) _all[idx] = stored; else _all.Add(stored);
            SaveLocked();
        }
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>移除一個設定檔 · Remove a profile by Id.</summary>
    public static void Remove(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        bool removed;
        lock (Gate)
        {
            removed = _all.RemoveAll(p => p.Id == id) > 0;
            if (removed) SaveLocked();
        }
        if (removed) Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>用 Id 攞返個設定檔（複本）· Get a profile by Id (a copy), or null.</summary>
    public static SshProfile? Get(string id)
    {
        lock (Gate)
        {
            var p = _all.FirstOrDefault(x => x.Id == id);
            return p is null ? null : Clone(p);
        }
    }

    /// <summary>
    /// 解密一個設定檔嘅秘密 · Decrypt the stored secret for a profile. Returns "" if none/failed.
    /// 只喺真正需要連線嗰刻先解密；明文唔會留喺記憶體耐過必要。
    /// </summary>
    public static string Reveal(SshProfile profile)
    {
        if (profile is null || string.IsNullOrEmpty(profile.SecretBlob)) return string.Empty;
        return Unprotect(profile.SecretBlob);
    }

    // ---- DPAPI -------------------------------------------------------------

    /// <summary>用 DPAPI（CurrentUser）加密一段秘密做 base64 · DPAPI-encrypt a secret to base64.</summary>
    public static string Protect(string plain)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(plain);
            var enc = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc);
        }
        catch { return string.Empty; }
    }

    /// <summary>用 DPAPI 解密 base64 秘密 · DPAPI-decrypt a base64 secret back to plaintext.</summary>
    public static string Unprotect(string blob)
    {
        try
        {
            var enc = Convert.FromBase64String(blob);
            var dec = ProtectedData.Unprotect(enc, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(dec);
        }
        catch { return string.Empty; }
    }
}
