using Artificer.Plugin;
using Artificer.Simulator;
using Artificer.Simulator.Actions;
using Artificer.Solver;
using Artificer.Utils;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using ActionType = Artificer.Simulator.Actions.ActionType;
using ClassJob = Artificer.Simulator.ClassJob;
using CSRecipeNote = FFXIVClientStructs.FFXIV.Client.Game.UI.RecipeNote;
using RecipeIngredient2 = Artificer.Utils.CSRecipeNote.RecipeIngredient;

namespace Artificer.Windows;

public sealed unsafe partial class CraftingHelper
{
    public enum CraftableStatus
    {
        OK,
        LockedClassJob,
        WrongClassJob,
        SpecialistRequired,
        RequiredItem,
        RequiredStatus,
        CraftsmanshipTooLow,
        ControlTooLow,
    }

    private CraftableStatus CalculateCraftStatus(Gearsets.GearsetItem[] gearItems)
    {
        if (RecipeData!.ClassJob.GetPlayerLevel() == 0)
            return CraftableStatus.LockedClassJob;

        if (PlayerState.Instance()->CurrentClassJobId != RecipeData.ClassJob.GetClassJobIndex())
            return CraftableStatus.WrongClassJob;

        if (RecipeData.Recipe.IsSpecializationRequired && !CharacterStats!.IsSpecialist)
            return CraftableStatus.SpecialistRequired;

        var itemRequired = RecipeData.Recipe.ItemRequired;
        if (itemRequired.RowId != 0 && itemRequired.IsValid)
        {
            if (!gearItems.Any(i => Gearsets.IsItem(i, itemRequired.RowId)))
                return CraftableStatus.RequiredItem;
        }

        var statusRequired = RecipeData.Recipe.StatusRequired;
        if (statusRequired.RowId != 0 && statusRequired.IsValid)
        {
            if (!Service.Objects.LocalPlayer!.StatusList.Any(s => s.StatusId == statusRequired.RowId))
                return CraftableStatus.RequiredStatus;
        }

        if (RecipeData.Recipe.RequiredCraftsmanship > CharacterStats!.Craftsmanship)
            return CraftableStatus.CraftsmanshipTooLow;

        if (RecipeData.Recipe.RequiredControl > CharacterStats.Control)
            return CraftableStatus.ControlTooLow;

        return CraftableStatus.OK;
    }

    private static (string NpcName, string Territory, Vector2 MapLocation, MapLinkPayload Payload) ResolveLevelData(uint levelRowId)
    {
        var level = LuminaSheets.LevelSheet.GetRow(levelRowId);
        var placeName = level.Territory.Value.PlaceName.Value.Name.ToString();
        var location = WorldToMap2(new(level.X, level.Z), level.Map.Value!);

        return (ResolveNpcResidentName(level.Object.RowId), placeName, location, new(level.Territory.RowId, level.Map.RowId, location.X, location.Y));
    }

    private static Vector2 WorldToMap2(Vector2 worldCoordinates, Lumina.Excel.Sheets.Map map)
    {
        return MapUtil.WorldToMap(worldCoordinates, map.OffsetX, map.OffsetY, map.SizeFactor);
    }

    private static string ResolveNpcResidentName(uint npcRowId)
    {
        return Service.SeStringEvaluator.EvaluateObjStr(ObjectKind.EventNpc, npcRowId);
    }

    private static string GetCoordinatesString(Vector2 pos)
    {
        return $"{pos.X.ToString("0.0", CultureInfo.InvariantCulture)}, {pos.Y.ToString("0.0", CultureInfo.InvariantCulture)}";
    }

    private static int? GetGearsetForJob(ClassJob job)
    {
        var gearsetModule = RaptureGearsetModule.Instance();
        var i = -1;
        foreach (ref var gearset in gearsetModule->Entries)
        {
            i++;

            if (!gearset.Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                continue;
            if (gearset.Id != i)
                continue;
            if (gearset.ClassJob != job.GetClassJobIndex())
                continue;

            return i;
        }
        return null;
    }
}
