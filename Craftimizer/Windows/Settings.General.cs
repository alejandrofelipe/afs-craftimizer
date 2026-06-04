using Craftimizer.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Linq;
using System.Numerics;
using Configuration = Craftimizer.Plugin.Configuration;
using MacroCopyConfiguration = Craftimizer.Plugin.MacroCopyConfiguration;
using Service = Craftimizer.Plugin.Service;

namespace Craftimizer.Windows;

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
            "Show Only One Macro Stat in Crafting Log",
            "Only one stat will be shown for a macro. If a craft will be finished, quality " +
            "is shown. Otherwise, progress is shown. Durability and remaining CP will be " +
            "hidden.",
            Config.ShowOptimalMacroStat,
            v => Config.ShowOptimalMacroStat = v,
            ref isDirty,
            "Shows HQ% or progress — whichever is most relevant for each macro."
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
            "Reliability Trial Count",
            "When testing for reliability of a macro in the editor, this many trials will be " +
            "run. You should set this value to at least 100 to get a reliable spread of data. " +
            "If it's too low, you may not find an outlier, and the average might be skewed.",
            Config.ReliabilitySimulationCount,
            5,
            5000,
            v => Config.ReliabilitySimulationCount = v,
            ref isDirty
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
                    ImGuiUtils.Tooltip("Macro Mate is not installed");
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
                        ImGuiUtils.Tooltip("Macro Chain is not installed");
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

        using (var panel = ImRaii2.GroupPanel("Icon Cache Management", -1, out _))
        {
            DrawOption(
                "Enable Automatic Cache Cleanup",
                "Unload unused icons after inactivity period. Disable for maximum performance on high-memory systems.",
                Config.EnableIconCacheEviction,
                v => Config.EnableIconCacheEviction = v,
                ref isDirty,
                "Automatically frees memory by unloading icons not recently used."
            );

            if (Config.EnableIconCacheEviction)
            {
                DrawOption(
                    "Sliding Expiration (min)",
                    "Icon unloaded if not accessed for this duration. Lower values save more memory but may cause brief loading delays.",
                    Config.IconCacheSlidingExpirationMinutes,
                    1, 60,
                    v => Config.IconCacheSlidingExpirationMinutes = v,
                    ref isDirty
                );

                DrawOption(
                    "Max Cache Time (min)",
                    "Icon unloaded after this time, even if accessed frequently. Prevents memory buildup in long sessions.",
                    Config.IconCacheAbsoluteExpirationMinutes,
                    5, 120,
                    v => Config.IconCacheAbsoluteExpirationMinutes = v,
                    ref isDirty
                );
            }

            DrawOption(
                "Cache Size Limit",
                "Maximum icons in cache (0 = unlimited). Recommended: 1024 for typical use, 2048 for power users.",
                Config.IconCacheSizeLimit,
                0, 4096,
                v => Config.IconCacheSizeLimit = v,
                ref isDirty
            );
        }

        ImGuiHelpers.ScaledDummy(5);

        using (var panel = ImRaii2.GroupPanel("Gear Durability Warning", -1, out _))
        {
            DrawOption(
                "Show Low Durability Warning",
                "Display prominent warning when gear condition is low. Helps prevent failed crafts due to broken gear.",
                Config.ShowLowDurabilityWarning,
                v => Config.ShowLowDurabilityWarning = v,
                ref isDirty,
                "⚠ Warn me when my crafting gear needs repair soon."
            );

            if (Config.ShowLowDurabilityWarning)
            {
                DrawOption(
                    "Warning Threshold (%)",
                    "Show warning when minimum gear condition falls below this percentage.",
                    Config.LowDurabilityThreshold,
                    1, 30,
                    v => Config.LowDurabilityThreshold = v,
                    ref isDirty
                );
            }

            ImGuiHelpers.ScaledDummy(3);

            DrawOption(
                "Enable Gear Wear Tracking (Experimental)",
                "Learn how much gear durability each recipe consumes over time. The plugin will monitor your gear condition before and after each craft, building a database of wear rates per recipe. After ~10 crafts of the same recipe, predictions become accurate. Data is stored locally and never shared.",
                Config.EnableGearWearTracking,
                v => Config.EnableGearWearTracking = v,
                ref isDirty,
                "🔬 Tracks gear wear to predict crafts remaining.\n\nHow it works:\n• Monitors gear condition before/after each craft\n• Stores average wear rate per recipe\n• Predicts remaining crafts with confidence level\n• Requires 10+ samples per recipe for accuracy"
            );

            if (Config.EnableGearWearTracking && Config.GearWearData.Count > 0)
            {
                ImGuiHelpers.ScaledDummy(2);
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                {
                    ImGui.TextWrapped($"Tracking data: {Config.GearWearData.Count} recipes monitored, {Config.GearWearData.Values.Sum(s => s.SampleCount)} crafts recorded.");
                }

                if (ImGui.Button("Clear Tracking Data", OptionButtonSize))
                {
                    Config.GearWearData.Clear();
                    isDirty = true;
                }
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip("Reset all collected gear wear data. Tracking will start fresh.");
            }

            ImGuiHelpers.ScaledDummy(3);

            DrawOption(
                "Enable Cosmic Tool Tracking",
                "Mostra o progresso de research data da Cosmic Tool no Crafting Log e no Macro Editor durante Stellar Missions (Patch 7.21+). Atualiza em tempo real após entregar um collectable. Não requer outros plugins instalados.",
                Config.EnableCosmicToolTracking,
                v => Config.EnableCosmicToolTracking = v,
                ref isDirty,
                "Cosmic Tool Progress Tracking\n\n• Research data atual / necessário exibido inline\n• Atualiza ao entregar collectables (Stellar Missions)\n• Funciona em Sinus Ardorum e Auxesia (Patch 7.51+)\n• Nenhum plugin externo necessário"
            );
        }

        if (isDirty)
            Config.Save();
    }
}
