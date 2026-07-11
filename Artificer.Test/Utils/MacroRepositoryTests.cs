using Artificer.Plugin;
using Artificer.Simulator.Actions;
using Artificer.Utils;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace Artificer.Test.Utils;

[TestClass]
public class MacroRepositoryTests
{
    private static string TempDb() => Path.Combine(Path.GetTempPath(), $"artificer-macros-{System.Guid.NewGuid():N}.db");

    [TestMethod]
    public void Source_RoundTrips_ThroughReopen()
    {
        var path = TempDb();
        try
        {
            using (var repo = new MacroRepository(path))
            {
                var m = new Macro { Name = "x", RecipeId = 42, Source = MacroSource.Auto };
                m.Actions = new[] { ActionType.BasicSynthesis };
                repo.Add(m);
            }
            using (var repo2 = new MacroRepository(path))
            {
                var loaded = repo2.Macros.Single();
                Assert.AreEqual(MacroSource.Auto, loaded.Source);
                Assert.AreEqual((ushort)42, loaded.RecipeId);
            }
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    [TestMethod]
    public void Source_RoundTrips_ThroughUpdate()
    {
        var path = TempDb();
        try
        {
            Macro m;
            using (var repo = new MacroRepository(path))
            {
                m = new Macro { Name = "x", RecipeId = 42, Source = MacroSource.User };
                m.Actions = new[] { ActionType.BasicSynthesis };
                repo.Add(m);

                m.Source = MacroSource.Auto;
                repo.Update(m);
            }
            using (var repo2 = new MacroRepository(path))
            {
                var loaded = repo2.Macros.Single();
                Assert.AreEqual(MacroSource.Auto, loaded.Source);
            }
        }
        // ClearAllPools() clears pools process-wide; assumes these SQLite tests don't run in parallel with other SQLite-backed tests.
        finally { SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    [TestMethod]
    public void MigrationV3_AddsSourceColumn_ExistingMacrosBecomeUser()
    {
        var path = TempDb();
        try
        {
            // Semeia um DB no schema V2 (sem coluna Source), user_version = 2, com uma macro.
            using (var db = new SqliteConnection($"Data Source={path}"))
            {
                db.Open();
                Exec(db, "CREATE TABLE Macros (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL DEFAULT '', RecipeId INTEGER, SavedScore REAL NOT NULL DEFAULT 0, DisplayOrder INTEGER NOT NULL DEFAULT 0, CharacterStatsHash INTEGER)");
                Exec(db, "CREATE TABLE MacroActions (MacroId INTEGER NOT NULL, Position INTEGER NOT NULL, ActionType TEXT NOT NULL, PRIMARY KEY (MacroId, Position))");
                Exec(db, "INSERT INTO Macros (Name, RecipeId) VALUES ('legacy', 7)");
                Exec(db, "PRAGMA user_version = 2");
            }

            using (var repo = new MacroRepository(path))
            {
                var loaded = repo.Macros.Single();
                Assert.AreEqual(MacroSource.User, loaded.Source, "macro pré-existente deve virar User");
            }

            // Reabrir é idempotente e user_version = 3.
            using (var repo2 = new MacroRepository(path)) { Assert.AreEqual(1, repo2.Macros.Count); }
            using (var db = new SqliteConnection($"Data Source={path}"))
            {
                db.Open();
                using var cmd = db.CreateCommand();
                cmd.CommandText = "PRAGMA user_version";
                Assert.AreEqual(3L, (long)cmd.ExecuteScalar()!);
            }
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    [TestMethod]
    public void SnapshotMacros_DoesNotThrow_WhileListMutated()
    {
        var path = TempDb();
        try
        {
            using var repo = new MacroRepository(path);
            for (var i = 0; i < 20; i++)
            {
                var m = new Macro { Name = $"m{i}", RecipeId = (ushort)i };
                m.Actions = new[] { ActionType.BasicSynthesis };
                repo.Add(m);
            }

            System.Exception? failure = null;
            var stop = false;
            var reader = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    while (!System.Threading.Volatile.Read(ref stop))
                        foreach (var _ in repo.SnapshotMacros()) { /* itera a cópia */ }
                }
                catch (System.Exception ex) { failure = ex; }
            });

            // Add/Remove mudam Count e forçam realocação do array interno (_size/_items),
            // ao contrário de Swap (que só troca elementos via indexador). É essa realocação
            // concorrente com o ToArray() do SnapshotMacros() que pode lançar sem o lock.
            for (var i = 0; i < 150; i++)
            {
                var m = new Macro { Name = $"churn{i}", RecipeId = (ushort)i };
                m.Actions = new[] { ActionType.BasicSynthesis };
                repo.Add(m);
                repo.Remove(m);
            }
            System.Threading.Volatile.Write(ref stop, true);
            reader.Wait();

            Assert.IsNull(failure, $"enumerar o snapshot não deve lançar: {failure}");
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    private static void Exec(SqliteConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
