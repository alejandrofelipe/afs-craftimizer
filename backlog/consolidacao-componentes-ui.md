# Backlog — Consolidação de Componentes UI (Refactor)

**Criado:** 2026-06-10
**Status:** 📝 Rascunho
**Tipo:** Refactor / Qualidade de Código
**Estimativa total:** 2–3h
**Escopo:** `Artificer.UI/` — sem alterações de comportamento ou API pública

---

## Resumo Executivo

Auditoria identificou 6 padrões de código similar ou duplicado em `Artificer.UI/`. Nenhum representa bug — são oportunidades de DRY que reduzem LOC, melhoram legibilidade e facilitam futuras manutenções. Todas as mudanças são internas (não afetam callers externos).

---

## Achados

### F1 — `DrawResearchTypeRow` e `DrawResearchTypeRowMinimized` fazem a mesma coisa com variantes leves

**Arquivos:** `Artificer.UI/ImGuiUtils.Cosmic.cs`

As duas funções (linhas ~22–156 e ~162–207) calculam `fillFraction` e `upgradeFraction` com código idêntico. A diferença real é o layout (full vs compacto com tooltip). O switch de cor `ResearchTypeState → Colors.Cosmic*` é duplicado manualmente nas duas.

**Solução proposta:**
1. Extrair `GetResearchTypeColors(ResearchTypeState state)` como helper privado
2. Criar enum `ResearchTypeRowMode { Full, Minimized }` e mesclar os dois métodos em um único `DrawResearchTypeRow(..., ResearchTypeRowMode mode = Full)`
3. Manter a assinatura original como overload de compatibilidade se necessário

**Impacto:** −~50 LOC, lógica de cor centralizada num único lugar

---

### F2 — Switch `ResearchTypeState → cor` copiado 2× no mesmo arquivo

**Arquivo:** `Artificer.UI/ImGuiUtils.Cosmic.cs` (linhas ~27–33 e ~169–181)

O mesmo switch `state switch { Active => CosmicActive, Complete => ..., Maxed => ..., _ => ... }` aparece duas vezes no mesmo arquivo, com leve variação nos campos retornados (single color vs tuple).

**Solução proposta:**
```csharp
private static (Vector4 Label, Vector4 Num, Vector4 Upgrade) GetResearchTypeColors(ResearchTypeState state) => state switch
{
    ResearchTypeState.Active   => (Colors.CosmicActive,   Colors.CosmicActive   with { W = 0.8f }, Colors.CosmicUpgrade),
    ResearchTypeState.Complete => (Colors.CosmicComplete, Colors.CosmicComplete with { W = 0.8f }, Colors.CosmicComplete),
    ResearchTypeState.Maxed    => (Colors.CosmicMaxed,    Colors.CosmicMaxed    with { W = 0.8f }, Colors.CosmicMaxed),
    _                          => (Colors.CosmicLocked,   Colors.CosmicLocked   with { W = 0.5f }, Colors.CosmicLocked),
};
```

**Impacto:** −~12 LOC, ponto único de verdade para as cores do CosmicTracker

---

### F3 — `AlignRight()` existe mas não é usada em `ImGuiUtils.Cosmic.cs`

**Arquivos:** `Artificer.UI/ImGuiUtils.cs` (linha ~271), `Artificer.UI/ImGuiUtils.Cosmic.cs` (linhas ~67, 86, 116, 125, 140)

`ImGuiUtils.AlignRight(width, containerWidth)` já existe como helper público. O arquivo Cosmic repete `ImGui.SameLine(barWidth - textWidth)` manualmente 5× em vez de chamar o helper.

**Solução proposta:**
Substituir as 5 ocorrências por `AlignRight(textWidth, barWidth)`.

**Impacto:** −5 linhas inline, consistência com o resto do codebase

---

### F4 — `Tooltip()` e `TooltipWrapped()` diferem apenas por um parâmetro de wrap

**Arquivo:** `Artificer.UI/ImGuiUtils.cs` (linhas ~217–230)

```csharp
// atual: dois métodos separados
public static void Tooltip(string text) { ... }
public static void TooltipWrapped(string text) { ... using var _wrap = ImRaii2.TextWrapPos(...); }
```

**Solução proposta:**
```csharp
public static void Tooltip(string text, bool wrap = false)
{
    using var _font    = ImRaii.PushFont(UiServices.Current.DefaultFont);
    using var _tooltip = ImRaii.Tooltip();
    if (wrap)
        using var _wrap = ImRaii2.TextWrapPos(450f * UiServices.Current.GlobalScale);
    ImGui.TextUnformatted(text);
}
```
Manter `TooltipWrapped` como alias com `[Obsolete]` por uma versão antes de deletar.

**Impacto:** −~8 LOC, API mais simples para callers futuros

---

### F5 — Arc caps em `DrawStatArc` repetem a mesma matemática 2×

**Arquivo:** `Artificer.UI/ImGuiUtils.Charts.cs` (linhas ~30–31 e ~38–39)

Track caps (cor de fundo) e fill caps (cor preenchida) usam o mesmo cálculo trigonométrico:
```csharp
center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius)
```
— quatro vezes seguidas, variando apenas `angle` e `color`.

**Solução proposta:**
```csharp
private static void DrawArcCaps(ImDrawListPtr drawList, Vector2 center, float radius,
    float startAngle, float endAngle, float capR, uint color)
{
    static Vector2 Pt(float a, float r, Vector2 c) =>
        c + new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r);

    drawList.AddCircleFilled(Pt(startAngle, radius, center), capR, color);
    drawList.AddCircleFilled(Pt(endAngle,   radius, center), capR, color);
}
```

**Impacto:** −~6 LOC, matemática com nome explícito

---

### F6 — `IFontHandleExtensions`: `CalcTextSize` e `Text` sem callers

**Arquivo:** `Artificer/Utils/UI/IFontHandleExtensions.cs`

`GetFontSize()` — 1 caller em `Settings.About.cs`.
`CalcTextSize()` — 0 callers.
`Text()` — 0 callers.

**Solução proposta:**
Deletar `CalcTextSize` e `Text`. Avaliar se `GetFontSize` pode ser inlined no único caller — se sim, deletar o arquivo inteiro.

**Impacto:** −~20 LOC, sem dead code

---

## Prioridade Sugerida de Implementação

| # | Achado | Esforço | Impacto |
|---|--------|---------|---------|
| 1 | F2 — Extrair `GetResearchTypeColors()` | 15 min | Alto |
| 2 | F3 — Usar `AlignRight()` em Cosmic.cs | 10 min | Médio |
| 3 | F6 — Deletar métodos mortos em `IFontHandleExtensions` | 10 min | Médio |
| 4 | F4 — Unificar `Tooltip`/`TooltipWrapped` | 20 min | Médio |
| 5 | F5 — Extrair `DrawArcCaps` helper | 15 min | Baixo |
| 6 | F1 — Mesclar `DrawResearchTypeRow` variants | 45 min | Alto (risco médio) |

---

## Critérios de Aceite

- [ ] 0 warnings de compilação após cada mudança
- [ ] Nenhuma alteração de comportamento visual (UIStudio deve renderizar igual antes/depois)
- [ ] Callers externos não são quebrados (nenhuma mudança de assinatura pública sem overload de compatibilidade)
- [ ] Build `dotnet build Artificer/Artificer.csproj -c Release` passa limpo

---

## O Que Não Muda

- `DrawMacroStatArcs`, `ViolinPlot`, `DrawConditionIndicator` — dependências de domínio, ficam no plugin
- `ImRaii2` no plugin — wrapper de ImPlot, Dalamud-only
- `SolverProgressBar` — adapter semântico, intencional
- `ProgressBarComponent` modos (Horizontal/Arc/Compact/Stacked) — já parametrizados via `DisplayMode` enum

---

## Plugins Externos

Nenhum.
