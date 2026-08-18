using System.Threading;

namespace Artificer.Utils;

internal sealed class ExecutionGeneration
{
    private long _current;

    public long Current => Interlocked.Read(ref _current);

    public long Next() => Interlocked.Increment(ref _current);

    public bool IsCurrent(long generation) => Current == generation;

    public bool TryInvalidate(long generation) =>
        Interlocked.CompareExchange(ref _current, generation + 1, generation) == generation;
}
