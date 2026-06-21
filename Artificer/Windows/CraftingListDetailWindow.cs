using Artificer.Application.CraftingLists;
using Artificer.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using PluginClass = Artificer.Plugin.Plugin;
using Service = Artificer.Plugin.Service;

namespace Artificer.Windows;

/// <summary>
/// Detail view of a single crafting list: editable recipes, resolved materials,
/// crystals, pre-crafts, inventory sync, market prices, export, and split/move.
/// </summary>
public sealed class CraftingListDetailWindow : Window, IDisposable
{
    private readonly PluginClass _plugin;

    private Guid? _listId;
    private CraftingList? _list;
    private List<CraftingListRecipe> _recipes = new();
    private ResolvedIngredientTree? _tree;
    private Dictionary<uint, MaterialProgress> _progress = new();
    private readonly Dictionary<uint, MarketPrice?> _prices = new();
    private bool _pricesLoading;
    private bool _treeLoading;
    private DateTime? _lastSyncTime;

    // Quantity debounce: recipeId -> (pending value, last-changed game time)
    private readonly Dictionary<Guid, (int Value, double LastChanged)> _quantityEdits = new();

    // Inline remove confirmation: set to recipe id when ✕ is first clicked
    private Guid? _pendingRemoveId;

    // Selection mode (for split / move)
    private bool _selectionMode;
    private readonly HashSet<Guid> _selectedRecipeIds = new();
    private string _moveTargetSelected = string.Empty;

    // Inline rename
    private bool _isRenaming;
    private string _renameBuffer = string.Empty;

    public CraftingListDetailWindow(PluginClass plugin) : base("###Artificer-cl-detail",
        ImGuiWindowFlags.NoScrollbar)
    {
        _plugin = plugin;
        _plugin.WindowSystem.AddWindow(this);
        _plugin.CraftingListManager.ListsChanged += OnListsChanged;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(460, 420),
            MaximumSize = new(float.PositiveInfinity)
        };
    }

    public void OpenList(Guid listId)
    {
        _listId = listId;
        _selectionMode = false;
        _selectedRecipeIds.Clear();
        _isRenaming = false;
        _pendingRemoveId = null;
        RefreshData();
        IsOpen = true;
        BringToFront();
        if (_plugin.Configuration.AutoSyncInventoryOnOpen)
            _ = SyncInventoryAsync();
    }

    private void RefreshData()
    {
        if (_listId is not { } id)
            return;
        _list = _plugin.CraftingListManager.Lists.FirstOrDefault(l => l.Id == id);
        _recipes = _plugin.CraftingListRepository.GetRecipesForList(id);
        var progressList = _plugin.CraftingListRepository.GetProgressForList(id);
        _progress = progressList.ToDictionary(p => p.ItemId);
        _ = RefreshTreeAsync();
    }

    private void OnListsChanged()
    {
        if (_listId is not { } id)
            return;
        // Lista deletada externamente → fechar; senão, recarregar (conserta "não atualiza após add").
        if (_plugin.CraftingListManager.Lists.All(l => l.Id != id))
        {
            IsOpen = false;
            return;
        }
        RefreshData();
    }

    private async Task RefreshTreeAsync()
    {
        if (_listId is not { } id)
            return;
        _treeLoading = true;
        try
        {
            _tree = await _plugin.CraftingListManager.ResolveIngredientsAsync(id).ConfigureAwait(false);
            if (_plugin.Configuration.ShowMarketPrices)
                _ = LoadPricesAsync();
        }
        finally
        {
            _treeLoading = false;
        }
    }

    private async Task SyncInventoryAsync()
    {
        if (_listId is not { } id)
            return;
        await _plugin.CraftingListManager.SyncWithInventoryAsync(id, _plugin.Configuration.IncludeRetainersInSync).ConfigureAwait(false);
        _lastSyncTime = DateTime.UtcNow;
        RefreshData();
    }

    private async Task LoadPricesAsync()
    {
        if (_tree == null)
            return;
        var player = Service.Objects.LocalPlayer;
        if (player == null)
            return;
        var worldId = player.CurrentWorld.RowId;
        // GetPriceAsync falls back to the player's world when no data-center name is provided.
        var dcName = string.Empty;

        _pricesLoading = true;
        try
        {
            var items = _tree.BaseMaterials.Concat(_tree.Crystals).Select(m => m.ItemId).Distinct().ToList();
            foreach (var itemId in items)
            {
                if (_prices.ContainsKey(itemId))
                    continue;
                var price = await _plugin.MarketboardHelper.GetPriceAsync(
                    itemId, worldId, dcName, _plugin.Configuration.MarketPriceCacheTtlMinutes).ConfigureAwait(false);
                _prices[itemId] = price;
            }
        }
        finally
        {
            _pricesLoading = false;
        }
    }

    private void FlushPendingEdits()
    {
        var now = ImGui.GetTime();
        foreach (var (id, (value, changedAt)) in _quantityEdits.ToList())
        {
            if (now - changedAt >= 0.3)
            {
                _ = _plugin.CraftingListManager.UpdateRecipeQuantityAsync(id, value);
                _quantityEdits.Remove(id);
                RefreshData();
            }
        }
    }

    public override void PreDraw()
    {
        Theme.Push();
        WindowName = $"{(_list?.Name ?? "Lista")}###Artificer-cl-detail";
    }

    public override void PostDraw() { Theme.Pop(); base.PostDraw(); }

    public override void Draw()
    {
        if (_listId is not { } || _list is not { } list)
        {
            ImGuiUtils.DrawEmptyState((int)FontAwesomeIcon.ListAlt, "Nenhuma lista aberta", null);
            return;
        }

        FlushPendingEdits();

        DrawHeader(list);
        ImGui.Separator();

        if (_plugin.Configuration.CraftingListViewMode == 1)
            DrawSimpleView();
        else
            DrawDetailedView();
    }

    // ── Header ─────────────────────────────────────────────────────────────────

    private void DrawHeader(CraftingList list)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowLeft = ImGui.GetCursorPosX();
        var fullW = ImGui.GetContentRegionAvail().X;

        // ── Linha 1: nome (2× clique renomeia) + toggle de visão + kebab ──
        if (_isRenaming)
        {
            ImGui.SetNextItemWidth(240 * scale);
            var enter = ImGui.InputText("##renameDetail", ref _renameBuffer, 128,
                ImGuiInputTextFlags.EnterReturnsTrue);
            if (enter && !string.IsNullOrWhiteSpace(_renameBuffer))
            {
                _ = _plugin.CraftingListManager.RenameListAsync(list.Id, _renameBuffer.Trim());
                _isRenaming = false;
                RefreshData();
            }
        }
        else
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(list.Name);
            if (ImGui.IsItemHovered())
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsItemClicked() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                _isRenaming = true;
                _renameBuffer = list.Name;
            }
        }

        // toggle segmentado [Detalhada | Simples] + kebab, right-aligned na borda do conteúdo.
        // rowLeft + fullW = X absoluto da borda direita do conteúdo (capturado no topo, antes do nome).
        var detailed = _plugin.Configuration.CraftingListViewMode == 0;
        var style = ImGui.GetStyle();
        var toggleW = ImGui.CalcTextSize("Detalhada").X + ImGui.CalcTextSize("Simples").X
                      + style.FramePadding.X * 4 + style.ItemSpacing.X;
        var kebabW = ImGui.GetFrameHeight();
        var rightW = toggleW + style.ItemSpacing.X + kebabW;
        ImGui.SameLine();
        ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX(), rowLeft + fullW - rightW));

        DrawViewToggleButton("Detalhada", detailed, 0);
        ImGui.SameLine(0, style.ItemSpacing.X);
        DrawViewToggleButton("Simples", !detailed, 1);

        ImGui.SameLine();
        if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.EllipsisV))
            ImGui.OpenPopup("##clMore");
        ImGuiUtils.HoveredTooltip("Mais opções");
        using (var more = ImRaii.Popup("##clMore"))
        {
            if (more)
            {
                if (ImGui.BeginMenu("Exportar"))
                {
                    var mats = AllMaterials();
                    if (ImGui.MenuItem("Lista completa"))
                        ExportHelper.CopyToClipboard(ExportHelper.ToFullList(list, mats, _progress));
                    if (ImGui.MenuItem("Apenas faltantes"))
                        ExportHelper.CopyToClipboard(ExportHelper.ToMissingOnly(list, mats, _progress));
                    ImGui.EndMenu();
                }
                if (ImGui.MenuItem("Sincronizar inventário"))
                    _ = SyncInventoryAsync();
                if (ImGui.MenuItem("🗺 Abrir Rota de Coleta"))
                    _plugin.CraftingListGatheringWindow.OpenForList(list.Id);
                if (_plugin.Configuration.ShowMarketPrices && ImGui.MenuItem("Atualizar preços"))
                {
                    _prices.Clear();
                    _ = LoadPricesAsync();
                }
                if (ImGui.MenuItem("Renomear"))
                {
                    _isRenaming = true;
                    _renameBuffer = list.Name;
                }
                if (ImGui.MenuItem("Duplicar"))
                    _ = _plugin.CraftingListManager.DuplicateListAsync(list.Id);
            }
        }

        // ── Linha 2: barra de progresso + % ──
        var overall = ComputeOverallFraction();
        using (ImRaii.PushColor(ImGuiCol.PlotHistogram, overall >= 1f ? Colors.Progress : Colors.Quality))
            ImGuiUtils.ProgressBar(overall, new Vector2(-1, 8 * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, overall >= 1f ? Colors.Progress : Colors.TextMuted))
            ImGui.TextUnformatted($"{(int)(overall * 100)}%");
    }

    private void DrawViewToggleButton(string label, bool active, int mode)
    {
        if (active) Theme.PushPrimaryButton();
        if (ImGui.Button(label) && !active)
        {
            _plugin.Configuration.CraftingListViewMode = mode;
            _plugin.Configuration.Save();
        }
        if (active) Theme.PopPrimaryButton();
    }

    // ── Detailed view ──────────────────────────────────────────────────────────

    private void DrawDetailedView()
    {
        // Estimated cost line
        if (_plugin.Configuration.ShowMarketPrices)
            DrawCostLine();

        using (var child = ImRaii.Child("##detailScroll", new Vector2(-1, -ImGui.GetFrameHeightWithSpacing()), false))
        {
            if (!child)
                return;

            DrawRecipesPanel();

            if (_treeLoading && _tree == null)
            {
                ImGuiUtils.DrawStateChip(ImGuiUtils.SolverState.Solving, "Resolvendo materiais...");
                return;
            }

            if (_tree == null)
                return;

            if (_tree.BaseMaterials.Count > 0)
                using (ImRaii2.GroupPanel("Materiais Base", -1, out _))
                {
                    foreach (var mat in OrderByProgress(_tree.BaseMaterials))
                        DrawMaterialRow(mat);
                }

            if (_tree.Crystals.Count > 0)
                using (ImRaii2.GroupPanel("Cristais", -1, out _))
                {
                    foreach (var mat in OrderByProgress(_tree.Crystals))
                        DrawMaterialRow(mat);
                }

            if (_tree.PreCrafts.Count > 0)
                using (ImRaii2.GroupPanel("Pré-crafts", -1, out _))
                {
                    foreach (var pc in OrderByProgress(_tree.PreCrafts))
                        DrawPreCraftRow(pc);
                }
        }

        DrawFooter();
    }

    private void DrawRecipesPanel()
    {
        using (ImRaii2.GroupPanel("Receitas", -1, out _))
        {
            if (_recipes.Count == 0)
                ImGuiUtils.DrawEmptyState(
                    (int)FontAwesomeIcon.PlusCircle,
                    "Lista vazia",
                    "Adicione receitas para começar.");

            foreach (var recipe in _recipes)
            {
                using var id = ImRaii.PushId(recipe.Id.ToString());
                var rowLeft = ImGui.GetCursorPosX();

                if (_selectionMode)
                {
                    var selected = _selectedRecipeIds.Contains(recipe.Id);
                    if (ImGui.Checkbox("##sel", ref selected))
                    {
                        if (selected) _selectedRecipeIds.Add(recipe.Id);
                        else _selectedRecipeIds.Remove(recipe.Id);
                    }
                    ImGui.SameLine();
                }

                var qty = _quantityEdits.TryGetValue(recipe.Id, out var pending)
                    ? pending.Value
                    : recipe.Quantity;
                ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
                if (ImGui.InputInt("##qty", ref qty))
                {
                    qty = Math.Clamp(qty, 1, 999);
                    _quantityEdits[recipe.Id] = (qty, ImGui.GetTime());
                }

                ImGui.SameLine(0, 6 * ImGuiHelpers.GlobalScale);
                PluginImGuiUtils.DrawItemIcon(recipe.ItemId, ImGui.GetFrameHeight());
                ImGui.SameLine(0, 6 * ImGuiHelpers.GlobalScale);
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(LookupItemName(recipe.ItemId));

                var restrictions = _plugin.RecipeRestrictionChecker.GetRestrictions(recipe.RecipeId);
                if (restrictions.Count > 0)
                {
                    ImGui.SameLine();
                    using (ImRaii.PushFont(UiBuilder.IconFont))
                    using (ImRaii.PushColor(ImGuiCol.Text, Colors.Durability))
                        ImGui.TextUnformatted(FontAwesomeIcon.ExclamationCircle.ToIconString());
                    ImGuiUtils.HoveredTooltip(string.Join('\n', restrictions.Select(r => r.Title)), wrapWidth: 300);
                }

                // Right-align à prova de overflow: âncora explícita na borda interna do painel.
                var innerW = ImGuiUtils.CurrentContentWidth ?? ImGui.GetContentRegionAvail().X;
                if (_pendingRemoveId == recipe.Id)
                {
                    var confirmW = ImGui.CalcTextSize("Remover?").X
                        + ImGui.CalcTextSize("Sim").X + ImGui.CalcTextSize("Não").X
                        + ImGui.GetStyle().FramePadding.X * 4
                        + ImGui.GetStyle().ItemSpacing.X * 2;
                    ImGui.SameLine();
                    ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX(), rowLeft + innerW - confirmW));
                    using (ImRaii.PushColor(ImGuiCol.Text, Colors.Bad))
                        ImGui.TextUnformatted("Remover?");
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Sim"))
                    {
                        _ = _plugin.CraftingListManager.RemoveRecipeFromListAsync(recipe.Id);
                        _pendingRemoveId = null;
                        RefreshData();
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Não"))
                        _pendingRemoveId = null;
                }
                else
                {
                    ImGui.SameLine();
                    ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX(), rowLeft + innerW - ImGui.GetFrameHeight()));
                    if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.Times))
                        _pendingRemoveId = recipe.Id;
                    ImGuiUtils.HoveredTooltip("Remover da lista");
                }
            }

            if (ImGui.Button("+ Adicionar receita"))
                _plugin.CraftingListAddWindow.OpenManualSearch(_listId);

            if (_recipes.Count > 1)
            {
                ImGui.SameLine();
                if (ImGui.Button(_selectionMode ? "Sair da seleção" : "Selecionar"))
                {
                    _selectionMode = !_selectionMode;
                    _selectedRecipeIds.Clear();
                }
            }

            if (_selectionMode && _selectedRecipeIds.Count > 0)
                DrawSelectionActionBar();
        }
    }

    private void DrawSelectionActionBar()
    {
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        var targets = _plugin.CraftingListManager.Lists
            .Where(l => l.Id != _listId)
            .ToList();
        if (ImGui.BeginCombo("##moveTarget", string.IsNullOrEmpty(_moveTargetSelected) ? "Mover para lista..." : _moveTargetSelected))
        {
            foreach (var list in targets)
            {
                if (ImGui.Selectable(list.Name, list.Name == _moveTargetSelected))
                    _moveTargetSelected = list.Name;
            }
            ImGui.EndCombo();
        }

        ImGui.SameLine();
        var target = targets.FirstOrDefault(l => l.Name == _moveTargetSelected);
        using (ImRaii.Disabled(target == null))
        {
            if (ImGui.Button("Mover selecionadas") && target != null && _listId is { } srcId)
            {
                _ = _plugin.CraftingListManager.MoveRecipesAsync(srcId, target.Id, _selectedRecipeIds.ToList());
                _selectionMode = false;
                _selectedRecipeIds.Clear();
                RefreshData();
            }
        }
    }

    private void DrawCostLine()
    {
        if (_tree == null)
            return;
        var loadedPrices = _prices.Where(p => p.Value != null).ToList();
        if (loadedPrices.Count == 0)
        {
            if (_pricesLoading)
                ImGuiUtils.DrawStateChip(ImGuiUtils.SolverState.Solving, "Buscando preços...");
            return;
        }

        var mats = _tree.BaseMaterials.Concat(_tree.Crystals).ToList();
        long currentCost = 0;
        long cheapestCost = 0;
        string dcServer = string.Empty;
        foreach (var mat in mats)
        {
            if (_prices.TryGetValue(mat.ItemId, out var price) && price != null)
            {
                currentCost += (long)price.PriceCurrentServer * mat.Quantity;
                cheapestCost += (long)price.PriceCheapestServer * mat.Quantity;
                if (price.PriceCheapestServer < price.PriceCurrentServer)
                    dcServer = price.CheapestServerName;
            }
        }

        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
        {
            var text = $"Custo estimado: {currentCost:N0} gil";
            if (cheapestCost < currentCost && !string.IsNullOrEmpty(dcServer))
                text += $" · {cheapestCost:N0} gil no DC ({dcServer})";
            ImGui.TextUnformatted(text);
        }
    }

    private void DrawMaterialRow(ResolvedIngredient mat)
    {
        _progress.TryGetValue(mat.ItemId, out var prog);
        var collected = prog?.QuantityCollected ?? 0;
        var needed = mat.Quantity;
        var missing = Math.Max(0, needed - collected);
        var fraction = needed > 0 ? Math.Clamp((float)collected / needed, 0f, 1f) : 1f;
        var scale = ImGuiHelpers.GlobalScale;
        var iconSize = ImGui.GetFrameHeight();

        using (ImRaii.Group())
        {
            // ── Linha 1: ícone + nome + tem/precisa/faltam ──
            PluginImGuiUtils.DrawItemIcon(mat.ItemId, iconSize);
            ImGui.SameLine(0, 6 * scale);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(mat.ItemName);
            ImGui.SameLine(0, 8 * scale);
            if (missing > 0)
            {
                using (ImRaii.PushColor(ImGuiCol.Text, collected == 0 ? Colors.Bad : Colors.TextMuted))
                    ImGui.TextUnformatted($"{collected}/{needed}");
                ImGui.SameLine(0, 6 * scale);
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.Bad))
                    ImGui.TextUnformatted($"faltam {missing}");
            }
            else
            {
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.Progress))
                    ImGui.TextUnformatted($"{collected}/{needed} ✓");
            }

            // ── Linha 2: barra de progresso (largura cheia) ──
            using (ImRaii.PushColor(ImGuiCol.PlotHistogram, fraction >= 1f ? Colors.Progress : Colors.Quality))
                ImGuiUtils.ProgressBar(fraction, new Vector2(-1, 6 * scale));

            // ── Linha-meta: [teleporte] zona · preço (fluxo à esquerda → sem overflow) ──
            var hasZone = mat.GatheringLocations.Count > 0;
            _prices.TryGetValue(mat.ItemId, out var price);
            var hasPrice = _plugin.Configuration.ShowMarketPrices && price != null;

            if (hasZone)
            {
                var loc = mat.GatheringLocations[0];
                var inCombat = Service.Condition[ConditionFlag.InCombat];
                using (ImRaii.Disabled(!_plugin.TeleportHelper.IsAvailable || inCombat))
                {
                    if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.LocationArrow))
                        _plugin.TeleportHelper.TeleportTo(loc.NearestAetheryteId);
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    var reason = !_plugin.TeleportHelper.IsAvailable ? "Teleporter não encontrado"
                        : inCombat ? "Em combate"
                        : $"Teleportar para {loc.NearestAetheryteName}";
                    ImGuiUtils.Tooltip(reason);
                }
                ImGui.SameLine(0, 6 * scale);
                ImGui.AlignTextToFramePadding();
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                    ImGui.TextUnformatted(loc.ZoneName);
            }

            if (hasPrice)
            {
                if (hasZone) ImGui.SameLine(0, 6 * scale);
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                    ImGui.TextUnformatted($"· {price!.PriceCurrentServer:N0}g");
                if (price.PriceCheapestServer < price.PriceCurrentServer)
                {
                    var saving = price.PriceCurrentServer > 0
                        ? (float)(price.PriceCurrentServer - price.PriceCheapestServer) / price.PriceCurrentServer : 0f;
                    var dcColor = saving > 0.25f ? Colors.Progress : saving > 0.10f ? Colors.Durability : Colors.TextMuted;
                    ImGui.SameLine(0, 6 * scale);
                    using (ImRaii.PushColor(ImGuiCol.Text, dcColor))
                        ImGui.TextUnformatted($"{price.PriceCheapestServer:N0}g ({price.CheapestServerName})");
                }
            }
            else if (_plugin.Configuration.ShowMarketPrices && _pricesLoading)
            {
                if (hasZone) ImGui.SameLine(0, 6 * scale);
                ImGuiUtils.DrawStateChip(ImGuiUtils.SolverState.Solving, "Buscando...");
            }
        }
        ImGuiUtils.HoveredTooltip($"{collected}/{needed} coletado");
    }

    private void DrawPreCraftRow(ResolvedIngredient preCraft)
    {
        _progress.TryGetValue(preCraft.ItemId, out var prog);
        var collected = prog?.QuantityCollected ?? 0;
        var isComplete = collected >= preCraft.Quantity;

        using var id = ImRaii.PushId((int)preCraft.ItemId);

        PluginImGuiUtils.DrawItemIcon(preCraft.ItemId, ImGui.GetFrameHeight());
        ImGui.SameLine(0, 6 * ImGuiHelpers.GlobalScale);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(preCraft.ItemName);
        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted($"×{preCraft.Quantity}");
        ImGui.SameLine();

        if (isComplete)
        {
            ImGuiUtils.DrawBadgePill("✓ Satisfeito", Colors.Progress);
        }
        else
        {
            ImGuiUtils.DrawBadgePill("Craftar", Colors.ActionBuff);
            if (preCraft.PreCraftRecipeId.HasValue)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton("→ Macro Editor"))
                    OpenMacroEditorFor(preCraft.PreCraftRecipeId.Value);
            }
        }
    }

    private void OpenMacroEditorFor(uint recipeId)
    {
        var stats = _plugin.GetDefaultStats();
        var recipeData = new RecipeData((ushort)recipeId);
        _plugin.OpenMacroEditor(stats.Character, recipeData, stats.Buffs, null, [], null);
    }

    // ── Simple view ────────────────────────────────────────────────────────────

    private void DrawSimpleView()
    {
        if (_tree == null)
        {
            if (_treeLoading)
                ImGuiUtils.DrawStateChip(ImGuiUtils.SolverState.Solving, "Resolvendo materiais...");
            return;
        }

        var overallFraction = ComputeOverallFraction();
        using (ImRaii.PushColor(ImGuiCol.PlotHistogram, Colors.Progress))
            ImGuiUtils.ProgressBar(overallFraction, new Vector2(-1, 8 * ImGuiHelpers.GlobalScale));
        ImGui.TextUnformatted($"{(int)(overallFraction * 100)}%");

        using var child = ImRaii.Child("##simpleScroll", new Vector2(-1, -ImGui.GetFrameHeightWithSpacing()), false);
        if (!child)
        {
            DrawFooter();
            return;
        }

        var allMats = _tree.BaseMaterials.Concat(_tree.Crystals).ToList();
        foreach (var mat in OrderByProgress(allMats))
            DrawSimpleMaterialRow(mat);
    }

    private void DrawSimpleMaterialRow(ResolvedIngredient mat)
    {
        _progress.TryGetValue(mat.ItemId, out var prog);
        var collected = prog?.QuantityCollected ?? 0;
        var isComplete = collected >= mat.Quantity;

        using (ImRaii.PushColor(ImGuiCol.Text, isComplete ? Colors.Progress : Colors.TextMuted))
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
                ImGui.TextUnformatted(isComplete
                    ? FontAwesomeIcon.CheckCircle.ToIconString()
                    : FontAwesomeIcon.Circle.ToIconString());
        }
        ImGui.SameLine(0, 6 * ImGuiHelpers.GlobalScale);
        PluginImGuiUtils.DrawItemIcon(mat.ItemId, ImGui.GetTextLineHeight());
        ImGui.SameLine(0, 6 * ImGuiHelpers.GlobalScale);
        ImGui.TextUnformatted(mat.ItemName);
        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted($"{collected}/{mat.Quantity}");
    }

    // ── Footer ─────────────────────────────────────────────────────────────────

    private void DrawFooter()
    {
        if (ImGuiUtils.IconButtonWithTooltip((int)FontAwesomeIcon.Sync, "Sincronizar inventário"))
            _ = SyncInventoryAsync();

        ImGui.SameLine();
        if (_lastSyncTime is { } t)
        {
            var mins = (int)(DateTime.UtcNow - t).TotalMinutes;
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGui.TextUnformatted(mins < 1 ? "Sincronizado agora" : $"Sincronizado há {mins} min");
        }
        else
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGui.TextUnformatted("Não sincronizado");
        }

        if (_plugin.Configuration.ShowMarketPrices)
        {
            ImGui.SameLine();
            using (ImRaii.Disabled(_pricesLoading))
            {
                if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.Redo))
                {
                    _prices.Clear();
                    _ = LoadPricesAsync();
                }
                ImGuiUtils.HoveredTooltip("Atualizar preços", (int)ImGuiHoveredFlags.AllowWhenDisabled);
            }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private List<ResolvedIngredient> AllMaterials()
    {
        if (_tree == null)
            return new();
        return _tree.BaseMaterials.Concat(_tree.Crystals).Concat(_tree.PreCrafts).ToList();
    }

    private IEnumerable<ResolvedIngredient> OrderByProgress(IEnumerable<ResolvedIngredient> items) =>
        items.OrderBy(m =>
        {
            if (!_progress.TryGetValue(m.ItemId, out var p))
                return 0;
            if (p.IsComplete) return 2;
            return p.QuantityCollected > 0 ? 1 : 0;
        });

    private float ComputeOverallFraction()
    {
        if (_tree == null)
            return 0f;
        var mats = _tree.BaseMaterials.Concat(_tree.Crystals).Concat(_tree.PreCrafts).ToList();
        if (mats.Count == 0)
            return 0f;
        var needed = mats.Sum(m => m.Quantity);
        if (needed == 0)
            return 1f;
        var collected = mats.Sum(m =>
            _progress.TryGetValue(m.ItemId, out var p)
                ? Math.Min(p.QuantityCollected, m.Quantity)
                : 0);
        return Math.Clamp((float)collected / needed, 0f, 1f);
    }

    private string LookupItemName(uint itemId) =>
        _plugin.RecipeSearchHelper.Index.FirstOrDefault(r => r.ItemId == itemId)?.ItemName
        ?? $"Item #{itemId}";

    public void Dispose()
    {
        _plugin.CraftingListManager.ListsChanged -= OnListsChanged;
        _plugin.WindowSystem.RemoveWindow(this);
    }
}
