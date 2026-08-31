using System.Collections.Generic;

namespace Artificer.Application.Fishing;

public static partial class CosmicFishCatchData
{
    private static partial Dictionary<uint, FishCatchInfo> BuildEntries() => new();
}
