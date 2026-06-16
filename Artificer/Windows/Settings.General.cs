using Artificer.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Linq;
using System.Numerics;
using Configuration = Artificer.Plugin.Configuration;
using MacroCopyConfiguration = Artificer.Plugin.MacroCopyConfiguration;
using Service = Artificer.Plugin.Service;

namespace Artificer.Windows;

public sealed partial class Settings
{
    private void DrawTabGeneral()
    {
        using var tab = ImRaii.TabItem("General", ConsumeSelectedTab("General"));
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        var isDirty = false;

        DrawSectionTitle("GENERAL");

        DrawOption(
            "Enable Synthesis Helper",
            "Adds a helper next to your synthesis window to help solve for the best craft. " +
            "Extremely useful for expert recipes, where the condition can greatly affect " +
            "which actions you take.",
            Config.EnableSynthHelper,
            v => Config.EnableSynthHelper = v,
            ref isDirty,
            "Adds a live overlay during crafting to suggest optimal next actions."
        );

        DrawOption(
            "Check For Delineations",
            "Your inventory will be checked to ensure that you have delineations available " +
            "before suggesting any specialist actions.",
            Config.CheckDelineations,
            v => Config.CheckDelineations = v,
            ref isDirty,
            "Checks inventory before the solver suggests specialist job actions."
        );

        DrawOption(
            "Progress Bar Style",
            "The style of progress bar to use when solving for a macro.",
            GetProgressBarTypeName,
            GetProgressBarTooltip,
            Config.ProgressType,
            v => Config.ProgressType = v,
            ref isDirty
        );

        ImGuiHelpers.ScaledDummy(5);

        using (var panel = ImRaii2.GroupPanel("Copying Settings", -1, out _))
        {
            DrawOption(
                "Macro Copy Method",
                "The method to copy a macro with.",
                GetCopyTypeName,
                GetCopyTypeTooltip,
                Config.MacroCopy.Type,
                v => Config.MacroCopy.Type = v,
                ref isDirty
            );

            if (Config.MacroCopy.Type == MacroCopyConfiguration.CopyType.CopyToMacroMate &&
                !Service.PluginInterface.InstalledPlugins.Any(p => p.IsLoaded && string.Equals(p.InternalName, "MacroMate", StringComparison.Ordinal)))
            {
                ImGui.SameLine();
                using (var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange))
                {
                    using var font = ImRaii.PushFont(UiBuilder.IconFont);
                    ImGui.TextUnformatted(FontAwesomeIcon.ExclamationCircle.ToIconString());
                }
                if (ImGui.IsItemHovered())
                    ImGuiUtils.HoveredTooltip("Macro Mate is not installed");
            }

            if (Config.MacroCopy.Type == MacroCopyConfiguration.CopyType.CopyToMacro)
            {
                DrawOption(
                    "Copy Downwards",
                    "Copy subsequent macros downward (#1 -> #11) instead of to the right.",
                    Config.MacroCopy.CopyDown,
                    v => Config.MacroCopy.CopyDown = v,
                    ref isDirty
                );

                DrawOption(
                    "Copy to Shared Macros",
                    "Copy to the shared macros tab. Leaving this unchecked copies to the " +
                    "individual tab.",
                    Config.MacroCopy.SharedMacro,
                    v => Config.MacroCopy.SharedMacro = v,
                    ref isDirty
                );

                DrawOption(
                    "Macro Number",
                    "The # of the macro to being copying to. Subsequent macros will be " +
                    "copied relative to this macro.",
                    Config.MacroCopy.StartMacroIdx,
                    0, 99,
                    v => Config.MacroCopy.StartMacroIdx = v,
                    ref isDirty
                );

                DrawOption(
                    "Max Macro Copy Count",
                    "The maximum number of macros to be copied. Any more and a window is " +
                    "displayed with the rest of them.",
                    Config.MacroCopy.MaxMacroCount,
                    1, 99,
                    v => Config.MacroCopy.MaxMacroCount = v,
                    ref isDirty
                );
            }
            else if (Config.MacroCopy.Type == MacroCopyConfiguration.CopyType.CopyToMacroMate)
            {
                DrawOption(
                    "Macro Name",
                    "The name of the macro to be created or updated in Macro Mate.",
                    Config.MacroCopy.MacroMateName,
                    v => Config.MacroCopy.MacroMateName = v,
                    ref isDirty
                );

                DrawOption(
                    "Macro Parent",
                    "The name of the parent group of the new macro. Leave blank or \"/\" if there is none.",
                    Config.MacroCopy.MacroMateParent,
                    v => Config.MacroCopy.MacroMateParent = v,
                    ref isDirty
                );
            }

            DrawOption(
                "Show Copied Message",
                "Shows a notification in the bottom right when a macro is copied successfully.",
                Config.MacroCopy.ShowCopiedMessage,
                v => Config.MacroCopy.ShowCopiedMessage = v,
                ref isDirty
            );

            if (Config.MacroCopy.Type != MacroCopyConfiguration.CopyType.CopyToMacroMate)
            {
                DrawOption(
                    "Use Macro Chain",
                    "Replaces the last step with /nextmacro to run the next macro " +
                    "automatically. Overrides the Intermediate Notification Sound.",
                    Config.MacroCopy.UseNextMacro,
                    v => Config.MacroCopy.UseNextMacro = v,
                    ref isDirty
                );

                if (Config.MacroCopy.UseNextMacro &&
                    !Service.PluginInterface.InstalledPlugins.Any(p => p.IsLoaded && string.Equals(p.InternalName, "MacroChain", StringComparison.Ordinal)))
                {
                    ImGui.SameLine();
                    using (var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange))
                    {
                        using var font = ImRaii.PushFont(UiBuilder.IconFont);
                        ImGui.TextUnformatted(FontAwesomeIcon.ExclamationCircle.ToIconString());
                    }
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.HoveredTooltip("Macro Chain is not installed");
                }
            }

            DrawOption(
                "Add Macro Lock",
                "Adds /mlock to the beginning of every macro. Prevents other " +
                "macros from being run.",
                Config.MacroCopy.UseMacroLock,
                v => Config.MacroCopy.UseMacroLock = v,
                ref isDirty
            );

            DrawOption(
                "Add Notification",
                "Replaces the last step of every macro with a /echo notification.",
                Config.MacroCopy.AddNotification,
                v => Config.MacroCopy.AddNotification = v,
                ref isDirty
            );

            if (Config.MacroCopy.AddNotification)
            {
                if ((Config.MacroCopy.Type == MacroCopyConfiguration.CopyType.CopyToMacro || !Config.MacroCopy.CombineMacro) && Config.MacroCopy.Type != MacroCopyConfiguration.CopyType.CopyToMacroMate)
                {
                    DrawOption(
                        "Force Notification",
                        "Prioritize always having a notification sound at the end of " +
                        "every macro. Keeping this off prevents macros with only 1 action.",
                        Config.MacroCopy.ForceNotification,
                        v => Config.MacroCopy.ForceNotification = v,
                        ref isDirty
                    );
                }

                DrawOption(
                    "Add Notification Sound",
                    "Adds a sound to the end of every macro.",
                    Config.MacroCopy.AddNotificationSound,
                    v => Config.MacroCopy.AddNotificationSound = v,
                    ref isDirty
                );

                if (Config.MacroCopy.AddNotificationSound)
                {
                    if (!Config.MacroCopy.UseNextMacro && Config.MacroCopy.Type != MacroCopyConfiguration.CopyType.CopyToMacroMate)
                    {
                        DrawOption(
                            "Intermediate Notification Sound",
                            "Ending notification sound for an intermediary macro.\n" +
                            "Uses <se.#>",
                            Config.MacroCopy.IntermediateNotificationSound,
                            1, 16,
                            v =>
                            {
                                Config.MacroCopy.IntermediateNotificationSound = v;
                                UIGlobals.PlayChatSoundEffect((uint)v);
                            },
                            ref isDirty
                        );
                    }

                    DrawOption(
                        "Final Notification Sound",
                        "Ending notification sound for the final macro.\n" +
                        "Uses <se.#>",
                        Config.MacroCopy.EndNotificationSound,
                        1, 16,
                        v =>
                        {
                            Config.MacroCopy.EndNotificationSound = v;
                            UIGlobals.PlayChatSoundEffect((uint)v);
                        },
                        ref isDirty
                    );
                }
            }

            if (Config.MacroCopy.Type != MacroCopyConfiguration.CopyType.CopyToMacro)
            {
                DrawOption(
                    "Remove Wait Times",
                    "Remove <wait.#> at the end of every action.",
                    Config.MacroCopy.RemoveWaitTimes,
                    v => Config.MacroCopy.RemoveWaitTimes = v,
                    ref isDirty
                );

                if (Config.MacroCopy.Type != MacroCopyConfiguration.CopyType.CopyToMacroMate)
                {
                    DrawOption(
                        "Combine Macro",
                        "Doesn't split the macro into smaller macros.",
                        Config.MacroCopy.CombineMacro,
                        v => Config.MacroCopy.CombineMacro = v,
                        ref isDirty
                    );
                }
            }
        }

        ImGuiHelpers.ScaledDummy(5);

        using (var panel = ImRaii2.GroupPanel("Gear Condition Alert", -1, out _))
        {
            DrawOption(
                "Show Gear Condition Alert",
                "Display a warning alert when gear condition drops below 50%. Helps prevent failed crafts due to broken gear.",
                Config.ShowGearCondition,
                v => Config.ShowGearCondition = v,
                ref isDirty,
                "⚙ Warn me when my crafting gear needs repair."
            );
        }

        if (isDirty)
            Config.Save();
    }
}
