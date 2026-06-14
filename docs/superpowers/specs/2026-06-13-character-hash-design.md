# Character Hash for Macros — Design

**Date:** 2026-06-13  
**Status:** Approved  
**Scope:** Store character stats hash with saved macros; pre-fill Suggested Macro from best hash-matching saved macro while solver runs; show mismatch indicator on Best Saved Macro when hash differs from current character.

---

## Problem

Macros are saved without any record of the character stats under which they were created. This causes two UX gaps:

1. **Suggested Macro shows "Loading..."** even when a perfect saved macro already exists for the current character — the solver runs from scratch every time.
2. **Best Saved Macro gives no feedback** when the displayed macro was created for a different character (different gear, level, or buffs) and may underperform or fail.

---

## Solution Overview

Add a `CharacterStatsHash` to each saved macro at save time. Use it to:
- Pre-fill the Suggested Macro slot immediately when a hash-matching saved macro completes the craft, while the solver runs in background.
- Display a visual mismatch indicator on the Best Saved Macro panel when its hash differs from the current character's hash.

---

## Section 1 — Migration System

Replace `EnsureSchema()` with a versioned migration runner based on SQLite's native `PRAGMA user_version`.

**`MacroRepository.cs`:**

```csharp
private const int TargetSchemaVersion = 2;

private void RunMigrations()
{
    Exec("PRAGMA journal_mode=WAL");
    Exec("PRAGMA foreign_keys=ON");

    var version = GetUserVersion();

    if (version < 1) ApplyV1_InitialSchema();
    if (version < 2) ApplyV2_CharacterHash();

    Exec($"PRAGMA user_version = {TargetSchemaVersion}");
}

private int GetUserVersion()
{
    using var cmd = Command("PRAGMA user_version");
    using var r   = cmd.ExecuteReader();
    return r.Read() ? r.GetInt32(0) : 0;
}

private void ApplyV1_InitialSchema()
{
    Exec("""
        CREATE TABLE IF NOT EXISTS Macros (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            Name         TEXT    NOT NULL DEFAULT '',
            RecipeId     INTEGER,
            SavedScore   REAL    NOT NULL DEFAULT 0,
            DisplayOrder INTEGER NOT NULL DEFAULT 0
        )
        """);
    Exec("""
        CREATE TABLE IF NOT EXISTS MacroActions (
            MacroId    INTEGER NOT NULL REFERENCES Macros(Id) ON DELETE CASCADE,
            Position   INTEGER NOT NULL,
            ActionType TEXT    NOT NULL,
            PRIMARY KEY (MacroId, Position)
        )
        """);
    Exec("CREATE INDEX IF NOT EXISTS idx_macros_order  ON Macros(DisplayOrder)");
    Exec("CREATE INDEX IF NOT EXISTS idx_macros_recipe ON Macros(RecipeId)");
}

private void ApplyV2_CharacterHash()
{
    Exec("ALTER TABLE Macros ADD COLUMN CharacterStatsHash INTEGER");
}
```

**Migration behavior:**
- **Existing databases** (`user_version = 0`): V1 runs `CREATE TABLE IF NOT EXISTS` (no-op, tables exist) → V2 adds the column → version set to 2. Zero data loss.
- **Clean installs**: V1 and V2 run in sequence on first launch.
- **Future migrations**: increment `TargetSchemaVersion`, add `if (version < N) ApplyVN_...()`.

---

## Section 2 — Model and Repository

### `Artificer/Models/Macro.cs`

Add one nullable property:

```csharp
public int? CharacterStatsHash { get; set; }
```

Nullable: macros saved before this feature have `null` — no indicator, no pre-fill eligibility.

### `Simulator/CharacterStats.cs` (or a static helper)

Add a stable hash computation:

```csharp
public static int ComputeHash(CharacterStats s) =>
    HashCode.Combine(
        s.Craftsmanship,
        s.Control,
        s.CP,
        s.Level,
        s.CanUseManipulation,
        s.HasSplendorousBuff,
        s.IsSpecialist);
```

`HashCode.Combine` is deterministic for the same input values within the same .NET runtime. Preferred over `record.GetHashCode()` because the hashed fields are explicit — a future field added to `CharacterStats` only enters the hash when intentionally added here.

### `MacroRepository.cs` changes

**`Add()` signature:**
```csharp
public void Add(Macro macro, int? characterStatsHash = null)
```
Optional parameter — callers that don't have stats context (e.g. legacy migration) pass nothing.

**`InsertMacroRow()`** — include hash in `INSERT`:
```csharp
"INSERT INTO Macros (Name, RecipeId, SavedScore, DisplayOrder, CharacterStatsHash)
 VALUES ($name, $recipeId, $score, $order, $hash); SELECT last_insert_rowid()"
```
with `cmd.Parameters.AddWithValue("$hash", characterStatsHash.HasValue ? (object)characterStatsHash.Value : DBNull.Value)`.

**`LoadAll()`** — read and populate:
```csharp
// SELECT Id, Name, RecipeId, SavedScore, CharacterStatsHash FROM Macros ...
macro.CharacterStatsHash = mr.IsDBNull(4) ? null : mr.GetInt32(4);
```

**`UpdateMacro()`** — preserve existing hash (do NOT overwrite on rename/reorder). The hash is only set at `Add()` time.

### Who computes and passes the hash

`CraftingHelper` — it already holds `CharacterStats!` at the moment the user saves a macro. It calls:
```csharp
_plugin.MacroRepository.Add(macro, CharacterStats.ComputeHash(CharacterStats!));
```
`MacroRepository` receives the already-computed integer and stores it. No coupling to `CharacterStats` type in the repository.

---

## Section 3 — Pre-fill Logic (CraftingHelper)

### Current character hash

```csharp
private int? _currentCharacterHash;
```

Set (or reset to `null`) whenever `CharacterStats` changes — same place it's already assigned. Recomputed if the character switches gear or levels.

### Extended `CalculateSavedMacro` result

Inside the simulation loop (which already simulates all macros), additionally track the best hash-matching macro:

```csharp
var bestHashMatch = results
    .Where(r => r.macro.CharacterStatsHash == _currentCharacterHash
             && r.macro.CharacterStatsHash != null
             && r.score > 0)          // score > 0 means craft completed
    .MaxBy(r => r.score);
```

Return alongside the existing best-macro result. The task return type expands to include `(Macro? bestHashMatch, SimulationState? hashMatchState, float hashMatchScore)`.

### Suggested Macro slot behavior

```
SavedMacroTask completes
├── bestHashMatch exists (score > 0, hash matches)?
│   ├── Yes → pre-fill Suggested slot immediately
│   │         SuggestedMacroTask (solver) still runs in background
│   │         SuggestedMacroTask completes → solver score > pre-fill score?
│   │         ├── Yes → replace Suggested with solver result (badge removed)
│   │         └── No  → keep pre-fill (badge removed — pre-fill "won")
│   └── No  → current behavior: Suggested shows "Loading..." until solver finishes
```

The solver **always runs** — the pre-fill only eliminates the empty/loading state, not the solver itself.

### Pre-fill visual badge

When the Suggested slot is showing a pre-filled saved macro (solver still running), render a small `FontAwesomeIcon.Bookmark` icon inline with the `"Suggested Macro"` GroupPanel title, in `ImGuiCol.TextDisabled` color.

Tooltip on the badge: `"Pre-filled from saved macro — solver still comparing"`.

Badge disappears when the solver completes (regardless of which result "wins").

---

## Section 4 — Mismatch Indicator (Best Saved Macro)

In `DrawMacro` for `MacroTaskType.Saved`, the footer already contains the edit and copy buttons. Add a `FontAwesomeIcon.ExclamationTriangle` icon to the **left side** of the footer when:

```csharp
macro.CharacterStatsHash != null && macro.CharacterStatsHash != _currentCharacterHash
```

Footer layout with mismatch active:
```
[⚠]  ···padding···  [✎] [⎘]
```

The `⚠` icon uses a yellow/orange color (`Colors.ActionFail` or a suitable theme color). Tooltip: `"This macro was saved with different character stats and may not perform as expected"`.

**No indicator** when:
- `CharacterStatsHash == null` (macro saved before this feature — unknown, not a confirmed mismatch)
- `CharacterStatsHash == _currentCharacterHash` (exact match — no concern)
- `_currentCharacterHash == null` (character stats not yet loaded)

---

## Files Changed

| File | Change |
|------|--------|
| `Artificer/Models/Macro.cs` | Add `CharacterStatsHash int?` property |
| `Artificer/Simulator/CharacterStats.cs` | Add `static ComputeHash(CharacterStats)` |
| `Artificer/Utils/MacroRepository.cs` | `EnsureSchema` → `RunMigrations`; `Add(macro, hash?)`; `InsertMacroRow`, `LoadAll`, `UpdateMacro` updated |
| `Artificer/Windows/CraftingHelper.cs` | `_currentCharacterHash` field; extend `CalculateSavedMacro`; pre-fill logic in Suggested slot; mismatch indicator in `DrawMacro` |

No new files. No new NuGet dependencies.
