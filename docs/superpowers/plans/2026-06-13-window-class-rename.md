# Window Class Rename Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename 4 window classes and 1 window title so that each class name matches the title of the window it represents.

**Architecture:** Pure mechanical rename — no logic changes. Each task renames one class: git mv the file, update the class declaration and constructor, update all references in other files. Config property names (e.g. `SynthHelperSolverConfig`, `PinRecipeNoteToWindow`) are **NOT** renamed — they are serialized JSON keys that would break saved user configs if changed. UIStudio story files are renamed for consistency.

**Tech Stack:** C#, git mv (preserves history), PowerShell.

> **⚠️ CRITICAL:** The `using CSRecipeNote = FFXIVClientStructs.FFXIV.Client.Game.UI.RecipeNote;` alias in `CraftingHelper.cs` (formerly `RecipeNote.cs`) refers to the **game struct**, not our class. Do NOT rename it.

> **⚠️ CRITICAL:** `Configuration.cs` properties like `SynthHelperSolverConfig`, `EnableSynthHelper`, `RecipeNoteSolverConfig`, `PinRecipeNoteToWindow`, etc. are serialized JSON — do NOT rename them.

---

### Task 1: RecipeNote → CraftingHelper

**Files:**
- Rename: `Artificer/Windows/RecipeNote.cs` → `Artificer/Windows/CraftingHelper.cs`
- Rename: `Artificer.UIStudio/Stories/Pages/RecipeNoteStory.cs` → `Artificer.UIStudio/Stories/Pages/CraftingHelperStory.cs`
- Modify: `Artificer/Windows/CraftingHelper.cs` (class and constructor name)
- Modify: `Artificer/Plugin.cs` (property type only)
- Modify: `Artificer.UIStudio/Stories/Pages/CraftingHelperStory.cs` (class and Name property)
- Modify: `Artificer.UIStudio/Program.cs` (story registration)

- [ ] **Step 1: Rename files with git mv**

```powershell
git mv Artificer/Windows/RecipeNote.cs Artificer/Windows/CraftingHelper.cs
git mv "Artificer.UIStudio/Stories/Pages/RecipeNoteStory.cs" "Artificer.UIStudio/Stories/Pages/CraftingHelperStory.cs"
```

- [ ] **Step 2: Update class declaration in CraftingHelper.cs**

In `Artificer/Windows/CraftingHelper.cs`, line 37:

```csharp
// BEFORE:
public sealed unsafe class RecipeNote : Window, IDisposable

// AFTER:
public sealed unsafe class CraftingHelper : Window, IDisposable
```

- [ ] **Step 3: Update constructor in CraftingHelper.cs**

Find the constructor (line ~85):

```csharp
// BEFORE:
public RecipeNote(global::Artificer.Plugin.Plugin plugin) : base(WindowNamePinned)

// AFTER:
public CraftingHelper(global::Artificer.Plugin.Plugin plugin) : base(WindowNamePinned)
```

- [ ] **Step 4: Update Plugin.cs**

In `Artificer/Plugin.cs`:

```csharp
// BEFORE (line 29):
public RecipeNote RecipeNoteWindow { get; }

// AFTER:
public CraftingHelper RecipeNoteWindow { get; }
```

The property name `RecipeNoteWindow` is kept unchanged — it is not a class name.  
The constructor call `RecipeNoteWindow = new(this);` needs no change (target-typed `new`).

- [ ] **Step 5: Update CraftingHelperStory.cs**

In `Artificer.UIStudio/Stories/Pages/CraftingHelperStory.cs`:

```csharp
// BEFORE:
internal sealed class RecipeNoteStory : IStory
{
    public string Category => "Pages";
    public string Name     => "RecipeNote";

// AFTER:
internal sealed class CraftingHelperStory : IStory
{
    public string Category => "Pages";
    public string Name     => "CraftingHelper";
```

- [ ] **Step 6: Update Program.cs**

In `Artificer.UIStudio/Program.cs`, update the story registration:

```csharp
// BEFORE:
new RecipeNoteStory(),

// AFTER:
new CraftingHelperStory(),
```

- [ ] **Step 7: Build to verify**

```powershell
& "C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" build Artificer/ Artificer.UIStudio/ -c Debug --no-restore 2>&1 | Select-String "error CS|Build succeeded|FAILED"
```

Expected: `Build succeeded`. Fix any missed references if errors appear.

- [ ] **Step 8: Commit**

```powershell
git add Artificer/Windows/CraftingHelper.cs Artificer/Plugin.cs "Artificer.UIStudio/Stories/Pages/CraftingHelperStory.cs" Artificer.UIStudio/Program.cs
git commit -m "refactor(ui): renomear RecipeNote -> CraftingHelper"
```

---

### Task 2: SynthHelper → SynthesisHelper

**Files:**
- Rename: `Artificer/Windows/SynthHelper.cs` → `Artificer/Windows/SynthesisHelper.cs`
- Rename: `Artificer/Windows/Settings.SynthHelper.cs` → `Artificer/Windows/Settings.SynthesisHelper.cs`
- Rename: `Artificer.UIStudio/Stories/Pages/SynthHelperStory.cs` → `Artificer.UIStudio/Stories/Pages/SynthesisHelperStory.cs`
- Modify: `Artificer/Windows/SynthesisHelper.cs` (class and constructor)
- Modify: `Artificer/Plugin.cs` (property type)
- Modify: `Artificer/Windows/Settings.SynthesisHelper.cs` (method name if desired — see note)
- Modify: `Artificer.UIStudio/Stories/Pages/SynthesisHelperStory.cs` (class and Name)
- Modify: `Artificer.UIStudio/Program.cs` (story registration)

- [ ] **Step 1: Rename files with git mv**

```powershell
git mv Artificer/Windows/SynthHelper.cs Artificer/Windows/SynthesisHelper.cs
git mv Artificer/Windows/Settings.SynthHelper.cs Artificer/Windows/Settings.SynthesisHelper.cs
git mv "Artificer.UIStudio/Stories/Pages/SynthHelperStory.cs" "Artificer.UIStudio/Stories/Pages/SynthesisHelperStory.cs"
```

- [ ] **Step 2: Update class declaration in SynthesisHelper.cs**

In `Artificer/Windows/SynthesisHelper.cs`, line 31:

```csharp
// BEFORE:
public sealed unsafe class SynthHelper : Window, IDisposable

// AFTER:
public sealed unsafe class SynthesisHelper : Window, IDisposable
```

- [ ] **Step 3: Update constructor in SynthesisHelper.cs**

Find the constructor (line ~57):

```csharp
// BEFORE:
public SynthHelper(global::Artificer.Plugin.Plugin plugin) : base(WindowNamePinned)

// AFTER:
public SynthesisHelper(global::Artificer.Plugin.Plugin plugin) : base(WindowNamePinned)
```

- [ ] **Step 4: Update Plugin.cs**

```csharp
// BEFORE (line 30):
public SynthHelper SynthHelperWindow { get; }

// AFTER:
public SynthesisHelper SynthHelperWindow { get; }
```

Property name `SynthHelperWindow` is kept. Constructor call `SynthHelperWindow = new(this);` needs no change.

- [ ] **Step 5: Update SynthesisHelperStory.cs**

```csharp
// BEFORE:
internal sealed class SynthHelperStory : IStory
{
    public string Category => "Pages";
    public string Name     => "SynthHelper";

// AFTER:
internal sealed class SynthesisHelperStory : IStory
{
    public string Category => "Pages";
    public string Name     => "SynthesisHelper";
```

- [ ] **Step 6: Update Program.cs**

```csharp
// BEFORE:
new SynthHelperStory(),

// AFTER:
new SynthesisHelperStory(),
```

- [ ] **Step 7: Build to verify**

```powershell
& "C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" build Artificer/ Artificer.UIStudio/ -c Debug --no-restore 2>&1 | Select-String "error CS|Build succeeded|FAILED"
```

Expected: `Build succeeded`.

- [ ] **Step 8: Commit**

```powershell
git add Artificer/Windows/SynthesisHelper.cs Artificer/Windows/Settings.SynthesisHelper.cs Artificer/Plugin.cs "Artificer.UIStudio/Stories/Pages/SynthesisHelperStory.cs" Artificer.UIStudio/Program.cs
git commit -m "refactor(ui): renomear SynthHelper -> SynthesisHelper"
```

---

### Task 3: MacroList → MacroLibrary

**Files:**
- Rename: `Artificer/Windows/MacroList.cs` → `Artificer/Windows/MacroLibrary.cs`
- Rename: `Artificer.UIStudio/Stories/Pages/MacroListStory.cs` → `Artificer.UIStudio/Stories/Pages/MacroLibraryStory.cs`
- Modify: `Artificer/Windows/MacroLibrary.cs` (class and constructor)
- Modify: `Artificer/Plugin.cs` (property type)
- Modify: `Artificer.UIStudio/Stories/Pages/MacroLibraryStory.cs` (class and Name)
- Modify: `Artificer.UIStudio/Program.cs` (story registration)

- [ ] **Step 1: Rename files with git mv**

```powershell
git mv Artificer/Windows/MacroList.cs Artificer/Windows/MacroLibrary.cs
git mv "Artificer.UIStudio/Stories/Pages/MacroListStory.cs" "Artificer.UIStudio/Stories/Pages/MacroLibraryStory.cs"
```

- [ ] **Step 2: Update class declaration in MacroLibrary.cs**

Find `class MacroList` near the top of the file:

```csharp
// BEFORE:
public sealed class MacroList : Window, IDisposable

// AFTER:
public sealed class MacroLibrary : Window, IDisposable
```

- [ ] **Step 3: Update constructor in MacroLibrary.cs**

Find the `MacroList(...)` constructor:

```csharp
// BEFORE:
public MacroList(global::Artificer.Plugin.Plugin plugin) : base(...)

// AFTER:
public MacroLibrary(global::Artificer.Plugin.Plugin plugin) : base(...)
```

- [ ] **Step 4: Update Plugin.cs**

```csharp
// BEFORE (line 31):
public MacroList ListWindow { get; private set; }

// AFTER:
public MacroLibrary ListWindow { get; private set; }
```

Property name `ListWindow` is kept. The `ListWindow = new(this);` call needs no change.

- [ ] **Step 5: Update MacroLibraryStory.cs**

```csharp
// BEFORE:
internal sealed class MacroListStory : IStory
{
    public string Category => "Pages";
    public string Name     => "MacroList";

// AFTER:
internal sealed class MacroLibraryStory : IStory
{
    public string Category => "Pages";
    public string Name     => "MacroLibrary";
```

- [ ] **Step 6: Update Program.cs**

```csharp
// BEFORE:
new MacroListStory(),

// AFTER:
new MacroLibraryStory(),
```

- [ ] **Step 7: Build to verify**

```powershell
& "C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" build Artificer/ Artificer.UIStudio/ -c Debug --no-restore 2>&1 | Select-String "error CS|Build succeeded|FAILED"
```

Expected: `Build succeeded`.

- [ ] **Step 8: Commit**

```powershell
git add Artificer/Windows/MacroLibrary.cs "Artificer.UIStudio/Stories/Pages/MacroLibraryStory.cs" Artificer/Plugin.cs Artificer.UIStudio/Program.cs
git commit -m "refactor(ui): renomear MacroList -> MacroLibrary"
```

---

### Task 4: FeatureHubWindow → FeatureHub

**Files:**
- Rename: `Artificer/Windows/FeatureHubWindow.cs` → `Artificer/Windows/FeatureHub.cs`
- Rename: `Artificer.UIStudio/Stories/Pages/FeatureHubWindowStory.cs` → `Artificer.UIStudio/Stories/Pages/FeatureHubStory.cs`
- Modify: `Artificer/Windows/FeatureHub.cs` (class and constructor)
- Modify: `Artificer/Plugin.cs` (property type AND property name — both had "Window" in them)
- Modify: `Artificer.UIStudio/Stories/Pages/FeatureHubStory.cs` (class and Name)
- Modify: `Artificer.UIStudio/Program.cs` (story registration)

> **⚠️ Property name change:** Unlike the other renames, here both the type (`FeatureHubWindow`) AND the property name (`FeatureHubWindow`) in Plugin.cs need to change to `FeatureHub`. Every usage of `plugin.FeatureHubWindow` or `FeatureHubWindow.Dispose()` etc. inside Plugin.cs must be updated.

- [ ] **Step 1: Rename files with git mv**

```powershell
git mv Artificer/Windows/FeatureHubWindow.cs Artificer/Windows/FeatureHub.cs
git mv "Artificer.UIStudio/Stories/Pages/FeatureHubWindowStory.cs" "Artificer.UIStudio/Stories/Pages/FeatureHubStory.cs"
```

- [ ] **Step 2: Update class declaration in FeatureHub.cs**

In `Artificer/Windows/FeatureHub.cs`, line 15:

```csharp
// BEFORE:
public sealed class FeatureHubWindow : Window, IDisposable

// AFTER:
public sealed class FeatureHub : Window, IDisposable
```

- [ ] **Step 3: Update constructor in FeatureHub.cs**

```csharp
// BEFORE:
public FeatureHubWindow(PluginClass plugin) : base("###Artificer-feature-hub", ...)

// AFTER:
public FeatureHub(PluginClass plugin) : base("###Artificer-feature-hub", ...)
```

- [ ] **Step 4: Update Plugin.cs — property type, property name, and all usages**

```csharp
// BEFORE (line 35):
public FeatureHubWindow FeatureHubWindow { get; }

// AFTER:
public FeatureHub FeatureHub { get; }
```

Then update the constructor assignment (line ~111):

```csharp
// BEFORE:
FeatureHubWindow = new(this);

// AFTER:
FeatureHub = new(this);
```

And update Dispose call (line ~257):

```csharp
// BEFORE:
FeatureHubWindow.Dispose();

// AFTER:
FeatureHub.Dispose();
```

Search Plugin.cs for any other `FeatureHubWindow` references and rename them to `FeatureHub`.

- [ ] **Step 5: Update FeatureHubStory.cs**

```csharp
// BEFORE:
internal sealed class FeatureHubWindowStory : IStory
{
    public string Category => "Pages";
    public string Name     => "FeatureHubWindow";

// AFTER:
internal sealed class FeatureHubStory : IStory
{
    public string Category => "Pages";
    public string Name     => "FeatureHub";
```

Also update any internal comment referencing `FeatureHubWindow` by name (e.g. line 28 of the original file: `"(FeatureHubWindow é uma janela flutuante..."` → `"(FeatureHub é uma janela flutuante..."`).

- [ ] **Step 6: Update Program.cs**

```csharp
// BEFORE:
new FeatureHubWindowStory(),

// AFTER:
new FeatureHubStory(),
```

- [ ] **Step 7: Search for remaining references**

```powershell
& "C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" build Artificer/ Artificer.UIStudio/ -c Debug --no-restore 2>&1 | Select-String "error CS"
```

If any `CS0246` (type not found) or `CS1061` (member not found) errors mention `FeatureHubWindow`, fix those references.

- [ ] **Step 8: Commit**

```powershell
git add Artificer/Windows/FeatureHub.cs Artificer/Plugin.cs "Artificer.UIStudio/Stories/Pages/FeatureHubStory.cs" Artificer.UIStudio/Program.cs
git commit -m "refactor(ui): renomear FeatureHubWindow -> FeatureHub"
```

---

### Task 5: CosmicTracker — window title change

**Files:**
- Modify: `Artificer/Windows/CosmicTracker.cs` (title string only, class unchanged)

- [ ] **Step 1: Update the window title**

In `Artificer/Windows/CosmicTracker.cs`, line 30, the constructor's `base(...)` call:

```csharp
// BEFORE:
"Cosmic Tool###Artificer-cosmic",

// AFTER:
"Cosmic Tracker###Artificer-cosmic",
```

The `###Artificer-cosmic` ImGui ID suffix MUST be preserved — it is what Dalamud uses to persist window position. Only the visible title prefix changes.

- [ ] **Step 2: Update the fallback string**

Still in `CosmicTracker.cs`, line 141, there is a fallback display string:

```csharp
// BEFORE:
var jobName = rawName.Length > 0 ? char.ToUpper(rawName[0]) + rawName[1..] : "Cosmic Tool";

// AFTER:
var jobName = rawName.Length > 0 ? char.ToUpper(rawName[0]) + rawName[1..] : "Cosmic Tracker";
```

- [ ] **Step 3: Build to verify**

```powershell
& "C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" build Artificer/ -c Debug --no-restore 2>&1 | Select-String "error CS|Build succeeded|FAILED"
```

Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```powershell
git add Artificer/Windows/CosmicTracker.cs
git commit -m "refactor(ui): renomear título da janela Cosmic Tool -> Cosmic Tracker"
```

---

### Task 6: Full build and deploy

- [ ] **Step 1: Full build + deploy**

```powershell
.\scripts\build.ps1 -Deploy
```

Expected: builds all projects, deploys the plugin, no errors.

- [ ] **Step 2: Visual verification (in-game)**

After deploying:
1. Open the Crafting Helper window — it should open as before (same functionality).
2. Open the Synthesis Helper window — it should open as before.
3. Open the Macro Library window — it should open as before.
4. Open the Feature Hub — it should open as before (no visible title, since it uses `###Artificer-feature-hub`).
5. Open Cosmic Tracker — the window title bar should now read "Cosmic Tracker" instead of "Cosmic Tool". Window position should be retained from before.
