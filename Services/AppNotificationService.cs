using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// Application-wide, local notification bus and reviewable history. History is retained only in the
/// current Windows profile through SettingsStore; action delegates and notices marked non-persistent
/// never leave memory.
/// </summary>
public static class AppNotificationService
{
    private const string HistorySettingsKey = "notifications.history.v1";
    private static readonly object Gate = new();
    private static readonly NotificationCenterState State = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static event EventHandler? Changed;

    static AppNotificationService()
    {
        if (IsAutomationFixture) return;
        try
        {
            var json = SettingsStore.Get(HistorySettingsKey, "[]");
            State.RestoreHistory(JsonSerializer.Deserialize<List<AppNoticeEntry>>(json, JsonOptions));
        }
        catch
        {
            State.RestoreHistory(null);
        }
    }

    public static IReadOnlyList<AppNoticeEntry> Active
    {
        get { lock (Gate) return State.Active.ToArray(); }
    }

    public static IReadOnlyList<AppNoticeEntry> History
    {
        get { lock (Gate) return State.History.ToArray(); }
    }

    public static IReadOnlyList<AppNoticeEntry> VisibleActive
    {
        get { lock (Gate) return State.Active.Where(IsVisibleInCurrentMode).ToArray(); }
    }

    public static IReadOnlyList<AppNoticeEntry> VisibleHistory
    {
        get { lock (Gate) return State.History.Where(IsVisibleInCurrentMode).ToArray(); }
    }

    public static int UnreadCount
    {
        get { lock (Gate) return State.UnreadCount; }
    }

    public static int VisibleUnreadCount
    {
        get { lock (Gate) return State.History.Count(x => x.IsUnread && IsVisibleInCurrentMode(x)); }
    }

    public static string Publish(AppNoticeDraft draft)
    {
        AppNoticeEntry entry;
        lock (Gate)
        {
            entry = State.Publish(draft);
            PersistLocked();
        }
        RaiseChanged();
        if (entry.AutoDismissMs > 0)
            _ = AutoDismissAsync(entry.Id, entry.CreatedAt, entry.AutoDismissMs);
        return entry.Id;
    }

    public static bool Dismiss(string id)
    {
        bool changed;
        lock (Gate)
        {
            changed = State.Dismiss(id);
            if (changed) PersistLocked();
        }
        if (changed) RaiseChanged();
        return changed;
    }

    public static void MarkAllViewed()
    {
        lock (Gate)
        {
            State.MarkAllViewed();
            PersistLocked();
        }
        RaiseChanged();
    }

    public static void ClearDismissedHistory()
    {
        lock (Gate)
        {
            State.ClearDismissedHistory();
            PersistLocked();
        }
        RaiseChanged();
    }

    private static async Task AutoDismissAsync(string id, DateTimeOffset createdAt, int delayMs)
    {
        try { await Task.Delay(delayMs).ConfigureAwait(false); }
        catch { return; }

        bool changed;
        lock (Gate)
        {
            changed = State.Dismiss(id, createdAt);
            if (changed) PersistLocked();
        }
        if (changed) RaiseChanged();
    }

    private static void PersistLocked()
    {
        if (IsAutomationFixture) return;
        try
        {
            SettingsStore.Set(
                HistorySettingsKey,
                JsonSerializer.Serialize(State.PersistentHistory(), JsonOptions));
        }
        catch { }
    }

    private static void RaiseChanged()
    {
        try { Changed?.Invoke(null, EventArgs.Empty); } catch { }
    }

    private static bool IsVisibleInCurrentMode(AppNoticeEntry entry)
        => !UniversalSettingsService.SchoolModeEnabled || !string.Equals(entry.Key, "dim-sum.surprise", StringComparison.Ordinal);

    private static bool IsAutomationFixture
    {
        get
        {
#if DEBUG
            return string.Equals(
                Environment.GetEnvironmentVariable("WINFORGE_NOTIFICATION_DEMO"),
                "1",
                StringComparison.Ordinal);
#else
            return false;
#endif
        }
    }
}
