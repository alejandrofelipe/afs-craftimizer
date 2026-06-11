# UIStudio Pages Stories + `/update-studio` Skill — Design Spec

**Data:** 2026-06-10
**Status:** Aprovado
**Escopo:** `Craftimizer.UIStudio/Stories/Pages/` (novo subfolder) + `.claude/commands/update-studio.md`

---

## Objetivo

Adicionar o nível **Pages** ao Atomic Design do UIStudio: 12 stories que reproduzem as janelas reais do plugin com dados mockados e todos os estados relevantes visíveis via controles interativos. Em paralelo, criar a skill `/update-studio` que mantém as stories em sincronia com o código real quando janelas são adicionadas, modificadas ou removidas.

---

## Arquitetura

### Estrutura de arquivos

```
Craftimizer.UIStudio/
  Stories/
    ← existentes (Atoms, Molecules, Templates) — sem alteração
    Pages/
      MacroEditorStory.cs
      SynthHelperStory.cs
      RecipeNoteStory.cs
      CosmicTrackerStory.cs
      MacroClipboardStory.cs
      MacroListStory.cs
      SettingsStory.cs
      CraftingListWindowStory.cs
      CraftingListAddWindowStory.cs
      CraftingListDetailWindowStory.cs
      CraftingListMergeWindowStory.cs
      FeatureHubWindowStory.cs
  Program.cs  ← +12 stories registradas
.claude/commands/
  update-studio.md  ← nova skill
```

### Padrão de cada story (abordagem B)

Cada story implementa `IStory` com `Category = "Pages"`. O topo do `Draw()` expõe os controles de estado; abaixo vem o layout da janela correspondente.

```csharp
internal sealed class ExampleStory : IStory
{
    public string Category => "Pages";
    public string Name     => "ExampleWindow";

    private static readonly string[] s_estados = ["Estado A", "Estado B", "Estado C"];
    private int _estado;
    private bool _toggleX;

    public void Draw()
    {
        // ── Controles de estado ───────────────────────────
        ImGui.SetNextItemWidth(220);
        ImGui.Combo("Estado", ref _estado, s_estados, s_estados.Length);
        ImGui.SameLine();
        ImGui.Checkbox("Toggle X", ref _toggleX);
        ImGui.Separator();
        ImGui.Spacing();

        // ── Layout da janela ──────────────────────────────
        switch (_estado)
        {
            case 0: DrawEstadoA(); break;
            case 1: DrawEstadoB(); break;
            case 2: DrawEstadoC(); break;
        }
    }
}
```

### Regras de implementação

- **Sem Dalamud** — nenhuma referência a `Service`, `Plugin`, Lumina, FFXIVClientStructs
- **Ícones de ação** — `FontAwesomeIcon` como placeholder (padrão das stories existentes)
- **Dados mockados** — hardcoded na story, sem arquivos externos
- **Fidelidade** — reproduzir layout e componentes da janela real; dados não precisam ser pixel-perfect
- **Theme** — usar `Theme.Push()` / `Theme.Pop()` quando a janela real o usa

---

## Stories — Estados por Janela

### 1. MacroEditorStory

**Controles:**
- Combo `Estado`: `Sem receita` | `Com receita` | `Solver rodando` | `Solver pronto` | `Solver error`
- Combo `Tipo receita`: `Normal` | `Expert` | `Collectible` | `Splendorous` | `Specialist` | `Cosmic`
- Checkbox `Cosmic button visível` (★ na titlebar)

**Mock data:** CharacterStats (Craftsmanship 4000, Control 3900, CP 600, Lv 100), receita mockada com nome/ícone placeholder, lista de 8 ações de craft como `FontAwesomeIcon` squares.

**Solver rodando:** ProgressBar animada simulada com `_progress` float incrementado no Draw.
**Solver pronto:** lista de ações preenchida, stats de HQ% e success rate.

---

### 2. SynthHelperStory

**Controles:**
- Combo `Estado`: `Calculando` | `Sugestão pronta` | `Collapsed`
- Combo `Condição`: `Normal` | `Good` | `Excellent` | `Poor`
- Checkbox `Cosmic button visível`

**Mock data:** barras de Progress/Quality/Durability/CP via `DrawBarRow`, ação sugerida como badge com FontAwesome icon.

---

### 3. RecipeNoteStory

**Controles:**
- Combo `Estado`: `Sem receita` | `Macro pronto` | `Carregando macro`
- Combo `Tipo`: `Normal` | `Expert` | `Collectible` | `Splendorous` | `Specialist` | `Cosmic`
- Combo `CraftableStatus`: `OK` | `WrongClassJob` | `CraftsmanshipTooLow` | `SpecialistRequired`

**Mock data:** nome de receita mockado, badges de tipo via `DrawBadge`, barras de stat, lista mockada de ações.

---

### 4. CosmicTrackerStory

**Controles:**
- Combo `Estado`: `Sem dados` | `Com dados`
- Checkbox `Modo minimizado`
- Checkbox `Ocultar concluídos`

**Mock data (com dados):** 7 tipos (Type I–VII) com valores mockados usando `DrawResearchTypeRow` / `DrawResearchTypeRowMinimized`. Type I completo (100/100), Type II ativo (42/60/100), Type III–V ativos, Type VI–VII locked.

---

### 5. MacroClipboardStory

**Controles:**
- Combo `Quantidade`: `1 macro` | `3 macros`

**Mock data:** strings de macro `/ac "Basic Synthesis" <wait.3>` etc. dentro de `ImRaii2.GroupPanel`.

---

### 6. MacroListStory

**Controles:**
- Combo `Estado`: `Vazia` | `Com macros` | `Busca ativa` | `Busca sem resultado`

**Mock data:** 6 macros com nome, classe, stats de HQ%. Empty state via `DrawEmptyState(FontAwesomeIcon.Book)`.

---

### 7. SettingsStory

**Controles:**
- Combo `Aba`: `General` | `MacroEditor` | `RecipeNote` | `SynthHelper` | `Solver` | `About` | `Experimental`

**Mock data:** cada aba renderiza um painel mockado com os controles principais (checkboxes, sliders, dropdowns) sem lógica — valores hardcoded. Aba Experimental inclui banner de aviso via `DrawEmptyState` com ícone `ExclamationTriangle`.

---

### 8. CraftingListWindowStory

**Controles:**
- Combo `Estado`: `Vazia` | `Com listas` | `Busca ativa` | `Confirmação de delete`
- Combo `Sort`: `MostRecent` | `NameAZ` | `PercentComplete`

**Mock data:** 5 listas com nome, % completo, data. Empty state via `DrawEmptyState(FontAwesomeIcon.List)`. Confirmação de delete: botão "Confirmar remoção" inline com cor danger.

---

### 9. CraftingListAddWindowStory

**Controles:**
- Combo `Estado`: `Inicial` | `Com resultados` | `Sem resultados`

**Mock data:** search input mockado, 6 resultados com nome de item, ícone placeholder, quantidade.

---

### 10. CraftingListDetailWindowStory

**Controles:**
- Combo `Estado`: `Carregando` | `Com ingredientes` | `Coleta concluída`

**Mock data:** lista de ingredientes com nome, quantidade necessária vs. inventário, checkbox de coletado. Estado "carregando" via spinner/ProgressBar indeterminate.

---

### 11. CraftingListMergeWindowStory

**Controles:**
- Combo `Estado`: `Seleção de listas` | `Confirmando merge`

**Mock data:** 3 listas com checkbox de seleção. Confirmação com botão Primary "Merge".

---

### 12. FeatureHubWindowStory

**Controles:**
- Combo `Estado`: `Botão` | `Popup aberto`
- Checkbox `Lista de Coleta desabilitada`

**Mock data:** `IconButtonSquare(FontAwesomeIcon.Boxes)`. Estado popup: `BeginChild` simulando o popup com os itens do menu.

---

## Skill `/update-studio`

**Arquivo:** `.claude/commands/update-studio.md`

### Processo (4 passos)

**Passo 1 — Inventário**
Ler todos os `Craftimizer/Windows/*.cs` e todos os `Craftimizer.UIStudio/Stories/Pages/*.cs`.
Montar:
- `windowSet`: nomes de classes `Window` encontradas em `Windows/`
- `storySet`: nomes derivados de `Name =>` em cada story de Pages

**Passo 2 — Detectar gaps**

| Situação | Ação |
|---|---|
| Janela sem story correspondente | Criar `Stories/Pages/<Janela>Story.cs` |
| Story sem janela correspondente | Avisar usuário (não deletar automaticamente) |
| Janela com campos/estados novos vs. story | Propor atualização direcionada |

**Regra de correspondência:** `MacroEditor` (janela) ↔ `MacroEditorStory` (story, campo `Name`).

**Passo 3 — Aplicar mudanças**
- Criar/editar arquivos de story
- Adicionar novas stories em `Program.cs` (linha de registro)
- Manter stories existentes intactas quando não há divergência detectada

**Passo 4 — Reportar**
```
UIStudio atualizado

+ Criadas:    <arquivo>.cs
~ Atualizadas: <arquivo>.cs  (<motivo>)
⚠ Sem janela: <arquivo>.cs  — confirme se deve ser removida
  Nenhuma mudança necessária.
```

### O que a skill NÃO faz
- Não reescreve stories do zero sem necessidade
- Não deleta stories (só avisa)
- Não infere lógica de render complexa — descreve o gap e orienta o agente a implementar

---

## Registro em `Program.cs`

Após as 6 Templates existentes, adicionar bloco Pages:

```csharp
// Pages
new MacroEditorStory(),
new SynthHelperStory(),
new RecipeNoteStory(),
new CosmicTrackerStory(),
new MacroClipboardStory(),
new MacroListStory(),
new SettingsStory(),
new CraftingListWindowStory(),
new CraftingListAddWindowStory(),
new CraftingListDetailWindowStory(),
new CraftingListMergeWindowStory(),
new FeatureHubWindowStory(),
```

---

## Critérios de Aceite

- [ ] 12 arquivos criados em `Craftimizer.UIStudio/Stories/Pages/`
- [ ] Todas as stories aparecem no UIStudio nav sob categoria "Pages"
- [ ] Cada story tem controles de estado funcionais no topo
- [ ] `dotnet build` passa com 0 erros e 0 warnings novos
- [ ] Nenhuma alteração em `Craftimizer.UI` ou `Craftimizer/`
- [ ] Arquivo `.claude/commands/update-studio.md` criado com o processo documentado
- [ ] Stories existentes (Atoms/Molecules/Templates) não foram alteradas

---

## O Que Não Muda

- `IStory` interface — sem alteração
- Stories existentes — sem alteração
- Código de produção do plugin — sem alteração
- `Craftimizer.UI` — sem alteração
