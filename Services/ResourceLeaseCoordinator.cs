using System;

namespace WinForge.Services;

/// <summary>
/// Owns one process-local resource while one or more callers hold leases.
/// 所有 caller 都釋放 lease 之後先會 close 個 resource，可以避免平行頁面喺其他 lease 仲用緊時提早清理。
/// </summary>
/// <remarks>
/// This deliberately models ownership of an in-process handle, not a machine-wide service. The
/// caller supplies the narrow close action for the exact resource it opened; no service-control,
/// registry, or file-system cleanup is performed here.
/// </remarks>
internal sealed class ResourceLeaseCoordinator<T> where T : class
{
    private readonly object _gate = new();
    private readonly Func<T> _create;
    private readonly Action<T> _open;
    private readonly Action<T> _close;
    private T? _resource;
    private int _leases;
    private bool _shutdown;

    public ResourceLeaseCoordinator(Func<T> create, Action<T> open, Action<T> close)
    {
        _create = create ?? throw new ArgumentNullException(nameof(create));
        _open = open ?? throw new ArgumentNullException(nameof(open));
        _close = close ?? throw new ArgumentNullException(nameof(close));
    }

    /// <summary>Acquire the shared resource, or return null when opening it failed or shutdown began.</summary>
    public Lease? Acquire()
    {
        lock (_gate)
        {
            if (_shutdown) return null;

            if (_resource is null)
            {
                T? candidate = null;
                try
                {
                    candidate = _create();
                    _open(candidate);
                    _resource = candidate;
                }
                catch
                {
                    // An Open() failure can occur after a driver/handle was partially created. Close
                    // that exact candidate immediately so a failed attempt cannot strand it.
                    if (candidate is not null) CloseNoThrow(candidate);
                    return null;
                }
            }

            _leases++;
            return new Lease(this, _resource);
        }
    }

    /// <summary>
    /// Close the currently owned resource even if callers forgot to release their leases. This is for
    /// deterministic process shutdown only; a coordinator cannot be reopened afterwards.
    /// </summary>
    public void Shutdown()
    {
        lock (_gate)
        {
            if (_shutdown) return;
            _shutdown = true;
            _leases = 0;
            CloseCurrentNoThrow();
        }
    }

    private void Release(Lease lease)
    {
        lock (_gate)
        {
            if (lease.Released) return;
            lease.Released = true;

            // Shutdown already detached the resource, so a stale lease has nothing left to close.
            if (!ReferenceEquals(_resource, lease.Resource)) return;
            if (_leases > 0) _leases--;
            if (_leases == 0) CloseCurrentNoThrow();
        }
    }

    private bool TryUse(Lease lease, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_gate)
        {
            if (_shutdown || lease.Released || !ReferenceEquals(_resource, lease.Resource)) return false;
            action(lease.Resource);
            return true;
        }
    }

    private void CloseCurrentNoThrow()
    {
        var resource = _resource;
        _resource = null;
        if (resource is not null) CloseNoThrow(resource);
    }

    private void CloseNoThrow(T resource)
    {
        try { _close(resource); }
        catch { /* shutdown/failed-open cleanup must never throw past the owner */ }
    }

    /// <summary>A single caller's idempotent claim on the coordinated resource.</summary>
    public sealed class Lease : IDisposable
    {
        private readonly ResourceLeaseCoordinator<T> _owner;
        internal readonly T Resource;
        internal bool Released;

        internal Lease(ResourceLeaseCoordinator<T> owner, T resource)
        {
            _owner = owner;
            Resource = resource;
        }

        /// <summary>Use the resource only while this lease and the coordinator are still active.</summary>
        public bool TryUse(Action<T> action) => _owner.TryUse(this, action);

        /// <summary>Release this caller's lease. The final release closes the owned resource.</summary>
        public void Dispose() => _owner.Release(this);
    }
}
