# Suggested Macro UX — Loading Overlay & Copy Feedback — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Corrigir o bug do estado de loading ao regenerar, implementar overlay sem layout-shift e feedback visual de cópia no painel Suggested Macro.

**Architecture:** Todas as mudanças ficam em `CraftingHelper.cs`. Dois novos campos de instância rastreiam o snapshot anterior (para overlay) e o timestamp de cópia (para o feedback). A lógica do estado `isPrefilled` ganha uma guarda `!isRegenerating`. O `DrawMacro` ganha um branch de overlay antes do branch de loading existente.

**Tech Stack:** C# 12, ImGui.NET via Dalamud (`ImRaii`, `ImGuiStyleVar`, `FontAwesomeIcon`), `PluginImGuiUtils.DrawMacroStatArcs`, `ProgressBarComponent`.

**Spec:** `docs/superpowers/specs/2026-06-14-suggested-macro-ux-design.md`

---

### Task 1: Adicionar campos de instância e novos membros em `MacroTaskState`

**Files:**
- Modify: `Artificer/Windows/CraftingHelper.cs:69-75` (campos de instância)
- Modify: `Artificer/Windows/CraftingHelper.cs:883-896` (struct `MacroTaskState`)

**Contexto:** `MacroTaskState` é um `record struct` em torno da linha 883. Os campos de instância da janela ficam em torno das linhas 69-82.

- [ ] **Step 1: Adicionar campos de instância em `CraftingHelper`**

Localize o bloco de campos privados (próximo de `_currentCharacterHash` na linha 69). Adicione logo após `private int? _currentCharacterHash;`:

```csharp
private IReadOnlyList<ActionType>? _prevSuggestedActions;
private SimulationState?           _prevSuggestedState;
private readonly Dictionary<MacroTaskType, DateTimeOffset> _copiedAt = new();
```

- [ ] **Step 2: Adicionar membros em `MacroTaskState`**

Localize o `record struct MacroTaskState` (~linha 883). Adicione dois novos campos após `public bool IsPrefilled;`:

```csharp
public bool IsPrefilled;      // only valid for MacroTaskType.Suggested
public bool IsRegenerating;   // only valid for MacroTaskType.Suggested
public (IReadOnlyList<ActionType> Actions, SimulationState State)? RegeneratingSnapshot;
```

- [ ] **Step 3: Build para garantir que compila**

```powershell
& "C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" build Artificer/Artificer.csproj -c Debug --no-restore 2>&1 | Select-String -NotMatch "^Build succeeded" | Select-Object -First 30
```

Esperado: saída limpa, sem erros de compilação.

- [ ] **Step 4: Commit**

```bash
git add Artificer/Windows/CraftingHelper.cs
git commit -m "feat(ui): adicionar campos de overlay e copiedAt em CraftingHelper"
```

---

### Task 2: Capturar snapshot anterior em `CalculateSuggestedMacro()`

**Files:**
- Modify: `Artificer/Windows/CraftingHelper.cs:1336-1363` (método `CalculateSuggestedMacro`)

**Contexto:** O método começa em ~linha 1336 com `SuggestedMacroTask?.Cancel();`. Precisamos capturar o resultado existente ANTES de cancelar.

- [ ] **Step 1: Inserir captura do snapshot antes do Cancel**

Substitua o início do método:

```csharp
// ANTES (linha 1336-1338):
private void CalculateSuggestedMacro()
{
    SuggestedMacroTask?.Cancel();
```

por:

```csharp
private void CalculateSuggestedMacro()
{
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
```

- [ ] **Step 2: Build**

```powershell
& "C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" build Artificer/Artificer.csproj -c Debug --no-restore 2>&1 | Select-String -NotMatch "^Build succeeded" | Select-Object -First 30
```

Esperado: sem erros.

- [ ] **Step 3: Commit**

```bash
git add Artificer/Windows/CraftingHelper.cs
git commit -m "fix(ui): capturar snapshot anterior ao regenerar Suggested Macro"
```

---

### Task 3: Corrigir `isPrefilled` e popular `IsRegenerating` / `RegeneratingSnapshot`

**Files:**
- Modify: `Artificer/Windows/CraftingHelper.cs:462-481` (bloco de build-state do Suggested Macro)

**Contexto:** O bloco começa em ~linha 462 com `var solverResult = SuggestedMacroTask?.Result;`. O bug está na linha 468 onde `isPrefilled` não exclui o caso de regeneração.

- [ ] **Step 1: Atualizar o bloco de build-state**

Substitua o bloco inteiro (linhas 462-481):

```csharp
// ANTES:
{
    var solverResult  = SuggestedMacroTask?.Result;
    var solverDone    = SuggestedMacroTask?.Completed ?? false;
    var savedResult   = SavedMacroTask?.Result;
    var hashMatch     = savedResult?.Item3;
    var hashMatchState = savedResult?.Item4;
    var isPrefilled   = hashMatch != null && !solverDone;

    var state = new MacroTaskState()
    {
        Type      = MacroTaskType.Suggested,
        Exception = SuggestedMacroTask?.Exception,
        Started   = SuggestedMacroTask != null || isPrefilled,
        Completed = solverDone || isPrefilled,
        Actions   = solverResult?.Actions ?? (isPrefilled ? hashMatch!.Actions : null),
        State     = solverResult?.State   ?? (isPrefilled ? hashMatchState     : null),
        Solver    = BestMacroSolver,
        IsPrefilled = isPrefilled && solverResult == null,
    };
    DrawMacro(in state, panelWidth);
}
```

por:

```csharp
{
    var solverResult   = SuggestedMacroTask?.Result;
    var solverDone     = SuggestedMacroTask?.Completed ?? false;
    var savedResult    = SavedMacroTask?.Result;
    var hashMatch      = savedResult?.Item3;
    var hashMatchState = savedResult?.Item4;
    var isRegenerating = _prevSuggestedActions != null
                      && SuggestedMacroTask is { Completed: false };
    var isPrefilled    = hashMatch != null && !solverDone && !isRegenerating;

    var state = new MacroTaskState()
    {
        Type      = MacroTaskType.Suggested,
        Exception = SuggestedMacroTask?.Exception,
        Started   = SuggestedMacroTask != null || isPrefilled,
        Completed = solverDone || isPrefilled,
        Actions   = solverResult?.Actions ?? (isPrefilled ? hashMatch!.Actions : null),
        State     = solverResult?.State   ?? (isPrefilled ? hashMatchState     : null),
        Solver    = BestMacroSolver,
        IsPrefilled          = isPrefilled && solverResult == null,
        IsRegenerating       = isRegenerating,
        RegeneratingSnapshot = isRegenerating
            ? (_prevSuggestedActions!, _prevSuggestedState!.Value)
            : null,
    };

    if (solverDone)
    {
        _prevSuggestedActions = null;
        _prevSuggestedState   = null;
    }

    DrawMacro(in state, panelWidth);
}
```

- [ ] **Step 2: Build**

```powershell
& "C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" build Artificer/Artificer.csproj -c Debug --no-restore 2>&1 | Select-String -NotMatch "^Build succeeded" | Select-Object -First 30
```

Esperado: sem erros.

- [ ] **Step 3: Commit**

```bash
git add Artificer/Windows/CraftingHelper.cs
git commit -m "fix(ui): corrigir isPrefilled para exibir loading ao regenerar"
```

---

### Task 4: Renderizar overlay de regeneração em `DrawMacro`

**Files:**
- Modify: `Artificer/Windows/CraftingHelper.cs:960-988` (branch `!state.Completed` em `DrawMacro`)

**Contexto:** O branch `else if (!state.Completed)` começa em ~linha 960. O case `MacroTaskType.Suggested` (linha 967) exibe a progress bar normalmente. Precisamos adicionar um branch ANTES desse `else if` para o caso `IsRegenerating` — que desenha o card anterior dimmed e sobrepõe a progress bar.

O card completo usa estas variáveis locais calculadas dentro do `else` final (linha ~1062):
```csharp
var spacing   = ImGui.GetStyle().ItemSpacing;
var miniRowH  = (windowHeight - spacing.Y) / 2f;
var arcColW   = miniRowH * 2 + spacing.X;
var botRowH   = ImGui.GetFrameHeight();
var innerW    = panelWidth;
var rightColW = MathF.Max(1f, innerW - arcColW - 1f);
```

- [ ] **Step 1: Inserir branch `IsRegenerating` antes do `else if (!state.Completed)`**

Localize `else if (!state.Completed)` em `DrawMacro` (~linha 960). Insira imediatamente ANTES dele:

```csharp
else if (state.IsRegenerating && state.RegeneratingSnapshot is { } rsnap)
{
    // Desenha o card anterior dimmed como âncora de tamanho
    var cardTopPos = ImGui.GetCursorPos();
    var spacing    = ImGui.GetStyle().ItemSpacing;
    var miniRowH   = (windowHeight - spacing.Y) / 2f;
    var arcColW    = miniRowH * 2 + spacing.X;
    var innerW     = panelWidth;
    var rightColW  = MathF.Max(1f, innerW - arcColW - 1f);
    var botRowH    = ImGui.GetFrameHeight();

    using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.25f))
    {
        using var table = ImRaii.Table("macroCardRegen", 2,
            ImGuiTableFlags.None,
            new Vector2(innerW, 0));
        if (table)
        {
            ImGui.TableSetupColumn("left",  ImGuiTableColumnFlags.WidthFixed, arcColW);
            ImGui.TableSetupColumn("right", ImGuiTableColumnFlags.WidthFixed, rightColW);

            // Row 1: arcs | action icons
            ImGui.TableNextRow(ImGuiTableRowFlags.None, windowHeight);
            ImGui.TableSetColumnIndex(0);
            PluginImGuiUtils.DrawMacroStatArcs(rsnap.State, windowHeight, asGrid: true);

            ImGui.TableSetColumnIndex(1);
            {
                var itemsPerRow = (int)MathF.Floor((rightColW + spacing.X) / (miniRowH + spacing.X));
                itemsPerRow     = Math.Max(1, itemsPerRow);
                var itemCount   = rsnap.Actions.Count;
                for (var i = 0; i < itemsPerRow * 2; i++)
                {
                    if (i % itemsPerRow != 0)
                        ImGui.SameLine(0, spacing.X);
                    if (i < itemCount)
                        ImGui.Image(rsnap.Actions[i].GetIcon(RecipeData!.ClassJob).Handle, new(miniRowH));
                    else
                        ImGui.Dummy(new(miniRowH));
                }
            }

            // Row 2: HQ% | name placeholder
            ImGui.TableNextRow(ImGuiTableRowFlags.None, botRowH);
            ImGui.TableSetColumnIndex(0);
            {
                var hqPct = rsnap.State.HQPercent;
                ImGui.AlignTextToFramePadding();
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                    ImGuiUtils.TextCentered($"{hqPct}%", arcColW);
            }
            ImGui.TableSetColumnIndex(1);
            {
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted("Regenerando...");
                }
            }
        }
    }

    // Overlay: progress bar centrada verticalmente sobre o card
    if (state.Solver is { } regenSolver)
    {
        var cardH     = windowHeight + spacing.Y + botRowH;
        var snapshot  = SolverProgressBar.FromSolver(regenSolver, "Solver");
        var barConfig = new ProgressBarComponent.VisualConfig(
            Mode: ProgressBarComponent.DisplayMode.Horizontal,
            ColorTheme: _plugin.Configuration.ProgressType,
            Width: panelWidth,
            ShowPercentage: true,
            ShowDetailedTooltip: true
        );

        // Fundo semi-transparente — posicionar cursor antes de obter screenPos
        var barH     = ImGui.GetFrameHeightWithSpacing();
        var overlayY = cardTopPos.Y + (cardH - barH) / 2f;
        ImGui.SetCursorPos(new Vector2(cardTopPos.X, overlayY));
        var screenMin = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddRectFilled(
            screenMin,
            screenMin + new Vector2(panelWidth, barH),
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.08f, 0.14f, 0.85f)),
            3f);

        ProgressBarComponent.DrawSingle(snapshot, barConfig);
    }
}
```

- [ ] **Step 2: Build**

```powershell
& "C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" build Artificer/Artificer.csproj -c Debug --no-restore 2>&1 | Select-String -NotMatch "^Build succeeded" | Select-Object -First 30
```

Esperado: sem erros.

- [ ] **Step 3: Commit**

```bash
git add Artificer/Windows/CraftingHelper.cs
git commit -m "feat(ui): overlay de regeneração com card dimmed e progress bar no Suggested Macro"
```

---

### Task 5: Feedback visual de cópia (ícone → ✓ por 2s)

**Files:**
- Modify: `Artificer/Windows/CraftingHelper.cs:1147-1161` (botões de copy/edit no card completo)

**Contexto:** O botão de cópia está em ~linha 1158:
```csharp
if (ImGuiUtils.IconButtonSquare((int)FontAwesomeIcon.Paste, iconH))
    MacroCopy.Copy(actions, _plugin);
if (ImGui.IsItemHovered())
    ImGuiUtils.Tooltip("Copy to Clipboard");
```

- [ ] **Step 1: Substituir o bloco do botão de cópia**

Substitua as 4 linhas do botão de cópia (paste button + tooltip) por:

```csharp
var justCopied = _copiedAt.TryGetValue(state.Type, out var copiedAt)
              && (DateTimeOffset.UtcNow - copiedAt).TotalSeconds < 2.0;
var copyIcon   = justCopied ? FontAwesomeIcon.Check : FontAwesomeIcon.Paste;
bool copyClicked;
if (justCopied)
{
    using (ImRaii.PushColor(ImGuiCol.Text, Colors.Progress))
        copyClicked = ImGuiUtils.IconButtonSquare((int)copyIcon, iconH);
}
else
{
    copyClicked = ImGuiUtils.IconButtonSquare((int)copyIcon, iconH);
}
if (copyClicked)
{
    MacroCopy.Copy(actions, _plugin);
    _copiedAt[state.Type] = DateTimeOffset.UtcNow;
}
if (ImGui.IsItemHovered())
    ImGuiUtils.Tooltip(justCopied ? "Copied!" : "Copy to Clipboard");
```

- [ ] **Step 2: Build**

```powershell
& "C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe" build Artificer/Artificer.csproj -c Debug --no-restore 2>&1 | Select-String -NotMatch "^Build succeeded" | Select-Object -First 30
```

Esperado: sem erros.

- [ ] **Step 3: Commit**

```bash
git add Artificer/Windows/CraftingHelper.cs
git commit -m "feat(ui): feedback visual de copia — icone muda para check por 2s"
```

---

### Task 6: Verificação manual no jogo

**Files:** nenhum — apenas verificação comportamental.

**Contexto:** Este é um plugin Dalamud renderizado dentro do FFXIV. Não há testes unitários para lógica de UI/ImGui. Verificar os três cenários manualmente no jogo.

- [ ] **Step 1: Deploy do plugin**

Use o comando `/deploy` para instalar o build atualizado no FFXIV.

- [ ] **Step 2: Verificar BUG — loading ao regenerar**

1. Abra o Crafting Helper em uma receita.
2. Clique "Suggest Macro" e aguarde o resultado aparecer.
3. Clique "Regenerate".
4. **Esperado:** o card anterior aparece dimmed com a progress bar do solver sobreposta. O painel NÃO exibe o resultado anterior como se estivesse completo.

- [ ] **Step 3: Verificar FEAT — no layout shift**

1. Observe o painel "Suggested Macro" durante a regeneração.
2. **Esperado:** o tamanho vertical do painel não muda — o card dimmed mantém a altura do card completo. A progress bar aparece no centro sem redimensionar o painel.

- [ ] **Step 4: Verificar FEAT — feedback de cópia**

1. Com um resultado de macro exibido, clique o botão 📋.
2. **Esperado:** o ícone muda para ✓ verde imediatamente. O tooltip mostra "Copied!" ao passar o mouse. Após ~2 segundos, volta para 📋 e "Copy to Clipboard".
3. Testar nas três abas: Saved Macro, Suggested Macro, Community Macro — cada uma deve rastrear o estado de cópia independentemente.

- [ ] **Step 5: Commit final se necessário**

Se houver ajustes finos de posicionamento do overlay após o teste visual:

```bash
git add Artificer/Windows/CraftingHelper.cs
git commit -m "fix(ui): ajuste fino no posicionamento do overlay de regeneração"
```
