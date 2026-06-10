# Template Stories — Design Spec

**Data:** 2026-06-10  
**Status:** Aprovado  
**Escopo:** `Craftimizer.UIStudio/Stories/` — sem alterações em `Craftimizer.UI` ou no plugin

---

## Objetivo

Adicionar o nível Templates ao Atomic Design documentado no UIStudio. Templates são os esqueletos de layout das janelas do plugin — mostram como Atoms e Molecules se combinam em estruturas de janela completas.

---

## Arquitetura

6 novas story classes, uma por template, todas em `Craftimizer.UIStudio/Stories/`. Nenhuma subpasta necessária — segue o padrão flat existente.

Cada classe implementa `IStory` com:
- `Category => "Templates"`
- `Name => "<nome do template>"`
- `Draw()` com seções via `Section()`, componentes reais, mock data hardcoded

**Sem código novo em `Craftimizer.UI`** — as stories compõem apenas o que já existe.

---

## Stories

### T1 — `TabbedWindowStory.cs`
**Name:** `"Tabbed Window"`  
**Janelas de referência:** MacroEditor, Settings

Conteúdo:
- `BeginTabBar` com 3 abas: "Geral", "Avançado", "Sobre"
- Aba 1: GroupPanel com campos mockados (InputText + CheckBox + SliderFloat)
- Aba 2: ProgressBar horizontal + DrawBarRow com mock data
- Aba 3: TextDisabled com texto de versão mockado
- Seção "Interativo": nenhuma — tabs são o próprio controle

---

### T2 — `ListWindowStory.cs`
**Name:** `"List Window"`  
**Janelas de referência:** CraftingListWindow, CraftingListDetailWindow

Conteúdo:
- Seção "Com itens": InputText search (readonly) + BeginChild scrollável com 6 list items mockados (texto + botão por linha) + footer com dois botões
- Seção "Sem itens (empty state)": mesma estrutura mas BeginChild mostra `DrawEmptyState`

---

### T3 — `StatDashboardStory.cs`
**Name:** `"Stat Dashboard"`  
**Janelas de referência:** SynthHelper, MacroEditor display

Conteúdo:
- Seção "DrawBarRow — arcos": `DrawBarRow` com 4 `BarData` (Progress/Quality/Durability/CP), fração controlada por slider
- Seção "ProgressBar — horizontal": 4 `ProgressBarComponent` horizontais, mesmas frações
- Seção "Interativo": `SliderFloat` para fração (0..1), atualiza ambas as seções acima

---

### T4 — `FloatingOverlayStory.cs`
**Name:** `"Floating Overlay"`  
**Janelas de referência:** CosmicTracker

Conteúdo:
- Seção "Estado Active": `DrawResearchTypeRow` com `ResearchTypeState.Active`, valores mock (current=42, needed=60, max=100)
- Seção "Estado Complete": `DrawResearchTypeRow` com `ResearchTypeState.Complete`
- Seção "Estado Locked": `DrawResearchTypeRow` com `ResearchTypeState.Locked`
- Seção "Modo Minimizado": mesmas 3 linhas acima com `ResearchTypeRowMode.Minimized`

---

### T5 — `DialogStory.cs`
**Name:** `"Dialog"`  
**Janelas de referência:** Confirmações de ação destrutiva, CraftingListAdd

Conteúdo:
- 3 variantes lado a lado (padrão EmptyStateStory com `BeginChild` por variante):
  - **Informativa**: ícone `InfoCircle` + título + subtítulo + botão "OK"
  - **Confirmação**: ícone `QuestionCircle` + título + botão "Cancelar" + botão "Confirmar"
  - **Destrutiva**: ícone `Trash` + título + botão "Cancelar" + botão "Remover" (cor `Colors.Bad`)

---

### T6 — `SinglePanelStory.cs`
**Name:** `"Single Panel"`  
**Janelas de referência:** RecipeNote, MacroClipboard

Conteúdo:
- Seção "Sem footer": `BeginGroupPanel("Título")` + `TextWrapped` com texto mockado
- Seção "Com footer": mesma estrutura + `Separator` + botão de ação alinhado à direita com `AlignRight`

---

## Critérios de Aceite

- [ ] 6 arquivos criados em `Craftimizer.UIStudio/Stories/`
- [ ] Todos aparecem no UIStudio nav sob categoria "Templates"
- [ ] `dotnet build` passa com 0 erros e 0 warnings novos
- [ ] Nenhuma alteração em `Craftimizer.UI` ou `Craftimizer/`

---

## O Que Não Muda

- `IStory` interface — sem alteração
- Stories existentes (Atoms/Molecules) — sem alteração
- Código de produção do plugin — sem alteração
