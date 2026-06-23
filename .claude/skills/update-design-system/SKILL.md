---
name: update-design-system
description: Reescreve mockup/design-system.html a partir das fontes de verdade do código (Colors.cs, Theme.cs, ImGuiUtils*.cs). Usar quando cores, tokens ou componentes de design mudarem e o design system precisar sincronizar.
---

# Update Design System

Cria ou atualiza o design system visual do plugin Artificer em `mockup/design-system.html`,
mantendo-o em sincronia com as fontes de verdade do código (`Colors.cs`, `Theme.cs`, `ImGuiUtils*.cs`).

Sem entrada — lê o estado atual do código e **reescreve o HTML completo** com os valores extraídos.

---

## O que esta skill faz

1. **Lê as fontes de verdade** de cor, espaçamento e componentes
2. **Extrai** todos os tokens, componentes e padrões vigentes
3. **Reescreve `mockup/design-system.html` do zero** usando a Write tool — não edita incrementalmente
4. **Não toca em `mockup/cosmic-tracker.html`** (mockup de screen, não design system)

> **Por que reescrever e não editar?** Edições incrementais acumulam drift silencioso
> (token antigo no `:root`, swatch fantasma, seção sem link na sidebar). Reescrever garante
> que o HTML é 100% derivado do estado atual do código, sem resíduos.

---

## Passo 1 — Ler fontes de verdade

Ler os seguintes arquivos **antes de qualquer edição**:

```
Artificer.UI/Colors.cs               ← todos os tokens de cor (Vector4)
Artificer.UI/Theme.cs                ← backgrounds, borders, Push/Pop de estilo
Artificer.UI/ImGuiUtils.cs           ← GroupPanel, Badge, helpers de layout
Artificer.UI/ImGuiUtils.Cosmic.cs    ← componentes Cosmic (DrawResearchTypeRow, etc.)
Artificer.UI/ImGuiUtils.Charts.cs    ← DrawStatArc, DrawBarRow
Artificer.UI/ProgressBarComponent.cs ← modos Horizontal/Arc/Compact/Stacked
Artificer/Windows/MacroEditor.cs     ← padrões de layout, Group Panels em uso
Artificer/Windows/CosmicTracker.cs   ← componentes Cosmic Tracker em uso
Artificer/Windows/SynthHelper.cs     ← padrões do overlay de síntese
```

> Nota: `Artificer/Utils/UI/` contém partial classes e extensões plugin-específicas
> (ex: `ProgressBarComponent.Solver.cs`, `DynamicBars.cs`, `IFontHandleExtensions.cs`).
> A fonte de verdade dos tokens de design é `Artificer.UI/`, não `Artificer/Utils/UI/`.

Extrair de `Colors.cs`:
- Todos os campos `public static readonly Vector4` → nome + valores RGBA (0.0–1.0)
- Converter para hex: `R*255, G*255, B*255` → `#RRGGBB`
- Manter agrupamentos por região (stat bars, conditions, cosmic, semantic)

---

## Passo 2 — Comparar com o HTML atual (para montar o diff do relatório)

Ler `mockup/design-system.html` e anotar (apenas para o relatório final):
- Quais tokens CSS existiam no `:root {}`
- Quais seções de componente existiam
- Quais swatches existiam

Esta leitura serve **apenas para gerar o relatório de diff** — não condiciona a reescrita.

---

## Passo 3 — Reescrever `mockup/design-system.html` completo

Usar a **Write tool** para sobrescrever o arquivo inteiro. Não usar Edit.

O HTML gerado deve seguir exatamente a estrutura do arquivo original:
- `<!DOCTYPE html>` → `<head>` com fontes, FontAwesome CDN e `<style>` completo
- Sidebar com nav links para todas as sections
- `<main>` com sections na ordem: Cores → Tipografia → Espaçamento → Superfícies → Componentes → Screens

### O que deve estar correto no HTML gerado

**`:root {}` — tokens CSS:**
```css
/* ─── Backgrounds ─────────── */
--bg-base:     #RRGGBB;   /* BgBase     em Theme.cs */
--bg-surface:  #RRGGBB;   /* BgSurface  em Theme.cs */
--bg-elevated: #RRGGBB;   /* BgElevated em Theme.cs */
--bg-overlay:  #RRGGBB;   /* BgOverlay  em Theme.cs */
--bg-hover:    #RRGGBB;   /* BgHover    em Theme.cs */

/* ─── Border ──────────────── */
--border:        rgba(R, G, B, 0.30);
--border-strong: rgba(R, G, B, 0.50);

/* ─── Stat Colors ─────────── */
--progress:    #RRGGBB;   /* Colors.Progress    */
--quality:     #RRGGBB;   /* Colors.Quality     */
--durability:  #RRGGBB;   /* Colors.Durability  */
--cp:          #RRGGBB;   /* Colors.CP          */
/* ... todas as demais de Colors.cs ... */
```

Para converter `Vector4(R, G, B, A)` → hex: `#${Math.round(R*255).toString(16).padStart(2,'0')}...`
Para alpha < 1.0: usar `rgba(Math.round(R*255), Math.round(G*255), Math.round(B*255), A)`

**Swatches:** cada `public static readonly Vector4` de `Colors.cs` deve ter um swatch
com `swatch-name` = nome exato do campo C#, `swatch-hex` = valor hex convertido, `swatch-token` = `--token-css`.

**Seções de componentes:** uma section para cada método público de `ImGuiUtils*.cs`
(`DrawStateChip`, `DrawConditionIndicator`, `DrawBadgePill`, `DrawBadge`, `ProgressBar`,
`DrawResearchTypeRow`, `DrawResearchTypeRowMinimized`, `DrawResearchTypeBar`, `DrawCosmicStageBadge`,
`IconButtonSquare`, `Hyperlink`, `BeginGroupPanel`, etc.).

**Sidebar nav links:** um `<a class="ds-nav-link">` para cada `<section id="...">` presente no `<main>`.

### Checklist antes de chamar Write

- [ ] Todos os tokens de `Colors.cs` estão no `:root {}`
- [ ] Todos os tokens no `:root {}` têm correspondente em `Colors.cs` (sem fantasmas)
- [ ] Cada cor tem swatch no grupo correto
- [ ] Cada método público de `ImGuiUtils*.cs` tem seção de componente
- [ ] Sidebar tem link para cada section
- [ ] Valores hex batem com os `Vector4` lidos do C# (calcular, não copiar da versão anterior)
- [ ] Cores com alpha < 1.0 usam `rgba(...)` no CSS, não hex

---

## Passo 4 — Reportar ao usuário

Ao final, mostrar um resumo:

```
Design system atualizado em mockup/design-system.html

Mudanças:
  + CosmicNewColor adicionada (#XXYYZZ)
  ~ Progress: #52E4A0 → #52E5A0 (corrigido)
  - OldColor removida

Nenhuma nova seção de componente detectada.
```

Se nenhuma mudança foi necessária:
```
Design system já em sincronia com Colors.cs. Nenhuma alteração feita.
```

---

## Referências

- Fontes de verdade: `Artificer.UI/Colors.cs`, `Artificer.UI/Theme.cs`, `Artificer.UI/ImGuiUtils*.cs`
- Output: `mockup/design-system.html`
- Documentação do DS: `docs/design-system/` (referência para agentes)
- Mockups de telas: `mockup/` (não modificar nesta skill)
