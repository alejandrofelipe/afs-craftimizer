using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Artificer.Solver;

public struct NodeScoresBuffer
{
    [StructLayout(LayoutKind.Auto)]
    public struct ScoresBatch
    {
        public Vector256<float> ScoreSum;
        public Vector256<float> MaxScore;
        public Vector256<int> Visits;
    }

    public ScoresBatch[]? Data;
    public int Count { get; private set; }

    // Garante o batch do próximo Add (índice atual Count), crescendo o array geometricamente.
    // Idempotente; chamado antes de incrementar Count para manter o par filhos/scores alinhado
    // mesmo se uma alocação falhar.
    internal void EnsureCapacityForNext()
    {
        var arrayIdx = Count >> ArenaBuffer.BatchSizeBits;
        Data ??= new ScoresBatch[ArenaBuffer.InitialBatchCount];

        if (arrayIdx >= Data.Length)
        {
            var required = checked(arrayIdx + 1);
            var doubled = checked(Data.Length * 2);
            Array.Resize(ref Data, Math.Max(required, doubled));
        }
    }

    public void Add()
    {
        EnsureCapacityForNext();
        var count = Count;
        if ((count & ArenaBuffer.BatchSizeMask) == 0)
            Data![count >> ArenaBuffer.BatchSizeBits] = new();
        Count++;
    }

    public readonly void Visit((int arrayIdx, int subIdx) at, float score)
    {
        ref var batch = ref Data![at.arrayIdx];
        batch.ScoreSum.At(at.subIdx) += score;
        ref var maxScore = ref batch.MaxScore.At(at.subIdx);
        maxScore = Math.Max(maxScore, score);
        batch.Visits.At(at.subIdx)++;
    }

    public readonly int GetVisits((int arrayIdx, int subIdx) at) =>
        Data![at.arrayIdx].Visits[at.subIdx];
}

internal static class VectorUtils
{
    public static ref T At<T>(this ref Vector256<T> me, int idx) =>
        ref Unsafe.Add(ref Unsafe.As<Vector256<T>, T>(ref me), idx);
}
