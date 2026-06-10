using Craftimizer.Plugin;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Utils;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Recipe = Lumina.Excel.Sheets.Recipe;
using Dalamud.Game.Text;

namespace Craftimizer.Windows;

public sealed partial class MacroEditor
{
    private readonly struct RecipeWrapper(Recipe recipe) : IEquatable<RecipeWrapper>
    {
        public readonly Recipe Recipe = recipe;

        public bool Equals(RecipeWrapper other) =>
            Recipe.RowId == other.Recipe.RowId;

        public override bool Equals(object? obj)
        {
            return obj is RecipeWrapper other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked((int)Recipe.RowId);
        }
    }

    private readonly List<RecipeWrapper> searchableRecipes = [.. LuminaSheets.RecipeSheet.Where(r => r.RecipeLevelTable.RowId != 0 && r.ItemResult.RowId != 0).Select(r => new RecipeWrapper(r))];

    private bool DrawRecipeParams()
    {
        var oldStartingQuality = StartingQuality;
        var adjustedJobLevel = RecipeData.AdjustedJobLevel;

        var textStars = new string('★', RecipeData!.Table.Stars);

        string? textLevel = null;
        float textLevelSize;
        if (adjustedJobLevel is { } adjJobLevel)
        {
            textLevelSize = GetLevelEntryWidth();
        }
        else
        {
            textLevel = SqText.LevelPrefix.ToIconChar() + SqText.ToLevelString(RecipeData.RecipeInfo.ClassJobLevel);
            textLevelSize = ImGui.CalcTextSize(textLevel).X;
        }

        var isExpert = RecipeData.RecipeInfo.IsExpert;
        var isCollectable = RecipeData.IsCollectable;
        var isAdjustable = RecipeData.AdjustedJobLevel.HasValue;
        var imageSize = ImGui.GetFrameHeight();

        var rightSideWidth =
            5 + textLevelSize +
            (!string.IsNullOrEmpty(textStars) ? ImGuiUtils.CalcBadgePillSize(textStars).X + 3 : 0) +
            (isAdjustable ? imageSize + 3 : 0) +
            (isCollectable ? ImGuiUtils.CalcBadgePillSize("Collectible").X + 3 : 0) +
            (isExpert ? ImGuiUtils.CalcBadgePillSize("Expert").X + 3 : 0);
        ImGui.AlignTextToFramePadding();

        ImGui.Image(_plugin.IconManager.GetIconCached(RecipeData.Recipe.ItemResult.Value.Icon).Handle, new Vector2(imageSize));

        ImGui.SameLine(0, 5);

        ushort? newRecipe = null;
        {
            var recipe = new RecipeWrapper(RecipeData.Recipe);
            using var lockedFontHandle = AxisFont.Available ? AxisFont.Lock() : null;
            var fontHandle = lockedFontHandle?.ImFont ?? ImGui.GetFont();
            global::ImGuiNET.ImFontPtr netFont;
            unsafe { netFont = new((nint)fontHandle.Handle); }
            if (ImGuiUtils.SearchableCombo(
                "combo",
                ref recipe,
                searchableRecipes,
                netFont,
                ImGui.GetContentRegionAvail().X - rightSideWidth,
                r => r.Recipe.ItemResult.Value.Name.ToString(),
                r => r.Recipe.RowId.ToString(),
                r =>
                {
                    ImGui.TextUnformatted($"{r.Recipe.ItemResult.Value.Name}");

                    var classJob = (ClassJob)r.Recipe.CraftType.RowId;
                    var textLevel = SqText.LevelPrefix.ToIconChar() + SqText.ToLevelString(r.Recipe.RecipeLevelTable.Value!.ClassJobLevel);
                    var textLevelSize = ImGui.CalcTextSize(textLevel);
                    ImGui.SameLine();

                    var imageSize = fontHandle.FontSize;
                    ImGuiUtils.AlignRight(
                        imageSize + 5 +
                        textLevelSize.X,
                        ImGui.GetContentRegionAvail().X);

                    var uv0 = UIConstants.ItemIconUv0;
                    var uv1 = UIConstants.ItemIconUv1;

                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetStyle().FramePadding.Y / 2);
                    ImGui.Image(_plugin.IconManager.GetIconCached(classJob.GetIconId()).Handle, new Vector2(imageSize), uv0, uv1);
                    ImGui.SameLine(0, 5);
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (fontHandle.FontSize - textLevelSize.Y) / 2);
                    ImGui.TextUnformatted(textLevel);
                }))
            {
                newRecipe = (ushort)recipe.Recipe.RowId;
            }
        }

        ushort? newAdjustedJobLevel = null;
        ImGui.SameLine(0, 5);
        if (adjustedJobLevel.HasValue)
        {
            var level = (int)adjustedJobLevel.Value;
            if (DrawLevelEntry(ref level))
            {
                newAdjustedJobLevel = (ushort)level;
            }
        }
        else
        {
            ImGui.TextUnformatted(textLevel);
        }

        if (!string.IsNullOrEmpty(textStars))
        {
            ImGui.SameLine(0, 3);
            ImGuiUtils.DrawBadgePill(textStars, Colors.ActionSpecial);
        }

        if (isAdjustable)
        {
            ImGui.SameLine(0, 3);
            ImGui.Image(CosmicExplorationBadge.Handle, new(imageSize));
            if (ImGui.IsItemHovered())
                ImGuiUtils.Tooltip($"Cosmic Exploration");

            if (_plugin.Configuration.EnableCosmicToolTracking && _cosmicProgress is { } cp)
            {
                var active = cp.Types[cp.ActiveType];
                ImGui.SameLine(0, 6);
                var frac = active.Needed > 0 ? (float)active.Current / active.Needed : 0f;
                ImGui.ProgressBar(frac, new Vector2(90 * ImGuiHelpers.GlobalScale, imageSize));
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip($"Research Data Type {ToRomanType(cp.ActiveType)}: {active.Current:N0} / {active.Needed:N0}");
            }
        }

        if (isCollectable)
        {
            ImGui.SameLine(0, 3);
            ImGuiUtils.DrawBadgePill("Collectible", Colors.Collectability);
        }

        if (isExpert)
        {
            ImGui.SameLine(0, 3);
            ImGuiUtils.DrawBadgePill("Expert", Colors.ActionSpecial);
        }

        using (var statsTable = ImRaii.Table("stats", 3, ImGuiTableFlags.BordersInnerV))
        {
            if (statsTable)
            {
                ImGui.TableSetupColumn("col1", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("col2", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("col3", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted("Progress");
                ImGui.SameLine();
                ImGuiUtils.TextRight($"{RecipeData.RecipeInfo.MaxProgress}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Quality");
                ImGui.SameLine();
                ImGuiUtils.TextRight($"{RecipeData.RecipeInfo.MaxQuality}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Durability");
                ImGui.SameLine();
                ImGuiUtils.TextRight($"{RecipeData.RecipeInfo.MaxDurability}");
            }
        }

        using (var table = ImRaii.Table("ingredientTable", 4, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
        {
            if (table)
            {
                ImGui.TableSetupColumn("col1", ImGuiTableColumnFlags.WidthStretch, 2);
                ImGui.TableSetupColumn("col2", ImGuiTableColumnFlags.WidthStretch, 2);
                ImGui.TableSetupColumn("col3", ImGuiTableColumnFlags.WidthStretch, 2);
                ImGui.TableSetupColumn("col4", ImGuiTableColumnFlags.WidthStretch, 2);

                ImGui.TableNextColumn();
                DrawIngredientHQEntry(0);
                DrawIngredientHQEntry(1);

                ImGui.TableNextColumn();
                DrawIngredientHQEntry(2);
                DrawIngredientHQEntry(3);

                ImGui.TableNextColumn();
                DrawIngredientHQEntry(4);
                DrawIngredientHQEntry(5);

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetStyle().FramePadding.Y);
                ImGuiUtils.TextCentered($"Starting Quality");
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ImGui.GetStyle().FramePadding.Y);
                ImGuiUtils.TextCentered($"{StartingQuality}");
            }
        }

        var modified = false;

        if (newAdjustedJobLevel is { } jobLevel)
        {
            RecipeData = new((ushort)RecipeData.Recipe.RowId, jobLevel);
            modified = true;
        }

        if (newRecipe is { } recipeId)
        {
            RecipeData = new(recipeId, RecipeData.AdjustedJobLevel);
            HQIngredientCounts.Clear();
            HQIngredientCounts.AddRange(Enumerable.Repeat(0, RecipeData.Ingredients.Count));
            modified = true;
        }

        if (oldStartingQuality != StartingQuality)
            modified = true;

        return modified;
    }

    private void DrawIngredientHQEntry(int idx)
    {
        if (idx >= RecipeData.Ingredients.Count)
        {
            ImGui.Dummy(new(0, ImGui.GetFrameHeight()));
            return;
        }

        var ingredient = RecipeData.Ingredients[idx];
        var hqCount = HQIngredientCounts[idx];

        var canHq = ingredient.Item.CanBeHq;
        var icon = _plugin.IconManager.GetIconCached(ingredient.Item.Icon, canHq);
        var imageSize = ImGui.GetFrameHeight();

        using (var d = ImRaii.Disabled(!canHq))
            ImGui.Image(icon.Handle, new Vector2(imageSize));
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            if (canHq)
            {
                var perItem = RecipeData.CalculateItemStartingQuality(idx, 1);
                var total = RecipeData.CalculateItemStartingQuality(idx, hqCount);
                ImGuiUtils.Tooltip($"{ingredient.Item.Name} {SeIconChar.HighQuality.ToIconString()}\n+{perItem} Quality/Item{(total > 0 ? $"\n+{total} Quality" : "")}");
            }
            else if (ingredient.Amount != 0)
                ImGuiUtils.Tooltip($"{ingredient.Item.Name}");
        }
        ImGui.SameLine(0, 5);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - (5 + ImGui.CalcTextSize("/").X + 5 + ImGui.CalcTextSize($"99").X));
        using var d2 = ImRaii.Disabled(!canHq);
        if (canHq)
        {
            var text = hqCount.ToString();
            if (ImGui.InputText($"##ingredient{idx}", ref text, 8, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal))
            {
                HQIngredientCounts[idx] =
                    int.TryParse(text, out var newCount)
                    ? Math.Clamp(newCount, 0, ingredient.Amount)
                    : 0;
            }
        }
        else
        {
            var text = ingredient.Amount.ToString();
            ImGui.InputText($"##ingredient{idx}", ref text, 8, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal);
        }
        ImGui.SameLine(0, 5);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("/");
        ImGui.SameLine(0, 5);
        ImGui.AlignTextToFramePadding();
        ImGuiUtils.TextCentered($"{ingredient.Amount}");
    }

    private const int MAX_LEVEL = 100;
    private static float GetLevelEntryWidth()
    {
        var levelTextWidth = ImGui.CalcTextSize(SqText.ToLevelString(MAX_LEVEL)).X + ImGui.GetStyle().FramePadding.X * 2 + 5;
        return ImGui.CalcTextSize(SqText.LevelPrefix.ToIconString()).X + 5 + levelTextWidth;
    }

    private static bool DrawLevelEntry(ref int level)
    {
        static int LevelInputCallback(ImGuiInputTextCallbackDataPtr data)
        {
            if (data.EventFlag == ImGuiInputTextFlags.CallbackCharFilter)
            {
                if (SqText.LevelNumReplacements.TryGetValue((char)data.EventChar, out var seChar))
                    data.EventChar = seChar.ToIconChar();
                else
                    return 1;
            }

            return 0;
        }

        var levelTextWidth = ImGui.CalcTextSize(SqText.ToLevelString(MAX_LEVEL)).X + ImGui.GetStyle().FramePadding.X * 2 + 5;

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(SqText.LevelPrefix.ToIconString());
        ImGui.SameLine(0, 3);
        ImGui.SetNextItemWidth(levelTextWidth);
        var levelText = SqText.ToLevelString(level);
        var textChanged = ImGui.InputText("##levelText", ref levelText, 12, ImGuiInputTextFlags.CallbackCharFilter | ImGuiInputTextFlags.AutoSelectAll, LevelInputCallback);
        if (textChanged)
        {
            var newLevel = SqText.TryParseLevelString(levelText, out var lv)
                    ? Math.Clamp(lv, 1, MAX_LEVEL)
                    : 1;
            if (newLevel != level)
            {
                level = newLevel;
                return true;
            }
        }
        return false;
    }

    private static string ToRomanType(int zeroBasedType) => (zeroBasedType + 1) switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV",
        5 => "V", 6 => "VI", 7 => "VII",
        _ => (zeroBasedType + 1).ToString()
    };
}
