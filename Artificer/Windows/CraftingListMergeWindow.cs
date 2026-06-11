using Artificer.Application.CraftingLists;
using Artificer.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using PluginClass = Artificer.Plugin.Plugin;

namespace Artificer.Windows;

/// <summary>
/// Modal window to merge one crafting list's recipes into another, optionally
/// deleting the source afterwards.
/// </summary>
public sealed class CraftingListMergeWindow : Window, IDisposable
{
    private readonly PluginClass _plugin;

    private Guid _sourceId;
    private Guid? _destId;
    private bool _deleteSource = true;

    public CraftingListMergeWindow(PluginClass plugin) : base("Mesclar Listas###Artificer-cl-merge",
        ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar)
    {
        _plugin = plugin;
        _plugin.WindowSystem.AddWindow(this);
    }

    public void OpenWithSource(Guid sourceListId)
    {
        _sourceId = sourceListId;
        _destId = null;
        _deleteSource = true;
        IsOpen = true;
        BringToFront();
    }

    public override void PreDraw() => Theme.Push();
    public override void PostDraw() { Theme.Pop(); base.PostDraw(); }

    public override void Draw()
    {
        var scale = ImGuiHelpers.GlobalScale;

        var source = _plugin.CraftingListManager.Lists.FirstOrDefault(l => l.Id == _sourceId);
        if (source == null)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.Bad))
                ImGui.TextUnformatted("Lista origem não encontrada.");
            if (ImGui.Button("Fechar"))
                IsOpen = false;
            return;
        }

        ImGui.TextUnformatted("Origem:");
        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.ActionBuff))
            ImGui.TextUnformatted(source.Name);

        ImGui.Separator();
        ImGui.TextUnformatted("Mesclar com:");

        var candidates = _plugin.CraftingListManager.Lists
            .Where(l => l.Id != _sourceId)
            .ToList();

        using (var box = ImRaii.ListBox("##destList", new Vector2(300 * scale, 140 * scale)))
        {
            if (box)
            {
                if (candidates.Count == 0)
                    ImGuiUtils.DrawEmptyState(
                        (int)FontAwesomeIcon.ListAlt,
                        "Nenhuma lista disponível",
                        "Crie outra lista antes de mesclar.");
                else
                    foreach (var list in candidates)
                    {
                        if (ImGui.Selectable(list.Name, _destId == list.Id))
                            _destId = list.Id;
                    }
            }
        }

        // ── Preview ─────────────────────────────────────────────────────────────
        if (_destId is { } destId)
        {
            ImGui.Separator();
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGui.TextUnformatted("Pré-visualização:");

            var preview = BuildPreview(_sourceId, destId);
            foreach (var line in preview.Take(8))
                ImGui.TextUnformatted($"  {line}");
            if (preview.Count > 8)
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                    ImGui.TextUnformatted($"  … e mais {preview.Count - 8} receita(s)");
        }

        ImGui.Separator();
        ImGui.Checkbox("Excluir lista origem após mesclar", ref _deleteSource);
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.Bad))
            ImGui.TextUnformatted("Esta ação não pode ser desfeita.");

        ImGui.Separator();
        if (ImGui.Button("Cancelar"))
            IsOpen = false;
        ImGui.SameLine();
        using (ImRaii.Disabled(!_destId.HasValue))
        {
            Theme.PushPrimaryButton();
            if (ImGui.Button("⊕ Mesclar") && _destId.HasValue)
            {
                _ = _plugin.CraftingListManager.MergeListsAsync(_sourceId, _destId.Value, _deleteSource);
                PluginClass.DisplayNotification(new()
                {
                    Content = "Listas mescladas com sucesso.",
                    Title = "Lista de Coleta",
                    Type = NotificationType.Success
                });
                IsOpen = false;
            }
            Theme.PopPrimaryButton();
        }
    }

    private List<string> BuildPreview(Guid srcId, Guid destId)
    {
        var src = _plugin.CraftingListRepository.GetRecipesForList(srcId);
        var dst = _plugin.CraftingListRepository.GetRecipesForList(destId);
        var combined = new Dictionary<uint, (string Name, int Qty)>();

        foreach (var r in dst.Concat(src))
        {
            var name = LookupItemName(r.ItemId);
            if (combined.TryGetValue(r.RecipeId, out var existing))
                combined[r.RecipeId] = (existing.Name, existing.Qty + r.Quantity);
            else
                combined[r.RecipeId] = (name, r.Quantity);
        }

        return combined.Values
            .Select(v => $"{v.Name} ×{v.Qty}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string LookupItemName(uint itemId) =>
        _plugin.RecipeSearchHelper.Index.FirstOrDefault(r => r.ItemId == itemId)?.ItemName
        ?? $"Item #{itemId}";

    public void Dispose()
    {
        _plugin.WindowSystem.RemoveWindow(this);
    }
}
