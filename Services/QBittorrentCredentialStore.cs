using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace WinForge.Services;

/// <summary>
/// Small settings abstraction for the qBittorrent credential boundary. Keeping it separate from
/// <see cref="SettingsStore"/> makes migration and DPAPI-failure behaviour testable without a user profile.
/// </summary>
internal interface IQBittorrentSettingsStore
{
    string Get(string key, string fallback);
    void Set(string key, string value);
}

/// <summary>DPAPI boundary for a remembered qBittorrent WebUI password.</summary>
internal interface IQBittorrentSecretProtector
{
    bool TryProtect(string plain, out string protectedSecret);
    bool TryUnprotect(string protectedSecret, out string plain);
}

/// <summary>Current-user DPAPI implementation for the qBittorrent WebUI password.</summary>
internal sealed class DpapiQBittorrentSecretProtector : IQBittorrentSecretProtector
{
    private const string Prefix = "dpapi:v1:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WinForge.QBittorrentCredentialStore.v1");

    public bool TryProtect(string plain, out string protectedSecret)
    {
        protectedSecret = "";
        if (string.IsNullOrEmpty(plain)) return true;
        try
        {
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
            protectedSecret = Prefix + Convert.ToBase64String(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool TryUnprotect(string protectedSecret, out string plain)
    {
        plain = "";
        if (string.IsNullOrEmpty(protectedSecret)) return true;
        if (!protectedSecret.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        try
        {
            var raw = Convert.FromBase64String(protectedSecret.Substring(Prefix.Length));
            plain = Encoding.UTF8.GetString(ProtectedData.Unprotect(raw, Entropy, DataProtectionScope.CurrentUser));
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Stores a qBittorrent WebUI password only as a DPAPI blob. It migrates the historical plaintext
/// <c>qb.pass</c> value by first writing a verified encrypted value and only then erasing plaintext.
/// If protection cannot be performed, the legacy value is neither exposed nor overwritten, so a later
/// healthy DPAPI session can migrate it without silently losing the user's saved credential.
/// </summary>
internal sealed class QBittorrentCredentialStore
{
    internal const string LegacyPasswordKey = "qb.pass";
    internal const string PasswordBlobKey = "qb.pass.dpapi";
    internal const string RememberKey = "qb.remember";

    private readonly IQBittorrentSettingsStore _settings;
    private readonly IQBittorrentSecretProtector _secrets;

    public QBittorrentCredentialStore(IQBittorrentSettingsStore settings, IQBittorrentSecretProtector secrets)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
    }

    public bool Remember => _settings.Get(RememberKey, "0") == "1";

    /// <summary>Runs the one-time plaintext migration. Returns false when no readable encrypted credential can be safely established.</summary>
    public bool MigrateLegacyPassword()
    {
        var legacy = _settings.Get(LegacyPasswordKey, "");
        if (string.IsNullOrEmpty(legacy)) return true;

        // A password cannot be remembered when the user explicitly turned remembering off.
        if (!Remember)
        {
            _settings.Set(LegacyPasswordKey, "");
            return true;
        }

        // A readable encrypted migration takes precedence. Do not discard a recoverable legacy
        // value when a partial/corrupt blob cannot be opened in this Windows user context.
        var existingBlob = _settings.Get(PasswordBlobKey, "");
        if (!string.IsNullOrEmpty(existingBlob))
        {
            if (!_secrets.TryUnprotect(existingBlob, out var existingPassword) || string.IsNullOrEmpty(existingPassword)) return false;
            _settings.Set(LegacyPasswordKey, "");
            return true;
        }

        if (!_secrets.TryProtect(legacy, out var blob) || string.IsNullOrEmpty(blob))
            return false;

        // Write the recoverable form first. Do not erase the legacy value unless this session can read back
        // the exact opaque blob it wrote; this preserves recovery on a failed settings session.
        _settings.Set(PasswordBlobKey, blob);
        if (!string.Equals(_settings.Get(PasswordBlobKey, ""), blob, StringComparison.Ordinal))
            return false;

        _settings.Set(LegacyPasswordKey, "");
        return true;
    }

    /// <summary>Returns an in-memory plaintext only after DPAPI succeeds; unreadable data fails closed.</summary>
    public string ReadSavedPassword()
    {
        if (!Remember || !MigrateLegacyPassword()) return "";
        var blob = _settings.Get(PasswordBlobKey, "");
        return _secrets.TryUnprotect(blob, out var plain) ? plain : "";
    }

    /// <summary>
    /// Replaces the remembered secret without ever writing plaintext. A failed protect preserves any
    /// existing DPAPI blob and returns false so the caller can tell the user that only this session is used.
    /// </summary>
    public bool SaveRememberedPassword(string? password, bool remember)
    {
        if (!remember)
        {
            _settings.Set(RememberKey, "0");
            _settings.Set(PasswordBlobKey, "");
            _settings.Set(LegacyPasswordKey, "");
            return true;
        }

        password ??= "";
        if (password.Length == 0)
        {
            _settings.Set(PasswordBlobKey, "");
            _settings.Set(LegacyPasswordKey, "");
            _settings.Set(RememberKey, "1");
            return true;
        }

        if (!_secrets.TryProtect(password, out var blob) || string.IsNullOrEmpty(blob))
            return false;

        _settings.Set(PasswordBlobKey, blob);
        if (!string.Equals(_settings.Get(PasswordBlobKey, ""), blob, StringComparison.Ordinal))
            return false;

        _settings.Set(LegacyPasswordKey, "");
        _settings.Set(RememberKey, "1");
        return true;
    }
}

/// <summary>
/// Generation-scoped cancellation for qBittorrent page requests. Starting a new scope invalidates every
/// continuation from the old one, preventing a late refresh from repainting a disconnected or reloaded page.
/// </summary>
internal sealed class QBittorrentRequestGeneration : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _source;
    private int _generation;

    public int CurrentGeneration
    {
        get { lock (_sync) return _generation; }
    }

    public int Restart()
    {
        CancellationTokenSource? previous;
        int generation;
        lock (_sync)
        {
            previous = _source;
            _source = new CancellationTokenSource();
            generation = ++_generation;
        }
        CancelAndDispose(previous);
        return generation;
    }

    public void Invalidate()
    {
        CancellationTokenSource? previous;
        lock (_sync)
        {
            previous = _source;
            _source = null;
            _generation++;
        }
        CancelAndDispose(previous);
    }

    public bool IsCurrent(int generation)
    {
        lock (_sync)
            return generation == _generation && _source is not null && !_source.IsCancellationRequested;
    }

    public bool TryGetToken(int generation, out CancellationToken token)
    {
        lock (_sync)
        {
            if (generation != _generation || _source is null || _source.IsCancellationRequested)
            {
                token = default;
                return false;
            }

            token = _source.Token;
            return true;
        }
    }

    public void Dispose() => Invalidate();

    private static void CancelAndDispose(CancellationTokenSource? source)
    {
        if (source is null) return;
        try { source.Cancel(); } catch { }
        source.Dispose();
    }
}
