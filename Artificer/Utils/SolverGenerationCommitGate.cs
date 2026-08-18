using System;
using System.Threading;

namespace Artificer.Utils;

internal sealed class SolverGenerationCommitGate
{
    private readonly Lock _gate = new();
    private long _generation;
    private bool _hasCurrentGeneration;

    public long Begin(Action reset, Func<long> beginGeneration)
    {
        var generation = beginGeneration();

        lock (_gate)
        {
            _hasCurrentGeneration = false;
            reset();
            _generation = generation;
            _hasCurrentGeneration = true;
        }

        return generation;
    }

    public void Invalidate(Action afterInvalidation)
    {
        lock (_gate)
        {
            _hasCurrentGeneration = false;
            afterInvalidation();
        }
    }

    public bool TryCommit(long generation, Func<bool> commit)
    {
        var result = false;
        Action mutation = () => result = commit();
        return TryCommit(generation, mutation) && result;
    }

    public bool TryCommit(long generation, Action commit)
    {
        lock (_gate)
        {
            if (!_hasCurrentGeneration || _generation != generation)
                return false;

            commit();
            return true;
        }
    }
}
