using Artificer.Plugin;
using Artificer.Simulator;
using Artificer.Simulator.Actions;
using Artificer.Utils;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Dalamud.Utility;

namespace Artificer.Windows;

public sealed partial class MacroEditor
{
    private bool DrawCharacterParams()
    {
        var oldStats = CharacterStats;

        var textClassName = RecipeData.ClassJob.GetAbbreviation();
        var textClassSize = AxisFont.CalcTextSize(textClassName);

        var imageSize = ImGui.GetFrameHeight();
        ImGuiUtils.AlignCentered(
                imageSize + 5 +
                textClassSize.X);
        ImGui.AlignTextToFramePadding();

        var uv0 = UIConstants.ItemIconUv0;
        var uv1 = UIConstants.ItemIconUv1;

        ImGui.Image(_plugin.IconManager.GetIconCached(RecipeData.ClassJob.GetIconId()).Handle, new Vector2(imageSize), uv0, uv1);
        ImGui.SameLine(0, 5);
        AxisFont.Text(textClassName);

        using (var statsTable = ImRaii.Table("stats", 3, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
        {
            if (statsTable)
            {
                ImGui.TableSetupColumn("col1", ImGuiTableColumnFlags.WidthStretch, 4.5f);
                ImGui.TableSetupColumn("col2", ImGuiTableColumnFlags.WidthStretch, 3);
                ImGui.TableSetupColumn("col3", ImGuiTableColumnFlags.WidthStretch, 2);

                var inputWidth = ImGui.CalcTextSize(SqText.ToLevelString(9999)).X + ImGui.GetStyle().FramePadding.X * 2 + 5;

                void DrawStat(string name, int value, Action<int> setter)
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(name);
                    ImGui.SameLine(0, 5);
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

                    var text = value.ToString();
                    if (ImGui.InputText($"##{name}", ref text, 8, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal))
                    {
                        setter(
                            int.TryParse(text, out var newLevel)
                            ? Math.Clamp(newLevel, 0, 9999)
                            : 0);
                    }
                }

                ImGui.TableNextColumn();
                DrawStat("Craftsmanship", CharacterStats.Craftsmanship, v => CharacterStats = CharacterStats with { Craftsmanship = v });

                ImGui.TableNextColumn();
                DrawStat("Control", CharacterStats.Control, v => CharacterStats = CharacterStats with { Control = v });

                ImGui.TableNextColumn();
                DrawStat("CP", CharacterStats.CP, v => CharacterStats = CharacterStats with { CP = v });
            }
        }

        using (var paramTable = ImRaii.Table("params", 3, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
        {
            if (paramTable)
            {
                ImGui.TableSetupColumn("col1", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("col2", ImGuiTableColumnFlags.WidthStretch, 3);
                ImGui.TableSetupColumn("col3", ImGuiTableColumnFlags.WidthStretch, 2);

                ImGui.TableNextColumn();
                ImGuiUtils.AlignCentered(GetLevelEntryWidth());

                {
                    var level = CharacterStats.Level;
                    if (DrawLevelEntry(ref level))
                    {
                        CharacterStats = CharacterStats with
                        {
                            Level = level
                        };
                    }
                }

                var disabledTint = Colors.Disabled;
                var imageButtonPadding = (int)(ImGui.GetStyle().FramePadding.Y / 2f);
                var imageButtonSize = imageSize - imageButtonPadding * 2;
                {
                    var splendorousLevel = 90;
                    if (CharacterStats.HasSplendorousBuff && splendorousLevel > CharacterStats.Level)
                        CharacterStats = CharacterStats with { HasSplendorousBuff = false };

                    using (var d = ImRaii.Disabled(splendorousLevel > CharacterStats.Level))
                    {
                        var v = CharacterStats.HasSplendorousBuff;
                        var tint = v ? Vector4.One : disabledTint;
                        if (ImGui.ImageButton(SplendorousBadge.Handle, new Vector2(imageButtonSize), default, Vector2.One, imageButtonPadding, default, tint))
                            CharacterStats = CharacterStats with { HasSplendorousBuff = !v };
                    }
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGuiUtils.Tooltip(CharacterStats.HasSplendorousBuff ? $"Splendorous Tool" : "No Splendorous Tool");
                }
                ImGui.SameLine(0, 5);
                bool? newIsSpecialist = null;
                {
                    var v = CharacterStats.IsSpecialist;

                    var specialistLevel = 55;
                    if (CharacterStats.IsSpecialist && specialistLevel > CharacterStats.Level)
                        newIsSpecialist = v = false;

                    using (var d = ImRaii.Disabled(specialistLevel > CharacterStats.Level))
                    {
                        var tint = Colors.SpecialistGold * (v ? Vector4.One : disabledTint);
                        if (ImGui.ImageButton(SpecialistBadge.Handle, new Vector2(imageButtonSize), default, Vector2.One, imageButtonPadding, default, tint))
                        {
                            v = !v;
                            newIsSpecialist = v;
                        }
                    }
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGuiUtils.Tooltip(v ? $"Specialist" : "Not a Specialist");
                }
                ImGui.SameLine(0, 5);
                {
                    var manipLevel = ActionType.Manipulation.GetActionRow(RecipeData.ClassJob).Action!.Value.ClassJobLevel;
                    using (var d = ImRaii.Disabled(manipLevel > CharacterStats.Level))
                    {
                        var v = CharacterStats.CanUseManipulation && manipLevel <= CharacterStats.Level;
                        var tint = (v || manipLevel > CharacterStats.Level) ? disabledTint : Vector4.One;
                        if (ImGui.ImageButton((v ? ManipulationBadge : NoManipulationBadge).Handle, new Vector2(imageButtonSize), default, Vector2.One, imageButtonPadding, default, tint))
                            CharacterStats = CharacterStats with { CanUseManipulation = !v };
                    }
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGuiUtils.Tooltip(CharacterStats.CanUseManipulation && manipLevel <= CharacterStats.Level ? $"Can Use Manipulation" : "Cannot Use Manipulation");
                }

                ImGui.TableNextColumn();

                var buffBadgeSize = new Vector2(imageSize * (WellFedBadge.AspectRatio ?? 1), imageSize);

                var newFoodBuff = DrawItemBuffCombo("##food", WellFedBadge, buffBadgeSize, "Food", Buffs.Food, FoodStatus.OrderedFoods);
                var newMedicineBuff = DrawItemBuffCombo("##medicine", MedicatedBadge, buffBadgeSize, "Medicine", Buffs.Medicine, FoodStatus.OrderedMedicines);

                ImGui.TableNextColumn();

                int? newFCCraftsmanshipBuff = null;
                ImGui.Image(EatFromTheHandBadge.Handle, buffBadgeSize);
                var fcBuffName = "Eat from the Hand";
                var fcStatName = "Craftsmanship";
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip(fcBuffName);
                ImGui.SameLine(0, 5);
                {
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    using var combo = ImRaii.Combo("##fcCraftsmanship", FormatFCBuff(fcBuffName, Buffs.FC.Craftsmanship));
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip(FormatFCBuffDescription(fcBuffName, fcStatName, Buffs.FC.Craftsmanship));
                    if (combo)
                    {
                        if (ImGui.Selectable("None", Buffs.FC.Craftsmanship == 0))
                            newFCCraftsmanshipBuff = 0;

                        for (var i = 1; i <= 3; ++i)
                        {
                            if (ImGui.Selectable(FormatFCBuff(fcBuffName, i), Buffs.FC.Craftsmanship == i))
                                newFCCraftsmanshipBuff = i;
                            if (ImGui.IsItemHovered())
                                ImGuiUtils.Tooltip(FormatFCBuffDescription(fcBuffName, fcStatName, i));
                        }
                    }
                }

                int? newFCControlBuff = null;
                ImGui.Image(InControlBadge.Handle, buffBadgeSize);
                fcBuffName = "In Control";
                fcStatName = "Control";
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip(fcBuffName);
                ImGui.SameLine(0, 5);
                {
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    using var combo = ImRaii.Combo("##fcControl", FormatFCBuff(fcBuffName, Buffs.FC.Control));
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip(FormatFCBuffDescription(fcBuffName, fcStatName, Buffs.FC.Control));
                    if (combo)
                    {
                        if (ImGui.Selectable("None", Buffs.FC.Control == 0))
                            newFCControlBuff = 0;

                        for (var i = 1; i <= 3; ++i)
                        {
                            if (ImGui.Selectable(FormatFCBuff(fcBuffName, i), Buffs.FC.Control == i))
                                newFCControlBuff = i;
                            if (ImGui.IsItemHovered())
                                ImGuiUtils.Tooltip(FormatFCBuffDescription(fcBuffName, fcStatName, i));
                        }
                    }
                }

                if (newIsSpecialist.HasValue || newFoodBuff.HasValue || newMedicineBuff.HasValue || newFCCraftsmanshipBuff.HasValue || newFCControlBuff.HasValue)
                {
                    var baseStat = GetBaseStats(CharacterStats);

                    Buffs = Buffs with
                    {
                        Food = newFoodBuff ?? Buffs.Food,
                        Medicine = newMedicineBuff ?? Buffs.Medicine,
                        FC = (newFCCraftsmanshipBuff ?? Buffs.FC.Craftsmanship, newFCControlBuff ?? Buffs.FC.Control)
                    };

                    var newStats = CharacterStats with { Craftsmanship = baseStat.Craftsmanship, Control = baseStat.Control, CP = baseStat.CP };
                    if (newIsSpecialist is { } isSpecialist)
                    {
                        if (isSpecialist != CharacterStats.IsSpecialist)
                        {
                            var craftsmanship = 20;
                            var control = 20;
                            var cp = 15;
                            if (!isSpecialist)
                            {
                                craftsmanship *= -1;
                                control *= -1;
                                cp *= -1;
                            }

                            newStats = newStats with
                            {
                                IsSpecialist = isSpecialist,
                                Craftsmanship = newStats.Craftsmanship + craftsmanship,
                                Control = newStats.Control + control,
                                CP = newStats.CP + cp
                            };
                        }
                    }

                    var bonus = CalculateConsumableBonus(newStats);
                    CharacterStats = newStats with
                    {
                        Craftsmanship = newStats.Craftsmanship + bonus.Craftsmanship,
                        Control = newStats.Control + bonus.Control,
                        CP = newStats.CP + bonus.CP
                    };
                }
            }
        }

        // Gear condition indicator (if available)
        if (_plugin.Configuration.ShowGearCondition)
        {
            var gearCondition = Gearsets.GetMinimumGearCondition();
            if (gearCondition.HasValue)
            {
                ImGuiHelpers.ScaledDummy(2);
                
                var conditionPercent = gearCondition.Value;
                var conditionColor = conditionPercent switch
                {
                    >= 70f => new Vector4(0.3f, 0.9f, 0.3f, 1f),
                    >= 30f => new Vector4(0.9f, 0.9f, 0.3f, 1f),
                    _      => new Vector4(0.9f, 0.3f, 0.3f, 1f)
                };

                using (ImRaii.PushColor(ImGuiCol.Text, conditionColor))
                {
                    ImGuiUtils.AlignCentered(ImGui.CalcTextSize($"⚙ Gear: {conditionPercent:0}%").X);
                    ImGui.TextUnformatted($"⚙ Gear: {conditionPercent:0}%");
                }
                
                if (ImGui.IsItemHovered())
                {
                    ImGuiUtils.Tooltip("Condição mínima do equipamento atual.\nRepare o equipamento se estiver baixo.");
                }
            }
        }

        return oldStats != CharacterStats;
    }

    private static string FormatItemBuff((uint ItemId, bool IsHQ) input)
    {
        if (input.ItemId == 0)
            return "None";

        var name = LuminaSheets.ItemSheet.GetRowOrDefault(input.ItemId)?.Name.ToString() ?? $"Unknown ({input.ItemId})";
        return input.IsHQ ? $"{name} (HQ)" : name;
    }

    private static string FormatItemBuffDescription((uint ItemId, bool IsHQ) input)
    {
        var s = new StringBuilder(FormatItemBuff(input) + "\n");

        void AddStat(string name, FoodStatus.FoodStat? statNullable)
        {
            if (statNullable is not { } stat)
                return;

            var (value, max) = input.IsHQ ? (stat.ValueHQ, stat.MaxHQ) : (stat.Value, stat.Max);

            if (!stat.IsRelative)
                s.AppendLine($"{name} +{value}");
            else
                s.AppendLine($"{name} +{value}% (Max {max})");
        }

        if (FoodStatus.TryGetFood(input.ItemId) is { } food)
        {
            AddStat("Craftsmanship", food.Craftsmanship);
            AddStat("Control", food.Control);
            AddStat("CP", food.CP);
        }
        return s.ToString();
    }

    private static string FormatFCBuff(string name, int level)
    {
        if (level == 0)
            return "None";

        return $"{name} {new string('I', level)}";
    }

    private static string FormatFCBuffDescription(string name, string statName, int level)
    {
        if (level == 0)
            return FormatFCBuff(name, level);

        return $"{FormatFCBuff(name, level)}\n{statName} +{level * 5}";
    }

    private (int Craftsmanship, int Control, int CP) GetBaseStats(CharacterStats stats)
    {
        var (craftsmanship, control, cp) = (stats.Craftsmanship, stats.Control, stats.CP);

        craftsmanship -= Buffs.FC.Craftsmanship * 5;
        control -= Buffs.FC.Control * 5;

        var food = FoodStatus.TryGetFood(Buffs.Food.ItemId);
        var medicine = FoodStatus.TryGetFood(Buffs.Medicine.ItemId);

        static void GetBaseStat(ref int val, bool isHq, FoodStatus.FoodStat? food, out float a, out int b)
        {
            a = 1;
            b = 0;
            if (food is { } stat)
            {
                if (stat.IsRelative)
                {
                    a = (isHq ? stat.ValueHQ : stat.Value) / 100f;
                    b = isHq ? stat.MaxHQ : stat.Max;
                }
                else
                    val -= isHq ? stat.ValueHQ : stat.Value;
            }
        }

        static int GetBaseStat2(int val, bool foodHq, FoodStatus.FoodStat? food, bool medicineHq, FoodStatus.FoodStat? medicine)
        {
            GetBaseStat(ref val, foodHq, food, out var a, out var b);
            GetBaseStat(ref val, medicineHq, medicine, out var c, out var d);
            return CalculateBaseStat(val, a, b, c, d);
        }

        craftsmanship = GetBaseStat2(craftsmanship, Buffs.Food.IsHQ, food?.Craftsmanship, Buffs.Medicine.IsHQ, medicine?.Craftsmanship);
        control = GetBaseStat2(control, Buffs.Food.IsHQ, food?.Control, Buffs.Medicine.IsHQ, medicine?.Control);
        cp = GetBaseStat2(cp, Buffs.Food.IsHQ, food?.CP, Buffs.Medicine.IsHQ, medicine?.CP);

        return (craftsmanship, control, cp);
    }

    private (int Craftsmanship, int Control, int CP) CalculateConsumableBonus(CharacterStats stats)
    {
        int craftsmanship = 0, control = 0, cp = 0;
        static int CalculateStatBonus(int val, bool isHq, FoodStatus.FoodStat? food)
        {
            if (food is { } stat)
            {
                if (stat.IsRelative)
                    return (int)Math.Min((isHq ? stat.ValueHQ : stat.Value) / 100f * val, isHq ? stat.MaxHQ : stat.Max);
                else
                    return isHq ? stat.ValueHQ : stat.Value;
            }
            return 0;
        }
        var food = FoodStatus.TryGetFood(Buffs.Food.ItemId);

        craftsmanship += CalculateStatBonus(stats.Craftsmanship, Buffs.Food.IsHQ, food?.Craftsmanship);
        control += CalculateStatBonus(stats.Control, Buffs.Food.IsHQ, food?.Control);
        cp += CalculateStatBonus(stats.CP, Buffs.Food.IsHQ, food?.CP);

        var medicine = FoodStatus.TryGetFood(Buffs.Medicine.ItemId);
        craftsmanship += CalculateStatBonus(stats.Craftsmanship, Buffs.Medicine.IsHQ, medicine?.Craftsmanship);
        control += CalculateStatBonus(stats.Control, Buffs.Medicine.IsHQ, medicine?.Control);
        cp += CalculateStatBonus(stats.CP, Buffs.Medicine.IsHQ, medicine?.CP);

        craftsmanship += Buffs.FC.Craftsmanship * 5;
        control += Buffs.FC.Control * 5;

        return (craftsmanship, control, cp);
    }

    // y: output stat
    // a: coefficient
    // b: max value for a product
    // c: coefficient
    // d: max value for c product
    // Implementation of https://www.desmos.com/calculator/qlj9f9qjqy for calculating x from y
    private static int CalculateBaseStat(int y, float a, int b, float c, int d)
    {
        if (y <= 0)
            return 0;

        if (d / c < b / a)
            (a, b, c, d) = (c, d, a, b);

        var dc = d / c;
        var ba = b / a;
        if (dc + b + d <= y)
            return y - b - d;
        else if (y <= (1 + a + c) * ba)
            return (int)Math.Ceiling(y / (a + c + 1));
        else
            return (int)Math.Ceiling((y - b) / (c + 1));
    }

    private static (uint ItemId, bool HQ)? DrawItemBuffCombo(
        string comboId,
        ITextureIcon badge,
        Vector2 badgeSize,
        string badgeTooltip,
        (uint ItemId, bool HQ) current,
        IEnumerable<FoodStatus.Food> items)
    {
        ImGui.Image(badge.Handle, badgeSize);
        if (ImGui.IsItemHovered())
            ImGuiUtils.Tooltip(badgeTooltip);
        ImGui.SameLine(0, 5);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        (uint ItemId, bool HQ)? result = null;
        using var combo = ImRaii.Combo(comboId, FormatItemBuff(current));
        if (ImGui.IsItemHovered())
            ImGuiUtils.Tooltip(FormatItemBuffDescription(current));
        if (combo)
        {
            if (ImGui.Selectable("None", current.ItemId == 0))
                result = (0, false);
            foreach (var food in items)
            {
                var row = (food.Item.RowId, false);
                if (ImGui.Selectable(FormatItemBuff(row), current == row))
                    result = row;
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip(FormatItemBuffDescription(row));
                if (food.Item.CanBeHq)
                {
                    row = (food.Item.RowId, true);
                    if (ImGui.Selectable(FormatItemBuff(row), current == row))
                        result = row;
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.Tooltip(FormatItemBuffDescription(row));
                }
            }
        }
        return result;
    }
}
