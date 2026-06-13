# Window Class Rename — Design

**Date:** 2026-06-13  
**Status:** Approved  
**Scope:** 5 window files + all references throughout the codebase

---

## Goal

Class names and window titles should match so that reading a class name immediately tells you which window it is, and vice versa.

## Changes

### Class renames (class name → matches window title)

| File (rename) | Class (rename) | Window title | Notes |
|---|---|---|---|
| `RecipeNote.cs` → `CraftingHelper.cs` | `RecipeNote` → `CraftingHelper` | "Crafting Helper" | No title change |
| `SynthHelper.cs` → `SynthesisHelper.cs` | `SynthHelper` → `SynthesisHelper` | "Synthesis Helper" | No title change |
| `MacroList.cs` → `MacroLibrary.cs` | `MacroList` → `MacroLibrary` | "Macro Library" | No title change |
| `FeatureHubWindow.cs` → `FeatureHub.cs` | `FeatureHubWindow` → `FeatureHub` | (no visible title) | Remove "Window" suffix |

### Window title change (title → matches class name)

| File | Class | Window title change |
|---|---|---|
| `CosmicTracker.cs` | `CosmicTracker` (unchanged) | `"Cosmic Tool###Artificer-cosmic"` → `"Cosmic Tracker###Artificer-cosmic"` |

The `###Artificer-cosmic` ImGui ID suffix must be preserved so user window position/size persists.

## Impact

Each rename requires updating all references in the codebase:

- Class declarations (`class RecipeNote` → `class CraftingHelper`)
- Constructor calls (`new RecipeNote(...)`)
- Field/property declarations (`private RecipeNote _recipeNote`)
- `typeof(RecipeNote)` expressions (if any)
- Namespace usages (class is in `Artificer.Windows`, stays there)
- The file itself must be renamed to match the new class name

The `RecipeNote` rename has the highest blast radius since it is referenced by plugin registration, service injection, and several helper classes. All are mechanical rename-only — no logic changes.

## What Does NOT Change

- Window ImGui IDs (the `###Artificer-*` suffix) — changing these resets user window positions
- Namespace (`Artificer.Windows` for all)
- Any behavior or logic
- `MacroClipboard`, `MacroEditor`, `Settings`, `CraftingListWindow` and variants (already consistent)

## Out of Scope

- Renaming `Settings` → `ArtificerSettings` (not requested)
- Updating comments or documentation references
- Changing any InternalName strings used for Dalamud plugin registration
