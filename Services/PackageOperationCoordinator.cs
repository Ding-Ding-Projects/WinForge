using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>Lifecycle state for one native package operation.</summary>
public enum PackageOperationStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    Skipped,
}

/// <summary>An immutable, UI-safe view of one queued or completed operation.</summary>
public sealed record PackageOperationSnapshot(
    Guid OperationId,
    string ManagerKey,
    string PackageId,
    string PackageName,
    PackageOperations.Op Operation,
    PackageOperationStatus Status,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string OutputTail,
    TweakResult? Result);

/// <summary>Handle returned as soon as an operation enters the queue.</summary>
public sealed record PackageOperationTicket(
    Guid Id,
    Guid? DuplicateOfId,
    Task<PackageOperationSnapshot> Completion);

public sealed class PackageOperationChangedEventArgs : EventArgs
{
    public required long Revision { get; init; }
    public required PackageOperationSnapshot Snapshot { get; init; }
}

/// <summary>
/// Global UniGetUI-style operation coordinator. All install/update/uninstall entry points use this queue,
/// so saved options, ignored updates, bounded concurrency, cancellation, output redaction, notifications,
/// duplicate suppression and history behave identically in rows, batches, bundles and the scheduler.
/// </summary>
public static class PackageOperationCoordinator
{
    private const int HistoryLimit = 200;
    private const int OutputTailLimit = 12_000;
    private static readonly object Gate = new();
    private static readonly Queue<Entry> Pending = new();
    private static readonly Dictionary<Guid, Entry> Entries = new();
    private static readonly Dictionary<string, Guid> LiveKeys = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<Guid> History = new();
    private static int _running;
    private static long _revision;

    private sealed class Entry
    {
        public required Guid Id { get; init; }
        public required PackageItem Item { get; init; }
        public required PackageOperations.Op Operation { get; init; }
        public required InstallOptions Options { get; init; }
        public required CancellationTokenSource Cancellation { get; init; }
        public required TaskCompletionSource<PackageOperationSnapshot> Completion { get; init; }
        public required DateTimeOffset QueuedAt { get; init; }
        public required string LiveKey { get; init; }
        public string RunnerTag { get; init; } = "";
        public Func<IProgress<string>, CancellationToken, Task<TweakResult>>? Runner { get; init; }
        public PackageOperationStatus Status { get; set; } = PackageOperationStatus.Queued;
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public StringBuilder Output { get; } = new();
        public TweakResult? Result { get; set; }
        public bool CountedRunning { get; set; }
        public CancellationTokenRegistration CancellationRegistration { get; set; }
        public DateTimeOffset LastOutputPublishedAt { get; set; }
        public bool OutputPublishScheduled { get; set; }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    public static event EventHandler<PackageOperationChangedEventArgs>? Changed;

    /// <summary>Queue one operation. An equivalent queued/running operation is de-duplicated.</summary>
    public static PackageOperationTicket Enqueue(PackageItem item, PackageOperations.Op operation,
        InstallOptions? options = null, CancellationToken ct = default)
        => EnqueueCore(item, operation, options, ct, runnerTag: "", runner: null);

    /// <summary>Run an app-defined package bootstrap through the same queue and lifecycle as normal managers.</summary>
    public static Task<PackageOperationSnapshot> RunCustomAsync(PackageItem item, string operationTag,
        Func<IProgress<string>, CancellationToken, Task<TweakResult>> runner, CancellationToken ct = default)
    {
        if (runner is null) throw new ArgumentNullException(nameof(runner));
        var tag = Regex.Replace(operationTag ?? "custom", @"[^A-Za-z0-9._-]", "-");
        return EnqueueCore(item, PackageOperations.Op.Install, new InstallOptions(), ct, tag, runner).Completion;
    }

    private static PackageOperationTicket EnqueueCore(PackageItem item, PackageOperations.Op operation,
        InstallOptions? options, CancellationToken ct, string runnerTag,
        Func<IProgress<string>, CancellationToken, Task<TweakResult>>? runner)
    {
        item ??= new PackageItem();
        var copy = CopyItem(item);
        var validation = ValidateItem(copy);
        if (validation is not null)
            return TerminalTicket(copy, operation, PackageOperationStatus.Failed, validation,
                options, runnerTag, runner);

        var effectiveOptions = (options ?? InstallOptions.Load(copy.ManagerKey, copy.Id)).Clone();
        validation = ValidateOptions(effectiveOptions);
        if (validation is not null)
            return TerminalTicket(copy, operation, PackageOperationStatus.Failed, validation,
                effectiveOptions, runnerTag, runner);
        if (ct.IsCancellationRequested)
            return TerminalTicket(copy, operation, PackageOperationStatus.Cancelled,
                TweakResult.Fail("Cancelled before queueing.", "排隊之前已取消。"),
                effectiveOptions, runnerTag, runner);

        lock (Gate)
        {
            var liveKey = Key(copy, operation, effectiveOptions, runnerTag);
            if (LiveKeys.TryGetValue(liveKey, out var existingId)
                && Entries.TryGetValue(existingId, out var existing))
            {
                var duplicateCompletion = ct.CanBeCanceled
                    ? existing.Completion.Task.WaitAsync(ct)
                    : existing.Completion.Task;
                return new PackageOperationTicket(existingId, existingId, duplicateCompletion);
            }

            var entry = new Entry
            {
                Id = Guid.NewGuid(),
                Item = copy,
                Operation = operation,
                Options = effectiveOptions,
                Cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct),
                Completion = new TaskCompletionSource<PackageOperationSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously),
                QueuedAt = DateTimeOffset.Now,
                LiveKey = liveKey,
                RunnerTag = runnerTag,
                Runner = runner,
            };
            Entries[entry.Id] = entry;
            LiveKeys[liveKey] = entry.Id;
            Pending.Enqueue(entry);
            entry.CancellationRegistration = entry.Cancellation.Token.Register(static state =>
            {
                var id = (Guid)state!;
                _ = Task.Run(() => CancelQueuedFromToken(id));
            }, entry.Id);
            var started = PumpLocked();
            PublishLater(entry.Id);
            foreach (var id in started)
                if (id != entry.Id) PublishLater(id);
            return new PackageOperationTicket(entry.Id, null, entry.Completion.Task);
        }
    }

    /// <summary>Queue and await a single operation.</summary>
    public static Task<PackageOperationSnapshot> RunAsync(PackageItem item, PackageOperations.Op operation,
        InstallOptions? options = null, CancellationToken ct = default)
        => Enqueue(item, operation, options, ct).Completion;

    /// <summary>Queue a set of operations. The shared queue enforces the configured global concurrency.</summary>
    public static async Task<IReadOnlyList<PackageOperationSnapshot>> RunManyAsync(
        IEnumerable<PackageItem> items, PackageOperations.Op operation,
        Func<PackageItem, InstallOptions?>? options = null, CancellationToken ct = default)
    {
        var tickets = (items ?? Array.Empty<PackageItem>())
            .Select(item => Enqueue(item, operation, options?.Invoke(item), ct))
            .ToList();
        if (tickets.Count == 0) return Array.Empty<PackageOperationSnapshot>();
        return await Task.WhenAll(tickets.Select(t => t.Completion));
    }

    public static bool Cancel(Guid operationId)
    {
        Entry? cancelledEntry = null;
        PackageOperationSnapshot? terminal = null;
        List<Guid> started = new();
        lock (Gate)
        {
            if (!Entries.TryGetValue(operationId, out var entry)) return false;
            if (IsTerminal(entry.Status)) return false;
            try { entry.Cancellation.Cancel(); } catch { }
            if (entry.Status == PackageOperationStatus.Queued)
            {
                entry.Status = PackageOperationStatus.Cancelled;
                entry.CompletedAt = DateTimeOffset.Now;
                entry.Result = TweakResult.Fail("Cancelled before starting.", "開始之前已取消。");
                LiveKeys.Remove(entry.LiveKey);
                AddHistoryLocked(entry);
                cancelledEntry = entry;
                terminal = SnapshotLocked(entry);
                started = PumpLocked();
            }
        }
        if (terminal is not null)
        {
            cancelledEntry!.Completion.TrySetResult(terminal);
            DisposeCancellation(cancelledEntry);
            Publish(operationId);
        }
        foreach (var id in started) Publish(id);
        return true;
    }

    /// <summary>Requeue a completed operation with the exact package metadata and options it originally used.</summary>
    public static PackageOperationTicket? Retry(Guid operationId, CancellationToken ct = default)
    {
        PackageItem item;
        PackageOperations.Op operation;
        InstallOptions options;
        string runnerTag;
        Func<IProgress<string>, CancellationToken, Task<TweakResult>>? runner;
        lock (Gate)
        {
            if (!Entries.TryGetValue(operationId, out var entry) || !IsTerminal(entry.Status)) return null;
            item = CopyItem(entry.Item);
            operation = entry.Operation;
            options = entry.Options.Clone();
            runnerTag = entry.RunnerTag;
            runner = entry.Runner;
        }
        return EnqueueCore(item, operation, options, ct, runnerTag, runner);
    }

    public static void CancelAll()
    {
        var queued = new List<(Entry Entry, PackageOperationSnapshot Snapshot)>();
        var publishIds = new List<Guid>();
        lock (Gate)
        {
            // Snapshot and transition every current entry before any completion can pump the queue.
            // This prevents a queued operation from starting halfway through a Cancel All pass.
            foreach (var entry in Entries.Values.Where(e => !IsTerminal(e.Status)).ToList())
            {
                try { entry.Cancellation.Cancel(); } catch { }
                publishIds.Add(entry.Id);
                if (entry.Status != PackageOperationStatus.Queued) continue;
                entry.Status = PackageOperationStatus.Cancelled;
                entry.CompletedAt = DateTimeOffset.Now;
                entry.Result = TweakResult.Fail("Cancelled before starting.", "開始之前已取消。");
                LiveKeys.Remove(entry.LiveKey);
                AddHistoryLocked(entry);
                queued.Add((entry, SnapshotLocked(entry)));
            }
        }

        foreach (var (entry, snapshot) in queued)
        {
            entry.Completion.TrySetResult(snapshot);
            DisposeCancellation(entry);
        }
        foreach (var id in publishIds) Publish(id);
    }

    /// <summary>Apply a changed ParallelOperationCount and start newly available queue slots.</summary>
    public static void RefreshConcurrency()
    {
        List<Guid> started;
        lock (Gate) started = PumpLocked();
        foreach (var id in started) Publish(id);
    }

    public static void ClearHistory()
    {
        lock (Gate)
        {
            foreach (var id in History)
                if (Entries.TryGetValue(id, out var entry) && IsTerminal(entry.Status)) Entries.Remove(id);
            History.Clear();
        }
    }

    public static IReadOnlyList<PackageOperationSnapshot> GetActiveSnapshots()
    {
        lock (Gate)
            return Entries.Values.Where(e => !IsTerminal(e.Status))
                .OrderBy(e => e.QueuedAt).Select(SnapshotLocked).ToList();
    }

    public static IReadOnlyList<PackageOperationSnapshot> GetHistory()
    {
        lock (Gate)
            return History.Select(id => Entries.TryGetValue(id, out var e) ? SnapshotLocked(e) : null)
                .Where(s => s is not null).Cast<PackageOperationSnapshot>().ToList();
    }

    public static IReadOnlyList<PackageOperationSnapshot> GetSnapshots()
    {
        var (active, history) = GetSnapshotSet();
        return active.Concat(history).ToList();
    }

    /// <summary>Read active operations and history under one lock so an operation cannot appear in both.</summary>
    public static (IReadOnlyList<PackageOperationSnapshot> Active,
        IReadOnlyList<PackageOperationSnapshot> History) GetSnapshotSet()
    {
        lock (Gate)
        {
            var active = Entries.Values.Where(e => !IsTerminal(e.Status))
                .OrderBy(e => e.QueuedAt).Select(SnapshotLocked).ToList();
            var history = History.Select(id => Entries.TryGetValue(id, out var e) ? SnapshotLocked(e) : null)
                .Where(s => s is not null).Cast<PackageOperationSnapshot>().ToList();
            return (active, history);
        }
    }

    public static bool TryGetSnapshot(Guid operationId, out PackageOperationSnapshot? snapshot)
    {
        lock (Gate)
        {
            if (Entries.TryGetValue(operationId, out var entry))
            {
                snapshot = SnapshotLocked(entry);
                return true;
            }
        }
        snapshot = null;
        return false;
    }

    /// <summary>Strict allow-list used by bundle import and every direct operation entry point.</summary>
    public static bool IsSafePackageId(string? packageId)
    {
        var id = packageId ?? "";
        if (id.Length is 0 or > 256) return false;
        foreach (var c in id)
            // Comma is required by vcpkg feature lists (port[a,b]); tilde is valid in npm package names.
            // Neither character is a command separator in cmd.exe or PowerShell.
            if (!(char.IsLetterOrDigit(c) || c is '.' or '_' or '-' or '+' or '@' or '/' or ':'
                  or '[' or ']' or ',' or '~'))
                return false;
        return true;
    }

    private static TweakResult? ValidateItem(PackageItem item)
    {
        if (PackageManagerRegistry.ByKey(item.ManagerKey) is null)
            return TweakResult.Fail($"Unknown package manager '{item.ManagerKey}'.", $"未知套件管理器「{item.ManagerKey}」。")
                with { Code = "invalid-manager" };
        if (!IsSafePackageId(item.Id))
            return TweakResult.Fail("The package ID contains unsafe characters.", "套件 ID 含有唔安全字元。")
                with { Code = "invalid-package-id" };
        return null;
    }

    private static TweakResult? ValidateOptions(InstallOptions options)
    {
        if (!IsSafeVersion(options.Version))
            return TweakResult.Fail("The requested version contains unsafe characters.", "指定版本含有唔安全字元。")
                with { Code = "invalid-version" };

        var scope = (options.Scope ?? "").Trim().ToLowerInvariant();
        if (scope.Length > 0 && scope is not ("user" or "machine" or "currentuser" or "allusers"))
            return TweakResult.Fail("The requested package scope is invalid.", "指定套件範圍無效。")
                with { Code = "invalid-scope" };

        var architecture = (options.Architecture ?? "").Trim().ToLowerInvariant();
        if (architecture.Length > 0 && architecture is not ("x64" or "x86" or "arm64" or "arm" or "neutral"))
            return TweakResult.Fail("The requested architecture is invalid.", "指定架構無效。")
                with { Code = "invalid-architecture" };

        if (!IsSafeInstallPath(options.CustomInstallLocation))
            return TweakResult.Fail("The custom install location contains unsafe characters.", "自訂安裝位置含有唔安全字元。")
                with { Code = "invalid-install-location" };

        string[] arbitraryText =
        {
            options.CustomArgsInstall ?? "", options.CustomArgsUpdate ?? "", options.CustomArgsUninstall ?? "",
            options.PreInstallCommand ?? "", options.PostInstallCommand ?? "",
            options.PreUpdateCommand ?? "", options.PostUpdateCommand ?? "",
            options.PreUninstallCommand ?? "", options.PostUninstallCommand ?? "",
        };
        if (arbitraryText.Any(value => value.Length > 16_384)
            || arbitraryText.Sum(value => value.Length) > 65_536
            || options.KillBeforeOperation is { Count: > 128 }
            || options.KillBeforeOperation?.Any(value => (value ?? "").Length > 260) == true)
            return TweakResult.Fail("The custom operation options exceed safe size limits.", "自訂操作選項超出安全大小限制。")
                with { Code = "options-too-large" };
        return null;
    }

    private static bool IsSafeVersion(string? value)
    {
        var version = (value ?? "").Trim();
        if (version.Length == 0) return true;
        if (version.Length > 128) return false;
        return version.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-' or '+' or ':' or '!' or '~' or '*');
    }

    private static bool IsSafeInstallPath(string? value)
    {
        var path = (value ?? "").Trim();
        if (path.Length == 0) return true;
        if (path.Length > 1024 || path.Any(char.IsControl)) return false;
        // These characters are expanded or interpreted by cmd.exe even inside a quoted argument.
        return path.IndexOfAny(new[] { '"', '`', '%', '!', '&', '|', '<', '>' }) < 0;
    }

    private static List<Guid> PumpLocked()
    {
        var changed = new List<Guid>();
        int limit = Math.Clamp(PackageManagerSettings.ParallelOperationCount, 1, 10);
        while (_running < limit && Pending.Count > 0)
        {
            int candidates = Pending.Count;
            bool startedAny = false;
            while (_running < limit && candidates-- > 0 && Pending.Count > 0)
            {
            var entry = Pending.Dequeue();
            if (entry.Status != PackageOperationStatus.Queued) continue;
            if (entry.Cancellation.IsCancellationRequested)
            {
                entry.Status = PackageOperationStatus.Cancelled;
                entry.CompletedAt = DateTimeOffset.Now;
                entry.Result = TweakResult.Fail("Cancelled before starting.", "開始之前已取消。");
                LiveKeys.Remove(entry.LiveKey);
                AddHistoryLocked(entry);
                var snap = SnapshotLocked(entry);
                entry.Completion.TrySetResult(snap);
                _ = Task.Run(() => DisposeCancellation(entry));
                changed.Add(entry.Id);
                continue;
            }

            // Different operations/options for one package are distinct requests, but must never execute
            // concurrently (for example, a scheduled update racing a user-requested uninstall).
            if (Entries.Values.Any(e => e.CountedRunning
                    && string.Equals(e.Item.ManagerKey, entry.Item.ManagerKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(e.Item.Id, entry.Item.Id, StringComparison.OrdinalIgnoreCase)))
            {
                Pending.Enqueue(entry);
                continue;
            }

            entry.Status = PackageOperationStatus.Running;
            entry.StartedAt = DateTimeOffset.Now;
            entry.CountedRunning = true;
            _running++;
            changed.Add(entry.Id);
            _ = Task.Run(() => ExecuteAsync(entry));
            startedAny = true;
            }
            if (!startedAny) break;
        }
        return changed;
    }

    private static async Task ExecuteAsync(Entry entry)
    {
        TweakResult result;
        PackageOperationStatus status;
        try
        {
            if (entry.Operation == PackageOperations.Op.Update && IgnoredUpdates.IsIgnored(entry.Item))
            {
                result = TweakResult.Ok("Skipped because this update is ignored or snoozed.",
                    "已略過，因為呢個更新已被忽略或暫停。") with { Code = "ignored-update" };
                status = PackageOperationStatus.Skipped;
            }
            else if (entry.Operation == PackageOperations.Op.Update
                     && entry.Options.SkipMinorUpdates && IsMinorUpdate(entry.Item))
            {
                result = TweakResult.Ok("Skipped by the package's minor-update policy.",
                    "已按套件嘅次版本更新規則略過。") with { Code = "minor-update-skipped" };
                status = PackageOperationStatus.Skipped;
            }
            else
            {
                PackageNotifier.ShowUpgrading(DisplayName(entry.Item), entry.Item.ManagerKey);
                var progress = new InlineProgress<string>(line => AppendOutput(entry.Id, line));
                result = entry.Runner is null
                    ? await PackageOperations.RunAsync(entry.Item.ManagerKey, entry.Item.Id,
                        entry.Operation, entry.Options, progress, entry.Cancellation.Token)
                    : await entry.Runner(progress, entry.Cancellation.Token);
                status = result.Success
                    ? PackageOperationStatus.Succeeded
                    : entry.Cancellation.IsCancellationRequested
                      && !string.Equals(result.Code, ShellRunner.ProcessCleanupTimeoutCode, StringComparison.Ordinal)
                        ? PackageOperationStatus.Cancelled
                        : PackageOperationStatus.Failed;
            }
        }
        catch (OperationCanceledException)
        {
            result = TweakResult.Fail("Cancelled.", "已取消。");
            status = PackageOperationStatus.Cancelled;
        }
        catch (Exception ex)
        {
            result = TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
            status = PackageOperationStatus.Failed;
        }

        string streamedOutput;
        lock (Gate) streamedOutput = entry.Output.ToString();
        var safeMessage = result.Message is null ? null : new LocalizedText(
            Redact(result.Message.En), Redact(result.Message.Zh));
        var safeOutput = string.IsNullOrWhiteSpace(result.Output)
            ? streamedOutput
            : Redact(Tail(result.Output, OutputTailLimit));
        var safeResult = result with { Message = safeMessage, Output = safeOutput };

        PackageOperationSnapshot snapshot;
        List<Guid> started;
        lock (Gate)
        {
            entry.Result = safeResult;
            if (!string.IsNullOrWhiteSpace(result.Output))
                ReplaceOutputTailLocked(entry, safeOutput);
            entry.Status = status;
            entry.CompletedAt = DateTimeOffset.Now;
            if (entry.CountedRunning) { entry.CountedRunning = false; _running = Math.Max(0, _running - 1); }
            LiveKeys.Remove(entry.LiveKey);
            AddHistoryLocked(entry);
            snapshot = SnapshotLocked(entry);
            started = PumpLocked();
        }

        entry.Completion.TrySetResult(snapshot);
        DisposeCancellation(entry);
        if (status == PackageOperationStatus.Succeeded)
            PackageNotifier.ShowSuccess(DisplayName(entry.Item), entry.Item.ManagerKey);
        else if (status == PackageOperationStatus.Failed)
            PackageNotifier.ShowError(DisplayName(entry.Item), entry.Result?.Message?.Primary, entry.Item.ManagerKey);
        Publish(entry.Id);
        foreach (var id in started) Publish(id);
    }

    private static void AppendOutput(Guid id, string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        var safeLine = Redact(Tail(line, OutputTailLimit));
        bool publishNow = false;
        TimeSpan publishDelay = TimeSpan.Zero;
        lock (Gate)
        {
            if (!Entries.TryGetValue(id, out var entry)) return;
            if (IsTerminal(entry.Status)) return;
            if (entry.Output.Length > 0) entry.Output.AppendLine();
            entry.Output.Append(safeLine);
            if (entry.Output.Length > OutputTailLimit)
                entry.Output.Remove(0, entry.Output.Length - OutputTailLimit);
            var now = DateTimeOffset.UtcNow;
            var remaining = TimeSpan.FromMilliseconds(250) - (now - entry.LastOutputPublishedAt);
            if (remaining <= TimeSpan.Zero && !entry.OutputPublishScheduled)
            {
                entry.LastOutputPublishedAt = now;
                publishNow = true;
            }
            else if (!entry.OutputPublishScheduled)
            {
                entry.OutputPublishScheduled = true;
                publishDelay = remaining;
            }
        }
        if (publishNow) Publish(id);
        else if (publishDelay > TimeSpan.Zero) _ = PublishOutputAfterDelayAsync(id, publishDelay);
    }

    private static async Task PublishOutputAfterDelayAsync(Guid id, TimeSpan delay)
    {
        try { await Task.Delay(delay); } catch { }
        bool publish = false;
        lock (Gate)
        {
            if (!Entries.TryGetValue(id, out var entry)) return;
            entry.OutputPublishScheduled = false;
            if (!IsTerminal(entry.Status))
            {
                entry.LastOutputPublishedAt = DateTimeOffset.UtcNow;
                publish = true;
            }
        }
        if (publish) Publish(id);
    }

    private static PackageOperationTicket TerminalTicket(PackageItem item, PackageOperations.Op operation,
        PackageOperationStatus status, TweakResult result, InstallOptions? options = null,
        string runnerTag = "", Func<IProgress<string>, CancellationToken, Task<TweakResult>>? runner = null)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.Now;
        var safeMessage = result.Message is null ? null : new LocalizedText(
            Redact(result.Message.En), Redact(result.Message.Zh));
        var safeResult = result with { Message = safeMessage, Output = Redact(result.Output ?? "") };
        var snapshot = new PackageOperationSnapshot(id, item.ManagerKey, item.Id, DisplayName(item), operation,
            status, now, null, now, safeResult.Output ?? "", safeResult);
        Entry terminalEntry;
        lock (Gate)
        {
            var entry = new Entry
            {
                Id = id, Item = item, Operation = operation, Options = (options ?? new InstallOptions()).Clone(),
                Cancellation = new CancellationTokenSource(),
                Completion = new TaskCompletionSource<PackageOperationSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously),
                QueuedAt = now, LiveKey = $"terminal|{id:N}", Status = status, CompletedAt = now, Result = safeResult,
                RunnerTag = runnerTag, Runner = runner,
            };
            ReplaceOutputTailLocked(entry, safeResult.Output ?? "");
            Entries[id] = entry;
            AddHistoryLocked(entry);
            entry.Completion.TrySetResult(snapshot);
            terminalEntry = entry;
        }
        DisposeCancellation(terminalEntry);
        PublishLater(id);
        return new PackageOperationTicket(id, null, Task.FromResult(snapshot));
    }

    private static void AddHistoryLocked(Entry entry)
    {
        History.Remove(entry.Id);
        History.AddFirst(entry.Id);
        while (History.Count > HistoryLimit)
        {
            var last = History.Last!.Value;
            History.RemoveLast();
            if (Entries.TryGetValue(last, out var old) && IsTerminal(old.Status)) Entries.Remove(last);
        }
    }

    private static void CancelQueuedFromToken(Guid id)
    {
        Entry? entry = null;
        PackageOperationSnapshot? snapshot = null;
        List<Guid> started = new();
        lock (Gate)
        {
            if (!Entries.TryGetValue(id, out var candidate)
                || candidate.Status != PackageOperationStatus.Queued) return;
            candidate.Status = PackageOperationStatus.Cancelled;
            candidate.CompletedAt = DateTimeOffset.Now;
            candidate.Result = TweakResult.Fail("Cancelled before starting.", "開始之前已取消。");
            LiveKeys.Remove(candidate.LiveKey);
            AddHistoryLocked(candidate);
            entry = candidate;
            snapshot = SnapshotLocked(candidate);
            started = PumpLocked();
        }
        entry.Completion.TrySetResult(snapshot);
        DisposeCancellation(entry);
        Publish(id);
        foreach (var startedId in started) Publish(startedId);
    }

    private static void DisposeCancellation(Entry entry)
    {
        try { entry.CancellationRegistration.Dispose(); } catch { }
        try { entry.Cancellation.Dispose(); } catch { }
    }

    private static void ReplaceOutputTailLocked(Entry entry, string output)
    {
        entry.Output.Clear();
        if (string.IsNullOrEmpty(output)) return;
        var start = Math.Max(0, output.Length - OutputTailLimit);
        entry.Output.Append(output.AsSpan(start));
    }

    private static PackageOperationSnapshot SnapshotLocked(Entry entry)
        => new(entry.Id, entry.Item.ManagerKey, entry.Item.Id, DisplayName(entry.Item), entry.Operation,
            entry.Status, entry.QueuedAt, entry.StartedAt, entry.CompletedAt,
            entry.Output.ToString(), entry.Result);

    private static void PublishLater(Guid id)
        => _ = Task.Run(() => Publish(id));

    private static void Publish(Guid id)
    {
        PackageOperationSnapshot? snapshot;
        long revision;
        lock (Gate)
        {
            if (!Entries.TryGetValue(id, out var entry)) return;
            snapshot = SnapshotLocked(entry);
            revision = ++_revision;
        }
        try { Changed?.Invoke(null, new PackageOperationChangedEventArgs { Revision = revision, Snapshot = snapshot }); }
        catch { /* subscribers cannot break the queue */ }
    }

    private static bool IsTerminal(PackageOperationStatus status)
        => status is PackageOperationStatus.Succeeded or PackageOperationStatus.Failed
            or PackageOperationStatus.Cancelled or PackageOperationStatus.Skipped;

    private static string Key(PackageItem item, PackageOperations.Op operation, InstallOptions options, string runnerTag)
    {
        var updateVersions = operation == PackageOperations.Op.Update
            ? $"|{item.Version.Length}:{item.Version}|{item.AvailableVersion.Length}:{item.AvailableVersion}"
            : "";
        return $"{item.ManagerKey}|{item.Id}|{operation}{updateVersions}|{runnerTag}|{OptionsFingerprint(options)}";
    }

    private static string OptionsFingerprint(InstallOptions options)
    {
        try
        {
            var json = JsonSerializer.Serialize(options);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        }
        catch
        {
            // Failing open here means no de-duplication for this request, never using another request's options.
            return Guid.NewGuid().ToString("N");
        }
    }

    private static string DisplayName(PackageItem item)
        => string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;

    private static PackageItem CopyItem(PackageItem item) => new()
    {
        Name = item.Name ?? "",
        Id = item.Id ?? "",
        Version = item.Version ?? "",
        AvailableVersion = item.AvailableVersion ?? "",
        Source = item.Source ?? "",
        ManagerKey = item.ManagerKey ?? "",
    };

    /// <summary>Whether a package's saved/effective policy hides this patch-level update.</summary>
    public static bool IsMinorUpdateSuppressed(PackageItem item, InstallOptions? options = null)
    {
        try
        {
            var effective = options ?? InstallOptions.Load(item.ManagerKey, item.Id);
            return effective.SkipMinorUpdates && IsMinorUpdate(item);
        }
        catch { return false; }
    }

    private static bool IsMinorUpdate(PackageItem item)
    {
        if (!TryNormalizedVersion(item.Version, out var current)
            || !TryNormalizedVersion(item.AvailableVersion, out var available)) return false;
        return current.Major == available.Major && current.Minor == available.Minor
            && (current.Patch != available.Patch || current.Remainder != available.Remainder);
    }

    private static bool TryNormalizedVersion(string? value,
        out (int Major, int Minor, int Patch, int Remainder) version)
    {
        version = default;
        var text = (value ?? "").Trim();
        if (text.Length == 0) return false;
        char[] separators = { '.', '-', '/', '#' };
        foreach (var segment in text.Split(separators, StringSplitOptions.RemoveEmptyEntries))
        {
            bool seenDigit = false, seenLetterAfterDigit = false, seenDigitAfterLetter = false;
            foreach (var c in segment)
            {
                if (char.IsDigit(c))
                {
                    if (seenLetterAfterDigit) seenDigitAfterLetter = true;
                    seenDigit = true;
                }
                else if (char.IsLetter(c) && seenDigit) seenLetterAfterDigit = true;
            }
            if (seenDigit && seenLetterAfterDigit && seenDigitAfterLetter) return false;
        }

        var parts = new StringBuilder[] { new(), new(), new(), new() };
        int index = 0;
        bool first = true;
        foreach (var c in text)
        {
            if (char.IsDigit(c)) parts[index].Append(c);
            else if (!first && separators.Contains(c) && index < 3) index++;
            first = false;
        }
        var numbers = new int[4];
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0 && !int.TryParse(parts[i].ToString(), out numbers[i])) return false;
        version = (numbers[0], numbers[1], numbers[2], numbers[3]);
        return parts.Any(p => p.Length > 0);
    }

    private static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var redacted = Tail(value, OutputTailLimit);
        try
        {
            var password = PackageManagerSettings.ProxyPassword;
            if (!string.IsNullOrEmpty(password)) redacted = redacted.Replace(password, "<redacted>", StringComparison.Ordinal);
            redacted = Regex.Replace(redacted,
                @"(?i)(https?://)[^\s/@:]+:[^\s/@]+@", "$1<redacted>@",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            redacted = Regex.Replace(redacted,
                @"(?i)(--(?:password|token|api[-_]?key)\s+)(?:""[^""]*""|'[^']*'|\S+)", "$1<redacted>",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            redacted = Regex.Replace(redacted,
                @"(?i)((?:--|/)(?:password|pass|token|api[-_]?key|secret)(?:=|:))(?:""[^""]*""|'[^']*'|[^\s]+)", "$1<redacted>",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            redacted = Regex.Replace(redacted,
                @"(?i)((?:token|secret|_authToken|authToken|accessToken|refreshToken|password|api[-_]?key|client[-_]?secret)\s*[=:]\s*)(?:""[^""]*""|'[^']*'|[^\s;]+)", "$1<redacted>",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            redacted = Regex.Replace(redacted,
                @"(?i)(Authorization\s*:\s*(?:Bearer|Basic)\s+)\S+", "$1<redacted>",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
        catch { return "<redaction-failed>"; }
        return Tail(redacted, OutputTailLimit);
    }

    private static string Tail(string? value, int maxLength)
    {
        var text = value ?? "";
        if (text.Length <= maxLength) return text;
        return text[^maxLength..];
    }
}
