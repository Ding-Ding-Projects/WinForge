using System;
using System.Threading;

namespace WinForge.Services;

/// <summary>
/// Owns one cancellation scope for the selected AWS profile and Region. Restarting the scope cancels
/// every continuation from the previous account context so late SDK responses cannot repaint or enable
/// controls for a newly selected identity.
/// </summary>
internal sealed class AwsContextGeneration : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _source;
    private int _generation;

    internal int CurrentGeneration
    {
        get { lock (_sync) return _generation; }
    }

    internal int Restart()
    {
        CancellationTokenSource? previous;
        int generation;
        lock (_sync)
        {
            previous = _source;
            _source = new CancellationTokenSource();
            generation = ++_generation;
        }
        CancelAndDispose(previous);
        return generation;
    }

    internal void Invalidate()
    {
        CancellationTokenSource? previous;
        lock (_sync)
        {
            previous = _source;
            _source = null;
            _generation++;
        }
        CancelAndDispose(previous);
    }

    internal bool IsCurrent(int generation)
    {
        lock (_sync)
            return generation == _generation && _source is not null && !_source.IsCancellationRequested;
    }

    internal bool TryGetToken(int generation, out CancellationToken token)
    {
        lock (_sync)
        {
            if (generation != _generation || _source is null || _source.IsCancellationRequested)
            {
                token = default;
                return false;
            }

            token = _source.Token;
            return true;
        }
    }

    public void Dispose() => Invalidate();

    private static void CancelAndDispose(CancellationTokenSource? source)
    {
        if (source is null) return;
        try { source.Cancel(); } catch { }
        source.Dispose();
    }
}
