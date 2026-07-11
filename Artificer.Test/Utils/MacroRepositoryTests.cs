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

    private static void Exec(SqliteConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
