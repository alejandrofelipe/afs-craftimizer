# Gear Alert Reposition + Text Fix + Build Default

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the Gear Condition alert to between the stats and macro panels, fix the clipped message text, and make `.\scripts\build.ps1` default to Release.

**Architecture:** Three independent single-file changes: (1) remove `(no data)` from `BuildGearMessage`, (2) move the `DrawAlert` call from inside the GroupPanel to after the early-return in `Draw()`, (3) change the default `$Configuration` param in `build.ps1` from `Debug` to `Release`.

**Tech Stack:** C# / MSTest / ImGui.NET / Dalamud / PowerShell

---

## Files

| File | Change |
|------|--------|
| `Artificer/Utils/UI/PluginImGuiUtils.GearCondition.cs` | Drop `(no data)` suffix from zero-confidence case |
| `Artificer/Windows/CraftingHelper.cs` | Remove alert block from inside GroupPanel; add it after early return |
| `scripts/build.ps1` | Change default `$Configuration` from `"Debug"` to `"Release"` |
| `Test/UI/GearMessageTests.cs` | New — regression tests for `BuildGearMessage` format |

---

### Task 1: Drop `(no data)` from BuildGearMessage

**Files:**
- Modify: `Artificer/Utils/UI/PluginImGuiUtils.GearCondition.cs:24`
- Create: `Test/UI/GearMessageTests.cs`

- [ ] **Step 1: Create the test file**

```csharp
// Test/UI/GearMessageTests.cs
using Artificer.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Artificer.Test.UI;

[TestClass]
public class GearMessageTests
{
    [TestMethod]
    public void BuildGearMessage_TrackingDisabled_ReturnsRepairMessage()
    {
        var result = PluginImGuiUtils.BuildGearMessage(27f, false, null, null!);
        Assert.AreEqual("27% — Repair gear before continuing!", result);
    }

    [TestMethod]
    public void BuildGearMessage_RecipeDataNull_ReturnsRepairMessage()
    {
        var result = PluginImGuiUtils.BuildGearMessage(95f, true, null, null!);
        Assert.AreEqual("95% — Repair gear before continuing!", result);
    }

    [TestMethod]
    public void BuildGearMessage_TrackingDisabled_NoParenNoData()
    {
        // Verifica que nenhuma variante produz "(no data)" quando tracking está desabilitado
        var result = PluginImGuiUtils.BuildGearMessage(50f, false, null, null!);
        StringAssert.DoesNotMatch(result, new System.Text.RegularExpressions.Regex(@"\(no data\)"));
    }
}
```

- [ ] **Step 2: Run tests to verify they pass (os três são independentes do tracker)**

```powershell
"C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" test Test/Artificer.Test.csproj --filter "FullyQualifiedName~GearMessageTests" -v minimal
```

Expected: `3 passed` (os casos sem tracker passam — `BuildGearMessage` retorna antes de usar o tracker quando `enableTracking=false || recipeData==null`).

- [ ] **Step 3: Verificar o caso atual que produz `(no data)`**

Ler `Artificer/Utils/UI/PluginImGuiUtils.GearCondition.cs` — linha 24:

```csharp
{ } e => $"{pct:0}% · ~{e.MinCrafts} crafts left (no data)",
```

Confirmar que este é o único lugar onde `(no data)` aparece.

- [ ] **Step 4: Remover `(no data)` — alterar linha 24**

**Antes:**
```csharp
{ } e                  => $"{pct:0}% · ~{e.MinCrafts} crafts left (no data)",
```

**Depois:**
```csharp
{ } e                  => $"{pct:0}% · ~{e.MinCrafts} crafts left",
```

O método completo depois da mudança:

```csharp
namespace Artificer.Utils;

internal static partial class PluginImGuiUtils
{
    public static string BuildGearMessage(
        float pct,
        bool enableTracking,
        RecipeData? recipeData,
        GearWearTracker tracker)
    {
        if (!enableTracking || recipeData == null)
            return $"{pct:0}% — Repair gear before continuing!";

        var recipe      = recipeData.Recipe;
        var recipeLevel = (ushort)recipeData.Table.RowId;
        var estimate    = tracker.EstimateCraftsRemaining(recipe.RowId, recipeLevel);

        return estimate switch
        {
            null                   => $"{pct:0}% — Repair gear before continuing!",
            { Confidence: > 0f } e => e.MinCrafts == e.MaxCrafts
                ? $"{pct:0}% · ~{e.MinCrafts} crafts left"
                : $"{pct:0}% · ~{e.MinCrafts}–{e.MaxCrafts} crafts left",
            { } e                  => $"{pct:0}% · ~{e.MinCrafts} crafts left",
        };
    }
}
```

- [ ] **Step 5: Rodar todos os testes**

```powershell
"C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" test 2>&1 | Select-String -Pattern "passed|failed|FAILED" | Select-Object -Last 3
```

Expected: todos os testes passam (baseline: 211 + 3 novos = 214 passed, 0 failed).

- [ ] **Step 6: Commitar**

```powershell
git add Test/UI/GearMessageTests.cs Artificer/Utils/UI/PluginImGuiUtils.GearCondition.cs
git commit -m "fix(ui): BuildGearMessage — remove (no data) suffix from zero-confidence case"
```

---

### Task 2: Mover o alerta para fora do GroupPanel

**Files:**
- Modify: `Artificer/Windows/CraftingHelper.cs` — método `Draw()` (linhas ~433–451 e ~463)

> Nota: este é código ImGui — não há testes unitários viáveis. O critério de sucesso é: build sem warnings + verificação visual em jogo.

- [ ] **Step 1: Localizar e remover o bloco de gear condition dentro do GroupPanel**

Em `CraftingHelper.cs`, no método `Draw()`, localizar este bloco (está dentro do `if (crPanel)`, após o fechamento da `using var table`):

```csharp
                if (CraftStatus == CraftableStatus.OK && _plugin.Configuration.ShowGearCondition)
                {
                    var gearCondition = Gearsets.GetMinimumGearCondition();
                    if (gearCondition.HasValue)
                    {
                        var pct     = gearCondition.Value;
                        var variant = pct < 25f ? AlertVariant.Danger
                                    : pct < 50f ? AlertVariant.Warning
                                    :             AlertVariant.Info;
                        var message = PluginImGuiUtils.BuildGearMessage(
                            pct,
                            _plugin.Configuration.EnableGearWearTracking,
                            RecipeData,
                            _plugin.GearWearTracker);

                        ImGuiHelpers.ScaledDummy(2);
                        ImGuiUtils.DrawAlert(variant, "Gear Condition", message, ImGuiHelpers.GlobalScale, tableW);
                    }
                }
```

**Deletar este bloco inteiramente** (incluindo o `ImGuiHelpers.ScaledDummy(2)`).

- [ ] **Step 2: Adicionar o alerta após `var panelWidth = ...`**

Localizar esta sequência no mesmo método `Draw()`:

```csharp
        var availWidth = gpWidth;
        var panelWidth = availWidth - ImGui.GetStyle().ItemSpacing.X * 2;

        {
            var savedResult = SavedMacroTask?.Result;
```

Inserir o bloco de gear condition entre `var panelWidth = ...` e a abertura do bloco do macro. O resultado deve ser:

```csharp
        var availWidth = gpWidth;
        var panelWidth = availWidth - ImGui.GetStyle().ItemSpacing.X * 2;

        if (_plugin.Configuration.ShowGearCondition)
        {
            var gearCondition = Gearsets.GetMinimumGearCondition();
            if (gearCondition.HasValue)
            {
                var pct     = gearCondition.Value;
                var variant = pct < 25f ? AlertVariant.Danger
                            : pct < 50f ? AlertVariant.Warning
                            :             AlertVariant.Info;
                var message = PluginImGuiUtils.BuildGearMessage(
                    pct,
                    _plugin.Configuration.EnableGearWearTracking,
                    RecipeData,
                    _plugin.GearWearTracker);
                ImGuiUtils.DrawAlert(variant, "Gear Condition", message, ImGuiHelpers.GlobalScale, availWidth);
                ImGui.Spacing();
            }
        }

        {
            var savedResult = SavedMacroTask?.Result;
```

Pontos-chave:
- `ShowGearCondition` (sem o `CraftStatus == OK` — estamos após o early return, já garantido OK)
- `availWidth` (não `tableW`) — a largura total do conteúdo da janela
- `ImGui.Spacing()` após o alerta separa-o do primeiro macro panel
- `ScaledDummy(2)` foi removido (era espaçamento interno do GroupPanel, não necessário fora)

- [ ] **Step 3: Build**

```powershell
.\scripts\build.ps1 2>&1 | Select-String -Pattern "warning|error|Build succeeded|FAILED"
```

Expected: `Build succeeded.` com 0 warnings e 0 errors.

- [ ] **Step 4: Deploy e verificar visualmente**

```powershell
.\scripts\build.ps1
```

Verificar em jogo:
- O alerta "GEAR CONDITION" aparece entre o painel "Crafter / Recipe" e "Best Saved Macro"
- O texto não está cortado (sem `(no data)`)
- A janela não cresce lateralmente após o alerta aparecer
- Quando `ShowGearCondition = false` nas configurações, o alerta não aparece

- [ ] **Step 5: Commitar**

```powershell
git add Artificer/Windows/CraftingHelper.cs
git commit -m "feat(ui): gear alert — mover para fora do GroupPanel, entre stats e macros"
```

---

### Task 3: Build script default → Release

**Files:**
- Modify: `scripts/build.ps1:49`

- [ ] **Step 1: Localizar o parâmetro `$Configuration` no script**

Em `scripts/build.ps1`, localizar (em torno da linha 49):

```powershell
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
```

- [ ] **Step 2: Mudar o default para `Release`**

```powershell
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
```

- [ ] **Step 3: Verificar que o script ainda aceita `-Configuration Debug`**

```powershell
.\scripts\build.ps1 -Configuration Debug 2>&1 | Select-String -Pattern "Building|Build succeeded|FAILED"
```

Expected: `Building Artificer X.X.X.X (Debug)...` seguido de `Build succeeded.`

- [ ] **Step 4: Verificar que o default agora é Release**

```powershell
.\scripts\build.ps1 2>&1 | Select-String -Pattern "Building|Build succeeded|FAILED"
```

Expected: `Building Artificer X.X.X.X (Release)...` seguido de `Build succeeded.`

- [ ] **Step 5: Commitar**

```powershell
git add scripts/build.ps1
git commit -m "chore(build): default Configuration Release para dev game server"
```

---

## Troubleshooting

**Build falha em `PluginImGuiUtils.BuildGearMessage`:**
- Verificar que a linha 24 tem apenas `=> $"{pct:0}% · ~{e.MinCrafts} crafts left",` (sem `(no data)`)

**Alerta ainda aparece dentro do GroupPanel:**
- Verificar que o bloco `if (CraftStatus == CraftableStatus.OK && _plugin.Configuration.ShowGearCondition)` foi deletado do interior do `if (crPanel)` block
- Verificar que o novo bloco está após `var panelWidth = ...`, não antes

**Janela crescendo após mudança:**
- O `availWidth = gpWidth` é calculado do `GetItemRectSize()` após o GroupPanel fechar (linha 454). O alerta usa esse valor. Se houver crescimento, verificar se `DrawAlert` está recebendo `availWidth` (não `gpWidth` diretamente — são o mesmo valor, mas para consistência usar `availWidth`)

**`.\scripts\build.ps1` ainda buildando Debug após Task 3:**
- Verificar que a linha 49 foi alterada para `[string]$Configuration = "Release"`
- Verificar que não há outro arquivo de build ou alias que override o parâmetro
