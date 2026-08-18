using System.Threading;

namespace Artificer.Utils;

internal sealed class GenerationSlot<T> where T : class
{
    private readonly Lock _gate = new();
    private long _generation;
    private T? _value;

    public T? Value
    {
        get
        {
            lock (_gate)
                return _value;
        }
    }

    public long Begin()
    {
        lock (_gate)
        {
            _value = null;
            return ++_generation;
        }
    }

    public bool TryPublish(long generation, T value)
    {
        lock (_gate)
        {
            if (_generation != generation)
                return false;

            _value = value;
            return true;
        }
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _value = null;
            _generation++;
        }
    }
}
