# Suggested Macro UX — Loading Overlay & Copy Feedback

**Date:** 2026-06-14
**Scope:** `Artificer/Windows/CraftingHelper.cs`

---

## Problem Summary

Three issues with the Suggested Macro panel:

1. **BUG:** When a result is already displayed and the user clicks "Regenerate", the loading state is never shown. The `isPrefilled` condition (`hashMatch != null && !solverDone`) evaluates to `true` because the new task starts with `Completed = false` — masking the loading branch entirely.
2. **FEAT:** The existing loading state (ProgressBarComponent) has a different height than the result card, causing layout shift when the panel transitions between states.
3. **FEAT:** After clicking the copy button (📋), there is no visual feedback indicating the macro was copied.

---

## Design

### 1. Regeneration overlay (fixes BUG + no-layout-shift FEAT)

**New state on `CraftingHelper`:**

```csharp
private List<ActionType>? _prevSuggestedActions;
private SimulationState?  _prevSuggestedState;
```

Set in `CalculateSuggestedMacro()` when called with an existing result:

```csharp
private void CalculateSuggestedMacro()
{
    // Capture previous result before cancelling
    if (SuggestedMacroTask?.Result is { } prev)
    {
        _prevSuggestedActions = prev.Actions;
        _prevSuggestedState   = prev.State;
    }
    else
    {
        _prevSuggestedActions = null;
        _prevSuggestedState   = null;
    }
    SuggestedMacroTask?.Cancel();
    // ... existing task creation ...
}
```

Cleared when the new task completes (in the draw loop or task callback — clearing on `solverDone` in the build-state block is sufficient).

**New fields on `MacroTaskState`:**

```csharp
public bool IsRegenerating;
public (List<ActionType> Actions, SimulationState State)? RegeneratingSnapshot;
```

**Updated state-building block (Suggested branch, ~line 462):**

```csharp
var isRegenerating = _prevSuggestedActions != null
                  && SuggestedMacroTask is { Completed: false };
var isPrefilled    = hashMatch != null && !solverDone && !isRegenerating; // ← bug fix

var state = new MacroTaskState()
{
    // ... existing fields ...
    IsPrefilled          = isPrefilled && solverResult == null,
    IsRegenerating       = isRegenerating,
    RegeneratingSnapshot = isRegenerating
        ? (_prevSuggestedActions!, _prevSuggestedState!.Value)
        : null,
};

// Clear stored snapshot once the solver finishes
if (solverDone)
{
    _prevSuggestedActions = null;
    _prevSuggestedState   = null;
}
```

**New rendering branch in `DrawMacro` — `!state.Completed && state.IsRegenerating`:**

Inserted before the existing `!state.Completed` branch for `MacroTaskType.Suggested`. The branch:

1. Draws the card layout (arcs + action icons + HQ% row) from `state.RegeneratingSnapshot` using `ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.35f)` to dim it.
2. Resets `SetCursorPos` back to the top of the card.
3. Draws the ProgressBarComponent (same as the existing loading branch) overlaid on top.

The panel height stays identical to a completed card — the dimmed card acts as the size anchor.

---

### 2. Copy feedback (icon → ✓ for 2 seconds)

**New state on `CraftingHelper`:**

```csharp
private readonly Dictionary<MacroTaskType, DateTimeOffset> _copiedAt = new();
```

**On copy click (~line 1158):**

```csharp
if (ImGuiUtils.IconButtonSquare((int)copyIcon, iconH))
{
    MacroCopy.Copy(actions, _plugin);
    _copiedAt[state.Type] = DateTimeOffset.UtcNow;
}
```

**Icon and color selection (computed before the button draw):**

```csharp
var justCopied = _copiedAt.TryGetValue(state.Type, out var copiedAt)
              && (DateTimeOffset.UtcNow - copiedAt).TotalSeconds < 2.0;
var copyIcon   = justCopied ? FontAwesomeIcon.Check : FontAwesomeIcon.Paste;
```

If `justCopied`:
- `ImRaii.PushColor(ImGuiCol.Text, Colors.Progress)` wraps the button to render it green.
- Tooltip shows `"Copied!"` instead of `"Copy to Clipboard"`.

No timers or background threads — the ImGui frame loop drives the 2-second timeout naturally.

---

## Affected Files

| File | Changes |
|------|---------|
| `Artificer/Windows/CraftingHelper.cs` | `_prevSuggestedActions`, `_prevSuggestedState`, `_copiedAt` fields; `CalculateSuggestedMacro()` capture logic; state-building block update; `MacroTaskState` new fields; `DrawMacro` new rendering branch and copy button update |

---

## Non-Goals

- No changes to Saved Macro or Community Macro loading states (they don't have the same prefill complexity).
- No toast/notification system — feedback is inline only.
- No persistence of copy state across panel redraws beyond the 2-second window.
