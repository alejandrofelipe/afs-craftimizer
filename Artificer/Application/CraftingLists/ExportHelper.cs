using Dalamud.Bindings.ImGui;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Artificer.Application.CraftingLists;

/// <summary>
/// Produces human-readable text exports of a crafting list's materials,
/// either in full or filtered to only the items still missing.
/// </summary>
public static class ExportHelper
{
    public static string ToFullList(
        CraftingList list,
        IReadOnlyList<ResolvedIngredient> materials,
        IReadOnlyDictionary<uint, MaterialProgress> progress)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== {list.Name} ===");

        void AppendSection(string header, IEnumerable<ResolvedIngredient> items)
        {
            var list = items.ToList();
            if (list.Count == 0)
                return;
            sb.AppendLine(header);
            foreach (var m in list)
            {
                progress.TryGetValue(m.ItemId, out var p);
                var collected = p?.QuantityCollected ?? 0;
                var suffix = collected > 0 ? $"  (coletado: {collected} de {m.Quantity})" : string.Empty;
                sb.AppendLine($"  {m.ItemName} × {m.Quantity}{suffix}");
            }
        }

        AppendSection("MATERIAIS BASE:", materials.Where(m => m.Kind == IngredientKind.BaseMaterial));
        AppendSection("CRISTAIS:", materials.Where(m => m.Kind == IngredientKind.Crystal));
        AppendSection("PRÉ-CRAFTS:", materials.Where(m => m.Kind == IngredientKind.PreCraft));
        return sb.ToString();
    }

    public static string ToMissingOnly(
        CraftingList list,
        IReadOnlyList<ResolvedIngredient> materials,
        IReadOnlyDictionary<uint, MaterialProgress> progress)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== {list.Name} ===");

        // Filter to incomplete only; show remaining quantity.
        void AppendSection(string header, IEnumerable<ResolvedIngredient> items)
        {
            var filtered = items.Where(m =>
            {
                progress.TryGetValue(m.ItemId, out var p);
                return p == null || !p.IsComplete;
            }).ToList();
            if (filtered.Count == 0)
                return;
            sb.AppendLine(header);
            foreach (var m in filtered)
            {
                progress.TryGetValue(m.ItemId, out var p);
                var remaining = p == null ? m.Quantity : m.Quantity - p.QuantityCollected;
                if (remaining <= 0)
                    continue;
                var suffix = p != null && p.QuantityCollected > 0
                    ? $"  (coletado: {p.QuantityCollected} de {m.Quantity})"
                    : string.Empty;
                sb.AppendLine($"  {m.ItemName} × {remaining}{suffix}");
            }
        }

        AppendSection("MATERIAIS BASE:", materials.Where(m => m.Kind == IngredientKind.BaseMaterial));
        AppendSection("CRISTAIS:", materials.Where(m => m.Kind == IngredientKind.Crystal));
        AppendSection("PRÉ-CRAFTS:", materials.Where(m => m.Kind == IngredientKind.PreCraft));
        return sb.ToString();
    }

    public static void CopyToClipboard(string text) => ImGui.SetClipboardText(text);
}
