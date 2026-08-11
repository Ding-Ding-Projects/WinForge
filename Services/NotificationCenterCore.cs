using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WinForge.Services;

public enum AppNoticeSeverity
{
    Informational,
    Success,
    Warning,
    Error,
    Progress,
}

public sealed record AppNoticeAction(
    string LabelEn,
    string LabelZh,
    string? Link = null,
    bool DismissOnInvoke = true,
    [property: JsonIgnore] Func<Task>? Handler = null);

public sealed record AppNoticeDraft(
    string TitleEn,
    string TitleZh,
    string BodyEn,
    string BodyZh,
    AppNoticeSeverity Severity = AppNoticeSeverity.Informational,
    string? Key = null,
    int? AutoDismissMs = null,
    bool PersistInHistory = true,
    IReadOnlyList<AppNoticeAction>? Actions = null,
    string? ImagePath = null,
    string? ImageAltEn = null,
    string? ImageAltZh = null);

public sealed record AppNoticeEntry(
    string Id,
    string? Key,
    string TitleEn,
    string TitleZh,
    string BodyEn,
    string BodyZh,
    AppNoticeSeverity Severity,
    DateTimeOffset CreatedAt,
    int AutoDismissMs,
    bool PersistInHistory,
    bool IsDismissed,
    bool IsUnread,
    [property: JsonIgnore] IReadOnlyList<AppNoticeAction>? Actions = null,
    string? ImagePath = null,
    string? ImageAltEn = null,
    string? ImageAltZh = null);

/// <summary>
/// Bounded, UI-independent notification state. It deliberately contains no WinUI types so the
/// retention, replacement, dismissal, and persistence contracts can be exercised without launching
/// or mutating the desktop application.
/// </summary>
public sealed class NotificationCenterState
{
    public const int DefaultHistoryLimit = 200;
    public const int DefaultActiveLimit = 4;
    public const int MaximumTitleLength = 160;
    public const int MaximumBodyLength = 2_048;
    public const int MaximumActionCount = 3;
    public const int MaximumAutoDismissMs = 10 * 60 * 1_000;

    private readonly int _historyLimit;
    private readonly int _activeLimit;
    private readonly List<AppNoticeEntry> _history = new();
    private readonly List<AppNoticeEntry> _active = new();

    public NotificationCenterState(
        int historyLimit = DefaultHistoryLimit,
        int activeLimit = DefaultActiveLimit)
    {
        _historyLimit = Math.Clamp(historyLimit, 1, 2_000);
        _activeLimit = Math.Clamp(activeLimit, 1, 12);
    }

    public IReadOnlyList<AppNoticeEntry> Active
        => _active.OrderBy(x => x.CreatedAt).ToArray();

    public IReadOnlyList<AppNoticeEntry> History
        => _history.OrderByDescending(x => x.CreatedAt).ToArray();

    public int UnreadCount => _history.Count(x => x.IsUnread);

    public AppNoticeEntry Publish(AppNoticeDraft draft, DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var now = createdAt ?? DateTimeOffset.Now;
        var key = Clean(draft.Key, 128, allowEmpty: true);
        var previous = string.IsNullOrWhiteSpace(key)
            ? null
            : _active.LastOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal));
        var id = previous?.Id ?? Guid.NewGuid().ToString("N");
        var actions = NormalizeActions(draft.Actions);
        var entry = new AppNoticeEntry(
            id,
            key,
            Clean(draft.TitleEn, MaximumTitleLength),
            Clean(draft.TitleZh, MaximumTitleLength),
            Clean(draft.BodyEn, MaximumBodyLength, allowEmpty: true),
            Clean(draft.BodyZh, MaximumBodyLength, allowEmpty: true),
            draft.Severity,
            now,
            NormalizeAutoDismiss(draft.Severity, draft.AutoDismissMs),
            draft.PersistInHistory,
            IsDismissed: false,
            IsUnread: true,
            actions,
            NormalizeImagePath(draft.ImagePath),
            Clean(draft.ImageAltEn, 300, allowEmpty: true),
            Clean(draft.ImageAltZh, 300, allowEmpty: true));

        if (previous is not null)
        {
            _active.RemoveAll(x => x.Id == previous.Id);
            _history.RemoveAll(x => x.Id == previous.Id);
        }

        _active.Add(entry);
        _history.Add(entry);
        TrimActive();
        TrimHistory();
        return entry;
    }

    public bool Dismiss(string id, DateTimeOffset? expectedCreatedAt = null)
    {
        var active = _active.FirstOrDefault(x => x.Id == id);
        if (active is null || (expectedCreatedAt is not null && active.CreatedAt != expectedCreatedAt))
            return false;

        _active.Remove(active);
        ReplaceHistory(active with { IsDismissed = true });
        return true;
    }

    public void MarkAllViewed()
    {
        for (var i = 0; i < _history.Count; i++)
            _history[i] = _history[i] with { IsUnread = false };

        for (var i = 0; i < _active.Count; i++)
            _active[i] = _active[i] with { IsUnread = false };
    }

    public void ClearDismissedHistory()
    {
        var activeIds = _active.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        _history.RemoveAll(x => !activeIds.Contains(x.Id));
    }

    public void RestoreHistory(IEnumerable<AppNoticeEntry>? entries)
    {
        _history.Clear();
        _active.Clear();
        if (entries is null) return;

        foreach (var source in entries
                     .Where(x => x is not null && x.PersistInHistory)
                     .OrderBy(x => x.CreatedAt)
                     .TakeLast(_historyLimit))
        {
            _history.Add(source with
            {
                Key = Clean(source.Key, 128, allowEmpty: true),
                TitleEn = Clean(source.TitleEn, MaximumTitleLength),
                TitleZh = Clean(source.TitleZh, MaximumTitleLength),
                BodyEn = Clean(source.BodyEn, MaximumBodyLength, allowEmpty: true),
                BodyZh = Clean(source.BodyZh, MaximumBodyLength, allowEmpty: true),
                AutoDismissMs = NormalizeAutoDismiss(source.Severity, source.AutoDismissMs),
                IsDismissed = true,
                Actions = null,
                ImagePath = NormalizeImagePath(source.ImagePath),
                ImageAltEn = Clean(source.ImageAltEn, 300, allowEmpty: true),
                ImageAltZh = Clean(source.ImageAltZh, 300, allowEmpty: true),
            });
        }
    }

    public IReadOnlyList<AppNoticeEntry> PersistentHistory()
        => _history.Where(x => x.PersistInHistory).OrderBy(x => x.CreatedAt).ToArray();

    private void TrimActive()
    {
        while (_active.Count > _activeLimit)
        {
            var overflow = _active
                .OrderBy(x => x.AutoDismissMs == 0 ? 1 : 0)
                .ThenBy(x => x.CreatedAt)
                .First();
            _active.Remove(overflow);
            ReplaceHistory(overflow with { IsDismissed = true });
        }
    }

    private void TrimHistory()
    {
        while (_history.Count > _historyLimit)
        {
            var activeIds = _active.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            var removable = _history
                .Where(x => !activeIds.Contains(x.Id))
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefault();
            if (removable is null) break;
            _history.Remove(removable);
        }
    }

    private void ReplaceHistory(AppNoticeEntry entry)
    {
        var index = _history.FindIndex(x => x.Id == entry.Id);
        if (index >= 0) _history[index] = entry;
    }

    private static IReadOnlyList<AppNoticeAction>? NormalizeActions(IReadOnlyList<AppNoticeAction>? actions)
    {
        if (actions is null || actions.Count == 0) return null;
        return actions
            .Where(x => x is not null)
            .Take(MaximumActionCount)
            .Select(x => x with
            {
                LabelEn = Clean(x.LabelEn, 80),
                LabelZh = Clean(x.LabelZh, 80),
                Link = NormalizeLink(x.Link),
            })
            .ToArray();
    }

    private static string? NormalizeLink(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return null;
        if (!Uri.TryCreate(link.Trim(), UriKind.Absolute, out var uri)) return null;
        return uri.Scheme is "http" or "https" ? uri.AbsoluteUri : null;
    }

    private static int NormalizeAutoDismiss(AppNoticeSeverity severity, int? requested)
    {
        if (severity is AppNoticeSeverity.Warning or AppNoticeSeverity.Error) return 0;
        var fallback = severity switch
        {
            AppNoticeSeverity.Success => 5_000,
            AppNoticeSeverity.Progress => 6_000,
            _ => 6_000,
        };
        if (requested is null) return fallback;
        if (severity is AppNoticeSeverity.Informational or AppNoticeSeverity.Success && requested <= 0)
            return fallback;
        return Math.Clamp(requested.Value, 0, MaximumAutoDismissMs);
    }

    private static string Clean(string? value, int maxLength, bool allowEmpty = false)
    {
        var cleaned = (value ?? string.Empty).Replace("\0", string.Empty).Trim();
        if (cleaned.Length > maxLength) cleaned = cleaned[..maxLength];
        if (cleaned.Length == 0 && !allowEmpty) cleaned = "WinForge";
        return cleaned;
    }

    private static string? NormalizeImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var full = Path.GetFullPath(path.Trim());
            return full.Length <= 1024 && File.Exists(full) ? full : null;
        }
        catch { return null; }
    }
}
