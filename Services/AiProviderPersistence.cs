using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// DPAPI boundary for AI provider secrets. Kept behind a small interface so
/// persistence failure behavior is deterministic and regression-testable.
/// </summary>
internal interface IAiProviderSecretProtector
{
    bool TryProtect(string plain, out string protectedSecret);
    bool TryUnprotect(string protectedSecret, out string plain);
}

/// <summary>Current-user DPAPI implementation for AI provider API keys.</summary>
internal sealed class DpapiAiProviderSecretProtector : IAiProviderSecretProtector
{
    private const string Prefix = "dpapi:";

    public bool TryProtect(string plain, out string protectedSecret)
    {
        protectedSecret = "";
        if (string.IsNullOrEmpty(plain)) return true;
        try
        {
            var enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            protectedSecret = Prefix + Convert.ToBase64String(enc);
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
        if (!protectedSecret.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // Backward compatibility for historical plaintext provider files.
            plain = protectedSecret;
            return true;
        }

        try
        {
            var raw = Convert.FromBase64String(protectedSecret.Substring(Prefix.Length));
            plain = Encoding.UTF8.GetString(ProtectedData.Unprotect(raw, null, DataProtectionScope.CurrentUser));
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Persists AI providers without ever replacing an unreadable or unprotectable
/// API key with an empty value. A failed protect aborts the complete write; a
/// failed unprotect retains the opaque DPAPI blob until an explicit replacement
/// key is successfully protected.
/// </summary>
internal sealed class AiProviderPersistence
{
    private readonly string _filePath;
    private readonly IAiProviderSecretProtector _secrets;
    private readonly Dictionary<string, string> _unreadableSecretBlobs = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public AiProviderPersistence(string filePath, IAiProviderSecretProtector secrets)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
    }

    public List<AiProvider> Load()
    {
        _unreadableSecretBlobs.Clear();
        try
        {
            if (!File.Exists(_filePath)) return new();
            var providers = JsonSerializer.Deserialize<List<AiProvider>>(File.ReadAllText(_filePath)) ?? new();
            foreach (var provider in providers)
            {
                var storedSecret = provider.ApiKey ?? "";
                if (_secrets.TryUnprotect(storedSecret, out var plain))
                {
                    provider.ApiKey = plain;
                    continue;
                }

                // Do not expose a stale or fabricated key in memory, but retain the exact
                // ciphertext so editing unrelated fields cannot erase it on the next save.
                provider.ApiKey = "";
                _unreadableSecretBlobs[provider.Id] = storedSecret;
            }
            return providers;
        }
        catch
        {
            return new();
        }
    }

    public bool HasUnreadableSecret(string providerId) =>
        !string.IsNullOrEmpty(providerId) && _unreadableSecretBlobs.ContainsKey(providerId);

    public bool TrySave(IEnumerable<AiProvider> providers)
    {
        if (providers is null) return false;

        var nextUnreadable = new Dictionary<string, string>(StringComparer.Ordinal);
        var copy = new List<AiProvider>();
        foreach (var provider in providers)
        {
            if (provider is null) continue;
            var storedSecret = "";
            if (string.IsNullOrEmpty(provider.ApiKey))
            {
                // An empty edit after a failed unprotect is ambiguous: preserve the old
                // ciphertext. Entering a non-empty replacement below is the explicit
                // recovery path that can replace it.
                if (_unreadableSecretBlobs.TryGetValue(provider.Id, out var opaqueSecret))
                {
                    storedSecret = opaqueSecret;
                    nextUnreadable[provider.Id] = opaqueSecret;
                }
            }
            else if (!_secrets.TryProtect(provider.ApiKey, out storedSecret) || string.IsNullOrEmpty(storedSecret))
            {
                // Never write a partial copy with an empty key if DPAPI is unavailable
                // or a protector returns an unusable result.
                return false;
            }

            copy.Add(new AiProvider
            {
                Id = provider.Id,
                Name = provider.Name,
                Kind = provider.Kind,
                BaseUrl = provider.BaseUrl,
                DefaultModel = provider.DefaultModel,
                ApiKey = storedSecret,
            });
        }

        var tempPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(tempPath, JsonSerializer.Serialize(copy, JsonOpts));
            // The temporary file is in the same directory. Replace an existing provider
            // file atomically so an I/O failure cannot leave a half-written key store.
            if (File.Exists(_filePath)) File.Replace(tempPath, _filePath, destinationBackupFileName: null);
            else File.Move(tempPath, _filePath);
            _unreadableSecretBlobs.Clear();
            foreach (var (providerId, opaqueSecret) in nextUnreadable)
                _unreadableSecretBlobs[providerId] = opaqueSecret;
            return true;
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            return false;
        }
    }
}
