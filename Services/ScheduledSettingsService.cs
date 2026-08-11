using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Credentials;

namespace WinForge.Services;

/// <summary>
/// Versioned, bounded scheduled-settings storage and evaluator.
///
/// A schedule is an ephemeral override: it never writes its resolved value back into a
/// user's base setting. Local rules are deterministic and synchronous; HTTPS API and
/// Home Assistant rules refresh into an in-memory last-valid cache and fail safe to the
/// base setting when a source is unavailable.
/// </summary>
public static class ScheduledSettingsService
{
    public const int SchemaVersion = 1;
    public const int MaxRules = 128;
    public const int MaxValueLength = 512;
    public const int MaxResponseBytes = 256 * 1024;
    public const int RefreshTimeoutSeconds = 5;

    private const string StorageKey = "universal.scheduledSettings.v1";
    private const string HomeAssistantCredentialResource = "WinForge.ScheduledSettings.HomeAssistant";
    private const string HomeAssistantCredentialUserPrefix = "token:";
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Dictionary<string, string>> ExternalValues = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> ExternalErrors = new(StringComparer.Ordinal);
    private static long ExternalRefreshGeneration;
    private static readonly HttpClient Http = CreateHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static event EventHandler? Changed;

    public static IReadOnlyList<ScheduledSettingRule> Rules
    {
        get
        {
            lock (Gate)
                return LoadDocumentLocked().Rules.Select(Clone).ToArray();
        }
    }

    public static IReadOnlyCollection<string> KnownSettingFields { get; } = new[]
    {
        "language", "theme", "density", "accent", "fontFamily", "fontScale", "fontWeight", "displayName",
    };

    public static ScheduledSettingsResolution Resolve(DateTimeOffset? now = null)
    {
        lock (Gate)
        {
            var document = LoadDocumentLocked();
            DateTimeOffset instant = now ?? DateTimeOffset.Now;
            var matches = document.Rules
                .Select((rule, index) => new { Rule = rule, Index = index })
                .Where(item => item.Rule.Enabled && Matches(item.Rule, instant))
                .OrderBy(item => item.Rule.Priority)
                .ThenBy(item => item.Index)
                .ToArray();

            if (matches.Length == 0)
                return ScheduledSettingsResolution.None;

            var selected = matches[^1].Rule;
            Dictionary<string, string> values = selected.Source switch
            {
                ScheduledSettingSource.Local => new(selected.Values, StringComparer.Ordinal),
                _ => ExternalValues.TryGetValue(selected.Id, out var cached)
                    ? new(cached, StringComparer.Ordinal)
                    : new(StringComparer.Ordinal),
            };

            bool pending = selected.Source != ScheduledSettingSource.Local && values.Count == 0;
            ExternalErrors.TryGetValue(selected.Id, out string? error);
            return new ScheduledSettingsResolution(selected.Id, selected.Label, selected.Source, values, pending, error);
        }
    }

    public static void Upsert(ScheduledSettingRule rule)
    {
        if (rule is null) throw new ArgumentNullException(nameof(rule));
        ScheduledSettingRule normalized = Normalize(rule);
        IReadOnlyList<string> errors = Validate(normalized);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(rule));

        lock (Gate)
        {
            var document = LoadDocumentLocked();
            int index = document.Rules.FindIndex(item => string.Equals(item.Id, normalized.Id, StringComparison.Ordinal));
            ScheduledSettingRule? previous = index >= 0 ? document.Rules[index] : null;
            if (index >= 0)
            {
                document.Rules[index] = normalized;
                // A rule id is not an external-source identity. Editing its endpoint, source,
                // entity, credential or values must not briefly reuse the previous source's data.
                ExternalValues.Remove(normalized.Id);
                ExternalErrors.Remove(normalized.Id);
            }
            else
            {
                if (document.Rules.Count >= MaxRules) throw new InvalidOperationException("The schedule limit is 128 rules.");
                document.Rules.Add(normalized);
            }

            if (previous?.Source == ScheduledSettingSource.HomeAssistantBoolean &&
                (!string.Equals(previous.CredentialKey, normalized.CredentialKey, StringComparison.Ordinal) ||
                 normalized.Source != ScheduledSettingSource.HomeAssistantBoolean))
                RemoveUnusedHomeAssistantTokenLocked(previous.CredentialKey, document, normalized.Id);

            SaveDocumentLocked(document);
            Interlocked.Increment(ref ExternalRefreshGeneration);
        }

        RaiseChanged();
    }

    public static void Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        lock (Gate)
        {
            var document = LoadDocumentLocked();
            ScheduledSettingRule? removed = document.Rules.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            if (removed is null) return;
            document.Rules.Remove(removed);
            ExternalValues.Remove(id);
            ExternalErrors.Remove(id);
            if (removed.Source == ScheduledSettingSource.HomeAssistantBoolean)
                RemoveUnusedHomeAssistantTokenLocked(removed.CredentialKey, document, null);
            SaveDocumentLocked(document);
            Interlocked.Increment(ref ExternalRefreshGeneration);
        }

        RaiseChanged();
    }

    public static IReadOnlyList<string> Validate(ScheduledSettingRule rule)
    {
        var errors = new List<string>();
        if (rule is null) return new[] { "A schedule rule is required." };
        if (!Enum.IsDefined(rule.Source)) errors.Add("The schedule source is invalid.");
        if (!Guid.TryParse(rule.Id, out _)) errors.Add("The rule identifier must be a GUID.");
        if (string.IsNullOrWhiteSpace(rule.Label) || rule.Label.Trim().Length > 120)
            errors.Add("The rule label must be 1–120 characters.");
        if (rule.Values.Count > 32) errors.Add("A rule may set at most 32 fields.");
        foreach (var pair in rule.Values)
        {
            if (!KnownSettingFields.Contains(pair.Key, StringComparer.Ordinal))
                errors.Add($"The setting field '{pair.Key}' is not allowlisted.");
            if (pair.Value is null || pair.Value.Length > MaxValueLength || pair.Value.IndexOf('\0') >= 0)
                errors.Add($"The value for '{pair.Key}' exceeds the {MaxValueLength}-character limit.");
        }

        if (rule.StartDate.HasValue && rule.EndDate.HasValue && rule.EndDate < rule.StartDate)
            errors.Add("The end date cannot be before the start date.");
        if (rule.StartTime.HasValue != rule.EndTime.HasValue)
            errors.Add("Start and end time must be supplied together, or both left empty for an all-day rule.");
        if (!rule.EveryDay && rule.Weekdays.Count == 0)
            errors.Add("Choose Every day or at least one weekday.");
        if (rule.Weekdays.Any(day => !Enum.IsDefined(day)))
            errors.Add("The weekday list contains an invalid value.");
        if (!TryFindTimeZone(rule.TimeZoneId, out _))
            errors.Add("The time zone is not installed on this computer.");

        switch (rule.Source)
        {
            case ScheduledSettingSource.Local when rule.Values.Count == 0:
                errors.Add("A local rule must set at least one allowlisted field.");
                break;
            case ScheduledSettingSource.HttpsApi when !IsAllowedEndpoint(rule.Endpoint):
                errors.Add("The API endpoint must be HTTPS, or HTTP on loopback for development, without embedded credentials.");
                break;
            case ScheduledSettingSource.HomeAssistantBoolean:
                if (!IsAllowedEndpoint(rule.Endpoint)) errors.Add("The Home Assistant base URL must be HTTPS, or HTTP on loopback for development.");
                if (!IsValidEntityId(rule.EntityId)) errors.Add("The Home Assistant entity must look like binary_sensor.name or input_boolean.name.");
                break;
        }

        return errors;
    }

    public static async Task<ScheduledSettingsRefreshReport> RefreshExternalSourcesAsync(
        DateTimeOffset? now = null, CancellationToken cancellationToken = default)
    {
        long generation = Interlocked.Increment(ref ExternalRefreshGeneration);
        ScheduledSettingRule[] candidates;
        lock (Gate)
        {
            var document = LoadDocumentLocked();
            DateTimeOffset instant = now ?? DateTimeOffset.Now;
            candidates = document.Rules.Where(rule => rule.Enabled && rule.Source != ScheduledSettingSource.Local && Matches(rule, instant)).Select(Clone).ToArray();
        }

        var failures = new List<ScheduledSettingsSourceFailure>();
        int refreshed = 0;
        foreach (ScheduledSettingRule rule in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Dictionary<string, string>? values = rule.Source switch
                {
                    ScheduledSettingSource.HttpsApi => await ReadApiValuesAsync(rule, cancellationToken).ConfigureAwait(false),
                    ScheduledSettingSource.HomeAssistantBoolean => await ReadHomeAssistantValuesAsync(rule, cancellationToken).ConfigureAwait(false),
                    _ => null,
                };

                if (values is null)
                    throw new InvalidDataException("The source returned no valid scheduled settings.");
                lock (Gate)
                {
                    if (generation != Volatile.Read(ref ExternalRefreshGeneration)) continue;
                    ExternalValues[rule.Id] = values;
                    ExternalErrors.Remove(rule.Id);
                }
                refreshed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                string safe = ex is HttpRequestException or InvalidDataException or SecurityException
                    ? ex.Message
                    : "The external source could not be refreshed.";
                lock (Gate)
                {
                    if (generation != Volatile.Read(ref ExternalRefreshGeneration)) continue;
                    ExternalErrors[rule.Id] = safe.Length > 240 ? safe[..240] : safe;
                    failures.Add(new ScheduledSettingsSourceFailure(rule.Id, rule.Label, safe));
                }
            }
        }

        if (candidates.Length > 0) RaiseChanged();
        return new ScheduledSettingsRefreshReport(candidates.Length, refreshed, failures);
    }

    public static void SetHomeAssistantToken(string credentialKey, string token)
    {
        string normalizedKey = NormalizeCredentialKey(credentialKey);
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096)
            throw new ArgumentException("The Home Assistant token must be 1–4096 characters.", nameof(token));
        var vault = new PasswordVault();
        RemoveHomeAssistantToken(normalizedKey);
        vault.Add(new PasswordCredential(HomeAssistantCredentialResource, HomeAssistantCredentialUserPrefix + normalizedKey, token));
    }

    public static bool HasHomeAssistantToken(string credentialKey)
    {
        try
        {
            string user = HomeAssistantCredentialUserPrefix + NormalizeCredentialKey(credentialKey);
            var vault = new PasswordVault();
            return vault.RetrieveAll().Any(item => string.Equals(item.Resource, HomeAssistantCredentialResource, StringComparison.Ordinal) &&
                                                   string.Equals(item.UserName, user, StringComparison.Ordinal));
        }
        catch { return false; }
    }

    public static void RemoveHomeAssistantToken(string credentialKey)
    {
        try
        {
            string user = HomeAssistantCredentialUserPrefix + NormalizeCredentialKey(credentialKey);
            var vault = new PasswordVault();
            foreach (var item in vault.RetrieveAll().Where(item => string.Equals(item.Resource, HomeAssistantCredentialResource, StringComparison.Ordinal) &&
                                                                    string.Equals(item.UserName, user, StringComparison.Ordinal)).ToArray())
                vault.Remove(item);
        }
        catch { }
    }

    private static async Task<Dictionary<string, string>> ReadApiValuesAsync(ScheduledSettingRule rule, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, rule.Endpoint);
        using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string json = await ReadBoundedTextAsync(response, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("version", out JsonElement version) || version.GetInt32() != SchemaVersion)
            throw new InvalidDataException("The API response version is not supported.");
        if (!root.TryGetProperty("settings", out JsonElement settings) || settings.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The API response did not contain a settings object.");
        return ReadAllowlistedValues(settings);
    }

    private static async Task<Dictionary<string, string>> ReadHomeAssistantValuesAsync(ScheduledSettingRule rule, CancellationToken cancellationToken)
    {
        string token = ReadHomeAssistantToken(rule.CredentialKey);
        if (string.IsNullOrEmpty(token)) throw new SecurityException("No Home Assistant token is stored for this rule.");
        string endpoint = rule.Endpoint.TrimEnd('/') + "/api/states/" + Uri.EscapeDataString(rule.EntityId);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string json = await ReadBoundedTextAsync(response, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("state", out JsonElement state) || state.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("The Home Assistant response did not contain a state.");
        string value = state.GetString() ?? string.Empty;
        if (!string.Equals(value, "on", StringComparison.OrdinalIgnoreCase))
            return new Dictionary<string, string>(StringComparer.Ordinal);
        return new(rule.Values, StringComparer.Ordinal);
    }

    private static Dictionary<string, string> ReadAllowlistedValues(JsonElement settings)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty property in settings.EnumerateObject())
        {
            if (!KnownSettingFields.Contains(property.Name, StringComparer.Ordinal))
                throw new InvalidDataException($"The API returned an unknown setting field '{property.Name}'.");
            if (property.Value.ValueKind is not JsonValueKind.String)
                throw new InvalidDataException($"The API value for '{property.Name}' was not text.");
            string value = property.Value.GetString() ?? string.Empty;
            if (value.Length > MaxValueLength) throw new InvalidDataException($"The API value for '{property.Name}' is too long.");
            values[property.Name] = value;
        }
        if (values.Count == 0) throw new InvalidDataException("The API returned no allowlisted settings.");
        return values;
    }

    private static async Task<string> ReadBoundedTextAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is > MaxResponseBytes)
            throw new InvalidDataException("The external response is larger than the 256 KiB limit.");
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var memory = new MemoryStream();
        byte[] buffer = new byte[8192];
        int total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > MaxResponseBytes) throw new InvalidDataException("The external response is larger than the 256 KiB limit.");
            memory.Write(buffer, 0, read);
        }
        return Encoding.UTF8.GetString(memory.ToArray());
    }

    private static string ReadHomeAssistantToken(string credentialKey)
    {
        try
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve(HomeAssistantCredentialResource, HomeAssistantCredentialUserPrefix + NormalizeCredentialKey(credentialKey));
            credential.RetrievePassword();
            return credential.Password ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static ScheduleDocument LoadDocumentLocked()
    {
        string raw = SettingsStore.Get(StorageKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw)) return new ScheduleDocument();
        try
        {
            var document = JsonSerializer.Deserialize<ScheduleDocument>(raw, JsonOptions);
            if (document is null || document.Version != SchemaVersion || document.Rules is null || document.Rules.Count > MaxRules)
                return new ScheduleDocument();
            var valid = new ScheduleDocument();
            foreach (ScheduledSettingRule? candidate in document.Rules)
            {
                if (candidate is null) continue;
                ScheduledSettingRule normalized = Normalize(candidate);
                if (Validate(normalized).Count == 0) valid.Rules.Add(normalized);
            }
            return valid;
        }
        catch { return new ScheduleDocument(); }
    }

    private static void SaveDocumentLocked(ScheduleDocument document)
        => SettingsStore.Set(StorageKey, JsonSerializer.Serialize(document, JsonOptions));

    private static ScheduledSettingRule Normalize(ScheduledSettingRule source)
    {
        var copy = Clone(source);
        copy.Id = Guid.TryParse(copy.Id, out _) ? copy.Id : Guid.NewGuid().ToString("D");
        copy.Label = (copy.Label ?? string.Empty).Trim();
        copy.TimeZoneId = string.IsNullOrWhiteSpace(copy.TimeZoneId) ? TimeZoneInfo.Local.Id : copy.TimeZoneId.Trim();
        copy.Endpoint = (copy.Endpoint ?? string.Empty).Trim();
        copy.EntityId = (copy.EntityId ?? string.Empty).Trim();
        copy.CredentialKey = string.IsNullOrWhiteSpace(copy.CredentialKey) ? copy.Id : NormalizeCredentialKey(copy.CredentialKey);
        copy.Values = new Dictionary<string, string>(copy.Values ?? new Dictionary<string, string>(), StringComparer.Ordinal);
        copy.Weekdays = (copy.Weekdays ?? new List<DayOfWeek>()).Distinct().ToList();
        return copy;
    }

    private static bool Matches(ScheduledSettingRule rule, DateTimeOffset instant)
    {
        if (!TryFindTimeZone(rule.TimeZoneId, out TimeZoneInfo? zone) || zone is null) return false;
        DateTime local = TimeZoneInfo.ConvertTime(instant, zone).DateTime;
        DateTime windowDate = local.Date;

        if (rule.StartTime.HasValue && rule.EndTime.HasValue && rule.StartTime.Value > rule.EndTime.Value && local.TimeOfDay < rule.EndTime.Value.ToTimeSpan())
            windowDate = windowDate.AddDays(-1);

        DateOnly date = DateOnly.FromDateTime(windowDate);
        if (rule.StartDate.HasValue && date < rule.StartDate.Value) return false;
        if (rule.EndDate.HasValue && date > rule.EndDate.Value) return false;
        if (!rule.EveryDay && !rule.Weekdays.Contains(windowDate.DayOfWeek)) return false;

        if (!rule.StartTime.HasValue || !rule.EndTime.HasValue) return true;
        TimeSpan now = local.TimeOfDay;
        TimeSpan start = rule.StartTime.Value.ToTimeSpan();
        TimeSpan end = rule.EndTime.Value.ToTimeSpan();
        if (start == end) return true; // Explicitly means a 24-hour window.
        return start < end ? now >= start && now < end : now >= start || now < end;
    }

    private static bool IsAllowedEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri) || !string.IsNullOrEmpty(uri.UserInfo) || uri.Fragment.Length > 0)
            return false;
        if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return IsPublicEndpointHost(uri.Host);
        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)) return false;
        return IPAddress.TryParse(uri.Host, out IPAddress? ip) && IPAddress.IsLoopback(ip)
            || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublicEndpointHost(string host)
    {
        try
        {
            IPAddress[] addresses = IPAddress.TryParse(host, out IPAddress? literal)
                ? new[] { literal! }
                : Dns.GetHostAddresses(host);
            return addresses.Length > 0 && addresses.All(IsPublicAddress);
        }
        catch { return false; }
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || IPAddress.Any.Equals(address) || IPAddress.IPv6Any.Equals(address)) return false;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] bytes = address.GetAddressBytes();
            int first = bytes[0], second = bytes[1];
            if (first is 0 or 10 or 127 or 169 or 224 or > 239) return false;
            if (first == 100 && second is >= 64 and <= 127) return false;
            if (first == 172 && second is >= 16 and <= 31) return false;
            if (first == 192 && second == 168) return false;
            if (first == 192 && second == 0) return false;
            if (first == 198 && second is 18 or 19 or 51) return false;
            if (first == 203 && second == 0 && bytes[2] == 113) return false;
            return true;
        }

        byte[] ipv6 = address.GetAddressBytes();
        bool uniqueLocal = (ipv6[0] & 0xFE) == 0xFC;
        return !address.IsIPv6LinkLocal && !address.IsIPv6SiteLocal && !address.IsIPv6Multicast && !uniqueLocal;
    }

    private static void RemoveUnusedHomeAssistantTokenLocked(string credentialKey, ScheduleDocument document, string? excludedId)
    {
        if (string.IsNullOrWhiteSpace(credentialKey)) return;
        bool stillUsed = document.Rules.Any(rule =>
            rule.Source == ScheduledSettingSource.HomeAssistantBoolean &&
            !string.Equals(rule.Id, excludedId, StringComparison.Ordinal) &&
            string.Equals(rule.CredentialKey, credentialKey, StringComparison.Ordinal));
        if (!stillUsed) RemoveHomeAssistantToken(credentialKey);
    }

    private static bool IsValidEntityId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 255) return false;
        string[] parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && (parts[0].Equals("binary_sensor", StringComparison.OrdinalIgnoreCase) ||
                                     parts[0].Equals("input_boolean", StringComparison.OrdinalIgnoreCase)) &&
               parts[1].All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-');
    }

    private static bool TryFindTimeZone(string? id, out TimeZoneInfo? zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(id) ? TimeZoneInfo.Local.Id : id);
            return true;
        }
        catch
        {
            zone = null;
            return false;
        }
    }

    private static string NormalizeCredentialKey(string value)
    {
        string normalized = new(value.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
        if (normalized.Length is < 1 or > 128) throw new ArgumentException("The credential key must be 1–128 letters, digits, hyphens, or underscores.", nameof(value));
        return normalized;
    }

    private static ScheduledSettingRule Clone(ScheduledSettingRule source) => new()
    {
        Id = source.Id,
        Label = source.Label,
        Enabled = source.Enabled,
        Priority = source.Priority,
        StartDate = source.StartDate,
        EndDate = source.EndDate,
        StartTime = source.StartTime,
        EndTime = source.EndTime,
        EveryDay = source.EveryDay,
        Weekdays = new List<DayOfWeek>(source.Weekdays ?? new List<DayOfWeek>()),
        TimeZoneId = source.TimeZoneId,
        Source = source.Source,
        Values = new Dictionary<string, string>(source.Values ?? new Dictionary<string, string>(), StringComparer.Ordinal),
        Endpoint = source.Endpoint,
        EntityId = source.EntityId,
        CredentialKey = source.CredentialKey,
    };

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(RefreshTimeoutSeconds) };
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = false };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static void RaiseChanged()
    {
        try { Changed?.Invoke(null, EventArgs.Empty); } catch { }
    }

    private sealed class ScheduleDocument
    {
        public int Version { get; set; } = SchemaVersion;
        public List<ScheduledSettingRule> Rules { get; set; } = new();
    }
}

public enum ScheduledSettingSource
{
    Local,
    HttpsApi,
    HomeAssistantBoolean,
}

public sealed class ScheduledSettingRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public string Label { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public bool EveryDay { get; set; } = true;
    public List<DayOfWeek> Weekdays { get; set; } = new();
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;
    public ScheduledSettingSource Source { get; set; } = ScheduledSettingSource.Local;
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);
    public string Endpoint { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string CredentialKey { get; set; } = string.Empty;
}

public sealed record ScheduledSettingsResolution(
    string? RuleId,
    string? Label,
    ScheduledSettingSource? Source,
    IReadOnlyDictionary<string, string> Values,
    bool PendingExternal,
    string? Error)
{
    public static ScheduledSettingsResolution None { get; } = new(null, null, null,
        new Dictionary<string, string>(StringComparer.Ordinal), false, null);
}

public sealed record ScheduledSettingsSourceFailure(string RuleId, string Label, string Error);

public sealed record ScheduledSettingsRefreshReport(
    int MatchingExternalRules,
    int RefreshedRules,
    IReadOnlyList<ScheduledSettingsSourceFailure> Failures);
