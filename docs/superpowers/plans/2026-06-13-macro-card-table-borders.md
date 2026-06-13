# Macro Card Table Borders Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove border flags from the macro card layout table inside GroupPanels in the Crafting Helper window, eliminating the spreadsheet-like row separators and double-border around the card.

**Architecture:** Single line change — replace `BordersOuter | BordersInnerV | BordersInnerH` with `ImGuiTableFlags.None` in `RecipeNote.DrawMacro`. Column widths and row heights are already controlled by `TableSetupColumn` + `TableNextRow(height)`, which are unaffected by border flags. The GroupPanel already provides the visual container.

**Tech Stack:** C#, Dalamud SDK 15, ImGui.NET (ImRaii.Table).

> **⚠️ Ordering note:** If the Window Class Rename plan has already been executed, `RecipeNote.cs` will have been renamed to `CraftingHelper.cs`. Target that file instead. The line number and content are identical.

---

### Task 1: Remove border flags from macroCard table

**Files:**
- Modify: `Artificer/Windows/RecipeNote.cs:1043-1045` (or `CraftingHelper.cs` if renamed)

> **Note:** This is a layout-only change. There are no automated tests for ImGui table rendering. Verification is build success + visual check in-game.

- [ ] **Step 1: Apply the fix**

In `Artificer/Windows/RecipeNote.cs`, find the `macroCard` table declaration (around line 1043):

```csharp
// BEFORE (causes double-border + spreadsheet row lines inside GroupPanel):
using var table = ImRaii.Table("macroCard", 2,
    ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.BordersInnerH,
    new Vector2(innerW, 0));
```

Replace with:

```csharp
// AFTER (layout table — no borders; GroupPanel provides the visual container):
using var table = ImRaii.Table("macroCard", 2,
    ImGuiTableFlags.None,
    new Vector2(innerW, 0));
```

- [ ] **Step 2: Build the plugin**

```powershell
.\scripts\build.ps1 -Deploy
```

Expected: build succeeds with no errors.

- [ ] **Step 3: Commit**

```powershell
git add Artificer/Windows/RecipeNote.cs
git commit -m "fix(ui): remover bordas de tabela no macro card dentro de GroupPanel"
```

- [ ] **Step 4: Visual verification (in-game)**

After deploying, open the Crafting Helper window and solve a craft. In the "Best Saved Macro" and "Suggested Macro" GroupPanels, the macro card should now look clean: no horizontal lines between the arcs row and the actions row, and no outer border rectangle duplicating the GroupPanel's own border.
