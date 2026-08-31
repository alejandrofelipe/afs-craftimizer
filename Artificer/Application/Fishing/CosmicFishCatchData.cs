// Dados de captura derivados do GatherBuddy (Apache License 2.0)
// https://github.com/Ottermandias/GatherBuddy — GatherBuddy.GameData/Data/Fish/Data7.2.cs..Data7.5.cs
// Extraído em 2026-08-31 (referência: patch 7.5). Manter via diff desses arquivos a cada patch Cosmic.
using System;
using System.Collections.Generic;

namespace Artificer.Application.Fishing;

public enum FishTug : byte { Weak, Strong, Legendary }
public enum FishHookset : byte { Regular, Precise, Powerful, Stellar }
public enum FishLure : byte { None, Modest, Ambitious }

public sealed record FishCatchInfo(
    uint BaitItemId,
    uint[] MoochChain,
    FishTug Tug,
    FishHookset Hookset,
    byte MultiHook = 0,
    FishLure Lure = FishLure.None,
    (uint FishId, int Count)[]? Predators = null);

public static class CosmicFishFormat
{
    public static string TugText(FishTug tug) => tug switch
    {
        FishTug.Weak => "!",
        FishTug.Strong => "!!",
        FishTug.Legendary => "!!!",
        _ => "?",
    };

    public static string HooksetName(FishHookset h) => h switch
    {
        FishHookset.Regular => "Hook",
        FishHookset.Precise => "Precision Hookset",
        FishHookset.Powerful => "Powerful Hookset",
        FishHookset.Stellar => "Stellar Hookset",
        _ => "?",
    };
}

public static partial class CosmicFishCatchData
{
    /// <summary>Catch data por item id do peixe. Preenchido em CosmicFishCatchData.Entries.cs (Task 2).</summary>
    public static IReadOnlyDictionary<uint, FishCatchInfo> Entries => _entries;
    private static readonly Dictionary<uint, FishCatchInfo> _entries = BuildEntries();
    private static partial Dictionary<uint, FishCatchInfo> BuildEntries();
}
