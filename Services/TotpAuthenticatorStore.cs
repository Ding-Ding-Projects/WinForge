using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Windows.Security.Credentials;

namespace WinForge.Services;

/// <summary>
/// Local TOTP authenticator metadata and secret store.
///
/// Entry metadata is versioned JSON in the normal local settings file. Secret values are never
/// written there: they live in the Windows credential vault under a stable per-entry key. A
/// candidate imported from a URI remains in memory until the user proves pairing with one current
/// code, so a mistyped QR or URI cannot silently arm a broken lock.
/// </summary>
public static class TotpAuthenticatorStore
{
    private const int SchemaVersion = 1;
    private const int MaxEntries = 256;
    private const int MaxText = 256;
    private const int MaxGroup = 128;
    private const int MaxHistory = 512;
    private const string MetadataKey = "totp.authenticator.entries.v1";
    private const string HistoryKey = "totp.authenticator.history.v1";
    private const string VaultResource = "WinForge.TotpAuthenticator.Secret.v1";
    private const string VaultUserPrefix = "entry:";

    private static readonly object Gate = new();
    private static List<Entry>? _entries;
    private static List<Mutation>? _history;
    private static string? _historyWarning;

    public static event EventHandler? Changed;

    public static string? LastHistoryWarning
    {
        get
        {
            lock (Gate) return _historyWarning;
        }
    }

    public sealed class Entry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("D");
        public string Issuer { get; set; } = string.Empty;
        public string Account { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public int Digits { get; set; } = 6;
        public int Period { get; set; } = 30;
        public TotpService.HashAlgo Algorithm { get; set; } = TotpService.HashAlgo.Sha1;
        public int Order { get; set; }
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

        public string DisplayLabel => string.IsNullOrWhiteSpace(Label)
            ? (string.IsNullOrWhiteSpace(Issuer) ? Account : Issuer + " · " + Account)
            : Label;
    }

    /// <summary>One-time in-memory candidate. The secret is deliberately not serializable.</summary>
    public sealed class PendingEntry
    {
        internal string Secret { get; init; } = string.Empty;
        public Entry Metadata { get; init; } = new();
    }

    public sealed class Mutation
    {
        public string Action { get; set; } = string.Empty;
        public string EntryId { get; set; } = string.Empty;
        public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    private sealed class Document
    {
        public int Version { get; set; } = SchemaVersion;
        public List<Entry> Entries { get; set; } = new();
    }

    public static IReadOnlyList<Entry> Entries
    {
        get
        {
            lock (Gate)
            {
                EnsureLoadedLocked();
                return _entries!.OrderBy(item => item.Order).ThenBy(item => item.DisplayLabel, StringComparer.OrdinalIgnoreCase)
                    .Select(Clone).ToArray();
            }
        }
    }

    public static IReadOnlyList<Mutation> History
    {
        get
        {
            lock (Gate)
            {
                EnsureLoadedLocked();
                return _history!.Select(Clone).ToArray();
            }
        }
    }

    public static PendingEntry? PrepareFromUri(string? uri, out string error)
    {
        error = string.Empty;
        TotpService.OtpAuth? parsed = TotpService.ParseUri(uri);
        if (parsed is null)
        {
            error = "The value is not a valid otpauth://totp/ URI.";
            return null;
        }

        string secret = NormalizeSecret(parsed.Secret);
        if (!IsUsableSecret(secret))
        {
            error = "The URI does not contain a valid Base32 secret.";
            return null;
        }
        if (parsed.Digits is < 6 or > 8 || parsed.Period is < 1 or > 3600)
        {
            error = "The URI must request 6–8 digits and a 1–3600 second period.";
            return null;
        }

        string label = Limit(parsed.Label ?? string.Empty, MaxText);
        string issuer = Limit(parsed.Issuer ?? string.Empty, MaxText);
        string account = label;
        if (!string.IsNullOrWhiteSpace(issuer) && account.StartsWith(issuer + ":", StringComparison.OrdinalIgnoreCase))
            account = account[(issuer.Length + 1)..];
        if (string.IsNullOrWhiteSpace(account) && !string.IsNullOrWhiteSpace(issuer)) account = issuer;
        Entry metadata = NewMetadata(issuer, account, label, parsed.Digits, parsed.Period, parsed.Algorithm);
        return new PendingEntry { Metadata = metadata, Secret = secret };
    }

    public static PendingEntry? PrepareManual(
        string? secret,
        string? issuer,
        string? account,
        int digits,
        int period,
        TotpService.HashAlgo algorithm,
        string? label,
        string? group,
        out string error)
    {
        error = string.Empty;
        string normalized = NormalizeSecret(secret ?? string.Empty);
        if (!IsUsableSecret(normalized))
        {
            error = "Enter a valid Base32 secret.";
            return null;
        }
        if (digits is < 6 or > 8 || period is < 1 or > 3600)
        {
            error = "Digits must be 6–8 and the period must be 1–3600 seconds.";
            return null;
        }

        string safeIssuer = Limit(issuer ?? string.Empty, MaxText);
        string safeAccount = Limit(account ?? string.Empty, MaxText);
        string safeLabel = Limit(string.IsNullOrWhiteSpace(label) ? safeAccount : label, MaxText);
        return new PendingEntry
        {
            Metadata = NewMetadata(safeIssuer, safeAccount, safeLabel, digits, period, algorithm, group),
            Secret = normalized,
        };
    }

    public static bool ConfirmAndSave(PendingEntry? pending, string? confirmationCode, long unixSeconds, out Entry? saved, out string error)
    {
        saved = null;
        error = string.Empty;
        if (pending is null || pending.Metadata is null || !IsUsableSecret(pending.Secret))
        {
            error = "There is no valid pending authenticator entry.";
            return false;
        }

        string expected = TotpService.Compute(
            pending.Secret,
            pending.Metadata.Digits,
            pending.Metadata.Period,
            pending.Metadata.Algorithm,
            unixSeconds) ?? string.Empty;
        string supplied = DigitsOnly(confirmationCode);
        if (expected.Length == 0 || supplied.Length != expected.Length ||
            !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(supplied)))
        {
            error = "The pairing code did not match the current code.";
            return false;
        }

        lock (Gate)
        {
            EnsureLoadedLocked();
            if (_entries!.Count >= MaxEntries)
            {
                error = $"The authenticator list is limited to {MaxEntries} entries.";
                return false;
            }

            Entry entry = Clone(pending.Metadata);
            entry.Order = _entries.Count == 0 ? 0 : _entries.Max(item => item.Order) + 1;
            if (!TryWriteSecretLocked(entry.Id, pending.Secret, out error)) return false;

            _entries.Add(entry);
            if (!TryPersistLocked())
            {
                _entries.RemoveAll(item => item.Id == entry.Id);
                if (!RemoveSecretLocked(entry.Id))
                    error = "The metadata store could not be written and the vault secret could not be removed; recovery is required before retrying.";
                else
                    error = "The metadata store could not be written; the entry was not saved.";
                return false;
            }
            AddMutationLocked("created", entry.Id);
            saved = Clone(entry);
        }

        RaiseChanged();
        return true;
    }

    public static bool TryGetCode(Entry? entry, long unixSeconds, out string code, out int secondsRemaining)
    {
        code = string.Empty;
        secondsRemaining = 0;
        if (entry is null) return false;
        if (!TryReadSecret(entry.Id, out string? secret) || string.IsNullOrWhiteSpace(secret)) return false;
        code = TotpService.Compute(secret, entry.Digits, entry.Period, entry.Algorithm, unixSeconds) ?? string.Empty;
        secondsRemaining = TotpService.SecondsRemaining(entry.Period, unixSeconds);
        return code.Length > 0;
    }

    public static bool TryBuildUri(Entry? entry, out string uri)
    {
        uri = string.Empty;
        if (entry is null || !TryReadSecret(entry.Id, out string? secret) || string.IsNullOrWhiteSpace(secret)) return false;
        string label = string.IsNullOrWhiteSpace(entry.Issuer)
            ? entry.Account
            : entry.Issuer + ":" + entry.Account;
        uri = "otpauth://totp/" + Uri.EscapeDataString(label) +
            "?secret=" + Uri.EscapeDataString(secret) +
            "&issuer=" + Uri.EscapeDataString(entry.Issuer) +
            "&algorithm=" + entry.Algorithm.ToString().ToUpperInvariant() +
            "&digits=" + entry.Digits.ToString(CultureInfo.InvariantCulture) +
            "&period=" + entry.Period.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    public static bool UpdateMetadata(string id, string? issuer, string? account, string? label, string? group, out string error)
    {
        error = string.Empty;
        lock (Gate)
        {
            EnsureLoadedLocked();
            Entry? entry = _entries!.FirstOrDefault(item => item.Id == id);
            if (entry is null) { error = "The authenticator entry no longer exists."; return false; }
            string oldIssuer = entry.Issuer;
            string oldAccount = entry.Account;
            string oldLabel = entry.Label;
            string oldGroup = entry.Group;
            entry.Issuer = Limit(issuer ?? string.Empty, MaxText);
            entry.Account = Limit(account ?? string.Empty, MaxText);
            entry.Label = Limit(label ?? string.Empty, MaxText);
            entry.Group = Limit(group ?? string.Empty, MaxGroup);
            if (!TryPersistLocked())
            {
                entry.Issuer = oldIssuer;
                entry.Account = oldAccount;
                entry.Label = oldLabel;
                entry.Group = oldGroup;
                error = "The authenticator metadata could not be saved; the prior value was restored.";
                return false;
            }
            AddMutationLocked("updated", entry.Id);
        }
        RaiseChanged();
        return true;
    }

    public static bool Move(string id, int newOrder, out string error)
    {
        error = string.Empty;
        lock (Gate)
        {
            EnsureLoadedLocked();
            Entry? entry = _entries!.FirstOrDefault(item => item.Id == id);
            if (entry is null) { error = "The authenticator entry no longer exists."; return false; }
            int oldOrder = entry.Order;
            entry.Order = Math.Clamp(newOrder, 0, MaxEntries - 1);
            NormalizeOrderLocked();
            if (!TryPersistLocked())
            {
                entry.Order = oldOrder;
                NormalizeOrderLocked();
                error = "The authenticator order could not be saved; the prior order was restored.";
                return false;
            }
            AddMutationLocked("reordered", entry.Id);
        }
        RaiseChanged();
        return true;
    }

    public static bool Delete(string id, out string error)
    {
        error = string.Empty;
        lock (Gate)
        {
            EnsureLoadedLocked();
            Entry? entry = _entries!.FirstOrDefault(item => item.Id == id);
            if (entry is null) { error = "The authenticator entry no longer exists."; return false; }
            if (!TryReadSecret(entry.Id, out string? secret) || string.IsNullOrWhiteSpace(secret))
            {
                error = "The Windows credential vault did not return the secret; the entry was kept.";
                return false;
            }
            if (!RemoveSecretLocked(entry.Id))
            {
                error = "The Windows credential vault did not remove the secret; the entry was kept.";
                return false;
            }
            int originalOrder = entry.Order;
            _entries!.Remove(entry);
            NormalizeOrderLocked();
            if (!TryPersistLocked())
            {
                _entries.Insert(Math.Clamp(originalOrder, 0, _entries.Count), entry);
                NormalizeOrderLocked();
                if (!TryWriteSecretLocked(entry.Id, secret, out string restoreError))
                {
                    error = "The metadata could not be saved and the vault secret could not be restored: " + restoreError;
                    return false;
                }
                error = "The metadata could not be saved; the entry and its vault secret were restored.";
                return false;
            }
            AddMutationLocked("deleted", entry.Id);
        }
        RaiseChanged();
        return true;
    }

    public static bool ExportRedactedJson(string path, out string error)
    {
        error = string.Empty;
        try
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            lock (Gate)
            {
                EnsureLoadedLocked();
                var payload = new
                {
                    version = SchemaVersion,
                    secretsOmitted = true,
                    encoding = "UTF-8",
                    entries = _entries!.OrderBy(item => item.Order).Select(Clone).ToArray(),
                };
                File.WriteAllText(fullPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            }
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static bool ExportRedactedCsv(string path, out string error)
    {
        error = string.Empty;
        try
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            lock (Gate)
            {
                EnsureLoadedLocked();
                var builder = new StringBuilder("id,issuer,account,label,group,digits,period,algorithm,createdUtc,secretsOmitted\n");
                foreach (Entry entry in _entries!.OrderBy(item => item.Order))
                {
                    builder.Append(Csv(entry.Id)).Append(',')
                        .Append(Csv(entry.Issuer)).Append(',')
                        .Append(Csv(entry.Account)).Append(',')
                        .Append(Csv(entry.Label)).Append(',')
                        .Append(Csv(entry.Group)).Append(',')
                        .Append(entry.Digits.ToString(CultureInfo.InvariantCulture)).Append(',')
                        .Append(entry.Period.ToString(CultureInfo.InvariantCulture)).Append(',')
                        .Append(entry.Algorithm.ToString()).Append(',')
                        .Append(entry.CreatedUtc.ToString("O", CultureInfo.InvariantCulture)).Append(",true\n");
                }
                File.WriteAllText(fullPath, builder.ToString(), new UTF8Encoding(false));
            }
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static Entry NewMetadata(string issuer, string account, string label, int digits, int period, TotpService.HashAlgo algorithm, string? group = null)
        => new()
        {
            Issuer = issuer,
            Account = account,
            Label = label,
            Group = Limit(group ?? string.Empty, MaxGroup),
            Digits = digits,
            Period = period,
            Algorithm = algorithm,
            CreatedUtc = DateTimeOffset.UtcNow,
        };

    private static void EnsureLoadedLocked()
    {
        if (_entries is not null && _history is not null) return;
        _entries = new List<Entry>();
        _history = new List<Mutation>();
        try
        {
            string raw = SettingsStore.Get(MetadataKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                Document? document = JsonSerializer.Deserialize<Document>(raw);
                if (document?.Version == SchemaVersion && document.Entries is not null)
                {
                    foreach (Entry? candidate in document.Entries.Take(MaxEntries))
                    {
                        if (candidate is null || !Guid.TryParse(candidate.Id, out _) || !IsMetadataValid(candidate)) continue;
                        if (_entries.Any(item => item.Id == candidate.Id)) continue;
                        _entries.Add(Clone(candidate));
                    }
                    NormalizeOrderLocked();
                }
            }
            string historyRaw = SettingsStore.Get(HistoryKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(historyRaw))
            {
                List<Mutation>? history = JsonSerializer.Deserialize<List<Mutation>>(historyRaw);
                if (history is not null) _history.AddRange(history.Where(IsMutationValid).TakeLast(MaxHistory).Select(Clone));
            }
        }
        catch
        {
            _entries.Clear();
            _history.Clear();
        }
    }

    private static bool TryPersistLocked()
    {
        try
        {
            var document = new Document { Version = SchemaVersion, Entries = _entries!.OrderBy(item => item.Order).Select(Clone).ToList() };
            return SettingsStore.Set(MetadataKey, JsonSerializer.Serialize(document));
        }
        catch { return false; }
    }

    private static void AddMutationLocked(string action, string id)
    {
        List<Mutation> previous = _history!.Select(Clone).ToList();
        Mutation mutation = new() { Action = action, EntryId = id, TimestampUtc = DateTimeOffset.UtcNow };
        _history!.Add(mutation);
        if (_history.Count > MaxHistory) _history.RemoveRange(0, _history.Count - MaxHistory);
        if (SettingsStore.Set(HistoryKey, JsonSerializer.Serialize(_history)))
        {
            _historyWarning = null;
            return;
        }

        _history.Clear();
        _history.AddRange(previous);
        _historyWarning = "The authenticator change completed, but its local history entry could not be persisted.";
    }

    private static bool TryWriteSecretLocked(string id, string secret, out string error)
    {
        error = string.Empty;
        try
        {
            if (!RemoveSecretLocked(id))
            {
                error = "The existing vault secret could not be replaced.";
                return false;
            }
            var vault = new PasswordVault();
            vault.Add(new PasswordCredential(VaultResource, VaultUserPrefix + id, secret));
            return true;
        }
        catch (Exception exception)
        {
            error = "The Windows credential vault could not store this secret: " + exception.Message;
            return false;
        }
    }

    private static bool TryReadSecret(string id, out string? secret)
    {
        secret = null;
        try
        {
            var vault = new PasswordVault();
            PasswordCredential credential = vault.Retrieve(VaultResource, VaultUserPrefix + id);
            credential.RetrievePassword();
            secret = credential.Password;
            return !string.IsNullOrWhiteSpace(secret);
        }
        catch { return false; }
    }

    private static bool RemoveSecretLocked(string id)
    {
        try
        {
            var vault = new PasswordVault();
            PasswordCredential? match = vault.RetrieveAll().FirstOrDefault(item =>
                string.Equals(item.Resource, VaultResource, StringComparison.Ordinal) &&
                string.Equals(item.UserName, VaultUserPrefix + id, StringComparison.Ordinal));
            if (match is null) return true;
            vault.Remove(match);
            return true;
        }
        catch { return false; }
    }

    private static bool IsUsableSecret(string secret) =>
        secret.Length is >= 8 and <= 512 && TotpService.DecodeBase32(secret) is { Length: > 0 };

    private static bool IsMetadataValid(Entry entry) =>
        entry is not null &&
        entry.Id is { Length: > 0 and <= MaxText } &&
        entry.Issuer is { Length: <= MaxText } &&
        entry.Account is { Length: <= MaxText } &&
        entry.Label is { Length: <= MaxText } &&
        entry.Group is { Length: <= MaxGroup } &&
        entry.Digits is >= 6 and <= 8 && entry.Period is >= 1 and <= 3600 &&
        Enum.IsDefined(entry.Algorithm);

    private static bool IsMutationValid(Mutation mutation) =>
        mutation is not null &&
        mutation.EntryId is { Length: > 0 and <= MaxText } &&
        mutation.Action is { Length: > 0 and <= MaxText };

    private static void NormalizeOrderLocked()
    {
        int order = 0;
        foreach (Entry entry in _entries!.OrderBy(item => item.Order).ThenBy(item => item.CreatedUtc)) entry.Order = order++;
    }

    private static string NormalizeSecret(string value) =>
        new string(value.Where(character => !char.IsWhiteSpace(character) && character != '-' && character != '=').ToArray()).ToUpperInvariant();

    private static string DigitsOnly(string? value) => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

    private static string Limit(string value, int max) => value.Length <= max ? value : value[..max];

    private static string Csv(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static Entry Clone(Entry entry) => new()
    {
        Id = entry.Id, Issuer = entry.Issuer, Account = entry.Account, Label = entry.Label, Group = entry.Group,
        Digits = entry.Digits, Period = entry.Period, Algorithm = entry.Algorithm, Order = entry.Order, CreatedUtc = entry.CreatedUtc,
    };

    private static Mutation Clone(Mutation mutation) => new()
    {
        Action = mutation.Action, EntryId = mutation.EntryId, TimestampUtc = mutation.TimestampUtc,
    };

    private static void RaiseChanged()
    {
        try { Changed?.Invoke(null, EventArgs.Empty); } catch { }
    }
}
