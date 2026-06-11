using Artificer.Application.CraftingLists;
using Artificer.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using PluginClass = Artificer.Plugin.Plugin;

namespace Artificer.Windows;

/// <summary>
/// Modal-style window for adding a recipe to a crafting list. Supports two modes:
/// auto-detecting a recipe from the current context, or a manual recipe search.
/// </summary>
public sealed class CraftingListAddWindow : Window, IDisposable
{
    public enum AddMode { AutoDetect, ManualSearch }

    private readonly PluginClass _plugin;


    private uint? _prefilledRecipeId;
    private Guid? _targetListId;
    private RecipeSearchResult? _selectedRecipe;
    private int _quantityBuffer = 1;
    private bool _focusSearch;
    private bool _newListMode;
    private string _newListName = string.Empty;
    private string _newListSelected = string.Empty;

    public CraftingListAddWindow(PluginClass plugin) : base("Adicionar Receita###Artificer-cl-add",
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize)
    {
        _plugin = plugin;
        _plugin.WindowSystem.AddWindow(this);
    }

    public void OpenWithRecipe(uint recipeId)
    {
        _prefilledRecipeId = recipeId;
        _targetListId = null;
        _selectedRecipe = _plugin.RecipeSearchHelper.Index.FirstOrDefault(r => r.RecipeId == recipeId);
        _quantityBuffer = 1;
        ResetListSelector();
        IsOpen = true;
        BringToFront();
    }

    public void OpenManualSearch(Guid? targetListId)
    {
        _prefilledRecipeId = null;
        _targetListId = targetListId;
        _selectedRecipe = null;
        _quantityBuffer = 1;
        ResetListSelector();
        IsOpen = true;
        BringToFront();
        _focusSearch = true;
    }

    private void ResetListSelector()
    {
        if (_targetListId is { } id)
        {
            var target = _plugin.CraftingListManager.Lists.FirstOrDefault(l => l.Id == id);
            if (target != null)
            {
                _newListMode = false;
                _newListSelected = target.Name;
                return;
            }
        }

        if (_plugin.CraftingListManager.Lists.Count == 0)
        {
            _newListMode = true;
            _newListName = "Nova Lista";
        }
        else
        {
            _newListMode = false;
            _newListSelected = _plugin.CraftingListManager.Lists[0].Name;
        }
    }

    public override void PreDraw() => Theme.Push();
    public override void PostDraw() { Theme.Pop(); base.PostDraw(); }

    public override void Draw()
    {
        var scale = ImGuiHelpers.GlobalScale;

        // ── Recipe selection ────────────────────────────────────────────────────
        ImGui.TextUnformatted("Receita");
        var selected = _selectedRecipe ?? _plugin.RecipeSearchHelper.Index[0];
        if (_focusSearch)
        {
            ImGui.SetKeyboardFocusHere();
            _focusSearch = false;
        }
        if (ImGuiUtils.SearchableCombo(
            "##recipeSearch",
            ref selected,
            _plugin.RecipeSearchHelper.Index,
            UiServices.Current.DefaultFont,
            320 * scale,
            r => r.ItemName,
            r => r.RecipeId.ToString(),
            r =>
            {
                ImGui.TextUnformatted(r.ItemName);
                ImGui.SameLine();
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                    ImGui.TextUnformatted($"{r.JobAbbrev} · Lv{r.Level}");
            }))
        {
            _selectedRecipe = selected;
        }

        // ── Restrictions ────────────────────────────────────────────────────────
        if (_selectedRecipe is { } recipe)
        {
            var restrictions = _plugin.RecipeRestrictionChecker.GetRestrictions(recipe.RecipeId);
            foreach (var r in restrictions)
            {
                ImGuiUtils.DrawBadgePill(r.Title, Colors.Durability);
                if (ImGui.IsItemHovered())
                    ImGuiUtils.TooltipWrapped($"{r.Detail}\n{r.HowToDetail}".Trim());
            }
        }

        ImGui.Separator();

        // ── Quantity ────────────────────────────────────────────────────────────
        ImGui.TextUnformatted("Quantidade");
        ImGui.SetNextItemWidth(120 * scale);
        if (ImGui.InputInt("##qty", ref _quantityBuffer))
            _quantityBuffer = Math.Clamp(_quantityBuffer, 1, 999);

        // ── List selector ───────────────────────────────────────────────────────
        ImGui.Separator();
        ImGui.TextUnformatted("Adicionar à lista");

        if (_plugin.CraftingListManager.Lists.Count == 0)
        {
            _newListMode = true;
            ImGui.SetNextItemWidth(280 * scale);
            ImGui.InputTextWithHint("##newListName", "Nome da nova lista", ref _newListName, 128);
        }
        else
        {
            if (ImGui.RadioButton("Nova lista", _newListMode))
                _newListMode = true;
            ImGui.SameLine();
            if (ImGui.RadioButton("Existente", !_newListMode))
                _newListMode = false;

            if (_newListMode)
            {
                ImGui.SetNextItemWidth(280 * scale);
                ImGui.InputTextWithHint("##newListName", "Nome da nova lista", ref _newListName, 128);
            }
            else
            {
                ImGui.SetNextItemWidth(280 * scale);
                if (ImGui.BeginCombo("##existingList", _newListSelected))
                {
                    foreach (var list in _plugin.CraftingListManager.Lists)
                    {
                        if (ImGui.Selectable(list.Name, list.Name == _newListSelected))
                            _newListSelected = list.Name;
                    }
                    ImGui.EndCombo();
                }
            }
        }

        // ── Footer ──────────────────────────────────────────────────────────────
        ImGui.Separator();
        if (ImGui.Button("Cancelar"))
            IsOpen = false;
        ImGui.SameLine();

        var canAdd = _selectedRecipe != null &&
            (!_newListMode || !string.IsNullOrWhiteSpace(_newListName));
        using (ImRaii.Disabled(!canAdd))
        {
            Theme.PushPrimaryButton();
            if (ImGui.Button("✓ Adicionar") && canAdd)
                _ = ConfirmAddAsync();
            Theme.PopPrimaryButton();
        }
    }

    private async Task ConfirmAddAsync()
    {
        if (_selectedRecipe is not { } recipe)
            return;

        Guid listId;
        if (_newListMode)
        {
            if (string.IsNullOrWhiteSpace(_newListName))
                return;
            var newList = await _plugin.CraftingListManager.CreateListAsync(_newListName.Trim()).ConfigureAwait(false);
            listId = newList.Id;
        }
        else
        {
            var existing = _plugin.CraftingListManager.Lists
                .FirstOrDefault(l => l.Name == _newListSelected);
            if (existing == null)
                return;
            listId = existing.Id;
        }

        await _plugin.CraftingListManager.AddRecipeToListAsync(listId, recipe.RecipeId, _quantityBuffer).ConfigureAwait(false);
        IsOpen = false;
    }

    public void Dispose()
    {
        _plugin.WindowSystem.RemoveWindow(this);
    }
}
