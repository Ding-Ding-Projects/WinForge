using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Windows.Security.Credentials;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// Shared app-wide settings required by every WinForge user-facing surface.
/// The values live in the common WinForge settings store so independently opened
/// surfaces observe the same state. The School-mode unlock credential is kept in
/// the current-user Windows credential vault and never enters JSON settings,
/// exports, history, logs, or the renderer.
/// </summary>
public static class UniversalSettingsService
{
    private const string EmojiKey = "universal.emojiDialogsEnabled";
    private const string SchoolKey = "universal.schoolModeEnabled";
    private const string SchoolNameKey = "universal.schoolModeName";
    private const string PreviousLanguageKey = "universal.previousLanguage";
    private const string CredentialResource = "WinForge.UniversalSchoolMode";
    private const string CredentialUser = "shared-unlock";
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge");
    private static readonly FileSystemWatcher? Watcher = CreateWatcher();

    public static event EventHandler? Changed;

    public static bool EmojiDialogsEnabled
    {
        get => ReadBool(EmojiKey, fallback: true);
        set => WriteBool(EmojiKey, value);
    }

    public static bool SchoolModeEnabled
    {
        get => ReadBool(SchoolKey, fallback: false);
        set => SetSchoolMode(value);
    }

    public static string SchoolModeName
    {
        get
        {
            string value = SettingsStore.Get(SchoolNameKey, "School mode").Trim();
            return IsValidName(value) ? value : "School mode";
        }
        set
        {
            string normalized = (value ?? string.Empty).Trim();
            if (!IsValidName(normalized)) throw new ArgumentException("The mode name must be 1–64 characters without control characters.", nameof(value));
            if (string.Equals(SchoolModeName, normalized, StringComparison.Ordinal)) return;
            SettingsStore.Set(SchoolNameKey, normalized);
            RaiseChanged();
        }
    }

    public static bool HasSchoolUnlock
    {
        get
        {
            try
            {
                var vault = new PasswordVault();
                return vault.RetrieveAll().Any(c => string.Equals(c.Resource, CredentialResource, StringComparison.Ordinal) &&
                                                    string.Equals(c.UserName, CredentialUser, StringComparison.Ordinal));
            }
            catch { return false; }
        }
    }

    public static void SetSchoolUnlock(string pinOrPassword)
    {
        string value = pinOrPassword ?? string.Empty;
        if (value.Length is < 4 or > 256 || value.IndexOf('\0') >= 0)
            throw new ArgumentException("The unlock value must be 4–256 characters.", nameof(pinOrPassword));

        var vault = new PasswordVault();
        try
        {
            foreach (var credential in vault.RetrieveAll())
            {
                if (string.Equals(credential.Resource, CredentialResource, StringComparison.Ordinal) &&
                    string.Equals(credential.UserName, CredentialUser, StringComparison.Ordinal))
                    vault.Remove(credential);
            }
        }
        catch { }

        vault.Add(new PasswordCredential(CredentialResource, CredentialUser, value));
        RaiseChanged();
    }

    public static bool VerifySchoolUnlock(string pinOrPassword)
    {
        try
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve(CredentialResource, CredentialUser);
            credential.RetrievePassword();
            byte[] expected = Encoding.UTF8.GetBytes(credential.Password ?? string.Empty);
            byte[] actual = Encoding.UTF8.GetBytes(pinOrPassword ?? string.Empty);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch { return false; }
    }

    public static void ClearSchoolUnlock()
    {
        try
        {
            var vault = new PasswordVault();
            foreach (var credential in vault.RetrieveAll())
            {
                if (string.Equals(credential.Resource, CredentialResource, StringComparison.Ordinal) &&
                    string.Equals(credential.UserName, CredentialUser, StringComparison.Ordinal))
                    vault.Remove(credential);
            }
        }
        catch { }
        RaiseChanged();
    }

    private static void SetSchoolMode(bool enabled)
    {
        bool current = SchoolModeEnabled;
        if (current == enabled) return;
        if (enabled)
        {
            SettingsStore.Set(PreviousLanguageKey, Loc.I.Language.ToString());
            Loc.I.Language = AppLanguage.English;
        }

        SettingsStore.Set(SchoolKey, enabled.ToString());
        if (!enabled && Enum.TryParse(SettingsStore.Get(PreviousLanguageKey, nameof(AppLanguage.Bilingual)), true, out AppLanguage language))
            Loc.I.Language = language;
        RaiseChanged();
    }

    private static bool ReadBool(string key, bool fallback)
        => bool.TryParse(SettingsStore.Get(key, fallback.ToString()), out bool value) ? value : fallback;

    private static void WriteBool(string key, bool value)
    {
        if (ReadBool(key, !value) == value) return;
        SettingsStore.Set(key, value.ToString());
        RaiseChanged();
    }

    private static bool IsValidName(string value)
    {
        if (value.Length is < 1 or > 64) return false;
        foreach (char ch in value) if (char.IsControl(ch)) return false;
        return true;
    }

    private static FileSystemWatcher? CreateWatcher()
    {
        try
        {
            if (!Directory.Exists(SettingsDirectory)) return null;
            var watcher = new FileSystemWatcher(SettingsDirectory, "settings.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };
            watcher.Changed += (_, _) => RaiseChanged();
            watcher.Created += (_, _) => RaiseChanged();
            watcher.Renamed += (_, _) => RaiseChanged();
            return watcher;
        }
        catch { return null; }
    }

    private static void RaiseChanged()
    {
        try { Changed?.Invoke(null, EventArgs.Empty); } catch { }
    }
}
