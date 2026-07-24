using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// Keeps only a short-lived, in-memory proof that the current HA endpoint/token passed check_config.
/// The token itself is never retained or logged.
/// </summary>
public sealed class HomeAssistantRestartGate
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(2);

    private readonly TimeSpan _lifetime;
    private string? _configurationFingerprint;
    private DateTimeOffset _validatedAt;

    public HomeAssistantRestartGate(TimeSpan? lifetime = null)
        => _lifetime = lifetime ?? DefaultLifetime;

    public bool RecordCheck(string endpoint, string token, bool requestSucceeded, string? responseBody, DateTimeOffset now)
    {
        Clear();
        if (!requestSucceeded || !IsValidResponse(responseBody)) return false;
        _configurationFingerprint = Fingerprint(endpoint, token);
        _validatedAt = now;
        return true;
    }

    public bool CanRestart(string endpoint, string token, DateTimeOffset now)
    {
        if (_configurationFingerprint is null) return false;
        if (now < _validatedAt || now - _validatedAt > _lifetime)
        {
            Clear();
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(_configurationFingerprint),
            Convert.FromHexString(Fingerprint(endpoint, token)));
    }

    public void Consume() => Clear();

    public static bool IsValidResponse(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return false;
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("result", out var result) &&
                   result.ValueKind == JsonValueKind.String &&
                   string.Equals(result.GetString(), "valid", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private void Clear()
    {
        _configurationFingerprint = null;
        _validatedAt = default;
    }

    private static string Fingerprint(string endpoint, string token)
    {
        var normalizedEndpoint = (endpoint ?? string.Empty).Trim().TrimEnd('/').ToUpperInvariant();
        var bytes = Encoding.UTF8.GetBytes(normalizedEndpoint + "\n" + (token ?? string.Empty));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
