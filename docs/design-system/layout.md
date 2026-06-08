# Layout — Design System

Fundação visual: superfícies, espaçamento, raios e anatomia das janelas.

Fonte de verdade: `Craftimizer/Utils/UI/Theme.cs`

---

## Superfícies (backgrounds)

Hierarquia de 5 camadas em dark mode. Cada janela escolhe qual nível de profundidade usar.

| Constante em Theme.cs | Hex | ImGuiCol aplicado | Uso |
|---|---|---|---|
| `BgBase` | `#060810` | `ScrollbarBg` | fundo mais escuro; scrollbar track |
| `BgSurface` | `#0D1120` | `WindowBg`, `TitleBg`, `ButtonActive` | fundo de janela |
| `BgElevated` | `#141928` | `ChildBg`, `TitleBgActive` | child windows, title bar ativa |
| `BgOverlay` | `#1B2235` | `FrameBg`, `Button`, `Header`, `ScrollbarGrab` | inputs, botões, itens de lista |
| `BgHover` | `#212A40` | `FrameBgHovered`, `ButtonHovered`, `HeaderHovered`, `ScrollbarGrabHovered` | estado hover |

### Regra de composição

```
Janela    →  BgSurface  (#0D1120)
  Title bar → BgElevated (#141928)   (por TitleBgActive)
  Child     → BgElevated (#141928)   (por ChildBg)
    Input   → BgOverlay  (#1B2235)   (por FrameBg)
    Botão   → BgOverlay  (#1B2235)   (por Button)
```

Nunca usar `BgBase` como fundo de janela — ele só existe para o scrollbar track e para
progress bars (o "trilho" da barra).

---

## Border

```csharp
// Theme.cs
private static readonly Vector4 Border = new(0.392f, 0.549f, 0.784f, 0.30f);
// = rgba(100, 140, 200, 0.30)
```

Aplicada a `ImGuiCol.Border`. É o único valor de borda — não criar variantes de cor de borda.
Se precisar de borda mais forte (ex: tooltip), usar `alpha = 0.50f` do mesmo RGB.

---

## Espaçamento (StyleVar)

Aplicados por `Theme.Push()` — vigem em todas as janelas do plugin.

| Variável | Valor | Equivalente |
|---|---|---|
| `WindowPadding` | `(12f, 8f) * GlobalScale` | 12px H / 8px V |
| `FrameRounding` | `4f * GlobalScale` | 4px (inputs, botões) |
| `ChildRounding` | `6f * GlobalScale` | 6px (child windows) |

### Regra de escala

**Todo espaçamento, padding e size deve multiplicar por `ImGuiHelpers.GlobalScale`.**

```csharp
// Correto
var badgePadding = new Vector2(7f, 2f) * ImGuiHelpers.GlobalScale;
var barHeight = 6f * ImGuiHelpers.GlobalScale;

// Errado — quebra com UI scale diferente de 1.0
var barHeight = 6f;
```

---

## Raios (border radius)

| Contexto | Valor | Fonte |
|---|---|---|
| Frames / inputs | `4f * GlobalScale` | `FrameRounding` via Theme |
| Child windows | `6f * GlobalScale` | `ChildRounding` via Theme |
| GroupPanel border | `style.ItemSpacing.X` | calculado em BeginGroupPanel |
| BadgePill | `totalSize.Y / 2f` | calculado em DrawBadgePill |
| ResearchTypeBar | `size.Y * 0.5f` | calculado em DrawResearchTypeBar |

---

## Tema global (Theme.Push / Theme.Pop)

Chamado uma vez por janela no `Draw()`. Todas as cores ImGui vigem no escopo da janela.

```csharp
protected override void Draw()
{
    Theme.Push();
    // ... conteúdo da janela ...
    Theme.Pop();
}
```

**Nunca chamar `Theme.Pop()` sem ter chamado `Theme.Push()`** — o ImGui mantém uma pilha e
um Pop a mais corrompe o estado de estilo de toda a sessão.

---

## Variantes de botão

Além do botão padrão (BgOverlay), dois atalhos:

### Primary Button (azul)

```csharp
Theme.PushPrimaryButton();
if (ImGui.Button("Run Solver"))
    StartSolver();
Theme.PopPrimaryButton();
```

| Estado | Cor |
|---|---|
| Normal | `Colors.ActionBuff @85%` (`#4AB8FF` a 85%) |
| Hovered | `Colors.ActionBuff @100%` |
| Active | `Colors.ActionBuff @70%` |

### Danger Button (vermelho)

```csharp
Theme.PushDangerButton();
if (ImGui.Button("Delete Macro"))
    ConfirmDelete();
Theme.PopDangerButton();
```

| Estado | Cor |
|---|---|
| Normal | `Colors.Bad @70%` (`#FF5C6E` a 70%) |
| Hovered | `Colors.Bad @90%` |
| Active | `Colors.Bad @55%` |

---

## Anatomia de uma janela

```
┌─────────────────────────────────────────────┐  ← BgElevated (TitleBgActive)
│  🔨  Macro Editor          ⚙ ♥ 👁 −         │  ← title bar (padding 5px × 10px)
├─────────────────────────────────────────────┤  ← Border @30%
│                                             │  ← BgSurface (WindowBg)
│  ┌── Craft Parameters ─────────────────┐   │  ← GroupPanel (border = ImGuiCol.Border)
│  │  Recipe Level        690            │   │    label cor = Colors.ActionBuff
│  │  Difficulty          6600           │   │
│  └─────────────────────────────────────┘   │
│                                             │
│  Progress  ████████████░░░  3200 / 3500    │  ← ProgressBar (FrameBg como trilho)
│  Quality   ████████░░░░░░░  8420 / 14040   │
│                                             │
│  [ Run Solver ]  ● Solving…                 │  ← PrimaryButton + DrawStateChip
│                                             │
└─────────────────────────────────────────────┘
```

- **Title bar:** `BgElevated` + `1px border-bottom` + padding `5px × 10px`
- **Corpo:** `BgSurface` + `WindowPadding (12px × 8px)`
- **Ícones da title bar:** `IconButtonSquare` (20×20px default)

---

## Tamanhos de janela

Dimensões definidas em `UIConstants` (verificar o arquivo para valores exatos):

| Janela | Largura | Altura |
|---|---|---|
| MacroEditor | `715px` @ scale 0.8 → `1504px` @ scale 2.0 (responsivo) | auto |
| SynthHelper | `UIConstants.SynthHelperWidth` (fixo) | auto |
| MacroList | mín 450px | mín 400px |
| CosmicTracker | auto-resize | auto |

---

## Tables (ImGui)

Padrão de tabela de duas colunas para parâmetros:

```csharp
using var table = ImRaii.Table("params", 2,
    ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp);

ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 120 * scale);
ImGui.TableSetupColumn("Value");

ImGui.TableNextRow(); ImGui.TableNextColumn();
using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
    ImGui.TextUnformatted("Recipe Level");
ImGui.TableNextColumn();
ImGui.TextUnformatted("690");
```

- Coluna esquerda (label): `Colors.TextMuted`
- Coluna direita (value): cor padrão ou semântica (`Colors.Good` / `Colors.Bad`)
- Borda interna vertical: `ImGuiTableFlags.BordersInnerV`
