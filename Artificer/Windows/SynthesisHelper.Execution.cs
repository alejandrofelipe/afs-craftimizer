using Artificer.Application.Crafting;
using Artificer.Plugin;
using Artificer.Simulator;
using Artificer.Simulator.Actions;
using Artificer.Utils;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using ActionType = Artificer.Simulator.Actions.ActionType;
using Sim = Artificer.Simulator.Simulator;
using SimNoRandom = Artificer.Simulator.SimulatorNoRandom;

namespace Artificer.Windows;

public sealed unsafe partial class SynthesisHelper
{
    public bool ExecuteNextAction()
    {
        var canExecute = !Service.Condition[ConditionFlag.ExecutingCraftingAction];
        var action = NextAction;
        if (canExecute && action != null)
        {
            Chat.SendMessage($"/ac \"{action.Value.GetName(Session.RecipeData!.ClassJob)}\"");
            return true;
        }
        return false;
    }

    public void AttemptRetry()
    {
        if (!Session.SolverRunning)
            Session.RequestSolve();
    }

    private static CharacterStats ComputeCharacterStats(RecipeData recipeData) =>
        Gearsets.TryComputeCurrentStats(recipeData.ClassJob.GetPlayerLevel(), recipeData.ClassJob.CanPlayerUseManipulation())?.Stats
            ?? throw new InvalidOperationException("Could not get inventory container");

    private void OnUseAction(ActionType action)
    {
        Addon = (AddonSynthesis*)Service.GameGui.GetAddonByName("Synthesis").Address;
        if (Addon == null)
            return;
        if (Addon->AtkUnitBase.WindowNode == null)
            return;
        if (Session.SimulationInput == null) // not ready: plugin loaded during active synthesis before StartCrafting runs
            return;

        Session.RegisterActionUsed(action, GetCurrentState());
    }

    private void RefreshCurrentState() =>
        Session.SetCurrentState(GetCurrentState(), ShouldCalculate);

    private SimulationState GetCurrentState()
    {
        var player = Service.Objects.LocalPlayer!;
        var values = new SynthesisValues(Addon);
        var statusManager = ((Character*)player.Address)->GetStatusManager();

        byte GetEffectStack(ushort id)
        {
            foreach (var status in statusManager->Status)
                if (status.StatusId == id)
                    return (byte)status.Param;
            return 0;
        }
        bool HasEffect(ushort id)
        {
            foreach (var status in statusManager->Status)
                if (status.StatusId == id)
                    return true;
            return false;
        }

        return new(Session.SimulationInput!)
        {
            ActionCount = Session.CurrentActionCount,
            StepCount = (int)values.StepCount - 1,
            Progress = (int)values.Progress,
            Quality = (int)values.Quality,
            Durability = (int)values.Durability,
            CP = (int)player.CurrentCp,
            Condition = values.Condition,
            ActiveEffects = new()
            {
                InnerQuiet = GetEffectStack((ushort)EffectType.InnerQuiet.StatusId()),
                WasteNot = GetEffectStack((ushort)EffectType.WasteNot.StatusId()),
                Veneration = GetEffectStack((ushort)EffectType.Veneration.StatusId()),
                GreatStrides = GetEffectStack((ushort)EffectType.GreatStrides.StatusId()),
                Innovation = GetEffectStack((ushort)EffectType.Innovation.StatusId()),
                FinalAppraisal = GetEffectStack((ushort)EffectType.FinalAppraisal.StatusId()),
                WasteNot2 = GetEffectStack((ushort)EffectType.WasteNot2.StatusId()),
                MuscleMemory = GetEffectStack((ushort)EffectType.MuscleMemory.StatusId()),
                Manipulation = GetEffectStack((ushort)EffectType.Manipulation.StatusId()),
                Expedience = GetEffectStack((ushort)EffectType.Expedience.StatusId()),
                TrainedPerfection = HasEffect((ushort)EffectType.TrainedPerfection.StatusId()),
                HeartAndSoul = HasEffect((ushort)EffectType.HeartAndSoul.StatusId()),
            },
            ActionStates = Session.CurrentActionStates
        };
    }

    private Sim CreateSim(in SimulationState state) =>
        _plugin.Configuration.ConditionRandomness ? new Sim() { State = state } : new SimNoRandom() { State = state };
}
