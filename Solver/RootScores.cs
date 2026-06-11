using System.Runtime.InteropServices;

namespace Artificer.Solver;

[StructLayout(LayoutKind.Auto)]
public sealed class RootScores
{
    public float MaxScore;
    public int Visits;

    public void Visit(float score)
    {
        MaxScore = Math.Max(MaxScore, score);
        Visits++;
    }
}
