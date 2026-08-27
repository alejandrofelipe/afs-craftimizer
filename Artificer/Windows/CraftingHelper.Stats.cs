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
    private void DrawCharacterStats()
    {
        var level = RecipeData!.ClassJob.GetPlayerLevel();
        {
            var textClassName = RecipeData.ClassJob.GetAbbreviation();
            var textClassSize = AxisFont.CalcTextSize(textClassName);
            var levelText = string.Empty;
            if (level != 0)
                levelText = SqText.LevelPrefix.ToIconChar() + SqText.ToLevelString(level);
            var imageSize = ImGui.GetFrameHeight();
            bool hasSplendorous = false, hasSpecialist = false, shouldHaveManip = false;
            if (CraftStatus is not (CraftableStatus.LockedClassJob or CraftableStatus.WrongClassJob))
            {
                hasSplendorous = CharacterStats!.HasSplendorousBuff;
                hasSpecialist = CharacterStats!.IsSpecialist;
                shouldHaveManip = !CharacterStats.CanUseManipulation && CharacterStats.Level >= ActionType.Manipulation.Level();
            }

            ImGuiUtils.AlignCentered(
                imageSize + 5 +
                textClassSize.X +
                (level == 0 ? 0 : (3 + ImGui.CalcTextSize(levelText).X)) +
                (hasSplendorous ? (3 + imageSize) : 0) +
                (hasSpecialist ? (3 + imageSize) : 0) +
                (shouldHaveManip ? (3 + imageSize) : 0)
                );
            ImGui.AlignTextToFramePadding();

            var uv0 = UIConstants.ItemIconUv0;
            var uv1 = UIConstants.ItemIconUv1;

            ImGui.Image(_plugin.IconManager.GetIconCached(RecipeData.ClassJob.GetIconId()).Handle, new Vector2(imageSize), uv0, uv1);
            ImGui.SameLine(0, 5);

            if (level != 0)
            {
                ImGui.TextUnformatted(levelText);
                ImGui.SameLine(0, 3);
            }

            AxisFont.Text(textClassName);

            if (hasSplendorous)
            {
                ImGui.SameLine(0, 3);
                ImGuiUtils.DrawBadge(Service.IconManager.GetAssemblyTextureCached("Graphics.splendorous.png").Handle.Handle, new Vector2(imageSize), "Splendorous Tool");
            }

            if (hasSpecialist)
            {
                ImGui.SameLine(0, 3);
                ImGuiUtils.DrawBadge(Service.IconManager.GetAssemblyTextureCached("Graphics.specialist.png").Handle.Handle, new Vector2(imageSize), "Specialist", Colors.SpecialistGold);
            }

            if (shouldHaveManip)
            {
                ImGui.SameLine(0, 3);
                ImGuiUtils.DrawBadge(Service.IconManager.GetAssemblyTextureCached("Graphics.no_manip.png").Handle.Handle, new Vector2(imageSize), "No Manipulation (Missing Job Quest)");
            }
        }

        switch (CraftStatus)
        {
            case CraftableStatus.LockedClassJob:
                {
                    ImGuiUtils.TextCentered($"You do not have {RecipeData.ClassJob.GetName()} unlocked.");
                    ImGui.Separator();
                    var unlockQuest = RecipeData.ClassJob.GetUnlockQuest();
                    var (questGiver, questTerritory, questLocation, mapPayload) = ResolveLevelData(unlockQuest.IssuerLocation.RowId);

                    var unlockText = $"Unlock it from {questGiver}";
                    ImGuiUtils.AlignCentered(ImGui.CalcTextSize(unlockText).X + 5 + ImGui.GetFrameHeight());
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(unlockText);
                    ImGui.SameLine(0, 5);
                    if (ImGuiUtils.IconButtonWithTooltip((int)FontAwesomeIcon.Flag, "Open in map"))
                        Service.GameGui.OpenMapWithMapLink(mapPayload);

                    ImGuiUtils.TextCentered($"{questTerritory} ({GetCoordinatesString(questLocation)})");
                }
                break;
            case CraftableStatus.WrongClassJob:
                {
                    ImGuiUtils.TextCentered($"You are not {RecipeData.ClassJob.GetNameArticle()} {RecipeData.ClassJob.GetName()}.");
                    var gearsetId = GetGearsetForJob(RecipeData.ClassJob);
                    if (gearsetId.HasValue)
                    {
                        if (ImGuiUtils.ButtonCentered("Switch Job"))
                            RaptureGearsetModule.Instance()->EquipGearset(gearsetId.Value);
                        ImGuiUtils.HoveredTooltip($"Swap to gearset {gearsetId + 1}");
                    }
                    else
                        ImGuiUtils.TextCentered($"You do not have any {RecipeData.ClassJob.GetName()} gearsets.");
                    ImGui.Dummy(Vector2.Zero);
                }
                break;
            case CraftableStatus.SpecialistRequired:
                {
                    ImGuiUtils.TextCentered($"You need to be a specialist to craft this recipe.");

                    var (vendorName, vendorTerritory, vendorLoation, mapPayload) = ResolveLevelData(5891399);

                    var unlockText = $"Trade a Soul of the Crafter to {vendorName}";
                    ImGuiUtils.AlignCentered(ImGui.CalcTextSize(unlockText).X + 5 + ImGui.GetFrameHeight());
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(unlockText);
                    ImGui.SameLine(0, 5);
                    if (ImGuiUtils.IconButtonWithTooltip((int)FontAwesomeIcon.Flag, "Open in map"))
                        Service.GameGui.OpenMapWithMapLink(mapPayload);

                    ImGuiUtils.TextCentered($"{vendorTerritory} ({GetCoordinatesString(vendorLoation)})");
                }
                break;
            case CraftableStatus.RequiredItem:
                {
                    var item = RecipeData.Recipe.ItemRequired.Value!;
                    var itemName = item.Name.ToString();
                    var imageSize = ImGui.GetFrameHeight();

                    ImGuiUtils.TextCentered($"You are missing the required equipment.");
                    ImGuiUtils.AlignCentered(imageSize + 5 + ImGui.CalcTextSize(itemName).X);
                    ImGui.AlignTextToFramePadding();
                    ImGui.Image(_plugin.IconManager.GetIconCached(item.Icon).Handle, new(imageSize));
                    ImGui.SameLine(0, 5);
                    ImGui.TextUnformatted(itemName);
                }
                break;
            case CraftableStatus.RequiredStatus:
                {
                    var status = RecipeData.Recipe.StatusRequired.Value!;
                    var statusName = status.Name.ToString();
                    var statusIcon = _plugin.IconManager.GetIconCached(status.Icon);
                    var imageSize = new Vector2(ImGui.GetFrameHeight() * (statusIcon.AspectRatio ?? 1), ImGui.GetFrameHeight());

                    ImGuiUtils.TextCentered($"You are missing the required status effect.");
                    ImGuiUtils.AlignCentered(imageSize.X + 5 + ImGui.CalcTextSize(statusName).X);
                    ImGui.AlignTextToFramePadding();
                    ImGui.Image(statusIcon.Handle, imageSize);
                    ImGui.SameLine(0, 5);
                    ImGui.TextUnformatted(statusName);
                }
                break;
            case CraftableStatus.CraftsmanshipTooLow:
                {
                    ImGuiUtils.TextCentered("Your Craftsmanship is too low.");

                    DrawRequiredStatsTable(CharacterStats!.Craftsmanship, RecipeData.Recipe.RequiredCraftsmanship);
                }
                break;
            case CraftableStatus.ControlTooLow:
                {
                    ImGuiUtils.TextCentered("Your Control is too low.");

                    DrawRequiredStatsTable(CharacterStats!.Control, RecipeData.Recipe.RequiredControl);
                }
                break;
            case CraftableStatus.OK:
                {
                    using var table = ImRaii.Table("characterStats", 2);
                    if (table)
                    {
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 100 * ImGuiHelpers.GlobalScale);
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted("Craftsmanship");
                        ImGui.TableNextColumn();
                        ImGuiUtils.TextRight($"{CharacterStats!.Craftsmanship}");

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted("Control");
                        ImGui.TableNextColumn();
                        ImGuiUtils.TextRight($"{CharacterStats.Control}");

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted("CP");
                        ImGui.TableNextColumn();
                        ImGuiUtils.TextRight($"{CharacterStats.CP}");
                    }
                }
                break;
        }

        using var _spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.Dummy(Vector2.Zero);
    }

    private void DrawRecipeStats()
    {
        {
            var textStars = new string('★', RecipeData!.Table.Stars);
            var textStarsSize = Vector2.Zero;
            if (!string.IsNullOrEmpty(textStars))
            {
                textStarsSize = AxisFont.CalcTextSize(textStars);
            }
            var textLevel = SqText.LevelPrefix.ToIconChar() + SqText.ToLevelString(RecipeData.AdjustedJobLevel ?? RecipeData.RecipeInfo.ClassJobLevel);
            var isExpert = RecipeData.RecipeInfo.IsExpert;
            var isCollectable = RecipeData.IsCollectable;
            var isAdjustable = RecipeData.AdjustedJobLevel.HasValue;
            var imageSize = ImGui.GetFrameHeight();
            var textSize = ImGui.GetFontSize();
            var badgeSize = new Vector2(textSize * (ExpertBadge.AspectRatio ?? 1), textSize);
            var badgeOffset = (imageSize - badgeSize.Y) / 2;

            ImGuiUtils.AlignCentered(
                imageSize + 5 +
                ImGui.CalcTextSize(textLevel).X +
                (textStarsSize != Vector2.Zero ? textStarsSize.X + 3 : 0) +
                (isAdjustable ? imageSize + 3 : 0) +
                (isCollectable ? badgeSize.X + 3 : 0) +
                (isExpert ? badgeSize.X + 3 : 0)
                );
            ImGui.AlignTextToFramePadding();

            ImGui.Image(_plugin.IconManager.GetIconCached(RecipeData.Recipe.ItemResult.Value!.Icon).Handle, new Vector2(imageSize));

            ImGui.SameLine(0, 5);
            ImGui.TextUnformatted(textLevel);

            if (textStarsSize != Vector2.Zero)
            {
                ImGui.SameLine(0, 3);

                // Aligns better
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 1);
                AxisFont.Text(textStars);
            }

            if (isAdjustable)
            {
                ImGui.SameLine(0, 3);
                ImGui.Image(Service.IconManager.GetIconCached(60810).Handle, new(imageSize));
                ImGuiUtils.HoveredTooltip($"Cosmic Exploration");
            }

            if (isCollectable)
            {
                ImGui.SameLine(0, 3);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + badgeOffset);
                ImGui.Image(CollectibleBadge.Handle, badgeSize);
                ImGuiUtils.HoveredTooltip($"Collectible");
            }

            if (isExpert)
            {
                ImGui.SameLine(0, 3);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + badgeOffset);
                ImGui.Image(ExpertBadge.Handle, badgeSize);
                ImGuiUtils.HoveredTooltip($"Expert Recipe");
            }
        }

        using var table = ImRaii.Table("recipeStats", 2);
        if (table)
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 100 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Progress");
            ImGui.TableNextColumn();
            ImGuiUtils.TextRight($"{RecipeData.RecipeInfo.MaxProgress}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Quality");
            ImGui.TableNextColumn();
            ImGuiUtils.TextRight($"{RecipeData.RecipeInfo.MaxQuality}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Durability");
            ImGui.TableNextColumn();
            ImGuiUtils.TextRight($"{RecipeData.RecipeInfo.MaxDurability}");

            if (RecipeData.IsCosmicExploration
                && _plugin.Configuration.EnableCosmicToolTracking
                && _cosmicProgress is { } cp)
            {
                var active = cp.Types[cp.ActiveType];
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"Research Type {CosmicRomanType(cp.ActiveType)}");
                ImGui.TableNextColumn();
                var frac = active.Needed > 0 ? (float)active.Current / active.Needed : 0f;
                ImGui.ProgressBar(frac, new Vector2(-1, ImGui.GetTextLineHeight()));
                ImGuiUtils.HoveredTooltip($"{active.Current:N0} / {active.Needed:N0}  ({frac:P0})");
            }
        }
    }

    private static string CosmicRomanType(int zeroBasedType) => (zeroBasedType + 1) switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV",
        5 => "V", 6 => "VI", 7 => "VII",
        _ => (zeroBasedType + 1).ToString()
    };

    private static void DrawRequiredStatsTable(int current, int required)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(current, required);

        using var table = ImRaii.Table("requiredStats", 2);
        if (table)
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 100 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Current");
            ImGui.TableNextColumn();
            ImGui.TextColored(Colors.Good, $"{current}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("Required");
            ImGui.TableNextColumn();
            ImGui.TextColored(Colors.Bad, $"{required}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted("You need");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{required - current}");
        }
    }
}
