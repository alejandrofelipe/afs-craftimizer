using System;
using System.Threading;

namespace Artificer.Utils;

/// <summary>
/// Coordinates one active cancellation generation. <see cref="Begin"/> supersedes the previous
/// generation. After <see cref="Dispose"/>, no generation is current, <see cref="Begin"/> throws,
/// and later <see cref="Cancel"/> or <see cref="Dispose"/> calls are safe no-ops.
/// </summary>
internal sealed class CancellationGeneration : IDisposable
{
    private readonly Lock _gate = new();
    private long _generation;
    private CancellationTokenSource? _current;
    private bool _disposed;

    public (long Generation, CancellationToken Token) Begin()
    {
        CancellationTokenSource? previous;
        CancellationTokenSource current;
        CancellationToken token;
        long generation;

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            generation = checked(_generation + 1);
            current = new CancellationTokenSource();
            token = current.Token;
            previous = _current;
            _generation = generation;
            _current = current;
        }

        CancelAndDispose(previous);
        return (generation, token);
    }

    public bool IsCurrent(long generation)
    {
        lock (_gate)
            return _current is not null && _generation == generation;
    }

    public void Cancel(long generation)
    {
        CancellationTokenSource? cancellation;

        lock (_gate)
        {
            if (_current is null || _generation != generation)
                return;

            cancellation = _current;
            _current = null;
        }

        CancelAndDispose(cancellation);
    }

    public void Dispose()
    {
        CancellationTokenSource? cancellation;

        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            cancellation = _current;
            _current = null;
        }

        CancelAndDispose(cancellation);
    }

    private static void CancelAndDispose(CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
            return;

        try
        {
            cancellation.Cancel();
        }
        finally
        {
            cancellation.Dispose();
        }
    }
}
