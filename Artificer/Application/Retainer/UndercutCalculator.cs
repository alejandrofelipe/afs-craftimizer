using System;

namespace Artificer.Application.Retainer;

/// <summary>Cálculo puro do preço de undercut a partir do menor preço do mercado.</summary>
public static class UndercutCalculator
{
    public static int Compute(int lowestPrice, UndercutMode mode, int amount, bool lowestIsOwnRetainer, bool undercutSelf, int floor = 1)
    {
        if (floor < 1) floor = 1;
        if (lowestPrice <= 0)
            return floor;

        int target;
        if (lowestIsOwnRetainer && !undercutSelf)
            target = lowestPrice; // não competir consigo mesmo — iguala
        else if (mode == UndercutMode.Percentage)
            target = lowestPrice * (100 - amount) / 100;
        else
            target = lowestPrice - amount;

        return Math.Max(floor, target);
    }
}
