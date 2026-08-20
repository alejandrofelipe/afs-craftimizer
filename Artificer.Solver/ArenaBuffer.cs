using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Artificer.Solver;

public struct ArenaBuffer
{
    // Capacidade inicial dos buffers de filhos/scores; eles crescem além disso sob demanda
    // (ver ArenaBuffer<T>.EnsureCapacityForNext / NodeScoresBuffer.EnsureCapacityForNext).
    // O pool completo de ações (StrictActions=false) pode exceder este valor.
    internal const int InitialCapacity = 32;

    internal const int BatchSize = 8;
    internal const int BatchSizeBits = 3; // int.Log2(BatchSize);
    internal const int BatchSizeMask = BatchSize - 1;

    internal const int InitialBatchCount = InitialCapacity / BatchSize;
}

// Adapted from https://github.com/dtao/ConcurrentList/blob/4fcf1c76e93021a41af5abb2d61a63caeba2adad/ConcurrentList/ConcurrentList.cs
public struct ArenaBuffer<T> where T : struct
{
    public ArenaNode<T>[][] Data;
    public int Count { get; private set; }

    // Garante que o slot do próximo Add (índice atual Count) exista: aloca Data na primeira vez,
    // cresce o array externo geometricamente quando o batch necessário passa da capacidade atual,
    // e aloca o batch interno. Idempotente; chamado antes de qualquer incremento de Count, de modo
    // que uma falha de alocação nunca deixa Count parcialmente incrementado.
    internal void EnsureCapacityForNext()
    {
        var (arrayIdx, _) = GetArrayIndex(Count);
        Data ??= new ArenaNode<T>[ArenaBuffer.InitialBatchCount][];

        if (arrayIdx >= Data.Length)
        {
            var required = checked(arrayIdx + 1);
            var doubled = checked(Data.Length * 2);
            Array.Resize(ref Data, Math.Max(required, doubled));
        }

        Data[arrayIdx] ??= new ArenaNode<T>[ArenaBuffer.BatchSize];
    }

    public void Add(ArenaNode<T> node)
    {
        EnsureCapacityForNext();

        var (arrayIdx, subIdx) = GetArrayIndex(Count);
        node.ChildIdx = (arrayIdx, subIdx);
        Data[arrayIdx][subIdx] = node;
        Count++;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int arrayIdx, int subIdx) GetArrayIndex(int idx) =>
        (idx >> ArenaBuffer.BatchSizeBits, idx & ArenaBuffer.BatchSizeMask);
}
