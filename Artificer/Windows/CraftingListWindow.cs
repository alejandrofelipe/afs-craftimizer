using Artificer.Application.CraftingLists;
using Artificer.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
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
/// Main crafting-list browser. Shows pending and completed lists with fuzzy search,
/// sorting, pagination, and per-list context actions (rename / duplicate / merge / delete).
/// </summary>
public sealed class CraftingListWindow : Window, IDisposable
{
    private readonly PluginClass _plugin;
    private string _searchQuery = string.Empty;
    private bool _showOnlyPending;
    private int _page;
    private const int PageSize = 15;

    private enum SortMode { MostRecent, NameAZ, PercentComplete }
    private SortMode _sortMode;

    private bool _isDeletingId;
    private Guid _deleteTargetId;

    private bool _isRenaming;
    private Guid _renameTargetId;
    private string _renameBuffer = string.Empty;

    public CraftingListWindow(PluginClass plugin) : base("Crafting Lists###Artificer-cl-list",
        ImGuiWindowFlags.NoScrollbar)
    {
        _plugin = plugin;
        _plugin.WindowSystem.AddWindow(this);

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(420, 360),
            MaximumSize = new(float.PositiveInfinity)
        };
    }

    public new void Toggle() => IsOpen = !IsOpen;
    public void OpenAndFocus() { IsOpen = true; BringToFront(); }

    public override void PreDraw() => Theme.Push();
    public override void PostDraw() { Theme.Pop(); base.PostDraw(); }

    public override void Draw()
    {
        var scale = ImGuiHelpers.GlobalScale;

        // ── Header row ──────────────────────────────────────────────────────────
        Theme.PushPrimaryButton();
        if (ImGui.Button("+ Nova Lista"))
            CreateNewList();
        Theme.PopPrimaryButton();

        ImGui.SameLine();
        ImGui.SetNextItemWidth(180 * scale);
        ImGui.InputTextWithHint("##search", "Buscar...", ref _searchQuery, 128);

        ImGui.SameLine();
        ImGui.Checkbox("Só pendentes", ref _showOnlyPending);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(140 * scale);
        if (ImGui.BeginCombo("##sort", SortModeLabel(_sortMode)))
        {
            foreach (var mode in Enum.GetValues<SortMode>())
            {
                if (ImGui.Selectable(SortModeLabel(mode), _sortMode == mode))
                    _sortMode = mode;
            }
            ImGui.EndCombo();
        }

        ImGui.Separator();

        if (_plugin.CraftingListManager.Lists.Count == 0)
        {
            ImGuiUtils.DrawEmptyState(
                (int)FontAwesomeIcon.ListAlt,
                "Nenhuma lista",
                "Crie sua primeira lista de coleta",
                ("+ Nova Lista", CreateNewList));
            return;
        }

        var (pending, completed) = GetFilteredLists();

        // ── Pagination over the pending list ────────────────────────────────────
        var totalPages = Math.Max(1, (pending.Count + PageSize - 1) / PageSize);
        if (_page >= totalPages)
            _page = totalPages - 1;
        if (_page < 0)
            _page = 0;
        var pageItems = pending.Skip(_page * PageSize).Take(PageSize).ToList();

        using (var child = ImRaii.Child("##listScroll", new Vector2(-1, -ImGui.GetFrameHeightWithSpacing()), false))
        {
            if (child)
            {
                if (pageItems.Count == 0 && completed.Count == 0)
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                        ImGui.TextUnformatted("Nenhuma lista corresponde ao filtro.");
                }

                foreach (var list in pageItems)
                    DrawListRow(list);

                if (completed.Count > 0)
                {
                    ImGui.Separator();
                    using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                        ImGui.TextUnformatted("CONCLUÍDAS");
                    foreach (var list in completed)
                        DrawListRow(list);
                }
            }
        }

        // ── Pagination row ──────────────────────────────────────────────────────
        if (totalPages > 1)
        {
            using (ImRaii.Disabled(_page <= 0))
                if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.ChevronLeft))
                    _page--;
            ImGuiUtils.HoveredTooltip("Página anterior", (int)ImGuiHoveredFlags.AllowWhenDisabled);
            ImGui.SameLine();
            ImGui.TextUnformatted($"Página {_page + 1} / {totalPages}");
            ImGui.SameLine();
            using (ImRaii.Disabled(_page >= totalPages - 1))
                if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.ChevronRight))
                    _page++;
            ImGuiUtils.HoveredTooltip("Próxima página", (int)ImGuiHoveredFlags.AllowWhenDisabled);
        }

        DrawDeleteModal();
        DrawRenameModal();
    }

    // ── Row rendering ──────────────────────────────────────────────────────────

    private void DrawListRow(CraftingList list)
    {
        var pct = ComputeListProgress(list);
        var pctText = $"{(int)(pct * 100)}%";
        var recipeCount = _plugin.CraftingListRepository.GetRecipesForList(list.Id).Count;
        var scale = ImGuiHelpers.GlobalScale;

        using var id = ImRaii.PushId(list.Id.ToString());

        // ── Linha 1: nome (abre detalhe) + menu de contexto + tempo (right) ──
        if (ImGui.Selectable($"{list.Name}##sel", false, ImGuiSelectableFlags.None,
                new Vector2(0, ImGui.GetFrameHeight())))
            _plugin.CraftingListDetailWindow.OpenList(list.Id);

        if (ImGui.BeginPopupContextItem($"##ctx_{list.Id}"))
        {
            if (ImGui.MenuItem("Abrir"))
                _plugin.CraftingListDetailWindow.OpenList(list.Id);
            if (ImGui.MenuItem("Renomear"))
            {
                _isRenaming = true;
                _renameTargetId = list.Id;
                _renameBuffer = list.Name;
            }
            if (ImGui.MenuItem("Duplicar"))
                _ = _plugin.CraftingListManager.DuplicateListAsync(list.Id);
            ImGui.Separator();
            if (ImGui.MenuItem("Mesclar com..."))
                _plugin.CraftingListMergeWindow.OpenWithSource(list.Id);
            ImGui.Separator();
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.Bad))
                if (ImGui.MenuItem("Deletar"))
                {
                    _deleteTargetId = list.Id;
                    _isDeletingId = true;
                }
            ImGui.EndPopup();
        }

        // tempo relativo, right-aligned ao fim da linha (cursor-relativo: alinha na borda da janela)
        var timeText = RelativeTime(list.UpdatedAt);
        ImGui.SameLine();
        var avail = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, avail - ImGui.CalcTextSize(timeText).X));
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted(timeText);

        // ── Linha 2: barra de progresso + % + N receitas ──
        using (ImRaii.PushColor(ImGuiCol.PlotHistogram, list.CompletedAt.HasValue ? Colors.Progress : Colors.Quality))
            ImGuiUtils.ProgressBar(pct, new Vector2(-1, 5 * scale));
        using (ImRaii.PushColor(ImGuiCol.Text,
            list.CompletedAt.HasValue ? Colors.Progress : pct > 0 ? Vector4.One : Colors.TextMuted))
            ImGui.TextUnformatted(pctText);
        ImGui.SameLine(0, 6 * scale);
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted($"· {recipeCount} receitas");

        ImGui.Dummy(new Vector2(0, 3 * scale));
    }

    private void DrawDeleteModal()
    {
        if (_isDeletingId)
        {
            ImGui.OpenPopup("##DeleteConfirm");
            _isDeletingId = false;
        }

        var open = true;
        using var popup = ImRaii.PopupModal("##DeleteConfirm", ref open,
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);
        if (!popup)
            return;

        var target = _plugin.CraftingListManager.Lists.FirstOrDefault(l => l.Id == _deleteTargetId);
        ImGui.TextUnformatted($"Deletar \"{target?.Name ?? "lista"}\"?");
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted("Esta ação não pode ser desfeita.");
        ImGui.Separator();

        if (ImGui.Button("Cancelar"))
            ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        Theme.PushDangerButton();
        if (ImGui.Button("Deletar"))
        {
            _ = _plugin.CraftingListManager.DeleteListAsync(_deleteTargetId);
            ImGui.CloseCurrentPopup();
        }
        Theme.PopDangerButton();
    }

    private void DrawRenameModal()
    {
        if (_isRenaming)
        {
            ImGui.OpenPopup("##RenameList");
            _isRenaming = false;
        }

        var open = true;
        using var popup = ImRaii.PopupModal("##RenameList", ref open,
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);
        if (!popup)
            return;

        ImGui.TextUnformatted("Novo nome:");
        ImGui.SetNextItemWidth(260 * ImGuiHelpers.GlobalScale);
        var enter = ImGui.InputText("##renameInput", ref _renameBuffer, 128,
            ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.Separator();

        if (ImGui.Button("Cancelar"))
            ImGui.CloseCurrentPopup();
        ImGui.SameLine();
        Theme.PushPrimaryButton();
        var confirm = ImGui.Button("Salvar");
        Theme.PopPrimaryButton();
        if ((confirm || enter) && !string.IsNullOrWhiteSpace(_renameBuffer))
        {
            _ = _plugin.CraftingListManager.RenameListAsync(_renameTargetId, _renameBuffer.Trim());
            ImGui.CloseCurrentPopup();
        }
    }

    // ── Data helpers ───────────────────────────────────────────────────────────

    private void CreateNewList()
    {
        var baseName = "Nova Lista";
        var existing = _plugin.CraftingListManager.Lists.Select(l => l.Name).ToHashSet();
        var name = baseName;
        var n = 2;
        while (existing.Contains(name))
            name = $"{baseName} {n++}";
        _ = _plugin.CraftingListManager.CreateListAsync(name);
    }

    private float ComputeListProgress(CraftingList list)
    {
        var progresses = _plugin.CraftingListRepository.GetProgressForList(list.Id);
        if (progresses.Count == 0)
            return 0f;
        var needed = progresses.Sum(p => p.QuantityNeeded);
        if (needed == 0)
            return 1f;
        return Math.Clamp((float)progresses.Sum(p => p.QuantityCollected) / needed, 0f, 1f);
    }

    private (List<CraftingList> Pending, List<CraftingList> Completed) GetFilteredLists()
    {
        var all = _plugin.CraftingListManager.Lists.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(_searchQuery))
            all = all.Where(l => FuzzyMatch(l.Name, _searchQuery));
        if (_showOnlyPending)
            all = all.Where(l => !l.CompletedAt.HasValue);
        var sorted = _sortMode switch
        {
            SortMode.NameAZ => all.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase),
            SortMode.PercentComplete => all.OrderByDescending(ComputeListProgress),
            _ => all.OrderByDescending(l => l.UpdatedAt)
        };
        var list = sorted.ToList();
        return (list.Where(l => !l.CompletedAt.HasValue).ToList(),
                list.Where(l => l.CompletedAt.HasValue).ToList());
    }

    private static string SortModeLabel(SortMode mode) => mode switch
    {
        SortMode.NameAZ => "Nome A→Z",
        SortMode.PercentComplete => "% Completo",
        _ => "Mais recente"
    };

    private static bool FuzzyMatch(string haystack, string needle)
    {
        var hi = 0;
        foreach (var c in needle.ToLowerInvariant())
        {
            var found = false;
            while (hi < haystack.Length)
                if (char.ToLowerInvariant(haystack[hi++]) == c) { found = true; break; }
            if (!found)
                return false;
        }
        return true;
    }

    private static string RelativeTime(DateTime dt)
    {
        var elapsed = DateTime.UtcNow - dt.ToUniversalTime();
        if (elapsed.TotalMinutes < 1) return "agora";
        if (elapsed.TotalHours < 1) return $"{(int)elapsed.TotalMinutes} min atrás";
        if (elapsed.TotalDays < 1) return $"{(int)elapsed.TotalHours}h atrás";
        if (elapsed.TotalDays < 2) return "ontem";
        return $"há {(int)elapsed.TotalDays} dias";
    }

    public void Dispose()
    {
        _plugin.WindowSystem.RemoveWindow(this);
    }
}
