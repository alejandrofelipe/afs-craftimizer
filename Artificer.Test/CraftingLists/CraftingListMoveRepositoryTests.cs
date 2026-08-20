using System;
using System.IO;
using System.Linq;
using Artificer.Application.CraftingLists;
using Artificer.Data;
using Microsoft.Data.Sqlite;

namespace Artificer.Test.CraftingLists;

[TestClass]
public class CraftingListMoveRepositoryTests
{
    private static string TempDb() =>
        Path.Combine(Path.GetTempPath(), $"artificer-move-{Guid.NewGuid():N}.db");

    private static CraftingList MakeList(Guid id, string name) =>
        new(id, name, DateTime.UnixEpoch, DateTime.UnixEpoch, null, 0);

    private static CraftingListRecipe Recipe(Guid listId, uint recipeId, int qty) =>
        new(Guid.NewGuid(), listId, recipeId, recipeId + 1000, qty, DateTime.UnixEpoch, 0);

    private static MaterialProgress Progress(Guid listId, uint itemId, int needed, int collected, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), listId, itemId, needed, collected, DateTime.UnixEpoch);

    private static readonly DateTime MoveTime = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void ApplyRecipeMove_Commit_ReplacesRecipesAndProgressForBothLists()
    {
        var path = TempDb();
        var src = Guid.NewGuid();
        var dst = Guid.NewGuid();
        try
        {
            using (var repo = new CraftingListRepository(path))
            {
                repo.InsertList(MakeList(src, "src"));
                repo.InsertList(MakeList(dst, "dst"));

                repo.InsertRecipe(Recipe(src, 10, 1));  // será movida (colide no destino)
                repo.InsertRecipe(Recipe(src, 20, 2));  // fica
                repo.InsertRecipe(Recipe(dst, 10, 5));

                repo.UpsertProgress(Progress(src, itemId: 100, needed: 4, collected: 1));  // vira órfão
                repo.UpsertProgress(Progress(dst, itemId: 200, needed: 3, collected: 0));  // vira órfão

                // Estado final: origem mantém receita 20; destino tem a 10 somada (5+1=6);
                // progresso da origem reconciliado só p/ item 300; do destino p/ item 100 (compartilhado).
                var writeSet = new CraftingListMoveWriteSet(
                    src, dst,
                    SourceRecipes: [Recipe(src, 20, 2)],
                    DestinationRecipes: [Recipe(dst, 10, 6)],
                    SourceProgress: [Progress(src, 300, 2, 0)],
                    DestinationProgress: [Progress(dst, 100, 4, 1)],
                    UpdatedAt: MoveTime);

                repo.ApplyRecipeMove(writeSet);
            }

            using (var repo = new CraftingListRepository(path))
            {
                var srcRecipes = repo.GetRecipesForList(src);
                var dstRecipes = repo.GetRecipesForList(dst);
                Assert.AreEqual(1, srcRecipes.Count);
                Assert.AreEqual(20u, srcRecipes[0].RecipeId);
                Assert.AreEqual(1, dstRecipes.Count);
                Assert.AreEqual(10u, dstRecipes[0].RecipeId);
                Assert.AreEqual(6, dstRecipes[0].Quantity);

                var srcProgress = repo.GetProgressForList(src);
                var dstProgress = repo.GetProgressForList(dst);
                CollectionAssert.AreEquivalent(new uint[] { 300 }, srcProgress.Select(p => p.ItemId).ToArray());
                CollectionAssert.AreEquivalent(new uint[] { 100 }, dstProgress.Select(p => p.ItemId).ToArray());
                Assert.IsFalse(srcProgress.Any(p => p.ItemId == 100));  // órfão removido
                Assert.IsFalse(dstProgress.Any(p => p.ItemId == 200));  // órfão removido

                var srcList = repo.GetList(src)!;
                var dstList = repo.GetList(dst)!;
                Assert.AreEqual(MoveTime, srcList.UpdatedAt);
                Assert.AreEqual(MoveTime, dstList.UpdatedAt);
                Assert.IsNull(srcList.CompletedAt);
                Assert.IsNull(dstList.CompletedAt);
            }
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    [TestMethod]
    public void ApplyRecipeMove_DuplicateProgressId_RollsBackEverything()
    {
        var path = TempDb();
        var src = Guid.NewGuid();
        var dst = Guid.NewGuid();
        var dupId = Guid.NewGuid();
        try
        {
            using (var repo = new CraftingListRepository(path))
            {
                repo.InsertList(MakeList(src, "src"));
                repo.InsertList(MakeList(dst, "dst"));
                repo.InsertRecipe(Recipe(src, 20, 2));
                repo.InsertRecipe(Recipe(dst, 10, 5));
                repo.UpsertProgress(Progress(src, 100, 4, 1));

                // Dois progressos diferentes com o MESMO id → viola a PK no segundo insert,
                // depois que deletes/inserts de receita já rodaram na transação.
                var writeSet = new CraftingListMoveWriteSet(
                    src, dst,
                    SourceRecipes: [Recipe(src, 20, 2)],
                    DestinationRecipes: [Recipe(dst, 10, 6)],
                    SourceProgress: [Progress(src, 300, 1, 0, id: dupId)],
                    DestinationProgress: [Progress(dst, 400, 1, 0, id: dupId)],
                    UpdatedAt: MoveTime);

                Assert.ThrowsException<SqliteException>(() => repo.ApplyRecipeMove(writeSet));
            }

            using (var repo = new CraftingListRepository(path))
            {
                var srcRecipes = repo.GetRecipesForList(src);
                var dstRecipes = repo.GetRecipesForList(dst);
                Assert.AreEqual(1, srcRecipes.Count);
                Assert.AreEqual(20u, srcRecipes[0].RecipeId);
                Assert.AreEqual(1, dstRecipes.Count);
                Assert.AreEqual(5, dstRecipes[0].Quantity);  // NÃO somou p/ 6

                var srcProgress = repo.GetProgressForList(src);
                Assert.AreEqual(1, srcProgress.Count);
                Assert.AreEqual(100u, srcProgress[0].ItemId);

                Assert.AreEqual(DateTime.UnixEpoch, repo.GetList(src)!.UpdatedAt);  // timestamp intacto
            }
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    [TestMethod]
    public void ApplyRecipeMove_RowWithWrongListId_ThrowsWithoutWriting()
    {
        var path = TempDb();
        var src = Guid.NewGuid();
        var dst = Guid.NewGuid();
        try
        {
            using var repo = new CraftingListRepository(path);
            repo.InsertList(MakeList(src, "src"));
            repo.InsertList(MakeList(dst, "dst"));
            repo.InsertRecipe(Recipe(src, 20, 2));

            // Uma receita "de origem" com ListId do destino → write-set malformado.
            var writeSet = new CraftingListMoveWriteSet(
                src, dst,
                SourceRecipes: [Recipe(dst, 20, 2)],
                DestinationRecipes: [],
                SourceProgress: [],
                DestinationProgress: [],
                UpdatedAt: MoveTime);

            Assert.ThrowsException<ArgumentException>(() => repo.ApplyRecipeMove(writeSet));
            Assert.AreEqual(1, repo.GetRecipesForList(src).Count);  // nada escrito
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(path); }
    }
}
