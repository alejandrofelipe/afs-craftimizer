using Artificer.Data;
using Artificer.Plugin;
using Dalamud.Interface.ImGuiNotification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PluginClass = Artificer.Plugin.Plugin;

namespace Artificer.Application.CraftingLists;

/// <summary>
/// Orchestrates the crafting-list repository, ingredient resolver, and inventory scanner.
/// Maintains an in-memory cache of lists and raises change/completion events for the UI layer.
/// </summary>
public sealed class CraftingListManager : IDisposable
{
    private readonly CraftingListRepository _repo;
    private readonly IngredientResolver _resolver;
    private readonly PluginClass _plugin;
    private readonly List<CraftingList> _lists = [];

    public IReadOnlyList<CraftingList> Lists => _lists;

    public event Action? ListsChanged;
    public event Action<CraftingList>? ListCompleted;

    public CraftingListManager(CraftingListRepository repo, PluginClass plugin)
    {
        _repo = repo;
        _plugin = plugin;
        _resolver = new IngredientResolver();
        _lists.AddRange(_repo.GetAllLists());
    }

    // ── List CRUD ───────────────────────────────────────────────────────────────

    public Task<CraftingList> CreateListAsync(string name)
    {
        var now = DateTime.UtcNow;
        var list = new CraftingList(Guid.NewGuid(), name, now, now, null, 0);
        _repo.InsertList(list);
        _lists.Insert(0, list);
        ListsChanged?.Invoke();
        return Task.FromResult(list);
    }

    public Task DeleteListAsync(Guid listId)
    {
        _repo.DeleteList(listId);
        _lists.RemoveAll(l => l.Id == listId);
        ListsChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task RenameListAsync(Guid listId, string newName)
    {
        var index = _lists.FindIndex(l => l.Id == listId);
        if (index < 0)
            return Task.CompletedTask;

        var updated = _lists[index] with { Name = newName, UpdatedAt = DateTime.UtcNow };
        _repo.UpdateList(updated);
        _lists[index] = updated;
        ListsChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task<CraftingList> DuplicateListAsync(Guid listId)
    {
        var source = _repo.GetList(listId)
            ?? throw new ArgumentException($"List {listId} not found", nameof(listId));

        var now = DateTime.UtcNow;
        var copy = new CraftingList(Guid.NewGuid(), $"{source.Name} (Cópia)", now, now, null, 0);
        _repo.InsertList(copy);

        foreach (var recipe in _repo.GetRecipesForList(listId))
        {
            var newRecipe = recipe with { Id = Guid.NewGuid(), ListId = copy.Id, AddedAt = now };
            _repo.InsertRecipe(newRecipe);
        }

        _lists.Insert(0, copy);
        ListsChanged?.Invoke();
        return Task.FromResult(copy);
    }

    // ── Recipe management ─────────────────────────────────────────────────────────

    public Task<CraftingListRecipe> AddRecipeToListAsync(Guid listId, uint recipeId, int quantity)
    {
        quantity = Math.Max(1, quantity);
        var existing = _repo.GetRecipesForList(listId).FirstOrDefault(r => r.RecipeId == recipeId);
        if (existing != null)
        {
            var newQty = existing.Quantity + quantity;
            _repo.UpdateRecipeQuantity(existing.Id, newQty);
            TouchList(listId);
            ListsChanged?.Invoke();
            return Task.FromResult(existing with { Quantity = newQty });
        }

        var itemId = LuminaSheets.RecipeSheet.GetRowOrDefault(recipeId)?.ItemResult.RowId ?? 0u;
        var recipe = new CraftingListRecipe(
            Guid.NewGuid(),
            listId,
            recipeId,
            itemId,
            quantity,
            DateTime.UtcNow,
            0);
        _repo.InsertRecipe(recipe);
        TouchList(listId);
        ListsChanged?.Invoke();
        return Task.FromResult(recipe);
    }

    public Task UpdateRecipeQuantityAsync(Guid recipeId, int newQuantity)
    {
        newQuantity = Math.Max(1, newQuantity);
        _repo.UpdateRecipeQuantity(recipeId, newQuantity);
        ListsChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task RemoveRecipeFromListAsync(Guid recipeId)
    {
        _repo.DeleteRecipe(recipeId);
        ListsChanged?.Invoke();
        return Task.CompletedTask;
    }

    // ── Inventory sync & resolution ────────────────────────────────────────────────

    public Task SyncWithInventoryAsync(Guid listId, bool includeRetainers)
    {
        var recipes = _repo.GetRecipesForList(listId);
        var snapshot = InventoryScanner.Read();

        // Resolve what the list needs (owned = empty), then reconcile progress against the snapshot.
        // Reconcile replaces the persisted set, dropping rows for ingredients no longer needed.
        var tree = _resolver.ResolveAll(recipes, new Dictionary<uint, int>());
        var existing = _repo.GetProgressForList(listId);
        var progress = MaterialProgressReconciler.Reconcile(
            listId, tree, snapshot, includeRetainers, existing, DateTime.UtcNow);

        _repo.ReplaceProgressForList(listId, progress);
        TouchList(listId);
        CheckListCompletion(listId);
        return Task.CompletedTask;
    }

    public Task<ResolvedIngredientTree> ResolveIngredientsAsync(Guid listId)
    {
        var recipes = _repo.GetRecipesForList(listId);
        var snapshot = InventoryScanner.Read();

        var owned = new Dictionary<uint, int>();
        foreach (var (id, qty) in snapshot.MainBags)
            owned[id] = owned.GetValueOrDefault(id) + qty;
        foreach (var (id, qty) in snapshot.Crystals)
            owned[id] = owned.GetValueOrDefault(id) + qty;
        foreach (var (id, qty) in snapshot.SaddleBag)
            owned[id] = owned.GetValueOrDefault(id) + qty;
        foreach (var r in snapshot.Retainers.Where(r => r.Loaded))
            foreach (var (id, qty) in r.Items)
                owned[id] = owned.GetValueOrDefault(id) + qty;

        var tree = _resolver.ResolveAll(recipes, owned);
        return Task.FromResult(tree);
    }

    // ── Completion ───────────────────────────────────────────────────────────────

    public Task CheckListCompletionAsync(Guid listId)
    {
        CheckListCompletion(listId);
        return Task.CompletedTask;
    }

    private void CheckListCompletion(Guid listId)
    {
        var progress = _repo.GetProgressForList(listId);
        if (progress.Count == 0 || !progress.TrueForAll(p => p.IsComplete))
            return;

        var list = _lists.FirstOrDefault(l => l.Id == listId) ?? _repo.GetList(listId);
        if (list == null)
            return;

        if (_plugin.Configuration.AutoDeleteCompletedLists)
        {
            _repo.DeleteList(listId);
            _lists.RemoveAll(l => l.Id == listId);
            ListsChanged?.Invoke();
        }
        else
        {
            var completed = list with { CompletedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            _repo.UpdateList(completed);
            var index = _lists.FindIndex(l => l.Id == listId);
            if (index >= 0)
                _lists[index] = completed;
            list = completed;
        }

        ListCompleted?.Invoke(list);

        Service.NotificationManager.AddNotification(new Notification
        {
            Title = "Lista Concluída",
            Content = list.Name,
            Type = NotificationType.Success
        });
    }

    // ── Merge / move ───────────────────────────────────────────────────────────────

    public Task MergeListsAsync(Guid sourceListId, Guid destinationListId, bool deleteSource)
    {
        var srcRecipes = _repo.GetRecipesForList(sourceListId);
        var dstRecipes = _repo.GetRecipesForList(destinationListId);
        var now = DateTime.UtcNow;

        foreach (var src in srcRecipes)
        {
            var match = dstRecipes.FirstOrDefault(d => d.RecipeId == src.RecipeId);
            if (match != null)
                _repo.UpdateRecipeQuantity(match.Id, match.Quantity + src.Quantity);
            else
            {
                var newRecipe = src with { Id = Guid.NewGuid(), ListId = destinationListId, AddedAt = now };
                _repo.InsertRecipe(newRecipe);
            }
        }

        // Merge progress: take the max collected per item id.
        var srcProgress = _repo.GetProgressForList(sourceListId);
        var dstProgress = _repo.GetProgressForList(destinationListId).ToDictionary(p => p.ItemId);
        var merged = new List<MaterialProgress>();
        foreach (var sp in srcProgress)
        {
            if (dstProgress.TryGetValue(sp.ItemId, out var dp))
            {
                merged.Add(dp with
                {
                    QuantityCollected = Math.Max(dp.QuantityCollected, sp.QuantityCollected),
                    QuantityNeeded = Math.Max(dp.QuantityNeeded, sp.QuantityNeeded),
                    UpdatedAt = now
                });
            }
            else
            {
                merged.Add(sp with { Id = Guid.NewGuid(), ListId = destinationListId, UpdatedAt = now });
            }
        }
        if (merged.Count > 0)
            _repo.UpsertProgressBatch(merged);

        TouchList(destinationListId);

        if (deleteSource)
        {
            _repo.DeleteList(sourceListId);
            _lists.RemoveAll(l => l.Id == sourceListId);
        }
        else
        {
            _repo.DeleteRecipesForList(sourceListId);
            _repo.DeleteProgressForList(sourceListId);
            TouchList(sourceListId);
        }

        ListsChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task MoveRecipesAsync(Guid sourceListId, Guid destinationListId, IReadOnlyList<Guid> recipeIds)
    {
        if (recipeIds.Count == 0)
            return Task.CompletedTask;

        var source = _repo.GetRecipesForList(sourceListId);
        var destination = _repo.GetRecipesForList(destinationListId);
        var plan = CraftingListMovePlanner.Plan(sourceListId, destinationListId, source, destination, recipeIds);

        // Reconcile each list's progress against its FINAL recipe tree (never inferred from the moved
        // product ids), then persist recipes, progress and timestamps atomically.
        var snapshot = InventoryScanner.Read();
        var emptyOwned = new Dictionary<uint, int>();
        var sourceTree = _resolver.ResolveAll(plan.SourceRecipes, emptyOwned);
        var destinationTree = _resolver.ResolveAll(plan.DestinationRecipes, emptyOwned);
        var now = DateTime.UtcNow;
        var includeRetainers = _plugin.Configuration.IncludeRetainersInSync;

        var sourceProgress = MaterialProgressReconciler.Reconcile(
            sourceListId, sourceTree, snapshot, includeRetainers, _repo.GetProgressForList(sourceListId), now);
        var destinationProgress = MaterialProgressReconciler.Reconcile(
            destinationListId, destinationTree, snapshot, includeRetainers, _repo.GetProgressForList(destinationListId), now);

        _repo.ApplyRecipeMove(new CraftingListMoveWriteSet(
            sourceListId, destinationListId,
            plan.SourceRecipes, plan.DestinationRecipes,
            sourceProgress, destinationProgress, now));

        // In-memory state + events only after the commit succeeds (ApplyRecipeMove already stamped the DB).
        SetListUpdatedInMemory(sourceListId, now);
        SetListUpdatedInMemory(destinationListId, now);
        ListsChanged?.Invoke();
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private void TouchList(Guid listId)
    {
        var list = _lists.FirstOrDefault(l => l.Id == listId) ?? _repo.GetList(listId);
        if (list == null)
            return;

        var updated = list with { UpdatedAt = DateTime.UtcNow };
        _repo.UpdateList(updated);
        var index = _lists.FindIndex(l => l.Id == listId);
        if (index >= 0)
            _lists[index] = updated;
    }

    /// <summary>Updates only the in-memory list entry after a write that already persisted the change
    /// (e.g. ApplyRecipeMove). Does not touch the database.</summary>
    private void SetListUpdatedInMemory(Guid listId, DateTime updatedAt)
    {
        var index = _lists.FindIndex(l => l.Id == listId);
        if (index >= 0)
            _lists[index] = _lists[index] with { UpdatedAt = updatedAt, CompletedAt = null };
    }

    public void Dispose()
    {
        // Repository is owned and disposed by Plugin separately.
    }
}
