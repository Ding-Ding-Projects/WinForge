using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("informational notices auto-dismiss", InformationalDuration);
Run("success notices auto-dismiss", SuccessDuration);
Run("errors persist until dismissed", ErrorPersists);
Run("warnings persist until dismissed", WarningPersists);
Run("progress may remain active until completion", PersistentProgress);
Run("channel replacement is stable and deduplicated", ChannelReplacement);
Run("active stack is bounded", ActiveBound);
Run("history is bounded", HistoryBound);
Run("dismissed notices stay reviewable", DismissedRetained);
Run("stale auto-dismiss cannot close a replacement", StaleTimerGuard);
Run("viewing clears unread state", ViewedState);
Run("clear history retains active notices", ClearRetainsActive);
Run("restored history never reopens old notices", RestoreIsHistoryOnly);
Run("text and actions are bounded", InputBounds);
Run("only safe web links survive normalization", SafeLinks);
Run("non-persistent notices stay out of durable snapshots", PersistenceOptOut);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} notification-centre contract tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} notification-centre contract tests");
return 1;

void Run(string name, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL {name}: {ex.Message}");
    }
}

static AppNoticeDraft Draft(
    string title = "Title",
    AppNoticeSeverity severity = AppNoticeSeverity.Informational,
    string? key = null,
    int? duration = null,
    bool persist = true,
    IReadOnlyList<AppNoticeAction>? actions = null)
    => new(title, "標題", "Body", "內容", severity, key, duration, persist, actions);

static void InformationalDuration()
{
    var entry = new NotificationCenterState().Publish(Draft());
    Assert(entry.AutoDismissMs == 6_000, "unexpected informational duration");
}

static void SuccessDuration()
{
    var entry = new NotificationCenterState().Publish(Draft(severity: AppNoticeSeverity.Success));
    Assert(entry.AutoDismissMs == 5_000, "unexpected success duration");
}

static void ErrorPersists()
{
    var entry = new NotificationCenterState().Publish(Draft(
        severity: AppNoticeSeverity.Error,
        duration: 1_000));
    Assert(entry.AutoDismissMs == 0, "error accepted auto-dismiss");
}

static void WarningPersists()
{
    var entry = new NotificationCenterState().Publish(Draft(
        severity: AppNoticeSeverity.Warning,
        duration: 1_000));
    Assert(entry.AutoDismissMs == 0, "warning accepted auto-dismiss");
}

static void PersistentProgress()
{
    var entry = new NotificationCenterState().Publish(Draft(
        severity: AppNoticeSeverity.Progress,
        duration: 0));
    Assert(entry.AutoDismissMs == 0, "persistent progress duration changed");
}

static void ChannelReplacement()
{
    var state = new NotificationCenterState();
    var first = state.Publish(Draft(title: "Starting", key: "update"), DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
    var replacement = state.Publish(Draft(title: "Complete", key: "update"), DateTimeOffset.Parse("2026-01-01T00:00:01Z"));
    Assert(first.Id == replacement.Id, "replacement changed stable id");
    Assert(state.Active.Count == 1 && state.History.Count == 1, "replacement duplicated state");
    Assert(state.Active[0].TitleEn == "Complete", "replacement did not update content");
}

static void ActiveBound()
{
    var state = new NotificationCenterState(activeLimit: 2);
    for (var i = 0; i < 4; i++)
        state.Publish(Draft(title: $"N{i}", severity: AppNoticeSeverity.Error), DateTimeOffset.UnixEpoch.AddSeconds(i));
    Assert(state.Active.Count == 2, "active stack exceeded limit");
    Assert(state.History.Count == 4, "overflow notices disappeared from history");
    Assert(state.History.Count(x => x.IsDismissed) == 2, "overflow notices were not marked dismissed");
}

static void HistoryBound()
{
    var state = new NotificationCenterState(historyLimit: 3, activeLimit: 1);
    for (var i = 0; i < 8; i++)
        state.Publish(Draft(title: $"N{i}"), DateTimeOffset.UnixEpoch.AddSeconds(i));
    Assert(state.History.Count == 3, "history exceeded limit");
    Assert(state.History[0].TitleEn == "N7", "newest history item was not retained");
}

static void DismissedRetained()
{
    var state = new NotificationCenterState();
    var entry = state.Publish(Draft());
    Assert(state.Dismiss(entry.Id), "dismiss failed");
    Assert(state.Active.Count == 0, "dismissed notice remains active");
    Assert(state.History.Single().IsDismissed, "dismissed notice not retained");
}

static void StaleTimerGuard()
{
    var state = new NotificationCenterState();
    var firstAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    var replacementAt = firstAt.AddSeconds(1);
    var first = state.Publish(Draft(key: "same"), firstAt);
    state.Publish(Draft(title: "New", key: "same"), replacementAt);
    Assert(!state.Dismiss(first.Id, firstAt), "stale timer dismissed replacement");
    Assert(state.Active.Single().CreatedAt == replacementAt, "replacement disappeared");
}

static void ViewedState()
{
    var state = new NotificationCenterState();
    state.Publish(Draft());
    Assert(state.UnreadCount == 1, "new notice was not unread");
    state.MarkAllViewed();
    Assert(state.UnreadCount == 0 && !state.History.Single().IsUnread, "viewed state not applied");
}

static void ClearRetainsActive()
{
    var state = new NotificationCenterState();
    var dismissed = state.Publish(Draft(title: "Old"));
    state.Dismiss(dismissed.Id);
    state.Publish(Draft(title: "Active"));
    state.ClearDismissedHistory();
    Assert(state.History.Count == 1 && state.History[0].TitleEn == "Active", "clear removed active or kept dismissed");
}

static void RestoreIsHistoryOnly()
{
    var source = new NotificationCenterState();
    source.Publish(Draft(title: "Before restart"));
    var restored = new NotificationCenterState();
    restored.RestoreHistory(source.PersistentHistory());
    Assert(restored.Active.Count == 0, "restored notice reopened");
    Assert(restored.History.Count == 1 && restored.History[0].IsDismissed, "restored history is wrong");
}

static void InputBounds()
{
    var actions = Enumerable.Range(0, 8)
        .Select(i => new AppNoticeAction(new string('A', 100), "動作"))
        .ToArray();
    var state = new NotificationCenterState();
    var entry = state.Publish(new AppNoticeDraft(
        new string('T', 220) + "\0",
        "標題",
        new string('B', 2_200),
        "內容",
        Actions: actions));
    Assert(entry.TitleEn.Length == NotificationCenterState.MaximumTitleLength, "title was not bounded");
    Assert(entry.BodyEn.Length == NotificationCenterState.MaximumBodyLength, "body was not bounded");
    Assert(entry.Actions?.Count == NotificationCenterState.MaximumActionCount, "actions were not bounded");
    Assert(!entry.TitleEn.Contains('\0'), "NUL survived normalization");
}

static void SafeLinks()
{
    var state = new NotificationCenterState();
    var entry = state.Publish(Draft(actions:
    [
        new("Unsafe", "唔安全", "file:///C:/secret"),
        new("Safe", "安全", "https://example.com/path"),
    ]));
    Assert(entry.Actions![0].Link is null, "unsafe link survived");
    Assert(entry.Actions[1].Link == "https://example.com/path", "safe HTTPS link changed");
}

static void PersistenceOptOut()
{
    var state = new NotificationCenterState();
    state.Publish(Draft(title: "Private", persist: false));
    state.Publish(Draft(title: "Durable"));
    Assert(state.History.Count == 2, "in-memory history missing notice");
    Assert(state.PersistentHistory().Count == 1, "opt-out notice entered persistence snapshot");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
