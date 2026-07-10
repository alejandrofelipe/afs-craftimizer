using System.Collections.Generic;
using System.Linq;

namespace Artificer.Utils;

public enum CraftStat { Craftsmanship, Control, CP }

public readonly record struct MeldMatch(CraftStat Stat, int Grade, bool Satisfied, bool IsOvermeld);

public static class MeldCompare
{
    /// <summary>
    /// Compara os melds atuais de uma peça contra o target (ordenado, guaranteed-first).
    /// Match tolerante a grade: um alvo é satisfeito por um meld atual da mesma stat com grade >= alvo.
    /// Matching ótimo: cada alvo (processado por grade desc) consome o menor meld atual suficiente.
    /// </summary>
    public static IReadOnlyList<MeldMatch> CompareMelds(
        IReadOnlyList<(CraftStat Stat, int Grade)> target,
        int guaranteedCount,
        IReadOnlyList<(CraftStat Stat, int Grade)> current)
    {
        var used      = new bool[current.Count];
        var satisfied = new bool[target.Count];

        foreach (var ti in Enumerable.Range(0, target.Count).OrderByDescending(i => target[i].Grade))
        {
            var t = target[ti];
            var best = -1;
            for (var ci = 0; ci < current.Count; ci++)
            {
                if (used[ci] || current[ci].Stat != t.Stat || current[ci].Grade < t.Grade)
                    continue;
                if (best == -1 || current[ci].Grade < current[best].Grade)
                    best = ci;
            }
            if (best != -1)
            {
                used[best]    = true;
                satisfied[ti] = true;
            }
        }

        var result = new List<MeldMatch>(target.Count);
        for (var ti = 0; ti < target.Count; ti++)
            result.Add(new MeldMatch(target[ti].Stat, target[ti].Grade, satisfied[ti], ti >= guaranteedCount));
        return result;
    }
}
